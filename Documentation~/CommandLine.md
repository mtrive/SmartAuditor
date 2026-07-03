# Command-Line Analysis

Smart Auditor provides a package-owned command-line bridge for running analysis from CI, scripts, and coding agents.
Use Unity's `-executeMethod` argument to call `SmartAuditor.Editor.CommandLine.Run`.

For agent workflow guidance, see [Agentic Analysis](AgenticAnalysis.md). For C# scripting entry points, see
[Scripting API Overview](API.md).

## Batch invocation

```bash
"/Applications/Unity/Hub/Editor/6000.0.0f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode \
  -quit \
  -projectPath "/path/to/UnityProject" \
  -executeMethod SmartAuditor.Editor.CommandLine.Run \
  -smartAuditorReportPath "/tmp/smart-auditor-report.json" \
  -smartAuditorScope "Code,Assets,ProjectSettings" \
  -smartAuditorCodeContext All
```

## Supported Smart Auditor arguments

| Argument | Value | Description |
|----------|-------|-------------|
| `-smartAuditorReportPath` | File path | JSON report path. Relative paths are resolved from the project root. Defaults to `smart-auditor-report.json` in the project root. |
| `-smartAuditorScope` | Comma-separated scopes | Broad project areas to analyze: `Code`, `ProjectSettings`, `Assets`, `Prefabs`, `Shaders`, `Build`, or `All`. Ignored when `-smartAuditorCategories` is supplied. |
| `-smartAuditorCategories` | Comma-separated categories | Categories to analyze. Accepts enum names such as `Code` and stable keys such as `project-setting`. Omit to analyze all categories. |
| `-smartAuditorPlatform` | Build target | Optional Unity `BuildTarget`, such as `Android`, `iOS`, or `StandaloneOSX`. Omit to use the active build target. |
| `-smartAuditorCodeContext` | `Runtime`, `Editor`, or `All` | Optional code context for code analysis. |
| `-smartAuditorAssemblies` | Comma-separated names | Optional assemblies for code analysis, such as `Assembly-CSharp`. |
| `-smartAuditorAssetPaths` | Comma-separated paths | Exact asset paths to include, such as `Assets/UI/Icon.png`. |
| `-smartAuditorAssetPathPrefixes` | Comma-separated prefixes | Asset path prefixes to include, such as `Assets/UI` or `Packages/com.company.game`. |
| `-smartAuditorAnalysisSource` | `Assets` or `LoadedScene` | Limits modules to asset-backed analysis or loaded-scene analysis. `LoadedScene` requires `-smartAuditorScenePath`. |
| `-smartAuditorScenePath` | Scene asset path | Scene asset to open before loaded-scene analysis, such as `Assets/Scenes/Main.unity`. Implies `-smartAuditorAnalysisSource LoadedScene` when source is omitted. |
| `-smartAuditorHierarchyPaths` | Pipe-separated hierarchy paths | Hierarchy roots to include in loaded-scene analysis, such as `Canvas/Header|Systems/Audio`. Requires `-smartAuditorAnalysisSource LoadedScene` and `-smartAuditorScenePath`. |
| `-smartAuditorAnalyzeReadOnlyPackages` | Optional flag or `true`/`false` | Includes read-only package assets and assemblies. Defaults to `false`. |
| `-smartAuditorPrettyPrint` | `true` or `false` | Controls JSON formatting. Defaults to `true` for easier human inspection. |
| `-smartAuditorDebugReport` | Optional flag or `true`/`false` | Includes Smart Auditor debug metadata in the JSON report: the full descriptor catalog and UI layout metadata. Defaults to `false`. |
| `-smartAuditorExportContentMode` | `Full`, `IssuesOnly`, or `IssuesPlusSummary` | Controls report payload size and sections. `issues`, `messages`, `descriptors`, and `sessionInfo` are included in compact modes. Defaults to `IssuesOnly` when omitted. Pick `Full` when you also need `insights` or `moduleMetadata`. |
| `-smartAuditorFailOnIssues` | Flag | Returns exit code `2` when analysis succeeds but reports at least one issue. |
| `-smartAuditorMinSaveSeverity` | `Default`, `Error`, `Critical`, `Major`, `Moderate`, or `Minor` | Drops items below this severity from the saved JSON report. `Default` (the sentinel) means "no filter" and explicitly disables the project setting for this run. Omit to fall back to **Project Settings &gt; Smart Auditor &gt; Report Filter**. The in-memory result is unaffected. `None` / `Hidden` are rejected. |

## Exit codes

| Code | Meaning |
|------|---------|
| `0` | Analysis completed and the report was saved. |
| `1` | Analysis failed, was canceled, or produced an invalid report. |
| `2` | Analysis completed, but issues were found and `-smartAuditorFailOnIssues` was supplied. |

## CI and automation recommendations

- Pass an explicit `-smartAuditorReportPath` so artifact collection is deterministic.
- Keep `-batchmode -quit` for unattended runs.
- Use `-smartAuditorFailOnIssues` when issues should fail the job.
- Archive the JSON report and Unity Editor log for diagnostics.
- Use compact export by default (`IssuesOnly`), and switch to `Full` only when deeper data is required.

## Targeted analysis examples

Use the narrowest scope that can answer the current question.

Broad project area:

```bash
-smartAuditorScope Code
```

Exact category:

```bash
-smartAuditorCategories Texture
```

Assembly-focused code run:

```bash
-smartAuditorScope Code \
-smartAuditorAssemblies Assembly-CSharp \
-smartAuditorCodeContext Runtime
```

Asset-folder-focused run:

```bash
-smartAuditorScope Assets,Prefabs \
-smartAuditorAssetPathPrefixes "Assets/UI,Assets/Characters"
```

CI-friendly run that only saves actionable issues:

```bash
-smartAuditorScope Code,Assets \
-smartAuditorMinSaveSeverity Major \
-smartAuditorFailOnIssues
```

Loaded-scene run with deterministic context:

```bash
-smartAuditorScope Prefabs \
-smartAuditorAnalysisSource LoadedScene \
-smartAuditorScenePath Assets/Scenes/BattleArena.unity \
-smartAuditorHierarchyPaths "BossRoot|Lighting/Realtime"
```

Do not use `-smartAuditorAnalysisSource LoadedScene` by itself. Provide `-smartAuditorScenePath` to guarantee
deterministic scene context.
