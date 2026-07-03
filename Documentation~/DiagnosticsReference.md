<a name="DiagnosticsReference"></a>
# Diagnostics Reference

Smart Auditor reports issues using **diagnostic descriptors**. Each descriptor is uniquely identified by an ID consisting of exactly 3 uppercase letters followed by 4 digits (for example, `CDE0192`, `PHY0001`, `TEX0000`).

The letter prefix indicates the **subsystem**, so the ID reads as its own taxonomy at a glance:

| Prefix | Subsystem | Scope |
|--------|-----------|-------|
| `CDE` | Code patterns | Unity API calls, System.* API calls, allocations, reflection, IMGUI, GC, boxing. |
| `PLY` | Player settings | iOS / Android / WebGL Player Settings — code stripping, architecture, audio output, IL2CPP, GC, accelerometer, WebGL data caching, player optimization toggles. |
| `GFX` | Graphics, lighting & render pipelines | Graphics APIs, Graphics Jobs, mipmap stripping, lightmap streaming, forward / deferred, mixed shader quality, fog stripping, reflection probes, URP HDR / MSAA / render path / GPU Resident Drawer / GPU Occlusion Culling, HDRP Lit Shader Mode, SRP Batcher. |
| `QLT` | Quality settings | Default levels, async upload, texture streaming, low-quality textures, anisotropic filtering. |
| `PHY` | Physics & time settings | 3D + 2D physics settings, fixed timestep, and max allowed timestep. |
| `EDT` | Editor settings | Enter Play Mode, Reload Domain, project generation, texture compression override. |
| `IMP` | Importer defaults | Preset Manager defaults for asset importers. |
| `PKG` | Packages | Package version, preview / experimental, deprecated. |
| `TEX` | Texture asset imports | Texture and Cursor Texture import settings, normal-map import settings, material-inferred texture usage. |
| `MSH` | Mesh asset imports | Read/Write, 32-bit index format, strict vertex data checks. |
| `MDL` | Model asset imports | Model-importer / model-wide settings: remapped external-object (material) references. |
| `SHD` | Shader source & variants | Shader pragmas, fallback, SRP Batcher compatibility, GPU instancing support. |
| `MAT` | Material configuration | GPU instancing, stale shader properties and stale shader keywords. |
| `ACL` | AudioClip imports | Streaming, compression, sample rate, preload, background load, stereo / mono. |
| `SPR` | Sprite & SpriteAtlas | Sprite atlas utilization. |
| `ANI` | Animation, Animator, Avatar, AvatarMask | Reserved for future animation-asset findings; no descriptors yet. |
| `ADR` | Addressables | Addressable group configuration, Addressable entries, AssetReference fields, and Addressables blocking API use. |
| `RES` | Resources & StreamingAssets | Resources folder inclusion, Resources.Load / LoadAll use, and StreamingAssets payload size. |
| `ASF` | Asset file integrity | Serialized asset data, Unity YAML structure, GUID collisions, case-only path collisions, and ScriptableObject asset naming. |
| `CLN` | Asset hygiene | Unused-asset candidates, duplicate source files, and source files that should usually stay outside the Unity project tree. |
| `ASM` | Assembly definitions | `.asmdef` configuration issues. |
| `OBJ` | Component findings on GameObjects | LODGroup, MeshCollider, Renderer, Camera, MonoBehaviour, missing references, lights, area-light support. |
| `SCN` | Scene-level structural findings | Hierarchy depth, GameObject count, scene-asset dependency density. |
| `SCS` | Scene-level settings findings | RenderSettings (skybox, fog, ambient, sun source) and future LightmapSettings / per-scene NavMesh build / reflection-probe coverage findings. |

`CDE` is the only prefix that needs internal banding (it carries ~140 findings):

- `CDE0xxx` — Unity API calls
- `CDE1xxx` — System.* API calls
- `CDE2xxx` — Other code patterns (allocations, attributes, IMGUI, P/Invoke)

---

## CDE — Code patterns

### CDE0xxx — Unity API

