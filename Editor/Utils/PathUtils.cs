using SystemPath = System.IO.Path;

namespace SmartAuditor.Editor.Utils
{
    internal static class PathUtils
    {
        public const char Separator = '/';

        static readonly char k_DirectorySeparatorChar = SystemPath.DirectorySeparatorChar;
        static readonly char k_AltDirectorySeparatorChar = SystemPath.AltDirectorySeparatorChar;
        static readonly char k_VolumeSeparatorChar = SystemPath.VolumeSeparatorChar;

        public static string Combine(params string[] parts)
        {
            if (parts == null || parts.Length == 0)
                return string.Empty;

            var result = parts[0];
            for (var i = 1; i < parts.Length; ++i)
                result = Combine(result, parts[i]);

            return result;
        }

        public static string Combine(string path1, string path2)
        {
            return ReplaceSeparators(SystemPath.Combine(path1, path2));
        }

        public static string GetDirectoryName(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            var directory = SystemPath.GetDirectoryName(path);
            return directory == null ? string.Empty : ReplaceSeparators(directory);
        }

        public static string GetFullPath(string path)
        {
            return ReplaceSeparators(SystemPath.GetFullPath(path));
        }

        /// <summary>
        /// Returns the index at which the file extension begins, matching <see cref="SystemPath.GetExtension"/>.
        /// When there is no extension the index equals <paramref name="path"/>.Length so the suffix is empty.
        /// </summary>
        public static int GetExtensionIndexFromPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return 0;

            var length = path.Length;
            var num = length;
            while (--num >= 0)
            {
                var c = path[num];
                if (c == '.')
                    return num != length - 1 ? num : length;

                if (c == k_DirectorySeparatorChar || c == k_AltDirectorySeparatorChar || c == k_VolumeSeparatorChar)
                    return length;
            }

            return length;
        }

        public static int GetFilenameIndexFromPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return 0;

            var length = path.Length;
            var num = length;
            while (--num >= 0)
            {
                var c = path[num];
                if (c == k_DirectorySeparatorChar || c == k_AltDirectorySeparatorChar || c == k_VolumeSeparatorChar)
                    return num + 1;
            }

            return 0;
        }

        public static string ReplaceSeparators(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            var length = path.Length;
            var chars = new char[length];

            for (var i = 0; i < length; ++i)
            {
                if (path[i] == '\\')
                    chars[i] = Separator;
                else
                    chars[i] = path[i];
            }

            return new string(chars);
        }

        public static string ReplaceInvalidChars(string path)
        {
            return path.Replace('|', '_').Replace(":", string.Empty);
        }

        public static string[] Split(string path)
        {
            return ReplaceSeparators(path).Split(Separator);
        }
    }
}
