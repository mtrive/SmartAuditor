// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using SmartAuditor.Editor.Core;
using UnityEngine;

namespace SmartAuditor.Editor.Core.Services
{
    /// <summary>
    /// Immutable model describing a single Unity Editor console message.
    /// </summary>
    internal readonly struct LiveConsoleMessage
    {
        public LiveConsoleMessage(
            int id,
            DateTime timestampUtc,
            LogType logType,
            string message,
            string stackTrace,
            int count = 1)
        {
            Id = id;
            TimestampUtc = timestampUtc;
            LogType = logType;
            Message = message ?? string.Empty;
            StackTrace = stackTrace ?? string.Empty;
            Count = Math.Max(1, count);
        }

        /// <summary>
        /// Monotonic identifier for stable row selection.
        /// </summary>
        public int Id { get; }

        /// <summary>
        /// UTC timestamp captured when the message was received.
        /// </summary>
        public DateTime TimestampUtc { get; }

        /// <summary>
        /// Original Unity log classification.
        /// </summary>
        public LogType LogType { get; }

        /// <summary>
        /// Main message text.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Stack trace string captured from Unity callbacks.
        /// </summary>
        public string StackTrace { get; }

        /// <summary>
        /// Number of occurrences represented by this row (used when collapsed).
        /// </summary>
        public int Count { get; }

        /// <summary>
        /// Console-style level derived from <see cref="LogType"/>.
        /// </summary>
        public LogLevel LogLevel => CoreUtils.LogTypeToLogLevel(LogType);

        /// <summary>
        /// Key used to collapse duplicates while preserving level semantics.
        /// </summary>
        public string CollapseKey => $"{(int)LogType}|{Message}|{StackTrace}";

        /// <summary>
        /// Returns a copy with an updated duplicate count.
        /// </summary>
        public LiveConsoleMessage WithCount(int count)
        {
            return new LiveConsoleMessage(Id, TimestampUtc, LogType, Message, StackTrace, count);
        }
    }
}
