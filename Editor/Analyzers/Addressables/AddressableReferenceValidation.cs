// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

#if PACKAGE_ADDRESSABLES

using UnityEditor.AddressableAssets.Settings;
using UnityEngine.AddressableAssets;

namespace SmartAuditor.Editor.Analyzers
{
    /// <summary>
    /// Shared predicate for the analyzers that flag an <see cref="AssetReference"/> pointing at an asset
    /// that belongs to no Addressable group. Kept settings-parameterised so the membership rule can be
    /// tested against a throwaway <see cref="AddressableAssetSettings"/> without touching the project's.
    /// </summary>
    internal static class AddressableReferenceValidation
    {
        /// <summary>
        /// True when the reference has a non-empty <b>AssetGUID</b> but no Addressable entry resolves it.
        /// This is the case <see cref="AssetReference.RuntimeKeyIsValid"/> misses — it only parses the GUID,
        /// so the reference looks valid in the editor yet its runtime Addressables load fails.
        /// </summary>
        public static bool IsAssignedButUnregistered(AssetReference assetReference, AddressableAssetSettings settings)
        {
            if (assetReference == null)
                return false;

            var guid = assetReference.AssetGUID;
            if (string.IsNullOrEmpty(guid))
                return false;

            // With Addressables unconfigured every reference would look unregistered; stay silent rather
            // than flood the report, matching how AddressablesModule bails on a null settings.
            if (settings == null)
                return false;

            // includeImplicit honours assets made addressable through a parent-folder entry, which load
            // at runtime just like explicit entries do.
            return settings.FindAssetEntry(guid, includeImplicit: true) == null;
        }
    }
}

#endif
