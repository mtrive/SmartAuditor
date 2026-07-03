<a name="DiagnosticDrafting"></a>
# Diagnostic Drafting Guidelines

This guide is for authors of Smart Auditor analyzers. It defines how to write the four user-facing strings on a `Descriptor` so that the rendered diagnostic is readable, actionable, and machine-consumable.

For the list of existing descriptor IDs and their numeric ranges, see [DiagnosticsReference.md](DiagnosticsReference.md). This document is about *how to write* a descriptor; that one is about *which IDs are taken*.

---

## 1. Audience and rendering

Every diagnostic Smart Auditor produces is read by two audiences:

- **A human developer** browsing the SmartAuditor window. They see the `Title` in a column, the `MessageFormat` (substituted with runtime args) as the per-instance row, and the `Description` + `Recommendation` in the details panel when they click a row.
- **An LLM agent** consuming the JSON / MCP export. The agent sees the same `Title`, `Description`, `Recommendation`, plus `DocumentationUrl`, `Impact`, and `Platforms`. **It does not see `MessageFormat`** — that field is stripped from the agent surface (see `DescriptorJsonConverter.cs`). It does see `Evidence` if the analyzer attached any via `WithEvidence(...)`.

Both audiences need to be able to decide what to do based on the descriptor text alone. The agent has the stricter requirement: it has no body language, no tooltip hover, no ability to ask a clarifying question.

| Field | Where it renders | Audience |
|---|---|---|
| `Title` | Issue list column, details panel header, JSON `title` | Human + Agent |
| `MessageFormat` (formatted with args) | Issue list row | Human only |
| `Description` | Details panel, JSON `description` | Human + Agent |
| `Recommendation` | Details panel, JSON `recommendation` | Human + Agent |
| `WithEvidence(key, value)` | Details panel evidence footer, JSON `evidence` | Human + Agent |
| `DocumentationUrl` | "Learn more" link, JSON `documentationUrl` | Human + Agent |

---

## 1.5 Picking the right `AnalysisCategory`

Before drafting any text, the analyzer author picks an `AnalysisCategory` for the descriptor. The category drives which window/tab the diagnostic shows up in and how reviewers mentally bucket it. The choice is **structural**, not stylistic — and the rule is:

> **Where does the user open to fix the finding?**

| Category | Where the fix opens | Owns |
|---|---|---|
| `ProjectSetting` | Project Settings window | Player / Quality / Editor / Graphics-pipeline settings — anything project-wide |
| `Scene` | The Hierarchy window or scene-level structural inspection | Scene structure: object count, hierarchy depth, asset reference density, scene-asset dependency graph |
| `SceneSettings` | Window > Rendering > Lighting (or scene-level RenderSettings inspectors) | Scene-local *settings*: skybox, fog, ambient, lightmap data, per-scene NavMesh build, reflection-probe coverage |
| `AssetIssue` | The Inspector for a specific `.asset` / import file | Per-asset import settings, per-asset configuration on disk |
| `GameObjectIssue` | The Inspector for a specific component in a scene/prefab | Component-level issues on a specific GameObject |

`Scene` vs `SceneSettings` is the easy one to get wrong: a finding like "this scene has 12 000 GameObjects" is `Scene` (structure); a finding like "this scene's ambient mode is Skybox but no skybox material is assigned" is `SceneSettings` (scene-local settings). The user fixes them in different parts of Unity's UI, so they belong in different buckets.

The other category that's easy to misuse is `ProjectSetting`. If a finding operates on a per-asset object loaded via `AssetDatabase.LoadAssetAtPath<...>(...)` — even an asset that *configures* the project, like a Reflection Probe or a Lighting Settings asset — it is an `AssetIssue`, not a `ProjectSetting`. `ProjectSetting` is reserved for the global state surfaced in the Project Settings window.

---

## 2. The four strings — one rule each

### Title

A short noun phrase identifying the issue.

- **Title Case.** First letter of each significant word capitalised.
- **No trailing period.**
- **≤ 80 characters.**
- **Domain prefix** where one applies. The corpus uses these consistently:

  | Prefix | Used for |
  |---|---|
  | `Audio:` | AudioClip / Audio settings |
  | `URP:` | Universal Render Pipeline settings & assets |
  | `HDRP:` | High Definition Render Pipeline |
  | `Player:` | Player Settings (per-platform) |
  | `Shader:` | Shader source / properties |
  | `Material:` | Material asset issues |
  | `Texture:` | Texture import settings |
  | `Mesh:` | Mesh import settings |
  | `Light:` | Light component issues |
  | `Camera:` | Camera component issues |
  | `LOD Group:` | LODGroup component issues |

  Generic asset/code findings that don't fit a domain are unprefixed (`"Object allocation"`, `"Possibly unused asset"`).