| ID | Title | Severity | Areas | Platforms |
|----|-------|----------|-------|-----------|
| CDE0000 | Camera.main Uncached Lookup | Moderate | Performance | Any |
| CDE0003 | Allocation: GeometryUtility.CalculateFrustumPlanes Allocates | Moderate | Memory | Any |
| CDE0004 | Resources: FindObjectsOfTypeAll Scans Everything | Moderate | Memory | Any |
| CDE0007 | Physics: Rigidbody.SweepTestAll Allocates | Moderate | Memory | Any |
| CDE0008 | Physics: Cast-All API Allocates | Moderate | Memory | Any |
| CDE0012 | Physics: Overlap API Allocates | Moderate | Memory | Any |
| CDE0015 | Physics 2D: Cast-All API Allocates | Moderate | Memory | Any |
| CDE0021 | Physics 2D: Overlap-All API Allocates | Moderate | Memory | Any |
| CDE0026 | Component: GetComponentsInChildren Allocates | Moderate | Memory | Any |
| CDE0027 | Component: GetComponentsInParent Allocates | Moderate | Memory | Any |
| CDE0028 | GameObject: GetComponentsInChildren Allocates | Moderate | Memory | Any |
| CDE0029 | GameObject: GetComponentsInParent Allocates | Moderate | Memory | Any |
| CDE0030 | Physics: OnTriggerStay Fires Every Physics Step | Moderate | Performance | Any |
| CDE0032 | Physics 2D: OnTriggerStay2D Fires Every Physics Step | Moderate | Performance | Any |
| CDE0034 | Physics: OnCollisionStay Fires Every Physics Step | Moderate | Performance | Any |
| CDE0037 | Texture: ImageConversion.LoadImage Retains CPU Copy | Moderate | Memory | Any |
| CDE0039 | Renderer.material Creates Instance | Moderate | Performance | Any |
| CDE0051 | GPU Readback: ComputeBuffer.GetData Stalls CPU | Moderate | Performance | Any |
| CDE0056 | Reflection: GameObject.SendMessage Uses Reflection | Moderate | Performance | Any |
| CDE0057 | Reflection: Component.SendMessage Uses Reflection | Moderate | Performance | Any |
| CDE0058 | MonoBehaviour: OnGUI Initializes Legacy IMGUI | Minor | Performance | Any |
| CDE0059 | Allocation: Cacheable Property Allocates | Minor | Memory | Any |
| CDE0060 | Animator.parameters Allocates | Moderate | Memory | Any |
| CDE0061 | Animation: ParentConstraint.translationOffsets Allocates | Minor | Memory | Any |
| CDE0062 | Animation: ParentConstraint.rotationOffsets Allocates | Minor | Memory | Any |
| CDE0063 | Animation: AnimationCurve.keys Allocates | Minor | Memory | Any |
| CDE0066 | Camera.allCameras Allocates | Moderate | Memory | Any |
| CDE0067 | Mesh.boneWeights Allocates | Moderate | Memory | Any |
| CDE0068 | Mesh.bindposes Allocates | Moderate | Memory | Any |
| CDE0069 | Mesh.vertices Allocates | Moderate | Memory | Any |
| CDE0070 | Mesh.normals Allocates | Moderate | Memory | Any |
| CDE0071 | Mesh.tangents Allocates | Moderate | Memory | Any |
| CDE0072 | Mesh: UV Channels Allocate | Moderate | Memory | Any |
| CDE0081 | Mesh.colors Allocates | Moderate | Memory | Any |
| CDE0082 | Mesh.colors32 Allocates | Moderate | Memory | Any |
| CDE0083 | Mesh.triangles Allocates | Moderate | Memory | Any |
| CDE0084 | Renderer.materials Allocates | Moderate | Memory | Any |
| CDE0085 | Renderer.sharedMaterials Allocates | Moderate | Memory | Any |
| CDE0094 | Input.touches Allocates | Minor | Memory | Any |
| CDE0095 | Input.accelerationEvents Allocates | Minor | Memory | Any |
| CDE0100 | IMGUI: GUISkin.customStyles Allocates | Minor | Memory | Any |
| CDE0101 | URP: GetUniversalAdditionalLightData Has a Side Effect | Moderate | Correctness | Any |
| CDE0102 | URP: GetUniversalAdditionalCameraData Has a Side Effect | Moderate | Correctness | Any |
| CDE0103 | Physics: Collision.contacts Allocates | Moderate | Memory | Any |
| CDE0104 | Physics 2D: Collision2D.contacts Allocates | Moderate | Memory | Any |
| CDE0110 | Terrain: TerrainData.treeInstances Allocates | Moderate | Memory | Any |
| CDE0111 | Terrain: TerrainData.alphamapTextures Allocates | Moderate | Memory | Any |
| CDE0112 | Font.characterInfo Allocates | Minor | Memory | Any |
| CDE0115 | Animator.GetCurrentAnimatorClipInfo Allocates | Moderate | Memory | Any |
| CDE0116 | Animator.GetBehaviours Allocates | Moderate | Memory | Any |
| CDE0117 | AssetBundle: LoadAssetWithSubAssets Allocates | Moderate | Memory | Any |
| CDE0118 | AssetBundle: LoadAllAssets Allocates | Moderate | Memory | Any |
| CDE0119 | AssetBundleManifest: GetAllAssetBundles Allocates | Moderate | Memory | Any |
| CDE0129 | Object: FindObjectsOfType Scans the Whole Scene | Moderate | Performance, Memory | Any |
| CDE0132 | Texture: ImageConversion.EncodeTo* Allocates | Moderate | Memory | Any |
| CDE0136 | Allocation: Cacheable Method Allocates | Moderate | Memory | Any |
| CDE0144 | AssetBundleRequest.allAssets Allocates | Moderate | Memory | Any |
| CDE0148 | Cloth: Array Properties Allocate | Moderate | Memory | Any |
| CDE0153 | Camera.layerCullDistances Allocates | Moderate | Memory | Any |
| CDE0159 | Light Probes: Array Properties Allocate | Moderate | Memory | Any |
| CDE0163 | Material.shaderKeywords Allocates | Minor | Memory | Any |
| CDE0165 | Rendering: RenderTargetBinding Array Allocates | Moderate | Memory | Any |
| CDE0168 | SkinnedMeshRenderer.bones Allocates | Moderate | Memory | Any |
| CDE0173 | TextAsset.bytes Allocates | Moderate | Memory | Any |
| CDE0177 | Sprite: Geometry Properties Allocate | Moderate | Memory | Any |
| CDE0183 | Physics 2D: Collider2D.points Allocates | Moderate | Memory | Any |
| CDE0185 | Terrain: Array Properties Allocate | Moderate | Memory | Any |
| CDE0188 | Terrain: TerrainData.splatPrototypes Allocates | Moderate | Memory | Any |
| CDE0192 | Logging: Debug.Log / Debug.LogFormat Call | Minor | Performance | Any |
| CDE0193 | Logging: Debug.LogWarning / Debug.LogWarningFormat Call | Minor | Performance | Any |
| CDE0200 | Networking: Payload Data Allocates | Moderate | Memory | Any |
| CDE0219 | NavMesh: NavMeshPath.corners Allocates | Minor | Memory | Any |
| CDE0223 | GameObject.tag Allocates | Moderate | Memory | Any |
| CDE0224 | Allocation: Object.Instantiate at Runtime | Moderate | Performance, Memory | Any |
| CDE0225 | Allocation: GameObject.AddComponent at Runtime | Moderate | Performance, Memory | Any |
| CDE0227 | Shader: WarmupAllShaders Is Ineffective on Modern APIs | Major | Performance | Any |
| CDE0228 | Shader: ShaderVariantCollection.WarmUp Is Ineffective on Modern APIs | Major | Performance | Any |
| CDE0229 | Component.tag Allocates | Moderate | Memory | Any |
| CDE0231 | Object.name Allocates | Minor | Memory | Any |
| CDE0232 | Editor: AssetDatabase.FindAssets Is Expensive | Moderate | EditorWorkflow | Any |
| CDE0233 | WebGL: Microphone API Not Supported | Moderate | Compatibility | WebGL |
| CDE0234 | Object: FindObjectOfType Scans the Whole Scene | Moderate | Performance, Memory | Any |
| CDE0235 | Animator: Parameter Name Rehashed Per Call | Minor | Performance | Any |
| CDE0236 | Material: Shader Property Rehashed Per Call | Minor | Performance | Any |
| CDE0237 | Material.mainTexture Rehashes _MainTex on Read | Moderate | Performance, Memory | Any |
| CDE0238 | Material.mainTexture Rehashes _MainTex on Write | Moderate | Performance, Memory | Any |
| CDE0240 | Texture: Texture2D Constructor Omits Color Space | Minor | Performance, Memory | Any |
| CDE0241 | Texture: WebCamTexture.GetPixels Allocates | Moderate | Memory | Any |
| CDE0242 | Texture: WebCamTexture.GetPixels32 Allocates Without Buffer | Moderate | Memory | Any |
| CDE0243 | Texture: Texture2D.GetPixels Allocates | Moderate | Memory | Any |
| CDE0244 | Texture: Texture2D.GetPixels32 Allocates | Moderate | Memory | Any |
| CDE0245 | Texture: Texture2D.SetPixels Is Slower Than SetPixels32 | Moderate | Performance | Any |
| CDE0246 | Texture: Texture2D.SetPixels32 Allocates | Moderate | Performance | Any |
| CDE0247 | Texture: Texture3D.GetPixels Allocates | Moderate | Memory | Any |
| CDE0248 | Texture: Texture3D.GetPixels32 Allocates | Moderate | Memory | Any |
| CDE0249 | Texture: Texture3D.SetPixels Is Slower Than SetPixels32 | Moderate | Performance | Any |
| CDE0250 | Texture: Texture3D.SetPixels32 Allocates | Moderate | Performance | Any |
| CDE0251 | Texture: Texture2DArray.GetPixels Allocates | Moderate | Memory | Any |
| CDE0252 | Texture: Texture2DArray.GetPixels32 Allocates | Moderate | Memory | Any |
| CDE0253 | Texture: Texture2DArray.SetPixels Is Slower Than SetPixels32 | Moderate | Performance | Any |
| CDE0254 | Texture: Texture2DArray.SetPixels32 Allocates | Moderate | Performance | Any |
| CDE0255 | Texture: Cubemap.GetPixels Allocates | Moderate | Memory | Any |
| CDE0256 | Texture: Cubemap.SetPixels Allocates | Moderate | Performance | Any |
| CDE0257 | Texture: CubemapArray.GetPixels Allocates | Moderate | Memory | Any |
| CDE0258 | Texture: CubemapArray.GetPixels32 Allocates | Moderate | Memory | Any |
| CDE0259 | Texture: CubemapArray.SetPixels Is Slower Than SetPixels32 | Moderate | Performance | Any |
| CDE0260 | Texture: CubemapArray.SetPixels32 Allocates | Moderate | Performance | Any |
| CDE0261 | Texture: Texture2D.GetPixel Per-Pixel Marshalling | Moderate | Performance | Any |
| CDE0262 | Texture: Texture2D.GetPixelBilinear Per-Pixel Marshalling | Moderate | Performance | Any |
| CDE0263 | Texture: Texture2D.SetPixel Per-Pixel Marshalling | Moderate | Performance | Any |
| CDE0264 | Texture: Texture3D.GetPixel Per-Pixel Marshalling | Moderate | Performance | Any |
| CDE0265 | Texture: Texture3D.SetPixel Per-Pixel Marshalling | Moderate | Performance | Any |
| CDE0266 | Reflection: GameObject.SendMessageUpwards Uses Reflection | Moderate | Performance | Any |
| CDE0267 | Reflection: Component.SendMessageUpwards Uses Reflection | Moderate | Performance | Any |
| CDE0268 | Reflection: GameObject.BroadcastMessage Uses Reflection | Moderate | Performance | Any |
| CDE0269 | Reflection: Component.BroadcastMessage Uses Reflection | Moderate | Performance | Any |
| CDE0270 | Texture: Texture2D.PackTextures Allocates | Moderate | Memory | Any |
| CDE0271 | URP: Compatibility Mode Rendering API Used | Major | Correctness | Any |

