using System.Collections.Generic;
using System.IO;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// A context object passed by AssetsModule to an AssetAnalyzer's Analyze() method.
    /// </summary>
    public class AssetAnalysisContext : AnalysisContext
    {
        public AssetAnalysisContext(AnalysisOptions options, IFindingSink sink) : base(options, sink) { }

        /// <summary>
        /// A path to an asset, of any type, in the project.
        /// </summary>
        public string AssetPath;

        /// <summary>
        /// The name of the asset without the file extension, derived from AssetPath.
        /// </summary>
        public string AssetName => Path.GetFileNameWithoutExtension(AssetPath);
    }

    /// <summary>
    /// Abstract base class for an Analyzer to be invoked by AssetsModule.
    /// </summary>
    internal abstract class AssetAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// Implement this method to detect Issues, construct ReportItem objects to describe them, and return them.
        /// </summary>
        /// <param name="context">Context object containing information necessary to perform analysis</param>
        /// <returns>An enumerator for a collection of ReportItem objects</returns>
        public abstract void Analyze(AssetAnalysisContext context);
    }

    /// <summary>
    /// Base class for asset analyzers that run once per analysis over the whole project
    /// (cross-file or aggregate checks) rather than once per asset. Override <see cref="DiagnosticAnalyzer.Finalize"/>;
    /// the per-asset <see cref="AssetAnalyzer.Analyze"/> entry point is sealed to a no-op so subclasses
    /// are not forced to provide a meaningless implementation.
    /// </summary>
    internal abstract class ProjectAssetAnalyzer : AssetAnalyzer
    {
        /// <inheritdoc/>
        public sealed override void Analyze(AssetAnalysisContext context) { }
    }
}
