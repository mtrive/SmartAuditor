using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.UI.Framework
{
    internal static class SharedStyles
    {
        const float k_RowSize = UiLayout.ComfortableListRowHeight;

        static GUIStyle s_Foldout;
        static GUIStyle s_BoldLabel;
        static GUIStyle s_Label;
        static GUIStyle s_LabelWrapped;
        static GUIStyle s_LinkLabel;
        static GUIStyle s_LinkLabelMuted;
        static GUIStyle s_RichLabel;
        static GUIStyle s_TextArea;
        static GUIStyle s_CodeSnippet;
        static GUIStyle s_CodeSnippetGutterDark;
        static GUIStyle s_CodeSnippetGutterLight;

        static GUIStyle s_LabelDarkWithDynamicSize;
        static GUIStyle s_WelcomeTextArea;
        static GUIStyle s_WelcomeCard;

        static GUIStyle s_TitleLabel;
        static GUIStyle s_MediumTitleLabel;
        static GUIStyle s_LargeLabel;
        static GUIStyle s_WrappedLargeLabel;
        static GUIStyle s_WhiteLargeLabel;

        static GUIStyle s_RowDark;
        static GUIStyle s_RowAlternateDark;
        static GUIStyle s_RowLight;
        static GUIStyle s_RowAlternateLight;
        static GUIStyle s_RowDarkBackground;
        static GUIStyle s_RowDarkBackgroundAlternate;
        static GUIStyle s_RowLightBackgroundAlternate;
        static GUIStyle s_SelectedRowStyleDark;
        static GUIStyle s_SelectedRowStyleLight;
        static GUIStyle s_CodeSnippetBackgroundDark;
        static GUIStyle s_CodeSnippetBackgroundLight;

        static GUIStyle s_TabHoverButtonDark;
        static GUIStyle s_TabHoverButtonLight;

        static GUIStyle s_DarkTextBoxBackground;
        static GUIStyle s_LightTextBoxBackground;
        static Font s_CodeSnippetFont;

        public static bool IsDarkMode => EditorGUIUtility.isProSkin;

        public static GUIStyle TabHoverButton
        {
            get
            {
                if (IsDarkMode)
                {
                    if (s_TabHoverButtonDark == null || s_TabHoverButtonDark.normal.background == null
                        || s_TabHoverButtonDark.hover.background == null || s_TabHoverButtonDark.active.background == null)
                    {
                        var darkBackground = Utility.MakeColorTexture(new Color(0.173f, 0.173f, 0.173f, 1));
                        var lightBackground = Utility.MakeColorTexture(new Color(0.25f, 0.25f, 0.25f, 1));
                        var activatedBackground = Utility.MakeColorTexture(new Color(0.4f, 0.4f, 0.4f, 1));

                        s_TabHoverButtonDark = new GUIStyle()
                        {
                            normal = { background = darkBackground, textColor = Color.white },
                            hover = { background = lightBackground, textColor = Color.white },
                            active = { background = activatedBackground, textColor = Color.white },
                            margin = new RectOffset(0, 0, 0, 0),
                            alignment = TextAnchor.MiddleCenter
                        };
                    }

                    return s_TabHoverButtonDark;
                }
                else
                {
                    if (s_TabHoverButtonLight == null || s_TabHoverButtonLight.hover.background == null
                        || s_TabHoverButtonLight.active.background == null)
                    {
                        var lightBackground = Utility.MakeColorTexture(new Color(0.85f, 0.85f, 0.85f, 1));
                        var activatedBackground = Utility.MakeColorTexture(new Color(0.95f, 0.95f, 0.95f, 1));

                        s_TabHoverButtonLight = new GUIStyle()
                        {
                            normal = { textColor = Color.black },
                            hover = { background = lightBackground, textColor = Color.black },
                            active = { background = activatedBackground, textColor = Color.black },
                            margin = new RectOffset(0, 0, 0, 0),
                            alignment = TextAnchor.MiddleCenter
                        };
                    }

                    return s_TabHoverButtonLight;
                }
            }
        }

        public static GUIStyle TextBoxBackground
        {
            get
            {
                if (IsDarkMode)
                {
                    if (s_DarkTextBoxBackground == null || s_DarkTextBoxBackground.normal.background == null)
                    {
                        var darkBackgroundTex = Utility.MakeColorTexture(new Color(0.173f, 0.173f, 0.173f, 1));

                        s_DarkTextBoxBackground = new GUIStyle()
                        {
                            normal = { background = darkBackgroundTex },
                            border = new RectOffset(2, 2, 2, 2),
                        };
                    }

                    return s_DarkTextBoxBackground;
                }
                else
                {
                    if (s_LightTextBoxBackground == null)
                    {
                        s_LightTextBoxBackground = new GUIStyle();
                    }

                    return s_LightTextBoxBackground;
                }
            }
        }

        /// <summary>
        /// Bold <see cref="EditorStyles.foldout"/> for section headers that use
        /// <see cref="EditorGUILayout.Foldout"/>.
        /// </summary>
        public static GUIStyle Foldout
        {
            get
            {
                if (s_Foldout == null)
                    s_Foldout = new GUIStyle(EditorStyles.foldout)
                    {
                        fontStyle = FontStyle.Bold
                    };
                return s_Foldout;
            }
        }

        /// <summary>
        /// Body-weight bold label for keys, counts, and inline emphasis in rows and detail panels.
        /// </summary>
        public static GUIStyle BoldLabel
        {
            get
            {
                if (s_BoldLabel == null)
                    s_BoldLabel = new GUIStyle(EditorStyles.boldLabel)
                    {
                        wordWrap = false,
                        alignment = TextAnchor.MiddleLeft
                    };
                return s_BoldLabel;
            }
        }

        public static GUIStyle Label
        {
            get
            {
                if (s_Label == null)
                    s_Label = new GUIStyle(EditorStyles.label)
                    {
                        wordWrap = false,
                        alignment = TextAnchor.MiddleLeft
                    };
                return s_Label;
            }
        }

        /// <summary>
        /// Word-wrapping variant of <see cref="Label"/>. Sizes to the content height, so
        /// it renders multi-line values without overflowing into adjacent controls.
        /// </summary>
        public static GUIStyle LabelWrapped
        {
            get
            {
                if (s_LabelWrapped == null)
                    s_LabelWrapped = new GUIStyle(EditorStyles.label)
                    {
                        wordWrap = true,
                        alignment = TextAnchor.UpperLeft
                    };
                return s_LabelWrapped;
            }
        }

        /// <summary>
        /// Unity link appearance for text buttons and clickable rows.
        /// </summary>
        public static GUIStyle LinkLabel
        {
            get
            {
                if (s_LinkLabel == null)
                    s_LinkLabel = new GUIStyle(EditorStyles.linkLabel)
                    {
                        alignment = TextAnchor.MiddleLeft
                    };
                return s_LinkLabel;
            }
        }

        /// <summary>
        /// Dimmed link style for secondary destinations in affected-item rows. Uses the same
        /// font size as <see cref="LinkLabel"/> but renders in grey so the message text on the
        /// left reads as the primary content and the filename on the right reads as metadata.
        /// </summary>
        public static GUIStyle LinkLabelMuted
        {
            get
            {
                if (s_LinkLabelMuted == null)
                    s_LinkLabelMuted = new GUIStyle(EditorStyles.linkLabel)
                    {
                        alignment = TextAnchor.MiddleLeft,
                        normal = { textColor = Color.gray },
                        hover = { textColor = Color.gray }
                    };
                return s_LinkLabelMuted;
            }
        }

        /// <summary>
        /// Single-line label with rich-text enabled. Used for issue-row titles where the domain
        /// prefix is rendered in a muted colour and the API symbol is bolded at draw time.
        /// </summary>
        public static GUIStyle RichLabel
        {
            get
            {
                if (s_RichLabel == null)
                    s_RichLabel = new GUIStyle(EditorStyles.label)
                    {
                        richText = true,
                        wordWrap = false,
                        alignment = TextAnchor.MiddleLeft
                    };
                return s_RichLabel;
            }
        }

        /// <summary>
        /// Read-only multi-line label for descriptions and recommendations. Supports rich text.
        /// </summary>
        public static GUIStyle TextArea
        {
            get
            {
                if (s_TextArea == null)
                    s_TextArea = new GUIStyle(EditorStyles.label)
                    {
                        richText = true,
                        wordWrap = true,
                        alignment = TextAnchor.UpperLeft
                    };
                return s_TextArea;
            }
        }

        public static GUIStyle CodeSnippet
        {
            get
            {
                if (s_CodeSnippet == null)
                {
                    s_CodeSnippet = new GUIStyle(EditorStyles.label)
                    {
                        wordWrap = false,
                        alignment = TextAnchor.UpperLeft,
                        richText = false,
                        font = GetCodeSnippetFont(),
                        fontSize = 11
                    };
                }

                return s_CodeSnippet;
            }
        }

        public static GUIStyle CodeSnippetGutter
        {
            get
            {
                if (IsDarkMode)
                {
                    if (s_CodeSnippetGutterDark == null)
                    {
                        s_CodeSnippetGutterDark = new GUIStyle(CodeSnippet)
                        {
                            alignment = TextAnchor.UpperRight,
                            normal = { textColor = new Color(0.60f, 0.60f, 0.60f, 1f) },
                            padding = new RectOffset(0, 4, 0, 0)
                        };
                    }

                    return s_CodeSnippetGutterDark;
                }

                if (s_CodeSnippetGutterLight == null)
                {
                    s_CodeSnippetGutterLight = new GUIStyle(CodeSnippet)
                    {
                        alignment = TextAnchor.UpperRight,
                        normal = { textColor = new Color(0.45f, 0.45f, 0.45f, 1f) },
                        padding = new RectOffset(0, 4, 0, 0)
                    };
                }

                return s_CodeSnippetGutterLight;
            }
        }

        public static GUIStyle CodeSnippetBackground
        {
            get
            {
                if (IsDarkMode)
                {
                    if (s_CodeSnippetBackgroundDark == null || s_CodeSnippetBackgroundDark.normal.background == null)
                    {
                        s_CodeSnippetBackgroundDark = new GUIStyle(GUIStyle.none)
                        {
                            normal = { background = Utility.MakeColorTexture(new Color(0.165f, 0.165f, 0.165f, 1f)) },
                            padding = new RectOffset(8, 8, 6, 6),
                            margin = new RectOffset(0, 0, 0, 0)
                        };
                    }

                    return s_CodeSnippetBackgroundDark;
                }

                if (s_CodeSnippetBackgroundLight == null || s_CodeSnippetBackgroundLight.normal.background == null)
                {
                    s_CodeSnippetBackgroundLight = new GUIStyle(GUIStyle.none)
                    {
                        normal = { background = Utility.MakeColorTexture(new Color(0.92f, 0.92f, 0.92f, 1f)) },
                        padding = new RectOffset(8, 8, 6, 6),
                        margin = new RectOffset(0, 0, 0, 0)
                    };
                }

                return s_CodeSnippetBackgroundLight;
            }
        }

        public static GUIStyle LabelDarkWithDynamicSize
        {
            get
            {
                if (s_LabelDarkWithDynamicSize == null)
                    s_LabelDarkWithDynamicSize = new GUIStyle(EditorStyles.label)
                    {
                        normal = { textColor = Color.gray },
                        wordWrap = false,
                        alignment = TextAnchor.MiddleLeft
                    };
                return s_LabelDarkWithDynamicSize;
            }
        }

        public static GUIStyle WelcomeTextArea
        {
            get
            {
                if (s_WelcomeTextArea == null)
                {
                    s_WelcomeTextArea = new GUIStyle(EditorStyles.label);
                    s_WelcomeTextArea.fontSize = 14;
                    s_WelcomeTextArea.richText = true;
                    s_WelcomeTextArea.wordWrap = true;
                    s_WelcomeTextArea.alignment = TextAnchor.UpperCenter;
                }

                return s_WelcomeTextArea;
            }
        }

        public static GUIStyle WelcomeCard
        {
            get
            {
                if (s_WelcomeCard == null)
                {
                    s_WelcomeCard = new GUIStyle(GUI.skin.box)
                    {
                        padding = new RectOffset(8, 8, 8, 8),
                        margin = new RectOffset(0, 0, 0, 0)
                    };
                }

                return s_WelcomeCard;
            }
        }

        public static GUIStyle TitleLabel
        {
            get
            {
                if (s_TitleLabel == null)
                {
                    s_TitleLabel = new GUIStyle(EditorStyles.boldLabel);
                    s_TitleLabel.fontSize = 26;
                }
                return s_TitleLabel;
            }
        }

        public static GUIStyle MediumTitleLabel
        {
            get
            {
                if (s_MediumTitleLabel == null)
                {
                    s_MediumTitleLabel = new GUIStyle(EditorStyles.boldLabel);
                    s_MediumTitleLabel.fontSize = 20;
                    s_MediumTitleLabel.fixedHeight = 30;
                }
                return s_MediumTitleLabel;
            }
        }

        public static GUIStyle LargeLabel
        {
            get
            {
                if (s_LargeLabel == null)
                {
                    s_LargeLabel = new GUIStyle(EditorStyles.boldLabel);
                    s_LargeLabel.fontSize = 14;
                    s_LargeLabel.fixedHeight = 22;
                    s_LargeLabel.alignment = TextAnchor.MiddleLeft;
                }
                return s_LargeLabel;
            }
        }

        public static GUIStyle WrappedLargeLabel
        {
            get
            {
                if (s_WrappedLargeLabel == null)
                {
                    s_WrappedLargeLabel = new GUIStyle(EditorStyles.boldLabel)
                    {
                        fontSize = 14,
                        wordWrap = true,
                        clipping = TextClipping.Clip,
                        alignment = TextAnchor.UpperLeft
                    };
                }
                return s_WrappedLargeLabel;
            }
        }

        public static GUIStyle WhiteLargeLabel
        {
            get
            {
                if (s_WhiteLargeLabel == null)
                {
                    s_WhiteLargeLabel = new GUIStyle(EditorStyles.boldLabel);
                    s_WhiteLargeLabel.fontSize = 14;
                    s_WhiteLargeLabel.fixedHeight = 22;
                    s_WhiteLargeLabel.alignment = TextAnchor.MiddleLeft;
                    s_WhiteLargeLabel.normal.textColor = Color.white;
                    s_WhiteLargeLabel.hover.textColor = Color.white;
                }
                return s_WhiteLargeLabel;
            }
        }

        public static GUIStyle Row
        {
            get
            {
                if (IsDarkMode)
                {
                    if (s_RowDark == null || s_RowDark.normal.background == null)
                    {
                        s_RowDark = new GUIStyle(GUIStyle.none)
                        {
                            normal = { background = Utility.MakeColorTexture(new Color(0.22f, 0.22f, 0.22f, 1.0f)) },
                            fixedHeight = k_RowSize
                        };
                    }

                    return s_RowDark;
                }
                else
                {
                    if (s_RowLight == null)
                    {
                        s_RowLight = new GUIStyle(GUIStyle.none)
                        {
                            fixedHeight = k_RowSize
                        };
                    }

                    return s_RowLight;
                }
            }
        }

        public static GUIStyle RowAlternate
        {
            get
            {
                if (IsDarkMode)
                {
                    if (s_RowAlternateDark == null || s_RowAlternateDark.normal.background == null)
                    {
                        s_RowAlternateDark = new GUIStyle(GUIStyle.none)
                        {
                            normal = { background = Utility.MakeColorTexture(new Color(0.275f, 0.275f, 0.275f, 1.0f)) },
                            fixedHeight = k_RowSize
                        };
                    }

                    return s_RowAlternateDark;
                }
                if (s_RowAlternateLight == null || s_RowAlternateLight.normal.background == null)
                {
                    s_RowAlternateLight = new GUIStyle(GUIStyle.none)
                    {
                        normal = { background = Utility.MakeColorTexture(new Color(0.729f, 0.729f, 0.729f, 1.0f)) },
                        fixedHeight = k_RowSize
                    };
                }

                return s_RowAlternateLight;
            }
        }

        /// <summary>Highlight style for the currently selected list row.</summary>
        public static GUIStyle SelectedRowStyle
        {
            get
            {
                if (IsDarkMode)
                {
                    if (s_SelectedRowStyleDark == null || s_SelectedRowStyleDark.normal.background == null)
                    {
                        s_SelectedRowStyleDark = new GUIStyle(GUIStyle.none)
                        {
                            normal = { background = Utility.MakeColorTexture(new Color(0.18f, 0.33f, 0.50f, 1f)) },
                            fixedHeight = k_RowSize
                        };
                    }

                    return s_SelectedRowStyleDark;
                }

                if (s_SelectedRowStyleLight == null || s_SelectedRowStyleLight.normal.background == null)
                {
                    s_SelectedRowStyleLight = new GUIStyle(GUIStyle.none)
                    {
                        normal = { background = Utility.MakeColorTexture(new Color(0.69f, 0.82f, 0.98f, 1f)) },
                        fixedHeight = k_RowSize
                    };
                }

                return s_SelectedRowStyleLight;
            }
        }

        public static GUIStyle RowBackground
        {
            get
            {
                if (IsDarkMode)
                {
                    if (s_RowDarkBackground == null || s_RowDarkBackground.normal.background == null)
                    {
                        s_RowDarkBackground = new GUIStyle(GUIStyle.none)
                        {
                            normal = { background = Utility.MakeColorTexture(new Color(0.22f, 0.22f, 0.22f, 1.0f)) }
                        };
                    }

                    return s_RowDarkBackground;
                }
                return GUIStyle.none;
            }
        }

        public static GUIStyle RowBackgroundAlternate
        {
            get
            {
                if (IsDarkMode)
                {
                    if (s_RowDarkBackgroundAlternate == null || s_RowDarkBackgroundAlternate.normal.background == null)
                    {
                        s_RowDarkBackgroundAlternate = new GUIStyle(GUIStyle.none)
                        {
                            normal = { background = Utility.MakeColorTexture(new Color(0.275f, 0.275f, 0.275f, 1.0f)) }
                        };
                    }

                    return s_RowDarkBackgroundAlternate;
                }
                if (s_RowLightBackgroundAlternate == null || s_RowLightBackgroundAlternate.normal.background == null)
                {
                    s_RowLightBackgroundAlternate = new GUIStyle(GUIStyle.none)
                    {
                        normal = { background = Utility.MakeColorTexture(new Color(0.729f, 0.729f, 0.729f, 1.0f)) }
                    };
                }

                return s_RowLightBackgroundAlternate;
            }
        }

        static Font GetCodeSnippetFont()
        {
            if (s_CodeSnippetFont != null)
                return s_CodeSnippetFont;

            try
            {
                // Platform-ordered fallback list. Font.CreateDynamicFontFromOSFont
                // tries each name in turn; Unity emits a warning for every name
                // that isn't installed before falling through. Putting the
                // OS-native font first means the warning never fires on common
                // platforms (e.g. Menlo on Windows logs "Unable to load font
                // face for [Menlo]" even though Consolas is found a moment later).
                s_CodeSnippetFont = Font.CreateDynamicFontFromOSFont(
                    GetMonospaceFontFallbackList(),
                    11);
            }
            catch
            {
                s_CodeSnippetFont = EditorStyles.label.font;
            }

            if (s_CodeSnippetFont == null)
                s_CodeSnippetFont = EditorStyles.label.font;

            return s_CodeSnippetFont;
        }

        static string[] GetMonospaceFontFallbackList()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                    return new[] { "Consolas", "Cascadia Mono", "Lucida Console", "Menlo", "DejaVu Sans Mono" };
                case RuntimePlatform.OSXEditor:
                    return new[] { "Menlo", "Monaco", "Andale Mono", "Consolas", "DejaVu Sans Mono" };
                case RuntimePlatform.LinuxEditor:
                    return new[] { "DejaVu Sans Mono", "Liberation Mono", "Ubuntu Mono", "Menlo", "Consolas" };
                default:
                    return new[] { "Menlo", "Consolas", "DejaVu Sans Mono" };
            }
        }
    }
}
