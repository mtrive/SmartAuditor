# Vendored Roslyn (Microsoft.CodeAnalysis)

These DLLs are the netstandard2.0 build of Microsoft.CodeAnalysis 4.8.0 from
NuGet. They are referenced from `SmartAuditor.Editor.asmdef`'s
`precompiledReferences` and used by `RoslynAssemblyCompilation` to compile
project assemblies.

## Why vendored

Unity's package manager doesn't natively integrate with NuGet, and the
Roslyn shipped inside the Unity editor (under `MonoBleedingEdge/.../Roslyn/`)
is too old to support C# 10/11/12 (Roslyn 3.7 era). Vendoring a known-good
version mirrors how the package already vendors `Mono.Cecil.dll` and
`Newtonsoft.Json.dll`.

## Why netstandard2.0

Mono (Unity's editor runtime) loads netstandard2.0 assemblies natively. The
NuGet packages also contain `lib/net6.0/` and `lib/net7.0/` builds; those
target a newer .NET runtime that Mono cannot load (`BadImageFormatException`).

## Why these four DLLs and no polyfills

`Microsoft.CodeAnalysis.dll` and `Microsoft.CodeAnalysis.CSharp.dll` are the
compiler. `System.Collections.Immutable.dll` and `System.Reflection.Metadata.dll`
are direct dependencies that Mono's BCL doesn't include. Other transitive
dependencies (`System.Memory`, `System.Buffers`, `System.Runtime.CompilerServices.Unsafe`,
`System.Numerics.Vectors`, `System.Threading.Tasks.Extensions`, `System.Text.Encoding.CodePages`)
are intentionally **not** vendored: Mono's BCL provides their types directly,
and shipping the polyfill DLLs would cause CS0433 type-ambiguity errors in
any assembly that pulls in both the polyfill and Mono's BCL.

## Upgrade procedure

```powershell
# Pick a target Roslyn version (e.g. 4.8.0).
$version = "4.8.0"
$dest = "<repo>\apps\game\FM Unity\Packages\SmartAuditor\Editor\Plugins\Roslyn"
$tmp = "$env:TEMP\smart-auditor-roslyn-upgrade"
Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $tmp | Out-Null

$packages = @(
    @{ Id = "Microsoft.CodeAnalysis.Common"; Version = $version; Dll = "Microsoft.CodeAnalysis.dll" },
    @{ Id = "Microsoft.CodeAnalysis.CSharp"; Version = $version; Dll = "Microsoft.CodeAnalysis.CSharp.dll" },
    @{ Id = "System.Collections.Immutable"; Version = "7.0.0";   Dll = "System.Collections.Immutable.dll" },
    @{ Id = "System.Reflection.Metadata";  Version = "7.0.0";   Dll = "System.Reflection.Metadata.dll" }
)

foreach ($p in $packages) {
    $idLower = $p.Id.ToLower()
    $url = "https://api.nuget.org/v3-flatcontainer/$idLower/$($p.Version)/$idLower.$($p.Version).nupkg"
    $nupkg = "$tmp\$($p.Id).$($p.Version).zip"
    Invoke-WebRequest -Uri $url -OutFile $nupkg -UseBasicParsing
    Expand-Archive -Path $nupkg -DestinationPath "$tmp\$($p.Id)" -Force
    Copy-Item "$tmp\$($p.Id)\lib\netstandard2.0\$($p.Dll)" $dest -Force
}
```

Then in Unity, re-import the DLLs (they should keep their existing GUIDs in
the .meta files; just verify the `validateReferences: 0` and Editor-only
platform settings are intact).

## Tests

`Tests/Editor/Roslyn/RoslynPdbReadbackTests.cs` is the gate that proves the
vendored Roslyn (a) loads, (b) compiles, (c) emits a portable PDB Mono.Cecil
can read. Run that test after any Roslyn upgrade.
