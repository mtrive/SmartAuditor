using System;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace SmartAuditor.Editor.Core
{
    public class TypeAnalysisContext : AnalysisContext
    {
        public TypeAnalysisContext(AnalysisOptions options, IFindingSink sink) : base(options, sink) { }

        /// <summary>
        /// A Mono.Cecil Method Definition containing information about the current type being analyzed.
        /// </summary>
        public TypeDefinition TypeDefinition;
    }

    public class MethodAnalysisContext : TypeAnalysisContext
    {
        public MethodAnalysisContext(AnalysisOptions options, IFindingSink sink) : base(options, sink) { }

        /// <summary>
        /// A Mono.Cecil Method Definition containing information about the current method being analyzed.
        /// </summary>
        public MethodDefinition MethodDefinition;
    }

    /// <summary>
    /// A context object passed by CodeModule to a CodeAnalyzer's Analyze() method.
    /// </summary>
    public class InstructionAnalysisContext : MethodAnalysisContext
    {
        public InstructionAnalysisContext(AnalysisOptions options, IFindingSink sink) : base(options, sink) { }

        /// <summary>
        /// A Mono.Cecil Instruction containing information about the current code instruction being analyzed.
        /// </summary>
        public Instruction Instruction;

        /// <summary>
        /// The name of the type (class/struct) containing the method being analyzed.
        /// </summary>
        /// <remarks>
        /// Provides easy access to the calling type name for use in diagnostic messages.
        /// Equivalent to MethodDefinition.DeclaringType.Name.
        /// </remarks>
        public string CallingType;

        /// <summary>
        /// The name of the method being analyzed.
        /// </summary>
        /// <remarks>
        /// Provides easy access to the calling method name for use in diagnostic messages.
        /// Equivalent to MethodDefinition.Name.
        /// </remarks>
        public string CallingMethod;
    }

    /// <summary>
    /// Abstract base class for an Analyzer to be invoked by CodeModule
    /// </summary>
    internal abstract class CodeAnalyzer : DiagnosticAnalyzer
    {
        bool m_SupportsTypeAnalysis;
        bool m_SupportsMethodAnalysis;

        /// <summary>
        /// A collection of Mono.Cecil OpCodes which are used by this analyzer.
        /// </summary>
        /// <remarks>
        /// To speed up the code analysis process, each CodeAnalyzer must provide a list of the
        /// Instruction OpCodes it's interested in. Smart Auditor will only invoke an InstructionAnalyzer if the
        /// OpCode of the Instruction currently under analysis matches one of the OpCodes in this list. For more
        /// details, see the following Mono.Cecil github page:
        /// <seealso cref="https://github.com/jbevain/cecil/blob/master/Mono.Cecil.Cil/OpCodes.cs"/>.
        /// </remarks>
        public abstract IReadOnlyCollection<OpCode> OpCodes { get; }

        /// <summary>
        /// Indicates whether this analyzer overrides the <see cref="Analyze(TypeAnalysisContext)"/> method.
        /// Cached during <see cref="Initialize"/> so <see cref="Modules.CodeModule"/> does not pay
        /// reflection cost when building its per-run analysis plan.
        /// </summary>
        public bool SupportsTypeAnalysis => m_SupportsTypeAnalysis;

        /// <summary>
        /// Indicates whether this analyzer overrides the <see cref="Analyze(MethodAnalysisContext)"/> method.
        /// Cached during <see cref="Initialize"/> so <see cref="Modules.CodeModule"/> does not pay
        /// reflection cost when building its per-run analysis plan.
        /// </summary>
        public bool SupportsMethodAnalysis => m_SupportsMethodAnalysis;

        /// <inheritdoc/>
        public override void Initialize()
        {
            base.Initialize();

            var type = GetType();
            var typeMethod = type.GetMethod(nameof(Analyze), new[] { typeof(TypeAnalysisContext) });
            m_SupportsTypeAnalysis = typeMethod != null && typeMethod.DeclaringType != typeof(CodeAnalyzer);

            var methodMethod = type.GetMethod(nameof(Analyze), new[] { typeof(MethodAnalysisContext) });
            m_SupportsMethodAnalysis = methodMethod != null && methodMethod.DeclaringType != typeof(CodeAnalyzer);
        }

        public virtual void Analyze(TypeAnalysisContext context)
        {
        }

        public virtual void Analyze(MethodAnalysisContext context)
        {
        }

        /// <summary>
        /// Implement this method to detect Issues in a code instruction, and report them using Diagnostic.Create and context.ReportIssue.
        /// </summary>
        /// <param name="context">Context object containing information necessary to perform analysis</param>
        /// <remarks>
        /// When Instruction Analyzers detect an issue, they should use <seealso cref="Diagnostic.Create"/> to create
        /// a diagnostic with an <see cref="AnalysisCategory"/> and a DescriptorId, then call <seealso cref="AnalysisContext.ReportIssue"/> to report it.
        /// The Code Module will add further information including the DependencyNode, Location and assembly name and add the resulting ReportItem to the report.
        /// </remarks>
        public virtual void Analyze(InstructionAnalysisContext context)
        {
        }
    }
}
