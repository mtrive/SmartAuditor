using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace SmartAuditor.Editor
{
    // Project-level compilation overrides applied on top of Unity's per-asmdef
    // define set and any csc.rsp defines.
    [Serializable]
    internal sealed class CompilationOverrides
    {
        [JsonProperty("additional_defines")]
        [SerializeField]
        List<string> m_AdditionalDefines = new List<string>();

        [JsonProperty("removed_defines")]
        [SerializeField]
        List<string> m_RemovedDefines = new List<string>();

        public IReadOnlyList<string> AdditionalDefines => m_AdditionalDefines;
        public IReadOnlyList<string> RemovedDefines => m_RemovedDefines;

        public CompilationOverrides()
        { }

        // Returns the user's defines with whitespace trimmed, invalid tokens
        // dropped, and duplicates removed -- without mutating the underlying
        // list. Validation happens here (not in Save) because the inspector
        // saves after every edit, and stripping a freshly-added empty slot
        // mid-typing would prevent the user from entering text at all.
        public IReadOnlyList<string> GetValidDefines() => Sanitize(m_AdditionalDefines);

        // Same trim/validate/dedup pass for the subtractive list. Tokens here
        // are stripped from the resolved compilation set after asmdef + csc.rsp
        // + additional defines have been merged, so this list can also drop
        // Unity-injected symbols (for example forcing UNITY_EDITOR off when
        // analysing an Editor-only asmdef as a player target).
        public IReadOnlyList<string> GetValidRemovedDefines() => Sanitize(m_RemovedDefines);

        static IReadOnlyList<string> Sanitize(List<string> source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<string>();

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var kept = new List<string>(source.Count);
            foreach (var raw in source)
            {
                if (string.IsNullOrWhiteSpace(raw))
                    continue;
                var trimmed = raw.Trim();
                if (!IsValidDefine(trimmed))
                    continue;
                if (seen.Add(trimmed))
                    kept.Add(trimmed);
            }
            return kept;
        }

        // C# preprocessor identifiers follow the same rules as C# identifiers:
        // first char letter or underscore, remaining chars letters / digits /
        // underscores. ASCII-only is enough for our purposes -- Unity's own
        // define set is ASCII.
        public static bool IsValidDefine(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;
            var first = value[0];
            if (!(char.IsLetter(first) || first == '_'))
                return false;
            for (var i = 1; i < value.Length; i++)
            {
                var c = value[i];
                if (!(char.IsLetterOrDigit(c) || c == '_'))
                    return false;
            }
            return true;
        }
    }
}
