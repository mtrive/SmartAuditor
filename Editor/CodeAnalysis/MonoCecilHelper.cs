using System.Collections.Generic;
using Mono.Cecil;

namespace SmartAuditor.Editor.CodeAnalysis
{
    internal static class MonoCecilHelper
    {
        // PDB sequence points that do not map to a source line use this sentinel (0x00feefee).
        internal const int HiddenLine = 16707566;

        /// <summary>
        /// Flattens top-level and nested type definitions from a Cecil module type list.
        /// </summary>
        public static IEnumerable<TypeDefinition> AggregateAllTypeDefinitions(IEnumerable<TypeDefinition> types)
        {
            var typeDefs = new List<TypeDefinition>();
            foreach (var typeDefinition in types)
                AppendTypeAndNested(typeDefinition, typeDefs);
            return typeDefs;
        }

        /// <summary>
        /// Returns whether <paramref name="typeReference"/> is, or derives from, the type identified by <paramref name="typeName"/>.
        /// </summary>
        public static bool IsOrInheritedFrom(TypeReference typeReference, string typeName)
        {
            if (typeReference == null || string.IsNullOrEmpty(typeName))
                return false;

            try
            {
                var typeDefinition = typeReference.Resolve();
                while (typeDefinition != null)
                {
                    if (typeDefinition.FullName == typeName)
                        return true;

                    if (typeDefinition.BaseType == null)
                        return false;

                    typeDefinition = typeDefinition.BaseType.Resolve();
                }
            }
            catch (AssemblyResolutionException)
            {
                return false;
            }

            return false;
        }

        /// <summary>
        /// Returns whether <paramref name="line"/> is a real source line rather than a hidden PDB sentinel.
        /// </summary>
        internal static bool IsValidSourceLine(int line) => line > 0 && line < HiddenLine;

        static void AppendTypeAndNested(TypeDefinition typeDefinition, List<TypeDefinition> sink)
        {
            sink.Add(typeDefinition);
            if (!typeDefinition.HasNestedTypes)
                return;

            foreach (var nestedType in typeDefinition.NestedTypes)
                AppendTypeAndNested(nestedType, sink);
        }
    }
}
