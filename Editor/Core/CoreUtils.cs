using System;
using System.Linq;
using System.Reflection;
using SmartAuditor.Editor;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.Core
{
    internal static class CoreUtils
    {
        public static bool SupportsPlatform(Type type, BuildTarget platform)
        {
            var attributes = type.GetCustomAttributes<AnalysisPlatformAttribute>().ToList();
            if (attributes.Count == 0)
                return true;
            return attributes.Any(a => a.Platform == platform);
        }

        public static LogLevel LogTypeToLogLevel(LogType logType)
        {
            switch (logType)
            {
                case LogType.Assert:
                case LogType.Error:
                case LogType.Exception:
                    return LogLevel.Error;
                case LogType.Warning:
                    return LogLevel.Warning;
                default:
                    return LogLevel.Info;
            }
        }
    }
}
