using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Utils;
using UnityEditor;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class CodeStrippingAnalyzer : SettingsAnalyzer
    {
        internal const string PLY0009 = nameof(PLY0009);
        internal const string PLY0025 = nameof(PLY0025);
        internal const string PLY0026 = nameof(PLY0026);

        static readonly Descriptor EngineCodeStrippingDescriptor = new Descriptor(
            PLY0009,
            "Player: Engine Code Stripping Disabled",
            Impact.BuildSize,
            "The <b>Strip Engine Code</b> option in Player Settings is off. The build includes UnityEngine subsystems and modules the project never references, inflating the shipped binary.",
            "Enable <b>Strip Engine Code</b> in <b>Project Settings &gt; Player &gt; Other Settings &gt; Optimization</b>.")
        {
            Platforms = new[] { BuildTarget.Android, BuildTarget.iOS, BuildTarget.WebGL }
        };

        static readonly Descriptor AndroidManagedStrippingDescriptor = new Descriptor(
            PLY0025,
            "Player (Android): Managed Code Stripping Too Low",
            Impact.BuildSize,
#if UNITY_2021_2_OR_NEWER
            "The Android Player Settings have <b>Managed Stripping Level</b> set to <b>Disabled</b>, <b>Minimal</b>, or <b>Low</b>. Unused managed code from packages and dependencies ships in the build, inflating the APK/AAB.",
#else
            "The Android Player Settings have <b>Managed Stripping Level</b> set to <b>Disabled</b> or <b>Low</b>. Unused managed code from packages and dependencies ships in the build, inflating the APK/AAB.",
#endif
            "Set <b>Managed Stripping Level</b> to <b>Medium</b> or <b>High</b> in the Android Player Settings.")
        {
            Platforms = new[] { BuildTarget.Android }
        };

        static readonly Descriptor iOSManagedStrippingDescriptor = new Descriptor(
            PLY0026,
            "Player (iOS): Managed Code Stripping Too Low",
            Impact.BuildSize,
            "The iOS Player Settings have <b>Managed Stripping Level</b> set to <b>Disabled</b>, <b>Minimal</b>, or <b>Low</b>. Unused managed code from packages and dependencies ships in the build, inflating the IPA and slowing App Store review.",
            "Set <b>Managed Stripping Level</b> to <b>Medium</b> or <b>High</b> in the iOS Player Settings.")
        {
            Platforms = new[] { BuildTarget.iOS }
        };

        public override void Analyze(SettingsAnalysisContext context)
        {
            if (context.IsDescriptorEnabled(EngineCodeStrippingDescriptor) &&
                EngineCodeStrippingDescriptor.IsApplicable(context.Options) &&
                !PlayerSettings.stripEngineCode)
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, EngineCodeStrippingDescriptor.Id)
                    .WithLocation(new Location("Project/Player"));
                context.ReportIssue(diagnostic);
            }

            if (context.IsDescriptorEnabled(AndroidManagedStrippingDescriptor))
            {
                var value = PlayerSettingsUtil.GetManagedStrippingLevel(BuildTargetGroup.Android);
                if (value == ManagedStrippingLevel.Disabled || value == ManagedStrippingLevel.Low
#if UNITY_2021_2_OR_NEWER
                    || value == ManagedStrippingLevel.Minimal
#endif
                )
                {
                    var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, AndroidManagedStrippingDescriptor.Id)
                        .WithLocation(new Location("Project/Player"));
                    context.ReportIssue(diagnostic);
                }
            }

            if (context.IsDescriptorEnabled(iOSManagedStrippingDescriptor))
            {
                var value = PlayerSettingsUtil.GetManagedStrippingLevel(BuildTargetGroup.iOS);
                if (value == ManagedStrippingLevel.Disabled || value == ManagedStrippingLevel.Low
#if UNITY_2021_2_OR_NEWER
                    || value == ManagedStrippingLevel.Minimal
#endif
                )
                {
                    var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, iOSManagedStrippingDescriptor.Id)
                        .WithLocation(new Location("Project/Player"));
                    context.ReportIssue(diagnostic);
                }
            }
        }
    }
}
