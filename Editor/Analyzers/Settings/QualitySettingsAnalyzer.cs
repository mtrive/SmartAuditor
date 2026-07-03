using System;
using SmartAuditor.Editor.Core;
using UnityEngine;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class QualitySettingsAnalyzer : SettingsAnalyzer
    {
        internal const string QLT0001 = nameof(QLT0001);
        internal const string QLT0002 = nameof(QLT0002);
        internal const string QLT0003 = nameof(QLT0003);
        internal const string QLT0004 = nameof(QLT0004);
        internal const string QLT0005 = nameof(QLT0005);

        internal const int DefaultAsyncUploadTimeSliceMs = 2;

#if UNITY_6000_0_OR_NEWER
        internal const int DefaultAsyncUploadBufferSizeMb = 16;
#else
        internal const int DefaultAsyncUploadBufferSizeMb = 4;
#endif

        internal static readonly string[] DefaultQualityLevelNames =
        {
            "Very Low", "Low", "Medium", "High", "Very High", "Ultra"
        };

        static readonly Descriptor DefaultSettingsDescriptor = new Descriptor(
            QLT0001,
            "Quality: Default Quality Levels Unmodified",
            Impact.Performance | Impact.BuildSize | Impact.LoadTime,
            "The project uses the default six-level <b>Quality Levels</b> ladder (Very Low / Low / Medium / High / Very High / Ultra). Unused levels still contribute to shader variant generation and obscure which level each platform actually runs.",
            "In <b>Project Settings &gt; Quality</b>, remove the levels the project doesn't ship and rename the remaining ones to match the project's tiering. The active level for each platform is the row with a green tick.");

        static readonly Descriptor UsingLowQualityTexturesDescriptor = new Descriptor(
            QLT0002,
            "Quality: Texture Quality Below Full Res",
            Impact.Performance,
            "A Quality Level has <b>Texture Quality</b> set below <b>Full Res</b>. This option drops higher-resolution mip levels at upload time, but only affects mipmapped textures, leaves install size untouched, and gives no control over the resize algorithm.",
            "Set <b>Texture Quality</b> to <b>Full Res</b> for the affected level in <b>Project Settings &gt; Quality</b>. For lower-spec targets, author lower-resolution texture variants and load them via AssetBundle variants instead.")
        {
            MessageFormat = "Quality: Texture Quality is below Full Res on Quality Level '{0}'"
        };

        static readonly Descriptor DefaultAsyncUploadTimeSliceDescriptor = new Descriptor(
            QLT0003,
            "Quality: Async Upload Time Slice Left at Default",
            Impact.LoadTime,
            "A Quality Level still has <b>Async Upload Time Slice</b> at the Unity default of <b>2ms</b>. The default is conservative and can bottleneck large texture or mesh uploads during scene loads.",
            "If load times during heavy texture or mesh uploads are a concern, increase <b>Async Upload Time Slice</b> in <b>Project Settings &gt; Quality</b> and profile the effect on loading performance.")
        {
            MessageFormat = "Quality: Async Upload Time Slice is still 2 ms on Quality Level '{0}'"
        };

        static readonly Descriptor DefaultAsyncUploadBufferSizeDescriptor = new Descriptor(
            QLT0004,
            "Quality: Async Upload Buffer Size Left at Default",
            Impact.LoadTime,
            "A Quality Level still has <b>Async Upload Buffer Size</b> at the Unity factory default. The default buffer (measured in megabytes) is small and can throttle GPU uploads when loading large textures.",
            "If load times spike when uploading large textures, increase <b>Async Upload Buffer Size</b> in <b>Project Settings &gt; Quality</b>. The value is in megabytes — set it with the project's memory budget in mind.")
        {
            MessageFormat = "Quality: Async Upload Buffer Size is still the factory default on Quality Level '{0}'"
        };

        static readonly Descriptor TextureStreamingDisabledDescriptor = new Descriptor(
            QLT0005,
            "Quality: Texture Streaming Disabled on Quality Level",
            Impact.Memory,
            "<b>Texture Streaming</b> is off on a Quality Level. With streaming disabled, every mip level of every loaded mipmapped texture stays in GPU memory regardless of whether the camera can see the high-resolution data.",
            "Enable <b>Texture Streaming</b> in <b>Project Settings &gt; Quality</b> for the affected level. Unity will then stream mip levels in and out based on camera distance, reducing GPU memory pressure.")
        {
            DocumentationUrl = "https://docs.unity3d.com/Manual/TextureStreaming.html",
            MessageFormat = "Quality: Texture Streaming is off on Quality Level '{0}'"
        };

        static readonly string QualityLocation = "Project/Quality";

        public override void Analyze(SettingsAnalysisContext context)
        {
            if (context.IsDescriptorEnabled(DefaultSettingsDescriptor) &&
                HasFactoryDefaultQualityLadder())
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, DefaultSettingsDescriptor.Id)
                    .WithLocation(new Location(QualityLocation));
                context.ReportIssue(diagnostic);
            }

            if (context.IsDescriptorEnabled(UsingLowQualityTexturesDescriptor))
            {
                var names = QualitySettings.names;
                ForEachQualityLevel(i =>
                {
#if UNITY_2022_2_OR_NEWER
                    if (QualitySettings.globalTextureMipmapLimit <= 0)
                        return;
#else
                    if (QualitySettings.masterTextureLimit <= 0)
                        return;
#endif

                    var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, UsingLowQualityTexturesDescriptor.Id, names[i])
                        .WithEvidence("QualityLevelIndex", i)
                        .WithLocation(new Location(QualityLocation));
                    context.ReportIssue(diagnostic);
                });
            }

            if (context.IsDescriptorEnabled(DefaultAsyncUploadTimeSliceDescriptor))
            {
                var names = QualitySettings.names;
                ForEachQualityLevel(i =>
                {
                    if (QualitySettings.asyncUploadTimeSlice != DefaultAsyncUploadTimeSliceMs)
                        return;

                    var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, DefaultAsyncUploadTimeSliceDescriptor.Id, names[i])
                        .WithEvidence("QualityLevelIndex", i)
                        .WithLocation(new Location(QualityLocation));
                    context.ReportIssue(diagnostic);
                });
            }

            if (context.IsDescriptorEnabled(DefaultAsyncUploadBufferSizeDescriptor))
            {
                var names = QualitySettings.names;
                ForEachQualityLevel(i =>
                {
                    if (QualitySettings.asyncUploadBufferSize != DefaultAsyncUploadBufferSizeMb)
                        return;

                    var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, DefaultAsyncUploadBufferSizeDescriptor.Id, names[i])
                        .WithEvidence("QualityLevelIndex", i)
                        .WithLocation(new Location(QualityLocation));
                    context.ReportIssue(diagnostic);
                });
            }

            if (context.IsDescriptorEnabled(TextureStreamingDisabledDescriptor))
            {
                var names = QualitySettings.names;
                ForEachQualityLevel(i =>
                {
                    if (QualitySettings.streamingMipmapsActive)
                        return;

                    var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, TextureStreamingDisabledDescriptor.Id, names[i])
                        .WithEvidence("QualityLevelIndex", i)
                        .WithLocation(new Location(QualityLocation));
                    context.ReportIssue(diagnostic);
                });
            }
        }

        internal static bool HasFactoryDefaultQualityLadder()
        {
            if (QualitySettings.count != DefaultQualityLevelNames.Length)
                return false;

            var names = QualitySettings.names;
            for (var i = 0; i < DefaultQualityLevelNames.Length; i++)
            {
                if (!string.Equals(names[i], DefaultQualityLevelNames[i], StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        internal static bool IsTextureStreamingEnabledOnAnyQualityLevel()
        {
            var enabled = false;
            ForEachQualityLevel(_ =>
            {
                if (QualitySettings.streamingMipmapsActive)
                    enabled = true;
            });
            return enabled;
        }

        internal static void ForEachQualityLevel(Action<int> action)
        {
            var initialQualityLevel = QualitySettings.GetQualityLevel();
            try
            {
                for (var i = 0; i < QualitySettings.names.Length; ++i)
                {
                    QualitySettings.SetQualityLevel(i, applyExpensiveChanges: false);
                    action(i);
                }
            }
            finally
            {
                QualitySettings.SetQualityLevel(initialQualityLevel, applyExpensiveChanges: false);
            }
        }
    }
}
