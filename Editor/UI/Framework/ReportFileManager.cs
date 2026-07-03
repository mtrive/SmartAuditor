// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.IO;
using SmartAuditor.Editor.Core;
using UnityEditor;
using UnityEngine;
using SmartAuditor.Editor.Settings;

namespace SmartAuditor.Editor.UI.Framework
{
    /// <summary>
    /// Handles file operations for Smart Auditor reports (save, load, autosave).
    /// This class encapsulates file I/O logic to keep the main window class cleaner.
    /// </summary>
    internal static class ReportFileManager
    {
        const string k_EditorPrefsAutosaveRelativePath = "SmartAuditor.AutosaveReportRelativePath";
        const string k_EditorPrefsSkipAutosaveRestoreOnce = "SmartAuditor.SkipAutosaveRestoreOnce";
        const string k_LoadFromFile = "Load from file";
        const string k_LoadingFailed = "Loading report from file was unsuccessful.";
        const string k_SaveToFile = "Save report to json file";
        const string k_SavingFailed = "Saving report to file was unsuccessful.";

        internal const string SkipAutosaveRestoreOnceKey = k_EditorPrefsSkipAutosaveRestoreOnce;

        /// <summary>
        /// Gets the full absolute path to the autosave file, creating a unique one if none exists yet.
        /// The path is persisted across domain reloads via <see cref="EditorPrefs"/>.
        /// </summary>
        static string GetAutosaveFilename()
        {
            var relativeStored = EditorPrefs.GetString(k_EditorPrefsAutosaveRelativePath, string.Empty);
            if (!string.IsNullOrEmpty(relativeStored))
            {
                var full = AssetPathUtils.ToAbsoluteProjectPath(relativeStored);
                if (File.Exists(full))
                    return full;
            }

            var relativeUnique = FileUtil.GetUniqueTempPathInProject() + ".json";
            EditorPrefs.SetString(k_EditorPrefsAutosaveRelativePath, relativeUnique);
            return AssetPathUtils.ToAbsoluteProjectPath(relativeUnique);
        }

        /// <summary>
        /// Saves a report to a user-selected file path.
        /// </summary>
        /// <param name="report">The report to save.</param>
        /// <param name="onSaved">Callback invoked after successful save with the file path.</param>
        /// <returns>True if the report was saved, false if the user cancelled.</returns>
        public static bool SaveReportToFile(Report report, Action<string> onSaved = null)
        {
            var path = EditorUtility.SaveFilePanel(k_SaveToFile, UserPreferences.LoadSavePath, report.DisplayName, "json");
            if (path.Length == 0)
                return false;

            try
            {
                report.Save(path,
                    UserPreferences.PrettifyJsonOutput,
                    UserPreferences.DebugReport,
                    UserPreferences.ReportExportContentMode);
                UserPreferences.LoadSavePath = Path.GetDirectoryName(path);
                EditorUtility.RevealInFinder(path);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(k_SaveToFile, k_SavingFailed, "Ok");
                return false;
            }

            onSaved?.Invoke(path);
            return true;
        }

        /// <summary>
        /// Loads a report from a user-selected file path.
        /// </summary>
        /// <param name="onLoaded">Callback invoked with the loaded report, or null if loading failed.</param>
        /// <returns>True if a file was selected (regardless of load success), false if cancelled.</returns>
        public static bool LoadReportFromFile(Action<Report> onLoaded)
        {
            var path = EditorUtility.OpenFilePanel(k_LoadFromFile, UserPreferences.LoadSavePath, "json");
            if (path.Length == 0)
                return false;

            if (!TryLoadReportFromPath(path, out var report))
            {
                EditorUtility.DisplayDialog(k_LoadFromFile, k_LoadingFailed, "Ok");
                onLoaded?.Invoke(null);
                return true;
            }

            UserPreferences.LoadSavePath = Path.GetDirectoryName(path);
            onLoaded?.Invoke(report);
            return true;
        }

        /// <summary>
        /// Loads a report from a specific file path.
        /// </summary>
        /// <param name="path">The file path to load from.</param>
        /// <returns>The loaded report, or null if loading failed.</returns>
        public static Report LoadReport(string path)
        {
            return TryLoadReportFromPath(path, out var report) ? report : null;
        }

        /// <summary>
        /// Autosaves a report to the temp directory so it survives a Unity domain reload.
        /// This is the canonical reload-survival mechanism for an in-Editor report — the
        /// window restores from this file via <see cref="TryLoadAutosavedReport"/> in
        /// <c>OnEnable</c> after the reload.
        /// </summary>
        /// <param name="report">The report to autosave.</param>
        public static void AutosaveReport(Report report)
        {
            try
            {
                EditorPrefs.DeleteKey(k_EditorPrefsSkipAutosaveRestoreOnce);
                report.SaveForReloadSurvival(GetAutosaveFilename());
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        /// <summary>
        /// Marks autosave restore to be skipped once after an explicit manual save and clears current autosave content.
        /// </summary>
        public static void SkipAutosaveRestoreAfterManualSave()
        {
            EditorPrefs.SetBool(k_EditorPrefsSkipAutosaveRestoreOnce, value: true);
            DeleteAutosave();
        }

        /// <summary>
        /// Attempts to load the autosaved report if it exists.
        /// </summary>
        /// <param name="onLoaded">Callback invoked with the loaded report, or null if not found or loading failed.</param>
        public static void TryLoadAutosavedReport(Action<Report> onLoaded)
        {
            if (EditorPrefs.GetBool(k_EditorPrefsSkipAutosaveRestoreOnce, defaultValue: false))
            {
                EditorPrefs.DeleteKey(k_EditorPrefsSkipAutosaveRestoreOnce);
                onLoaded?.Invoke(null);
                return;
            }

            var autosavePath = GetAutosaveFilename();

            if (!File.Exists(autosavePath))
            {
                onLoaded?.Invoke(null);
                return;
            }

            if (!TryLoadReportFromPath(autosavePath, out var report))
            {
                onLoaded?.Invoke(null);
                return;
            }

            onLoaded?.Invoke(report);
        }

        /// <summary>
        /// Deletes the autosaved report file if it exists and clears the stored path from preferences.
        /// </summary>
        public static void DeleteAutosave()
        {
            var relativeStored = EditorPrefs.GetString(k_EditorPrefsAutosaveRelativePath, string.Empty);
            try
            {
                if (!string.IsNullOrEmpty(relativeStored))
                {
                    var full = AssetPathUtils.ToAbsoluteProjectPath(relativeStored);
                    if (File.Exists(full))
                        File.Delete(full);
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                EditorPrefs.DeleteKey(k_EditorPrefsAutosaveRelativePath);
            }
        }

        static bool TryLoadReportFromPath(string path, out Report report)
        {
            report = null;
            try
            {
                report = Report.Load(path);
                return report != null;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return false;
            }
        }
    }
}
