using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mono.Cecil.Cil;
using SmartAuditor.Editor;
using SmartAuditor.Editor.CodeAnalysis.Compilation;
using SmartAuditor.Editor.CodeAnalysis;
using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Utils;
using UnityEngine;
using UnityEngine.Profiling;
using SmartAuditor.Editor.Settings;

namespace SmartAuditor.Editor.Modules
{
    sealed class CodeModule : AnalysisModule<CodeAnalyzer>
    {
        internal static readonly InsightSchema k_AssemblyInsightSchema = new InsightSchema(
            new InsightColumn(AssemblyColumns.Name, "Assembly Name", PropertyFormat.Text),
            new InsightColumn(AssemblyColumns.ReadOnly, "Read Only", PropertyFormat.Boolean, ColumnHints.Categorical, isDefaultGroup: true),
            new InsightColumn(AssemblyColumns.CompileTime, "Compile Time (seconds)", PropertyFormat.DurationAdaptive, ColumnHints.Aggregatable | ColumnHints.TopNMetric),
            new InsightColumn(AssemblyColumns.StartTime, "Start Time (UTC)", PropertyFormat.Text),
            new InsightColumn(AssemblyColumns.EndTime, "End Time (UTC)", PropertyFormat.Text),
            new InsightColumn(AssemblyColumns.Dependencies, "Dependencies", PropertyFormat.Text));

        internal static readonly InsightSchema k_PrecompiledAssemblyInsightSchema = new InsightSchema(
            new InsightColumn(PrecompiledAssemblyColumns.Name, "Assembly Name", PropertyFormat.Text));

        internal static readonly InsightSchema k_CodeDiagnosticSchema = new InsightSchema(
            new InsightColumn(CodeColumns.Assembly, "Assembly", PropertyFormat.Text, longName: "Managed Assembly Name"));

        internal static readonly InsightSchema k_CodeCompilerMessageSchema = new InsightSchema(
            new InsightColumn(CompilerMessageColumns.Code, "Code", PropertyFormat.Text, isDefaultGroup: true),
            new InsightColumn(CompilerMessageColumns.Assembly, "Assembly", PropertyFormat.Text, longName: "Managed Assembly Name"));

        List<OpCode> m_OpCodes;
        readonly AssemblyIlAnalyzer m_IlAnalyzer = new AssemblyIlAnalyzer();

        static readonly IReadOnlyDictionary<AnalysisCategory, InsightSchema> s_CategorySchemas =
            new Dictionary<AnalysisCategory, InsightSchema>
            {
                { AnalysisCategory.Code, k_CodeDiagnosticSchema },
                { AnalysisCategory.CodeCompilerMessage, k_CodeCompilerMessageSchema },
                { AnalysisCategory.Assembly, k_AssemblyInsightSchema },
                { AnalysisCategory.PrecompiledAssembly, k_PrecompiledAssemblyInsightSchema },
            };

        public override IReadOnlyDictionary<AnalysisCategory, InsightSchema> CategorySchemas => s_CategorySchemas;

        public override string Name => "Code";

        public override IReadOnlyCollection<AnalysisCategory> OwnedCategories => new[]
        {
            AnalysisCategory.Code,
            AnalysisCategory.CodeCompilerMessage,
            AnalysisCategory.Assembly,
            AnalysisCategory.PrecompiledAssembly,
        };

        public override void Initialize()
        {
            base.Initialize();
            m_OpCodes = GetAnalyzers().Select(a => a.OpCodes).SelectMany(c => c).Distinct().ToList();
        }

