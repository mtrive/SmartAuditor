using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SmartAuditor.Editor.Core;
using SmartAuditor.Editor.Settings;
using SmartAuditor.Editor.Utils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace SmartAuditor.Editor.Modules
{
    enum ParseLogResult
    {
        Success,
        NoCompiledVariants,
        ReadError
    }

    class ShaderVariantData
    {
        public PassType PassType;
        public string PassName;
        public ShaderType ShaderType;
        public string[] Keywords;
        public string[] PlatformKeywords;
        public ShaderRequirements[] Requirements;
        public GraphicsTier GraphicsTier;
        public BuildTarget BuildTarget;
        public ShaderCompilerPlatform CompilerPlatform;
    }

    class ComputeShaderVariantData
    {
        public string KernelName;
        public string KernelThreadCount;
        public string[] Keywords;
        public string[] PlatformKeywords;
        public GraphicsTier GraphicsTier;
        public BuildTarget BuildTarget;
        public ShaderCompilerPlatform CompilerPlatform;
    }

    class CompiledVariantData
    {
        public string Pass;
        public string Stage;
        public string[] Keywords;
    }

    readonly struct ShaderVariantKey : IEquatable<ShaderVariantKey>
    {
        readonly PassType m_PassType;
        readonly string m_PassName;
        readonly ShaderType m_ShaderType;
        readonly string m_Keywords;
        readonly string m_PlatformKeywords;
        readonly string m_Requirements;
        readonly GraphicsTier m_GraphicsTier;
        readonly BuildTarget m_BuildTarget;
        readonly ShaderCompilerPlatform m_CompilerPlatform;

        public ShaderVariantKey(ShaderVariantData data)
        {
            m_PassType = data.PassType;
            m_PassName = data.PassName ?? string.Empty;
            m_ShaderType = data.ShaderType;
            m_Keywords = Canonicalize(data.Keywords);
            m_PlatformKeywords = Canonicalize(data.PlatformKeywords);
            m_Requirements = Canonicalize(data.Requirements?.Select(r => r.ToString()));
            m_GraphicsTier = data.GraphicsTier;
            m_BuildTarget = data.BuildTarget;
            m_CompilerPlatform = data.CompilerPlatform;
        }

        public bool Equals(ShaderVariantKey other)
        {
            return m_PassType == other.m_PassType &&
                m_PassName == other.m_PassName &&
                m_ShaderType == other.m_ShaderType &&
                m_Keywords == other.m_Keywords &&
                m_PlatformKeywords == other.m_PlatformKeywords &&
                m_Requirements == other.m_Requirements &&
                m_GraphicsTier == other.m_GraphicsTier &&
                m_BuildTarget == other.m_BuildTarget &&
                m_CompilerPlatform == other.m_CompilerPlatform;
        }

        public override bool Equals(object obj) => obj is ShaderVariantKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = (int)m_PassType;
                hash = (hash * 397) ^ m_PassName.GetHashCode();
                hash = (hash * 397) ^ (int)m_ShaderType;
                hash = (hash * 397) ^ m_Keywords.GetHashCode();
                hash = (hash * 397) ^ m_PlatformKeywords.GetHashCode();
                hash = (hash * 397) ^ m_Requirements.GetHashCode();
                hash = (hash * 397) ^ (int)m_GraphicsTier;
                hash = (hash * 397) ^ (int)m_BuildTarget;
                hash = (hash * 397) ^ (int)m_CompilerPlatform;
                return hash;
            }
        }

        static string Canonicalize(IEnumerable<string> values)
        {
            return values == null
                ? string.Empty
                : string.Join("\u001f", values.OrderBy(value => value, StringComparer.Ordinal));
        }
    }

    readonly struct ComputeShaderVariantKey : IEquatable<ComputeShaderVariantKey>
    {
        readonly string m_KernelName;
        readonly string m_KernelThreadCount;
        readonly string m_Keywords;
        readonly string m_PlatformKeywords;
        readonly GraphicsTier m_GraphicsTier;
        readonly BuildTarget m_BuildTarget;
        readonly ShaderCompilerPlatform m_CompilerPlatform;

        public ComputeShaderVariantKey(ComputeShaderVariantData data)
        {
            m_KernelName = data.KernelName ?? string.Empty;
            m_KernelThreadCount = data.KernelThreadCount ?? string.Empty;
            m_Keywords = Canonicalize(data.Keywords);
            m_PlatformKeywords = Canonicalize(data.PlatformKeywords);
            m_GraphicsTier = data.GraphicsTier;
            m_BuildTarget = data.BuildTarget;
            m_CompilerPlatform = data.CompilerPlatform;
        }

        public bool Equals(ComputeShaderVariantKey other)
        {
            return m_KernelName == other.m_KernelName &&
                m_KernelThreadCount == other.m_KernelThreadCount &&
                m_Keywords == other.m_Keywords &&
                m_PlatformKeywords == other.m_PlatformKeywords &&
                m_GraphicsTier == other.m_GraphicsTier &&
                m_BuildTarget == other.m_BuildTarget &&
                m_CompilerPlatform == other.m_CompilerPlatform;
        }

        public override bool Equals(object obj) => obj is ComputeShaderVariantKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = m_KernelName.GetHashCode();
                hash = (hash * 397) ^ m_KernelThreadCount.GetHashCode();
                hash = (hash * 397) ^ m_Keywords.GetHashCode();
                hash = (hash * 397) ^ m_PlatformKeywords.GetHashCode();
                hash = (hash * 397) ^ (int)m_GraphicsTier;
                hash = (hash * 397) ^ (int)m_BuildTarget;
                hash = (hash * 397) ^ (int)m_CompilerPlatform;
                return hash;
            }
        }

        static string Canonicalize(IEnumerable<string> values)
        {
            return values == null
                ? string.Empty
                : string.Join("\u001f", values.OrderBy(value => value, StringComparer.Ordinal));
        }
    }

    sealed class ShadersModule : AnalysisModule<ShaderAnalyzer>
        , IPreprocessBuildWithReport
        , IPreprocessShaders
        , IPreprocessComputeShaders
    {
        internal static readonly InsightSchema k_ShaderInsightSchema = new InsightSchema(
            new InsightColumn(ShaderColumns.Name, "Shader Name", PropertyFormat.Text),
            new InsightColumn(ShaderColumns.Size, "Size", PropertyFormat.DataSize, ColumnHints.Aggregatable | ColumnHints.TopNMetric, longName: "Size of the variants in the build"),
            new InsightColumn(ShaderColumns.VariantCount, "Num Variants", PropertyFormat.NumberAbbreviated, ColumnHints.Aggregatable, longName: "Number of potential shader variants for a single stage (e.g. fragment), per shader platform (e.g. GLES30)"),
            new InsightColumn(ShaderColumns.BuiltFragmentVariants, "Built Fragment Variants", PropertyFormat.Text, ColumnHints.Aggregatable, longName: "Total number of fragment shader variants in the selected target build"),
            new InsightColumn(ShaderColumns.PassCount, "Num Passes", PropertyFormat.Text, longName: "Number of Passes"),
            new InsightColumn(ShaderColumns.KeywordCount, "Num Keywords", PropertyFormat.Text, longName: "Number of Keywords"),
            new InsightColumn(ShaderColumns.PropertyCount, "Num Properties", PropertyFormat.Number, longName: "Number of Properties"),
            new InsightColumn(ShaderColumns.TexturePropertyCount, "Num Tex Properties", PropertyFormat.Number, longName: "Number of Texture Properties"),
            new InsightColumn(ShaderColumns.RenderQueue, "Render Queue", PropertyFormat.Number),
            new InsightColumn(ShaderColumns.Instancing, "Instancing", PropertyFormat.Boolean, ColumnHints.Categorical, longName: "GPU Instancing Support"),
            new InsightColumn(ShaderColumns.SrpBatcher, "SRP Batcher", PropertyFormat.Boolean, ColumnHints.Categorical, longName: "SRP Batcher Compatible"),
            new InsightColumn(ShaderColumns.AlwaysIncluded, "Always Included", PropertyFormat.Boolean, ColumnHints.Categorical, longName: "Always Included in Build"));

        internal static readonly InsightSchema k_MaterialInsightSchema = new InsightSchema(
            new InsightColumn(MaterialColumns.Name, "Material Name", PropertyFormat.Text),
            new InsightColumn(MaterialColumns.Shader, "Shader", PropertyFormat.Text, ColumnHints.Categorical, isDefaultGroup: true),
            new InsightColumn(MaterialColumns.InstancingEnabled, "Instancing", PropertyFormat.Boolean, ColumnHints.Categorical));

        internal static readonly InsightSchema k_ShaderVariantInsightSchema = new InsightSchema(
            new InsightColumn(ShaderVariantColumns.ShaderName, "Shader Name", PropertyFormat.Text, ColumnHints.Categorical, isDefaultGroup: true),
            new InsightColumn(ShaderVariantColumns.Compiled, "Compiled", PropertyFormat.Boolean, longName: "Compiled at runtime by the player"),
            new InsightColumn(ShaderVariantColumns.Platform, "Graphics API", PropertyFormat.Text, ColumnHints.Categorical),
            new InsightColumn(ShaderVariantColumns.Tier, "Tier", PropertyFormat.Text, ColumnHints.Categorical),
            new InsightColumn(ShaderVariantColumns.Stage, "Stage", PropertyFormat.Text, ColumnHints.Categorical),
            new InsightColumn(ShaderVariantColumns.PassType, "Pass Type", PropertyFormat.Text, ColumnHints.Categorical),
            new InsightColumn(ShaderVariantColumns.PassName, "Pass Name", PropertyFormat.Text),
            new InsightColumn(ShaderVariantColumns.KeywordCount, "Num Keywords", PropertyFormat.Number, ColumnHints.Aggregatable, longName: "Number of Keywords"),
            new InsightColumn(ShaderVariantColumns.Keywords, "Keywords", PropertyFormat.Text, maxAutoWidth: 500),
            new InsightColumn(ShaderVariantColumns.PlatformKeywords, "Platform Keywords", PropertyFormat.Text, maxAutoWidth: 500),
            new InsightColumn(ShaderVariantColumns.Requirements, "Requirements", PropertyFormat.Text));

        internal static readonly InsightSchema k_ComputeShaderVariantInsightSchema = new InsightSchema(
            new InsightColumn(ComputeShaderVariantColumns.ShaderName, "Shader Name", PropertyFormat.Text, ColumnHints.Categorical, isDefaultGroup: true),
            new InsightColumn(ComputeShaderVariantColumns.Platform, "Graphics API", PropertyFormat.Text, ColumnHints.Categorical),
            new InsightColumn(ComputeShaderVariantColumns.Tier, "Tier", PropertyFormat.Text, ColumnHints.Categorical),
            new InsightColumn(ComputeShaderVariantColumns.Kernel, "Kernel", PropertyFormat.Text),
            new InsightColumn(ComputeShaderVariantColumns.KernelThreadCount, "Kernel Thread Count", PropertyFormat.Text),
            new InsightColumn(ComputeShaderVariantColumns.Keywords, "Keywords", PropertyFormat.Text, maxAutoWidth: 500),
            new InsightColumn(ComputeShaderVariantColumns.PlatformKeywords, "Platform Keywords", PropertyFormat.Text, maxAutoWidth: 500));

        internal static readonly InsightSchema k_ShaderCompilerMessageSchema = new InsightSchema(
            new InsightColumn(ShaderCompilerMessageColumns.ShaderName, "Shader Name", PropertyFormat.Text, isDefaultGroup: true),
            new InsightColumn(ShaderCompilerMessageColumns.Platform, "Platform", PropertyFormat.Text));

        // k_NoPassNames and k_NoKeywords must be consistent with values assigned in SubProgram::Compile()
        internal static readonly string[] k_NoPassNames = new[] { "unnamed", "<unnamed>"}; // 2019.x uses: <unnamed>, whilst 2020.x uses unnamed
        internal static readonly Dictionary<string, string> k_StageNameMap = new Dictionary<string, string>()
        {
            { "all", "vertex" },       // GLES* / OpenGLCore
            { "pixel", "fragment" }    // Metal
        };
        internal const string k_NoKeywords = "<no keywords>";
        internal const string k_UnnamedPassPrefix = "Pass ";
        internal const string k_NoRuntimeData = "This feature requires runtime data.";
        internal const string k_NotAvailable = "This feature requires a build.";
        internal const string k_Unknown = "Unknown";
        internal const string k_ComputeShaderMayHaveBadVariants = "Compute shader may have bad (but unused) variants preventing this from being evaluated.";

        static Dictionary<Shader, List<ShaderVariantData>> s_ShaderVariantData =
            new Dictionary<Shader, List<ShaderVariantData>>();
        static Dictionary<ComputeShader, List<ComputeShaderVariantData>> s_ComputeShaderVariantData =
            new Dictionary<ComputeShader, List<ComputeShaderVariantData>>();
        static Dictionary<Shader, HashSet<ShaderVariantKey>> s_ShaderVariantKeys =
            new Dictionary<Shader, HashSet<ShaderVariantKey>>();
        static Dictionary<ComputeShader, HashSet<ComputeShaderVariantKey>> s_ComputeShaderVariantKeys =
            new Dictionary<ComputeShader, HashSet<ComputeShaderVariantKey>>();

        internal sealed class BufferedFindingSink : IFindingSink
        {
            readonly List<Action<IFindingSink>> m_PendingReports = new List<Action<IFindingSink>>();

            public void ReportItems(IEnumerable<ReportItem> items)
            {
                var snapshot = items.ToArray();
                m_PendingReports.Add(sink => sink.ReportItems(snapshot));
            }

            public void ReportInsightTable(InsightTable table)
            {
                m_PendingReports.Add(sink => sink.ReportInsightTable(table));
            }

            public void ReportMessage(Message message)
            {
                m_PendingReports.Add(sink => sink.ReportMessage(message));
            }

            public void CommitTo(IFindingSink sink)
            {
                foreach (var report in m_PendingReports)
                    report(sink);
                m_PendingReports.Clear();
            }

            public void Discard()
            {
                m_PendingReports.Clear();
            }
        }

        static readonly IReadOnlyDictionary<AnalysisCategory, InsightSchema> s_CategorySchemas =
            new Dictionary<AnalysisCategory, InsightSchema>
            {
                { AnalysisCategory.Shader, k_ShaderInsightSchema },
                { AnalysisCategory.Material, k_MaterialInsightSchema },
                { AnalysisCategory.ShaderVariant, k_ShaderVariantInsightSchema },
                { AnalysisCategory.ComputeShaderVariant, k_ComputeShaderVariantInsightSchema },
                { AnalysisCategory.ShaderCompilerMessage, k_ShaderCompilerMessageSchema },
            };

        public override IReadOnlyDictionary<AnalysisCategory, InsightSchema> CategorySchemas => s_CategorySchemas;

        public override string Name => "Shaders";

        public override IReadOnlyCollection<AnalysisCategory> OwnedCategories => new[]
        {
            AnalysisCategory.Shader,
            AnalysisCategory.Material,
            AnalysisCategory.ShaderVariant,
            AnalysisCategory.ComputeShaderVariant,
            AnalysisCategory.ShaderCompilerMessage,
        };

        public override IReadOnlyCollection<AnalysisCategory> AdditionalEmittedCategories =>
            new[] { AnalysisCategory.AssetIssue };

        public override Task<AnalysisResult> AnalyzeAsync(AnalysisSession session, CancellationToken cancellationToken = default)
        {
            var options = session.Options;
            var progress = session.Progress;
            var bufferedSink = new BufferedFindingSink();

            using var context = new AnalysisContext(options, bufferedSink);

            try
            {
                var shaderPathMap = CollectShaders(context, cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                {
                    context.Discard();
                    bufferedSink.Discard();
                    return Task.FromResult(AnalysisResult.Cancelled);
                }

                ProcessShaders(context, shaderPathMap, cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                {
                    context.Discard();
                    bufferedSink.Discard();
                    return Task.FromResult(AnalysisResult.Cancelled);
                }

                ProcessComputeShaders(context, cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                {
                    context.Discard();
                    bufferedSink.Discard();
                    return Task.FromResult(AnalysisResult.Cancelled);
                }

                ProcessMaterials(context, cancellationToken);
                if (cancellationToken.IsCancellationRequested)
                {
                    context.Discard();
                    bufferedSink.Discard();
                    return Task.FromResult(AnalysisResult.Cancelled);
                }

                var analyzers = GetCompatibleAnalyzers(options);
                foreach (var analyzer in analyzers)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        context.Discard();
                        bufferedSink.Discard();
                        return Task.FromResult(AnalysisResult.Cancelled);
                    }

                    analyzer.Finalize(context, progress);
                }

                context.Flush();
                bufferedSink.CommitTo(session);
                return Task.FromResult(AnalysisResult.Success);
            }
            finally
            {
                ClearBuildData();
            }
        }

        Dictionary<Shader, string> CollectShaders(AnalysisContext context, CancellationToken cancellationToken)
        {
            var shaderPathMap = new Dictionary<Shader, string>();
            var assetPaths = AssetPathUtils.GetAssetPathsByFilter($"t:{nameof(Shader)}", context.Options);
            foreach (var assetPath in assetPaths)
            {
                if (cancellationToken.IsCancellationRequested)
                    return shaderPathMap;

                // vfx shaders are not currently supported
                if (Path.HasExtension(assetPath) && Path.GetExtension(assetPath).Equals(".vfx"))
                    continue;

                var shader = AssetDatabase.LoadMainAssetAtPath(assetPath) as Shader;
                if (shader == null)
                {
                    Debug.LogError(assetPath + " is not a Shader.");
                    continue;
                }

                shaderPathMap.Add(shader, assetPath);
            }

            var builtShaderPaths = GetBuiltShaderPaths();

            foreach (var builtShader in builtShaderPaths)
            {
                if (cancellationToken.IsCancellationRequested)
                    return shaderPathMap;

                if (!shaderPathMap.ContainsKey(builtShader.Key))
                {
                    shaderPathMap.Add(builtShader.Key, builtShader.Value);
                }
            }

            return shaderPathMap;
        }

        Dictionary<Material, string> CollectMaterials(AnalysisContext context)
        {
            var materialPathMap = new Dictionary<Material, string>();
            var assetPaths = AssetPathUtils.GetAssetPathsByFilter($"t:{nameof(Material)}", context.Options);
            foreach (var assetPath in assetPaths)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
                if (material == null)
                {
                    Debug.LogError(assetPath + " is not a Material.");
                    continue;
                }

                materialPathMap.Add(material, assetPath);
            }

            return materialPathMap;
        }

        static Dictionary<Shader, string> GetBuiltShaderPaths()
        {
            // note this will find hidden shaders too
            return s_ShaderVariantData.Select(variant => variant.Key)
                .Where(shader => shader != null) // skip shader if it's been removed since the last build
                .ToDictionary(s => s, AssetDatabase.GetAssetPath);
        }

        static HashSet<Shader> GetAlwaysIncludedShaders()
        {
            var alwaysIncludedShaders = new HashSet<Shader>();
            var graphicsSettings = Unsupported.GetSerializedAssetInterfaceSingleton("GraphicsSettings");
            var graphicsSettingsSerializedObject = new SerializedObject(graphicsSettings);
            var alwaysIncludedShadersSerializedProperty =
                graphicsSettingsSerializedObject.FindProperty("m_AlwaysIncludedShaders");

            for (var i = 0; i < alwaysIncludedShadersSerializedProperty.arraySize; i++)
            {
                var shader = (Shader)alwaysIncludedShadersSerializedProperty.GetArrayElementAtIndex(i)
                    .objectReferenceValue;

                // sanity check, maybe the shader was removed/deleted
                if (shader == null)
                    continue;

                if (!alwaysIncludedShaders.Contains(shader))
                {
                    alwaysIncludedShaders.Add(shader);
                }
            }

            return alwaysIncludedShaders;
        }

        void ProcessShaders(AnalysisContext context, Dictionary<Shader, string> shaderPathMap, CancellationToken cancellationToken)
        {
            var options = context.Options;
            var platform = options.Platform;
            var alwaysIncludedShaders = GetAlwaysIncludedShaders();
            var buildReportInfoAvailable = false;

            var packetAssetInfos = Array.Empty<PackedAssetInfo>();
            var buildReport = BuildReportModule.BuildReportProvider.GetBuildReport(platform);
            if (buildReport != null)
            {
                packetAssetInfos = buildReport.packedAssets.SelectMany(packedAsset => packedAsset.contents)
                    .Where(c => c.type == typeof(UnityEngine.Shader)).ToArray();
            }

            buildReportInfoAvailable = packetAssetInfos.Length > 0;

            var sortedShaders = shaderPathMap.Keys
                .ToList()
                .OrderBy(shader => shader.name);

            var analyzers = GetCompatibleAnalyzers(options);
            foreach (var shader in sortedShaders)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                var assetPath = shaderPathMap[shader];
                var assetSize = buildReportInfoAvailable ? k_Unknown : k_NotAvailable;

                if (!assetPath.Equals("Resources/unity_builtin_extra"))
                {
                    var builtAssets = packetAssetInfos.Where(p => p.sourceAssetPath.Equals(assetPath)).ToArray();
                    if (builtAssets.Length > 0)
                    {
                        assetSize = builtAssets[0].packedSize.ToString();
                    }
                    else if (!s_ShaderVariantData.ContainsKey(shader))
                    {
                        // if not processed, it was not built into either player data or AssetBundles.
                        assetSize = "0";
                    }
                }

                using var shaderAnalysisContext = new ShaderAnalysisContext(options, context.Sink)
                {
                    AssetPath = assetPath,
                    Shader = shader,
                    SourceCode = ReadShaderSource(assetPath)
                };

                ProcessShader(shaderAnalysisContext, assetSize, alwaysIncludedShaders.Contains(shader));
                ProcessVariants(shaderAnalysisContext, cancellationToken);
                shaderAnalysisContext.Flush();

                // skip diagnostics for internal Hidden shaders
                if (shader.name.StartsWith("Hidden/"))
                    continue;

                foreach (var analyzer in analyzers)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    analyzer.AnalyzeShader(shaderAnalysisContext);
                }
                shaderAnalysisContext.Flush();
            }
        }

        void ProcessComputeShaders(AnalysisContext parentContext, CancellationToken cancellationToken)
        {
            using var context = new AnalysisContext(parentContext.Options, parentContext.Sink);

            var table = context.GetInsightTable(AnalysisCategory.ComputeShaderVariant, k_ComputeShaderVariantInsightSchema);

            foreach (var shaderCompilerData in s_ComputeShaderVariantData)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                var computeShaderName = shaderCompilerData.Key.name;
                foreach (var shaderVariantData in shaderCompilerData.Value)
                {
                    if (shaderVariantData.BuildTarget != BuildTarget.NoTarget && shaderVariantData.BuildTarget != parentContext.Options.Platform)
                        continue;

                    table.AddRow(string.Empty, new Dictionary<string, object>
                    {
                        [ComputeShaderVariantColumns.ShaderName] = computeShaderName,
                        [ComputeShaderVariantColumns.Platform] = shaderVariantData.CompilerPlatform.ToString(),
                        [ComputeShaderVariantColumns.Tier] = shaderVariantData.GraphicsTier.ToString(),
                        [ComputeShaderVariantColumns.Kernel] = shaderVariantData.KernelName,
                        [ComputeShaderVariantColumns.KernelThreadCount] = shaderVariantData.KernelThreadCount,
                        [ComputeShaderVariantColumns.Keywords] = CombineKeywords(shaderVariantData.Keywords),
                        [ComputeShaderVariantColumns.PlatformKeywords] = CombineKeywords(shaderVariantData.PlatformKeywords),
                    });
                }
            }
            context.Flush();
        }

        void ProcessMaterials(AnalysisContext context, CancellationToken cancellationToken)
        {
            var analyzers = GetCompatibleAnalyzers(context.Options);

            var materialPathMap = CollectMaterials(context);
            foreach (var entry in materialPathMap)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                var material = entry.Key;
                var materialTable = context.GetInsightTable(AnalysisCategory.Material, k_MaterialInsightSchema);
                materialTable.AddRow(entry.Value, new Dictionary<string, object>
                {
                    [MaterialColumns.Name] = material.name,
                    [MaterialColumns.Shader] = material.shader.name,
                    [MaterialColumns.InstancingEnabled] = material.enableInstancing,
                });

                using var shaderAnalysisContext = new MaterialAnalysisContext(context.Options, context.Sink)
                {
                    AssetPath = entry.Value,
                    Material = material,
                };

                foreach (var analyzer in analyzers)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    analyzer.AnalyzeMaterial(shaderAnalysisContext);
                }
                shaderAnalysisContext.Flush();

                // Analyze texture usage for each texture property assignment
                var texturePropertyNames = new List<string>();
                material.GetTexturePropertyNames(texturePropertyNames);
                foreach (var propName in texturePropertyNames)
                {
                    var texture = material.GetTexture(propName);
                    if (texture == null)
                        continue;

                    var texturePath = AssetDatabase.GetAssetPath(texture);
                    if (string.IsNullOrEmpty(texturePath) || !texturePath.StartsWith("Assets/"))
                        continue;

                    var textureImporter = AssetImporter.GetAtPath(texturePath) as TextureImporter;
                    if (textureImporter == null)
                        continue;

                    using var textureUsageContext = new TextureUsageAnalysisContext(context.Options, context.Sink)
                    {
                        Material = material,
                        MaterialPath = entry.Value,
                        ShaderPropertyName = propName,
                        Texture = texture,
                        TexturePath = texturePath,
                        TextureImporter = textureImporter,
                    };

                    foreach (var analyzer in analyzers)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return;

                        analyzer.AnalyzeTextureUsage(textureUsageContext);
                    }
                    textureUsageContext.Flush();
                }
            }
        }

        void ProcessShader(ShaderAnalysisContext context, string assetSize, bool isAlwaysIncluded)
        {
            // set initial state (-1: info not available)
            var hasBuildDataForTarget = s_ShaderVariantData.Values
                .SelectMany(variants => variants)
                .Any(variant => VariantMatchesBuildTarget(variant.BuildTarget, context.Options.Platform));
            var builtFragmentVariantCount = hasBuildDataForTarget ? 0 : -1;

            if (s_ShaderVariantData.ContainsKey(context.Shader))
            {
                builtFragmentVariantCount = GetBuiltFragmentVariantCount(
                    s_ShaderVariantData[context.Shader], context.Options.Platform, hasBuildDataForTarget);
            }

            var shaderName = context.Shader.name;
            var passCount = context.Shader.passCount;
            var globalKeywords = ShaderUtilProxy.GetShaderGlobalKeywords(context.Shader);
            var localKeywords = ShaderUtilProxy.GetShaderLocalKeywords(context.Shader);
            var hasInstancing = ShaderUtilProxy.HasInstancing(context.Shader);
            var isSrpBatcherCompatible = ShaderUtilProxy.IsSRPBatcherCompatible(context.Shader);
            var variantCount = ShaderUtilProxy.GetVariantCount(context.Shader);
            var propertyCount = ShaderUtilProxy.GetPropertyCount(context.Shader);
            var texturePropertyCount = ShaderUtilProxy.GetTexturePropertyCount(context.Shader);

            var table = context.GetInsightTable(AnalysisCategory.Shader, k_ShaderInsightSchema);
            table.AddRow(context.AssetPath, new Dictionary<string, object>
            {
                [ShaderColumns.Name] = shaderName,
                [ShaderColumns.Size] = assetSize,
                [ShaderColumns.VariantCount] = variantCount == null ? k_Unknown : variantCount.Value,
                [ShaderColumns.BuiltFragmentVariants] = builtFragmentVariantCount == -1
                    ? (object)k_NotAvailable
                    : builtFragmentVariantCount,
                [ShaderColumns.PassCount] = passCount == -1 ? k_NotAvailable : passCount.ToString(),
                [ShaderColumns.KeywordCount] = globalKeywords == null || localKeywords == null ? k_NotAvailable : (globalKeywords.Length + localKeywords.Length).ToString(),
                [ShaderColumns.PropertyCount] = propertyCount,
                [ShaderColumns.TexturePropertyCount] = texturePropertyCount,
                [ShaderColumns.RenderQueue] = context.Shader.renderQueue,
                [ShaderColumns.Instancing] = hasInstancing == null ? k_Unknown : hasInstancing.Value,
                [ShaderColumns.SrpBatcher] = isSrpBatcherCompatible == null ? k_Unknown : isSrpBatcherCompatible.Value,
                [ShaderColumns.AlwaysIncluded] = isAlwaysIncluded,
            });
        }

        void ProcessVariants(ShaderAnalysisContext context, CancellationToken cancellationToken)
        {
            if (s_ShaderVariantData.ContainsKey(context.Shader))
            {
                var shaderVariants = s_ShaderVariantData[context.Shader];

                foreach (var shaderVariantData in shaderVariants)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    if (shaderVariantData.BuildTarget != BuildTarget.NoTarget && shaderVariantData.BuildTarget != context.Options.Platform)
                        continue;

                    var variantTable = context.GetInsightTable(AnalysisCategory.ShaderVariant, k_ShaderVariantInsightSchema);
                    variantTable.AddRow(context.AssetPath, new Dictionary<string, object>
                    {
                        [ShaderVariantColumns.ShaderName] = context.Shader.name,
                        [ShaderVariantColumns.Compiled] = k_NoRuntimeData,
                        [ShaderVariantColumns.Platform] = shaderVariantData.CompilerPlatform.ToString(),
                        [ShaderVariantColumns.Tier] = shaderVariantData.GraphicsTier.ToString(),
                        [ShaderVariantColumns.Stage] = shaderVariantData.ShaderType.ToString(),
                        [ShaderVariantColumns.PassType] = shaderVariantData.PassType.ToString(),
                        [ShaderVariantColumns.PassName] = shaderVariantData.PassName,
                        [ShaderVariantColumns.KeywordCount] = shaderVariantData.Keywords.Length,
                        [ShaderVariantColumns.Keywords] = CombineKeywords(shaderVariantData.Keywords),
                        [ShaderVariantColumns.PlatformKeywords] = CombineKeywords(shaderVariantData.PlatformKeywords),
                        [ShaderVariantColumns.Requirements] = CombineKeywords(shaderVariantData.Requirements.Select(r => r.ToString()).ToArray()),
                    });
                }
            }
        }

        internal static void ClearBuildData()
        {
            ClearCapturedVariantData();

#if UNITY_2021_1_OR_NEWER
            TryDeleteDirectory(Path.Combine("Library", "PlayerDataCache"));
#endif
        }

        internal static void ClearCapturedVariantData()
        {
            s_ShaderVariantData.Clear();
            s_ComputeShaderVariantData.Clear();
            s_ShaderVariantKeys.Clear();
            s_ComputeShaderVariantKeys.Clear();
        }

        internal static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, recursive: true);
            }
            catch (IOException ex)
            {
                Debug.LogWarning($"[ShadersModule] Failed to delete '{path}': {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.LogWarning($"[ShadersModule] Access denied when deleting '{path}': {ex.Message}");
            }
        }

        internal static int NumBuiltVariants()
        {
            return s_ShaderVariantData.Count;
        }

        public int callbackOrder => Int32.MaxValue;

        static bool IsVariantCollectionEnabled => UserPreferences.CollectShaderVariantsOnBuild;

        public void OnPreprocessBuild(BuildReport report)
        {
            ClearCapturedVariantData();
        }

        public void OnProcessComputeShader(ComputeShader shader, string kernelName, IList<ShaderCompilerData> data)
        {
            if (!IsVariantCollectionEnabled)
                return;

            if (data.Count == 0)
                return; // no variants

            var buildTargetPropertyInfo = typeof(ShaderCompilerData).GetRuntimeProperty("buildTarget");
            foreach (var shaderCompilerData in data)
            {
                int kernelThreadCount = 0;
#if SMARTAUDITOR_CAN_USE_COMPUTESHADER_KEYWORDSPACE
                if (shader.HasKernel(kernelName))
                {
                    var kernelIndex = shader.FindKernel(kernelName);
                    // This is gross and it deserves some explaination.
                    // Unlike raster shaders, it is possible for this callback to give you a compute kernel that's invalid for the keyword state.
                    // This seems to only happen when you have a multi_compile without a leading _ default entry, but it's not guaranteed for that situation to cause a problem.
                    // We care because calling GetKernelThreadGroupSizes for a "bad" kernel puts spurious errors in the console and we don't want that.
                    // As it currently exists the check prevents all intended error scenarios but does also skip some perfectly valid kernels.
                    // For now it's an ok compromise but the goal is to get the false positives down to zero.
                    // In service of that, here's the current thinking behind the check.
                    // 1) A variant can't have problems if the base shader defines no keywords.
                    // 2) A variant can't have problems if it has every defined or enabled keyword of the base shader.
                    // 3) A variant can have problems if it has keywords but the base shader has enabled no keywords.
                    bool keywordSpaceValid =
                        (shaderCompilerData.shaderKeywordSet.GetShaderKeywords().Length == shader.shaderKeywords.Length) ||
                        (shaderCompilerData.shaderKeywordSet.GetShaderKeywords().Length == shader.keywordSpace.keywordCount) ||
                        !((shader.shaderKeywords.Length == 0 && shaderCompilerData.shaderKeywordSet.GetShaderKeywords().Length != 0) && shader.keywordSpace.keywordCount > 0);
                    if (keywordSpaceValid && shader.IsSupported(kernelIndex))
                    {
                        shader.GetKernelThreadGroupSizes(kernelIndex, out uint x, out uint y, out uint z);
                        kernelThreadCount = (int)(x * y * z);
                    }
                }
#endif

                TryAddComputeShaderVariant(shader, new ComputeShaderVariantData
                {
                    KernelName = kernelName,
                    KernelThreadCount = kernelThreadCount == 0 ? k_ComputeShaderMayHaveBadVariants : kernelThreadCount.ToString(),
                    Keywords = GetShaderKeywords(shader, shaderCompilerData.shaderKeywordSet.GetShaderKeywords()),
                    PlatformKeywords = PlatformKeywordSetToStrings(shaderCompilerData.platformKeywordSet),
                    GraphicsTier = shaderCompilerData.graphicsTier,
                    BuildTarget = (buildTargetPropertyInfo != null) ? (BuildTarget)buildTargetPropertyInfo.GetValue(shaderCompilerData) : BuildTarget.NoTarget,
                    CompilerPlatform = shaderCompilerData.shaderCompilerPlatform
                });
            }
        }

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (!IsVariantCollectionEnabled)
                return;

            if (data.Count == 0)
                return; // no variants

            // the buildTarget property is only available as of 2020_3_35 so we need to use reflection to get the value
            var buildTargetPropertyInfo = typeof(ShaderCompilerData).GetRuntimeProperty("buildTarget");
            foreach (var shaderCompilerData in data)
            {
                var shaderRequirements = shaderCompilerData.shaderRequirements;
                var shaderRequirementsList = new List<ShaderRequirements>();
                foreach (ShaderRequirements value in Enum.GetValues(shaderRequirements.GetType()))
                    if ((shaderRequirements & value) != 0)
                        shaderRequirementsList.Add(value);

                if (shaderRequirementsList.Count > 1)
                    shaderRequirementsList.Remove(ShaderRequirements.None);

                TryAddShaderVariant(shader, new ShaderVariantData
                {
                    PassType = snippet.passType,
                    PassName =  snippet.passName,
                    ShaderType = snippet.shaderType,
                    Keywords = GetShaderKeywords(shader, shaderCompilerData.shaderKeywordSet.GetShaderKeywords()),
                    PlatformKeywords = PlatformKeywordSetToStrings(shaderCompilerData.platformKeywordSet),
                    Requirements = shaderRequirementsList.ToArray(),
                    GraphicsTier = shaderCompilerData.graphicsTier,
                    BuildTarget = (buildTargetPropertyInfo != null) ? (BuildTarget)buildTargetPropertyInfo.GetValue(shaderCompilerData) : BuildTarget.NoTarget,
                    CompilerPlatform = shaderCompilerData.shaderCompilerPlatform
                });
            }
        }

        public static void ExportVariantsToSvc(string svcName, string path, ReportItem[] variants)
        {
            var svc = new ShaderVariantCollection();
            svc.name = svcName;

            foreach (var issue in variants)
            {
                var shader = Shader.Find(ReportItemExtensions.GetProperty(issue, PropertyType.Description));
                var passType = issue.GetProperty(k_ShaderVariantInsightSchema, ShaderVariantColumns.PassType);
                var keywords = SplitKeywords(issue.GetProperty(k_ShaderVariantInsightSchema, ShaderVariantColumns.Keywords));

                if (shader != null && !passType.Equals(string.Empty))
                {
                    var shaderVariant = new ShaderVariantCollection.ShaderVariant();
                    shaderVariant.shader = shader;
                    shaderVariant.passType = (UnityEngine.Rendering.PassType)Enum.Parse(typeof(UnityEngine.Rendering.PassType), passType);
                    shaderVariant.keywords = keywords;
                    svc.Add(shaderVariant);
                }
            }
            AssetDatabase.CreateAsset(svc, path);
        }

        public static ParseLogResult ParsePlayerLog(string logFile, ReportItem[] builtVariants, IProgress progress = null)
        {
            var compiledVariants = new Dictionary<string, List<CompiledVariantData>>();
            var lines = GetCompiledShaderLines(logFile);
            if (lines == null)
                return ParseLogResult.ReadError;

            foreach (var line in lines)
            {
                var parts = line.Split(new[] {", pass: ", ", stage: ", ", keywords "}, StringSplitOptions.None);
                if (parts.Length != 4)
                {
                    Debug.LogError("Malformed shader compilation log info: " + line);
                    continue;
                }

                var shaderName = parts[0];
                var pass = parts[1];
                var stage = parts[2];
                var keywordsString = parts[3];
                var keywords = SplitKeywords(keywordsString, " ");

                // fix-up stage to be consistent with built variants stage
                if (k_StageNameMap.ContainsKey(stage))
                    stage = k_StageNameMap[stage];

                if (!compiledVariants.ContainsKey(shaderName))
                {
                    compiledVariants.Add(shaderName, new List<CompiledVariantData>());
                }
                compiledVariants[shaderName].Add(new CompiledVariantData
                {
                    Pass = pass,
                    Stage = stage,
                    Keywords = keywords
                });
            }

            if (!compiledVariants.Any())
                return ParseLogResult.NoCompiledVariants;

            builtVariants = builtVariants.OrderBy(v => v.Description).ToArray();
            var shader = (Shader)null;
            foreach (var builtVariant in builtVariants)
            {
                if (shader == null || !shader.name.Equals(builtVariant.Description))
                {
                    shader = Shader.Find(builtVariant.Description);
                }

                if (shader == null)
                {
                    builtVariant.SetProperty(k_ShaderVariantInsightSchema, ShaderVariantColumns.Compiled, "?");
                    continue;
                }

                var shaderName = shader.name;
                var stage = builtVariant.GetProperty(k_ShaderVariantInsightSchema, ShaderVariantColumns.Stage);
                var passName = builtVariant.GetProperty(k_ShaderVariantInsightSchema, ShaderVariantColumns.PassName);
                var keywordsString = builtVariant.GetProperty(k_ShaderVariantInsightSchema, ShaderVariantColumns.Keywords);
                var keywords = SplitKeywords(keywordsString);
                var isVariantCompiled = false;

                if (compiledVariants.ContainsKey(shaderName))
                {
                    // note that we are not checking pass name since there is an inconsistency regarding "unnamed" passes between build vs compiled
                    var matchingVariants = compiledVariants[shaderName].Where(cv => ShaderVariantsMatch(cv, stage, passName, keywords)).ToArray();
                    isVariantCompiled = matchingVariants.Length > 0;
                }

                builtVariant.SetProperty(k_ShaderVariantInsightSchema, ShaderVariantColumns.Compiled, isVariantCompiled);
            }

            return ParseLogResult.Success;
        }

        // Older Unity versions use the first string in their log, new versions use the second.
        // Rather than trying to identify the specific version when the change occurred, we'll just check both.
        static readonly string[] k_CompiledShaderPrefixes = { "Compiled shader: ", "Uploaded shader variant to the GPU driver: " };

        static string[] GetCompiledShaderLines(string logFile)
        {
            var compilationLines = new List<string>();
            try
            {
                using (var file = new StreamReader(logFile))
                {
                    string line;
                    while ((line = file.ReadLine()) != null)
                    {
                        for (int i = 0; i < k_CompiledShaderPrefixes.Length; ++i)
                        {
                            var compilationLogIndex = line.IndexOf(k_CompiledShaderPrefixes[i], StringComparison.Ordinal);
                            if (compilationLogIndex >= 0)
                            {
                                compilationLines.Add(
                                    line.Substring(compilationLogIndex + k_CompiledShaderPrefixes[i].Length));
                                break;
                            }
                        }
                    }
                }
                return compilationLines.ToArray();
            }
            catch (Exception e)
            {
                Debug.Log(e);
                return null;
            }
        }

        static bool ShaderVariantsMatch(CompiledVariantData cv, string stage, string passName, string[] secondSet)
        {
            if (!cv.Stage.Equals(stage, StringComparison.InvariantCultureIgnoreCase))
                return false;

            var passMatch = cv.Pass.Equals(passName);
            if (!passMatch)
            {
                var isUnnamed = k_NoPassNames.Contains(cv.Pass) || cv.Pass.StartsWith("<Unnamed Pass ");
#if UNITY_2021_3_OR_NEWER || UNITY_2021_2_14 || UNITY_2021_2_15 || UNITY_2021_2_16 || UNITY_2021_2_17 || UNITY_2021_2_18 || UNITY_2021_2_19
                passMatch = isUnnamed && string.IsNullOrEmpty(passName);
#else
                var pass = 0;
                passMatch = isUnnamed && passName.StartsWith(k_UnnamedPassPrefix) && int.TryParse(passName.Substring(k_UnnamedPassPrefix.Length), out pass);
#endif
            }

            if (!passMatch)
                return false;
            return cv.Keywords.OrderBy(e => e).SequenceEqual(secondSet.OrderBy(e => e));
        }

        static string[] GetShaderKeywords(Shader shader, ShaderKeyword[] shaderKeywords)
        {
#if UNITY_2021_2_OR_NEWER
            var keywords = shaderKeywords.Select(keyword => keyword.name);
#else
            var keywords = shaderKeywords.Select(keyword => ShaderKeyword.IsKeywordLocal(keyword) ? ShaderKeyword.GetKeywordName(shader, keyword) : ShaderKeyword.GetGlobalKeywordName(keyword));
#endif
            return keywords.ToArray();
        }

        static string[] GetShaderKeywords(ComputeShader shader, ShaderKeyword[] shaderKeywords)
        {
#if UNITY_2021_2_OR_NEWER
            var keywords = shaderKeywords.Select(keyword => keyword.name);
#else
            var keywords = shaderKeywords.Select(keyword => ShaderKeyword.IsKeywordLocal(keyword) ? ShaderKeyword.GetKeywordName(shader, keyword) : ShaderKeyword.GetGlobalKeywordName(keyword));
#endif
            return keywords.ToArray();
        }

        static string[] SplitKeywords(string keywordsString, string separator = null)
        {
            if (keywordsString.Equals(k_NoKeywords))
                return new string[] {};
            return Formatting.SplitStrings(keywordsString, separator);
        }

        static string CombineKeywords(string[] strings, string separator = null)
        {
            if (strings.Length > 0)
                return Formatting.CombineStrings(strings, separator);
            return k_NoKeywords;
        }

        static string[] PlatformKeywordSetToStrings(PlatformKeywordSet platformKeywordSet)
        {
            var builtinShaderDefines = new List<BuiltinShaderDefine>();

            foreach (BuiltinShaderDefine value in Enum.GetValues(typeof(BuiltinShaderDefine)))
                if (platformKeywordSet.IsEnabled(value))
                    builtinShaderDefines.Add(value);

            return builtinShaderDefines.Select(d => d.ToString()).ToArray();
        }

        static string ReadShaderSource(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return null;

            if (!assetPath.EndsWith(".shader", StringComparison.OrdinalIgnoreCase))
                return null;

            if (!File.Exists(assetPath))
                return null;

            try
            {
                return File.ReadAllText(assetPath);
            }
            catch (IOException ex)
            {
                Debug.LogWarning($"[ShadersModule] Failed to read shader source at '{assetPath}': {ex.Message}");
                return null;
            }
            catch (UnauthorizedAccessException ex)
            {
                Debug.LogWarning($"[ShadersModule] Access denied when reading shader at '{assetPath}': {ex.Message}");
                return null;
            }
        }

        static bool ShaderTypeIsFragment(ShaderType shaderType, ShaderCompilerPlatform shaderCompilerPlatform)
        {
            switch (shaderCompilerPlatform)
            {
                // On OpenGL and Vulkan, all stages supported by the shader are combined into a single ShaderType (Vertex).
#if !UNITY_2023_1_OR_NEWER
                case ShaderCompilerPlatform.GLES20:
#endif
                case ShaderCompilerPlatform.GLES3x:
                case ShaderCompilerPlatform.OpenGLCore:
                case ShaderCompilerPlatform.Vulkan:
                    return true;
                default:
                    return shaderType == ShaderType.Fragment;
            }
        }

        internal static int GetBuiltFragmentVariantCount(
            IEnumerable<ShaderVariantData> variants,
            BuildTarget buildTarget,
            bool hasBuildDataForTarget = true)
        {
            if (!hasBuildDataForTarget)
                return -1;

            return variants.Count(variant =>
                VariantMatchesBuildTarget(variant.BuildTarget, buildTarget) &&
                ShaderTypeIsFragment(variant.ShaderType, variant.CompilerPlatform));
        }

        static bool VariantMatchesBuildTarget(BuildTarget variantBuildTarget, BuildTarget analysisBuildTarget)
        {
            return variantBuildTarget == BuildTarget.NoTarget || variantBuildTarget == analysisBuildTarget;
        }

        internal static bool TryAddShaderVariant(Shader shader, ShaderVariantData data)
        {
            if (!s_ShaderVariantData.TryGetValue(shader, out var variants))
            {
                variants = new List<ShaderVariantData>();
                s_ShaderVariantData.Add(shader, variants);
            }

            if (!s_ShaderVariantKeys.TryGetValue(shader, out var keys))
            {
                keys = new HashSet<ShaderVariantKey>();
                s_ShaderVariantKeys.Add(shader, keys);
            }

            if (!keys.Add(new ShaderVariantKey(data)))
                return false;

            variants.Add(data);
            return true;
        }

        internal static bool TryAddComputeShaderVariant(ComputeShader shader, ComputeShaderVariantData data)
        {
            if (!s_ComputeShaderVariantData.TryGetValue(shader, out var variants))
            {
                variants = new List<ComputeShaderVariantData>();
                s_ComputeShaderVariantData.Add(shader, variants);
            }

            if (!s_ComputeShaderVariantKeys.TryGetValue(shader, out var keys))
            {
                keys = new HashSet<ComputeShaderVariantKey>();
                s_ComputeShaderVariantKeys.Add(shader, keys);
            }

            if (!keys.Add(new ComputeShaderVariantKey(data)))
                return false;

            variants.Add(data);
            return true;
        }
    }
}
