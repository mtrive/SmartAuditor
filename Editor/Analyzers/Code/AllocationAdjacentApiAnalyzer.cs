using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class AllocationAdjacentApiAnalyzer : CodeAnalyzer
    {
        internal const string CDE0051 = nameof(CDE0051);
        internal const string CDE0037 = nameof(CDE0037);

        static readonly Descriptor ComputeBufferGetDataDescriptor = new Descriptor(
            CDE0051,
            "GPU Readback: ComputeBuffer.GetData Stalls CPU",
            Impact.Performance,
            "<b>ComputeBuffer.GetData()</b> blocks the CPU until the GPU finishes all pending work on the buffer. The synchronisation cost can dominate a frame when called in hot code.",
            "Move ComputeBuffer readbacks off the hot path. Where a readback is unavoidable, use <b>AsyncGPUReadback.Request</b> and consume the data when the request completes.")
        {
            MessageFormat = "ComputeBuffer.GetData stalls the CPU waiting on GPU work"
        };

        static readonly Descriptor LoadImageDescriptor = new Descriptor(
            CDE0037,
            "Texture: ImageConversion.LoadImage Retains CPU Copy",
            Impact.Memory,
            "<b>ImageConversion.LoadImage()</b> defaults to keeping a CPU-side copy of the decoded pixels alongside the GPU upload, doubling memory usage for the texture.",
            "Pass <b>markNonReadable: true</b> to <b>ImageConversion.LoadImage()</b> when CPU-side pixel access is not required.")
        {
            MessageFormat = "ImageConversion.LoadImage retains a CPU copy of texture data by default"
        };

        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;
            var declaringType = methodReference.DeclaringType.FullName;
            var methodName = methodReference.Name;

            if (declaringType == "UnityEngine.ComputeBuffer" && methodName == "GetData")
            {
                if (!context.IsDescriptorEnabled(ComputeBufferGetDataDescriptor))
                    return;

                context.ReportIssue(Diagnostic.Create(AnalysisCategory.Code, ComputeBufferGetDataDescriptor.Id));
                return;
            }

            if (declaringType == "UnityEngine.ImageConversion" && methodName == "LoadImage")
            {
                if (!context.IsDescriptorEnabled(LoadImageDescriptor))
                    return;

                context.ReportIssue(Diagnostic.Create(AnalysisCategory.Code, LoadImageDescriptor.Id));
            }
        }
    }
}
