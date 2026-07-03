// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.UI.Framework
{
    /// <summary>
    /// Options used to draw a shared issue details body.
    /// </summary>
    internal sealed class IssueDetailsBodyOptions
    {
        public ReportItem Item { get; set; }
        public Severity PresentationSeverity { get; set; }

        public bool ShowLocationPath { get; set; }
        public GUIStyle LocationStyle { get; set; }

        public bool AllowItemDescriptionFallback { get; set; } = true;
        public string DescriptionFallback { get; set; }
        public GUIStyle DescriptionStyle { get; set; }
        public float DescriptionMaxHeight { get; set; }

        public GUIStyle RecommendationStyle { get; set; }
        public float RecommendationMaxHeight { get; set; }

        public bool ShowNoRecommendationPlaceholder { get; set; }
        public string NoRecommendationPlaceholderText { get; set; }
        public GUIStyle NoRecommendationPlaceholderStyle { get; set; }

        public float MetadataBottomSpacing { get; set; } = UiLayout.SpaceSmall;
        public float DescriptionTopSpacing { get; set; } = 6f;
        public float RecommendationTopSpacing { get; set; } = UiLayout.SpaceMedium;
        public float NoRecommendationTopSpacing { get; set; } = 6f;
    }

    /// <summary>
    /// Shared issue details composer used by report and scoped views.
    /// </summary>
    internal static class IssueDetailsBody
    {
        public static void Draw(IssueDetailsBodyOptions options, Action drawMetadataRow, Action drawTitleRow = null)
        {
            if (options == null || options.Item == null)
                return;

            drawMetadataRow?.Invoke();

            if (options.MetadataBottomSpacing > 0f)
                EditorGUILayout.Space(options.MetadataBottomSpacing);

            drawTitleRow?.Invoke();

            if (options.ShowLocationPath)
            {
                var path = options.Item.Location?.Path;
                if (!string.IsNullOrEmpty(path))
                    EditorGUILayout.LabelField(path, options.LocationStyle ?? SharedStyles.LabelDarkWithDynamicSize);
            }

            var detailsText = IssueDetailsUi.GetDescription(
                options.Item,
                options.AllowItemDescriptionFallback,
                options.DescriptionFallback);
            if (!string.IsNullOrEmpty(detailsText))
            {
                if (options.DescriptionTopSpacing > 0f)
                    EditorGUILayout.Space(options.DescriptionTopSpacing);

                if (options.DescriptionMaxHeight > 0f)
                {
                    EditorGUILayout.LabelField(
                        detailsText,
                        options.DescriptionStyle ?? SharedStyles.TextArea,
                        GUILayout.MaxHeight(options.DescriptionMaxHeight));
                }
                else
                {
                    EditorGUILayout.LabelField(
                        detailsText,
                        options.DescriptionStyle ?? SharedStyles.TextArea);
                }
            }

            var recommendation = IssueDetailsUi.GetRecommendation(options.Item);
            if (!string.IsNullOrEmpty(recommendation))
            {
                if (options.RecommendationTopSpacing > 0f)
                    EditorGUILayout.Space(options.RecommendationTopSpacing);

                IssueDetailsUi.DrawRecommendationCallout(
                    recommendation,
                    SeverityPresentation.GetColorForSeverity(options.PresentationSeverity),
                    options.RecommendationStyle ?? SharedStyles.TextArea,
                    options.RecommendationMaxHeight);
            }
            else if (options.ShowNoRecommendationPlaceholder)
            {
                if (options.NoRecommendationTopSpacing > 0f)
                    EditorGUILayout.Space(options.NoRecommendationTopSpacing);

                EditorGUILayout.LabelField(
                    string.IsNullOrEmpty(options.NoRecommendationPlaceholderText)
                        ? "No recommendation is available for this issue."
                        : options.NoRecommendationPlaceholderText,
                    options.NoRecommendationPlaceholderStyle ?? SharedStyles.LabelDarkWithDynamicSize);
            }
        }
    }
}