### CDE1xxx — System API

| ID | Title | Severity | Areas | Platforms |
|----|-------|----------|-------|-----------|
| CDE1000 | LINQ: System.Linq Call | Moderate | Performance, Memory | Any |
| CDE1001 | Reflection: System.Reflection Call | Minor | Performance | Any |
| CDE1002 | Allocation: String.Concat Allocates | Minor | Memory | Any |
| CDE1003 | Time: DateTime.Now Performs Timezone Conversion | Minor | Performance | Any |
| CDE1004 | Reflection: AppDomain.GetAssemblies Is Expensive | Moderate | EditorWorkflow | Any |
| CDE1005 | WebGL: System.Net Not Supported | Moderate | Compatibility | WebGL |
| CDE1006 | WebGL: System.Threading Not Supported | Moderate | Compatibility | WebGL |
| CDE1007 | LINQ: System.Linq Reachable From Editor Hot Path | Moderate | EditorWorkflow, Performance, Memory | Any |
| CDE1008 | Strings: Comparison In Hot Path | Moderate | Performance | Any |

### CDE2xxx — Other Code Patterns

| ID | Title | Severity | Areas | Platforms |
|----|-------|----------|-------|-----------|
| CDE2000 | Allocation: Value Type Boxed to Object | Hidden | Memory | Any |
| CDE2001 | MonoBehaviour: Empty Unity Message | Minor | Performance | Any |
| CDE2002 | Allocation: Reference-Type Allocation | Hidden | Memory | Any |
| CDE2003 | Allocation: Closure Captures Local State | Minor | Memory | Any |
| CDE2004 | Allocation: Managed Array Allocation | Hidden | Memory | Any |
| CDE2005 | Allocation: params Array Allocation | Minor | Memory | Any |
| CDE2006 | Interop: P/Invoke Call | Minor | Performance, Memory | Any |
| CDE2007 | Editor: [InitializeOnLoad] Runs on Every Domain Reload | Moderate | EditorWorkflow | Any |
| CDE2008 | Editor: [InitializeOnLoadMethod] Runs on Every Domain Reload | Moderate | EditorWorkflow | Any |
| CDE2009 | Editor: [InitializeOnLoad] Missing Static Constructor Has No Effect | Major | EditorWorkflow | Any |
| CDE2010 | Editor: [InitializeOnLoad] Without Static Constructor Has No Effect | Major | EditorWorkflow | Any |
| CDE2011 | Editor: [InitializeOnLoadMethod] on Non-Static Method Has No Effect | Major | EditorWorkflow | Any |

