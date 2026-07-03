// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.UI.Framework
{
    /// <summary>
    /// Centralized Repaint-guarded solid rect drawing for IMGUI views.
    /// </summary>
    internal static class SolidDraw
    {
        const float k_BarVerticalInset = 1f;
        const float k_DefaultSegmentGap = 2f;

        static readonly Color k_ChartSeparatorColor = new Color(0.3f, 0.3f, 0.3f);

        internal static Color HoverOverlayColor => SharedStyles.IsDarkMode
            ? new Color(1f, 1f, 1f, 0.08f)
            : new Color(0f, 0f, 0f, 0.06f);

        internal static Color SelectionOverlayColor => SharedStyles.IsDarkMode
            ? new Color(0.24f, 0.49f, 0.91f, 0.35f)
            : new Color(0.18f, 0.42f, 0.84f, 0.22f);

        internal static Color VerticalSeparatorColor => SharedStyles.IsDarkMode
            ? new Color(1f, 1f, 1f, 0.08f)
            : new Color(0f, 0f, 0f, 0.12f);

        internal static Color HorizontalSplitterColor => SharedStyles.IsDarkMode
            ? new Color(1f, 1f, 1f, 0.18f)
            : new Color(0f, 0f, 0f, 0.24f);

        static Color SegmentTrackColor => SharedStyles.IsDarkMode
            ? new Color(0.2f, 0.2f, 0.2f, 1f)
            : new Color(0.75f, 0.75f, 0.75f, 1f);

        static GUIStyle s_TooltipHitAreaStyle;

        /// <summary>
        /// Invisible style for tooltip hit areas. Do not use <see cref="GUIStyle.none"/> for
        /// large rects — Unity paints its internal magenta placeholder as the background.
        /// </summary>
        internal static GUIStyle TooltipHitAreaStyle
        {
            get
            {
                if (s_TooltipHitAreaStyle == null)
                {
                    s_TooltipHitAreaStyle = new GUIStyle
                    {
                        normal =
                        {
                            background = Utility.MakeColorTexture(new Color(0f, 0f, 0f, 0f)),
                            textColor = Color.clear
                        }
                    };
                }

                return s_TooltipHitAreaStyle;
            }
        }

        public static void Rect(Rect rect, Color color)
        {
            if (Event.current.type != EventType.Repaint)
                return;

            EditorGUI.DrawRect(rect, color);
        }

        public static void InsetRect(Rect outer, Color color, float top, float bottom, float left, float right)
        {
            var rect = new Rect(
                outer.x + left,
                outer.y + top,
                outer.width - left - right,
                outer.height - top - bottom);

            if (rect.width <= 0f || rect.height <= 0f)
                return;

            Rect(rect, color);
        }

        public static void VerticalBar(Rect rect, Color color)
        {
            InsetRect(rect, color, k_BarVerticalInset, k_BarVerticalInset, 0f, 0f);
        }

        public static void HorizontalRule(Rect rect, Color color)
        {
            Rect(rect, color);
        }

        public static void ProportionalSegments(Rect rect, ReadOnlySpan<float> values, ReadOnlySpan<Color> colors,
            float gap = k_DefaultSegmentGap)
        {
            if (values.Length != colors.Length || values.Length == 0)
                return;

            if (Event.current.type != EventType.Repaint)
                return;

            var totalValue = 0f;
            for (var i = 0; i < values.Length; i++)
                totalValue += values[i];

            if (totalValue <= 0f)
                return;

            var x = rect.x;
            for (var i = 0; i < values.Length; i++)
            {
                var value = values[i];
                if (value <= 0f)
                    continue;

                var barWidth = Math.Max(1f, rect.width * value / totalValue);
                InsetRect(
                    new Rect(x, rect.y, barWidth, rect.height),
                    colors[i],
                    k_BarVerticalInset,
                    0f,
                    0f,
                    gap);

                x += barWidth;
            }
        }

        public static void EqualSegments(Rect rect, ReadOnlySpan<Color> colors, float gap = k_DefaultSegmentGap)
        {
            if (colors.Length == 0)
                return;

            if (Event.current.type != EventType.Repaint)
                return;

            Rect(rect, SegmentTrackColor);

            var segmentWidth = rect.width / colors.Length;
            for (var i = 0; i < colors.Length; i++)
            {
                var x = rect.x + i * segmentWidth;
                InsetRect(
                    new Rect(x, rect.y, segmentWidth, rect.height),
                    colors[i],
                    k_BarVerticalInset,
                    0f,
                    0f,
                    gap);
            }
        }

        public static void Swatch(Color color, float size = 10f)
        {
            var swatchRect = GUILayoutUtility.GetRect(size, size, GUILayout.Width(size), GUILayout.Height(size));
            Rect(swatchRect, color);
        }

        public static void HorizontalSeparator()
        {
            var rect = EditorGUILayout.GetControlRect(GUILayout.Height(1));
            HorizontalRule(rect, k_ChartSeparatorColor);
        }

        public static void ThemeHoverOverlay(Rect rect)
        {
            Rect(rect, HoverOverlayColor);
        }

        public static void ThemeSelectionOverlay(Rect rect)
        {
            Rect(rect, SelectionOverlayColor);
        }

        public static void ThemeVerticalSeparator()
        {
            var sepRect = GUILayoutUtility.GetRect(3f, 3f, 1f, float.MaxValue, GUILayout.ExpandHeight(true));
            Rect(sepRect, VerticalSeparatorColor);
        }

        public static void ThemeHorizontalSeparator(Rect splitterRect)
        {
            var lineRect = new Rect(splitterRect.x, splitterRect.center.y - 0.5f, splitterRect.width, 1f);
            Rect(lineRect, HorizontalSplitterColor);
        }
    }
}
