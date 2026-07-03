namespace SmartAuditor.Editor
{
    /// <summary>
    /// Reports progress for a long-running analysis run. Implementations may show a modal progress bar,
    /// an in-window activity log, or another UI; callers remain UI-agnostic.
    /// </summary>
    /// <remarks>
    /// <para><b>Lifecycle.</b> Each phase follows <c>Start</c> → one or more <c>Advance</c> calls → <c>Clear</c>.
    /// A single instance is reused across phases: calling <see cref="Start"/> again begins a new phase and
    /// resets the step counter for that phase.</para>
    /// <para><b>Null callers.</b> Progress is optional. Pass <see langword="null"/> to
    /// <see cref="SmartAuditor.Analyze(AnalysisOptions, IProgress, System.Threading.CancellationToken)"/>
    /// or use null-conditional calls (<c>progress?.Start(...)</c>) when no UI is needed.</para>
    /// <para><b>Thread safety.</b> Implementations are expected to be safe to call from background threads
    /// (typically by marshaling UI updates to the main thread).</para>
    /// <para><b>Cancellation.</b> Cancellation is not part of this interface. Pass a
    /// <see cref="System.Threading.CancellationToken"/> separately to
    /// <see cref="SmartAuditor.Analyze(AnalysisOptions, IProgress, System.Threading.CancellationToken)"/> or
    /// <see cref="SmartAuditor.AnalyzeAsync(AnalysisOptions, IProgress, System.Threading.CancellationToken)"/>.
    /// Concrete UI implementations (for example, a modal progress bar) may expose their own token when the user can cancel from the progress UI.</para>
    /// </remarks>
    public interface IProgress
    {
        /// <summary>
        /// Begins a new progress phase.
        /// </summary>
        /// <param name="title">Phase title shown in the progress UI. Empty values may be replaced by the implementation.</param>
        /// <param name="description">Initial status message for the phase; may be null.</param>
        /// <param name="total">Expected number of <see cref="Advance"/> calls in this phase. Implementations typically no-op when <paramref name="total"/> is zero or negative.</param>
        void Start(string title, string description, int total);

        /// <summary>
        /// Advances the current phase by one step.
        /// </summary>
        /// <param name="description">Optional per-step status message. When empty, the previous description from <see cref="Start"/> or the last non-empty <see cref="Advance"/> is retained.</param>
        /// <remarks>
        /// Callers need not match the step count passed to <see cref="Start"/> exactly; implementations clamp the displayed fraction.
        /// For long blocking work, calling <see cref="Advance"/> before the step starts keeps the UI aligned with current activity.
        /// </remarks>
        void Advance(string description = "");

        /// <summary>
        /// Ends the current progress phase.
        /// </summary>
        /// <remarks>
        /// Semantics are implementation-defined: modal progress bars typically hide the bar and reset counters;
        /// in-window reporters may preserve a scrolling log across phases and only reset phase state.
        /// Safe to call without a preceding <see cref="Start"/>.
        /// </remarks>
        void Clear();
    }
}
