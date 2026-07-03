using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Utils;
using UnityEngine;

namespace SmartAuditor.Editor.Analyzers
{
    internal sealed class FogShaderStrippingAnalyzer : SettingsAnalyzer
    {
        internal const string GFX0006 = nameof(GFX0006);

        static readonly Descriptor FogModeDescriptor = new Descriptor(
            GFX0006,
            "Graphics: Fog Shader Variants Always Included",
            Impact.BuildSize,
            "The project's Graphics Settings explicitly preserve fog shader variants for a fog mode even when no scene references them. The shader compiler builds and ships those variants in the player, inflating the build's shader payload.",
            "Set <b>Project Settings &gt; Graphics &gt; Fog Modes</b> to <b>Automatic</b>, or uncheck the specific mode (<b>Linear</b> / <b>Exponential</b> / <b>Exponential Squared</b>) if the project doesn't use it.")
        {
            MessageFormat = "Fog Mode '{0}' shader variants are always included in the build"
        };

        public override void Analyze(SettingsAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(FogModeDescriptor))
                return;

            ShaderStripping.GetPreservedFogModes(out var linear, out var exponential, out var exponentialSquared);

            if (linear)
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, FogModeDescriptor.Id, FogMode.Linear)
                    .WithLocation(new Location("Project/Graphics"));
                context.ReportIssue(diagnostic);
            }

            if (exponential)
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, FogModeDescriptor.Id, FogMode.Exponential)
                    .WithLocation(new Location("Project/Graphics"));
                context.ReportIssue(diagnostic);
            }

            if (exponentialSquared)
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, FogModeDescriptor.Id, FogMode.ExponentialSquared)
                    .WithLocation(new Location("Project/Graphics"));
                context.ReportIssue(diagnostic);
            }
        }
    }
}
