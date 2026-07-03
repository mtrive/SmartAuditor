using SmartAuditor.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// Context passed to <see cref="ShaderAnalyzer.AnalyzeTextureUsage"/> for each texture assignment in a material.
    /// </summary>
    public class TextureUsageAnalysisContext : AnalysisContext
    {
        public TextureUsageAnalysisContext(AnalysisOptions options, IFindingSink sink) : base(options, sink) { }

        /// <summary>
        /// The material that assigns the texture.
        /// </summary>
        public Material Material;

        /// <summary>
        /// Path to the material asset in the project.
        /// </summary>
        public string MaterialPath;

        /// <summary>
        /// Shader property name (e.g. _BumpMap, _MainTex).
        /// </summary>
        public string ShaderPropertyName;

        /// <summary>
        /// The texture assigned to the property.
        /// </summary>
        public Texture Texture;

        /// <summary>
        /// Path to the texture asset in the project. Empty for built-in or non-asset textures.
        /// </summary>
        public string TexturePath;

        /// <summary>
        /// Texture importer for the texture. Null if not a project asset (e.g. RenderTexture, built-in).
        /// </summary>
        public TextureImporter TextureImporter;
    }

    public class MaterialAnalysisContext : AnalysisContext
    {
        public MaterialAnalysisContext(AnalysisOptions options, IFindingSink sink) : base(options, sink) { }

        /// <summary>
        /// A path to a material asset in the project.
        /// </summary>
        public string AssetPath;

        /// <summary>
        /// The material object to be analyzed.
        /// </summary>
        public Material Material;
    }

    /// <summary>
    /// A context object passed by ShadersModule to a ShaderModuleAnalyzer's Analyze() method.
    /// </summary>
    public class ShaderAnalysisContext : AnalysisContext
    {
        public ShaderAnalysisContext(AnalysisOptions options, IFindingSink sink) : base(options, sink) { }

        /// <summary>
        /// A path to a shader asset in the project.
        /// </summary>
        public string AssetPath;

        /// <summary>
        /// The shader object to be analyzed.
        /// </summary>
        public Shader Shader;

        /// <summary>
        /// The raw source code of the shader. Only populated for <c>.shader</c> (ShaderLab) files that exist on disk.
        /// Null for non-readable assets such as <c>.shadergraph</c> files or built-in shaders.
        /// </summary>
        public string SourceCode;

        string m_StrippedSourceCode;
        bool m_StrippedSourceCodeBuilt;

        /// <summary>
        /// <see cref="SourceCode"/> with ShaderLab and HLSL comments stripped, computed lazily on
        /// first access. Several analyzers run regex passes over the source; sharing one stripped
        /// copy on the context avoids re-stripping every shader for every analyzer.
        /// </summary>
        public string StrippedSourceCode
        {
            get
            {
                if (!m_StrippedSourceCodeBuilt)
                {
                    m_StrippedSourceCode = SourceCode != null
                        ? ShaderSourceUtils.StripComments(SourceCode)
                        : null;
                    m_StrippedSourceCodeBuilt = true;
                }
                return m_StrippedSourceCode;
            }
        }
    }

    /// <summary>
    /// Abstract base class for an Analyzer to be invoked by ShaderModule
    /// </summary>
    internal abstract class ShaderAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// Implement this method to detect Issues, construct ReportItem objects to describe them, and return them.
        /// </summary>
        /// <param name="context">Context object containing information necessary to perform analysis</param>
        /// <returns>An enumerator for a collection of ReportItem objects</returns>
        public virtual void AnalyzeMaterial(MaterialAnalysisContext context)
        {
        }

        public virtual void AnalyzeShader(ShaderAnalysisContext context)
        {
        }

        /// <summary>
        /// Implement this method to analyze how a texture is used in a material and suggest import settings.
        /// Called for each texture property assignment in each material during ProcessMaterials.
        /// </summary>
        /// <param name="context">Context with material, shader property name, texture, and importer</param>
        public virtual void AnalyzeTextureUsage(TextureUsageAnalysisContext context)
        {
        }
    }
}
