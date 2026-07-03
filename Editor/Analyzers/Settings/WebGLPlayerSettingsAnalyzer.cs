using SmartAuditor.Editor.Core;
using UnityEditor;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class WebGLPlayerSettingsAnalyzer : SettingsAnalyzer
    {
        internal const string PLY0010 = nameof(PLY0010);

        static readonly Descriptor DataCachingDescriptor = new Descriptor(
            PLY0010,
            "Player (WebGL): Data Caching Disabled",
            Impact.LoadTime,
            "<b>Data Caching</b> in WebGL Player Settings is off. The browser re-downloads the build's asset data on every visit instead of reusing a cached copy, inflating load time for repeat visitors.",
            "Enable <b>Project Settings &gt; Player &gt; WebGL &gt; Publishing Settings &gt; Data Caching</b> so the browser stores build files in its cache.")
        {
            Platforms = new[] { BuildTarget.WebGL }
        };

        public override void Analyze(SettingsAnalysisContext context)
        {
            if (context.IsDescriptorEnabled(DataCachingDescriptor) &&
                DataCachingDescriptor.IsApplicable(context.Options) &&
                !PlayerSettings.WebGL.dataCaching)
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.ProjectSetting, DataCachingDescriptor.Id)
                    .WithLocation(new Location("Project/Player"));
                context.ReportIssue(diagnostic);
            }
        }
    }
}
