# Smart Auditor

Smart Auditor is a suite of static analysis tools for Unity projects. It inspects code, assets, settings, scenes, and packages without running the game, then produces an interactive report of actionable findings.

It complements runtime tools — Unity Profiler, Frame Debugger, Memory Profiler, Profile Analyzer — by catching whole categories of issue you would otherwise only notice on the device: slow loads from unstripped mip levels, IL2CPP misconfiguration, GC-allocating Unity API patterns, scene render-settings drift, unconfigured Default Presets, missing LOD groups, oversized StreamingAssets folders, and so on.

> **Smart Auditor is an independent open-source project. It is not affiliated with, endorsed by, or sponsored by Unity Technologies.**

## What it analyzes

After analyzing your project, Smart Auditor produces a report covering:

- **Code** — Unity-API misuse, allocating property and method patterns, reflection-driven dispatch, IMGUI legacy paths, attribute mis-application. Backed by `Mono.Cecil` IL inspection.
- **Assets** — texture, mesh, audio-clip, shader, material, sprite, and lighting-asset import settings, plus Resources, Addressables, StreamingAssets, asset-hygiene, and asmdef diagnostics.
- **Project settings** — Player, Quality, Physics, Time, Editor, Graphics, and render-pipeline-specific (URP, HDRP, SRP common) configuration. Defaults that ship without review get called out.
- **GameObjects** — component-level findings in scenes and prefabs (LODGroup misuse, high-poly MeshCollider warnings, missing references, light setup, …).
- **Scene structure and settings** — hierarchy depth, object count, RenderSettings (skybox, fog, ambient, sun source), and future LightmapSettings / NavMesh / reflection-probe-coverage findings.
- **Build report** — per-step build duration and per-asset payload from the last clean build.
- **Packages** — installed package versions plus preview / experimental / deprecated status.

Every finding carries a diagnostic ID whose 3-letter prefix names the subsystem it concerns — `CDE` for code patterns, `TEX` for texture imports, `PHY` for physics settings, `OBJ` for GameObject components, and so on. The full prefix catalog and per-ID reference are in [`Documentation~/DiagnosticsReference.md`](Documentation~/DiagnosticsReference.md).

## Requirements

Unity 6 or newer.

## Disclaimer

Smart Auditor is in active pre-release development. APIs, diagnostic IDs, and report formats may change before a stable release. Pin your installation to a specific commit or tag if you depend on stable IDs or output shapes.

## Installation

The simplest install path is via the Unity Package Manager.

1. Open **Window > Package Manager**.
2. Click the `+` icon (top-left) and choose **Add package from git URL**.
3. Paste:

   ```
   https://github.com/mtrive/SmartAuditor.git
   ```

Pin to a specific release by appending `#<tag>`, for example `#0.1.0`. Releases are listed at <https://github.com/mtrive/SmartAuditor/releases>.

Alternatively, clone the repository into your project's `Packages/` folder, or clone elsewhere and add `com.mtrive.smart-auditor` to `Packages/manifest.json` with a local file path.

Full install instructions and troubleshooting (notably the `Mono.Cecil` namespace clash with older Burst versions) are in [`Documentation~/Installing.md`](Documentation~/Installing.md).

## How to use

In the Unity Editor:

- **`Window > Smart Auditor > Smart Auditor`** opens the main analysis window. Click **Start Analysis** to scan the project, or load a previously-saved report. The Summary view links into per-category Views (Code, Assets, Project Settings, Scenes, Packages, Build Report, …). Each View supports filtering, sorting, ignoring false positives, and CSV export.
- **`Window > Smart Auditor > Smart Console`** opens a live replacement for the Unity Console — log-level filters, regex search, duplicate collapsing, and stack-trace navigation.

For programmatic and CI use, see [`Documentation~/CommandLine.md`](Documentation~/CommandLine.md). Smart Auditor also ships an MCP server (`smart-auditor`) for agent-driven analysis from Claude Code, Cursor, and other MCP-aware tools; see [`Documentation~/AgenticAnalysis.md`](Documentation~/AgenticAnalysis.md).

## Documentation

The full documentation set lives under [`Documentation~/`](Documentation~/), starting with [`index.md`](Documentation~/index.md). Highlights:

- [Diagnostic reference](Documentation~/DiagnosticsReference.md) — every descriptor by subsystem prefix.
- [Diagnostic drafting](Documentation~/DiagnosticDrafting.md) — authoring rules for new descriptors.
- [Agentic analysis](Documentation~/AgenticAnalysis.md) — using the MCP server from agent tooling.
- [API reference](Documentation~/API.md) — extending Smart Auditor in-project.

## License

Smart Auditor is licensed under the [MIT License](LICENSE.md).

## Contributing

Bug reports, feature requests, and pull requests are welcome at <https://github.com/mtrive/SmartAuditor>.

### Pull requests

1. Open a pull request against `main` at <https://github.com/mtrive/SmartAuditor/compare>.
2. Describe the problem, the chosen solution, and the alternatives you considered.
3. Make sure all tests pass and the diagnostic-text validation in `Diagnostic.Create` is clean.
4. Add [@mtrive](https://github.com/mtrive) as a reviewer.
5. Once approved, use **Squash and merge** and delete the remote branch.

PRs that add or change a diagnostic descriptor should follow the rules in [`Documentation~/DiagnosticDrafting.md`](Documentation~/DiagnosticDrafting.md) and update the per-ID table in [`Documentation~/DiagnosticsReference.md`](Documentation~/DiagnosticsReference.md) in the same change.
