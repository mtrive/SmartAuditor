using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.CodeAnalysis.Compilation;
using SmartAuditor.Editor.Core;
using UnityEngine;
using UnityEngine.Profiling;

namespace SmartAuditor.Editor.CodeAnalysis
{
    /// <summary>
    /// Walks compiled assembly IL and dispatches <see cref="CodeAnalyzer"/> instances at type,
    /// method, and instruction granularity.
    /// </summary>
    internal sealed class AssemblyIlAnalyzer
    {
        public bool AnalyzeAssemblies(
            CodeAnalysisRun run,
            IReadOnlyCollection<CompiledAssemblyInfo> assemblyInfos,
            IReadOnlyCollection<string> assemblyDirectories)
        {
            var includedAssemblies = run.Options.IncludedAssemblies;

            using (var assemblyResolver = new DefaultAssemblyResolver())
            {
                foreach (var path in assemblyDirectories)
                    assemblyResolver.AddSearchDirectory(path);

                foreach (var dir in assemblyInfos.Select(info => Path.GetDirectoryName(info.Path)).Distinct())
                    assemblyResolver.AddSearchDirectory(dir);

                run.Progress?.Start("Analyzing Assemblies", string.Empty, assemblyInfos.Count());

                try
                {
                    foreach (var assemblyInfo in assemblyInfos)
                    {
                        if (run.CancellationToken.IsCancellationRequested)
                            return false;

                        run.Progress?.Advance(assemblyInfo.Name);

                        if (!File.Exists(assemblyInfo.Path))
                        {
                            Debug.LogError(assemblyInfo.Path + " not found.");
                            continue;
                        }

                        var issueReporter = run.IssueReporter;
                        if (issueReporter != null && includedAssemblies != null && !includedAssemblies.Contains(assemblyInfo.Name))
                            issueReporter = null;

                        if (!AnalyzeAssembly(run, assemblyInfo, assemblyResolver, issueReporter))
                            return false;
                    }
                }
                finally
                {
                    run.Progress?.Clear();
                }
            }

            return true;
        }

        bool AnalyzeAssembly(
            CodeAnalysisRun run,
            CompiledAssemblyInfo assemblyInfo,
            IAssemblyResolver assemblyResolver,
            Action<ReportItem> issueReporter)
        {
            Profiler.BeginSample("AssemblyIlAnalyzer.Analyze " + assemblyInfo.Name);

            var typeAnalyzers = run.Plan.TypeLevel;

            using (var assembly = AssemblyDefinition.ReadAssembly(assemblyInfo.Path,
                new ReaderParameters { ReadSymbols = true, AssemblyResolver = assemblyResolver, MetadataResolver = new MetadataResolverWithCache(assemblyResolver) }))
            {
                foreach (var typeDefinition in MonoCecilHelper.AggregateAllTypeDefinitions(assembly.MainModule.Types))
                {
                    if (run.CancellationToken.IsCancellationRequested)
                    {
                        Profiler.EndSample();
                        return false;
                    }

                    Profiler.BeginSample(typeDefinition.Name);
                    Profiler.BeginSample("AssemblyIlAnalyzer.IsPerformanceCriticalType");
                    var isPerformanceCriticalType = PerfCriticalEntryPoints.IsPerfCriticalType(typeDefinition);
                    Profiler.EndSample();

                    using var context = new TypeAnalysisContext(run.Options, run.Sink)
                    {
                        TypeDefinition = typeDefinition,
                    };
                    var typeLocation = CodeLocationResolver.ResolveTypeLocation(assemblyInfo, typeDefinition);

                    if (issueReporter != null && typeAnalyzers.Length > 0)
                    {
                        foreach (var analyzer in typeAnalyzers)
                        {
                            context.OnIssueCreated = CodeDiagnosticEnricher.CreateTypeLevelReporter(
                                assemblyInfo.Name,
                                typeLocation,
                                issueReporter);
                            analyzer.Analyze(context);
                        }
                    }
                    context.Discard();

                    foreach (var methodDefinition in typeDefinition.Methods)
                    {
                        if (run.CancellationToken.IsCancellationRequested)
                        {
                            Profiler.EndSample();
                            Profiler.EndSample();
                            return false;
                        }

                        if (!methodDefinition.HasBody)
                            continue;

                        if (methodDefinition.DeclaringType.FullName.StartsWith("Unity.Burst.Editor.BurstDisassembler"))
                            continue;

                        if (!methodDefinition.DebugInformation.HasSequencePoints)
                            continue;

                        var isPerformanceCriticalContext = isPerformanceCriticalType
                            && PerfCriticalEntryPoints.IsPerfCriticalMethod(methodDefinition);
                        AnalyzeMethodBody(run, context, assemblyInfo, methodDefinition, isPerformanceCriticalContext, issueReporter);
                    }
                    Profiler.EndSample();
                }
            }

            Profiler.EndSample();
            return true;
        }

