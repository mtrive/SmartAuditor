using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// A context object passed by MeshesModule to a MeshAnalyzer's AnalyzeMesh() method, once per Mesh sub-asset.
    /// Use this for diagnostics that depend on an individual mesh's geometry or per-mesh import settings.
    /// </summary>
    public class MeshAnalysisContext : AnalysisContext
    {
        public MeshAnalysisContext(AnalysisOptions options, IFindingSink sink) : base(options, sink) { }

        /// <summary>
        /// The name of a Mesh asset to be analyzed.
        /// </summary>
        public string Name;

        /// <summary>
        /// The Mesh asset to be analyzed.
        /// </summary>
        public Mesh Mesh;

        /// <summary>
        /// The Mesh asset's AssetImporter
        /// </summary>
        /// <remarks>
        /// Meshes can be created from source assets by a number of different types of importer in Unity. Therefore,
        /// it's important to check the results of any attempts to cast this AssetImporter to an inherited importer type
        /// to ensure the cast was successful.
        /// </remarks>
        public AssetImporter Importer;

        /// <summary>
        /// An estimate of the Mesh's runtime memory footprint.
        /// </summary>
        public long Size;
    }

    /// <summary>
    /// A context object passed by MeshesModule to a MeshAnalyzer's AnalyzeModel() method, once per source asset
    /// (model) that contains one or more meshes. Use this for diagnostics that are a property of the importer
    /// or of the model as a whole, rather than of an individual mesh.
    /// </summary>
    public class ModelAnalysisContext : AnalysisContext
    {
        public ModelAnalysisContext(AnalysisOptions options, IFindingSink sink) : base(options, sink) { }

        /// <summary>
        /// The name of the source asset (model), without its file extension.
        /// </summary>
        public string Name;

        /// <summary>
        /// The asset path of the source asset (model).
        /// </summary>
        public string AssetPath;

        /// <summary>
        /// The source asset's AssetImporter.
        /// </summary>
        /// <remarks>
        /// Meshes can be created from source assets by a number of different types of importer in Unity. Therefore,
        /// it's important to check the results of any attempts to cast this AssetImporter to an inherited importer type
        /// to ensure the cast was successful. <see cref="TryGetModelImporter"/> is provided for the common case.
        /// </remarks>
        public AssetImporter Importer;

        /// <summary>
        /// The Mesh sub-assets found at <see cref="AssetPath"/>.
        /// </summary>
        public IReadOnlyList<Mesh> Meshes;

        /// <summary>
        /// Tries to return the source asset's importer as a <see cref="ModelImporter"/>.
        /// </summary>
        public bool TryGetModelImporter(out ModelImporter importer)
        {
            importer = Importer as ModelImporter;
            return importer != null;
        }
    }

    /// <summary>
    /// Abstract base class for an Analyzer to be invoked by MeshesModule.
    /// </summary>
    /// <remarks>
    /// A source asset (model) can contain one or more meshes. Override <see cref="AnalyzeMesh"/> for diagnostics that
    /// inspect an individual mesh; override <see cref="AnalyzeModel"/> for diagnostics that inspect the importer or
    /// the model as a whole. An analyzer may override either or both.
    /// </remarks>
    internal abstract class MeshAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// Override this method to analyze an individual Mesh. It is invoked once per Mesh sub-asset.
        /// Report issues using Diagnostic.Create and context.ReportIssue.
        /// </summary>
        /// <param name="context">Context describing the Mesh to analyze.</param>
        public virtual void AnalyzeMesh(MeshAnalysisContext context)
        {
        }

        /// <summary>
        /// Override this method to analyze a source asset (model) as a whole. It is invoked once per asset that
        /// contains meshes, before the per-mesh <see cref="AnalyzeMesh"/> calls. Report issues using Diagnostic.Create
        /// and context.ReportIssue.
        /// </summary>
        /// <param name="context">Context describing the model to analyze.</param>
        public virtual void AnalyzeModel(ModelAnalysisContext context)
        {
        }
    }
}
