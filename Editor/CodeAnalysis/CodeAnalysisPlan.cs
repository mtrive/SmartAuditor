using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.CodeAnalysis
{
    /// <summary>
    /// Per-run snapshot of compatible analyzers and precomputed lookup tables. Built once at the
    /// start of code analysis so IL walking does not re-filter analyzers per type, method, or
    /// instruction.
    /// </summary>
    internal readonly struct CodeAnalysisPlan
    {
        public CodeAnalyzer[] Compatible { get; }
        public CodeAnalyzer[] TypeLevel { get; }
        public CodeAnalyzer[] MethodLevel { get; }
        public ICallGraphCodeAnalyzer[] CallGraph { get; }
        public Dictionary<OpCode, List<CodeAnalyzer>> OpCodeIndex { get; }

        public CodeAnalysisPlan(
            CodeAnalyzer[] compatible,
            CodeAnalyzer[] typeLevel,
            CodeAnalyzer[] methodLevel,
            ICallGraphCodeAnalyzer[] callGraph,
            Dictionary<OpCode, List<CodeAnalyzer>> opCodeIndex)
        {
            Compatible = compatible;
            TypeLevel = typeLevel;
            MethodLevel = methodLevel;
            CallGraph = callGraph;
            OpCodeIndex = opCodeIndex;
        }
    }
}
