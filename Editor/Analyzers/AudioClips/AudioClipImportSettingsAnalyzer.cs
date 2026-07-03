using System.Collections.Generic;
using SmartAuditor.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class AudioClipImportSettingsAnalyzer : AudioClipAnalyzer
    {
        internal const string ACL0000 = nameof(ACL0000);    // Long AudioClips which aren't set to streaming
        internal const string ACL0001 = nameof(ACL0001);    // Very small ACs (uncompressed size <200KB) that ARE set to streaming. These should probably be Decompress on Load
        internal const string ACL0002 = nameof(ACL0002);    // Stereo clips not forced to Mono on mobile platforms
        internal const string ACL0003 = nameof(ACL0003);    // Stereo clips not forced to Mono if they're not streaming audio (only non-diagetic music should be stereo, really)
        internal const string ACL0004 = nameof(ACL0004);    // Decompress on Load used with long clips
        internal const string ACL0005 = nameof(ACL0005);    // Compressed In Memory used with compression formats that are not trivial to decompress (e.g. everything other than PCM or ADPCM)
        internal const string ACL0006 = nameof(ACL0006);    // Large compressed samples on mobile: Decrease quality or downsample
        internal const string ACL0007 = nameof(ACL0007);    // Bitrates > 48kHz
        internal const string ACL0008 = nameof(ACL0008);    // Preload Audio Data ticked (increases load times and is only needed for audio that must start IMMEDIATELY upon scene load)
        internal const string ACL0009 = nameof(ACL0009);    // If Load In Background isn't enabled on ACs over (TUNEABLE) size/length (if it's not ticked, loading will block the main thread)
        internal const string ACL0010 = nameof(ACL0010);    // If MP3 is used. Vorbis is better
        internal const string ACL0011 = nameof(ACL0011);    // Source assets that aren't .WAV or .AIFF. Other formats (.MP3, .OGG, etc.) are lossy

        internal static readonly Descriptor AudioLongClipDoesNotStreamDescriptor = new Descriptor(
            ACL0000,
            "Audio: Long Clip Not Streaming",
            Impact.Memory,
            "The AudioClip's runtime memory footprint exceeds the 200 KB streaming-buffer size, but its <b>Load Type</b> is not set to <b>Streaming</b>. The whole clip is held in memory at runtime instead of streamed from disk.",
            "Set <b>Load Type</b> to <b>Streaming</b> in the AudioClip Import Settings."
        )
        {
            MessageFormat = "AudioClip '{0}' Load Type is not set to Streaming"
        };

        internal static readonly Descriptor AudioShortClipStreamsDescriptor = new Descriptor(
            ACL0001,
            "Audio: Short Clip Set to Streaming",
            Impact.Memory,
            "The AudioClip's runtime memory footprint is smaller than the 200 KB streaming-buffer size, but its <b>Load Type</b> is set to <b>Streaming</b>. The streaming buffer itself costs more memory than just holding the clip in RAM would.",
            "Set <b>Load Type</b> to <b>Compressed in Memory</b> or <b>Decompress On Load</b> in the AudioClip Import Settings."
        )
        {
            MessageFormat = "AudioClip '{0}' Load Type is set to Streaming"
        };

        internal static readonly Descriptor AudioStereoClipsOnMobileDescriptor = new Descriptor(
            ACL0002,
            "Audio: Stereo Clip on Mobile",
            Impact.Memory,
            "The AudioClip's source asset is stereo and <b>Force To Mono</b> is off in the Import Settings. On mobile, stereo separation is usually inaudible and the stereo clip costs twice the memory of an equivalent mono clip.",
            "Enable <b>Force To Mono</b> in the AudioClip Import Settings."
        )
        {
            MessageFormat = "AudioClip '{0}' is stereo on a mobile target",
            Platforms = new[] { BuildTarget.Android, BuildTarget.iOS}
        };

        internal static readonly Descriptor AudioStereoClipWhichIsNotStreamingDescriptor = new Descriptor(
            ACL0003,
            "Audio: Stereo Clip Used for Positional Audio",
            Impact.Memory | Impact.Quality,
            "The AudioClip's source asset is stereo, <b>Force To Mono</b> is off, and the <b>Load Type</b> is not <b>Streaming</b> — the typical configuration for diegetic positional sound effects. Positional 3D effects should be mono so the spatializer can place them; stereo source data is wasted and the clip costs twice the memory of a mono equivalent.",
            "Enable <b>Force To Mono</b> in the AudioClip Import Settings unless the clip is non-diegetic music or a stereo ambience."
        )
        {
            MessageFormat = "AudioClip '{0}' is stereo and not streamed"
        };

        internal static readonly Descriptor AudioLongDecompressedClipDescriptor = new Descriptor(
            ACL0004,
            "Audio: Long Clip Decompresses on Load",
            Impact.Memory | Impact.LoadTime,
            "The AudioClip is large and its <b>Load Type</b> is set to <b>Decompress On Load</b>. The clip is decompressed in full at load time, costing both runtime memory (the full decompressed PCM) and load-time CPU.",
            "Set <b>Load Type</b> to <b>Compressed In Memory</b> or <b>Streaming</b> in the AudioClip Import Settings. If per-play decompression CPU is a concern with <b>Compressed In Memory</b>, switch the <b>Compression Format</b> to <b>ADPCM</b>, which is fast to decompress."
        )
        {
            MessageFormat = "AudioClip '{0}' is set to Decompress On Load"
        };

        internal static readonly Descriptor AudioCompressedInMemoryDescriptor = new Descriptor(
            ACL0005,
            "Audio: Compressed In Memory With Expensive Codec",
            Impact.Performance,
            "The AudioClip's <b>Load Type</b> is <b>Compressed In Memory</b> and its <b>Compression Format</b> is neither <b>PCM</b> nor <b>ADPCM</b>. The clip will be decompressed at every playback, paying CPU cost on each play instead of once at load.",
            "Set <b>Load Type</b> to <b>Decompress On Load</b>, or set <b>Compression Format</b> to <b>ADPCM</b> (which is cheap enough to decompress per-playback)."
        )
        {
            MessageFormat = "AudioClip '{0}' is Compressed In Memory with an expensive codec"
        };

        internal static readonly Descriptor AudioLargeCompressedMobileDescriptor = new Descriptor(
            ACL0006,
            "Audio: High-Quality Compressed Clip on Mobile",
            Impact.Memory | Impact.BuildSize,
            "The AudioClip is large, compressed, at 48 kHz or above, and at maximum <b>Quality</b>. Mobile speakers and headphones cannot reproduce the fidelity that those settings preserve, so the file size and memory footprint are higher than the audible result warrants.",
            "Lower the <b>Quality</b> slider until artifacts become audible, then back off one notch. Alternatively, set <b>Sample Rate Setting</b> to <b>Override</b> and pick <b>22050</b> Hz for most sounds (or <b>44100</b> Hz for prominent music or high-frequency content)."
        )
        {
            MessageFormat = "AudioClip '{0}' compression settings could be optimized for mobile",
            Platforms = new[] {BuildTarget.Android, BuildTarget.iOS}
        };

        internal static readonly Descriptor Audio48kHzDescriptor = new Descriptor(
            ACL0007,
            "Audio: Source Sample Rate Above 48 kHz",
            Impact.Memory | Impact.BuildSize | Impact.LoadTime,
            "The AudioClip's source sample rate is above 48 kHz and the importer's <b>Sample Rate Setting</b> does not override it. Above 48 kHz is recording-process territory; for shipped audio the importer caps compressed clips at 48 kHz anyway, and uncompressed clips carry the full sample-rate cost into both source size and runtime memory.",
            "Set <b>Sample Rate Setting</b> to <b>Override</b> and <b>Sample Rate</b> to <b>48000</b> Hz or lower in the AudioClip Import Settings."
        )
        {
            MessageFormat = "AudioClip '{0}' sample rate is above 48 kHz"
        };

        internal static readonly Descriptor AudioPreloadDescriptor = new Descriptor(
            ACL0008,
            "Audio: Preload Audio Data Enabled",
            Impact.LoadTime,
            "The AudioClip's <b>Preload Audio Data</b> checkbox is on. Scene and prefab loads block synchronously until the clip has finished loading, adding to scene startup time.",
            "Disable <b>Preload Audio Data</b> in the AudioClip Import Settings. Keep it on only when the clip must play exactly when the scene begins simulating, or when first-play timing must be frame-precise."
        )
        {
            MessageFormat = "AudioClip '{0}' has Preload Audio Data enabled"
        };

        internal static readonly Descriptor AudioLoadInBackgroundDisabledDescriptor = new Descriptor(
            ACL0009,
            "Audio: Large Clip Loads on Main Thread",
            Impact.Performance | Impact.LoadTime,
            "The AudioClip is large and its <b>Load In Background</b> checkbox is off. The clip is loaded synchronously on the main thread, blocking scene initialization or producing CPU spikes when first accessed.",
            "Enable <b>Load In Background</b> in the AudioClip Import Settings."
        )
        {
            MessageFormat = "AudioClip '{0}' has Load In Background disabled"
        };

        internal static readonly Descriptor AudioMP3Descriptor = new Descriptor(
            ACL0010,
            "Audio: MP3 Compression Format",
            Impact.Quality,
            "The AudioClip's <b>Compression Format</b> is set to <b>MP3</b>. MP3 is an older lossy codec that Vorbis surpasses in both compression efficiency and audible quality at the same bitrate.",
            "Set <b>Compression Format</b> to <b>Vorbis</b> in the AudioClip Import Settings."
        )
        {
            MessageFormat = "AudioClip '{0}' uses MP3 compression"
        };

        internal static readonly Descriptor AudioCompressedSourceAssetDescriptor = new Descriptor(
            ACL0011,
            "Audio: Lossy Source Asset Format",
            Impact.Quality,
            "The AudioClip's source asset is stored in a lossy compressed format (e.g. <b>.mp3</b>, <b>.ogg</b>). The Asset Import process decompresses the source and re-encodes it in the chosen runtime format, compounding compression artifacts.",
            "Replace the source asset with a lossless format such as <b>.wav</b> or <b>.aiff</b>."
        )
        {
            MessageFormat = "AudioClip '{0}' source asset is in a lossy compressed format"
        };

        [DiagnosticParameter("StreamingClipThresholdBytes", 1 * (64000 + (int)(1.6 * 48000 * 2)) + 694)]
        int m_StreamingClipThresholdBytes;

        [DiagnosticParameter("LongDecompressedClipThresholdBytes", 200 * 1024)]
        int m_LongDecompressedClipThresholdBytes;

        [DiagnosticParameter("LongCompressedMobileClipThresholdBytes", 200 * 1024)]
        int m_LongCompressedMobileClipThresholdBytes;

        [DiagnosticParameter("LoadInBackGroundClipSizeThresholdBytes", 200 * 1024)]
        int m_LoadInBackGroundClipSizeThresholdBytes;

        public override void Analyze(AudioClipAnalysisContext context)
        {
            var clipName = context.Name;
            var audioImporter = context.Importer;
            var assetPath = audioImporter.assetPath;
            var audioClip = context.AudioClip;
            var sampleSettings = context.SampleSettings;

            bool isMobileTarget = (context.Options.Platform == BuildTarget.Android ||
                context.Options.Platform == BuildTarget.iOS ||
                context.Options.Platform == BuildTarget.Switch);

            bool isStreaming = sampleSettings.loadType == AudioClipLoadType.Streaming;

            // Size (bytes) of the decompressed PCM data for the clip.
            int decompressedClipSize = audioClip.samples * audioClip.channels * sizeof(float);

            var sourceFileExtension = System.IO.Path.GetExtension(assetPath).ToUpper() ?? string.Empty;
            if (sourceFileExtension.StartsWith("."))
                sourceFileExtension = sourceFileExtension.Substring(1);

#if UNITY_2022_2_OR_NEWER
            var preloadAudioData = sampleSettings.preloadAudioData;
#else
            var preloadAudioData = context.Importer.preloadAudioData;
#endif
            if (context.IsDescriptorEnabled(AudioLongClipDoesNotStreamDescriptor, assetPath) &&
                context.RuntimeSize > m_StreamingClipThresholdBytes && !isStreaming)
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, AudioLongClipDoesNotStreamDescriptor.Id, clipName)
                    .WithLocation(new Location(assetPath))
                    .WithEvidence("RuntimeSize", context.RuntimeSize)
                    .WithEvidence("ThresholdBytes", m_StreamingClipThresholdBytes)
                    .WithEvidence("LoadType", sampleSettings.loadType.ToString());
                context.ReportIssue(diagnostic);
            }

            if (context.IsDescriptorEnabled(AudioShortClipStreamsDescriptor, assetPath) &&
                decompressedClipSize < m_StreamingClipThresholdBytes && isStreaming)
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, AudioShortClipStreamsDescriptor.Id, clipName)
                    .WithLocation(new Location(assetPath))
                    .WithEvidence("DecompressedSize", decompressedClipSize)
                    .WithEvidence("ThresholdBytes", m_StreamingClipThresholdBytes)
                    .WithEvidence("LoadType", sampleSettings.loadType.ToString());
                context.ReportIssue(diagnostic);
            }

            if (audioClip.channels > 1 && context.Importer.forceToMono == false)
            {
                if (isMobileTarget)
                {
                    if (context.IsDescriptorEnabled(AudioStereoClipsOnMobileDescriptor, assetPath))
                    {
                        var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, AudioStereoClipsOnMobileDescriptor.Id, clipName)
                            .WithLocation(new Location(assetPath))
                            .WithEvidence("ChannelCount", audioClip.channels);
                        context.ReportIssue(diagnostic);
                    }
                }
                else if (!isStreaming)
                {
                    if (context.IsDescriptorEnabled(AudioStereoClipWhichIsNotStreamingDescriptor, assetPath))
                    {
                        var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, AudioStereoClipWhichIsNotStreamingDescriptor.Id, clipName)
                            .WithLocation(new Location(assetPath))
                            .WithEvidence("ChannelCount", audioClip.channels)
                            .WithEvidence("LoadType", sampleSettings.loadType.ToString());
                        context.ReportIssue(diagnostic);
                    }
                }
            }

            if (context.IsDescriptorEnabled(AudioLongDecompressedClipDescriptor, assetPath) &&
                context.RuntimeSize > m_LongDecompressedClipThresholdBytes &&
                sampleSettings.loadType == AudioClipLoadType.DecompressOnLoad)
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, AudioLongDecompressedClipDescriptor.Id, clipName)
                    .WithLocation(new Location(assetPath))
                    .WithEvidence("RuntimeSize", context.RuntimeSize)
                    .WithEvidence("ThresholdBytes", m_LongDecompressedClipThresholdBytes)
                    .WithEvidence("LoadType", sampleSettings.loadType.ToString());
                context.ReportIssue(diagnostic);
            }

            if (context.IsDescriptorEnabled(AudioCompressedInMemoryDescriptor, assetPath) &&
                sampleSettings.loadType == AudioClipLoadType.CompressedInMemory &&
                sampleSettings.compressionFormat != AudioCompressionFormat.PCM &&
                sampleSettings.compressionFormat != AudioCompressionFormat.ADPCM)
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, AudioCompressedInMemoryDescriptor.Id, clipName)
                    .WithLocation(new Location(assetPath))
                    .WithEvidence("LoadType", sampleSettings.loadType.ToString())
                    .WithEvidence("CompressionFormat", sampleSettings.compressionFormat.ToString());
                context.ReportIssue(diagnostic);
            }

            if (context.IsDescriptorEnabled(AudioLargeCompressedMobileDescriptor, assetPath) &&
                isMobileTarget &&
                context.ImportedSize > m_LongCompressedMobileClipThresholdBytes &&
                sampleSettings.compressionFormat != AudioCompressionFormat.PCM &&
                sampleSettings.compressionFormat != AudioCompressionFormat.ADPCM &&
                audioClip.frequency >= 48000 &&
                sampleSettings.quality == 1.0f)
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, AudioLargeCompressedMobileDescriptor.Id, clipName)
                    .WithLocation(new Location(assetPath))
                    .WithEvidence("ImportedSize", context.ImportedSize)
                    .WithEvidence("ThresholdBytes", m_LongCompressedMobileClipThresholdBytes)
                    .WithEvidence("CompressionFormat", sampleSettings.compressionFormat.ToString())
                    .WithEvidence("SampleFrequency", audioClip.frequency)
                    .WithEvidence("SampleQuality", sampleSettings.quality);
                context.ReportIssue(diagnostic);
            }

            // Annoyingly, if a clip is compressed, it can't go higher than 48kHz: The frequency gets clamped when it's
            // passed to FMOD and it's not trivial to get the sample rate of the original source audio file. If we find
            // a workaround for that, we should change this. In the meantime, it's useful for uncompressed samples at least.
            if (context.IsDescriptorEnabled(Audio48kHzDescriptor, assetPath) &&
                audioClip.frequency > 48000)
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, Audio48kHzDescriptor.Id, clipName)
                    .WithLocation(new Location(assetPath))
                    .WithEvidence("SampleFrequency", audioClip.frequency);
                context.ReportIssue(diagnostic);
            }

            if (context.IsDescriptorEnabled(AudioPreloadDescriptor, assetPath) &&
                preloadAudioData)
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, AudioPreloadDescriptor.Id, clipName)
                    .WithLocation(new Location(assetPath))
                    .WithEvidence("PreloadAudioData", value: true);
                context.ReportIssue(diagnostic);
            }

            if (context.IsDescriptorEnabled(AudioLoadInBackgroundDisabledDescriptor, assetPath) &&
                !context.Importer.loadInBackground && context.ImportedSize > m_LoadInBackGroundClipSizeThresholdBytes)
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, AudioLoadInBackgroundDisabledDescriptor.Id, clipName)
                    .WithLocation(new Location(assetPath))
                    .WithEvidence("ImportedSize", context.ImportedSize)
                    .WithEvidence("ThresholdBytes", m_LoadInBackGroundClipSizeThresholdBytes)
                    .WithEvidence("LoadInBackground", value: false);
                context.ReportIssue(diagnostic);
            }

            if (context.IsDescriptorEnabled(AudioMP3Descriptor, assetPath) &&
                sampleSettings.compressionFormat == AudioCompressionFormat.MP3)
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, AudioMP3Descriptor.Id, clipName)
                    .WithLocation(new Location(assetPath))
                    .WithEvidence("CompressionFormat", sampleSettings.compressionFormat.ToString());
                context.ReportIssue(diagnostic);
            }

            if (context.IsDescriptorEnabled(AudioCompressedSourceAssetDescriptor, assetPath) &&
                sourceFileExtension != "WAV" &&
                sourceFileExtension != "AIFF" &&
                sourceFileExtension != "AIF")
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, AudioCompressedSourceAssetDescriptor.Id, clipName)
                    .WithLocation(new Location(assetPath))
                    .WithEvidence("SourceExtension", sourceFileExtension);
                context.ReportIssue(diagnostic);
            }
        }
    }
}
