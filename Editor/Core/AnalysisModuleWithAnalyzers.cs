using System;
using System.Collections.Generic;
using UnityEditor;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// Base class for analysis modules that own a set of <see cref="DiagnosticAnalyzer"/> instances.
    /// Concrete modules should inherit from <c>AnalysisModule&lt;T&gt;</c> where <c>T</c> is the
    /// specific analyzer base type (e.g. <see cref="AssetAnalyzer"/>).
    /// </summary>
    internal abstract class AnalysisModule<T> : AnalysisModule where T : DiagnosticAnalyzer
    {
        T[] m_Analyzers;

        /// <summary>Returns all analyzers registered for this module.</summary>
        protected T[] GetAnalyzers() => m_Analyzers;

        /// <summary>
        /// Returns the subset of analyzers that are compatible with the target platform and have their
        /// diagnostic parameters applied.
        /// </summary>
        protected T[] GetCompatibleAnalyzers(AnalysisOptions options)
        {
            var analyzers = new List<T>();
            foreach (var analyzer in m_Analyzers)
            {
                if (CoreUtils.SupportsPlatform(analyzer.GetType(), options.Platform))
                {
                    analyzer.CacheParameters(options.DiagnosticParams);
                    analyzers.Add(analyzer);
                }
            }
            return analyzers.ToArray();
        }

        /// <inheritdoc/>
        public override void Initialize()
        {
            base.Initialize();

            var analyzers = new List<T>();
            foreach (var type in TypeCache.GetTypesDerivedFrom(typeof(T)))
            {
                if (type.IsAbstract)
                    continue;
                var analyzer = (DiagnosticAnalyzer)Activator.CreateInstance(type);
                analyzer.Initialize();
                analyzers.Add((T)analyzer);
            }
            m_Analyzers = analyzers.ToArray();
        }
    }
}
