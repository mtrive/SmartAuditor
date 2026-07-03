using System.Collections.Generic;
using UnityEditor.PackageManager;
using UpmPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// A context object passed by PackagesModule to a PackagesModuleAnalyzer's Analyze() method.
    /// </summary>
    public class PackageAnalysisContext : AnalysisContext
    {
        public PackageAnalysisContext(AnalysisOptions options, IFindingSink sink) : base(options, sink) { }

        /// <summary>
        /// Information about a Unity package to be analyzed.
        /// </summary>
        public UpmPackageInfo PackageInfo;
    }

    /// <summary>
    /// Abstract base class for an Analyzer to be invoked by PackagesModule
    /// </summary>
    internal abstract class PackageAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// Implement this method to detect Issues, construct ReportItem objects to describe them, and return them.
        /// </summary>
        /// <param name="context">Context object containing information necessary to perform analysis</param>
        /// <returns>An enumerator for a collection of ReportItem objects</returns>
        public abstract void Analyze(PackageAnalysisContext context);
    }
}
