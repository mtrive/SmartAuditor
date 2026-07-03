// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class TerrainPropertyAllocatingAnalyzer : CodeAnalyzer
    {
        internal const string CDE0110 = nameof(CDE0110);
        internal const string CDE0111 = nameof(CDE0111);
        internal const string CDE0185 = nameof(CDE0185);
        internal const string CDE0188 = nameof(CDE0188);

        static readonly Descriptor TreeInstancesDescriptor = new Descriptor(
            CDE0110,
            "Terrain: TerrainData.treeInstances Allocates",
            Impact.Memory,
            "<b>TerrainData.treeInstances</b> allocates a fresh <b>TreeInstance[]</b> on every access.",
            "Read individual entries with <b>TerrainData.GetTreeInstance(int index)</b>, or cache <b>TerrainData.treeInstances</b> during initialization and reuse the array."
        )
        {
            MessageFormat = "{0} allocates a managed array on each access"
        };

        static readonly Descriptor AlphamapTexturesDescriptor = new Descriptor(
            CDE0111,
            "Terrain: TerrainData.alphamapTextures Allocates",
            Impact.Memory,
            "<b>TerrainData.alphamapTextures</b> allocates a fresh <b>Texture2D[]</b> on every access.",
            "Read individual entries with <b>TerrainData.GetAlphamapTexture(int index)</b>, or cache the array during initialization."
        )
        {
            MessageFormat = "{0} allocates a managed array on each access"
        };

        static readonly Descriptor TerrainCachedArrayDescriptor = new Descriptor(
            CDE0185,
            "Terrain: Array Properties Allocate",
            Impact.Memory,
            "Terrain array properties (<b>Terrain.activeTerrains</b>, <b>TerrainData.detailPrototypes</b>, <b>TerrainData.treePrototypes</b>, <b>TerrainData.terrainLayers</b>) allocate a fresh managed array on every access.",
            "Cache the terrain array property during initialization and reuse it across frames."
        )
        {
            MessageFormat = "{0} allocates a managed array on each access"
        };

        static readonly Descriptor SplatPrototypesDescriptor = new Descriptor(
            CDE0188,
            "Terrain: TerrainData.splatPrototypes Allocates",
            Impact.Memory,
            "<b>TerrainData.splatPrototypes</b> is deprecated and allocates a fresh <b>SplatPrototype[]</b> on every access.",
            "Migrate to <b>TerrainData.terrainLayers</b>. Where the legacy property is still needed, cache the result once during initialization."
        )
        {
            MessageFormat = "{0} allocates a managed array on each access"
        };

        static readonly Dictionary<string, Dictionary<string, Descriptor>> s_TypeToPropertyDescriptors = new Dictionary<string, Dictionary<string, Descriptor>>
        {
            {
                "UnityEngine.Terrain", new Dictionary<string, Descriptor>
                {
                    { "activeTerrains", TerrainCachedArrayDescriptor }
                }
            },
            {
                "UnityEngine.TerrainData", new Dictionary<string, Descriptor>
                {
                    { "treeInstances", TreeInstancesDescriptor },
                    { "alphamapTextures", AlphamapTexturesDescriptor },
                    { "detailPrototypes", TerrainCachedArrayDescriptor },
                    { "treePrototypes", TerrainCachedArrayDescriptor },
                    { "splatPrototypes", SplatPrototypesDescriptor },
                    { "terrainLayers", TerrainCachedArrayDescriptor }
                }
            }
        };

        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;
            var declaringType = methodReference.DeclaringType.FullName;

            // Check if it's one of our target types
            if (!s_TypeToPropertyDescriptors.TryGetValue(declaringType, out var propertyToDescriptor))
                return;

            // Property getters are compiled to methods starting with "get_"
            if (!methodReference.Name.StartsWith("get_"))
                return;

            // Extract the property name by removing "get_" prefix
            var propertyName = methodReference.Name.Substring("get_".Length);

            // Check if it's one of our target properties
            if (!propertyToDescriptor.TryGetValue(propertyName, out var descriptor))
                return;

            if (!context.IsDescriptorEnabled(descriptor))
                return;

            var typeName = methodReference.DeclaringType.Name;
            var diagnostic = Diagnostic.Create(AnalysisCategory.Code, descriptor.Id, $"{typeName}.{propertyName}");
            context.ReportIssue(diagnostic);
        }
    }
}