        void AnalyzeMethodBody(
            CodeAnalysisRun run,
            TypeAnalysisContext typeAnalysisContext,
            CompiledAssemblyInfo assemblyInfo,
            MethodDefinition caller,
            bool perfCriticalContext,
            Action<ReportItem> issueReporter)
        {
            Profiler.BeginSample("AssemblyIlAnalyzer.AnalyzeMethodBody");

            var methodAnalyzers = run.Plan.MethodLevel;

            using var methodAnalysisContext = new MethodAnalysisContext(typeAnalysisContext.Options, typeAnalysisContext.Sink)
            {
                TypeDefinition = typeAnalysisContext.TypeDefinition,
                MethodDefinition = caller,
            };
            var methodLocation = CodeLocationResolver.ResolveMethodLocation(assemblyInfo, caller);

            // If the method's source file is globally suppressed by a no-Id rule, skip
            // running per-method and per-instruction analyzers entirely. Call-graph
            // crawling below still runs because the call graph is shared state used to
            // promote severities for non-suppressed callers (e.g. hot-path detection).
            var rules = typeAnalysisContext.Options.Rules;
            var pathSuppressed = methodLocation != null
                && rules != null
                && rules.IsPathGloballySuppressed(methodLocation.Path);
            var runIssueAnalyzers = issueReporter != null && !pathSuppressed;

            if (runIssueAnalyzers && methodAnalyzers.Length > 0)
            {
                methodAnalysisContext.OnIssueCreated = CodeDiagnosticEnricher.CreateMethodLevelReporter(
                    assemblyInfo.Name,
                    methodLocation,
                    caller.FullName,
                    issueReporter);

                foreach (var analyzer in methodAnalyzers)
                    analyzer.Analyze(methodAnalysisContext);
            }
            methodAnalysisContext.Discard();

            var callerNode = new CallTreeNode(caller) { PerfCriticalContext = perfCriticalContext };
            var sequencePoints = caller.DebugInformation.SequencePoints;
            var lastSequencePointIndex = 0;
            var instructions = caller.Body.Instructions;

            for (var i = 0; i < instructions.Count; i++)
            {
                var inst = instructions[i];

                // When per-method analyzers won't run (no issue sink, or the source file is
                // globally suppressed), only the call-graph step matters here; we still need
                // to walk every Call/Callvirt for the crawler. Other opcodes can be skipped.
                if (!runIssueAnalyzers &&
                    inst.OpCode != OpCodes.Call &&
                    inst.OpCode != OpCodes.Callvirt)
                    continue;

                SequencePoint s = null;
                for (var j = lastSequencePointIndex; j < sequencePoints.Count; j++)
                {
                    var potentialPoint = sequencePoints[j];
                    if (inst.Offset < potentialPoint.Offset)
                        break;
                    s = potentialPoint;
                    lastSequencePointIndex = j;
                }

                Location location = null;
                if (s != null)
                {
                    location = CodeLocationResolver.CreateLocation(assemblyInfo, s, includeHidden: true);
                    callerNode.Location = location;
                }

                if (inst.OpCode == OpCodes.Call || inst.OpCode == OpCodes.Callvirt)
                {
                    Profiler.BeginSample("AssemblyIlAnalyzer.OnCallFound");
                    run.RecordCall(new CallInfo((MethodReference)inst.Operand, caller, location, perfCriticalContext));
                    Profiler.EndSample();
                }

                List<CodeAnalyzer> analyzers;
                if (!runIssueAnalyzers || !run.Plan.OpCodeIndex.TryGetValue(inst.OpCode, out analyzers))
                    continue;

                using var context = new InstructionAnalysisContext(methodAnalysisContext.Options, methodAnalysisContext.Sink)
                {
                    TypeDefinition = methodAnalysisContext.TypeDefinition,
                    MethodDefinition = methodAnalysisContext.MethodDefinition,
                    Instruction = inst,
                    CallingType = methodAnalysisContext.MethodDefinition.DeclaringType.Name,
                    CallingMethod = methodAnalysisContext.MethodDefinition.Name
                };

                Profiler.BeginSample(inst.OpCode.Name);
                // The per-instruction location can be null when the IL offset has no
                // SequencePoint (typical for compiler-generated synthetics: lambdas,
                // async state machines, iterator helpers — and some P/Invoke call
                // sites end up there). Fall back to the method's first sequence point
                // so the issue is at least navigable to the surrounding method.
                var resolvedLocation = location ?? methodLocation;
                var callee = inst.Operand as MethodReference;
                context.OnIssueCreated = CodeDiagnosticEnricher.CreateInstructionLevelReporter(
                    assemblyInfo.Name,
                    resolvedLocation,
                    callerNode,
                    caller.FullName,
                    callee,
                    issueReporter);

                foreach (var analyzer in analyzers)
                {
                    Profiler.BeginSample(analyzer.GetType().Name);
                    analyzer.Analyze(context);
                    Profiler.EndSample();
                }
                Profiler.EndSample();
                context.Discard();
            }
            Profiler.EndSample();
        }
    }
}
