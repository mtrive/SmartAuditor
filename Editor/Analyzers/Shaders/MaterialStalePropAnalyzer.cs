// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using SmartAuditor.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Analyzes materials for serialized properties that no longer exist on the assigned shader.
    /// </summary>
    sealed class MaterialStalePropAnalyzer : ShaderAnalyzer
    {
        internal const string MAT0004 = nameof(MAT0004);

        internal static readonly Descriptor StalePropertyDescriptor = new Descriptor(
            MAT0004,
            "Material: Stale Shader Property",
            Impact.Correctness,
            "The material's <b>m_SavedProperties</b> serialized data holds an entry for a property name that the currently-assigned shader does not declare. Stale entries accumulate when a material's shader is changed or when a shader property is renamed, and they inflate the asset file without affecting rendering.",
            "Reassign the shader in the Material Inspector to prune the stale entries, or strip them programmatically via <b>SerializedObject</b> on the material asset."
        )
        {
            DefaultSeverity = Severity.Minor,
            // Gated off by default: shader.FindPropertyIndex doesn't see properties added at
            // runtime via ShaderLab-defined material overrides, so a stale-property report can
            // be a false positive on materials that rely on runtime injection. Verify on a
            // broad sample before flipping to enabled by default.
            IsEnabledByDefault = false,
            MessageFormat = "Material '{0}' has stale {3} property '{1}' not found in shader '{2}'"
        };

        // Maps serialized bucket names to human-readable property type labels.
        static readonly (string Bucket, string Label)[] k_SavedPropertyBuckets =
        {
            ("m_TexEnvs", "Texture"),
            ("m_Ints",    "Int"),
            ("m_Floats",  "Float"),
            ("m_Colors",  "Color")
        };

        public override void AnalyzeMaterial(MaterialAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(StalePropertyDescriptor, context.AssetPath))
                return;

            var material = context.Material;
            var shader = material.shader;

            if (shader == null)
                return;

            var serializedMaterial = new SerializedObject(material);
            var savedProperties = serializedMaterial.FindProperty("m_SavedProperties");
            if (savedProperties == null)
                return;

            foreach (var (bucketName, typeLabel) in k_SavedPropertyBuckets)
            {
                var bucket = savedProperties.FindPropertyRelative(bucketName);
                if (bucket == null || !bucket.isArray)
                    continue;

                for (int i = 0; i < bucket.arraySize; i++)
                {
                    var entry = bucket.GetArrayElementAtIndex(i);
                    var keyProp = entry.FindPropertyRelative("first");
                    if (keyProp == null)
                        continue;

                    var propertyName = keyProp.stringValue;
                    if (string.IsNullOrEmpty(propertyName))
                        continue;

                    if (shader.FindPropertyIndex(propertyName) >= 0)
                        continue;

                    var diagnostic = Diagnostic.Create(
                            AnalysisCategory.AssetIssue,
                            StalePropertyDescriptor.Id,
                            material.name,
                            propertyName,
                            shader.name,
                            typeLabel)
                        .WithLocation(new Location(context.AssetPath))
                        .WithEvidence("PropertyName", propertyName)
                        .WithEvidence("PropertyType", typeLabel)
                        .WithEvidence("ShaderName", shader.name);
                    context.ReportIssue(diagnostic);
                }
            }
        }
    }
}
