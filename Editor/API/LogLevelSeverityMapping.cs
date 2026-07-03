// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

namespace SmartAuditor.Editor
{
    /// <summary>
    /// Bidirectional mapping between <see cref="LogLevel"/> (message classification) and <see cref="Severity"/>
    /// stored on report items, used when emitting findings and displaying console-style icons in the UI.
    /// </summary>
    public static class LogLevelSeverityMapping
    {
        /// <summary>
        /// Maps a code issue <see cref="LogLevel"/> to the <see cref="Severity"/> stored on report items.
        /// </summary>
        public static Severity LogLevelToSeverity(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Error:
                    return Severity.Error;
                case LogLevel.Warning:
                    return Severity.Warning;
                default:
                    return Severity.Info;
            }
        }

        /// <summary>
        /// Maps report <see cref="Severity"/> back to <see cref="LogLevel"/> for console-style icons.
        /// For <see cref="Severity.Error"/> and <see cref="Severity.Info"/> (including <see cref="Severity.Minor"/>,
        /// which aliases Info), returns the corresponding log level; otherwise returns <see cref="LogLevel.Warning"/>
        /// (including for Warning/Moderate).
        /// </summary>
        public static LogLevel SeverityToLogLevel(Severity severity)
        {
            if (severity == Severity.Error)
                return LogLevel.Error;
            if (severity == Severity.Info)
                return LogLevel.Info;
            return LogLevel.Warning;
        }
    }
}
