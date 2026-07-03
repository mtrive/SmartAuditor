<a name="API"></a>
# Scripting API Overview

Smart Auditor is a Unity Editor tool and exposes a C# API for running static analysis from Editor scripts, tests,
CI jobs, and coding-agent integrations.

The main entry points are:

* `SmartAuditor.Analyze(options)` for synchronous analysis.
* `SmartAuditor.AnalyzeAsync(options, progress, cancellationToken)` for asynchronous analysis.
* `CommandLine.Run` for Unity `-executeMethod` command-line runs.

## Run analysis from C#

The simplest API call runs a full analysis for the active build target and returns a `Report`.

```csharp
using SmartAuditor.Editor;
using UnityEngine;

public static class SmartAuditorExample
{
    public static void AnalyzeProject()
    {
        var report = SmartAuditor.Analyze();
        Debug.Log($"Smart Auditor found {report.Issues.Length} issues");

        report.Save("smart-auditor-report.json", prettyPrint: true);
    }
}
```

To configure analysis, create an `AnalysisOptions` object.

```csharp
using SmartAuditor.Editor;
using UnityEditor;
using UnityEngine;

public static class SmartAuditorExample
{
    public static void AnalyzeCodeForAndroid()
    {
        var options = new AnalysisOptions
        {
            Categories = new[] { AnalysisCategory.Code },
            IncludedAssemblies = new[] { "Assembly-CSharp" },
            Platform = BuildTarget.Android,
            CodeContext = CodeContext.All,
            OnIncomingIssues = issues =>
            {
                foreach (var issue in issues)
                    Debug.Log($"{issue.Id}: {issue.Description}");
            }
        };

        var report = SmartAuditor.Analyze(options);
        report.Save("smart-auditor-code-report.json", prettyPrint: true);
    }
}
```

The `Report` object can be saved as JSON or examined directly. It contains session metadata, issues, messages,
insights, module metadata, and descriptors. Each `ReportItem` represents one row shown in the Smart Auditor UI.
Issues have a valid `Id`; insights do not. JSON exports put compiler, shader compiler, and asset import diagnostics
in a top-level `messages` array. Issues and messages include stable `Fingerprint` and `FingerprintStability` values
so agents and CI jobs can diff reports across iterations without relying on line numbers alone.

```csharp
foreach (var issue in report.Issues)
{
    Debug.Log($"{issue.Category}: {issue.Id} {issue.Fingerprint} at {issue.RelativePath}");
}
```

## Run analysis from command line

Smart Auditor includes a package-owned command-line bridge for tools that need to trigger analysis from outside the
Unity Editor UI. Full command examples, argument reference, defaults, targeted scope examples, and CI exit-code
behavior are documented in [Command-Line Analysis](CommandLine.md).

## Agent integrations

Codex, Claude Code, CI systems, and MCP servers can all use the same integration pattern:

1. Launch Unity in batch mode with `CommandLine.Run`.
2. Wait for the process to finish.
3. Read the JSON report from `-smartAuditorReportPath`.
4. Summarize, cluster, or act on the `issues`, `messages`, `insights`, `descriptors`, and `sessionInfo` fields.

For example, an agent wrapper can expose a tool named `run_smart_auditor` that internally runs the Unity command and
returns the report path plus the process exit code. The agent should treat Smart Auditor findings as the source of truth,
then use its own reasoning to prioritize issues, connect related findings, and propose remediation plans.

For more details on this workflow, see [Agentic Analysis](AgenticAnalysis.md).
