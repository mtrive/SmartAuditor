using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// A context object passed by AudioClipsModule to an AudioClipModuleAnalyzer's Analyze() method.
    /// </summary>
    public class AudioClipAnalysisContext : AnalysisContext
    {
        public AudioClipAnalysisContext(AnalysisOptions options, IFindingSink sink) : base(options, sink) { }

        /// <summary>
        /// The name of an AudioClip in the project.
        /// </summary>
        public string Name;

        /// <summary>
        /// The AudioClip to be analyzed.
        /// </summary>
        public AudioClip AudioClip;

        /// <summary>
        /// The AudioImporter used to import the AudioClip to be analyzed.
        /// </summary>
        public AudioImporter Importer;

        /// <summary>
        /// The AudioImporter's sample settings.
        /// </summary>
        public AudioImporterSampleSettings SampleSettings;

        /// <summary>
        /// The file size of the imported AudioClip.
        /// </summary>
        public long ImportedSize;

        /// <summary>
        /// An estimate of the runtime memory footprint of this AudioClip, when it's playing.
        /// </summary>
        public long RuntimeSize;
    }

    /// <summary>
    /// Abstract base class for an Analyzer to be invoked by AudioClipsModule
    /// </summary>
    internal abstract class AudioClipAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// Implement this method to detect Issues, construct ReportItem objects to describe them, and return them.
        /// </summary>
        /// <param name="context">Context object containing information necessary to perform analysis</param>
        /// <returns>An enumerator for a collection of ReportItem objects</returns>
        public abstract void Analyze(AudioClipAnalysisContext context);
    }
}
