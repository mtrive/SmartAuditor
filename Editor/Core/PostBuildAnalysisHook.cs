using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using SmartAuditor.Editor.Settings;

namespace SmartAuditor.Editor.Core
{
    public class PostBuildAnalysisHook : IPostprocessBuildWithReport
    {
        /// <summary>
        /// Returns the relative callback order for callbacks. Callbacks with lower values are called before ones with higher values.
        /// </summary>
        public int callbackOrder => 1;  // We want LastBuildReportProvider to update its cached report before we run analysis.

        /// <summary>
        /// Callback function which is called after a build is completed.
        /// If UserPreferences.AnalyzeAfterBuild is true, performs a full analysis and logs the number of issues found.
        /// </summary>
        /// <param name="report">A report containing information about the build, such as its target platform and output path.</param>
        public void OnPostprocessBuild(BuildReport report)
        {
            if (UserPreferences.AnalyzeAfterBuild)
            {
                // Library/LastBuild.buildreport is only created AFTER OnPostprocessBuild so we need to defer analysis until the file is copied.
                EditorApplication.update += DelayedPostBuildAnalysis;
            }
        }

        internal void DelayedPostBuildAnalysis()
        {
            var report = SmartAuditor.Analyze();

            var numIssues = report.IssueCount;
            if (numIssues > 0)
            {
                if (UserPreferences.FailBuildOnIssues)
                    Debug.LogError($"[{SmartAuditor.DisplayName}] Analysis found " + numIssues + " issues");
                else
                    Debug.Log($"[{SmartAuditor.DisplayName}] Analysis found " + numIssues + " issues");
            }

            EditorApplication.update -= DelayedPostBuildAnalysis;
        }
    }
}
