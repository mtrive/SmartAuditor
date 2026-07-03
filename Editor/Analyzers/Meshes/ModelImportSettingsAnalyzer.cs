// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using SmartAuditor.Editor.Core;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class ModelImportSettingsAnalyzer : MeshAnalyzer
    {
        internal const string MDL0000 = nameof(MDL0000);
        internal const string MDL0001 = nameof(MDL0001);
        internal const string MDL0002 = nameof(MDL0002);

        internal static readonly Descriptor ModelExternalObjectUnresolvedDescriptor = new Descriptor(
            MDL0000,
            "Model: External Object Reference Unresolved",
            Impact.Correctness | Impact.EditorWorkflow,
            "The Model Import Settings have a <b>Remapped Materials</b> entry whose target asset cannot be resolved. The referenced GUID may point to a deleted asset, or to an asset of a type that does not match the slot. Unresolved remaps cause the model to import non-deterministically: the importer falls back to an internal object whose identity can differ between machines, producing Asset Pipeline V2 'inconsistent result' warnings and breaking the Accelerator / Cache Server.",
            "Open the model in the Inspector. Under <b>Materials</b> -> <b>Remapped Materials</b> (or the equivalent remap list for non-material types), locate the empty / red-tinted entry and either remap it to an existing asset of the correct type, or remove the mapping."
        )
        {
            MessageFormat = "Model '{0}' has unresolved external object remaps",
            DocumentationUrl = "https://docs.unity3d.com/Manual/FBXImporter-Materials.html"
        };

        internal static readonly Descriptor ModelLegacyMaterialImportModeDescriptor = new Descriptor(
            MDL0001,
            "Model: Legacy Material Import Mode",
            Impact.Correctness | Impact.EditorWorkflow,
            "The Model Import Settings have <b>Material Creation Mode</b> set to <b>Standard (Legacy)</b>. The legacy material-import path is a known source of Asset Pipeline V2 import non-determinism: re-importing the same FBX can produce different mesh data between machines, triggering 'inconsistent result' warnings and breaking the Accelerator / Cache Server.",
            "In the Model Import Settings, change <b>Material Creation Mode</b> to <b>Import via MaterialDescription</b>, then re-import the model."
        )
        {
            MessageFormat = "Model '{0}' uses the legacy Standard material import mode",
            DefaultSeverity = Severity.Minor,
            DocumentationUrl = "https://docs.unity3d.com/Manual/FBXImporter-Materials.html"
        };

        internal static readonly Descriptor ModelExternalObjectOrphanedDescriptor = new Descriptor(
            MDL0002,
            "Model: Orphaned External Object Entry",
            Impact.EditorWorkflow,
            "The model's <b>.fbx.meta</b> carries a <b>Remapped Materials</b> entry whose slot Unity does not recognize under the current <b>Material Creation Mode</b>. Orphan entries typically survive a material-import-mode switch or a material-slot rename in the FBX. They bloat the meta and confuse anyone reading it, even though Unity ignores them at import time.",
            "Open the model in the Inspector, locate the entry under <b>Remapped Materials</b>, and click <b>Remove</b>. Alternatively, edit the <b>.fbx.meta</b> to delete the matching <b>externalObjects</b> entry."
        )
        {
            MessageFormat = "Model '{0}' has orphaned external object entries",
            DefaultSeverity = Severity.Minor,
            DocumentationUrl = "https://docs.unity3d.com/Manual/FBXImporter-Materials.html"
        };

        public override void AnalyzeModel(ModelAnalysisContext context)
        {
            if (!context.TryGetModelImporter(out var modelImporter))
            {
                return;
            }

            AnalyzeMaterialImportMode(context, modelImporter);
            AnalyzeExternalObjectMap(context, modelImporter);
        }

        static void AnalyzeMaterialImportMode(ModelAnalysisContext context, ModelImporter modelImporter)
        {
            if (!context.IsDescriptorEnabled(ModelLegacyMaterialImportModeDescriptor, context.AssetPath))
            {
                return;
            }

            if (modelImporter.materialImportMode != ModelImporterMaterialImportMode.ImportStandard)
            {
                return;
            }

            var diagnostic = Diagnostic.Create(
                    AnalysisCategory.AssetIssue,
                    ModelLegacyMaterialImportModeDescriptor.Id,
                    context.Name)
                .WithLocation(new Location(context.AssetPath));
            context.ReportIssue(diagnostic);
        }

        static void AnalyzeExternalObjectMap(ModelAnalysisContext context, ModelImporter modelImporter)
        {
            var unresolvedEnabled = context.IsDescriptorEnabled(ModelExternalObjectUnresolvedDescriptor, context.AssetPath);
            var orphanEnabled = context.IsDescriptorEnabled(ModelExternalObjectOrphanedDescriptor, context.AssetPath);

            if (!unresolvedEnabled && !orphanEnabled)
            {
                return;
            }

            var externalObjects = modelImporter.GetExternalObjectMap();

            if (unresolvedEnabled)
            {
                var unresolvedSlots = new List<string>();
                foreach (var entry in externalObjects)
                {
                    var sourceId = entry.Key;
                    var target = entry.Value;

                    if (!TryClassifyBreakage(sourceId.type, target, out _))
                        continue;

                    unresolvedSlots.Add(sourceId.name ?? string.Empty);
                }

                if (unresolvedSlots.Count > 0)
                {
                    unresolvedSlots.Sort();
                    var diagnostic = Diagnostic.Create(
                            AnalysisCategory.AssetIssue,
                            ModelExternalObjectUnresolvedDescriptor.Id,
                            context.Name)
                        .WithLocation(new Location(context.AssetPath))
                        .WithEvidence("UnresolvedSlots", unresolvedSlots.ToArray())
                        .WithEvidence("SlotCount", unresolvedSlots.Count);
                    context.ReportIssue(diagnostic);
                }
            }

            if (orphanEnabled)
            {
                var liveKeys = new HashSet<(string typeFullName, string name)>();
                foreach (var key in externalObjects.Keys)
                {
                    liveKeys.Add((key.type != null ? key.type.FullName : "", key.name ?? ""));
                }

                using var serialized = new SerializedObject(modelImporter);
                var array = serialized.FindProperty("m_ExternalObjects");
                if (array == null || !array.isArray)
                {
                    return;
                }

                var orphanedSlots = new List<string>();
                for (var i = 0; i < array.arraySize; i++)
                {
                    var first = array.GetArrayElementAtIndex(i).FindPropertyRelative("first");
                    if (first == null)
                        continue;

                    var typeYaml = first.FindPropertyRelative("type")?.stringValue ?? "";
                    var nameYaml = first.FindPropertyRelative("name")?.stringValue ?? "";

                    if (!IsOrphan(typeYaml, nameYaml, liveKeys))
                        continue;

                    orphanedSlots.Add(nameYaml);
                }

                if (orphanedSlots.Count > 0)
                {
                    orphanedSlots.Sort();
                    var diagnostic = Diagnostic.Create(
                            AnalysisCategory.AssetIssue,
                            ModelExternalObjectOrphanedDescriptor.Id,
                            context.Name)
                        .WithLocation(new Location(context.AssetPath))
                        .WithEvidence("OrphanedSlots", orphanedSlots.ToArray())
                        .WithEvidence("SlotCount", orphanedSlots.Count);
                    context.ReportIssue(diagnostic);
                }
            }
        }

        /// <summary>
        /// Returns true if the (expectedType, target) pair represents a broken external-object
        /// remap, with <paramref name="reason"/> set to either "Missing" (target unresolved) or
        /// "TypeMismatch" (target resolved to a different type). Returns false when the entry is
        /// healthy.
        /// </summary>
        internal static bool TryClassifyBreakage(System.Type expectedType, Object target, out string reason)
        {
            if (!target)
            {
                reason = "Missing";
                return true;
            }
            if (expectedType != null && !expectedType.IsInstanceOfType(target))
            {
                reason = "TypeMismatch";
                return true;
            }
            reason = null;
            return false;
        }

        /// <summary>
        /// Returns true if a serialised external-object entry does not appear in the live importer
        /// map -- i.e. the slot identified by <paramref name="serialisedTypeYaml"/> +
        /// <paramref name="serialisedName"/> isn't honoured under the current materialImportMode.
        /// The YAML type form is <c>"UnityEngine:Material"</c>; <paramref name="liveKeys"/> stores
        /// <c>Type.FullName</c> values (<c>"UnityEngine.Material"</c>), so we normalise the YAML
        /// colon to a dot before comparing.
        /// </summary>
        internal static bool IsOrphan(
            string serialisedTypeYaml,
            string serialisedName,
            HashSet<(string typeFullName, string name)> liveKeys)
        {
            var typeFullName = (serialisedTypeYaml ?? "").Replace(":", ".");
            var name = serialisedName ?? "";
            return !liveKeys.Contains((typeFullName, name));
        }
    }
}
