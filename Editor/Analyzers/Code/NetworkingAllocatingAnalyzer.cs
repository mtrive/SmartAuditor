// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Analyzes code for usage of Unity Networking properties that allocate managed memory.
    /// </summary>
    sealed class NetworkingAllocatingAnalyzer : CodeAnalyzer
    {
        internal const string CDE0200 = nameof(CDE0200);

        static readonly Descriptor NetworkingDataPropertyDescriptor = new Descriptor(
            CDE0200,
            "Networking: Payload Data Allocates",
            Impact.Memory,
            "Networking payload properties (<b>DownloadHandler.data</b>, <b>UploadHandler.data</b>, <b>WWWForm.data</b>, <b>WWW.bytes</b>, <b>IMultipartFormSection.sectionData</b>) allocate a fresh <b>byte[]</b> on every access.",
            "Read the property once when the request completes and cache the byte array; reuse the cached copy thereafter."
        )
        {
            MessageFormat = "'{0}' allocates a managed byte array on each access"
        };

        static readonly HashSet<(string DeclaringType, string PropertyName)> s_TargetProperties = new HashSet<(string DeclaringType, string PropertyName)>
        {
            ("UnityEngine.Networking.IMultipartFormSection", "sectionData"),
            ("UnityEngine.Networking.MultipartFormDataSection", "sectionData"),
            ("UnityEngine.Networking.MultipartFormFileSection", "sectionData"),
            ("UnityEngine.WWWForm", "data"),
            ("UnityEngine.Networking.DownloadHandler", "data"),
            ("UnityEngine.Networking.UploadHandler", "data"),
            ("UnityEngine.WWW", "bytes")
        };

        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;
            var declaringType = methodReference.DeclaringType.FullName;

            // Property getters are compiled to methods starting with "get_"
            if (!methodReference.Name.StartsWith("get_"))
                return;

            // Extract the property name by removing "get_" prefix
            var propertyName = methodReference.Name.Substring("get_".Length);

            if (!s_TargetProperties.Contains((declaringType, propertyName)))
                return;

            if (!context.IsDescriptorEnabled(NetworkingDataPropertyDescriptor))
                return;

            var typeName = methodReference.DeclaringType.Name;
            var diagnostic = Diagnostic.Create(AnalysisCategory.Code, NetworkingDataPropertyDescriptor.Id, $"{typeName}.{propertyName}");
            context.ReportIssue(diagnostic);
        }
    }
}
