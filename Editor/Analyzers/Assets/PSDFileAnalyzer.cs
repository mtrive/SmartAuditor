// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class PsdFileAnalyzer : AssetAnalyzer
    {
        internal const string CLN0002 = nameof(CLN0002);

        internal static readonly Descriptor PsdFileDescriptor = new Descriptor(
            CLN0002,
            "Asset: PSD Source File",
            Impact.EditorWorkflow,
            "The project contains a <b>.psd</b> source file. PSD files are larger than equivalent PNGs, slow asset imports, inflate version-control history, and can trigger Photoshop on import.",
            "Flatten the layered art to <b>.png</b> before import, and keep the layered <b>.psd</b> source outside the Unity project tree."
        )
        {
            DefaultSeverity = Severity.Minor,
            MessageFormat = "PSD source file '{0}' is in the project"
        };

        public override void Analyze(AssetAnalysisContext context)
        {
            if (!context.AssetPath.EndsWith(".psd", StringComparison.OrdinalIgnoreCase))
                return;

            if (!context.IsDescriptorEnabled(PsdFileDescriptor, context.AssetPath))
                return;
            var diagnostic = Diagnostic.Create(
                    AnalysisCategory.AssetIssue,
                    PsdFileDescriptor.Id,
                    context.AssetName)
                .WithLocation(new Location(context.AssetPath));
            context.ReportIssue(diagnostic);
        }
    }
}
