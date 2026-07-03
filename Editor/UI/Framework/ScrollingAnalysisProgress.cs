// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.UI.Framework
{
    /// <summary>
    /// Progress reporting for long-running analysis with an in-window scrolling activity log.
    /// Thread-safe: calls from background threads are marshaled to the main thread.
    /// Does not use <see cref="EditorUtility.DisplayCancelableProgressBar"/>; use <see cref="RequestCancel"/> from UI.
    /// </summary>
    internal sealed class ScrollingAnalysisProgress : IProgress, IDisposable
    {
        const int k_MaxLogLines = 500;

        readonly List<string> m_Lines = new List<string>(256);
        readonly object m_LinesLock = new object();
        readonly CancellationTokenSource m_CancellationTokenSource = new CancellationTokenSource();
        readonly SynchronizationContext m_MainThreadContext;
        readonly EditorWindow m_HostWindow;

        int m_Current;
        int m_Total;
        int m_DisplayCurrent;
        string m_Description = string.Empty;
        string m_Title = string.Empty;
        bool m_IsDisposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScrollingAnalysisProgress"/> class.
        /// </summary>
        /// <param name="hostWindow">Editor window to repaint when progress updates; may be null (no automatic repaint).</param>
        public ScrollingAnalysisProgress(EditorWindow hostWindow)
        {
            m_HostWindow = hostWindow;
            m_MainThreadContext = SynchronizationContext.Current;
        }

        /// <summary>Gets a token that is cancelled when <see cref="RequestCancel"/> is called.</summary>
        public CancellationToken CancellationToken => m_CancellationTokenSource.Token;

        /// <summary>Gets the title from the last <see cref="Start"/> call.</summary>
        public string Title => m_Title;

        /// <summary>Gets the linear progress for the current phase (0–1).</summary>
        public float ProgressFraction
        {
            get
            {
                if (m_Total <= 0)
                    return 0f;
                var clamped = Mathf.Clamp(m_DisplayCurrent, 0, m_Total);
                return Mathf.Clamp01((float)clamped / m_Total);
            }
        }

        /// <summary>Copies the current log lines for display (thread-safe snapshot).</summary>
        public void CopyLogLines(List<string> destination)
        {
            destination.Clear();
            lock (m_LinesLock)
                destination.AddRange(m_Lines);
        }

        /// <summary>Requests cancellation of the analysis (same role as the modal cancel button).</summary>
        public void RequestCancel()
        {
            if (!m_CancellationTokenSource.IsCancellationRequested)
                m_CancellationTokenSource.Cancel();
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

            if (m_MainThreadContext != null && SynchronizationContext.Current != m_MainThreadContext)
            {
                var t = title;
                var d = description;
                var tot = total;
                m_MainThreadContext.Post(_ => ApplyStart(t, d, tot), null);
                return;
            }

            ApplyStart(title, description, total);
        }

        /// <inheritdoc/>
        public void Advance(string description = "")
        {
            if (m_CancellationTokenSource.IsCancellationRequested || m_IsDisposed)
                return;

            if (!string.IsNullOrEmpty(description))
                m_Description = description;

            var current = Interlocked.Increment(ref m_Current);

            if (m_MainThreadContext != null && SynchronizationContext.Current != m_MainThreadContext)
            {
                var snapshot = description;
                var snapshotCurrent = current;
                m_MainThreadContext.Post(_ => ApplyAdvance(snapshot, snapshotCurrent), null);
                return;
            }

            ApplyAdvance(description, current);
        }

        /// <inheritdoc/>
        public void Clear()
        {
            // Preserve the scrolling log across module phases (modules call Clear between phases).
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!m_IsDisposed)
                m_IsDisposed = true;
        }

        void ApplyStart(string title, string description, int total)
        {
            if (m_CancellationTokenSource.IsCancellationRequested || m_IsDisposed)
                return;

            m_Current = 0;
            m_DisplayCurrent = 0;
            m_Total = total;
            m_Title = title;
            m_Description = description ?? string.Empty;

            lock (m_LinesLock)
            {
                m_Lines.Add($"— {title} —");
                if (!string.IsNullOrEmpty(m_Description))
                    m_Lines.Add(m_Description);
                TrimLogIfNeeded();
            }

            RequestRepaint();
        }

        void ApplyAdvance(string description, int current)
        {
            if (m_CancellationTokenSource.IsCancellationRequested || m_IsDisposed)
                return;

            m_DisplayCurrent = Mathf.Clamp(current, 0, m_Total > 0 ? m_Total : int.MaxValue);

            if (!string.IsNullOrEmpty(description))
            {
                lock (m_LinesLock)
                {
                    m_Lines.Add(description);
                    TrimLogIfNeeded();
                }
            }

            RequestRepaint();
        }

        void TrimLogIfNeeded()
        {
            while (m_Lines.Count > k_MaxLogLines)
                m_Lines.RemoveAt(0);
        }

        void RequestRepaint()
        {
            m_HostWindow?.Repaint();
        }
    }
}