---

## PLY — Player settings

| ID | Title | Severity | Areas | Platforms |
|----|-------|----------|-------|-----------|
| PLY0002 | Player (iOS): Accelerometer Polling Enabled | Moderate | Performance | iOS |
| PLY0003 | Player (iOS): Architecture Set to Universal | Moderate | BuildSize | iOS |
| PLY0004 | Player (Android): Both ARMv7 and ARM64 Targeted | Moderate | BuildSize | Android |
| PLY0007 | Player: Prebake Collision Meshes Disabled | Moderate | BuildSize, LoadTime | Any |
| PLY0008 | Player: Optimize Mesh Data Disabled | Moderate | BuildSize, LoadTime, Performance | Any |
| PLY0009 | Player: Engine Code Stripping Disabled | Moderate | BuildSize | Android, iOS, WebGL |
| PLY0010 | Player (WebGL): Data Caching Disabled | Moderate | LoadTime | WebGL |
| PLY0025 | Player (Android): Managed Code Stripping Too Low | Moderate | BuildSize | Android |
| PLY0026 | Player (iOS): Managed Code Stripping Too Low | Moderate | BuildSize | iOS |
| PLY0033 | Player: Audio Speaker Mode is Stereo or Surround | Moderate | BuildSize, Memory | Android, iOS |
| PLY0034 | Player: Incremental GC Disabled | Moderate | Performance | Any |
| PLY1004 | Player: IL2CPP Compiler Configuration is Master | Moderate | BuildTime | Any |
| PLY1005 | Player: IL2CPP Compiler Configuration is Debug | Moderate | Performance | Any |

---

## GFX — Graphics, lighting & render pipelines

