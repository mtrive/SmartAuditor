using System.IO;
using Mono.Cecil;
using Mono.Cecil.Cil;
using SmartAuditor.Editor.CodeAnalysis.Compilation;
using SmartAuditor.Editor.Core;

namespace SmartAuditor.Editor.CodeAnalysis
{
    /// <summary>
    /// Maps Mono.Cecil sequence points to Smart Auditor <see cref="Location"/> values for a
    /// compiled assembly.
    /// </summary>
    internal static class CodeLocationResolver
    {
        public static Location ResolveTypeLocation(CompiledAssemblyInfo assemblyInfo, TypeDefinition typeDefinition)
        {
            if (typeDefinition == null)
                return null;

            foreach (var methodDefinition in typeDefinition.Methods)
            {
                var location = ResolveMethodLocation(assemblyInfo, methodDefinition);
                if (location != null)
                    return location;
            }

            return null;
        }

        public static Location ResolveMethodLocation(CompiledAssemblyInfo assemblyInfo, MethodDefinition methodDefinition)
        {
            if (methodDefinition?.DebugInformation == null)
                return null;

            var sequencePoints = methodDefinition.DebugInformation.SequencePoints;
            if (sequencePoints == null)
                return null;

            foreach (var sequencePoint in sequencePoints)
            {
                var location = CreateLocation(assemblyInfo, sequencePoint, includeHidden: false);
                if (location != null)
                    return location;
            }

            return null;
        }

        public static Location CreateLocation(CompiledAssemblyInfo assemblyInfo, SequencePoint sequencePoint, bool includeHidden)
        {
            if (sequencePoint == null || sequencePoint.Document == null || string.IsNullOrEmpty(sequencePoint.Document.Url))
                return null;
            if (!includeHidden && sequencePoint.IsHidden)
                return null;

            var path = AssemblyResolver.ResolveAssetPath(assemblyInfo, sequencePoint.Document.Url);
            if (string.IsNullOrEmpty(path))
                return null;

            return new Location(path,
                sequencePoint.IsHidden ? -1 : sequencePoint.StartLine,
                sequencePoint.IsHidden ? null : (int?)sequencePoint.EndLine,
                sequencePoint.IsHidden ? null : (int?)sequencePoint.StartColumn,
                sequencePoint.IsHidden ? null : (int?)sequencePoint.EndColumn);
        }
    }
}
