// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using UnityEngine;
using UnityEditor;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// A context object passed by a LightingModule to a LightingModuleAnalyzer's Analyze() method.
    /// </summary>
    public class LightingAnalysisContext : AnalysisContext
    {
        public LightingAnalysisContext(AnalysisOptions options, IFindingSink sink) : base(options, sink) { }

        /// <summary>
        /// The name of the lighting asset being analyzed
        /// </summary>
        public string Name;

        /// <summary>
        /// The path to the asset being analyzed
        /// </summary>
        public string AssetPath;
    }

    public class ReflectionProbeAnalysisContext : LightingAnalysisContext
    {
        public ReflectionProbeAnalysisContext(AnalysisOptions options, IFindingSink sink) : base(options, sink) { }
        public ReflectionProbe Probe { get; set; }
        public int Resolution { get; set; }
    }

    public class LightingSettingsAnalysisContext : LightingAnalysisContext
    {
        public LightingSettingsAnalysisContext(AnalysisOptions options, IFindingSink sink) : base(options, sink) { }
        public LightingSettings Settings { get; set; }
    }

    /// <summary>
    /// Abstract base class for an Analyzer to be invoked by LightingModule
    /// </summary>
    internal abstract class LightingAnalyzer : DiagnosticAnalyzer
    {
        /// <summary>
        /// Override this method to analyze ReflectionProbe assets
        /// </summary>
        public virtual void AnalyzeReflectionProbe(ReflectionProbeAnalysisContext context)
        {
        }

        /// <summary>
        /// Override this method to analyze LightingSettings assets
        /// </summary>
        public virtual void AnalyzeLightingSettings(LightingSettingsAnalysisContext context)
        {
        }
    }
}
