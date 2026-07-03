using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// A context object passed by <see cref="TexturesModule"/> to a <see cref="TextureAnalyzer"/>'s
    /// <see cref="TextureAnalyzer.Analyze"/> method.
    /// </summary>
    /// <remarks>
    /// Modules always pass a derived context type (<see cref="Texture2DAnalysisContext"/>, etc.).
    /// A plain <see cref="TextureAnalysisContext"/> is not constructed at runtime.
    /// </remarks>
    public class TextureAnalysisContext : AnalysisContext
    {
        public TextureAnalysisContext(AnalysisOptions options, IFindingSink sink) : base(options, sink) { }

        /// <summary>
        /// The name of a texture asset in the project.
        /// </summary>
        public string Name;

        /// <summary>
        /// The TextureImporter used to import the texture to be analyzed.
        /// </summary>
        public TextureImporter Importer;

        /// <summary>
        /// The texture importer's platform settings, matching the target analysis platform.
        /// </summary>
        public TextureImporterPlatformSettings ImporterPlatformSettings;

        /// <summary>
        /// An estimate of the texture's runtime memory footprint.
        /// </summary>
        public long Size;
    }

    /// <summary>
    /// Context for analyzing <see cref="Texture2D"/> assets, including sprites and UI textures.
    /// </summary>
    public class Texture2DAnalysisContext : TextureAnalysisContext
    {
        public Texture2DAnalysisContext(AnalysisOptions options, IFindingSink sink) : base(options, sink) { }

        /// <summary>
        /// The Texture2D asset to be analyzed.
        /// </summary>
        public Texture2D Texture;

        /// <summary>
        /// A flat copy of the texture's pixel data.
        /// </summary>
        /// <remarks>
        /// <c>null</c> when <see cref="TextureImporter.isReadable"/> is off. Populating this array
        /// calls <see cref="Texture2D.GetPixels"/>, which is expensive and requires Read/Write to be enabled.
        /// </remarks>
        public Color[] Pixels;
    }

    /// <summary>
    /// Context for analyzing <see cref="Texture3D"/> assets.
    /// </summary>
    public class Texture3DAnalysisContext : TextureAnalysisContext
    {
        public Texture3DAnalysisContext(AnalysisOptions options, IFindingSink sink) : base(options, sink) { }

        /// <summary>
        /// The Texture3D asset to be analyzed.
        /// </summary>
        public Texture3D Texture;

        /// <summary>
        /// A flat copy of the texture's pixel data.
        /// </summary>
        /// <remarks>
        /// <c>null</c> when the texture is not readable. Populating this array calls
        /// <see cref="Texture3D.GetPixels"/>, which is expensive and requires Read/Write to be enabled.
        /// </remarks>
        public Color[] Pixels;

        /// <summary>
        /// The depth (number of slices) of the Texture3D asset.
        /// </summary>
        public int Depth;
    }

    /// <summary>
    /// Context for analyzing <see cref="Cubemap"/> assets.
    /// </summary>
    public class CubemapAnalysisContext : TextureAnalysisContext
    {
        public CubemapAnalysisContext(AnalysisOptions options, IFindingSink sink) : base(options, sink) { }

        /// <summary>
        /// The Cubemap asset to be analyzed.
        /// </summary>
        public Cubemap Texture;

        /// <summary>
        /// Per-face pixel data for the cubemap.
        /// </summary>
        /// <remarks>
        /// Empty when the cubemap is not readable. Populating face arrays calls
        /// <see cref="Cubemap.GetPixels"/> per face, which is expensive and requires Read/Write to be enabled.
        /// </remarks>
        public Dictionary<CubemapFace, Color[]> FacePixels;
    }

    /// <summary>
    /// Context for analyzing <see cref="RenderTexture"/> assets.
    /// </summary>
    /// <remarks>
    /// <see cref="TexturesModule"/> currently skips assets without a <see cref="TextureImporter"/>,
    /// so this context type is not constructed during a standard texture audit. It is reserved for future
    /// RenderTexture support.
    /// </remarks>
    public class RenderTextureAnalysisContext : TextureAnalysisContext
    {
        public RenderTextureAnalysisContext(AnalysisOptions options, IFindingSink sink) : base(options, sink) { }

        /// <summary>
        /// The RenderTexture asset to be analyzed.
        /// </summary>
        public RenderTexture Texture;

        /// <summary>
        /// The RenderTexture's pixel format.
        /// </summary>
        public RenderTextureFormat Format;
    }

    /// <summary>
    /// Abstract base class for an analyzer invoked by <see cref="TexturesModule"/>.
    /// </summary>
    /// <remarks>
    /// Override <see cref="Analyze"/> for diagnostics that depend only on importer settings or other
    /// properties shared across all texture shapes (read/write, mipmaps, compression, cursor rules, etc.).
    /// Override the type-specific methods (<see cref="AnalyzeTexture2D"/>, <see cref="AnalyzeTexture3D"/>,
    /// <see cref="AnalyzeCubemap"/>, <see cref="AnalyzeRenderTexture"/>) for diagnostics that need the
    /// loaded texture or its pixel data. If you override <see cref="Analyze"/>, call
    /// <c>base.Analyze(context)</c> when you still want the type-specific hooks to run.
    /// </remarks>
    internal abstract class TextureAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// Dispatches analysis to the type-specific hook matching the context's texture shape.
        /// </summary>
        /// <param name="context">Context object containing information necessary to perform analysis.</param>
        /// <remarks>
        /// When texture analyzers detect an issue, they should use <see cref="Diagnostic.Create"/> to create
        /// a diagnostic with an <see cref="AnalysisCategory"/> and a descriptor id, then call
        /// <see cref="AnalysisContext.ReportIssue"/> to report it.
        /// </remarks>
        public virtual void Analyze(TextureAnalysisContext context)
        {
            switch (context)
            {
                case Texture2DAnalysisContext ctx:
                    AnalyzeTexture2D(ctx);
                    break;
                case Texture3DAnalysisContext ctx:
                    AnalyzeTexture3D(ctx);
                    break;
                case CubemapAnalysisContext ctx:
                    AnalyzeCubemap(ctx);
                    break;
                case RenderTextureAnalysisContext ctx:
                    AnalyzeRenderTexture(ctx);
                    break;
            }
        }

        /// <summary>
        /// Override to analyze <see cref="Texture2D"/> assets (sprites, UI textures, and default 2D textures).
        /// </summary>
        /// <param name="context">Context describing the Texture2D to analyze.</param>
        public virtual void AnalyzeTexture2D(Texture2DAnalysisContext context)
        {
        }

        /// <summary>
        /// Override to analyze <see cref="Texture3D"/> assets.
        /// </summary>
        /// <param name="context">Context describing the Texture3D to analyze.</param>
        public virtual void AnalyzeTexture3D(Texture3DAnalysisContext context)
        {
        }

        /// <summary>
        /// Override to analyze <see cref="Cubemap"/> assets.
        /// </summary>
        /// <param name="context">Context describing the Cubemap to analyze.</param>
        public virtual void AnalyzeCubemap(CubemapAnalysisContext context)
        {
        }

        /// <summary>
        /// Override to analyze <see cref="RenderTexture"/> assets.
        /// </summary>
        /// <param name="context">Context describing the RenderTexture to analyze.</param>
        /// <remarks>
        /// Not invoked by <see cref="TexturesModule"/> today; see
        /// <see cref="RenderTextureAnalysisContext"/>.
        /// </remarks>
        public virtual void AnalyzeRenderTexture(RenderTextureAnalysisContext context)
        {
        }
    }
}
