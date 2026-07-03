using System.Collections.Generic;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// A context object passed by SpriteAtlasesModule to a SpriteAtlasModuleAnalyzer's Analyze() method.
    /// </summary>
    public class SpriteAtlasAnalysisContext : AnalysisContext
    {
        public SpriteAtlasAnalysisContext(AnalysisOptions options, IFindingSink sink) : base(options, sink) { }

        /// <summary>
        /// The path to a Sprite Atlas asset in the project.
        /// </summary>
        public string AssetPath;
    }

    /// <summary>
    /// Abstract base class for an Analyzer to be invoked by SpriteAtlasesModule
    /// </summary>
    internal abstract class SpriteAtlasAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// Implement this method to detect Issues, construct ReportItem objects to describe them, and return them.
        /// </summary>
        /// <param name="context">Context object containing information necessary to perform analysis</param>
        /// <returns>An enumerator for a collection of ReportItem objects</returns>
        public abstract void Analyze(SpriteAtlasAnalysisContext context);
    }
}