| ID | Title | Severity | Areas | Platforms |
|----|-------|----------|-------|-----------|
| GFX0001 | Graphics: Mixed Standard Shader Quality Across Tiers | Moderate | BuildSize | Any |
| GFX0002 | Graphics: Forward Rendering Path | Info (off by default) | Performance | Any |
| GFX0003 | Graphics: Deferred Rendering Path | Info (off by default) | Performance | Any |
| GFX0004 | HDRP: Lit Shader Mode is Both in Render Pipeline Asset | Moderate | BuildSize, BuildTime | Any |
| GFX0005 | HDRP: Cameras Mix Forward and Deferred Lit Shader Modes | Moderate | BuildSize, BuildTime | Any |
| GFX0006 | Graphics: Fog Shader Variants Always Included | Moderate | BuildSize | Any |
| GFX0007 | SRP Asset: SRP Batcher Disabled | Moderate | Performance | Any |
| GFX0008 | URP: Active Render Pipeline Asset is Not a URP Asset | Moderate | Performance, Quality | Any |
| GFX0009 | URP: HDR Enabled on Mobile Platform | Moderate | Performance, Quality | Android, iOS, Switch |
| GFX0010 | URP: MSAA Sample Count is 4x or 8x on Mobile | Moderate | Performance, Quality | Android, iOS, Switch |
| GFX0011 | Graphics: Mixed Rendering Path Across Tiers | Moderate | BuildSize | Any |
| GFX0012 | Reflection Probe: Realtime Every-Frame Update | Moderate | Performance | Any |
| GFX0013 | Graphics (iOS): Metal API Validation Enabled | Moderate | Performance | iOS, StandaloneOSX, tvOS |
| GFX0014 | Graphics: Graphics Jobs Disabled | Moderate | Performance | Any |
| GFX0015 | Graphics API (iOS): Metal and OpenGLES Both Enabled | Moderate | BuildSize | iOS |
| GFX0016 | Graphics API (iOS): Metal API Not Enabled | Moderate | Performance | iOS |
| GFX0017 | Graphics: Texture Mipmap Stripping Disabled | Moderate | BuildSize | Any |
| GFX0018 | Graphics API (Android): Vulkan API Not Enabled | Moderate | Performance | Android |
| GFX0019 | Lighting: Lightmap Streaming is Disabled | Moderate | Memory | Any |
| GFX0020 | URP: GPU Resident Drawer Enabled Without Forward+ or Deferred+ | Moderate | Performance | Any |
| GFX0021 | URP: GPU Resident Drawer Enabled but BatchRendererGroup Variants Not Kept | Moderate | Quality | Any |
| GFX0022 | URP: GPU Occlusion Culling Enabled Without GPU Resident Drawer | Moderate | Performance | Any |
| GFX0023 | URP: Forward Renderer Under Light and Instancing Pressure | Moderate | Performance | Any |
| GFX0024 | URP: GPU Resident Drawer Blocked by Scene Features | Moderate | Performance | Any |
| GFX0025 | URP: GPU Occlusion Culling Disabled for Large Renderer Set | Moderate | Performance | Any |

---

## URP Render-Path Decision Guide

Use this table to choose a URP rendering path. Profile on target hardware — the right choice depends on your scene content, target device class, and which GPU-driven features you intend to use.

| Path | Best fits | Key trade-offs |
|------|-----------|----------------|
| **Forward** | Few lights, simple scenes, 2D, low-end/legacy mobile. | Per-object additional-light limit (default 8). Cannot use GPU Resident Drawer or GPU Occlusion Culling. |
| **Forward+** | Many realtime/mixed lights, MSAA, GPU Resident Drawer, GPU Occlusion Culling, Entities/DOTS rendering, probe blending, CPU-submission-heavy scenes. | Tile-grid GPU memory overhead; less efficient than Forward for very sparse light scenes. |
| **Deferred** | Many opaque realtime lights, no MSAA requirement, desktop / console style budgets. | Fills a G-buffer every frame; incompatible with MSAA; cannot use GPU Resident Drawer without combining with Deferred+. |
| **Deferred+** | Many lights where Deferred benefits apply AND GPU Resident Drawer / GPU Occlusion Culling are wanted. Validate Unity version and URP package support before targeting. | Same G-buffer cost as Deferred; requires Unity 6; feature support parity with Forward+ is still evolving. |

**GPU Resident Drawer prerequisites (Unity 6 / URP 17+):**
1. Set `Project Settings > Graphics > Shader Stripping > BatchRendererGroup Variants` to **Keep All**.
2. Enable **SRP Batcher** on the URP Asset.
3. Set **GPU Resident Drawer** to **Instanced Drawing** on the URP Asset.
4. Set every renderer in the Renderer List to **Forward+** (or **Deferred+**).

---

## QLT — Quality settings

