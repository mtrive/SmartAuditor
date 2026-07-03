using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.UI.Framework
{
    /// <summary>
    /// Registry that manages issue opening actions for different issue categories.
    /// Decouples action behavior from view descriptors.
    /// </summary>
    internal static class IssueActionRegistry
    {
        static readonly Dictionary<AnalysisCategory, Action<Location>> s_OpenActions = new Dictionary<AnalysisCategory, Action<Location>>();

        /// <summary>
        /// Initialize and register issue open actions that differ from the default behavior.
        /// Most asset categories use the default fallback (FocusOnAssetInProjectWindow) and don't need explicit registration.
        /// </summary>
        [InitializeOnLoadMethod]
        static void Initialize()
        {
            // Code categories - open in text editor
            RegisterOpenAction(AnalysisCategory.Code, EditorInterop.OpenTextFile<TextAsset>);
            RegisterOpenAction(AnalysisCategory.CodeCompilerMessage, EditorInterop.OpenTextFile<TextAsset>);

            // Shader compiler messages - location may point to a .shader, .hlsl, or .cginc file
            RegisterOpenAction(AnalysisCategory.ShaderCompilerMessage, EditorInterop.OpenTextAsset);

            // Package issues - open package manager
            RegisterOpenAction(AnalysisCategory.Package, EditorInterop.OpenPackage);

            // Project settings - custom logic for different path types
            RegisterOpenAction(AnalysisCategory.ProjectSetting, location =>
            {
                if (location.Path.StartsWith("Packages/"))
                {
                    EditorInterop.OpenPackage(location);
                    return;
                }

                if (location.Path.StartsWith("Preferences/"))
                {
                    EditorInterop.OpenUserPreferences(location);
                    return;
                }

                var guid = AssetDatabase.AssetPathToGUID(location.Path);
                if (string.IsNullOrEmpty(guid))
                {
                    EditorInterop.OpenProjectSettings(location);
                    return;
                }

                EditorInterop.FocusOnAssetInProjectWindow(location);
            });
        }

        /// <summary>
        /// Register a custom open action for a specific issue category
        /// </summary>
        /// <param name="category">The issue category</param>
        /// <param name="action">The action to execute when opening issues of this category</param>
        public static void RegisterOpenAction(AnalysisCategory category, Action<Location> action)
        {
            s_OpenActions[category] = action;
        }

        /// <summary>
        /// Opens an issue using the appropriate action for its category
        /// </summary>
        /// <param name="location">The location of the issue</param>
        /// <param name="category">The issue category</param>
        public static void OpenIssue(Location location, AnalysisCategory category)
        {
            if (location == null || !location.IsValid)
                return;

            if (s_OpenActions.TryGetValue(category, out var action))
            {
                action(location);
            }
            else
            {
                // Fallback: try to open as asset in project window
                EditorInterop.FocusOnAssetInProjectWindow(location);
            }
        }
    }
}

