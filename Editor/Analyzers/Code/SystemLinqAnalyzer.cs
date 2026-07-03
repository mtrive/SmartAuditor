// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.CodeAnalysis;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Analyzes code for usage of System.Linq methods which allocate large amounts of
    /// managed memory and exhibit poor CPU performance.
    /// </summary>
    /// <remarks>
    /// Reports two diagnostics:
    /// <list type="bullet">
    /// <item><description><b>CDE1000</b> -- per-instruction, fires on any LINQ call site.</description></item>
    /// <item><description><b>CDE1007</b> -- post-graph, fires when a LINQ call site is
    /// reachable from a Unity Editor entry point (see
    /// <see cref="EditorEntryPointRoots.IsEntryPoint"/>). Mutually exclusive with CDE1000 at
    /// the same call site: when CDE1007 is reported, the matching CDE1000 is dropped.</description></item>
    /// </list>
    /// </remarks>
    sealed class SystemLinqAnalyzer : CodeAnalyzer, ICallGraphCodeAnalyzer
    {
        const string k_LinqNamespace = "System.Linq";

        internal const string CDE1000 = nameof(CDE1000);
        internal const string CDE1007 = nameof(CDE1007);

        static readonly Descriptor LinqDescriptor = new Descriptor(
            CDE1000,
            "LINQ: System.Linq Call",
            Impact.Performance | Impact.Memory,
            "<b>System.Linq</b> operators allocate iterator and closure objects, box value-type element references, and walk sequences lazily through delegate chains. The CPU and GC cost compounds in hot code.",
            "Replace LINQ calls in hot code with explicit <b>for</b> / <b>foreach</b> loops over the concrete collection. Reserve LINQ for one-shot initialization or tooling paths."
        )
        {
            MessageFormat = "'{0}' is a LINQ call",
            DefaultSeverity = Severity.Moderate
        };

        internal static readonly Descriptor LinqInEditorHotPathDescriptor = new Descriptor(
            CDE1007,
            "LINQ: System.Linq Reachable From Editor Hot Path",
            Impact.EditorWorkflow | Impact.Performance | Impact.Memory,
            "A <b>System.Linq</b> call is reachable from a frequently-invoked editor entry point: <b>OnGUI</b>, <b>[InitializeOnLoad]</b>, <b>[InitializeOnLoadMethod]</b>, <b>[MenuItem]</b>, custom editors, or asset post-processors. LINQ cost on these paths compounds across domain reloads, inspector redraws, asset imports, and menu population.",
            "Move the LINQ call off the editor hot path. For <b>[InitializeOnLoad]</b> / <b>[InitializeOnLoadMethod]</b>, compute lazily on first use. For <b>OnGUI</b> / <b>OnInspectorGUI</b> / <b>OnSceneGUI</b>, cache the result and recompute only when inputs change. Where the call is unavoidable, replace it with an explicit loop."
        )
        {
            MessageFormat = "'{0}' is a LINQ call reachable from editor hot path '{1}'",
            DefaultSeverity = Severity.Moderate
        };

        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;
            var declaringType = methodReference.DeclaringType;

            if (declaringType.Namespace != k_LinqNamespace)
                return;

            if (!context.IsDescriptorEnabled(LinqDescriptor))
                return;

            var description = $"{declaringType.FullName}.{methodReference.Name}";
            var diagnostic = Diagnostic.Create(AnalysisCategory.Code, LinqDescriptor.Id, description);
            context.ReportIssue(diagnostic);
        }

        public void Analyze(CallGraphAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(LinqInEditorHotPathDescriptor))
            {
                return;
            }

            var pac1000ByLocation = BuildPac1000LocationIndex(context.Issues);
            var alreadyEmitted = new HashSet<string>();

            foreach (var call in context.CallCrawler.EnumerateCalls(IsLinqCallee))
            {
                if (!TryFindEditorRoot(context.CallCrawler, call.Caller, out var root))
                {
                    continue;
                }

                // The same caller-callee pair can appear in the bucket more than once when
                // a method calls the same LINQ method at multiple offsets; dedupe by the
                // sequence-point location so we emit one CDE1007 per source line.
                var fingerprint = BuildCallSiteFingerprint(call);
                if (!alreadyEmitted.Add(fingerprint))
                {
                    continue;
                }

                var calleeName = $"{call.Callee.DeclaringType.FullName}.{call.Callee.Name}";
                var rootName = $"{root.DeclaringType.Name}.{root.Name}";
                var diagnostic = Diagnostic
                    .Create(AnalysisCategory.Code, CDE1007, calleeName, rootName)
                    .WithLocation(call.Location)
                    .WithDependencies(new CallTreeNode(call.Caller))
                    .WithEvidence(CodeEvidenceKeys.ApiType, call.Callee.DeclaringType.FullName)
                    .WithEvidence(CodeEvidenceKeys.ApiMethod, call.Callee.Name);
                context.ReportIssue(diagnostic);

                if (TryGetMatchingPac1000(pac1000ByLocation, call.Location, out var existing))
                {
                    context.RemoveIssue(existing);
                }
            }
        }

        static bool TryFindEditorRoot(CallCrawler callCrawler, MethodReference start, out MethodReference root)
        {
            if (EditorEntryPointRoots.IsEntryPoint(start))
            {
                root = start;
                return true;
            }
            return callCrawler.TryFindRootAncestor(start, EditorEntryPointRoots.IsEntryPoint, out root);
        }

        static bool IsLinqCallee(MethodReference callee)
        {
            return callee?.DeclaringType?.Namespace == k_LinqNamespace;
        }

        static Dictionary<string, ReportItem> BuildPac1000LocationIndex(IReadOnlyList<ReportItem> issues)
        {
            var index = new Dictionary<string, ReportItem>();
            for (var i = 0; i < issues.Count; i++)
            {
                var issue = issues[i];
                if (issue.Id != CDE1000)
                {
                    continue;
                }
                var key = LocationKey(issue.Location);
                if (key != null)
                {
                    index[key] = issue;
                }
            }
            return index;
        }

        static bool TryGetMatchingPac1000(Dictionary<string, ReportItem> index, Location location, out ReportItem match)
        {
            match = null;
            var key = LocationKey(location);
            return key != null && index.TryGetValue(key, out match);
        }

        static string LocationKey(Location location)
        {
            if (location == null)
            {
                return null;
            }
            return string.Concat(
                location.Path,
                "|",
                location.Line?.ToString() ?? string.Empty,
                "|",
                location.Column?.ToString() ?? string.Empty);
        }

        static string BuildCallSiteFingerprint(CallInfo call)
        {
            return string.Concat(
                call.Caller.FastFullName(),
                "|",
                call.Callee.FastFullName(),
                "|",
                LocationKey(call.Location) ?? string.Empty);
        }
    }
}
