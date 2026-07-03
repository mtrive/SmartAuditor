using System.Collections.Generic;
using UnityEditor;

namespace SmartAuditor.Editor.Utils
{
    /// <summary>
    /// Reads Unity's External Tools "Generate .csproj files for" preference flags from EditorPrefs.
    /// Values mirror Unity's <c>ProjectGenerationFlag</c> enum in the IDE integration packages.
    /// </summary>
    internal static class ProjectGenerationPrefs
    {
        internal const string ProjectGenerationFlagKey = "unity_project_generation_flag";

        internal const int RegistryFlag = 1 << 2;
        internal const int GitFlag = 1 << 3;
        internal const int BuiltInFlag = 1 << 4;
        internal const int LocalTarBallFlag = 1 << 5;
        internal const int UnknownFlag = 1 << 6;

        internal const int OptionalPackageProjectGenerationFlags =
            RegistryFlag | GitFlag | BuiltInFlag | LocalTarBallFlag | UnknownFlag;

        static readonly (int Flag, string Label)[] s_OptionalPackageFlags =
        {
            (RegistryFlag, "Registry packages"),
            (GitFlag, "Git packages"),
            (BuiltInFlag, "Built-in packages"),
            (LocalTarBallFlag, "Local tarball"),
            (UnknownFlag, "Packages from unknown sources"),
        };

        internal static int GetProjectGenerationFlags()
        {
            return EditorPrefs.GetInt(ProjectGenerationFlagKey, 0);
        }

        internal static bool TryGetEnabledOptionalPackageFlags(out string[] enabledLabels)
        {
            var flags = GetProjectGenerationFlags();
            if ((flags & OptionalPackageProjectGenerationFlags) == 0)
            {
                enabledLabels = null;
                return false;
            }

            var labels = new List<string>(s_OptionalPackageFlags.Length);
            foreach (var (flag, label) in s_OptionalPackageFlags)
            {
                if ((flags & flag) == flag)
                    labels.Add(label);
            }

            enabledLabels = labels.ToArray();
            return enabledLabels.Length > 0;
        }
    }
}
