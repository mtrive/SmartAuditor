// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class AssetBundleAllocatingAnalyzer : CodeAnalyzer
    {
        internal const string CDE0117 = nameof(CDE0117);
        internal const string CDE0118 = nameof(CDE0118);
        internal const string CDE0119 = nameof(CDE0119);
        internal const string CDE0144 = nameof(CDE0144);

        static readonly Descriptor LoadAssetWithSubAssetsDescriptor = new Descriptor(
            CDE0117,
            "AssetBundle: LoadAssetWithSubAssets Allocates",
            Impact.Memory,
            "<b>AssetBundle.LoadAssetWithSubAssets()</b> loads every sub-asset and returns them in a freshly allocated managed array. The load itself is also CPU-heavy.",
            "Call <b>AssetBundle.LoadAssetWithSubAssets()</b> once during initialization and cache the returned array."
        )
        {
            MessageFormat = "AssetBundle.LoadAssetWithSubAssets allocates a managed array of assets"
        };

        static readonly Descriptor LoadAllAssetsDescriptor = new Descriptor(
            CDE0118,
            "AssetBundle: LoadAllAssets Allocates",
            Impact.Memory,
            "<b>AssetBundle.LoadAllAssets()</b> loads every asset in the bundle and returns them in a freshly allocated managed array. The load itself is also CPU-heavy.",
            "Call <b>AssetBundle.LoadAllAssets()</b> once during initialization and cache the returned array."
        )
        {
            MessageFormat = "AssetBundle.LoadAllAssets allocates a managed array of assets"
        };

        static readonly Descriptor GetAllAssetBundlesDescriptor = new Descriptor(
            CDE0119,
            "AssetBundleManifest: GetAllAssetBundles Allocates",
            Impact.Memory,
            "<b>AssetBundleManifest.GetAllAssetBundles()</b> allocates a fresh managed <b>string[]</b> on every call.",
            "Cache the result of <b>AssetBundleManifest.GetAllAssetBundles()</b> during initialization and reuse the array."
        )
        {
            MessageFormat = "AssetBundleManifest.GetAllAssetBundles allocates a managed string array"
        };

        static readonly Descriptor AllAssetsDescriptor = new Descriptor(
            CDE0144,
            "AssetBundleRequest.allAssets Allocates",
            Impact.Memory,
            "<b>AssetBundleRequest.allAssets</b> allocates a fresh managed array on every access.",
            "Read <b>AssetBundleRequest.allAssets</b> once when the request completes and cache the returned array."
        )
        {
            MessageFormat = "AssetBundleRequest.allAssets allocates a managed array on each access"
        };

        static readonly Dictionary<string, (string typeName, Descriptor descriptor)> s_MethodToDescriptor = new Dictionary<string, (string, Descriptor)>
        {
            { "LoadAssetWithSubAssets", ("UnityEngine.AssetBundle", LoadAssetWithSubAssetsDescriptor) },
            { "LoadAllAssets", ("UnityEngine.AssetBundle", LoadAllAssetsDescriptor) },
            { "GetAllAssetBundles", ("UnityEngine.AssetBundleManifest", GetAllAssetBundlesDescriptor) },
            { "get_allAssets", ("UnityEngine.AssetBundleRequest", AllAssetsDescriptor) }
        };

        readonly OpCode[] m_OpCodes = { Mono.Cecil.Cil.OpCodes.Call, Mono.Cecil.Cil.OpCodes.Callvirt };

        public override IReadOnlyCollection<OpCode> OpCodes => m_OpCodes;

        public override void Analyze(InstructionAnalysisContext context)
        {
            var methodReference = (MethodReference)context.Instruction.Operand;

            // Check if it's one of our target methods
            if (!s_MethodToDescriptor.TryGetValue(methodReference.Name, out var tuple))
                return;

            // Check if it's the correct type
            if (methodReference.DeclaringType.FullName != tuple.typeName)
                return;

            if (!context.IsDescriptorEnabled(tuple.descriptor))
                return;

            var diagnostic = Diagnostic.Create(AnalysisCategory.Code, tuple.descriptor.Id);
            context.ReportIssue(diagnostic);
        }
    }
}
