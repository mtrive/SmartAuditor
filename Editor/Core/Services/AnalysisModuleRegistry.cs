// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.Core.Services
{
    /// <summary>
    /// Default implementation of <see cref="IAnalysisModuleRegistry"/>. Discovers and manages
    /// <see cref="AnalysisModule"/> instances using Unity's <see cref="TypeCache"/>, and owns
    /// the <see cref="DescriptorLibrary"/> lifecycle.
    /// </summary>
    internal class AnalysisModuleRegistry : IAnalysisModuleRegistry
    {
        private readonly List<AnalysisModule> m_Modules = new List<AnalysisModule>();
        private readonly object m_Lock = new object();
        private bool m_Initialized;

        /// <inheritdoc/>
        public IReadOnlyList<AnalysisModule> GetModules()
        {
            lock (m_Lock)
            {
                return m_Modules.ToList();
            }
        }

        /// <inheritdoc/>
        public IReadOnlyList<AnalysisModule> GetModules(AnalysisCategory[] categories)
        {
            if (categories == null || categories.Length == 0)
            {
                return Array.Empty<AnalysisModule>();
            }

            var categorySet = new HashSet<AnalysisCategory>(categories);
            lock (m_Lock)
            {
                return m_Modules
                    .Where(m => m.Categories.Any(c => categorySet.Contains(c)))
                    .ToList();
            }
        }

        /// <inheritdoc/>
        public IReadOnlyList<AnalysisModule> GetModulesForPlatform(BuildTarget platform)
        {
            lock (m_Lock)
            {
                return m_Modules
                    .Where(m => CoreUtils.SupportsPlatform(m.GetType(), platform))
                    .ToList();
            }
        }

        /// <inheritdoc/>
        public void Initialize()
        {
            lock (m_Lock)
            {
                m_Modules.Clear();

                DescriptorLibrary.Reset();
                DescriptorLibrary.InitializeFromAnalyzers();

                // Two-phase init: instantiate every module first so AnalysisModule.Initialize()
                // overrides can query the registry and see the full set.
                var instances = new List<AnalysisModule>();
                foreach (var type in TypeCache.GetTypesDerivedFrom(typeof(AnalysisModule)))
                {
                    if (type.IsAbstract)
                    {
                        continue;
                    }

                    try
                    {
                        instances.Add((AnalysisModule)Activator.CreateInstance(type));
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[{SmartAuditor.CompactName}] Failed to instantiate module {type.Name}: {e.Message}\n{e.StackTrace}");
                    }
                }

                m_Modules.AddRange(instances);

                foreach (var instance in instances)
                {
                    try
                    {
                        instance.Initialize();
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[{SmartAuditor.CompactName}] Failed to initialize module {instance.GetType().Name}: {e.Message}\n{e.StackTrace}");
                        m_Modules.Remove(instance);
                    }
                }

                m_Initialized = true;
            }
        }

        /// <inheritdoc/>
        public void Reset()
        {
            lock (m_Lock)
            {
                m_Modules.Clear();
                DescriptorLibrary.Reset();
                m_Initialized = false;
            }
        }

        /// <inheritdoc/>
        public bool IsInitialized()
        {
            lock (m_Lock)
            {
                return m_Initialized;
            }
        }

        /// <inheritdoc/>
        public string[] GetAllDescriptorIds() => DescriptorLibrary.GetAllIds();

        /// <inheritdoc/>
        public bool IsModuleSupported(AnalysisCategory category)
        {
            lock (m_Lock)
            {
                return m_Modules.Any(m => m.Categories.Contains(category));
            }
        }

        /// <inheritdoc/>
        public int GetModuleCount()
        {
            lock (m_Lock)
            {
                return m_Modules.Count;
            }
        }
    }
}
