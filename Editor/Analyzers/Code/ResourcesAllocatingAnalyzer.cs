// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class ResourcesAllocatingAnalyzer : CodeAnalyzer
    {
        internal const string CDE0004 = nameof(CDE0004);
        internal const string RES0004 = nameof(RES0004);
        internal const string RES0005 = nameof(RES0005);

        static readonly Descriptor FindObjectsOfTypeAllDescriptor = new Descriptor(
            CDE0004,
            "Resources: FindObjectsOfTypeAll Scans Everything",
            Impact.Memory,
            "<b>Resources.FindObjectsOfTypeAll()</b> walks every loaded object of the requested type, including hidden and editor-only ones, and returns the matches in a freshly allocated array.",
            "Cache the result during initialization and reuse the array. Where a narrow search is sufficient, prefer <b>Object.FindObjectsByType</b> with explicit sorting and include flags."
        )
        {
            MessageFormat = "'{0}' allocates a managed array and searches all loaded objects"
        };

        static readonly Descriptor LoadAllDescriptor = new Descriptor(
            RES0004,
            "Resources: LoadAll Loads All Matching Assets",
            Impact.Memory,
            "<b>Resources.LoadAll()</b> loads every matching asset under the supplied path into memory and returns them in a freshly allocated managed array.",
            "Call <b>Resources.LoadAll()</b> once during initialization and cache the returned array. Migrate runtime content to Addressables to load only the assets actually needed."
        )
        {
            MessageFormat = "'{0}' loads all matching assets into a fresh array"
        };

        static readonly Descriptor LoadDescriptor = new Descriptor(
            RES0005,
            "Resources: Resources.Load Usage",
            Impact.BuildSize | Impact.Memory | Impact.LoadTime | Impact.Quality,
            "<b>Resources.Load</b> uses string paths into a <b>Resources/</b> folder whose entire contents are forced into every build. The load is synchronous and the path-based lookup is not refactor-safe.",
            "Replace runtime <b>Resources.Load</b> calls with serialized references or Addressables. Reserve <b>Resources/</b> for small bootstrap and configuration assets that genuinely need static loading."
        )
        {
            MessageFormat = "'{0}' uses Resources.Load"
        };

        static readonly Dictionary<string, Descriptor> s_MethodToDescriptor = new Dictionary<string, Descriptor>
        {
            { "FindObjectsOfTypeAll", FindObjectsOfTypeAllDescriptor },
            { "LoadAll", LoadAllDescriptor },
            { "Load", LoadDescriptor }
        };

        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;

            // Check if it's UnityEngine.Resources type
            if (methodReference.DeclaringType.FullName != "UnityEngine.Resources")
                return;

            // Check if it's one of our target methods
            if (!s_MethodToDescriptor.TryGetValue(methodReference.Name, out var descriptor))
                return;

            if (!context.IsDescriptorEnabled(descriptor))
                return;

            var methodSignature = BuildMethodSignature(methodReference);
            var diagnostic = Diagnostic.Create(AnalysisCategory.Code, descriptor.Id, methodSignature);
            context.ReportIssue(diagnostic);
        }

        static string BuildMethodSignature(MethodReference methodReference)
        {
            var typeName = methodReference.DeclaringType.Name;
            var memberName = methodReference.Name;

            var genericInstanceMethod = methodReference as GenericInstanceMethod;
            if (genericInstanceMethod != null && genericInstanceMethod.HasGenericArguments)
            {
                var genericTypeNames = genericInstanceMethod.GenericArguments.Select(a => a.Name).ToArray();
                return $"{typeName}.{memberName}<{string.Join(", ", genericTypeNames)}>";
            }

            return $"{typeName}.{memberName}";
        }
    }
}
