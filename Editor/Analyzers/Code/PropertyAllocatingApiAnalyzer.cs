using System;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.CodeAnalysis;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class PropertyAllocatingApiAnalyzer : CodeAnalyzer
    {
        internal const string CDE0059 = nameof(CDE0059);
        internal const string CDE0219 = nameof(CDE0219);
        internal const string CDE0231 = nameof(CDE0231);

        static readonly Descriptor CacheResultPropertyDescriptor = new Descriptor(
            CDE0059,
            "Allocation: Cacheable Property Allocates",
            Impact.Memory,
            "The call site reads a property that allocates a fresh managed array or string on every access (for example <b>QualitySettings.names</b>, <b>Screen.resolutions</b>, <b>WebCamTexture.devices</b>).",
            "Read the property once during initialization and cache the result.")
        {
            DefaultSeverity = Severity.Minor,
            MessageFormat = "'{0}' allocates managed memory"
        };

        static readonly Descriptor NavMeshPathCornersDescriptor = new Descriptor(
            CDE0219,
            "NavMesh: NavMeshPath.corners Allocates",
            Impact.Memory,
            "<b>AI.NavMeshPath.corners</b> allocates a fresh <b>Vector3[]</b> on every access.",
            "Call <b>AI.NavMeshPath.GetCornersNonAlloc(Vector3[])</b> with a reusable buffer.")
        {
            DefaultSeverity = Severity.Minor,
            MessageFormat = "NavMeshPath.corners allocates managed memory"
        };

        static readonly Descriptor ParentConstraintTranslationOffsetsDescriptor = new Descriptor(
            "CDE0061",
            "Animation: ParentConstraint.translationOffsets Allocates",
            Impact.Memory,
            "<b>Animations.ParentConstraint.translationOffsets</b> allocates a fresh <b>Vector3[]</b> on every access.",
            "Read individual offsets with <b>Animations.ParentConstraint.GetTranslationOffset(int sourceIndex)</b>.")
        {
            DefaultSeverity = Severity.Minor,
            MessageFormat = "ParentConstraint.translationOffsets allocates managed memory"
        };

        static readonly Descriptor ParentConstraintRotationOffsetsDescriptor = new Descriptor(
            "CDE0062",
            "Animation: ParentConstraint.rotationOffsets Allocates",
            Impact.Memory,
            "<b>Animations.ParentConstraint.rotationOffsets</b> allocates a fresh <b>Vector3[]</b> on every access.",
            "Read individual offsets with <b>Animations.ParentConstraint.GetRotationOffset(int sourceIndex)</b>.")
        {
            DefaultSeverity = Severity.Minor,
            MessageFormat = "ParentConstraint.rotationOffsets allocates managed memory"
        };

        static readonly Descriptor AnimationCurveKeysDescriptor = new Descriptor(
            "CDE0063",
            "Animation: AnimationCurve.keys Allocates",
            Impact.Memory,
            "<b>AnimationCurve.keys</b> allocates a fresh <b>Keyframe[]</b> on every access.",
            "Mutate the curve with <b>AnimationCurve.AddKey</b>, <b>AnimationCurve.MoveKey</b>, or <b>AnimationCurve.RemoveKey</b>. Cache the array on the rare paths that need it whole.")
        {
            DefaultSeverity = Severity.Minor,
            MessageFormat = "AnimationCurve.keys allocates managed memory"
        };

        static readonly Descriptor InputTouchesDescriptor = new Descriptor(
            "CDE0094",
            "Input.touches Allocates",
            Impact.Memory,
            "<b>Input.touches</b> allocates a fresh <b>Touch[]</b> on every access.",
            "Iterate touches with <b>Input.GetTouch(int index)</b> up to <b>Input.touchCount</b>.")
        {
            DefaultSeverity = Severity.Minor,
            MessageFormat = "Input.touches allocates managed memory"
        };

        static readonly Descriptor InputAccelerationEventsDescriptor = new Descriptor(
            "CDE0095",
            "Input.accelerationEvents Allocates",
            Impact.Memory,
            "<b>Input.accelerationEvents</b> allocates a fresh <b>AccelerationEvent[]</b> on every access.",
            "Iterate events with <b>Input.GetAccelerationEvent(int index)</b> up to <b>Input.accelerationEventCount</b>.")
        {
            DefaultSeverity = Severity.Minor,
            MessageFormat = "Input.accelerationEvents allocates managed memory"
        };

        static readonly Descriptor GuiSkinCustomStylesDescriptor = new Descriptor(
            "CDE0100",
            "IMGUI: GUISkin.customStyles Allocates",
            Impact.Memory,
            "<b>GUISkin.customStyles</b> allocates a fresh <b>GUIStyle[]</b> on every access.",
            "Look up styles by name with <b>GUISkin.GetStyle(string)</b> or <b>GUISkin.FindStyle(string)</b>.")
        {
            DefaultSeverity = Severity.Minor,
            MessageFormat = "GUISkin.customStyles allocates managed memory"
        };

        static readonly Descriptor FontCharacterInfoDescriptor = new Descriptor(
            "CDE0112",
            "Font.characterInfo Allocates",
            Impact.Memory,
            "<b>Font.characterInfo</b> allocates a fresh <b>CharacterInfo[]</b> on every access.",
            "Look up glyph info with <b>Font.GetCharacterInfo(char, out CharacterInfo)</b>.")
        {
            DefaultSeverity = Severity.Minor,
            MessageFormat = "Font.characterInfo allocates managed memory"
        };

        static readonly Descriptor ObjectNameDescriptor = new Descriptor(
            CDE0231,
            "Object.name Allocates",
            Impact.Memory,
            "<b>UnityEngine.Object.name</b> marshals the name back from native code and returns a fresh managed string on every access.",
            "Cache <b>Object.name</b> during initialization and reuse the cached string. For comparison, use <b>GameObject.CompareTag</b> or store a stable identifier alongside the object.")
        {
            DefaultSeverity = Severity.Minor,
            MessageFormat = "'Object.name' allocates a managed string on each access"
        };

        class PropertyRule
        {
            public readonly string TypeName;
            public readonly string MemberName;
            public readonly Descriptor Descriptor;
            public readonly bool UsePropertyNameArgument;

            public PropertyRule(string typeName, string memberName, Descriptor descriptor, bool usePropertyNameArgument = false)
            {
                TypeName = typeName;
                MemberName = memberName;
                Descriptor = descriptor;
                UsePropertyNameArgument = usePropertyNameArgument;
            }
        }

        static readonly PropertyRule[] s_Rules =
        {
            new PropertyRule("UnityEngine.AI.NavMeshPath", "corners", NavMeshPathCornersDescriptor),
            new PropertyRule("UnityEngine.Animations.ParentConstraint", "translationOffsets", ParentConstraintTranslationOffsetsDescriptor),
            new PropertyRule("UnityEngine.Animations.ParentConstraint", "rotationOffsets", ParentConstraintRotationOffsetsDescriptor),
            new PropertyRule("UnityEngine.AnimationCurve", "keys", AnimationCurveKeysDescriptor),
            new PropertyRule("UnityEngine.Input", "touches", InputTouchesDescriptor),
            new PropertyRule("UnityEngine.Input", "accelerationEvents", InputAccelerationEventsDescriptor),
            new PropertyRule("UnityEngine.GUISkin", "customStyles", GuiSkinCustomStylesDescriptor),
            new PropertyRule("UnityEngine.Font", "characterInfo", FontCharacterInfoDescriptor),
            new PropertyRule("UnityEngine.AnimationClip", "events", CacheResultPropertyDescriptor, usePropertyNameArgument: true),
            new PropertyRule("UnityEngine.AnimatorOverrideController", "animationClips", CacheResultPropertyDescriptor, usePropertyNameArgument: true),
            new PropertyRule("UnityEngine.HumanTrait", "MuscleName", CacheResultPropertyDescriptor, usePropertyNameArgument: true),
            new PropertyRule("UnityEngine.HumanTrait", "BoneName", CacheResultPropertyDescriptor, usePropertyNameArgument: true),
            new PropertyRule("UnityEngine.RuntimeAnimatorController", "animationClips", CacheResultPropertyDescriptor, usePropertyNameArgument: true),
            new PropertyRule("UnityEngine.Microphone", "devices", CacheResultPropertyDescriptor, usePropertyNameArgument: true),
            new PropertyRule("UnityEngine.WebCamDevice", "availableResolutions", CacheResultPropertyDescriptor, usePropertyNameArgument: true),
            new PropertyRule("UnityEngine.WebCamTexture", "devices", CacheResultPropertyDescriptor, usePropertyNameArgument: true),
            new PropertyRule("UnityEngine.CrashReport", "reports", CacheResultPropertyDescriptor, usePropertyNameArgument: true),
            new PropertyRule("UnityEngine.Gradient", "colorKeys", CacheResultPropertyDescriptor, usePropertyNameArgument: true),
            new PropertyRule("UnityEngine.Gradient", "alphaKeys", CacheResultPropertyDescriptor, usePropertyNameArgument: true),
            new PropertyRule("UnityEngine.Screen", "resolutions", CacheResultPropertyDescriptor, usePropertyNameArgument: true),
            new PropertyRule("UnityEngine.LightmapSettings", "lightmaps", CacheResultPropertyDescriptor, usePropertyNameArgument: true),
            new PropertyRule("UnityEngine.QualitySettings", "names", CacheResultPropertyDescriptor, usePropertyNameArgument: true),
            new PropertyRule("UnityEngine.Light", "layerShadowCullDistances", CacheResultPropertyDescriptor, usePropertyNameArgument: true),
            new PropertyRule("UnityEngine.Network", "connections", CacheResultPropertyDescriptor, usePropertyNameArgument: true),
            new PropertyRule("UnityEngine.HostData", "ip", CacheResultPropertyDescriptor, usePropertyNameArgument: true),
            new PropertyRule("UnityEngine.SortingLayer", "layers", CacheResultPropertyDescriptor, usePropertyNameArgument: true),
            new PropertyRule("UnityEngine.GUIStyleState", "scaledBackgrounds", CacheResultPropertyDescriptor, usePropertyNameArgument: true),
            new PropertyRule("UnityEngine.Tilemaps.TileAnimationData", "animatedSprites", CacheResultPropertyDescriptor, usePropertyNameArgument: true),
            new PropertyRule("UnityEngine.UIElements.UxmlAttributeDescription", "obsoleteNames", CacheResultPropertyDescriptor, usePropertyNameArgument: true),
            new PropertyRule("UnityEngine.XR.XRSettings", "supportedDevices", CacheResultPropertyDescriptor, usePropertyNameArgument: true),
            new PropertyRule("UnityEngine.Object", "name", ObjectNameDescriptor)
        };

        static readonly Dictionary<string, List<PropertyRule>> s_RulesByMember = BuildRuleLookup();
        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;
            var methodName = methodReference.Name;
            if (!methodName.StartsWith("get_", StringComparison.Ordinal))
                return;

            var propertyName = methodName.Substring("get_".Length);
            if (!s_RulesByMember.TryGetValue(propertyName, out var candidateRules))
                return;

            foreach (var rule in candidateRules)
            {
                if (!MonoCecilHelper.IsOrInheritedFrom(methodReference.DeclaringType, rule.TypeName))
                    continue;

                if (!context.IsDescriptorEnabled(rule.Descriptor))
                    return;

                var diagnostic = rule.UsePropertyNameArgument
                    ? Diagnostic.Create(AnalysisCategory.Code, rule.Descriptor.Id, $"{methodReference.DeclaringType.Name}.{propertyName}")
                    : Diagnostic.Create(AnalysisCategory.Code, rule.Descriptor.Id);
                context.ReportIssue(diagnostic);
                return;
            }
        }

        static Dictionary<string, List<PropertyRule>> BuildRuleLookup()
        {
            var lookup = new Dictionary<string, List<PropertyRule>>(StringComparer.Ordinal);
            foreach (var rule in s_Rules)
            {
                if (!lookup.TryGetValue(rule.MemberName, out var rules))
                {
                    rules = new List<PropertyRule>();
                    lookup[rule.MemberName] = rules;
                }

                rules.Add(rule);
            }

            return lookup;
        }
    }
}