- **Never contains a `{N}` placeholder.** Per-instance information goes in `MessageFormat`, not `Title`. The title is the *class of problem*, not the *specific instance*.

#### API member titles

When the finding is rooted in a specific Unity API member (property, method, or field), use the dotted-symbol grammar:

- **Subject is the dotted symbol in source casing.** Write `Animator.parameters`, `Renderer.material`, `Mesh.vertices`, `NavMeshPath.corners` — not `parameters Property`, `material Getter`, or `vertices Array`.
- **Drop the filler nouns `Property`, `Getter`, `Setter`.** The `.` already tells the reader it is a member; the noun adds no information and forces awkward Title-Case treatment of a lowercase identifier.
- **Prefix rule.** Drop the domain prefix when it duplicates the declaring type (the symbol carries it):
  - Drop: `Animator:` before `Animator.parameters`, `Renderer:` before `Renderer.material`.
  - Keep: subsystem prefixes that name a broader concept different from the type: `Animation:`, `Physics:`, `Physics 2D:`, `Input:`, `NavMesh:`, `IMGUI:`, `Rendering:`, `Terrain:`, `Font:`.
- **Read vs write.** When a getter and setter are separate descriptors, append `on Read` / `on Write` after the verb phrase. Never use `Getter` or `Setter`. Example: `Material.mainTexture Rehashes _MainTex on Read`.
- **Aggregates.** When no single member is the subject (e.g., any UV channel), keep the subsystem prefix and use a clear noun phrase: `Mesh: UV Channels Allocate`, `Terrain: Array Property Allocates` -> `Terrain: Detached Array Allocates`.
- **Titles stay markup-free.** `Title` is exported verbatim to the JSON/MCP/LLM surface (§1). Bold the symbol only in `Description`/`Recommendation` (§3); the UI handles emphasis at render time.

### Description

One or two sentences explaining *what is happening* and *why it's a problem*. Not what to do — that's the recommendation's job.

- **Self-contained.** An LLM agent reading only the `Description` (without `Title` or `Recommendation` as context) must understand the issue. Spell out the exact Unity setting, property, or API responsible.
- **No demonstrative pronouns without an antecedent.** `"this can cause incorrect rendering"` reads fine to a human looking at the row, but an agent receiving only the field text has nothing for `"this"` to refer to. Rewrite to name the noun: `"Sampling normal-map data in sRGB space causes incorrect lighting."`
- **No second-person voice.** `"You should disable this..."` is informal and harder for agents to parse. Use third-person or imperative: `"Disable the option when..."`.
- **`<b>...</b>` markup** on every UI label, settings path, property name, or API symbol referenced. See §3.
- **One to two sentences.** A paragraph-length description is a smell — break it into two sentences or move the trailing context into the recommendation.

### Recommendation

One to three sentences telling the reader what to do, in imperative voice.

- **Imperative verbs.** `"Set X to Y."` `"Use Z instead of W."` `"Enable W."` `"Add a B component."` `"Remove the orphaned entry."` Avoid hedged openings like `"Try to avoid..."`, `"You might want to..."`, `"Consider..."` unless the right action genuinely depends on context the analyzer can't see. `"Consider"` is reserved for "the analyzer can't tell which is right for your situation; here are the trade-offs."
- **Concrete steps.** Name the exact menu path: `<b>Project Settings &gt; Audio &gt; Default Speaker Mode</b>`. Or the exact property: `<b>Mesh.GetVertices(List&lt;Vector3&gt;)</b>`.
- **Multiple steps if needed**, but cap at three. Anything longer belongs in the linked documentation.
- **No hedging.** If the analyzer flagged it, the user already knows it might be a problem. The recommendation tells them how to fix it, not whether they should.

### MessageFormat

The per-instance render. Names *which entity has the problem*; structured detail goes in evidence.

- **Shape.** `"[Type] '{0}' [verb-phrase]"` matches ≈70 % of the corpus and reads well. Example: `"AudioClip '{0}' Load Type is set to Streaming"`. Other shapes are fine when they read more naturally, but the entity-name placeholder should usually be early.
- **Length budget: ≤ 120 characters once realistic args are substituted.** A typical asset path or object name is 30–50 characters; budget around that. The format renders into a single row of the SmartAuditor window — anything longer wraps or clips.
- **Placeholder count: 1–3 `{N}` placeholders.** More than 3 is a smell. More than 5 is almost certainly cramming evidence into the message — split the detail out into `WithEvidence(...)` calls (see below).
- **Never duplicates `Title` verbatim.** If the only thing the format adds is the entity name in quotes, the format is doing nothing — but a verbatim copy is worse than no format at all, because it implies per-instance content that isn't there.
- **`{N}` indices must match `Diagnostic.Create` args exactly.** `Diagnostic.Create` validates this at runtime and throws on mismatch — see §8.
- **Quote placeholders that render identifiers** (asset paths, GameObject names, field names, version strings). **Do not quote placeholders that render numbers** (counts, indices, slot numbers, percentages, thresholds). Use the `LOD{0}` prefix form for LOD indices.

