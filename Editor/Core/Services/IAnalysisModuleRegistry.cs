// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using UnityEditor;

namespace SmartAuditor.Editor.Core.Services
{
    /// <summary>
    /// Interface for managing Smart Auditor analysis modules.
    /// </summary>
    internal interface IAnalysisModuleRegistry
    {
        /// <summary>Gets all registered modules.</summary>
        IReadOnlyList<AnalysisModule> GetModules();

        /// <summary>
        /// Gets modules that emit findings for any of the specified categories. Matches against
        /// the full <see cref="AnalysisModule.Categories"/> union (owned layouts and categories
        /// plus borrowed categories). Returns an empty list when <paramref name="categories"/> is
        /// null or empty.
        /// </summary>
        IReadOnlyList<AnalysisModule> GetModules(AnalysisCategory[] categories);

        /// <summary>Gets modules that are compatible with the specified platform.</summary>
        IReadOnlyList<AnalysisModule> GetModulesForPlatform(BuildTarget platform);

        /// <summary>
        /// Initializes the registry by resetting the descriptor library, rebuilding it from
        /// analyzers, and discovering and instantiating every <see cref="AnalysisModule"/>.
        /// </summary>
        void Initialize();

        /// <summary>Resets the registry and the descriptor library.</summary>
        void Reset();

        /// <summary>Returns <c>true</c> if the registry has been initialized.</summary>
        bool IsInitialized();

        /// <summary>Gets all registered descriptor IDs.</summary>
        string[] GetAllDescriptorIds();

        /// <summary>Returns <c>true</c> if any registered module emits findings for the specified category.</summary>
        bool IsModuleSupported(AnalysisCategory category);

        /// <summary>Gets the total number of registered modules.</summary>
        int GetModuleCount();
    }
}
