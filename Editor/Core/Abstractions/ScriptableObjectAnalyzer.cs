using System.IO;
using UnityEngine;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// A context object passed by ScriptableObjectsModule to a ScriptableObjectAnalyzer's Analyze() method.
    /// </summary>
    public class ScriptableObjectAnalysisContext : AnalysisContext
    {
        public ScriptableObjectAnalysisContext(AnalysisOptions options, IFindingSink sink) : base(options, sink) { }

        /// <summary>
        /// The loaded ScriptableObject asset being analyzed.
        /// </summary>
        public ScriptableObject ScriptableObject;

        /// <summary>
        /// The asset path of the ScriptableObject.
        /// </summary>
        public string AssetPath;

        /// <summary>
        /// The asset filename without extension, derived from AssetPath.
        /// </summary>
        public string AssetName => Path.GetFileNameWithoutExtension(AssetPath);
    }

    /// <summary>
    /// Abstract base class for an Analyzer to be invoked by ScriptableObjectsModule.
    /// </summary>
    internal abstract class ScriptableObjectAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// Implement this method to analyze a single ScriptableObject asset.
        /// The asset is already loaded and available via <see cref="ScriptableObjectAnalysisContext.ScriptableObject"/>.
        /// Report issues using <c>Diagnostic.Create</c> and <c>context.ReportIssue</c>.
        /// </summary>
        /// <param name="context">Context object containing the loaded ScriptableObject and its path.</param>
        public abstract void Analyze(ScriptableObjectAnalysisContext context);
    }
}