### MessageFormat vs. `WithEvidence(...)`

`MessageFormat` answers *which entity has the problem*. `WithEvidence(key, value)` carries the numeric counts, ratios, thresholds, computed values, and secondary IDs that explain *how bad* the problem is.

Evidence is rendered in the Diagnostic Evidence footer of the details panel and exposed verbatim in the JSON / MCP surface — where an LLM agent can read it as **structured data**, not text it has to parse. That makes evidence the right place for anything numeric.

As a rule:
- **Numbers, percentages, thresholds, comparison values, secondary IDs → evidence.**
- **The entity name (asset path, GameObject name, property name) → MessageFormat.**

Worked example for OBJ0007 below in §7.

---

## 3. Markup conventions

`<b>...</b>` is the only markup allowed. Smart Auditor's text renderer supports it; nothing else (no `<i>`, no `<color>`, no Markdown).

**Use `<b>` on:**
- UI labels exactly as they appear in Unity: `<b>Read/Write Enabled</b>`, `<b>Force To Mono</b>`.
- Settings paths: `<b>Project Settings &gt; Audio &gt; Default Speaker Mode</b>`.
- Property / field / API symbols: `<b>Mesh.boneWeights</b>`, `<b>Material.GetTexture()</b>`, `<b>[FormerlySerializedAs]</b>`.
- Format values: `<b>16 bits</b>`, `<b>Disabled</b>`, `<b>OnAwake</b>`.

**Don't use `<b>` for:**
- General-prose emphasis. If the sentence reads fine without bold, leave it plain.
- Entire sentences. Bold should call out the specific token; readers skim for the unbolded prose around it.

---

## 4. LLM-agent self-containment checklist

Before merging a descriptor, the drafter should be able to answer **yes** to every one of these:

1. Does the `Description` name the exact Unity setting, property, or API responsible for the issue, by its in-product name?
2. Does the `Recommendation` give one concrete action (or a small numbered set), with the UI path or API symbol the user navigates to?
3. Are all demonstrative pronouns (`this`, `it`, `that`) resolved to their referent within the same field?
4. Does `DocumentationUrl` point to a canonical reference (Unity manual, package readme, vendor blog) — not a forum post or a Stack Overflow answer?
5. Are `Impact` and `Platforms` set correctly so the finding doesn't surface on platforms where it isn't relevant?

If any answer is "no", fix it before opening the PR. Agents can't ask follow-up questions.

---

## 5. Conventions inherited from established tools

The rules above didn't appear from nowhere — they mirror what the major static-analysis tools have settled on:

| Tool | What it shares with this guide |
|---|---|
| **Roslyn** (`Microsoft.CodeAnalysis.DiagnosticDescriptor`) | Parameterised `MessageFormat` with `{N}` placeholders; `Title` + `MessageFormat` separation; `HelpLink` per descriptor. |
| **clippy** (Rust) | The "what does this lint do" / "why is it bad" / "what should you do instead" three-way split, mirrored here as `Title` + `Description` + `Recommendation`. |
| **ReSharper / Rider** | Imperative-voice quick-fix wording (`"Use 'is' pattern"`, `"Remove redundant cast"`) — same shape as our recommendations. |
| **ESLint** | Short rule name, longer prose description, link to the rule's documentation page. The prefix scheme (`CDE`, `PLY`, `TEX`, `OBJ`, `PKG`, …) plays the same role as ESLint's `eqeqeq` / `no-unused-vars` identifiers. |

If you're familiar with any of these, the Smart Auditor descriptor model should feel natural. If you're not, those tools' documentation is a worthwhile reference.

---

## 6. Identifier scheme

Smart Auditor descriptor IDs (`CDE0067`, `GFX0008`, `OBJ0018`, …) follow a prefix-and-range scheme documented in [DiagnosticsReference.md](DiagnosticsReference.md). When you add a new descriptor, pick the next free ID in the relevant range and add a row to that file's table. The drafting rules in *this* document don't override anything there — they describe the text, not the ID.

---

## 7. Worked examples

