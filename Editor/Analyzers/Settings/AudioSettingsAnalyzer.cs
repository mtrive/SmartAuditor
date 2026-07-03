using SmartAuditor.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class AudioSettingsAnalyzer : SettingsAnalyzer
    {
        internal const string PLY0033 = nameof(PLY0033);

        static readonly Descriptor SpeakerModeDescriptor = new Descriptor(
            PLY0033,
            "Player: Audio Speaker Mode is Stereo or Surround",
            Impact.BuildSize | Impact.Memory,
            "<b>Default Speaker Mode</b> in Audio Settings is not <b>Mono</b>. Stereo and surround modes mix and store audio with multiple channels, inflating build size and runtime audio memory — most mobile devices play through a single speaker anyway.",
            "Set <b>Project Settings &gt; Audio &gt; Default Speaker Mode</b> to <b>Mono</b>. Pair with the <b>Force To Mono</b> import setting on individual AudioClips to shrink build size further.")
        {
            Platforms = new[] { BuildTarget.Android, BuildTarget.iOS }
        };

        public override void Analyze(SettingsAnalysisContext context)
        {
            if (context.IsDescriptorEnabled(SpeakerModeDescriptor) &&
                SpeakerModeDescriptor.IsApplicable(context.Options) &&
                !IsSpeakerModeMono())
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, SpeakerModeDescriptor.Id)
                    .WithLocation(new Location("Project/Audio"));
                context.ReportIssue(diagnostic);
            }
        }

        internal static bool IsSpeakerModeMono()
        {
            return AudioSettings.GetConfiguration().speakerMode == AudioSpeakerMode.Mono;
        }
    }
}
