# Project Instructions

- Smart Auditor is a static analysis tool for Unity projects. It analyzes code, assets, and project settings. The diagnostics framework is inspired by Microsoft Roslyn Code Analysis.
- This package has its own git history at `github.com/mtrive/SmartAuditor`. When working from an embedded copy inside another repository, changes flow through git → PR, not through the host repository's VCS.

## General

- Strive to find and implement solutions that are straightforward, elegant, and efficient.
- When adding new features, consider whether test coverage makes sense.
- When refactoring, ensure tests are in place first; add them if they are missing.

## Architecture

- When adding or changing code, follow established repository patterns.
- The module base class is `AnalysisModule` (not `Module`). Concrete modules inherit from `AnalysisModule<T>` where `T : DiagnosticAnalyzer`.
- The main entry point is `SmartAuditor.Analyze(options)` (sync) or `SmartAuditor.AnalyzeAsync(options, progress, ct)` (async). The old `Audit`/`AuditAsync` names no longer exist.
- `AnalysisSession` is the runtime boundary object passed from the orchestrator to each module. It carries `Options`, `Progress`, and a `ReportItems(items)` method for reporting findings.
- `CancellationToken` is passed as an explicit parameter to `AnalyzeAsync` and propagated down. It is NOT part of `IProgress`.
- `ProgressBar` exposes its own `CancellationToken` property; cast to `ProgressBar` to retrieve it when calling `AnalyzeAsync`.
- Asset path helpers live in `AssetPathUtils` (not on the module base class).

## Layout

### API Surfaces

Two `API/` directories serve different audiences:

- `Editor/API/` — *consumer surface*. Stable types the host project reads or passes through: `Report`, `Rule`, `Descriptor`, `Severity`, `AnalysisOptions`, `AnalysisResult`, `Location`, `HealthScore`, `Impact`, `LogLevel`, `CodeContext`, etc.
- `Editor/Core/API/` — *analyzer-author surface*. Types produced when writing a new analyzer: `Diagnostic`, `Message`, `MessageBuilder`, `ReportItemBuilder`, `InsightSchema`, `InsightColumn`, `InsightRow`, `InsightTable`, `InsightTableBuilder`, `InsightSummary`, `IReportItemFilter`, `PropertyFormat`, `DiagnosticParameterAttribute`.

If you're consuming the report, work against `Editor/API/`. If you're emitting findings, work against `Editor/Core/API/`.

### Modules and Analyzers

`Editor/Modules/` holds one `AnalysisModule` per subsystem. `Editor/Analyzers/<Subsystem>/` holds the `DiagnosticAnalyzer`s the module dispatches over. Subsystems pick one of three shapes:

- **Pure dispatcher** — `AssetsModule`, `CodeModule`, `ShadersModule`, `SettingsModule`, `PackagesModule`, `TexturesModule`, `MeshesModule`, `AddressablesModule`, `LightingModule`. Iterate items and dispatch many small analyzers in `Editor/Analyzers/<Subsystem>/`.
- **Pure self-contained** — `AnimationsModule`, `BuildReportModule`. Own a fixed `InsightSchema` and do the analysis inline. No matching `Analyzers/<Subsystem>/` folder.
- **Hybrid** — `ScenesModule`, `PrefabsModule`, `AudioClipsModule`, `SpriteAtlasesModule`. Dispatch analyzers from `Editor/Analyzers/<Subsystem>/` *and* produce their own insight table.

When adding a new subsystem, pick the shape that fits: many rules and third-party extension expected → dispatcher; one fixed table → self-contained; both → hybrid.

**Category schemas** — column metadata for diagnostics, messages, and insight tables lives on `AnalysisModule.CategorySchemas` (`InsightSchema` per `AnalysisCategory`). Register every owned category; empty schemas (`new InsightSchema()`) are valid when a category has no custom `ReportItem.Properties` columns. Shared categories whose schema is not owned by one module live in `Editor/Core/SharedSchemas.cs` (`AssetIssue`, `AssetImportMessage`, `ProjectSetting`, `GameObjectIssue`). UI and export code resolve schemas via `CategorySchemaRegistry.GetSchema(category)`. Custom property dict keys are defined as `*Columns` string constants in `Editor/Modules/` (e.g. `CodeColumns.Assembly`); use the same constants in `WithProperty`, `GetProperty`, and `InsightColumn` definitions.

