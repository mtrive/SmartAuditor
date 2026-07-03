// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

#if PACKAGE_ADDRESSABLES

using UnityEditor.AddressableAssets.Settings;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// A context object passed by AddressablesModule to an AddressableGroupAnalyzer's AnalyzeGroup() method.
    /// </summary>
    public class AddressableGroupAnalysisContext : AnalysisContext
    {
        public AddressableGroupAnalysisContext(AnalysisOptions options, IFindingSink sink) : base(options, sink) { }

        /// <summary>
        /// The AddressableAssetGroup to be analyzed.
        /// </summary>
        public AddressableAssetGroup Group;
    }

    internal abstract class AddressableGroupAnalyzer : DiagnosticAnalyzer
    {
        public abstract void AnalyzeGroup(AddressableGroupAnalysisContext group);
    }
}
#endif
