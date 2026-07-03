using SmartAuditor.Editor.Core;
using UnityEditor;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class EditorSettingsAnalyzer : SettingsAnalyzer
    {
        internal const string EDT0001 = nameof(EDT0001);
        internal const string EDT0002 = nameof(EDT0002);

        static readonly Descriptor EnterPlayModeOptionsDescriptor = new Descriptor(
            EDT0001,
            "Editor: Enter Play Mode Options Disabled",
            Impact.EditorWorkflow,
            "The <b>Enter Play Mode Options</b> checkbox in Editor Settings is off. Domain Reload cannot be disabled until this option is enabled, so every play-mode entry pays full reload time.",
            "Enable <b>Enter Play Mode Settings &gt; Enter Play Mode Options</b> in Editor Settings, then disable the <b>Reload Domain</b> sub-option. Review the package's <b>Code / Domain Reload</b> diagnostics to fix any code that relied on the reload behavior."
        )
        {
            MaximumVersion = "2023.4"
        };

        static readonly Descriptor DomainReloadDescriptor = new Descriptor(
            EDT0002,
            "Editor: Reload Domain Enabled",
            Impact.EditorWorkflow,
            "The <b>Reload Domain</b> sub-option of <b>Enter Play Mode Settings</b> is on. Every play-mode entry, exit, and code change triggers a full script-state reload, costing seconds of iteration time per cycle.",
            "Disable the <b>Reload Domain</b> checkbox in Editor Settings. Review the package's <b>Code / Domain Reload</b> diagnostics for code that needs adjusting once domain reload is off (static state, lazy initializers, event-subscription patterns)."
        )
        {
            MaximumVersion = "2023.4"
        };

        public override void Analyze(SettingsAnalysisContext context)
        {
            if (context.IsDescriptorEnabled(EnterPlayModeOptionsDescriptor) &&
                EnterPlayModeOptionsDescriptor.IsVersionCompatible() &&
                !EditorSettings.enterPlayModeOptionsEnabled)
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, EnterPlayModeOptionsDescriptor.Id)
                    .WithLocation(new Location("Project/Editor"));
                context.ReportIssue(diagnostic);
            }
            else if (context.IsDescriptorEnabled(DomainReloadDescriptor) &&
                DomainReloadDescriptor.IsVersionCompatible() &&
                (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) != EnterPlayModeOptions.DisableDomainReload)
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, DomainReloadDescriptor.Id)
                    .WithLocation(new Location("Project/Editor"));
                context.ReportIssue(diagnostic);
            }
        }
    }
}
