using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using UnityEngine;

namespace SmartAuditor.Editor.Utils
{
    /// <summary>
    /// Utility class for detecting and retrieving version control system information.
    /// </summary>
    public static class VersionControlUtil
    {
        /// <summary>
        /// Gets the current version control revision for the project.
        /// Supports Git, Perforce, and other common VCS systems.
        /// </summary>
        /// <returns>The current revision/commit hash, or null if not available</returns>
        public static string GetCurrentRevision()
        {
            // Try Git first (most common)
            var gitRevision = GetGitRevision();
            if (!string.IsNullOrEmpty(gitRevision))
                return gitRevision;

            // Try Perforce
            var p4Revision = GetPerforceRevision();
            if (!string.IsNullOrEmpty(p4Revision))
                return p4Revision;

            // Try other VCS systems if needed
            // Could add SVN, Mercurial, etc. here

            return null;
        }

        /// <summary>
        /// Gets the Git commit hash for the current HEAD.
        /// </summary>
        /// <returns>The Git commit hash, or null if not in a Git repository</returns>
        static string GetGitRevision()
        {
            try
            {
                var gitRoot = FindGitRoot(Application.dataPath);
                if (gitRoot == null)
                    return null;

                var startInfo = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = "rev-parse HEAD",
                    WorkingDirectory = gitRoot,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                        return null;

                    process.WaitForExit(5000); // 5 second timeout

                    if (process.ExitCode == 0)
                    {
                        var output = process.StandardOutput.ReadToEnd().Trim();
                        return string.IsNullOrEmpty(output) ? null : output;
                    }
                }
            }
            catch (Win32Exception)
            {
                // Git executable is not available in this environment.
                return null;
            }
            catch (Exception ex)
            {
                // Log the exception but don't throw - this is a utility function
                UnityEngine.Debug.Log($"Failed to get Git revision: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Walks up from <paramref name="startPath"/> looking for a <c>.git</c> directory or file.
        /// </summary>
        static string FindGitRoot(string startPath)
        {
            var dir = startPath;
            while (!string.IsNullOrEmpty(dir))
            {
                var gitPath = Path.Combine(dir, ".git");
                if (Directory.Exists(gitPath) || File.Exists(gitPath))
                    return dir;

                var parent = Directory.GetParent(dir);
                if (parent == null)
                    break;

                dir = parent.FullName;
            }

            return null;
        }

        /// <summary>
        /// Gets the Perforce change-list number for the current workspace.
        /// </summary>
        /// <returns>The Perforce change-list number, or null if not in a Perforce workspace</returns>
        static string GetPerforceRevision()
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "p4",
                    Arguments = "changes -m 1 -s submitted",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(startInfo))
                {
                    if (process == null)
                        return null;

                    process.WaitForExit(5000); // 5 second timeout

                    if (process.ExitCode == 0)
                    {
                        var output = process.StandardOutput.ReadToEnd().Trim();
                        if (!string.IsNullOrEmpty(output))
                        {
                            // Parse the output to get the change number
                            // Format is typically: "Change 12345 on 2023/01/01 by user@workspace 'Description'"
                            var lines = output.Split('\n');
                            if (lines.Length > 0)
                            {
                                var firstLine = lines[0];
                                var changeIndex = firstLine.IndexOf("Change ");
                                if (changeIndex >= 0)
                                {
                                    var start = changeIndex + 7;
                                    var end = firstLine.IndexOf(" ", start);
                                    if (end > start)
                                        return firstLine.Substring(start, end - start);
                                }
                            }
                        }
                    }
                }
            }
            catch (Win32Exception)
            {
                // Perforce executable is not available in this environment.
                return null;
            }
            catch (Exception ex)
            {
                // Log the exception but don't throw - this is a utility function
                UnityEngine.Debug.Log($"Failed to get Perforce revision: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Gets a short version of the revision (first 8 characters for Git, full number for others).
        /// </summary>
        /// <param name="revision">The full revision string</param>
        /// <returns>A shortened version of the revision</returns>
        public static string GetShortRevision(string revision)
        {
            if (string.IsNullOrEmpty(revision))
                return null;

            // For Git hashes, return first 8 characters
            if (revision.Length >= 8 && IsHexString(revision))
                return revision.Substring(0, 8);

            // For other VCS (like Perforce), return as-is
            return revision;
        }

        /// <summary>
        /// Checks if a string contains only hexadecimal characters.
        /// </summary>
        /// <param name="str">The string to check</param>
        /// <returns>True if the string is hexadecimal</returns>
        static bool IsHexString(string str)
        {
            foreach (var c in str)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    return false;
            }
            return true;
        }
    }
}
