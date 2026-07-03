using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.UI.Framework
{
    internal static class Utility
    {
        public enum IconType
        {
            Info,
            Warning,
            Error,

            Help,
            Refresh,
            Settings,

            StatusWheel,
            Load,
            Save,
            CopyToClipboard,
            FoldoutExpanded,
            FoldoutFolded
        }

        readonly struct IconDescriptor
        {
            public readonly string UnityIconName;
            public readonly string PackageIconName;
            public readonly string DefaultTooltip;

            public IconDescriptor(string unityIconName = null, string packageIconName = null, string defaultTooltip = null)
            {
                UnityIconName = unityIconName;
                PackageIconName = packageIconName;
                DefaultTooltip = defaultTooltip;
            }
        }

        static readonly IconDescriptor[] s_IconDescriptors =
        {
            /* Info */ new IconDescriptor("console.infoicon", defaultTooltip: "Info"),
            /* Warning */ new IconDescriptor("console.warnicon", defaultTooltip: "Warning"),
            /* Error */ new IconDescriptor("console.erroricon", defaultTooltip: "Error"),
            /* Help */ new IconDescriptor("_Help"),
            /* Refresh */ new IconDescriptor("Refresh"),
            /* Settings */ new IconDescriptor("Settings"),
            /* StatusWheel */ default,
            /* Load */ new IconDescriptor("Import"),
            /* Save */ new IconDescriptor("SaveAs"),
            /* CopyToClipboard */ new IconDescriptor(packageIconName: "CopyToClipboard", defaultTooltip: "Copy to Clipboard"),
            /* FoldoutExpanded */ new IconDescriptor(packageIconName: "FoldoutArrowOpen"),
            /* FoldoutFolded */ new IconDescriptor(packageIconName: "FoldoutArrowClose"),
        };

        static readonly GUIContent[] s_IconCache = new GUIContent[s_IconDescriptors.Length];
        static readonly GUIContent s_FoldoutExpandedFallback = new GUIContent("v");
        static readonly GUIContent s_FoldoutFoldedFallback = new GUIContent(">");
        static GUIContent[] s_StatusWheel;

        /// <summary>Foldout icon for expanded sections. Safe to use on repaint hot paths.</summary>
        public static GUIContent FoldoutExpandedIcon => GetIcon(IconType.FoldoutExpanded) ?? s_FoldoutExpandedFallback;

        /// <summary>Foldout icon for collapsed sections. Safe to use on repaint hot paths.</summary>
        public static GUIContent FoldoutFoldedIcon => GetIcon(IconType.FoldoutFolded) ?? s_FoldoutFoldedFallback;

        public static GUIContent GetIcon(IconType iconType, string tooltip = null)
        {
            if (iconType == IconType.StatusWheel)
                return GetStatusWheel();

            var index = (int)iconType;
            var cached = GetOrCreateIcon(index);
            if (cached == null)
                return null;

            if (string.IsNullOrEmpty(tooltip))
                tooltip = s_IconDescriptors[index].DefaultTooltip;

            if (tooltip == cached.tooltip)
                return cached;

            return new GUIContent(cached.text, cached.image, tooltip);
        }

        public static GUIContent GetLogLevelIcon(LogLevel logLevel, string tooltip = null)
        {
            switch (logLevel)
            {
                case LogLevel.Info:
                    return GetIcon(IconType.Info, tooltip);
                case LogLevel.Warning:
                    return GetIcon(IconType.Warning, tooltip);
                case LogLevel.Error:
                    return GetIcon(IconType.Error, tooltip);
                default:
                    return GetIcon(IconType.Help, tooltip);
            }
        }

        /// <summary>
        /// Console-style log icon for severities that map to <see cref="LogLevel"/> (Error, Warning, Info), via
        /// <see cref="LogLevelSeverityMapping.SeverityToLogLevel"/>.
        /// </summary>
        public static GUIContent GetSeverityLogLevelIcon(Severity severity, string tooltip = null)
        {
            return GetLogLevelIcon(LogLevelSeverityMapping.SeverityToLogLevel(severity), tooltip);
        }

        static GUIContent GetOrCreateIcon(int index)
        {
            var cached = s_IconCache[index];
            if (cached != null)
                return cached;

            var descriptor = s_IconDescriptors[index];
            if (!string.IsNullOrEmpty(descriptor.PackageIconName))
            {
                var texture = LoadIcon(descriptor.PackageIconName);
                if (texture == null)
                    return null;

                cached = string.IsNullOrEmpty(descriptor.DefaultTooltip)
                    ? EditorGUIUtility.TrIconContent(texture)
                    : EditorGUIUtility.TrIconContent(texture, descriptor.DefaultTooltip);
            }
            else if (!string.IsNullOrEmpty(descriptor.UnityIconName))
            {
                cached = string.IsNullOrEmpty(descriptor.DefaultTooltip)
                    ? EditorGUIUtility.TrIconContent(descriptor.UnityIconName)
                    : EditorGUIUtility.TrIconContent(descriptor.UnityIconName, descriptor.DefaultTooltip);
            }

            if (cached != null)
                s_IconCache[index] = cached;

            return cached;
        }

        static GUIContent GetStatusWheel()
        {
            if (s_StatusWheel == null)
            {
                s_StatusWheel = new GUIContent[12];
                for (var i = 0; i < 12; i++)
                    s_StatusWheel[i] = EditorGUIUtility.IconContent("WaitSpin" + i.ToString("00"));
            }

            var frame = (int)Mathf.Repeat(Time.realtimeSinceStartup * 10, 11.99f);
            return s_StatusWheel[frame];
        }

        static Texture2D LoadIcon(string iconName)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>($"{PackageInfo.Path}/Editor/Icons/{iconName}.png");
        }

        /// <summary>
        /// Creates a 1×1 texture for use as a <see cref="GUIStyle"/> background.
        /// Callers must cache the result; do not invoke on repaint hot paths.
        /// </summary>
        /// <param name="col">The solid color to fill the texture with.</param>
        /// <returns>A 1×1 texture with <see cref="HideFlags.HideAndDontSave"/>.</returns>
        public static Texture2D MakeColorTexture(Color col)
        {
            var result = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            result.SetPixel(0, 0, col);
            result.Apply();
            return result;
        }
    }
}
