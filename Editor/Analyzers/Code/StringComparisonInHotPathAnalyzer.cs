// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.CodeAnalysis;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Reports <see cref="string"/> comparison and matching calls (<c>==</c>, <c>!=</c>,
    /// <c>Equals</c>, <c>Compare</c>, <c>CompareOrdinal</c>, <c>CompareTo</c>, <c>StartsWith</c>,
    /// <c>EndsWith</c>, <c>IndexOf</c>, <c>Contains</c>) that are reachable from a runtime
    /// performance-critical method.
    /// </summary>
    /// <remarks>
    /// Unlike most code analyzers this one fires <em>only</em> inside hot paths and stays silent
    /// elsewhere. Detection is purely call-graph based — the per-instruction pass reports nothing,
    /// so comparisons inside helper methods called from an update loop are caught as well. A method
    /// counts as a hot path when <see cref="PerfCriticalEntryPoints.IsEntryPoint(MethodReference)"/>
    /// is satisfied (a <c>MonoBehaviour</c> update callback).
    /// </remarks>
    sealed class StringComparisonInHotPathAnalyzer : CodeAnalyzer, ICallGraphCodeAnalyzer
    {
        const string k_StringTypeFullName = "System.String";

        internal const string CDE1008 = nameof(CDE1008);

        internal static readonly Descriptor StringComparisonInHotPathDescriptor = new Descriptor(
            CDE1008,
            "Strings: Comparison In Hot Path",
            Impact.Performance,
            "A string comparison or substring search (<b>==</b>, <b>Equals</b>, <b>Compare</b>, <b>CompareTo</b>, <b>StartsWith</b>, <b>EndsWith</b>, <b>IndexOf</b>, <b>Contains</b>) is reachable from a per-frame update callback. String matching walks characters and, for culture-aware overloads, runs the globalization collation machinery; repeated every frame the cost compounds into avoidable CPU time.",
            "Hoist the comparison out of the update loop, or compare a cached enum, integer, or hash instead of the string. When a string comparison is unavoidable, call the overload that takes <b>StringComparison.Ordinal</b> to skip culture-aware collation."
        )
        {
            MessageFormat = "'{0}' is a string comparison reachable from hot path '{1}'",
            DefaultSeverity = Severity.Moderate
        };

        static readonly HashSet<string> k_ComparisonMethodNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "op_Equality",
            "op_Inequality",
            "Equals",
            "Compare",
            "CompareOrdinal",
            "CompareTo",
            "StartsWith",
            "EndsWith",
            "IndexOf",
            "Contains"
        };

        // Reporting is entirely call-graph driven, so this analyzer opts out of per-instruction
        // analysis. The CallCrawler records every Call/Callvirt regardless of this set.
        public override IReadOnlyCollection<OpCode> OpCodes => Array.Empty<OpCode>();

        public void Analyze(CallGraphAnalysisContext context)
        {
            if (!context.IsDescriptorEnabled(StringComparisonInHotPathDescriptor))
            {
                return;
            }

            // The same caller-callee pair can appear in a bucket more than once when a method
            // compares strings at multiple offsets, and the runtime/editor compilation passes can
            // record the same call twice; dedupe by source location so we emit one issue per line.
            var alreadyEmitted = new HashSet<string>();

            foreach (var call in context.CallCrawler.EnumerateCalls(IsStringComparisonCallee))
            {
                if (!TryFindPerfCriticalRoot(context.CallCrawler, call.Caller, out var root))
                {
                    continue;
                }

                var fingerprint = BuildCallSiteFingerprint(call);
                if (!alreadyEmitted.Add(fingerprint))
                {
                    continue;
                }

                var calleeName = FormatCallee(call.Callee);
                var rootName = $"{root.DeclaringType.Name}.{root.Name}";
                var diagnostic = Diagnostic
                    .Create(AnalysisCategory.Code, CDE1008, calleeName, rootName)
                    .WithLocation(call.Location)
                    .WithDependencies(new CallTreeNode(call.Caller))
                    .WithEvidence(CodeEvidenceKeys.ApiType, call.Callee.DeclaringType.FullName)
                    .WithEvidence(CodeEvidenceKeys.ApiMethod, call.Callee.Name);
                context.ReportIssue(diagnostic);
            }
        }

        static bool TryFindPerfCriticalRoot(CallCrawler callCrawler, MethodReference start, out MethodReference root)
        {
            if (PerfCriticalEntryPoints.IsEntryPoint(start))
            {
                root = start;
                return true;
            }
            return callCrawler.TryFindRootAncestor(start, PerfCriticalEntryPoints.IsEntryPoint, out root);
        }

        static bool IsStringComparisonCallee(MethodReference callee)
        {
            if (callee?.DeclaringType == null)
            {
                return false;
            }
            if (callee.DeclaringType.FullName != k_StringTypeFullName)
            {
                return false;
            }
            return k_ComparisonMethodNames.Contains(callee.Name);
        }

        static string FormatCallee(MethodReference callee)
        {
            switch (callee.Name)
            {
                case "op_Equality": return "string ==";
                case "op_Inequality": return "string !=";
                default: return $"String.{callee.Name}";
            }
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
    }
}
