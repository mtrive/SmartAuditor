# Installing Smart Auditor

The package can be installed directly from this Git repository. The instructions that follow are specific to Smart Auditor, however, more information on how a package can be added to a project as a dependency from Git, you can check the Package Manager [documentation](https://docs.unity3d.com/Manual/upm-git.html).

### Package Manager UI (Recommended)
The easiest way to install Smart Auditor is via Package Manager with the following steps:

1. On the repository page, click **Code** and copy the **HTTPS** URL to the clipboard.
2. In Unity's Package Manager (**Window > Package Manager**), click the **+** button (top-left) and select **Add package from git URL**.
3. Paste the URL and click **Add**.

To install a specific version, append `#<version>` to the URL. For example:

```https://github.com/mtrive/SmartAuditor.git#0.1.0```

A list of releases can be found [here](https://github.com/mtrive/SmartAuditor/releases).

## Upgrade to a newer version
Under the hood, the method described above adds `com.mtrive.smart-auditor` as a dependency in the project `Packages/manifest.json` file. To upgrade to a new Smart Auditor version, you can simply modify the tag. For example:

```
{
  "dependencies": {
    "com.mtrive.smart-auditor": "https://github.com/mtrive/SmartAuditor.git#0.1.0",
  }
}
```

## Install as a tarball
If you are working in Unity 2020.3 and cannot install the package directly from the Git URL for some reason, another option is to install the package from a tarball. See the Package Manager [documentation](https://docs.unity3d.com/Manual/upm-localpath.html) about tarballs for more information.

## Installation troubleshooting
Under rare and specific circumstances, installing the Smart Auditor package may result in a console error similar to
the following:

```
error CS0433: The type 'MethodAttributes' exists in both 'Mono.Cecil, Version=0.11.4.0, Culture=neutral, PublicKeyToken=50cebf1cceb9d05e'
and 'Unity.Burst.Cecil, Version=0.10.0.0, Culture=neutral, PublicKeyToken=fc15b93552389f74'
```
Smart Auditor uses a library called
[Mono.Cecil](https://www.mono-project.com/docs/tools+libraries/libraries/Mono.Cecil/) in order to perform static
analysis of C# code. Smart Auditor specifies Mono.Cecil as a dependency, meaning that Mono.Cecil is automatically
installed alongside the Smart Auditor package, unless some other package has already installed it as a dependency.
This allows multiple packages that use Mono.Cecil to coexist in a Unity project. However, some older versions of other
Unity packages include Mono.Cecil directly rather than as a dependency. If these package versions are installed in a
project, and if any user code assemblies also make explicit use of Mono.Cecil, namespace clashes can occur. The error
message above was generated from a project which included Burst 1.8.3 and the following code in a user script:

```
using MethodAttributes = Mono.Cecil.MethodAttributes;
```

The solution in this situation is to either update Burst to 1.8.4 or above, or to remove any user code which uses
Mono.Cecil.