### UI and UI/Framework

The UI tree splits across two asmdefs by reusability:

- `Editor/UI/Framework/` — the **reusable view kit** (`SmartAuditor.Editor.UI.Framework.asmdef`). Tables, drawers, layout primitives, selection plumbing, code-snippet cache, severity/log-level filters. Nothing in here knows about a specific Smart Auditor view; it's the toolbox the views are built from.
- `Editor/UI/` — **Smart Auditor's windows and views** (`SmartAuditor.Editor.UI.asmdef`, depends on Framework). `SmartAuditorWindow`, `SmartConsoleWindow`, the per-category views under `Views/`, settings providers, the view-descriptor registry.

When adding a new generic widget (chart, filter toolbar, table primitive), it belongs in Framework. When adding a Smart Auditor-specific window or view composition, it belongs in `UI/`.

### Core/Services

`Editor/Core/Services/` contains process-wide services used by the orchestrator:

- `IAnalysisModuleRegistry` / `AnalysisModuleRegistry` — discovers `AnalysisModule` instances via Unity's `TypeCache`, owns the `DescriptorLibrary` lifecycle, and answers queries by category or build target. Operations are thread-safe.
- `LiveConsoleLogService` / `LiveConsoleMessage` — `[InitializeOnLoad]` singleton that captures Unity Editor log callbacks into a bounded in-memory buffer for `SmartConsoleWindow`.

Both classes are `internal sealed`. New services in this folder should follow the same shape: interface + sealed implementation, scoped to a single concern, no static state outside the singleton entry point.

## Terminology

- Use "hot path" as the noun phrase, "hot-path" only as a compound modifier before another noun, and `HotPath` only where required by code identifier casing.
- Use "mipmap" / "mipmaps" as the general prose term, with "mip" only for established graphics shorthand such as "mip level", "mip chain", and "highest mip". Preserve exact Unity UI labels and API names such as `mipmapEnabled`, `mipmapCount`, and "Generate Mip Maps"; avoid spaced or hyphenated variants except when quoting Unity UI text.
- Use exact Unity/API spellings for engine concepts: `GameObject`, `MonoBehaviour`, `ScriptableObject`, `AssetBundle`, `AssetReference`, `Addressables`, `IMGUI`, `UI Toolkit`, `uGUI`, `sRGB`, `CPU`, `GPU`, `GC`, and `.asmdef`.
- Use "runtime" as one word. Use "build time" / "load time" as noun phrases and "build-time" / "load-time" only as compound modifiers; keep code identifiers such as `BuildTime` and `LoadTime`.
- Use "normal map", "shader variant", "draw call", "call graph", and "call tree" as noun phrases. Hyphenate only when they modify another noun, such as "normal-map import settings", "shader-variant collection", "draw-call overhead", "call-graph pass", and "call-tree node".
- Use "SRP Batcher" with a space. Use "Read/Write" for Unity UI labels and lower-case "read/write" only in generic prose.

## Code Style