| ID | Title | Severity | Areas | Platforms |
|----|-------|----------|-------|-----------|
| QLT0001 | Quality: Default Quality Levels Unmodified | Moderate | Performance, BuildSize, LoadTime | Any |
| QLT0002 | Quality: Texture Quality Below Full Res | Moderate | Performance | Any |
| QLT0003 | Quality: Async Upload Time Slice Left at Default | Moderate | LoadTime | Any |
| QLT0004 | Quality: Async Upload Buffer Size Left at Default | Moderate | LoadTime | Any |
| QLT0005 | Quality: Texture Streaming Disabled on Quality Level | Moderate | Memory | Any |
| QLT0006 | Quality: Anisotropic Texture Filtering Forced On | Moderate | Performance, Quality | Android, iOS, Switch |

---

## PHY — Physics & time settings

| ID | Title | Severity | Areas | Platforms |
|----|-------|----------|-------|-----------|
| PHY0001 | Physics: Auto Sync Transforms Enabled | Moderate | Performance | Any |
| PHY0002 | Physics: All Layer Collision Pairs Enabled | Moderate | Performance | Any |
| PHY0003 | Physics 2D: Auto Sync Transforms Enabled | Moderate | Performance | Any |
| PHY0004 | Physics 2D: All Layer Collision Pairs Enabled | Moderate | Performance | Any |
| PHY0005 | Physics: Reuse Collision Callbacks Disabled | Moderate | Memory | Any |
| PHY0006 | Physics 2D: Simulation Mode Drives Automatic Updates | Moderate | Performance | Any |
| PHY0007 | Time: Fixed Timestep Is the Default Value | Moderate | Performance | Any |
| PHY0008 | Time: Maximum Allowed Timestep Is the Default Value | Moderate | Performance | Any |

---

## EDT — Editor settings

| ID | Title | Severity | Areas | Platforms |
|----|-------|----------|-------|-----------|
| EDT0001 | Editor: Enter Play Mode Options Disabled | Moderate | EditorWorkflow | Any |
| EDT0002 | Editor: Reload Domain Enabled | Moderate | EditorWorkflow | Any |
| EDT0003 | Editor: Optional Package .csproj Generation Enabled | Moderate | EditorWorkflow | Any |
| EDT0004 | Editor: Texture Compression Override Not Set | Moderate | EditorWorkflow | 2021.3+ |

---

## IMP — Importer defaults

| ID | Title | Severity | Areas | Platforms |
|----|-------|----------|-------|-----------|
| IMP0000 | Default Presets: Importer Has No Enabled Default Preset | Moderate | Quality | Any |
| IMP0001 | Default Presets: Importer Default Preset Has No Asset Assigned | Moderate | Quality | Any |

---

## PKG — Packages

| ID | Title | Severity | Areas | Platforms |
|----|-------|----------|-------|-----------|
| PKG0001 | Package: Newer Recommended Version Available | Minor | Quality | Any |
| PKG0002 | Package: Preview or Experimental Version | Moderate | Quality | Any |
| PKG0003 | Package: Deprecated | Moderate | Quality | Any |

---

## TEX — Texture asset imports

| ID | Title | Severity | Areas | Platforms |
|----|-------|----------|-------|-----------|
| TEX0000 | Texture: Mipmaps Not Enabled | Moderate | Performance, Quality | Any |
| TEX0001 | Texture: Mipmaps Enabled on Sprite or UI Texture | Moderate | BuildSize, Quality | Any |
| TEX0002 | Texture: Read/Write Enabled | Moderate | Memory | Any |
| TEX0003 | Texture: Streaming Mipmaps Not Enabled | Moderate | Memory, Quality | Any |
| TEX0004 | Texture: Anisotropic Level Above 1 | Moderate | Performance, Quality | Android, iOS, Switch |
| TEX0005 | Texture: Solid Color Above 1x1 Size | Moderate | Memory | Any |
| TEX0006 | Normal Map: sRGB Enabled | Critical | Quality | Any |
| TEX0007 | Texture: High Empty-Space Ratio | Moderate | Memory | Any |
| TEX0009 | Texture: Alpha-Capable Format on Source Without Alpha | Moderate | Memory | Any |
| TEX0010 | Texture: Source Has Empty Alpha Channel | Moderate | Memory | Any |
| TEX0019 | Cursor Texture: Invalid Format | Moderate | Correctness | Any |
| TEX0020 | Cursor Texture: Read/Write Disabled | Moderate | Correctness | Any |
| TEX0021 | Cursor Texture: Alpha Is Transparency Disabled | Moderate | Quality | Any |
| TEX0022 | Cursor Texture: Mipmaps Enabled | Moderate | BuildSize | Any |
| TEX0023 | Normal Map: Mipmaps Not Enabled | Major | Quality, Performance | Any |
| TEX0024 | Normal Map: Point Filtering | Moderate | Quality | Any |
| TEX0025 | Normal Map: Inappropriate Compression Format | Moderate | Quality, Memory | Any |
| TEX0026 | Normal Map: Default-Value Texture | Moderate | Memory, Quality | Any |
| TEX0027 | Texture: Used as Normal Map With Wrong Import Type | Major | Quality | Any |
| TEX0028 | Texture: Used as Normal Map With sRGB Enabled | Critical | Quality | Any |
| TEX0029 | Texture: Used as Color Map Without sRGB | Major | Quality | Any |
| TEX0030 | Texture: Used as Data Map With sRGB Enabled | Major | Quality | Any |

---

## MSH — Mesh asset imports