### 7.1 A good descriptor — `TEX0002` (Texture: Read/Write Enabled)

```csharp
Title         = "Texture: Read/Write Enabled"
Description   = "The <b>Read/Write Enabled</b> flag in the Texture Import Settings is enabled. This causes the texture data to be duplicated in memory."
Recommendation= "If not required, disable the <b>Read/Write Enabled</b> option in the Texture Import Settings."
MessageFormat = "Texture2D '{0}' Read/Write is enabled"
```

Why it works:
- `Title` is short, Title-Case, `Texture:` domain prefix, no trailing period, no placeholder.
- `Description` names the exact UI flag (`<b>Read/Write Enabled</b>`), in bold. Two sentences: what, then why.
- `Recommendation` is one imperative sentence with the same UI label in bold.
- `MessageFormat` carries the asset name in `{0}`, doesn't duplicate the title, well under 120 characters.

### 7.2 A descriptor that needed tightening — `GFX0008`

**Before:**
```csharp
Title         = "URP: URP Asset is not specified"
MessageFormat = "URP: URP Asset is not specified"   // verbatim Title — adds nothing
```

**After:**
```csharp
Title         = "URP: URP Asset is not specified"
MessageFormat = "URP Asset is not specified for '{0}'"   // names the platform / quality tier
```

The instance now identifies *which* platform/quality tier is missing the URP Asset, instead of telling the user the same thing the title already said.

### 7.3 A descriptor with paragraph-length prose — `CLN0001` (Possibly unused asset)

**Before:**
```
Description = "No chain of Unity references runs from your enabled Build Settings scenes, Resources files, or (when on) Addressables entries to this asset. Runtime loading, AssetBundles, or editor-only use can still require it."
```

Three sentences, second-person voice (`"your"`), and the second half drifts into "what you should know before deleting it" — which is recommendation territory, not description territory.

**After:**
```
Description    = "No chain of Unity references reaches this asset from enabled Build Settings scenes, Resources folders, or Addressables entries."
Recommendation = "Confirm the asset is unreachable before deleting it. Runtime loading via <b>Resources.Load</b>, AssetBundles, or editor-only references can still require an asset that fails this static check."
```

The description now states one fact. The qualifications about runtime loading move to the recommendation, where they belong — they're guidance about how to act on the finding.

### 7.4 A descriptor cramming evidence into MessageFormat — `OBJ0007` (LOD Triangle Reduction)

**Before:**
```csharp
MessageFormat = "LOD{0} on '{1}' has {2} triangles (only {3}% reduction from LOD{4}'s {5} triangles), below the {6}% minimum reduction threshold"
```

Seven placeholders. Once `{1}` is a real GameObject path, the rendered row is well over 200 characters — it wraps, it clips, and the structured numbers are buried in a sentence the agent has to regex-parse.

**After:**
```csharp
MessageFormat = "LOD{0} on '{1}' has insufficient triangle reduction"

// at the call site:
Diagnostic.Create(AnalysisCategory.GameObjectIssue, descriptor.Id, lodIndex, gameObjectName)
    .WithLocation(...)
    .WithEvidence("TriangleCount", currentTriangles)
    .WithEvidence("PreviousTriangleCount", previousTriangles)
    .WithEvidence("ReductionPercent", reductionPercent)
    .WithEvidence("MinimumReductionPercent", threshold);
```

Same information surfaced — but the row stays readable, the evidence panel shows the numbers in a structured table, and the JSON / MCP surface delivers them as named fields an LLM can use directly. **This is the canonical pattern**: short message identifying the entity; structured data on the side.

---

## 8. Validation at runtime

Two safety nets catch the most common mechanical errors so authors don't ship them:

- **`Diagnostic.Create(...)` validates `MessageFormat` against `messageArgs`.** A descriptor with `MessageFormat = "{0} foo {1}"` called with one arg, or with no `MessageFormat` but two args, throws `InvalidOperationException` at the call site. The first analyzer run after writing a new descriptor will surface mismatches immediately.

- **`InsightTableBuilder.AddRow(...)` validates row keys against the registered schema.** A row with a key that isn't in the schema throws `ArgumentException` with the key name. Typo-grade bugs (`Coloumn` vs `Column`) surface at the emission site, not silently in the rendered report.

You don't need to write your own argument-count check or schema check — they happen automatically. Just match the placeholder count to your args and use the schema-declared column names.

---

## See also

- [DiagnosticsReference.md](DiagnosticsReference.md) — full list of registered descriptor IDs by prefix.
- [AgenticAnalysis.md](AgenticAnalysis.md) — how the report flows to LLM agents via the MCP surface; useful background for the self-containment requirements in §4.