- Follow [.NET Runtime C# coding style](https://github.com/dotnet/runtime/blob/main/docs/coding-guidelines/coding-style.md) for formatting and control-flow layout. Use [Microsoft Learn C# conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions) for broader guidance where runtime style is silent.
- Use Allman braces: opening and closing braces on their own lines, aligned with the current indent.
- For single-statement `if`/`else if`/`else` chains:
  - Never use the single-line form (e.g. `if (source == null) throw new ArgumentNullException(nameof(source));`).
  - Braces are always acceptable, and required if any branch uses braces or if a body spans multiple lines.
  - Braces may be omitted only when every branch is a single statement on its own indented line.
- Remove unused `using` namespaces.
- Omit `private` access modifiers.
- Use `var` when possible.
- Use argument labels when passing bools to a method
- For public data, use properties by default; use fields only when you must. For internal implementation details, use fields or private properties.
- `AnalysisModule` and `DiagnosticAnalyzer` implementation classes should not be public. However, they can be `internal` if needed.
- Seal classes when possible.
- Add XML docs on all public classes, methods, and properties.
- Prefer C# best practices over Unity-specific patterns where they diverge.
- Be Unity-agnostic when possible.
- LINQ and Reflection are permitted in Editor code, but avoid them on editor hot paths (see below).
- Early out methods when possible.
- Avoid redundant code.
- Keep things simple. Don't over-engineer unless the complexity is justified.
- Place each attribute on its own line above the member, type, or method it applies to. Do not put attributes on the same line as the declaration. Multiple attributes each get their own line. Inline attributes are only acceptable on parameters and type parameters, where a preceding line is not valid.

## Editor Hot-Path Performance

The editor invokes certain entry points frequently — on every repaint, domain reload, asset import, or script compilation. Patterns that incur allocations or per-call overhead in these paths accumulate into noticeable editor slowdowns. The following rules reflect what Smart Auditor's own analyzers detect and report.

**Entry points considered hot paths:**

- GUI repaint methods on `Editor` and `EditorWindow` subclasses: `OnGUI`, `OnInspectorGUI`, `OnSceneGUI`
- Domain-reload hooks: `[InitializeOnLoad]` static constructors, `[InitializeOnLoadMethod]`, `[DidReloadScripts]`
- Menu items: `[MenuItem]`
- Asset pipeline callbacks: `AssetPostprocessor` overrides and `OnPostprocessAllAssets`

**Rules:**

- Do not use `System.Linq` in, or reachable from, hot-path entry points. LINQ operators allocate iterators and closures on every call. Use explicit loops and pre-cached collections instead.
- Cache `System.Reflection` results (e.g., `MemberInfo`, compiled delegates). Reflection metadata lookups are not inlined and should never run per-frame or per-repaint.
- Prefer `TypeCache` for editor type discovery over `AppDomain.CurrentDomain.GetAssemblies()`. `TypeCache` is populated once at domain load and is significantly faster.
- Avoid `AssetDatabase.FindAssets` on hot paths without caching. It walks the entire asset database; narrow the filter and cache the result, refreshing only when assets change.
- Keep `[InitializeOnLoad]` static constructors and `[InitializeOnLoadMethod]` bodies minimal. Defer heavy initialization to first use.
- Avoid string concatenation and transient collection allocation in repaint paths. Cache formatted strings and reuse buffers where possible.
- Avoid reading allocating properties in tight IMGUI loops. For example, `GUISkin.customStyles` allocates on each read; use `GetStyle` or `FindStyle` instead.
- Avoid `MonoBehaviour.OnGUI` outside of intentional legacy IMGUI use — it initializes the full IMGUI system on every frame.
- Guard or conditionally compile `Debug.Log`/`Debug.LogWarning` calls that run on frequent paths. Each call incurs formatting, stack capture, and a native pipeline round-trip.

## Diagnostics

- When adding a new diagnostic, check the ID is unique.
- Take into account that the final diagnostic report might be consumed by a LLM.
- Keep the title short and scannable. It should act as a label, not a full explanation.
- Make titles consistent across descriptors.
- Match the strength of the wording to the certainty of the rule. Use strong wording like "Do not use …" only when the pattern is always undesirable. Use softer wording like "Consider avoiding …" or neutral wording when the issue is context-dependent.
- Messages should match the tone of the title. Description and Recommendation don't have to.
- Make the message sentence-like, specific, and contextual. Use `messageFormat` for the fuller explanation, ideally with placeholders for symbol names or context.
- Avoid overqualified names unless precision matters. `Object.Instantiate<GameObject>` is easier to scan than `UnityEngine.Object.Instantiate<UnityEngine.GameObject>` when ambiguity is low.
- Use the descriptor's description for more detailed guidance. Put longer explanations, rationale, caveats, and remediation guidance in the description rather than in the title or message.
- The description is explanatory, not prescriptive. Its job is to explain why the rule exists and when it applies.
- The recommendation explains how to fix the issue.
