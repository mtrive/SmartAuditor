// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.UI.Framework
{
    /// <summary>
    /// Centers report body content horizontally with a max-width vertical scope.
    /// </summary>
    internal sealed class CenteredContentScope : IDisposable
    {
        bool m_Disposed;

        public static CenteredContentScope Begin(float maxWidth, bool expandHeight = false)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (expandHeight)
                EditorGUILayout.BeginVertical(GUILayout.MaxWidth(maxWidth), GUILayout.ExpandHeight(true));
            else
                EditorGUILayout.BeginVertical(GUILayout.MaxWidth(maxWidth));

            return new CenteredContentScope();
        }

        public void Dispose()
        {
            if (m_Disposed)
                return;

            EditorGUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            m_Disposed = true;
        }
    }
}
