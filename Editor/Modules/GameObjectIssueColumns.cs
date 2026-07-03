// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

namespace SmartAuditor.Editor.Modules
{
    /// <summary>
    /// Property key constants for <see cref="AnalysisCategory.GameObjectIssue"/> diagnostics.
    /// Shared by scene and prefab analyzers.
    /// </summary>
    internal static class GameObjectIssueColumns
    {
        public const string Scene = "Scene";
        public const string HierarchyPath = "HierarchyPath";
        public const string GlobalObjectId = "GlobalObjectId";
    }
}
