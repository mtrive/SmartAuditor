using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SmartAuditor.Editor.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace SmartAuditor.Editor.Modules
{
    sealed class AudioClipsModule : AnalysisModule<AudioClipAnalyzer>
    {
        internal static readonly InsightSchema k_AudioClipInsightSchema = new InsightSchema(
            new InsightColumn(AudioClipColumns.Length, "Length", PropertyFormat.DurationFixed, ColumnHints.Aggregatable | ColumnHints.TopNMetric),
            new InsightColumn(AudioClipColumns.SourceFileSize, "Source File Size", PropertyFormat.DataSize, ColumnHints.Aggregatable | ColumnHints.TopNMetric),
            new InsightColumn(AudioClipColumns.ImportedFileSize, "Imported File Size", PropertyFormat.DataSize, ColumnHints.Aggregatable | ColumnHints.TopNMetric),
            new InsightColumn(AudioClipColumns.RuntimeSize, "Runtime Size (Estimate)", PropertyFormat.DataSize, ColumnHints.Aggregatable | ColumnHints.TopNMetric),
            new InsightColumn(AudioClipColumns.CompressionRatio, "Compression Ratio", PropertyFormat.Percentage),
            new InsightColumn(AudioClipColumns.CompressionFormat, "Compression Format", PropertyFormat.Text, ColumnHints.Categorical),
            new InsightColumn(AudioClipColumns.SampleRate, "Sample Rate", PropertyFormat.Frequency, ColumnHints.Categorical),
            new InsightColumn(AudioClipColumns.ForceToMono, "Force To Mono", PropertyFormat.Boolean),
            new InsightColumn(AudioClipColumns.LoadInBackground, "Load In Background", PropertyFormat.Boolean),
            new InsightColumn(AudioClipColumns.PreloadAudioData, "Preload Audio Data", PropertyFormat.Boolean),
            new InsightColumn(AudioClipColumns.LoadType, "Load Type", PropertyFormat.Text, ColumnHints.Categorical));

        static readonly IReadOnlyDictionary<AnalysisCategory, InsightSchema> s_CategorySchemas =
            new Dictionary<AnalysisCategory, InsightSchema>
            {
                { AnalysisCategory.AudioClip, k_AudioClipInsightSchema },
            };

        public override IReadOnlyDictionary<AnalysisCategory, InsightSchema> CategorySchemas => s_CategorySchemas;

        public override string Name => "Audio Clips";

        public override IReadOnlyCollection<AnalysisCategory> OwnedCategories => new[]
        {
            AnalysisCategory.AudioClip,
        };

        public override IReadOnlyCollection<AnalysisCategory> AdditionalEmittedCategories =>
            new[] { AnalysisCategory.AssetIssue };

        const int k_StreamingBuffer = 64000;        // The per-instance streaming buffer, which Unity's FMOD implementation defaults to 64000.
        const int k_AudioClipManagedSize = 694;     // The managed AudioClip object size, as reported by Profiler.GetRuntimeMemorySizeLong().

        // Yield to the message loop every N clips so the in-window progress overlay can
        // repaint and the user can hit Cancel. AudioImporter / AssetDatabase are main-thread-
        // only, so we use await Task.Yield rather than offloading to a worker.
        const int k_YieldEveryNClips = 32;

        public override async Task<AnalysisResult> AnalyzeAsync(AnalysisSession session, CancellationToken cancellationToken = default)
        {
            var options = session.Options;
            var progress = session.Progress;
            var analyzers = GetCompatibleAnalyzers(options);
            var assetPaths = AssetPathUtils.GetAssetPathsByFilter($"t:{nameof(AudioClip)}, a:assets", options);

            using var context = new AudioClipAnalysisContext(options, session);

            progress?.Start("Analyzing Audio Clips", string.Empty, assetPaths.Length);

            for (var i = 0; i < assetPaths.Length; i++)
            {
                if (cancellationToken.IsCancellationRequested)
                    return AnalysisResult.Cancelled;
                var assetPath = assetPaths[i];
                progress?.Advance(assetPath);

                var audioImporter = AssetImporter.GetAtPath(assetPath) as AudioImporter;
                if (audioImporter == null)
                {
                    continue;
                }

                var sampleSettings = audioImporter.GetOverrideSampleSettings(options.PlatformAsString);
                var audioClip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
                var clipName = Path.GetFileNameWithoutExtension(assetPath);

                var origSize = (int)GetPropertyValue(audioImporter, "origSize");
                var compSize = (int)GetPropertyValue(audioImporter, "compSize");

                var runtimeSize = Profiler.GetRuntimeMemorySizeLong(audioClip);
                // Profiler.GetRuntimeMemorySizeLong() has a habit of returning 694 bytes - that is, the size of the managed AudioClip object, but not
                // the size of the native footprint of the AudioClip at runtime. So let's try calculating what we think that would be.
                if (runtimeSize == k_AudioClipManagedSize)
                {
                    // The decompression buffer is defined as "400ms of float sample data".
                    // 1 second of audio is (sizeof(float) * audioClip.frequency * audioClip.channels) bytes.
                    // So 400ms is 400 * ((sizeof(float) * audioClip.frequency * audioClip.channels) / 1000).
                    // We can simplify this to the following:
                    int decompressionBufferSize = (int)(1.6 * audioClip.frequency * audioClip.channels);

                    // NOTE: Actual runtime memory footprint at any given moment depends on the number of instances of an AudioClip that are currently playing.
                    // Each instance will need its own decompression buffer (if Streaming or CompressedInMemory) and streaming buffer (if Streaming)
                    // In static analysis, we can't calculate the maximum number of instances of a clip that could play simultaneously at runtime, so let's estimate it at its most likely value: 1.
                    switch (audioClip.loadType)
                    {
                        case AudioClipLoadType.DecompressOnLoad:
                            // Since the decompression buffer is only needed during loading, let's ignore it. Just calculate the size of the decompressed PCM data.
                            runtimeSize += sizeof(float) * audioClip.samples * audioClip.channels;
                            break;
                        case AudioClipLoadType.CompressedInMemory:
                            runtimeSize += compSize + decompressionBufferSize;
                            break;
                        case AudioClipLoadType.Streaming:
                            runtimeSize += k_StreamingBuffer + decompressionBufferSize;
                            break;
                    }
                }

                context.Name = clipName;
                context.Importer = audioImporter;
                context.AudioClip = audioClip;
                context.SampleSettings = sampleSettings;
                context.ImportedSize = compSize;
                context.RuntimeSize = runtimeSize;

                var table = context.GetInsightTable(AnalysisCategory.AudioClip, k_AudioClipInsightSchema);
                table.AddRow(assetPath, new Dictionary<string, object>
                {
                    [AudioClipColumns.Length] = audioClip.length,
                    [AudioClipColumns.SourceFileSize] = origSize,
                    [AudioClipColumns.ImportedFileSize] = compSize,
                    [AudioClipColumns.RuntimeSize] = runtimeSize,
                    [AudioClipColumns.CompressionRatio] = (float)compSize / (float)origSize,
                    [AudioClipColumns.CompressionFormat] = sampleSettings.compressionFormat.ToString(),
                    [AudioClipColumns.SampleRate] = audioClip.frequency,
                    [AudioClipColumns.ForceToMono] = context.Importer.forceToMono,
                    [AudioClipColumns.LoadInBackground] = context.Importer.loadInBackground,
#if UNITY_2022_2_OR_NEWER
                    [AudioClipColumns.PreloadAudioData] = sampleSettings.preloadAudioData,
#else
                    [AudioClipColumns.PreloadAudioData] = context.Importer.preloadAudioData,
#endif
                    [AudioClipColumns.LoadType] = sampleSettings.loadType.ToString(),
                });

                foreach (var analyzer in analyzers)
                {
                    analyzer.Analyze(context);
                }

                if ((i + 1) % k_YieldEveryNClips == 0)
                    await Task.Yield();
            }

            FinalizeAnalyzers(session, analyzers);
            return AnalysisResult.Success;
        }

        static object GetPropertyValue(AssetImporter assetImporter, string propertyName)
        {
            var objType = assetImporter.GetType();
            var propInfo = objType.GetProperty(propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (propInfo == null)
                throw new ArgumentException(
                    $"Couldn't find property {propertyName} in type {objType.FullName}",
                    nameof(propertyName));

            return propInfo.GetValue(assetImporter, null);
        }
    }
}
