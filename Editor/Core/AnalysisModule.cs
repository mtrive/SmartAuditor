// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// Base class for Smart Auditor analysis modules. Any class derived from <see cref="AnalysisModule"/> will be
    /// instantiated by <see cref="SmartAuditor"/> and used to analyze the project.
    /// </summary>
    internal abstract class AnalysisModule
    {
        /// <summary>Gets the display name of this module.</summary>
        public abstract string Name { get; }

        /// <summary>All categories this module emits findings for: owned categories plus borrowed categories.</summary>
        public AnalysisCategory[] Categories => OwnedCategories
            .Concat(AdditionalEmittedCategories)
            .Distinct()
            .ToArray();

        /// <summary>
        /// Categories this module owns. Every category the module analyzes or emits data for
        /// must be listed here (insight inventory, diagnostics, and messages). Borrowed categories
        /// whose schema is owned elsewhere use <see cref="AdditionalEmittedCategories"/> instead.
        /// </summary>
        public virtual IReadOnlyCollection<AnalysisCategory> OwnedCategories =>
            System.Array.Empty<AnalysisCategory>();

        /// <summary>
        /// Per-category schemas for column metadata (labels, formats, hidden flags).
        /// Modules register every owned category; empty schemas are valid for categories
        /// with no custom <see cref="ReportItem.Properties"/> columns.
        /// </summary>
        public virtual IReadOnlyDictionary<AnalysisCategory, InsightSchema> CategorySchemas { get; } =
            s_EmptySchemas;

        static readonly IReadOnlyDictionary<AnalysisCategory, InsightSchema> s_EmptySchemas =
            new Dictionary<AnalysisCategory, InsightSchema>();

        /// <summary>
        /// Categories this module produces findings for whose layouts are owned by other modules.
        /// Defaults to empty. Modules whose analyzers emit findings under a "shared" category (e.g.
        /// asset-inventory modules emitting <see cref="AnalysisCategory.AssetIssue"/> findings whose
        /// layout lives on <see cref="AssetsModule"/>) override this to advertise the borrowed
        /// category to the analysis dispatcher.
        /// </summary>
        public virtual IReadOnlyCollection<AnalysisCategory> AdditionalEmittedCategories =>
            System.Array.Empty<AnalysisCategory>();

        /// <summary>
        /// The analysis source this module supports.
        /// If <c>null</c>, the module supports all analysis sources (Assets and LoadedScene).
        /// </summary>
        public virtual AnalysisSource? SupportedAnalysisSource => null;

        /// <summary>Called once by the registry after the module is instantiated.</summary>
        public virtual void Initialize() { }

        /// <summary>
        /// Performs analysis and returns a <see cref="Task{AnalysisResult}"/> that completes when the module
        /// finishes. Synchronous modules return <c>Task.FromResult(AnalysisResult.Success)</c>; modules with
        /// CPU-bound background work use <c>Task.Run</c> internally.
        /// </summary>
        /// <param name="session">The session providing options, progress, and result routing.</param>
        /// <param name="cancellationToken">Token to observe for cancellation requests.</param>
        public abstract Task<AnalysisResult> AnalyzeAsync(
            AnalysisSession session,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Runs the finalize phase for one or more analyzer groups. Each analyzer emits aggregate
        /// findings into a fresh <see cref="AnalysisContext"/> that flushes to the session when it
        /// leaves scope; the progress overlay is then cleared. Call once, after all per-item analysis
        /// contexts have left their own <c>using</c> scope.
        /// </summary>
        /// <remarks>
        /// Tier-3 follow-up: move context creation and lifecycle entirely into the orchestrator so
        /// modules never call <c>new AnalysisContext</c> or touch <see cref="AnalysisContext.Flush"/>
        /// directly — the fully Roslyn-style model where the driver owns buffering and
        /// <see cref="DiagnosticAnalyzer.Finalize"/> is a true compilation-end action. That requires
        /// refactoring every module, <see cref="AnalysisSession"/>, and <see cref="SmartAuditor"/>.
        /// </remarks>
        protected static void FinalizeAnalyzers(AnalysisSession session, params IEnumerable<DiagnosticAnalyzer>[] analyzerGroups)
        {
            using (var context = new AnalysisContext(session.Options, session))
            {
                foreach (var group in analyzerGroups)
                    foreach (var analyzer in group)
                        analyzer.Finalize(context, session.Progress);
            }
            session.Progress?.Clear();
        }
    }
}