| ID | Title | Severity | Areas | Platforms |
|----|-------|----------|-------|-----------|
| MSH0000 | Mesh: Read/Write Enabled | Moderate | Memory | Any |
| MSH0001 | Mesh: 32-bit Index Format on a Sub-65k Mesh | Moderate | Memory | Any |
| MSH0002 | Mesh: Strict Vertex Data Checks Enabled | Moderate | EditorWorkflow | Any |

---

## MDL — Model asset imports

| ID | Title | Severity | Areas | Platforms |
|----|-------|----------|-------|-----------|
| MDL0000 | Model: External Object Reference Unresolved | Moderate | Correctness, EditorWorkflow | Any |
| MDL0001 | Model: Legacy Material Import Mode | Minor | Correctness, EditorWorkflow | Any |
| MDL0002 | Model: Orphaned External Object Entry | Minor | EditorWorkflow | Any |

---

## SHD — Shader source & variants

| ID | Title | Severity | Areas | Platforms |
|----|-------|----------|-------|-----------|
| SHD0000 | Shader: Not Compatible With SRP Batcher | Moderate | Performance | Any |
| SHD0003 | Shader: Fallback Shader Not Found | Moderate | Correctness | Any |
| SHD0004 | Shader: Debug Symbols Pragma | Moderate | BuildSize | Any |
| SHD0005 | Shader: multi_compile Inflates Variant Count | Moderate | BuildSize | Any |
| SHD0006 | Shader: Global Keyword Space Consumed | Moderate | Memory | Any |
| SHD0007 | Shader: GPU Instancing Not Supported | Moderate | Performance | Any |
| SHD0008 | Shader: shader_feature Keyword Toggled at Runtime | Moderate | Correctness, BuildSize | Any |
| SHD0009 | Shader: Deferred Pass Compiled in Forward-Only URP Project | Moderate | BuildSize | Any |
| SHD0010 | Shader: shader_feature Axes Over-Broad for Material Usage | Info | Quality | Any |
| SHD0011 | Shader: Misspelled Tag Key | Major | Correctness | Any |
| SHD0012 | Shader: Unrecognized Tag Key | Moderate | Correctness | Any |

---

## MAT — Material configuration

| ID | Title | Severity | Areas | Platforms |
|----|-------|----------|-------|-----------|
| MAT0001 | Material: GPU Instancing Not Enabled | Minor | Performance | Any |
| MAT0004 | Material: Stale Shader Property | Minor | Correctness | Any |
| MAT0005 | Material: Stale Shader Keyword | Minor | Correctness, BuildSize | Any |

---

## ACL — AudioClip imports

| ID | Title | Severity | Areas | Platforms |
|----|-------|----------|-------|-----------|
| ACL0000 | Audio: Long Clip Not Streaming | Moderate | Memory | Any |
| ACL0001 | Audio: Short Clip Set to Streaming | Moderate | Memory | Any |
| ACL0002 | Audio: Stereo Clip on Mobile | Moderate | Memory | Android, iOS |
| ACL0003 | Audio: Stereo Clip Used for Positional Audio | Moderate | Memory, Quality | Any (except Android, iOS) |
| ACL0004 | Audio: Long Clip Decompresses on Load | Moderate | Memory, LoadTime | Any |
| ACL0005 | Audio: Compressed In Memory With Expensive Codec | Moderate | Performance | Any |
| ACL0006 | Audio: High-Quality Compressed Clip on Mobile | Moderate | Memory, BuildSize | Android, iOS |
| ACL0007 | Audio: Source Sample Rate Above 48 kHz | Moderate | Memory, BuildSize, LoadTime | Any |
| ACL0008 | Audio: Preload Audio Data Enabled | Moderate | LoadTime | Any |
| ACL0009 | Audio: Large Clip Loads on Main Thread | Moderate | Performance, LoadTime | Any |
| ACL0010 | Audio: MP3 Compression Format | Moderate | Quality | Any |
| ACL0011 | Audio: Lossy Source Asset Format | Moderate | Quality | Any |

---

## SPR — Sprite & SpriteAtlas

| ID | Title | Severity | Areas | Platforms |
|----|-------|----------|-------|-----------|
| SPR0001 | Sprite Atlas: High Empty-Space Ratio | Moderate | Memory | Any |

---

## ANI — Animation, Animator, Avatar, AvatarMask

*No descriptors registered yet.*

---

## ADR — Addressables

| ID | Title | Severity | Areas | Platforms |
|----|-------|----------|-------|-----------|
| ADR0001 | Addressable: Missing Asset | Major | Correctness | Any |
| ADR0002 | Addressable: Long Asset Address | Moderate | Memory, BuildSize | Any |
| ADR0003 | Addressable Group: Empty | Minor | Correctness | Any |
| ADR0004 | Addressable: Duplicate Asset | Moderate | Correctness | Any |
| ADR0005 | Addressable: Duplicate Address | Moderate | Correctness | Any |
| ADR0006 | Addressable Group: No Compression | Moderate | BuildSize | Any |
| ADR0007 | Addressable: Asset in Default Group | Moderate | BuildSize, LoadTime | Any |
| ADR0008 | Addressable Group: All Catalog Lookup Keys Included | Minor | BuildSize, Memory | Any |
| ADR0009 | Addressables: ScriptableObject AssetReference Not In Any Group | Moderate | Correctness | Any |
| ADR0010 | Addressables: Unassigned AssetReference | Moderate | Correctness | Any |
| ADR0011 | Addressables: Unspecialized AssetReference | Minor | Quality | Any |
| ADR0012 | Addressables: AssetReference Not In Any Group | Moderate | Correctness | Any |
| ADR0013 | Addressables: WaitForCompletion Blocks the Calling Thread | Moderate | Performance | Any |

