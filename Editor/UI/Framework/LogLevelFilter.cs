// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;

namespace SmartAuditor.Editor.UI.Framework
{
    [Flags]
    internal enum LogLevelMask
    {
        None = 0,
        Error = 1 << 0,
        Warning = 1 << 1,
        Info = 1 << 2,
        All = Error | Warning | Info
    }

    internal static class LogLevelFilter
    {
        public static bool IsMatch(LogLevelMask mask, LogLevel level)
        {
            return (mask & ToMask(level)) != 0;
        }

        public static bool HasLevel(LogLevelMask mask, LogLevel level)
        {
            return (mask & ToMask(level)) != 0;
        }

        public static LogLevelMask SetLevel(LogLevelMask mask, LogLevel level, bool enabled)
        {
            var levelMask = ToMask(level);
            return enabled ? mask | levelMask : mask & ~levelMask;
        }

        static LogLevelMask ToMask(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Error:
                    return LogLevelMask.Error;
                case LogLevel.Warning:
                    return LogLevelMask.Warning;
                case LogLevel.Info:
                default:
                    return LogLevelMask.Info;
            }
        }
    }
}
