// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class MeshPropertyAllocatingAnalyzer : CodeAnalyzer
    {
        internal const string CDE0067 = nameof(CDE0067);
        internal const string CDE0068 = nameof(CDE0068);
        internal const string CDE0069 = nameof(CDE0069);
        internal const string CDE0070 = nameof(CDE0070);
        internal const string CDE0071 = nameof(CDE0071);
        internal const string CDE0072 = nameof(CDE0072);
        internal const string CDE0081 = nameof(CDE0081);
        internal const string CDE0082 = nameof(CDE0082);
        internal const string CDE0083 = nameof(CDE0083);

        static readonly Descriptor BoneWeightsDescriptor = new Descriptor(
            CDE0067,
            "Mesh.boneWeights Allocates",
            Impact.Memory,
            "<b>Mesh.boneWeights</b> allocates a fresh <b>BoneWeight[]</b> on every access.",
            "Use <b>Mesh.GetAllBoneWeights()</b>, which returns a <b>NativeArray&lt;BoneWeight1&gt;</b> view onto the existing data."
        )
        {
            MessageFormat = "Mesh.boneWeights allocates a managed array on each access"
        };

        static readonly Descriptor BindposesDescriptor = new Descriptor(
            CDE0068,
            "Mesh.bindposes Allocates",
            Impact.Memory,
            "<b>Mesh.bindposes</b> allocates a fresh <b>Matrix4x4[]</b> on every access.",
            "Use the <b>Mesh.GetBindposes(List&lt;Matrix4x4&gt;)</b> overload with a reusable list."
        )
        {
            MessageFormat = "Mesh.bindposes allocates a managed array on each access"
        };

        static readonly Descriptor VerticesDescriptor = new Descriptor(
            CDE0069,
            "Mesh.vertices Allocates",
            Impact.Memory,
            "<b>Mesh.vertices</b> allocates a fresh <b>Vector3[]</b> on every access.",
            "Use the <b>Mesh.GetVertices(List&lt;Vector3&gt;)</b> overload with a reusable list."
        )
        {
            MessageFormat = "Mesh.vertices allocates a managed array on each access"
        };

        static readonly Descriptor NormalsDescriptor = new Descriptor(
            CDE0070,
            "Mesh.normals Allocates",
            Impact.Memory,
            "<b>Mesh.normals</b> allocates a fresh <b>Vector3[]</b> on every access.",
            "Use the <b>Mesh.GetNormals(List&lt;Vector3&gt;)</b> overload with a reusable list."
        )
        {
            MessageFormat = "Mesh.normals allocates a managed array on each access"
        };

        static readonly Descriptor TangentsDescriptor = new Descriptor(
            CDE0071,
            "Mesh.tangents Allocates",
            Impact.Memory,
            "<b>Mesh.tangents</b> allocates a fresh <b>Vector4[]</b> on every access.",
            "Use the <b>Mesh.GetTangents(List&lt;Vector4&gt;)</b> overload with a reusable list."
        )
        {
            MessageFormat = "Mesh.tangents allocates a managed array on each access"
        };

        static readonly Descriptor UvDescriptor = new Descriptor(
            CDE0072,
            "Mesh: UV Channels Allocate",
            Impact.Memory,
            "The <b>Mesh.uv</b> / <b>Mesh.uv2</b> ... <b>Mesh.uv8</b> properties each allocate a fresh <b>Vector2[]</b> on every access.",
            "Use the <b>Mesh.GetUVs(int channel, List&lt;Vector2&gt;)</b> overload with a reusable list."
        )
        {
            MessageFormat = "Mesh.{0} allocates a managed array on each access"
        };

        static readonly Descriptor ColorsDescriptor = new Descriptor(
            CDE0081,
            "Mesh.colors Allocates",
            Impact.Memory,
            "<b>Mesh.colors</b> allocates a fresh <b>Color[]</b> on every access.",
            "Use the <b>Mesh.GetColors(List&lt;Color&gt;)</b> overload with a reusable list."
        )
        {
            MessageFormat = "Mesh.colors allocates a managed array on each access"
        };

        static readonly Descriptor Colors32Descriptor = new Descriptor(
            CDE0082,
            "Mesh.colors32 Allocates",
            Impact.Memory,
            "<b>Mesh.colors32</b> allocates a fresh <b>Color32[]</b> on every access.",
            "Use the <b>Mesh.GetColors(List&lt;Color32&gt;)</b> overload with a reusable list."
        )
        {
            MessageFormat = "Mesh.colors32 allocates a managed array on each access"
        };

        static readonly Descriptor TrianglesDescriptor = new Descriptor(
            CDE0083,
            "Mesh.triangles Allocates",
            Impact.Memory,
            "<b>Mesh.triangles</b> allocates a fresh <b>int[]</b> on every access.",
            "Use the <b>Mesh.GetTriangles(List&lt;int&gt;, int submesh)</b> overload with a reusable list."
        )
        {
            MessageFormat = "Mesh.triangles allocates a managed array on each access"
        };

        static readonly Dictionary<string, Descriptor> s_PropertyToDescriptor = new Dictionary<string, Descriptor>
        {
            { "boneWeights", BoneWeightsDescriptor },
            { "bindposes", BindposesDescriptor },
            { "vertices", VerticesDescriptor },
            { "normals", NormalsDescriptor },
            { "tangents", TangentsDescriptor },
            { "uv", UvDescriptor },
            { "uv1", UvDescriptor },
            { "uv2", UvDescriptor },
            { "uv3", UvDescriptor },
            { "uv4", UvDescriptor },
            { "uv5", UvDescriptor },
            { "uv6", UvDescriptor },
            { "uv7", UvDescriptor },
            { "uv8", UvDescriptor },
            { "colors", ColorsDescriptor },
            { "colors32", Colors32Descriptor },
            { "triangles", TrianglesDescriptor }
        };

        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;

            // Check if it's UnityEngine.Mesh type
            if (methodReference.DeclaringType.FullName != "UnityEngine.Mesh")
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

            var diagnostic = descriptor == UvDescriptor
                ? Diagnostic.Create(AnalysisCategory.Code, descriptor.Id, propertyName)
                : Diagnostic.Create(AnalysisCategory.Code, descriptor.Id);
            context.ReportIssue(diagnostic);
        }
    }
}

