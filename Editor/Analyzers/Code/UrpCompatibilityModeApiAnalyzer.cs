// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

#if PACKAGE_URP

using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Flags calls to URP scripting APIs that belong to Compatibility Mode (non-Render Graph path).
    /// These members are obsolete and only execute when the project defines
    /// <c>URP_COMPATIBILITY_MODE</c>; otherwise they compile but have no effect.
    /// </summary>
    sealed class UrpCompatibilityModeApiAnalyzer : CodeAnalyzer
    {
        internal const string CDE0271 = nameof(CDE0271);

        const string UrpNamespacePrefix = "UnityEngine.Rendering.Universal.";

        static readonly Descriptor Descriptor = new Descriptor(
            CDE0271,
            "URP: Compatibility Mode Rendering API Used",
            Impact.Correctness,
            "<b>URP Compatibility Mode</b> is the legacy non-Render Graph scripting path used when Render Graph is disabled. Unity has deprecated it: members marked for compatibility mode only run when the project defines <b>URP_COMPATIBILITY_MODE</b>. Without that define, calls compile but have no effect at runtime.",
            "Migrate custom <b>ScriptableRenderPass</b> and <b>ScriptableRendererFeature</b> code to the Render Graph API by overriding <b>RecordRenderGraph</b>. Remove the <b>URP_COMPATIBILITY_MODE</b> scripting define from <b>Project Settings &gt; Player</b> after migration."
        )
        {
            MessageFormat = "'{0}' in {1}.{2}() uses a deprecated URP Compatibility Mode API",
            DefaultSeverity = Severity.Major,
            DocumentationUrl = "https://docs.unity3d.com/Manual/urp/upgrade-guide-unity-6.html"
        };

        static readonly HashSet<string> s_CompatibilityApiKeys = BuildCompatibilityApiKeys();

        static readonly OpCode[] s_OpCodes =
        {
            Mono.Cecil.Cil.OpCodes.Call,
            Mono.Cecil.Cil.OpCodes.Callvirt,
            Mono.Cecil.Cil.OpCodes.Ldsfld,
            Mono.Cecil.Cil.OpCodes.Ldsflda,
        };

        public override IReadOnlyCollection<OpCode> OpCodes => s_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            string memberKey;
            string displayName;
            if (context.Instruction.OpCode == Mono.Cecil.Cil.OpCodes.Ldsfld || context.Instruction.OpCode == Mono.Cecil.Cil.OpCodes.Ldsflda)
            {
                var fieldReference = (FieldReference)context.Instruction.Operand;
                if (!TryBuildMemberKey(fieldReference.DeclaringType.FullName, fieldReference.Name, out memberKey, out displayName))
                    return;
            }
            else
            {
                var methodReference = (MethodReference)context.Instruction.Operand;
                if (!TryBuildMemberKey(methodReference.DeclaringType.FullName, methodReference.Name, out memberKey, out displayName))
                    return;
            }

            if (!s_CompatibilityApiKeys.Contains(memberKey))
                return;

            if (!context.IsDescriptorEnabled(Descriptor))
                return;

            var diagnostic = Diagnostic.Create(
                AnalysisCategory.Code,
                Descriptor.Id,
                displayName,
                context.CallingType,
                context.CallingMethod);
            context.ReportIssue(diagnostic);
        }

        static bool TryBuildMemberKey(string declaringTypeFullName, string memberName, out string memberKey, out string displayName)
        {
            memberKey = null;
            displayName = null;

            if (string.IsNullOrEmpty(declaringTypeFullName)
                || !declaringTypeFullName.StartsWith(UrpNamespacePrefix, System.StringComparison.Ordinal))
                return false;

            memberKey = declaringTypeFullName + "." + memberName;
            displayName = FormatDisplayName(declaringTypeFullName, memberName);
            return true;
        }

        static string FormatDisplayName(string declaringTypeFullName, string memberName)
        {
            var typeName = declaringTypeFullName.Substring(UrpNamespacePrefix.Length);
            if (memberName.StartsWith("get_", System.StringComparison.Ordinal))
                return typeName + "." + memberName.Substring("get_".Length);

            return typeName + "." + memberName;
        }

        static HashSet<string> BuildCompatibilityApiKeys()
        {
            var keys = new HashSet<string>();

            AddMembers(keys, "UnityEngine.Rendering.Universal.ScriptableRenderer",
                "get_cameraColorTargetHandle",
                "get_cameraDepthTargetHandle",
                "ConfigureCameraTarget",
                "Setup",
                "SetupLights");

            AddMembers(keys, "UnityEngine.Rendering.Universal.ScriptableRenderPass",
                "k_CameraTarget",
                "ConfigureColorStoreAction",
                "ConfigureColorStoreActions",
                "ConfigureDepthStoreAction",
                "ResetTarget",
                "ConfigureTarget",
                "ConfigureClear",
                "OnCameraSetup",
                "Configure",
                "OnFinishCameraStackRendering",
                "Execute",
                "Blit");

            AddMembers(keys, "UnityEngine.Rendering.Universal.ScriptableRendererFeature",
                "SetupRenderPasses");

            AddMembers(keys, "UnityEngine.Rendering.Universal.RenderingData",
                "IsCameraProjectionMatrixFlipped");

            return keys;
        }

        static void AddMembers(HashSet<string> keys, string declaringType, params string[] memberNames)
        {
            foreach (var memberName in memberNames)
                keys.Add(declaringType + "." + memberName);
        }
    }
}

#endif
