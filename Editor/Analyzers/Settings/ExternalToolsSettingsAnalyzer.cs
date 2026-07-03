using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Utils;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class ExternalToolsSettingsAnalyzer : SettingsAnalyzer
    {
        internal const string EDT0003 = nameof(EDT0003);

        internal const string EnabledOptionsEvidenceKey = "Enabled options";

        static readonly string ExternalToolsLocation = "Preferences/External Tools";

        static readonly Descriptor OptionalPackageProjectGenerationDescriptor = new Descriptor(
            EDT0003,
            "Editor: Optional Package .csproj Generation Enabled",
            Impact.EditorWorkflow,
            "External Tools is generating <b>.csproj</b> files for one or more read-only package sources (registry, git, built-in, and similar). Unity synchronizes those projects on domain reload and script changes, which adds work to every iteration cycle. Most developers only need <b>Embedded packages</b> and <b>Local packages</b> in the IDE solution for day-to-day development.",
            "In <b>Edit &gt; Preferences &gt; External Tools</b>, disable <b>Registry packages</b>, <b>Git packages</b>, <b>Built-in packages</b>, <b>Local tarball</b>, and <b>Packages from unknown sources</b> unless you are actively debugging or patching that package source. Click <b>Regenerate project files</b> once after changing the setting."
        )
        {
            MessageFormat = "Editor: Generating .csproj files for {0}"
        };

        public override void Analyze(SettingsAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(OptionalPackageProjectGenerationDescriptor))
                return;

            if (!ProjectGenerationPrefs.TryGetEnabledOptionalPackageFlags(out var enabledLabels))
                return;

            foreach (var label in enabledLabels)
            {
                var diagnostic = Diagnostic.Create(
                        AnalysisCategory.ProjectSetting,
                        OptionalPackageProjectGenerationDescriptor.Id,
                        label)
                    .WithLocation(new Location(ExternalToolsLocation))
                    .WithEvidence(EnabledOptionsEvidenceKey, label);

                context.ReportIssue(diagnostic);
            }
        }
    }
}
