using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.CodeAnalysis;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class ObjectInstantiationApiAnalyzer : CodeAnalyzer
    {
        internal const string CDE0224 = nameof(CDE0224);
        internal const string CDE0225 = nameof(CDE0225);
        internal const string CDE0039 = nameof(CDE0039);

        static readonly Descriptor InstantiateDescriptor = new Descriptor(
            CDE0224,
            "Allocation: Object.Instantiate at Runtime",
            Impact.Performance | Impact.Memory,
            "<b>Object.Instantiate</b> performs a full deep copy of the source object and its children at runtime, allocating managed memory and spending CPU on component cloning. Calling it from hot code produces visible frame spikes.",
            "Pre-instantiate the prefab pool during initialization and recycle instances at runtime. Avoid <b>Object.Instantiate</b> in <b>Update</b>, <b>FixedUpdate</b>, or other per-frame paths.")
        {
            MessageFormat = "{0} allocates managed memory and may cause frame spikes"
        };

        static readonly Descriptor AddComponentDescriptor = new Descriptor(
            CDE0225,
            "Allocation: GameObject.AddComponent at Runtime",
            Impact.Performance | Impact.Memory,
            "<b>GameObject.AddComponent</b> allocates the component and performs a managed-to-native registration on every call. Calling it from hot code produces visible frame spikes.",
            "Author prefabs with the required components already attached and instantiate the prefab instead of calling <b>GameObject.AddComponent</b> at runtime.")
        {
            MessageFormat = "GameObject.AddComponent is called at runtime"
        };

        static readonly Descriptor RendererMaterialDescriptor = new Descriptor(
            CDE0039,
            "Renderer.material Creates Instance",
            Impact.Performance,
            "<b>Renderer.material</b> returns a unique instance of the assigned material on first access, breaking GPU instancing and SRP Batcher batching for that renderer.",
            "Read materials via <b>Renderer.sharedMaterial</b>. Access <b>Renderer.material</b> only when a per-renderer mutable instance is intentionally required; otherwise drive per-renderer values with a <b>MaterialPropertyBlock</b>.")
        {
            MessageFormat = "'{0}' in {1}.{2}() creates a unique material instance"
        };

        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;
            var declaringType = methodReference.DeclaringType.FullName;
            var methodName = methodReference.Name;

            if (methodName == "Instantiate" && MonoCecilHelper.IsOrInheritedFrom(methodReference.DeclaringType, "UnityEngine.Object"))
            {
                if (!context.IsDescriptorEnabled(InstantiateDescriptor))
                    return;

                var methodSignature = BuildMethodSignature(methodReference);
                var diagnostic = Diagnostic.Create(AnalysisCategory.Code, InstantiateDescriptor.Id, methodSignature);
                context.ReportIssue(diagnostic);
                return;
            }

            if (declaringType == "UnityEngine.GameObject" && methodName == "AddComponent")
            {
                if (!context.IsDescriptorEnabled(AddComponentDescriptor))
                    return;

                var diagnostic = Diagnostic.Create(AnalysisCategory.Code, AddComponentDescriptor.Id);
                context.ReportIssue(diagnostic);
                return;
            }

            if (declaringType == "UnityEngine.Renderer" && methodName == "get_material")
            {
                if (!context.IsDescriptorEnabled(RendererMaterialDescriptor))
                    return;

                var diagnostic = Diagnostic.Create(AnalysisCategory.Code, RendererMaterialDescriptor.Id, "Renderer.material", context.CallingType, context.CallingMethod);
                context.ReportIssue(diagnostic);
            }
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
