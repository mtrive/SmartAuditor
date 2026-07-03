using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// Base class for all diagnostic analyzers.
    /// </summary>
    /// <remarks>
    /// Inheriting directly from DiagnosticAnalyzer will not create an Analyzer that a Module will create or run. You should
    /// inherit from one of the following classes, all of which declare Analyze() methods:
    /// * <seealso cref="AnimationClipAnalyzer"/>
    /// * <seealso cref="AssetAnalyzer"/> — per-asset analysis dispatched by AssetsModule over every asset path.
    /// * <seealso cref="ProjectAssetAnalyzer"/> — whole-project analysis; override Finalize() instead of Analyze().
    /// * <seealso cref="AudioClipAnalyzer"/>
    /// * <seealso cref="CodeAnalyzer"/>
    /// * <seealso cref="MeshAnalyzer"/>
    /// * <seealso cref="PackageAnalyzer"/>
    /// * <seealso cref="ScriptableObjectAnalyzer"/> — per-ScriptableObject analysis dispatched by ScriptableObjectsModule.
    /// * <seealso cref="SettingsAnalyzer"/>
    /// * <seealso cref="ShaderAnalyzer"/>
    /// * <seealso cref="SpriteAtlasAnalyzer"/>
    /// * <seealso cref="TextureAnalyzer"/>
    /// </remarks>
    public class DiagnosticAnalyzer
    {
        /// <summary>
        /// Gets the descriptors supported by this analyzer.
        /// </summary>
        /// <remarks>
        /// The default implementation uses reflection to find all static Descriptor fields on the analyzer type.
        /// Override this property to provide descriptors from other sources (e.g., JSON files).
        /// </remarks>
        public virtual IReadOnlyList<Descriptor> SupportedDescriptors
        {
            get
            {
                return GetType()
                    .GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(field => field.FieldType == typeof(Descriptor))
                    .Select(field => (Descriptor)field.GetValue(null))
                    .Where(d => d != null)
                    .ToList();
            }
        }

        /// <summary>
        /// Initializes the Analyzer.
        /// </summary>
        /// <remarks>
        /// Override this method to perform any initialization required by the analyzer,
        /// such as constructing and/or caching data structures to optimize the Analyze() methods.
        /// Descriptor registration is handled automatically via the SupportedDescriptors property.
        /// </remarks>
        public virtual void Initialize()
        {
        }

        public virtual void Finalize(AnalysisContext context, IProgress progress = null)
        {
        }

        internal void CacheParameters(DiagnosticParams diagnosticParams)
        {
            foreach (var field in this.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var attribute = field.GetCustomAttribute<DiagnosticParameterAttribute>();
                if (attribute == null)
                    continue;

                if (field.FieldType == typeof(float))
                    field.SetValue(this, diagnosticParams.GetFloatParameter(attribute.Name));
                else
                    field.SetValue(this, diagnosticParams.GetParameter(attribute.Name));
            }
        }

        internal void RegisterParameters(DiagnosticParams diagnosticParams)
        {
            foreach (var field in this.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var attribute = field.GetCustomAttribute<DiagnosticParameterAttribute>();
                if (attribute == null)
                    continue;

                if (attribute.IsFloat)
                    diagnosticParams.RegisterParameter(attribute.Name, attribute.DefaultFloatValue);
                else
                    diagnosticParams.RegisterParameter(attribute.Name, attribute.DefaultValue);
            }
        }
    }
}
