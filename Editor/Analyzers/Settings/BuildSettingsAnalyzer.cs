using SmartAuditor.Editor;
using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Utils;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class BuildSettingsAnalyzer : SettingsAnalyzer
    {
        internal const string EDT0004 = nameof(EDT0004);

        internal const string TextureCompressionOverrideEvidenceKey = "Texture compression override";

        static readonly string BuildSettingsLocation = "Build Settings";

        static readonly Descriptor TextureCompressionOverrideDescriptor = new Descriptor(
            EDT0004,
            "Editor: Texture Compression Override Not Set",
            Impact.EditorWorkflow,
            "<b>EditorUserBuildSettings.overrideTextureCompression</b> is set to <b>No Override</b>, so Unity applies each texture's import compression settings during reimport and platform switches. Textures with <b>Crunch Compression</b> enabled can spend a long time compressing on every import, which slows local iteration even though the shipped import settings stay unchanged.",
            "For local development, set <b>EditorUserBuildSettings.overrideTextureCompression</b> to <b>ForceNoCrunchCompression</b> (Unity 6+) or <b>ForceFastCompressor</b> (also disables Crunch on earlier versions), then call <b>AssetDatabase.Refresh()</b>. The override is stored locally in <c>Library/EditorUserBuildSettings.asset</c> and does not modify committed texture import settings."
        )
        {
            MinimumVersion = "2021.3"
        };

        public override void Analyze(SettingsAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(TextureCompressionOverrideDescriptor))
                return;

            if (EditorUserBuildSettings.overrideTextureCompression != OverrideTextureCompression.NoOverride)
                return;

            if (!ProjectUsesCrunchCompression(context.Options))
                return;

            var diagnostic = Diagnostic.Create(
                    AnalysisCategory.ProjectSetting,
                    TextureCompressionOverrideDescriptor.Id)
                .WithLocation(new Location(BuildSettingsLocation))
                .WithEvidence(
                    TextureCompressionOverrideEvidenceKey,
                    EditorUserBuildSettings.overrideTextureCompression.ToString());

            context.ReportIssue(diagnostic);
        }

        static bool ProjectUsesCrunchCompression(AnalysisOptions options)
        {
            var platformString = options.PlatformAsString;
            var assetPaths = AssetPathUtils.GetAssetPathsByFilter($"t:{nameof(Texture)}, a:assets", options);

            foreach (var assetPath in assetPaths)
            {
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                    continue;

                if (TextureImporterUsesCrunch(importer, platformString))
                    return true;
            }

            return false;
        }

        static bool TextureImporterUsesCrunch(TextureImporter importer, string platformString)
        {
            var platformSettings = importer.GetPlatformTextureSettings(platformString);
            if (platformSettings.overridden)
                return platformSettings.crunchedCompression;

            return importer.GetDefaultPlatformTextureSettings().crunchedCompression;
        }
    }
}
