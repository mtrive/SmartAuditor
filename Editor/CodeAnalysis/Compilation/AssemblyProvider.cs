using System;
using System.Collections.Generic;
using System.Threading;

namespace SmartAuditor.Editor.CodeAnalysis.Compilation
{
    internal interface IAssemblyProvider : IDisposable
    {
        IReadOnlyList<CompiledAssemblyInfo> GetAssemblies(IProgress progress = null, CancellationToken cancellationToken = default);
    }
}
