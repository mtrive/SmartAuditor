// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

#if PACKAGE_ADDRESSABLES

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace SmartAuditor.Editor.Utils
{
    /// <summary>
    /// Caches the set of public-or-[SerializeField] instance fields on a Component type whose
    /// type is <see cref="AssetReference"/> or a subclass. Analyzers that scan AssetReference
    /// fields share this cache because reflection over the same MonoBehaviour types is invoked
    /// once per component visited, which adds up across thousands of scene components.
    /// </summary>
    internal static class AssetReferenceFieldCache
    {
        static readonly Dictionary<Type, FieldInfo[]> s_Cache = new Dictionary<Type, FieldInfo[]>();

        public static FieldInfo[] GetFields(Type componentType)
        {
            if (s_Cache.TryGetValue(componentType, out var cached))
                return cached;

            var fields = componentType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            List<FieldInfo> matches = null;
            foreach (var field in fields)
            {
                var isPublic = field.IsPublic;
                var isSerialized = field.GetCustomAttribute(typeof(SerializeField)) != null;
                if (!isPublic && !isSerialized)
                    continue;
                if (!typeof(AssetReference).IsAssignableFrom(field.FieldType))
                    continue;

                matches ??= new List<FieldInfo>(2);
                matches.Add(field);
            }

            var result = matches?.ToArray() ?? Array.Empty<FieldInfo>();
            s_Cache[componentType] = result;
            return result;
        }
    }
}

#endif
