// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Flags calls to URP's <c>GetUniversalAdditionalCameraData()</c> /
    /// <c>GetUniversalAdditionalLightData()</c> helpers, which silently <c>AddComponent</c> the
    /// additional-data component if it is missing. The side effect is benign at runtime but is
    /// a real bug in editor analyzers, asset post-processors, and any other read-only
    /// inspection of a Camera / Light.
    /// </summary>
    sealed class UrpAdditionalDataSideEffectAnalyzer : CodeAnalyzer
    {
        internal const string CDE0101 = nameof(CDE0101);
        internal const string CDE0102 = nameof(CDE0102);

        static readonly Descriptor AdditionalCameraDataDescriptor = new Descriptor(
            CDE0102,
            "URP: GetUniversalAdditionalCameraData Has a Side Effect",
            Impact.Correctness,
            "<b>Camera.GetUniversalAdditionalCameraData()</b> attaches a <b>UniversalAdditionalCameraData</b> component to the Camera's GameObject when one is not already present. The implicit <b>AddComponent</b> mutates the scene or prefab, which is unsafe in editor analyzers, asset post-processors, and other read-only inspection code.",
            "Use <b>Camera.TryGetComponent&lt;UniversalAdditionalCameraData&gt;(out var data)</b> for read-only access and treat the absent case as defaults. Reserve <b>GetUniversalAdditionalCameraData()</b> for runtime code that intentionally wants the component attached."
        )
        {
            MessageFormat = "'{0}' in {1}.{2}() may AddComponent<UniversalAdditionalCameraData> as a side effect"
        };

        static readonly Descriptor AdditionalLightDataDescriptor = new Descriptor(
            CDE0101,
            "URP: GetUniversalAdditionalLightData Has a Side Effect",
            Impact.Correctness,
            "<b>Light.GetUniversalAdditionalLightData()</b> attaches a <b>UniversalAdditionalLightData</b> component to the Light's GameObject when one is not already present. The implicit <b>AddComponent</b> mutates the scene or prefab, which is unsafe in editor analyzers, asset post-processors, and other read-only inspection code.",
            "Use <b>Light.TryGetComponent&lt;UniversalAdditionalLightData&gt;(out var data)</b> for read-only access and treat the absent case as defaults. Reserve <b>GetUniversalAdditionalLightData()</b> for runtime code that intentionally wants the component attached."
        )
        {
            MessageFormat = "'{0}' in {1}.{2}() may AddComponent<UniversalAdditionalLightData> as a side effect"
        };

        static readonly OpCode[] s_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };
        public override IReadOnlyCollection<OpCode> OpCodes => s_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;
            var methodName = methodReference.Name;

            Descriptor descriptor;
            string displayName;
            if (methodName == "GetUniversalAdditionalCameraData")
            {
                descriptor = AdditionalCameraDataDescriptor;
                displayName = "Camera.GetUniversalAdditionalCameraData";
            }
            else if (methodName == "GetUniversalAdditionalLightData")
            {
                descriptor = AdditionalLightDataDescriptor;
                displayName = "Light.GetUniversalAdditionalLightData";
            }
            else
            {
                return;
            }

            // Both modern (extension class) and older (direct on Camera/Light) URP shapes.
            var declaringType = methodReference.DeclaringType.FullName;
            if (declaringType != "UnityEngine.Rendering.Universal.CameraExtensions" &&
                declaringType != "UnityEngine.Rendering.Universal.LightExtensions" &&
                declaringType != "UnityEngine.Camera" &&
                declaringType != "UnityEngine.Light")
                return;

            if (!context.IsDescriptorEnabled(descriptor))
                return;

            var diagnostic = Diagnostic.Create(AnalysisCategory.Code, descriptor.Id, displayName, context.CallingType, context.CallingMethod);
            context.ReportIssue(diagnostic);
        }
    }
}
