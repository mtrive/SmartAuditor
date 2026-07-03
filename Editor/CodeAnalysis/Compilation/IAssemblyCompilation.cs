// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using System.Threading;
using UnityEditor;

namespace SmartAuditor.Editor.CodeAnalysis.Compilation
{
    enum CompilerMessageType
    {
        /// <summary>
        ///   <para>Error message.</para>
        /// </summary>
        Error,
        /// <summary>
        ///   <para>Warning message.</para>
        /// </summary>
        Warning,
        /// <summary>
        ///   <para>Info message.</para>
        /// </summary>
        Info
    }

    struct CompilerMessage
    {
        /// <summary>
        ///   <para>Message code.</para>
        /// </summary>
        public string Code;
        /// <summary>
        ///   <para>Message type.</para>
        /// </summary>
        public CompilerMessageType Type;
        /// <summary>
        ///   <para>Message body.</para>
        /// </summary>
        public string Message;
        /// <summary>
        ///   <para>File for the message.</para>
        /// </summary>
        public string File;
        /// <summary>
        ///   <para>File line for the message.</para>
        /// </summary>
        public int Line;
    }

    enum CompilationStatus
    {
        NotStarted,
        IsCompiling,
        CompilerError,
        MissingDependency,
        Success,
    }

    class AssemblyCompilationResult
    {
        public string AssemblyName;
        public string AssemblyPath;
        public string[] DependentAssemblyNames;
        public DateTime StartTime;
        public DateTime EndTime;
        public long DurationInMs;
        public CompilerMessage[] Messages;
        public CompilationStatus Status;
    }

    // Common surface of the Roslyn assembly-compilation pipeline.
    // PlatformAssemblyProvider works through this interface so the rest of
    // SmartAuditor doesn't care about pipeline internals.
    //
    // Pipeline-specific knobs (e.g. RunDiagnosticAnalyzers on
    // RoslynAssemblyCompilation) are set on the concrete class before being
    // assigned to a variable of this interface type -- they're not part of
    // the contract.
    internal interface IAssemblyCompilation : IDisposable
    {
        string[] TargetAssemblies { get; set; }
        CompilationMode CompilationMode { get; set; }
        BuildTarget Platform { get; set; }
        Action<AssemblyCompilationResult> OnAssemblyCompilationFinished { get; set; }

        IReadOnlyList<string> Compile(IProgress progress = null, CancellationToken cancellationToken = default);
    }
}
