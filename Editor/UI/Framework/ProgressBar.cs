using System;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.UI.Framework
{
    /// <summary>
    /// Progress bar implementation for Unity Editor operations.
    /// Thread-safe: calls from background threads are automatically marshaled to the main thread.
    /// Implements <see cref="IDisposable"/> to ensure cleanup even if exceptions occur.
    /// Cancellation is exposed via <see cref="CancellationToken"/> separately from <see cref="IProgress"/>.
    /// </summary>
    internal class ProgressBar : IProgress, IDisposable
    {
        const string k_CancelingTitle = "Canceling...";
        const string k_CancelingMessage = "Please wait while the operation is canceled";

        int m_Current;
        int m_Total;
        string m_Description;
        string m_Title;
        bool m_IsDisposed;

        readonly CancellationTokenSource m_CancellationTokenSource = new CancellationTokenSource();

        // Captured at construction time (always on the main thread) so we can marshal back to it.
        readonly SynchronizationContext m_MainThreadContext;

        public ProgressBar()
        {
            m_MainThreadContext = SynchronizationContext.Current;
        }

        /// <summary>A token that is canceled when the user clicks the cancel button.</summary>
        public CancellationToken CancellationToken => m_CancellationTokenSource.Token;

        /// <inheritdoc/>
        public void Advance(string description = "")
        {
            if (m_CancellationTokenSource.IsCancellationRequested || m_IsDisposed)
                return;

            if (!string.IsNullOrEmpty(description))
                m_Description = description;

            // Interlocked so that background-thread increments are atomic.
            var current = Interlocked.Increment(ref m_Current);

            if (m_MainThreadContext != null && SynchronizationContext.Current != m_MainThreadContext)
            {
                var snapshot = description;
                var snapshotCurrent = current;
                m_MainThreadContext.Post(_ => UpdateProgressBarUI(snapshot, snapshotCurrent), null);
                return;
            }

            UpdateProgressBarUI(description, current);
        }

        /// <inheritdoc/>
        public void Start(string title, string description, int total)
        {
            if (m_CancellationTokenSource.IsCancellationRequested || m_IsDisposed)
                return;

            if (total <= 0)
                return;

            if (string.IsNullOrEmpty(title))
                title = "Processing...";

            m_Current = 0;
            m_Total = total;
            m_Title = title;
            m_Description = description ?? string.Empty;

            if (m_MainThreadContext != null && SynchronizationContext.Current != m_MainThreadContext)
            {
                m_MainThreadContext.Post(_ => ShowInitialBar(), null);
                return;
            }

            ShowInitialBar();
        }

        /// <inheritdoc/>
        public void Clear()
        {
            if (m_IsDisposed)
                return;

            if (m_MainThreadContext != null && SynchronizationContext.Current != m_MainThreadContext)
            {
                m_MainThreadContext.Post(_ => EditorUtility.ClearProgressBar(), null);
                return;
            }

            EditorUtility.ClearProgressBar();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!m_IsDisposed)
            {
                Clear();
                m_IsDisposed = true;
            }
        }

        void UpdateProgressBarUI(string description, int current)
        {
            if (m_CancellationTokenSource.IsCancellationRequested || m_IsDisposed)
                return;

            var clamped = Mathf.Clamp(current, 0, m_Total);
            var progress = m_Total > 0 ? Mathf.Clamp01((float)clamped / m_Total) : 0f;
            var displayDescription = !string.IsNullOrEmpty(description) ? description : m_Description;

            if (!EditorUtility.DisplayCancelableProgressBar(m_Title, displayDescription, progress))
                return;

            EditorUtility.DisplayProgressBar(k_CancelingTitle, k_CancelingMessage, 0f);
            if (!m_CancellationTokenSource.IsCancellationRequested)
                m_CancellationTokenSource.Cancel();
        }

        void ShowInitialBar()
        {
            if (m_CancellationTokenSource.IsCancellationRequested || m_IsDisposed)
                return;

            if (!EditorUtility.DisplayCancelableProgressBar(m_Title, m_Description, 0f))
                return;

            EditorUtility.DisplayProgressBar(k_CancelingTitle, k_CancelingMessage, 0f);
            if (!m_CancellationTokenSource.IsCancellationRequested)
                m_CancellationTokenSource.Cancel();
        }
    }
}
