// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using UnityEditor;

namespace SmartAuditor.Editor.Core
{
    /// <summary>
    /// Discovers per-category <see cref="InsightSchema"/> instances declared on
    /// <see cref="AnalysisModule"/> implementations. First registered owner wins.
    /// </summary>
    internal static class CategorySchemaRegistry
    {
        /// <summary>Returns the schema for <paramref name="category"/>, or null if none is registered.</summary>
        public static InsightSchema GetSchema(AnalysisCategory category)
        {
            foreach (var type in TypeCache.GetTypesDerivedFrom(typeof(AnalysisModule)))
            {
                if (type.IsAbstract)
                    continue;

                var module = Activator.CreateInstance(type) as AnalysisModule;
                var schemas = module.CategorySchemas;
                if (schemas != null && schemas.TryGetValue(category, out var schema))
                    return schema;
            }

            return null;
        }
    }
}
