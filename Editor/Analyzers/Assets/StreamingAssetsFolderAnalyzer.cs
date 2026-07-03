using System.IO;
using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Utils;
using UnityEditor;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class StreamingAssetsFolderAnalyzer : ProjectAssetAnalyzer
    {
        internal const string RES0001 = nameof(RES0001);

        static readonly Descriptor StreamingAssetsFolderDescriptor = new Descriptor(
            RES0001,
            "Build: StreamingAssets Folder Above Size Limit",
            Impact.BuildSize,
            "The <b>Assets/StreamingAssets</b> folder contains more data than the configured size limit. Every byte in StreamingAssets ships inside the player build as a raw file, inflating the APK/IPA and limiting the user's ability to install the app on space-constrained devices.",
            "Move files out of <b>Assets/StreamingAssets</b> and load them through <b>AssetBundles</b> or <b>Addressables</b>, so they can be downloaded on demand instead of shipping inside the binary."
        )
        {
            Platforms = new[] { BuildTarget.Android, BuildTarget.iOS},
            MessageFormat = "StreamingAssets folder contains {0} of data",
        };

        [DiagnosticParameter("StreamingAssetsFolderSizeLimit", 50)]
        int m_FolderSizeLimit;

        public override void Finalize(AnalysisContext context, IProgress progress = null)
        {
            if (context.IsDescriptorEnabled(StreamingAssetsFolderDescriptor) &&
                StreamingAssetsFolderDescriptor.IsApplicable(context.Options))
            {
                AnalyzeStreamingAssets(context);
            }
        }

        void AnalyzeStreamingAssets(AnalysisContext context)
        {
            if (!Directory.Exists("Assets/StreamingAssets"))
                return;

            var totalBytes = 0L;
            var files = Directory.GetFiles("Assets/StreamingAssets", "*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                totalBytes += fileInfo.Length;
            }

            var folderSizeLimitMB = m_FolderSizeLimit;

            if (totalBytes <= folderSizeLimitMB * 1024 * 1024)
                return;

            var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue, StreamingAssetsFolderDescriptor.Id,
                    Formatting.FormatSize((ulong)totalBytes))
                .WithEvidence("FolderSizeBytes", totalBytes)
                .WithEvidence("FolderSizeLimitMB", m_FolderSizeLimit)
                .WithLocation(new Location("Assets/StreamingAssets"));
            context.ReportIssue(diagnostic);
        }
    }
}
