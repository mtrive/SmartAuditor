using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Utils;
using UnityEditor;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class ConfigurationSettingsAnalyzer : SettingsAnalyzer
    {
        internal const string PLY0034 = nameof(PLY0034);
        internal const string PLY1004 = nameof(PLY1004);
        internal const string PLY1005 = nameof(PLY1005);

        static readonly Descriptor IncrementalGCDescriptor = new Descriptor(
            PLY0034,
            "Player: Incremental GC Disabled",
            Impact.Performance,
            "<b>Use Incremental GC</b> in Player Settings is off. Garbage collection runs as a single stop-the-world pass, producing visible CPU spikes when collection coincides with a heavy frame.",
            "Enable <b>Project Settings &gt; Player &gt; Other Settings &gt; Configuration &gt; Use incremental GC</b>. The collector then runs across multiple frames, smoothing the spike — but it is not a substitute for reducing per-frame managed allocations.")
        {
            MinimumVersion = "2019.1"
        };

        static readonly Descriptor IL2CPPCompilerConfigurationMasterDescriptor = new Descriptor(
            PLY1004,
            "Player: IL2CPP Compiler Configuration is Master",
            Impact.BuildTime,
            "The IL2CPP <b>C++ Compiler Configuration</b> is set to <b>Master</b>. This mode applies the heaviest C++ optimizations and is intended for the final shipping build; it can multiply build times relative to <b>Release</b>.",
            "Set <b>Project Settings &gt; Player &gt; Other Settings &gt; Configuration &gt; C++ Compiler Configuration</b> to <b>Release</b> for development and CI builds; reserve <b>Master</b> for the shipping build.")
        {
            MessageFormat = "Player: C++ Compiler Configuration is 'Master'"
        };

        static readonly Descriptor IL2CPPCompilerConfigurationDebugDescriptor = new Descriptor(
            PLY1005,
            "Player: IL2CPP Compiler Configuration is Debug",
            Impact.Performance,
            "The IL2CPP <b>C++ Compiler Configuration</b> is set to <b>Debug</b>. This mode disables C++ optimizations to support native debuggers and produces runtime CPU performance well below release builds.",
            "Set <b>Project Settings &gt; Player &gt; Other Settings &gt; Configuration &gt; C++ Compiler Configuration</b> to <b>Release</b> unless actively debugging native code.")
        {
            MessageFormat = "Player: C++ Compiler Configuration is 'Debug'"
        };

        public override void Analyze(SettingsAnalysisContext context)
        {
            if (context.IsDescriptorEnabled(IncrementalGCDescriptor) &&
                IncrementalGCDescriptor.IsApplicable(context.Options) &&
                !PlayerSettings.gcIncremental)
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, IncrementalGCDescriptor.Id)
                    .WithLocation(new Location("Project/Player"));
                context.ReportIssue(diagnostic);
            }

            if (context.IsDescriptorEnabled(IL2CPPCompilerConfigurationMasterDescriptor) &&
                CheckIL2CPPCompilerConfiguration(Il2CppCompilerConfiguration.Master, context.Options))
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, IL2CPPCompilerConfigurationMasterDescriptor.Id)
                    .WithLocation(new Location("Project/Player"));
                context.ReportIssue(diagnostic);
            }

            if (context.IsDescriptorEnabled(IL2CPPCompilerConfigurationDebugDescriptor) &&
                CheckIL2CPPCompilerConfiguration(Il2CppCompilerConfiguration.Debug, context.Options))
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, IL2CPPCompilerConfigurationDebugDescriptor.Id)
                    .WithLocation(new Location("Project/Player"));
                context.ReportIssue(diagnostic);
            }
        }

        internal static bool CheckIL2CPPCompilerConfiguration(Il2CppCompilerConfiguration compilerConfiguration, AnalysisOptions options)
        {
            var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(options.Platform);
            if (PlayerSettingsUtil.GetScriptingBackend(buildTargetGroup) != ScriptingImplementation.IL2CPP)
            {
                return false;
            }

            return PlayerSettingsUtil.GetIl2CppCompilerConfiguration(buildTargetGroup) == compilerConfiguration;
        }
    }
}
