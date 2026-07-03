using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor.PackageManager;
using UnityEngine;

namespace SmartAuditor.Editor.Utils
{
    internal static class PackageUtils
    {
        public static int CompareVersions(string lhs, string rhs)
        {
            const string regex = "[^0-9.]";
            var leftStr = Regex.Replace(lhs, regex, "", RegexOptions.IgnoreCase);
            var rightStr = Regex.Replace(rhs, regex, "", RegexOptions.IgnoreCase);
            var leftVersion = new Version(leftStr);
            var rightVersion = new Version(rightStr);
            return leftVersion.CompareTo(rightVersion);
        }

        public static PackageInfo[] GetClientPackages()
        {
#if UNITY_2021_1_OR_NEWER
            return PackageInfo.GetAllRegisteredPackages();
#else
            var getAllMethod = typeof(PackageInfo).GetMethod("GetAll", BindingFlags.Static | BindingFlags.NonPublic);
            if (getAllMethod != null)
            {
                return getAllMethod.Invoke(null, new object[] {}) as PackageInfo[];
            }
#endif
            throw new NotSupportedException("PackageInfo.GetAll() is not available.");
        }

        public static string GetPackageRecommendedVersion(UnityEditor.PackageManager.PackageInfo package)
        {
#if UNITY_2022_2_OR_NEWER
            return package.versions.recommended;
#else
            return package.versions.verified;
#endif
        }

        /// <summary>
        /// Check if a path is in a read-only package
        /// </summary>
        /// <param name="assetPath">Path to check</param>
        /// <returns>True if the path is in a read-only package</returns>
        public static bool IsAssetInReadOnlyPackage(string assetPath)
        {
            // Check if the path is in a package directory
            if (assetPath.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
            {
                // Get package info to check if it's read-only
                var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(assetPath);
                if (packageInfo != null)
                {
                    return packageInfo.source != PackageSource.Embedded && packageInfo.source != PackageSource.Local;
                }
            }

            return false;
        }

        public static bool IsClientPackage(string packageName)
        {
            var packages = GetClientPackages();
            if (packages != null)
            {
                foreach (var packageInfo in packages)
                {
                    if (packageInfo.name == packageName)
                        return true;
                }
            }

            Debug.LogWarning($"Can't find Package {packageName}.");

            return false;
        }
    }
}
