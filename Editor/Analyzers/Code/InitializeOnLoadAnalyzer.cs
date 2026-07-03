// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;
using UnityEngine;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class InitializeOnLoadAnalyzer : CodeAnalyzer
    {
        internal const string CDE2007 = nameof(CDE2007);
        internal const string CDE2008 = nameof(CDE2008);
        internal const string CDE2009 = nameof(CDE2009);
        internal const string CDE2010 = nameof(CDE2010);
        internal const string CDE2011 = nameof(CDE2011);

        static readonly int k_InitializeOnLoadAttributeHashCode = "UnityEditor.InitializeOnLoadAttribute".GetHashCode();
        static readonly int k_InitializeOnLoadMethodAttributeHashCode = "UnityEditor.InitializeOnLoadMethodAttribute".GetHashCode();

        static readonly Descriptor InitializeOnLoadTypeDescriptor = new Descriptor
        (
            CDE2007,
            "Editor: [InitializeOnLoad] Runs on Every Domain Reload",
            Impact.EditorWorkflow,
            "Static constructors of classes marked with <b>[InitializeOnLoad]</b> run on every editor domain reload and project load. Heavy initialization in such a constructor compounds across enter-play-mode and recompile cycles.",
            "Defer expensive work out of the static constructor: keep the entry point lightweight and compute the actual state lazily on first use, or use <b>[InitializeOnLoadMethod]</b> on a specific method so the trigger is explicit."
        )
        {
            MessageFormat = "Class '{0}' uses [InitializeOnLoad] attribute",
            DefaultSeverity = Severity.Moderate
        };

        static readonly Descriptor InitializeOnLoadMethodDescriptor = new Descriptor
        (
            CDE2008,
            "Editor: [InitializeOnLoadMethod] Runs on Every Domain Reload",
            Impact.EditorWorkflow,
            "Methods marked with <b>[InitializeOnLoadMethod]</b> run on every editor domain reload and project load. Heavy work in such a method compounds across enter-play-mode and recompile cycles.",
            "Keep the <b>[InitializeOnLoadMethod]</b> body lightweight and compute the actual state lazily on first use. Cache results where the inputs are stable across the session."
        )
        {
            MessageFormat = "Method '{0}.{1}' uses [InitializeOnLoadMethod] attribute",
            DefaultSeverity = Severity.Moderate
        };

        static readonly Descriptor InitializeOnLoadNonStaticClassDescriptor = new Descriptor
        (
            CDE2009,
            "Editor: [InitializeOnLoad] Missing Static Constructor Has No Effect",
            Impact.EditorWorkflow,
            "The <b>[InitializeOnLoad]</b> attribute only triggers via the type's static constructor. Without one, Unity has no entry point to invoke on domain reload.",
            "Add a static constructor to the class, or remove the <b>[InitializeOnLoad]</b> attribute if no initialization is required."
        )
        {
            MessageFormat = "Class '{0}' uses [InitializeOnLoad] but has no static constructor",
            DefaultSeverity = Severity.Major
        };

        static readonly Descriptor InitializeOnLoadNoStaticConstructorDescriptor = new Descriptor
        (
            CDE2010,
            "Editor: [InitializeOnLoad] Without Static Constructor Has No Effect",
            Impact.EditorWorkflow,
            "The <b>[InitializeOnLoad]</b> attribute only triggers via the type's static constructor. A static class with no <b>.cctor</b> exposes no entry point for Unity to call.",
            "Add a static constructor to the class, or remove the <b>[InitializeOnLoad]</b> attribute if no initialization is required."
        )
        {
            MessageFormat = "Static class '{0}' uses [InitializeOnLoad] but has no static constructor",
            DefaultSeverity = Severity.Major
        };

        static readonly Descriptor InitializeOnLoadMethodNonStaticDescriptor = new Descriptor
        (
            CDE2011,
            "Editor: [InitializeOnLoadMethod] on Non-Static Method Has No Effect",
            Impact.EditorWorkflow,
            "The <b>[InitializeOnLoadMethod]</b> attribute is silently ignored on instance methods. Unity only invokes static methods marked with the attribute during domain reload.",
            "Make the method <b>static</b>, or remove the <b>[InitializeOnLoadMethod]</b> attribute."
        )
        {
            MessageFormat = "Method '{0}.{1}' uses [InitializeOnLoadMethod] but is not static",
            DefaultSeverity = Severity.Major
        };

        public override IReadOnlyCollection<OpCode> OpCodes => Array.Empty<OpCode>();

        public override void Analyze(TypeAnalysisContext context)
        {
            var typeDefinition = context.TypeDefinition;

            // Check for [InitializeOnLoad] on the type
            if (typeDefinition != null && typeDefinition.HasCustomAttributes)
            {
                var hasInitializeOnLoad = typeDefinition.CustomAttributes.Any(a =>
                    a.AttributeType.FullName.GetHashCode() == k_InitializeOnLoadAttributeHashCode);

                if (hasInitializeOnLoad)
                {
                    // Verify by checking the actual name to avoid hash collisions
                    try
                    {
                        var attribute = typeDefinition.CustomAttributes.FirstOrDefault(a =>
                            a.AttributeType.FullName == "UnityEditor.InitializeOnLoadAttribute" ||
                            a.AttributeType.FullName == "UnityEditor.InitializeOnLoad");

                        if (attribute != null)
                        {
                            var hasStaticConstructor = typeDefinition.Methods.Any(m => m.IsConstructor && m.IsStatic && m.Name == ".cctor");

                            if (!hasStaticConstructor)
                            {
                                // In C#/IL, static classes are both abstract and sealed.
                                var isStaticClass = typeDefinition.IsAbstract && typeDefinition.IsSealed;
                                var descriptor = isStaticClass
                                    ? InitializeOnLoadNoStaticConstructorDescriptor
                                    : InitializeOnLoadNonStaticClassDescriptor;

                                if (context.IsDescriptorEnabled(descriptor))
                                {
                                    var diagnostic = Diagnostic.Create(AnalysisCategory.Code, descriptor.Id, typeDefinition.Name);
                                    context.ReportIssue(diagnostic);
                                }

                                return;
                            }

                            // Valid usage - report as performance issue
                            if (context.IsDescriptorEnabled(InitializeOnLoadTypeDescriptor))
                            {
                                var diagnostic = Diagnostic.Create(AnalysisCategory.Code, InitializeOnLoadTypeDescriptor.Id, typeDefinition.Name);
                                context.ReportIssue(diagnostic);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarningFormat("Could not check InitializeOnLoad attribute on type {0}: {1}", typeDefinition.FullName, e.Message);
                    }
                }
            }
        }

        public override void Analyze(MethodAnalysisContext context)
        {
            var methodDefinition = context.MethodDefinition;
            var typeDefinition = methodDefinition.DeclaringType;

            // Check for [InitializeOnLoadMethod] on the method first (more specific)
            // Note: Unity only calls static methods with [InitializeOnLoadMethod]
            if (!methodDefinition.HasCustomAttributes)
                return;
            var hasInitializeOnLoadMethod = methodDefinition.CustomAttributes.Any(a =>
                a.AttributeType.FullName.GetHashCode() == k_InitializeOnLoadMethodAttributeHashCode);

            if (!hasInitializeOnLoadMethod)
                return;
            {
                // Verify by checking the actual name to avoid hash collisions
                try
                {
                    var attribute = methodDefinition.CustomAttributes.FirstOrDefault(a =>
                        a.AttributeType.FullName == "UnityEditor.InitializeOnLoadMethodAttribute" ||
                        a.AttributeType.FullName == "UnityEditor.InitializeOnLoadMethod");

                    if (attribute == null)
                        return;
                    if (!methodDefinition.IsStatic)
                    {
                        // [InitializeOnLoadMethod] has no effect on non-static methods
                        if (!context.IsDescriptorEnabled(InitializeOnLoadMethodNonStaticDescriptor))
                            return;
                        var diagnostic = Diagnostic.Create(AnalysisCategory.Code, InitializeOnLoadMethodNonStaticDescriptor.Id, typeDefinition.Name, methodDefinition.Name);
                        context.ReportIssue(diagnostic);
                    }
                    else
                    {
                        // Valid usage - report as performance issue
                        if (!context.IsDescriptorEnabled(InitializeOnLoadMethodDescriptor))
                            return;
                        var diagnostic = Diagnostic.Create(AnalysisCategory.Code, InitializeOnLoadMethodDescriptor.Id, typeDefinition.Name, methodDefinition.Name);
                        context.ReportIssue(diagnostic);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarningFormat("Could not check InitializeOnLoadMethod attribute on method {0}: {1}", methodDefinition.FullName, e.Message);
                }
            }
        }
    }
}

