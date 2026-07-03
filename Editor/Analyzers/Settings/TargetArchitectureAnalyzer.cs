using System;
using System.Collections.Generic;
using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Utils;
using UnityEditor;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class TargetArchitectureAnalyzer : SettingsAnalyzer
    {
        internal const string PLY0003 = nameof(PLY0003);
        internal const string PLY0004 = nameof(PLY0004);

        static readonly Descriptor DescriptorIOS = new Descriptor(
            PLY0003,
            "Player (iOS): Architecture Set to Universal",
            Impact.BuildSize,
            "The iOS Player Settings have <b>Architecture</b> set to <b>Universal</b>. The build compiles for both 32-bit ARMv7 (iPhone 5 / 5c) and 64-bit ARM64 (iPhone 5s and later), doubling build time and shipped binary size.",
            "Set <b>Architecture</b> to <b>ARM64</b> in <b>Project Settings &gt; Player</b> unless the app explicitly targets 32-bit iOS devices.")
        {
            Platforms = new[] { BuildTarget.iOS }
        };

        static readonly Descriptor DescriptorAndroid = new Descriptor(
            PLY0004,
            "Player (Android): Both ARMv7 and ARM64 Targeted",
            Impact.BuildSize,
            "The Android Player Settings' <b>Target Architectures</b> include both <b>ARMv7</b> and <b>ARM64</b>. The build compiles native code twice and ships both slices, increasing build time and APK/AAB size.",
            "Uncheck <b>ARMv7</b> in <b>Project Settings &gt; Player &gt; Other Settings &gt; Target Architectures</b> unless the app explicitly supports 32-bit Android devices.")
        {
            Platforms = new[] { BuildTarget.Android }
        };

        public override void Analyze(SettingsAnalysisContext context)
        {
            // PlayerSettings.GetArchitecture returns an integer value associated with the architecture of a BuildTargetPlatformGroup. 0 - None, 1 - ARM64, 2 - Universal.
            if (context.IsDescriptorEnabled(DescriptorIOS) &&
                PlayerSettingsUtil.GetArchitecture(BuildTargetGroup.iOS) == 2)
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, DescriptorIOS.Id)
                    .WithLocation(new Location("Project/Player"));
                context.ReportIssue(diagnostic);
            }

            if (context.IsDescriptorEnabled(DescriptorAndroid) &&
                (PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARMv7) != 0 &&
                (PlayerSettings.Android.targetArchitectures & AndroidArchitecture.ARM64) != 0)
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, DescriptorAndroid.Id)
                    .WithLocation(new Location("Project/Player"));
                context.ReportIssue(diagnostic);
            }
        }
    }
}
