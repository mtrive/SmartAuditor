// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;
using UnityEditor;

namespace SmartAuditor.Editor.Analyzers
{
    internal sealed class UnsupportedOnWebGLAnalyzer : CodeAnalyzer
    {
        internal const string CDE1005 = nameof(CDE1005);
        internal const string CDE1006 = nameof(CDE1006);
        internal const string CDE0233 = nameof(CDE0233);

        internal static readonly Descriptor DescriptorSystemNet = new Descriptor
            (
            CDE1005,
            "WebGL: System.Net Not Supported",
            Impact.Compatibility,
            "The <b>System.Net</b> namespace is not supported on WebGL. Calls fail at runtime or block the build.",
            "Replace <b>System.Net</b> usage with <b>UnityEngine.Networking.UnityWebRequest</b> or another WebGL-compatible networking API."
            )
        {
            MessageFormat = "'{0}' is in System.Net, which is not supported on WebGL",
            Platforms = new[] { BuildTarget.WebGL }
        };

        internal static readonly Descriptor DescriptorSystemThreading = new Descriptor
            (
            CDE1006,
            "WebGL: System.Threading Not Supported",
            Impact.Compatibility,
            "The <b>System.Threading</b> namespace is not supported on WebGL. The platform runs on a single-threaded JavaScript host, so threading APIs fail at build or runtime.",
            "Replace threading APIs with coroutines, <b>async</b>/<b>await</b> on the main thread, or Unity's <b>JobSystem</b> with WebGL-supported job scheduling."
            )
        {
            MessageFormat = "'{0}' is in System.Threading, which is not supported on WebGL",
            Platforms = new[] { BuildTarget.WebGL }
        };

        internal static readonly Descriptor DescriptorMicrophone = new Descriptor
            (
            CDE0233,
            "WebGL: Microphone API Not Supported",
            Impact.Compatibility,
            "The <b>UnityEngine.Microphone</b> API is not supported on WebGL. Calls fail at runtime.",
            "Remove <b>UnityEngine.Microphone</b> usage from code that targets WebGL, or gate it behind a platform check."
            )
        {
            MessageFormat = "'{0}' uses Microphone, which is not supported on WebGL",
            Platforms = new[] { BuildTarget.WebGL }
        };

        readonly OpCode[] m_OpCodes =
        {
            Mono.Cecil.Cil.OpCodes.Call,
            Mono.Cecil.Cil.OpCodes.Callvirt
        };

        bool descriptorSystemNetSupported;
        bool descriptorSystemThreadingSupported;
        bool descriptorMicrophoneSupported;

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Initialize()
        {
            base.Initialize();
            descriptorSystemNetSupported = DescriptorSystemNet.IsSupported();
            descriptorSystemThreadingSupported = DescriptorSystemThreading.IsSupported();
            descriptorMicrophoneSupported = DescriptorMicrophone.IsSupported();
        }

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;
            if (descriptorSystemNetSupported && methodReference.DeclaringType.FullName.StartsWith("System.Net."))
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.Code, DescriptorSystemNet.Id, methodReference.FullName);
                context.ReportIssue(diagnostic);
                return;
            }
            if (descriptorSystemThreadingSupported && methodReference.DeclaringType.FullName.StartsWith("System.Threading."))
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.Code, DescriptorSystemThreading.Id, methodReference.FullName);
                context.ReportIssue(diagnostic);
                return;
            }
            if (descriptorMicrophoneSupported && methodReference.DeclaringType.FullName.Equals("UnityEngine.Microphone"))
            {
                var diagnostic = Diagnostic.Create(AnalysisCategory.Code, DescriptorMicrophone.Id, methodReference.FullName);
                context.ReportIssue(diagnostic);
            }
        }
    }
}
