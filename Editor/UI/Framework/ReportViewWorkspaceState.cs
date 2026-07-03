using System;
using UnityEngine;

namespace SmartAuditor.Editor.UI.Framework
{
    /// <summary>
    /// Serialized report-view navigation state that survives Unity domain reloads.
    /// <see cref="AnalysisReportView"/> is recreated after a reload, so its workspace mode
    /// and investigation pins are persisted on <see cref="SmartAuditorWindow"/> instead.
    /// </summary>
    [Serializable]
    internal sealed class ReportViewWorkspaceState
    {
        public int workspaceMode;
        public string investigatedIssueId;
        public Severity investigatedIssueSeverity = Severity.Default;
        public Impact focusImpact = Impact.None;
        public AnalysisCategory investigatedMessageCategory;
        public string investigatedMessageGroupKey;
        public string expandedAffectedItemKey;
        public Vector2 affectedItemsScrollPos;
    }
}
