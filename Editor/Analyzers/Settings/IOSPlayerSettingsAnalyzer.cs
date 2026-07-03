using SmartAuditor.Editor.Core;
using UnityEditor;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class IOSPlayerSettingsAnalyzer : SettingsAnalyzer
    {
        internal const string PLY0002 = nameof(PLY0002);

        static readonly Descriptor AccelerometerDescriptor = new Descriptor(
            PLY0002,
            "Player (iOS): Accelerometer Polling Enabled",
            Impact.Performance,
            "<b>Accelerometer Frequency</b> in iOS Player Settings is non-zero. Polling the device's accelerometer runs every frame and incurs CPU time even when the project never reads the data.",
            "Set <b>Project Settings &gt; Player &gt; iOS &gt; Other Settings &gt; Accelerometer Frequency</b> to <b>Disabled</b> if the project never reads <b>Input.acceleration</b> or related APIs.")
        {
            Platforms = new[] { BuildTarget.iOS }
        };

        public override void Analyze(SettingsAnalysisContext context)
        {
            if (context.IsDescriptorEnabled(AccelerometerDescriptor) &&
                AccelerometerDescriptor.IsApplicable(context.Options) &&
                IsAccelerometerEnabled())
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, AccelerometerDescriptor.Id)
                    .WithLocation(new Location("Project/Player"));
                context.ReportIssue(diagnostic);
            }
        }

        internal static bool IsAccelerometerEnabled()
        {
            return PlayerSettings.accelerometerFrequency != 0;
        }
    }
}
