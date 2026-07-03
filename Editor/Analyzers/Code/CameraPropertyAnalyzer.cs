// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class CameraPropertyAnalyzer : CodeAnalyzer
    {
        internal const string CDE0000 = nameof(CDE0000);
        internal const string CDE0066 = nameof(CDE0066);
        internal const string CDE0153 = nameof(CDE0153);

        static readonly Descriptor MainDescriptor = new Descriptor(
            CDE0000,
            "Camera.main Uncached Lookup",
            Impact.Performance,
            "<b>Camera.main</b> calls <b>GameObject.FindGameObjectsWithTag(\"MainCamera\")</b> internally on every access without caching the result. The scene-wide tag scan is too slow for frequently-updated code.",
            "Cache the <b>Camera</b> reference in <b>Awake</b> or <b>Start</b> and use the cached value thereafter."
        )
        {
            MessageFormat = "Camera.main performs an uncached scene lookup on each access",
            MaximumVersion = "2019.4.8"
        };

        static readonly Descriptor AllCamerasDescriptor = new Descriptor(
            CDE0066,
            "Camera.allCameras Allocates",
            Impact.Memory,
            "<b>Camera.allCameras</b> allocates a fresh <b>Camera[]</b> on every access.",
            "Use <b>Camera.GetAllCameras(Camera[])</b> with a pre-allocated array of size <b>Camera.allCamerasCount</b>."
        )
        {
            MessageFormat = "Camera.allCameras allocates a managed Camera array on each access"
        };

        static readonly Descriptor LayerCullDistancesDescriptor = new Descriptor(
            CDE0153,
            "Camera.layerCullDistances Allocates",
            Impact.Memory,
            "<b>Camera.layerCullDistances</b> allocates a fresh 32-element <b>float[]</b> on every access.",
            "Cache the result of <b>Camera.layerCullDistances</b> during initialization and reuse the array."
        )
        {
            MessageFormat = "Camera.layerCullDistances allocates a managed float array on each access"
        };

        static readonly Dictionary<string, Descriptor> s_PropertyToDescriptor = new Dictionary<string, Descriptor>
        {
            { "main", MainDescriptor },
            { "allCameras", AllCamerasDescriptor },
            { "layerCullDistances", LayerCullDistancesDescriptor }
        };

        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;

            // Check if it's UnityEngine.Camera type
            if (methodReference.DeclaringType.FullName != "UnityEngine.Camera")
                return;

            // Property getters are compiled to methods starting with "get_"
            if (!methodReference.Name.StartsWith("get_"))
                return;

            // Extract the property name by removing "get_" prefix
            var propertyName = methodReference.Name.Substring("get_".Length);

            // Check if it's one of our target properties
            if (!s_PropertyToDescriptor.TryGetValue(propertyName, out var descriptor))
                return;

            if (!context.IsDescriptorEnabled(descriptor))
                return;

            var diagnostic = Diagnostic.Create(AnalysisCategory.Code, descriptor.Id);
            context.ReportIssue(diagnostic);
        }
    }
}
