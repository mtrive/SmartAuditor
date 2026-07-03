// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using SmartAuditor.Editor;
using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Utils;
using UnityEditor;
using UnityEditor.AssetImporters;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class AssetImportMessageAnalyzer : AssetAnalyzer
    {
        public override void Analyze(AssetAnalysisContext context)
        {
            var importLog = AssetImporter.GetImportLog(context.AssetPath);
            if (importLog == null)
                return;
            foreach (var logEntry in importLog.logEntries)
            {
                if (logEntry.flags != ImportLogFlags.Error && logEntry.flags != ImportLogFlags.Warning)
                    continue;
                var logLevel = logEntry.flags == ImportLogFlags.Error ? LogLevel.Error : LogLevel.Warning;
                // Asset importers often embed blank lines between the message and the trailer
                // (e.g. "(Filename: ... Line: N)"). Strip them so wrapped views don't waste
                // a row on whitespace.
                var message = Formatting.StripEmptyLines(logEntry.message);
                context.AddMessage(AnalysisCategory.AssetImportMessage, message)
                    .WithLogLevel(logLevel)
                    .WithLocation(new Location(context.AssetPath));
            }
        }
    }
}
