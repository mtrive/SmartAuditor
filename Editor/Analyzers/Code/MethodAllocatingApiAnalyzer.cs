using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.CodeAnalysis;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class MethodAllocatingApiAnalyzer : CodeAnalyzer
    {
        internal const string CDE0129 = nameof(CDE0129);
        internal const string CDE0234 = nameof(CDE0234);
        internal const string CDE0136 = nameof(CDE0136);
        internal const string CDE0132 = nameof(CDE0132);

        static readonly Descriptor FindObjectsOfTypeDescriptor = new Descriptor(
            CDE0129,
            "Object: FindObjectsOfType Scans the Whole Scene",
            Impact.Performance | Impact.Memory,
            "<b>Object.FindObjectsOfType()</b> walks every loaded object of the requested type and returns the matches in a freshly allocated managed array.",
            "Cache the result during initialization and reuse it. Where a narrow search is sufficient, prefer <b>Object.FindObjectsByType</b> with explicit include flags.")
        {
            MessageFormat = "'{0}' searches all loaded objects and allocates a managed array"
        };

        static readonly Descriptor FindObjectOfTypeDescriptor = new Descriptor(
            CDE0234,
            "Object: FindObjectOfType Scans the Whole Scene",
            Impact.Performance | Impact.Memory,
            "<b>Object.FindObjectOfType()</b> walks every loaded object of the requested type until it finds a match.",
            "Hold a serialized reference to the target, or cache the lookup during initialization. Where a one-off lookup is acceptable, use <b>Object.FindAnyObjectByType</b> with explicit include flags.")
        {
            MessageFormat = "'{0}' searches all loaded objects and allocates managed memory"
        };

        static readonly Descriptor CacheResultMethodDescriptor = new Descriptor(
            CDE0136,
            "Allocation: Cacheable Method Allocates",
            Impact.Memory,
            "The call site invokes a method that allocates managed memory on every call (for example <b>Windows.File.ReadAllBytes</b> or <b>SpriteShapeUtility.Generate</b>).",
            "Call the method once during initialization or at a deliberate refresh point and cache the result.")
        {
            MessageFormat = "'{0}' allocates managed memory"
        };

        static readonly Descriptor ImageEncodeDescriptor = new Descriptor(
            CDE0132,
            "Texture: ImageConversion.EncodeTo* Allocates",
            Impact.Memory,
            "<b>ImageConversion.EncodeToPNG</b>, <b>EncodeToJPG</b>, <b>EncodeToTGA</b>, and <b>EncodeToEXR</b> all return a fresh managed <b>byte[]</b> containing the encoded image data.",
            "Call <b>ImageConversion.EncodeTo*</b> off the hot path. Cache the encoded byte array where reuse is possible.")
        {
            MessageFormat = "ImageConversion.{0} allocates a managed byte array"
        };

        class MethodRule
        {
            public readonly string TypeName;
            public readonly string MethodName;
            public readonly Descriptor Descriptor;

            public MethodRule(string typeName, string methodName, Descriptor descriptor)
            {
                TypeName = typeName;
                MethodName = methodName;
                Descriptor = descriptor;
            }
        }

        static readonly HashSet<string> s_ImageEncodeMethods = new HashSet<string>
        {
            "EncodeToJPG",
            "EncodeToEXR",
            "EncodeToTGA",
            "EncodeToPNG"
        };

        static readonly MethodRule[] s_Rules =
        {
            new MethodRule("UnityEngine.Object", "FindObjectsOfType", FindObjectsOfTypeDescriptor),
            new MethodRule("UnityEngine.Object", "FindObjectOfType", FindObjectOfTypeDescriptor),
            new MethodRule("UnityEngine.Windows.Crypto", "ComputeMD5Hash", CacheResultMethodDescriptor),
            new MethodRule("UnityEngine.Windows.File", "ReadAllBytes", CacheResultMethodDescriptor),
            new MethodRule("UnityEngine.U2D.SpriteShapeUtility", "Generate", CacheResultMethodDescriptor)
        };

        static readonly Dictionary<string, List<MethodRule>> s_RulesByMethod = BuildRuleLookup();
        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;
            var methodName = methodReference.Name;

            if (s_ImageEncodeMethods.Contains(methodName))
            {
                if (!context.IsDescriptorEnabled(ImageEncodeDescriptor))
                    return;

                var diagnostic = Diagnostic.Create(AnalysisCategory.Code, ImageEncodeDescriptor.Id, methodName);
                context.ReportIssue(diagnostic);
                return;
            }

            if (!s_RulesByMethod.TryGetValue(methodName, out var candidateRules))
                return;

            foreach (var rule in candidateRules)
            {
                if (!MonoCecilHelper.IsOrInheritedFrom(methodReference.DeclaringType, rule.TypeName))
                    continue;

                if (!context.IsDescriptorEnabled(rule.Descriptor))
                    return;

                var methodSignature = BuildMethodSignature(methodReference);
                var diagnostic = Diagnostic.Create(AnalysisCategory.Code, rule.Descriptor.Id, methodSignature);
                context.ReportIssue(diagnostic);
                return;
            }
        }

        static Dictionary<string, List<MethodRule>> BuildRuleLookup()
        {
            var lookup = new Dictionary<string, List<MethodRule>>();
            foreach (var rule in s_Rules)
            {
                if (!lookup.TryGetValue(rule.MethodName, out var rules))
                {
                    rules = new List<MethodRule>();
                    lookup[rule.MethodName] = rules;
                }

                rules.Add(rule);
            }

            return lookup;
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
