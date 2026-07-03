// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Modules;
using UnityEngine;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Detects GameObjects assigned to a layer index that has no name in the Tags and Layers settings.
    /// Reports at Major when the object carries a Renderer (its geometry is culled by named-layer masks)
    /// and at Info otherwise (an empty node — harmless now, but a latent risk if a renderer is added).
    /// </summary>
    sealed class GameObjectLayerAnalyzer : GameObjectAnalyzer
    {
        internal const string OBJ0021 = nameof(OBJ0021);

        internal const string HasRendererEvidence = "HasRenderer";

        internal static readonly Descriptor UndefinedLayerDescriptor = new Descriptor(
            OBJ0021,
            "GameObject: Undefined Layer",
            Impact.Correctness,
            "A GameObject is assigned to a layer index that has no name in the project's <b>Tags and Layers</b> settings. " +
            "A camera whose <b>Culling Mask</b> is built from named layers excludes the unnamed layer, so any renderer on the " +
            "GameObject is culled at runtime even when it still renders in the Editor.",
            "Assign the GameObject to a named layer in <b>Tags and Layers</b>, or name the empty layer slot if the index is meant to be used."
        )
        {
            MessageFormat = "GameObject '{0}' is on undefined layer {1}",
            DefaultSeverity = Severity.Major
        };

        public override void AnalyzeGameObject(GameObjectAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(UndefinedLayerDescriptor, context.AssetPath))
                return;

            var gameObject = context.GameObject;
            if (gameObject == null)
                return;

            var layer = gameObject.layer;
            if (!string.IsNullOrEmpty(LayerMask.LayerToName(layer)))
                return;

            // Layer is per-renderer at cull time and not inherited by children, so an object without a
            // Renderer is never culled itself — downgrade those to Info rather than flagging them as breakage.
            var hasRenderer = gameObject.TryGetComponent<Renderer>(out _);

            var diagnostic = Diagnostic.Create(
                AnalysisCategory.GameObjectIssue,
                OBJ0021,
                gameObject.name,
                layer)
                .WithSeverity(hasRenderer ? Severity.Major : Severity.Info)
                .WithEvidence(HasRendererEvidence, hasRenderer)
                .WithLocation(new Location(context.AssetPath))
                .WithProperty(GameObjectIssueColumns.HierarchyPath, context.HierarchyPath)
                .WithProperty(GameObjectIssueColumns.Scene, context.SceneName)
                .WithProperty(GameObjectIssueColumns.GlobalObjectId, context.GlobalObjectId);
            context.ReportIssue(diagnostic);
        }
    }
}