---

## RES — Resources & StreamingAssets

| ID | Title | Severity | Areas | Platforms |
|----|-------|----------|-------|-----------|
| RES0001 | Build: StreamingAssets Folder Above Size Limit | Moderate | BuildSize | Android, iOS |
| RES0002 | Resources Folder: Asset Direct Reference | Moderate | BuildSize | Any |
| RES0003 | Resources Folder: Asset Dependency | Moderate | BuildSize | Any |
| RES0004 | Resources: LoadAll Loads All Matching Assets | Moderate | Memory | Any |
| RES0005 | Resources: Resources.Load Usage | Moderate | BuildSize, Memory, LoadTime, Quality | Any |

---

## ASF — Asset file integrity

| ID | Title | Severity | Areas | Platforms |
|----|-------|----------|-------|-----------|
| ASF0001 | ScriptableObject: Name Mismatch With Filename | Minor | Correctness | Any |
| ASF0002 | Asset: Orphaned Serialized Field | Minor | Correctness | Any |
| ASF0003 | Asset: Malformed Unity YAML | Major | Correctness | Any |
| ASF0004 | Asset: Duplicate Unity GUID | Major | Correctness | Any |
| ASF0005 | Asset: Case-Only Filename Collision | Major | Correctness | Non-Windows Editor |

---

## CLN — Asset hygiene

| ID | Title | Severity | Areas | Platforms |
|----|-------|----------|-------|-----------|
| CLN0001 | Asset: Possibly Unused | Info | BuildSize | Any |
| CLN0002 | Asset: PSD Source File | Moderate | EditorWorkflow | Any |
| CLN0003 | Asset: Duplicate Source File | Minor | BuildSize, EditorWorkflow | Any |

---

## ASM — Assembly definitions

| ID | Title | Severity | Areas | Platforms |
|----|-------|----------|-------|-----------|
| ASM0001 | Assembly Definition: Test References Without Test-Assembly Flag | Moderate | Correctness, BuildSize | Any |

---

## OBJ — Component findings on GameObjects

| ID | Title | Severity | Areas | Platforms |
|----|-------|----------|-------|-----------|
| OBJ0001 | Camera: Stop NaNs Enabled | Moderate | Performance | Any |
| OBJ0004 | LOD Group: No Renderers Assigned | Moderate | Quality | Any |
| OBJ0005 | Mesh: High Triangle Count Without LOD Group | Moderate | Performance | Any |
| OBJ0006 | MeshCollider: High Triangle Count | Moderate | Performance | Any |
| OBJ0007 | LOD Group: Insufficient Triangle Reduction | Moderate | Performance | Any |
| OBJ0009 | Renderer: No Materials Assigned | Moderate | Quality | Any |
| OBJ0010 | Renderer: Material Slot Unassigned | Moderate | Quality | Any |
| OBJ0011 | URP: Disc Area Light Unsupported | Moderate | Compatibility, Quality | Any |
| OBJ0012 | LOD Group: Fewer Than Recommended LOD Levels | Moderate | Quality | Any |
| OBJ0013 | LOD Group: Null Renderer in Non-Lowest LOD | Moderate | Quality | Any |
| OBJ0014 | LOD Group: Missing for LOD-Named Children | Moderate | Quality | Any |
| OBJ0015 | LOD Group: Zero Culling Screen Percentage | Moderate | Performance | Any |
| OBJ0017 | Light: Zero Intensity | Moderate | Quality | Any |
| OBJ0018 | GameObject: Missing Component | Major | Correctness | Any |
| OBJ0020 | LOD Group: Shadow Casting Increases With Distance | Moderate | Quality | Any |
| OBJ0021 | GameObject: Undefined Layer | Major | Correctness | Any |

---

## SCN — Scene-level structural findings

| ID | Title | Severity | Areas | Platforms |
|----|-------|----------|-------|-----------|
| SCN0001 | Scene: Deep Hierarchy | Moderate | Performance | Any |
| SCN0002 | Scene: GameObject Count Above Threshold | Moderate | Performance | Any |

---

## SCS — Scene-level settings findings

| ID | Title | Severity | Areas | Platforms |
|----|-------|----------|-------|-----------|
| SCS0000 | RenderSettings: Skybox Material Missing | Moderate | Quality | Any |
| SCS0001 | RenderSettings: Fog Enabled With Degenerate Configuration | Moderate | Performance | Any |
| SCS0002 | RenderSettings: Ambient Intensity Unusually High | Moderate | Quality | Any |
| SCS0003 | RenderSettings: Sun Source Unset | Moderate | Quality | Any |
