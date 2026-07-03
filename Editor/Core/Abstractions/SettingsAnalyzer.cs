
namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// A context object passed by SettingsModule to a SettingsAnalyzer's Analyze() method.
    /// </summary>
    public class SettingsAnalysisContext : AnalysisContext
    {
        public SettingsAnalysisContext(AnalysisOptions options, IFindingSink sink) : base(options, sink) { }
    }

    /// <summary>
    /// Abstract base class for an Analyzer to be invoked by SettingsModule.
    /// </summary>
    internal abstract class SettingsAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// Implement this method to detect Issues and report them using Diagnostic.Create and context.ReportIssue.
        /// </summary>
        /// <param name="context">Context object containing information necessary to perform analysis</param>
        /// <remarks>
        /// When Settings Analyzers detect an issue, they should use <seealso cref="Diagnostic.Create"/> to create
        /// a diagnostic with an <see cref="AnalysisCategory"/> and a DescriptorId, then call <seealso cref="AnalysisContext.ReportIssue"/> to report it.
        /// </remarks>
        public abstract void Analyze(SettingsAnalysisContext context);
    }
}
