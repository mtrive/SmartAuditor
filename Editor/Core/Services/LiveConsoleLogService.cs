// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.Core.Services
{
    /// <summary>
    /// Captures Unity Editor log callbacks and stores them in a bounded in-memory buffer.
    /// </summary>
    [InitializeOnLoad]
    internal sealed class LiveConsoleLogService
    {
        const int k_DefaultCapacity = 5000;

        readonly ConcurrentQueue<PendingEntry> m_PendingEntries = new ConcurrentQueue<PendingEntry>();
        readonly List<LiveConsoleMessage> m_Messages = new List<LiveConsoleMessage>(512);
        readonly object m_Gate = new object();

        int m_NextId;
        bool m_HasPendingChanges;
        bool m_PauseOnError;

        struct PendingEntry
        {
            public DateTime TimestampUtc;
            public string Message;
            public string StackTrace;
            public LogType LogType;
        }

        static LiveConsoleLogService()
        {
            Instance = new LiveConsoleLogService();
            Instance.Initialize();
        }

        LiveConsoleLogService()
        {
        }

        /// <summary>
        /// Shared singleton used by Smart Console.
        /// </summary>
        public static LiveConsoleLogService Instance { get; private set; }

        /// <summary>
        /// Maximum number of raw messages retained in memory.
        /// </summary>
        public int Capacity { get; set; } = k_DefaultCapacity;

        /// <summary>
        /// True when new messages have been added since the last drain operation.
        /// </summary>
        public bool HasPendingChanges => m_HasPendingChanges;

        /// <summary>
        /// Pauses the editor automatically when an error/assert/exception is received.
        /// </summary>
        public bool PauseOnError
        {
            get => m_PauseOnError;
            set => m_PauseOnError = value;
        }

        void Initialize()
        {
            Application.logMessageReceivedThreaded -= OnLogMessageReceivedThreaded;
            Application.logMessageReceivedThreaded += OnLogMessageReceivedThreaded;
            EditorApplication.update -= DrainPendingOnEditorUpdate;
            EditorApplication.update += DrainPendingOnEditorUpdate;
        }

        /// <summary>
        /// Removes all buffered messages.
        /// </summary>
        public void Clear()
        {
            lock (m_Gate)
                m_Messages.Clear();

            while (m_PendingEntries.TryDequeue(out _))
            {
            }

            m_HasPendingChanges = true;
        }

        /// <summary>
        /// Drains threaded callback queue into the main message buffer.
        /// </summary>
        /// <returns>Number of drained messages.</returns>
        public int DrainPendingEntries()
        {
            var drained = 0;
            while (m_PendingEntries.TryDequeue(out var pending))
            {
                AppendCore(pending);
                drained++;
            }

            if (drained > 0)
                m_HasPendingChanges = true;

            return drained;
        }

        /// <summary>
        /// Returns a snapshot of currently buffered messages.
        /// </summary>
        public List<LiveConsoleMessage> GetSnapshot()
        {
            lock (m_Gate)
                return new List<LiveConsoleMessage>(m_Messages);
        }

        /// <summary>
        /// Marks pending changes as consumed by the UI.
        /// </summary>
        public void ConsumePendingChanges()
        {
            m_HasPendingChanges = false;
        }

        void OnLogMessageReceivedThreaded(string message, string stackTrace, LogType logType)
        {
            m_PendingEntries.Enqueue(new PendingEntry
            {
                TimestampUtc = DateTime.UtcNow,
                Message = message ?? string.Empty,
                StackTrace = stackTrace ?? string.Empty,
                LogType = logType
            });
        }

        void DrainPendingOnEditorUpdate()
        {
            var drained = DrainPendingEntries();
            if (drained == 0)
                return;

            if (m_PauseOnError && ContainsErrorOrException())
                EditorApplication.isPaused = true;
        }

        bool ContainsErrorOrException()
        {
            lock (m_Gate)
            {
                if (m_Messages.Count == 0)
                    return false;

                var last = m_Messages[m_Messages.Count - 1];
                return last.LogType == LogType.Error ||
                    last.LogType == LogType.Assert ||
                    last.LogType == LogType.Exception;
            }
        }

        void AppendCore(PendingEntry pending)
        {
            var capacity = Math.Max(1, Capacity);
            lock (m_Gate)
            {
                m_Messages.Add(new LiveConsoleMessage(
                    id: ++m_NextId,
                    timestampUtc: pending.TimestampUtc,
                    logType: pending.LogType,
                    message: pending.Message,
                    stackTrace: pending.StackTrace));

                var overflow = m_Messages.Count - capacity;
                if (overflow > 0)
                    m_Messages.RemoveRange(0, overflow);
            }
        }

        // Test hook: allows constructing deterministic snapshots without requiring Unity callback plumbing.
        internal void AppendForTests(string message, string stackTrace, LogType logType, DateTime timestampUtc)
        {
            AppendCore(new PendingEntry
            {
                TimestampUtc = timestampUtc,
                Message = message,
                StackTrace = stackTrace,
                LogType = logType
            });
            m_HasPendingChanges = true;
        }
    }
}
