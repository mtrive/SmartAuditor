using System.Linq;
using SmartAuditor.Editor.Utils;
using UnityEditor;
using UnityEditor.PackageManager;

namespace SmartAuditor.Editor
{
    internal static class PackageInfo
    {
        const string k_CanonicalPath = "Packages/" + Name;

        static PackageInfo()
        {
            var paths = AssetDatabase.FindAssets("t:asmdef", new string[] { "Packages" })
                .Select(AssetDatabase.GUIDToAssetPath);
            var asmDefPath = paths.FirstOrDefault(path => path.EndsWith($"{BaseNamespace}.Editor.asmdef"));
            Path = string.IsNullOrEmpty(asmDefPath) ?
                k_CanonicalPath :
                PathUtils.GetDirectoryName(PathUtils.GetDirectoryName(asmDefPath));

            var packageInfo = PackageUtils.GetClientPackages().First(p => p.name == Name);

            IsLocal = packageInfo.source == PackageSource.Local;
            Version = packageInfo.version;
        }

        public static bool IsLocal { get; }

        public const string Name = "com.mtrive.smart-auditor";
        public const string BaseNamespace = "SmartAuditor";

        public static string Path { get; }

        public static string Version { get; }
    }
}
