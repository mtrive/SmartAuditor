using System.IO;
using UnityEditor;
using UnityEngine;
using SmartAuditor.Editor.Settings;

namespace SmartAuditor.Editor.UI.Framework
{
    internal static class EditorInterop
    {
        public static void CopyToClipboard(string text)
        {
            EditorGUIUtility.systemCopyBuffer = text;
        }

        public static void OpenTextFile<T>(Location location) where T : UnityEngine.Object
        {
            var obj = AssetDatabase.LoadAssetAtPath<T>(location.Path);
            if (obj != null)
            {
                // open text file in the text editor
                AssetDatabase.OpenAsset(obj, location.Line ?? -1);
            }
        }

        /// <summary>
        /// Opens any text-like asset at the specified location using the main asset type.
        /// Works for <c>.shader</c>, <c>.hlsl</c>, <c>.cginc</c>, and similar text assets
        /// that cannot all be loaded as a single typed asset.
        /// </summary>
        public static void OpenTextAsset(Location location)
        {
            var obj = AssetDatabase.LoadMainAssetAtPath(location.Path);
            if (obj != null)
                AssetDatabase.OpenAsset(obj, location.Line ?? -1);
        }

        public static void OpenPackage(Location location)
        {
            var packageName = Path.GetFileName(location.Path);
            UnityEditor.PackageManager.UI.Window.Open(packageName);
        }

        public static void OpenProjectSettings(Location location)
        {
            if (location.Path.Equals("Project/Build"))
                BuildPlayerWindow.ShowBuildPlayerWindow();
            else
            {
                // Some Quality setting issue paths will end with the quality level name to identify a specific level
                // However, the SettingsService API does not support this, so we need to strip the level name
                var path = location.Path.StartsWith("Project/Quality") ? "Project/Quality" : location.Path;
                var window = SettingsService.OpenProjectSettings(path);
                window.Repaint();
            }
        }

        public static void FocusOnAssetInProjectWindow(Location location)
        {
            if (location == null || !location.IsValid)
                return;

            // Select and ping rather than ShowCreatedAsset, which triggers the inline rename flow
            // meant for newly created assets.
            var obj = AssetDatabase.LoadMainAssetAtPath(location.Path);
            if (obj == null)
            {
                Debug.LogWarning($"Smart Auditor: could not locate asset at '{location.Path}' in the Project window.");
                return;
            }

            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
        }

        public static void OpenUserPreferences(Location location)
        {
            var window = SettingsService.OpenUserPreferences(location.Path);
            if (window == null)
            {
                Debug.LogWarning($"Smart Auditor: could not open user preferences at '{location.Path}'.");
                return;
            }

            window.Repaint();
        }

        public static void OpenSettings()
        {
            var window = SettingsService.OpenProjectSettings($"Project/{SmartAuditor.CompactName}");
            window.Repaint();
        }
    }
}
