using System.Collections.Generic;
using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Utils;
using UnityEditor;
using UnityEngine.U2D;

namespace SmartAuditor.Editor.Analyzers
{
    internal sealed class SpriteAtlasUtilizationAnalyzer : SpriteAtlasAnalyzer
    {
        internal const string SPR0001 = nameof(SPR0001);

        internal static readonly Descriptor PoorUtilizationDescriptor = new Descriptor(
            SPR0001,
            "Sprite Atlas: High Empty-Space Ratio",
            Impact.Memory,
            "The packed Sprite Atlas has more transparent pixels than the configured threshold. Empty space costs the same atlas memory and bandwidth as visible sprite content.",
            "Repack the atlas more tightly by reducing <b>Padding</b>, enabling <b>Allow Rotation</b>, or splitting unrelated sprites into separate atlases."
        )
        {
            IsEnabledByDefault = false,
            MessageFormat = "Sprite Atlas '{0}' has too much empty space ({1})"
        };

        [DiagnosticParameter("SpriteAtlasEmptySpaceLimit", 50)]
        int m_EmptySpaceLimit;

        public override void Analyze(SpriteAtlasAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(PoorUtilizationDescriptor, context.AssetPath))
                return;

            var spriteAtlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(context.AssetPath);
            if (spriteAtlas == null)
                return;

            var emptySpace = TextureUtils.GetEmptySpacePercentage(spriteAtlas);
            if (emptySpace is { } percent && percent > m_EmptySpaceLimit)
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.AssetIssue,
                    PoorUtilizationDescriptor.Id, spriteAtlas.name, Formatting.FormatPercentage(percent / 100.0f))
                    .WithLocation(new Location(context.AssetPath))
                    .WithEvidence("EmptySpacePercent", percent)
                    .WithEvidence("EmptySpaceLimit", m_EmptySpaceLimit);
                context.ReportIssue(diagnostic);
            }
        }
    }
}
