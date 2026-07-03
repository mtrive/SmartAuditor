using System.Collections.Generic;
using Mono.Cecil;

namespace SmartAuditor.Editor.CodeAnalysis
{
    /// <summary>
    /// Cecil metadata resolver that memoizes resolve results for the lifetime of one assembly read.
    /// Subclasses <see cref="MetadataResolver"/> so internal base-type walks reuse cached type resolutions.
    /// </summary>
    sealed class MetadataResolverWithCache : MetadataResolver
    {
        readonly Dictionary<TypeReference, TypeDefinition> m_TypeCache = new Dictionary<TypeReference, TypeDefinition>();
        readonly Dictionary<FieldReference, FieldDefinition> m_FieldCache = new Dictionary<FieldReference, FieldDefinition>();
        readonly Dictionary<MethodReference, MethodDefinition> m_MethodCache = new Dictionary<MethodReference, MethodDefinition>();

        public MetadataResolverWithCache(IAssemblyResolver assemblyResolver)
            : base(assemblyResolver)
        {
        }

        public override TypeDefinition Resolve(TypeReference reference)
        {
            if (m_TypeCache.TryGetValue(reference, out var definition))
                return definition;

            definition = base.Resolve(reference);
            m_TypeCache[reference] = definition;
            return definition;
        }

        public override FieldDefinition Resolve(FieldReference reference)
        {
            if (m_FieldCache.TryGetValue(reference, out var definition))
                return definition;

            definition = base.Resolve(reference);
            m_FieldCache[reference] = definition;
            return definition;
        }

        public override MethodDefinition Resolve(MethodReference reference)
        {
            if (m_MethodCache.TryGetValue(reference, out var definition))
                return definition;

            definition = base.Resolve(reference);
            m_MethodCache[reference] = definition;
            return definition;
        }
    }
}
