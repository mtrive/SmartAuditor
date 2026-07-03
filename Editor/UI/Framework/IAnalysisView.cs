// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using SmartAuditor.Editor.Core;
using UnityEngine;

namespace SmartAuditor.Editor.UI.Framework
{
    /// <summary>
    /// Contract for report views rendered by a host editor window.
    /// </summary>
    /// <remarks>
    /// The host window owns scroll state and calls the draw lifecycle methods each frame.
    /// Views read report data from the host rather than holding their own copy.
    /// </remarks>
    internal interface IAnalysisView
    {
        /// <summary>Short, human-readable description shown in the window header.</summary>
        string Description { get; }

        /// <summary>True when the host should wrap <see cref="DrawContent"/> in a vertical scroll view.</summary>
        bool ShowVerticalScrollView { get; }

        /// <summary>Persisted scroll position for the host vertical scroll view.</summary>
        Vector2 VerticalScrollViewPos { get; set; }

        /// <summary>Last measured size of the host scroll viewport, used for clip rect calculations.</summary>
        Vector2 LastVerticalScrollViewSize { get; set; }

        /// <summary>True when the view is fully initialized and ready to draw.</summary>
        bool IsValid();

        /// <summary>Called when the view is wired up to load any persisted UI state.</summary>
        void OnEnable();

        /// <summary>Loads any persisted UI state.</summary>
        void LoadSettings();

        /// <summary>Saves any persisted UI state.</summary>
        void SaveSettings();

        /// <summary>Adds the matching subset of <paramref name="issues"/> to the view.</summary>
        void AddIssues(IEnumerable<IReportEntry> issues);

        /// <summary>Drops every issue currently held by the view.</summary>
        void Clear();

        /// <summary>Marks the view as needing a redraw or data reload.</summary>
        void MarkDirty();

        /// <summary>Marks the view's column widths as needing recomputation. No-op for views without columns.</summary>
        void MarkColumnWidthsDirty();

        /// <summary>
        /// Renders content that must remain visible when <see cref="DrawContent"/> scrolls.
        /// </summary>
        /// <remarks>
        /// Called by the host immediately before opening its vertical scroll view, so anything
        /// drawn here stays pinned at the top of the report area regardless of the user's
        /// scroll position. Default implementation is a no-op; override on views that have a
        /// header (e.g. an investigation back link plus issue summary) the user should always
        /// be able to see while reading the body.
        /// </remarks>
        void DrawPinnedHeader();

        /// <summary>Renders the view's content.</summary>
        void DrawContent(bool showDetails = false);

        /// <summary>
        /// Renders content that must remain visible at the bottom of the report area, after
        /// <see cref="DrawContent"/>'s scroll view closes.
        /// </summary>
        /// <remarks>
        /// Symmetric counterpart to <see cref="DrawPinnedHeader"/>: anything drawn here stays
        /// pinned at the bottom regardless of the user's scroll position. Default implementation
        /// is a no-op; override on views that want a fixed footer (e.g. a per-selection details
        /// panel that should always be visible while the user reads the body). The host calls
        /// this after <see cref="DrawContent"/> on the same frame, so the override can rely on
        /// any state populated during content rendering.
        /// </remarks>
        void DrawPinnedFooter();
    }
}
