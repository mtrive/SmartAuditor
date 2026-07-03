using System;
using System.Runtime.CompilerServices;

// Exposes Framework internals to the UI assembly and editor tests.
[assembly: InternalsVisibleTo("SmartAuditor.Editor.UI")]
[assembly: InternalsVisibleTo("SmartAuditor.EditorTests")]
