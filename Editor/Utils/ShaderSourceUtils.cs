using System.Text.RegularExpressions;

namespace SmartAuditor.Editor.Utils
{
    /// <summary>
    /// Utilities for processing shader source text.
    /// </summary>
    internal static class ShaderSourceUtils
    {
        // Matches block comments: /* ... */
        static readonly Regex s_BlockCommentRegex = new Regex(@"/\*.*?\*/", RegexOptions.Singleline | RegexOptions.Compiled);

        // Matches line comments: // ... (to end of line)
        static readonly Regex s_LineCommentRegex = new Regex(@"//.*?$", RegexOptions.Multiline | RegexOptions.Compiled);

        /// <summary>
        /// Removes both block comments (<c>/* ... */</c>) and line comments (<c>// ...</c>) from shader source code.
        /// </summary>
        /// <param name="shaderSource">The raw shader source text.</param>
        /// <returns>The source text with all comments replaced by empty strings.</returns>
        internal static string StripComments(string shaderSource)
        {
            var withoutBlockComments = s_BlockCommentRegex.Replace(shaderSource, string.Empty);
            return s_LineCommentRegex.Replace(withoutBlockComments, string.Empty);
        }
    }
}
