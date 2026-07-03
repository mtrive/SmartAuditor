// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using SmartAuditor.Editor.Modules;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// Schemas shared across multiple modules. Lives here (not on any specific module) because
    /// the categories they describe are emitted by many modules — texture / mesh / audio analyzers
    /// all emit <see cref="AnalysisCategory.AssetIssue"/> findings — and locating the schema on
    /// one specific module would imply ownership it does not really have.
    /// </summary>
    internal static class SharedSchemas
    {
        internal static readonly InsightSchema AssetIssue = new InsightSchema();

        internal static readonly InsightSchema AssetImportMessage = new InsightSchema();

        internal static readonly InsightSchema ProjectSetting = new InsightSchema();

        internal static readonly InsightSchema GameObjectIssue = new InsightSchema(
            new InsightColumn(GameObjectIssueColumns.Scene, "Scene", PropertyFormat.Text),
            new InsightColumn(GameObjectIssueColumns.HierarchyPath, "Hierarchy Path", PropertyFormat.Text, longName: "GameObject Hierarchy Path", maxAutoWidth: 500),
            new InsightColumn(GameObjectIssueColumns.GlobalObjectId, "Global Object Id", PropertyFormat.Text, longName: "Unity GlobalObjectId", maxAutoWidth: 500, isHidden: true));
    }
}
