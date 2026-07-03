using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// Base class for a context object passed by a Module to an Analyzer's Analyze() method.
    /// </summary>
    /// <remarks>
    /// AnalysisContext provides information to the Analyze() method which is used to decide which issues to report.
    /// It also provides helper methods to build Issues or Insights.
    /// </remarks>
    public class AnalysisContext : IDisposable
    {
        readonly IFindingSink m_Sink;

        /// <summary>
        /// The sink this context was constructed with. Exposed so helpers that hold a parent
        /// context can construct child contexts (e.g. per-type, per-method, per-instruction
        /// analysis contexts in the code module) without having to thread the sink separately.
        /// </summary>
        internal IFindingSink Sink => m_Sink;

        List<Diagnostic> m_PendingIssues;
        List<MessageBuilder> m_PendingMessages;
        Dictionary<AnalysisCategory, InsightTableBuilder> m_InsightBuilders;
        bool m_Disposed;

        public Func<Diagnostic, Diagnostic> OnIssueCreated;

        /// <summary>
        /// The AnalysisOptions object that was passed to (or created by) <see cref="SmartAuditor.Analyze(AnalysisOptions)"/>.
        /// </summary>
        /// <remarks>
        /// This contains information that can be useful during analysis: for example, the analysis target platform, or
        /// information to determine whether a particular Descriptor is applicable in the current analysis.
        /// </remarks>
        public AnalysisOptions Options { get; }

        /// <summary>
        /// Initializes a new AnalysisContext. The sink receives all findings emitted through this
        /// context when <see cref="Flush"/> is called. Modules pass the orchestrator's
        /// <see cref="AnalysisSession"/>; direct-API callers implement <see cref="IFindingSink"/>
        /// themselves.
        /// </summary>
        protected internal AnalysisContext(AnalysisOptions options, IFindingSink sink)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
            if (sink == null)
                throw new ArgumentNullException(nameof(sink));

            Options = options;
            m_Sink = sink;
            m_PendingIssues = new List<Diagnostic>();
            m_PendingMessages = new List<MessageBuilder>();
            m_InsightBuilders = new Dictionary<AnalysisCategory, InsightTableBuilder>();
        }

        /// <summary>
        /// Reports a diagnostic issue. This is the recommended way to report issues, similar to Roslyn analyzers.
        /// </summary>
        /// <param name="diagnostic">The diagnostic to report</param>
        public void ReportIssue(Diagnostic diagnostic)
        {
            if (diagnostic == null)
                throw new ArgumentNullException(nameof(diagnostic));

            if (OnIssueCreated != null)
                diagnostic = OnIssueCreated(diagnostic);

            m_PendingIssues.Add(diagnostic);
        }

        internal void Flush()
        {
            m_Sink.ReportItems(m_PendingIssues.Select(i => i.ToReportItem()));

            foreach (var builder in m_PendingMessages)
                m_Sink.ReportMessage(builder.Build());

            foreach (var builder in m_InsightBuilders.Values)
                m_Sink.ReportInsightTable(builder.Build());

            m_PendingIssues.Clear();
            m_PendingMessages.Clear();
            m_InsightBuilders.Clear();
        }

        /// <summary>
        /// Clears all pending findings from the context.
        /// </summary>
        internal void Discard()
        {
            m_PendingIssues.Clear();
            m_PendingMessages.Clear();
            m_InsightBuilders.Clear();
        }

        /// <summary>
        /// Returns the shared <see cref="InsightTableBuilder"/> for the given category. The first call
        /// for a category in this context establishes the schema; subsequent calls return the same
        /// builder and must pass the same schema reference. Multiple analyzers in one context can
        /// share a category builder and append rows independently.
        /// </summary>
        public InsightTableBuilder GetInsightTable(AnalysisCategory category, InsightSchema schema)
        {
            if (schema == null)
                throw new ArgumentNullException(nameof(schema));

            if (m_InsightBuilders.TryGetValue(category, out var existing))
            {
                if (!ReferenceEquals(existing.Schema, schema))
                    throw new InvalidOperationException(
                        $"InsightTable schema mismatch for {category}: a different schema was already registered in this context.");
                return existing;
            }

            var builder = new InsightTableBuilder(category, schema);
            m_InsightBuilders[category] = builder;
            return builder;
        }

        /// <summary>
        /// Begins building a toolchain message (compiler diagnostic, asset-importer warning, etc.).
        /// The returned builder is mutable and queued for flush; the analyzer can keep chaining calls
        /// after the builder is registered.
        /// </summary>
        public MessageBuilder AddMessage(AnalysisCategory category, string description)
        {
            var builder = new MessageBuilder(category, description);
            m_PendingMessages.Add(builder);
            return builder;
        }

        /// <summary>
        /// Checks whether a given Descriptor is enabled for the current analysis.
        /// </summary>
        /// <param name="descriptor">The descriptor to check</param>
        /// <returns>True if the Descriptor is applicable to the current target platform and Unity version, and if it's
        /// either enabled by default or by a Rule specified in Options. Otherwise, returns false.</returns>
        /// <remarks>
        /// The analysis for some Issues can take a long time to run, particularly in a large project. The Descriptors
        /// for such issues may declare <seealso cref="Descriptor.IsEnabledByDefault"/> to be false to stop them running
        /// when running Smart Auditor interactively in the Editor. When running Smart Auditor in a CI/CD environment
        /// it may be desirable to re-enable analysis for these Descriptors.
        /// <seealso cref="AnalysisOptions.WithAdditionalDiagnosticRules"/> can be used to add temporary Rules to increase
        /// the <seealso cref="Severity"/> of a Descriptor to anything other than Severity.None in order to re-enable
        /// analysis in this context.
        /// </remarks>
        public bool IsDescriptorEnabled(Descriptor descriptor, string context = "")
        {
            if (!descriptor.IsApplicable(Options))
                return false;

            var rule = Options.Rules.GetRule(descriptor.Id, context);
            if (rule != null)
                return rule.Severity != Severity.Suppressed;

            return descriptor.IsEnabledByDefault;
        }

        /// <summary>
        /// Flushes any findings still pending on this context to the sink, then marks it disposed.
        /// Idempotent: safe to call multiple times. Call <see cref="Discard"/> before disposing on
        /// cancellation or error paths where pending findings should not be reported.
        /// </summary>
        public void Dispose()
        {
            if (m_Disposed)
                return;

            Flush();
            m_Disposed = true;
        }
    }
}