        public override async Task<AnalysisResult> AnalyzeAsync(AnalysisSession session, CancellationToken cancellationToken = default)
        {
            var options = session.Options;
            var progress = session.Progress;

            var context = new AnalysisContext(options, session);

            ProcessPrecompiledAssemblies(context);
            if (cancellationToken.IsCancellationRequested)
            {
                context.Discard();
                return AnalysisResult.Cancelled;
            }

            var run = new CodeAnalysisRun(BuildAnalysisPlan(options), options, session, progress, cancellationToken);

            // Runtime first: Phase 1 (compilation) on main thread, Phase 2 (IL analysis) on
            // thread pool. Runtime appends to foundIssues before the Editor branch so the
            // fingerprint dedup in Report.AddIssues keeps the player-context attribution
            // when both contexts produce the same diagnostic on the same line.
            if ((options.CodeContext & CodeContext.Runtime) != 0)
            {
                PlatformAssemblyProvider assemblyProvider = null;
                IReadOnlyList<CompiledAssemblyInfo> assemblyInfos;

                try
                {
                    assemblyProvider = new PlatformAssemblyProvider(options.Platform, options.IncludedAssemblies);
                    assemblyProvider.OnAssemblyCompilationFinished = result => ProcessCompilerMessages(context, result);

                    // Phase 1: compilation (must run on main thread — Unity API)
                    assemblyInfos = assemblyProvider.GetAssemblies(progress, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    assemblyProvider?.Dispose();
                    context.Discard();
                    return AnalysisResult.Cancelled;
                }
                catch (Exception e)
                {
                    assemblyProvider?.Dispose();
                    Debug.LogError($"[{SmartAuditor.DisplayName}] Code analysis failed due to compilation errors: {e.Message}\n{e.StackTrace}");
                    context.Flush();
                    return AnalysisResult.Failure;
                }

                if (options.IncludedAssemblies != null)
                    assemblyInfos = assemblyInfos.Where(a => options.IncludedAssemblies.Contains(a.Name)).ToArray();

                var lookup = assemblyInfos.ToLookup(info => info.IsPackageReadOnly);
                var localAssemblyInfos = lookup[false].ToArray();
                var readOnlyAssemblyInfos = lookup[true].ToArray();

                var assemblyDirectories = AssemblyResolver.GetPrecompiledAssemblyDirectories(
                    PrecompiledAssemblyTypes.UserAssembly |
                    PrecompiledAssemblyTypes.UnityEngine |
                    PrecompiledAssemblyTypes.SystemAssembly).ToList();

                Profiler.BeginSample("CodeModule.AnalyzeAsync.LocalAssemblies");
                run.IssueReporter = run.FoundIssues.Add;
                if (!m_IlAnalyzer.AnalyzeAssemblies(run, localAssemblyInfos, assemblyDirectories))
                {
                    Profiler.EndSample();
                    assemblyProvider.Dispose();
                    context.Discard();
                    return AnalysisResult.Cancelled;
                }
                Profiler.EndSample();

                if (cancellationToken.IsCancellationRequested)
                {
                    assemblyProvider.Dispose();
                    context.Discard();
                    return AnalysisResult.Cancelled;
                }

                // Phase 2: IL analysis of read-only assemblies on thread pool — does NOT block the main thread.
                // Note: no ConfigureAwait(false) here. The Editor branch below calls
                // CompilationPipeline.GetAssemblies, which is main-thread-only; we need
                // the await continuation to resume on Unity's SynchronizationContext.
                try
                {
                    var readOnlyAnalysisCompleted = await Task.Run(() =>
                    {
                        run.IssueReporter = options.AnalyzeReadOnlyPackages ? run.FoundIssues.Add : null;
                        return m_IlAnalyzer.AnalyzeAssemblies(run, readOnlyAssemblyInfos, assemblyDirectories);
                    }, cancellationToken);

                    if (!readOnlyAnalysisCompleted)
                    {
                        context.Discard();
                        return AnalysisResult.Cancelled;
                    }
                }
                catch (OperationCanceledException)
                {
                    assemblyProvider.Dispose();
                    context.Discard();
                    return AnalysisResult.Cancelled;
                }
                finally
                {
                    assemblyProvider.Dispose();
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                context.Discard();
                return AnalysisResult.Cancelled;
            }

            // Editor second: synchronous analysis on main thread. Walks the broader
            // editor compilation set (see EditorAssemblyProvider) so #if UNITY_EDITOR
            // blocks inside player asmdefs are reached. Diagnostics from lines that
            // also exist in the player IL are dropped by the fingerprint dedup at
            // Report.AddIssues; #if UNITY_EDITOR-only diagnostics pass through.
            if ((options.CodeContext & CodeContext.Editor) != 0)
            {
                if (!AnalyzeEditorAssemblies(run, context))
                {
                    context.Discard();
                    return AnalysisResult.Cancelled;
                }
            }

            Profiler.BeginSample("CodeModule.AnalyzeAsync.BuildCallHierarchies");
            if (!run.CallCrawler.BuildCallHierarchies(run.FoundIssues.Where(i => i.Dependencies != null).ToList(), run.Progress, run.CancellationToken))
            {
                Profiler.EndSample();
                context.Discard();
                return AnalysisResult.Cancelled;
            }
            Profiler.EndSample();

            // Invoke any analyzers that need cross-method reachability (e.g. promoting a
            // diagnostic when its call chain reaches a known entry point). Analyzers opt in
            // by implementing ICallGraphCodeAnalyzer; the pipeline is otherwise unaware of
            // which descriptors participate.
            RunCallGraphAnalyzers(run);

            // Tag and optionally promote hot-path issues.
            foreach (var d in run.FoundIssues)
            {
                if (d.Dependencies == null || !d.Dependencies.PerfCriticalContext)
                    continue;

                d.SetEvidence("Hot Path", value: true);

                if (!d.IsMajorOrCritical)
                {
                    switch (d.Severity)
                    {
                        case Severity.Minor: d.Severity = Severity.Moderate; break;
                        case Severity.Moderate: d.Severity = Severity.Major; break;
                    }
                }
            }

            foreach (var analyzer in run.Plan.Compatible)
            {
                if (run.CancellationToken.IsCancellationRequested)
                {
                    context.Discard();
                    return AnalysisResult.Cancelled;
                }

                analyzer.Finalize(context, run.Progress);
            }

            context.Flush();

            // Report all collected code issues (workaround: relativePath not available at issue-found time)
            if (run.FoundIssues.Count > 0)
                session.ReportItems(run.FoundIssues);

            return AnalysisResult.Success;
        }

        // Player-bound precompiled assemblies only: UnityEngine, system framework, and
        // user/package precompiled DLLs. We deliberately exclude UnityEditor so editor-only
        // assemblies (UnityEditor.*, editor modules) don't surface as build-shipped insights.
        const PrecompiledAssemblyTypes k_PlayerPrecompiledFlags =
            PrecompiledAssemblyTypes.UserAssembly |
            PrecompiledAssemblyTypes.UnityEngine |
            PrecompiledAssemblyTypes.SystemAssembly;

        void ProcessPrecompiledAssemblies(AnalysisContext context)
        {
            var table = context.GetInsightTable(AnalysisCategory.PrecompiledAssembly, k_PrecompiledAssemblyInsightSchema);
            foreach (var assemblyPath in AssemblyResolver.GetPrecompiledAssemblyPaths(k_PlayerPrecompiledFlags))
            {
                table.AddRow(assemblyPath, new Dictionary<string, object>
                {
                    [PrecompiledAssemblyColumns.Name] = Path.GetFileNameWithoutExtension(assemblyPath),
                });
            }
            // Roslyn analyzer / source-generator DLLs are inventoried on
            // SessionInfo.RoslynAnalyzerDllPaths — they're build-time tooling, not assemblies
            // linked into the player, so they don't belong in this insight category.
        }

        CodeAnalysisPlan BuildAnalysisPlan(AnalysisOptions options)
        {
            var compatible = GetCompatibleAnalyzers(options);

            var typeLevel = new List<CodeAnalyzer>();
            var methodLevel = new List<CodeAnalyzer>();
            var callGraph = new List<ICallGraphCodeAnalyzer>();
            foreach (var analyzer in compatible)
            {
                if (analyzer.SupportsTypeAnalysis)
                    typeLevel.Add(analyzer);
                if (analyzer.SupportsMethodAnalysis)
                    methodLevel.Add(analyzer);
                if (analyzer is ICallGraphCodeAnalyzer callGraphAnalyzer)
                    callGraph.Add(callGraphAnalyzer);
            }

            var opCodeIndex = new Dictionary<OpCode, List<CodeAnalyzer>>();
            foreach (var opCode in m_OpCodes)
            {
                var opCodeAnalyzers = new List<CodeAnalyzer>();
                foreach (var analyzer in compatible)
                {
                    if (analyzer.OpCodes.Contains(opCode))
                        opCodeAnalyzers.Add(analyzer);
                }
                if (opCodeAnalyzers.Count > 0)
                    opCodeIndex[opCode] = opCodeAnalyzers;
            }

            return new CodeAnalysisPlan(
                compatible,
                typeLevel.ToArray(),
                methodLevel.ToArray(),
                callGraph.ToArray(),
                opCodeIndex);
        }

        void RunCallGraphAnalyzers(CodeAnalysisRun run)
        {
            var callGraphAnalyzers = run.Plan.CallGraph;
            if (callGraphAnalyzers.Length == 0)
            {
                return;
            }

            using var context = new CallGraphAnalysisContext(run.Options, run.Sink);
            context.Configure(run.CallCrawler, run.FoundIssues, item => run.FoundIssues.Remove(item));
            context.OnIssueCreated = diagnostic =>
            {
                diagnostic = CodeDiagnosticEnricher.EnrichCallGraphDiagnostic(diagnostic);
                run.FoundIssues.Add(diagnostic.ToReportItem());
                return diagnostic;
            };

            foreach (var analyzer in callGraphAnalyzers)
            {
                if (run.CancellationToken.IsCancellationRequested)
                {
                    context.Discard();
                    return;
                }
                analyzer.Analyze(context);
            }

            // The OnIssueCreated callback already routed each reported diagnostic into
            // foundIssues, so the context's pending buffer is now redundant. Discard to
            // satisfy AnalysisContext's "no pending items at dispose" invariant.
            context.Discard();
        }

        bool AnalyzeEditorAssemblies(CodeAnalysisRun run, AnalysisContext context)
        {
            using var assemblyProvider = new EditorAssemblyProvider();
            var assemblyInfos = assemblyProvider.GetAssemblies(run.Progress, run.CancellationToken);
            var editorAssemblyInfos = assemblyInfos
                .Where(info => !info.IsPackageReadOnly)
                .ToArray();

            // Editor IL can surface perf/memory patterns that runtime already reported;
            // keep correctness and editor-only API findings, drop duplicate hot-path noise.
            run.IssueReporter = issue =>
            {
                if ((issue.Descriptor.Impact & Impact.Performance) == Impact.Performance ||
                    (issue.Descriptor.Impact & Impact.Memory) == Impact.Memory)
                    return;
                run.FoundIssues.Add(issue);
            };

            return m_IlAnalyzer.AnalyzeAssemblies(
                run,
                editorAssemblyInfos,
                AssemblyResolver.GetPrecompiledAssemblyDirectories(PrecompiledAssemblyTypes.UnityEditor).ToArray());
        }

        void ProcessCompilerMessages(AnalysisContext context, AssemblyCompilationResult compilationResult)
        {
            Profiler.BeginSample("CodeModule.ProcessCompilerMessages");

            var compilerMessages = compilationResult.Messages;
            var severity = Severity.Info;
            if (compilationResult.Status == CompilationStatus.MissingDependency)
                severity = Severity.Warning;
            else if (compilerMessages.Any(m => m.Type == CompilerMessageType.Error))
                severity = Severity.Error;

            var assemblyInfo = AssemblyResolver.GetAssemblyInfoFromAssemblyPath(compilationResult.AssemblyPath);
            var assemblyTable = context.GetInsightTable(AnalysisCategory.Assembly, k_AssemblyInsightSchema);
            assemblyTable.AddRow(assemblyInfo.AsmDefPath, new Dictionary<string, object>
            {
                [AssemblyColumns.Name] = assemblyInfo.Name,
                [AssemblyColumns.ReadOnly] = assemblyInfo.IsPackageReadOnly,
                [AssemblyColumns.CompileTime] = compilationResult.DurationInMs / 1000.0f,
                [AssemblyColumns.StartTime] = Formatting.FormatUtcTimestamp(compilationResult.StartTime),
                [AssemblyColumns.EndTime] = Formatting.FormatUtcTimestamp(compilationResult.EndTime),
                [AssemblyColumns.Dependencies] = string.Join(", ", compilationResult.DependentAssemblyNames ?? new string[0]),
            });
            // Severity ({severity}) for failed-compile assemblies is lost in the migration.
            // Surface it as an issue (descriptor-bearing) if it's load-bearing.
            _ = severity;

            foreach (var message in compilerMessages)
            {
                // Suppress non-error messages from source files outside Assets/ when the
                // user prefers a quieter report. Targets Unity-package and third-party-package
                // warnings (CS0109, CS8019, CS0414, etc.) that the project doesn't own.
                // Errors are always kept -- a real compile failure must surface even when
                // it originates in package code, since it can prevent dependent assemblies
                // from compiling.
                if (UserPreferences.SuppressPackageCompilerMessages
                    && message.Type != CompilerMessageType.Error
                    && IsOutsideAssetsFolder(message.File))
                    continue;

                var relativePath = AssemblyResolver.ResolveAssetPath(assemblyInfo, message.File);
                var logLevel = CompilerMessageTypeToLogLevel(message.Type);
                // Compiler messages can carry blank lines between the diagnostic body and
                // a trailing "(Filename: ... Line: N)" footer; strip them so wrapped views
                // don't render whitespace rows.
                var messageText = Formatting.StripEmptyLines(message.Message);
                context.AddMessage(AnalysisCategory.CodeCompilerMessage, messageText)
                    .WithLogLevel(logLevel)
                    .WithLocation(relativePath, message.Line)
                    .WithProperty(CompilerMessageColumns.Code, message.Code)
                    .WithProperty(CompilerMessageColumns.Assembly, assemblyInfo.Name);
            }

            Profiler.EndSample();

            return;

            LogLevel CompilerMessageTypeToLogLevel(CompilerMessageType compilerMessageType)
            {
                switch (compilerMessageType)
                {
                    case CompilerMessageType.Error: return LogLevel.Error;
                    case CompilerMessageType.Warning: return LogLevel.Warning;
                    case CompilerMessageType.Info: return LogLevel.Info;
                }
                return LogLevel.Info;
            }
        }

        // True when filePath does NOT live under Assets/ (i.e. it's package code,
        // a Library/PackageCache/ resolved package, or an absolute path to a file:
        // package outside the project tree). Empty paths are treated as "inside
        // Assets/" so synthetic <Roslyn> exceptions with no file aren't suppressed.
        static bool IsOutsideAssetsFolder(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;
            var n = filePath.Replace('\\', '/');
            if (n.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                return false;
            // Absolute paths are package code unless the absolute path lands
            // inside the project's Assets/ directory. The /Assets/ contains-check
            // is a good-enough approximation because a project's own Assets
            // folder always appears as "/Assets/" somewhere in an absolute path
            // that points into it.
            if (Path.IsPathRooted(n)
                && n.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;
            return true;
        }
    }
}
