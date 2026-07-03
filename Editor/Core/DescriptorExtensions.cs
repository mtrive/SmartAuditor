using System;
using System.Linq;
using System.Text.RegularExpressions;
using SmartAuditor.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.Core
{
    internal static class DescriptorExtensions
    {
        public static string GetImpactSummary(this Descriptor descriptor)
        {
            return DescriptorLibrary.GetImpactString(descriptor.Impact);
        }

        public static bool MatchesAnyImpact(this Descriptor descriptor, Impact impactToMatch)
        {
            return (descriptor.Impact & impactToMatch) != 0;
        }

        public static string GetPlatformsSummary(this Descriptor descriptor)
        {
            return (descriptor.Platforms == null || descriptor.Platforms.Length == 0) ? "Any" : Formatting.CombineStrings(descriptor.Platforms.Select(p => p.ToString()).ToArray());
        }

        /// <summary>
        /// Check if the descriptor applies to the given platform
        /// </summary>
        public static bool IsPlatformCompatible(this Descriptor descriptor, BuildTarget buildTarget)
        {
            if (descriptor.Platforms == null || descriptor.Platforms.Length == 0)
                return true;
            return descriptor.Platforms.Contains(buildTarget);
        }

        public static bool IsApplicable(this Descriptor desc, AnalysisOptions options)
        {
            return desc.IsVersionCompatible() && desc.IsPlatformCompatible(options.Platform);
        }

        public static bool IsSupported(this Descriptor desc)
        {
            return desc.IsPlatformSupported() && desc.IsVersionCompatible();
        }

        /// <summary>
        /// Check if any descriptor's platforms are supported by the current editor
        /// </summary>
        public static bool IsPlatformSupported(this Descriptor desc)
        {
            var platforms = desc.Platforms;
            if (platforms == null)
                return true;
            foreach (var buildTarget in platforms)
            {
                var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
                if (BuildPipeline.IsBuildTargetSupported(buildTargetGroup, buildTarget))
                    return true;
            }

            return false;
        }

        static Version s_UnityVersion = (Version)null;

        /// <summary>
        /// Check if the descriptor's version is compatible with the current editor
        /// </summary>
        public static bool IsVersionCompatible(this Descriptor desc)
        {
            if (s_UnityVersion == null)
            {
                var unityVersionString = Application.unityVersion;
                unityVersionString = unityVersionString.Remove(
                    Regex.Match(unityVersionString, "[A-Za-z]").Index);
                s_UnityVersion = new Version(unityVersionString);
            }

            var minimumVersion = (Version)null;
            var maximumVersion = (Version)null;

            if (!string.IsNullOrEmpty(desc.MinimumVersion))
            {
                try
                {
                    minimumVersion = new Version(desc.MinimumVersion);
                }
                catch (Exception exception)
                {
                    Debug.LogErrorFormat("Descriptor ({0}) minimumVersion ({1}) is invalid. Exception: {2}", desc.Id, desc.MinimumVersion, exception.Message);
                }
            }

            if (!string.IsNullOrEmpty(desc.MaximumVersion))
            {
                try
                {
                    maximumVersion = new Version(desc.MaximumVersion);
                }
                catch (Exception exception)
                {
                    Debug.LogErrorFormat("Descriptor ({0}) maximumVersion ({1}) is invalid. Exception: {2}", desc.Id, desc.MaximumVersion, exception.Message);
                }
            }

            if (minimumVersion != null && maximumVersion != null && minimumVersion > maximumVersion)
            {
                Debug.LogErrorFormat("Descriptor ({0}) minimumVersion ({1}) is greater than maximumVersion ({2}).", desc.Id, minimumVersion, maximumVersion);
                return false;
            }

            if (minimumVersion != null && s_UnityVersion < minimumVersion)
                return false;
            if (maximumVersion != null && s_UnityVersion > maximumVersion)
                return false;

            return true;
        }
    }
}
