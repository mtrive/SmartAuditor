using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SmartAuditor.Editor.Core
{
    [Serializable]
    class DescriptorLibrary : ISerializationCallbackReceiver
    {
        static Dictionary<string, Descriptor> s_Descriptors;
        static bool s_Initialized;

        static Dictionary<Impact, string> s_ImpactStrings;

        [SerializeField]
        internal List<Descriptor> m_SerializedDescriptors;

        /// <summary>
        /// Initializes the descriptor library by discovering all analyzers and registering their supported descriptors.
        /// This method is idempotent - calling it multiple times has no effect after the first call.
        /// </summary>
        public static void InitializeFromAnalyzers()
        {
            if (s_Initialized)
                return;

            if (s_Descriptors == null)
                s_Descriptors = new Dictionary<string, Descriptor>();

            // Discover all DiagnosticAnalyzer types
            var analyzerTypes = TypeCache.GetTypesDerivedFrom(typeof(DiagnosticAnalyzer));

            foreach (var type in analyzerTypes)
            {
                if (type.IsAbstract)
                    continue;

                try
                {
                    var analyzer = (DiagnosticAnalyzer)Activator.CreateInstance(type);
                    var descriptors = analyzer.SupportedDescriptors;

                    foreach (var descriptor in descriptors)
                    {
                        if (descriptor == null)
                            continue;

                        // Filter by platform and version compatibility
                        if (!descriptor.IsPlatformSupported())
                            continue;
                        if (!descriptor.IsVersionCompatible())
                            continue;

                        if (!s_Descriptors.ContainsKey(descriptor.Id))
                        {
                            s_Descriptors[descriptor.Id] = descriptor;
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"[{SmartAuditor.CompactName}] Failed to get descriptors from analyzer {type.Name}: {e.Message}");
                }
            }

            s_Initialized = true;
        }

        /// <summary>
        /// Gets all registered descriptor IDs.
        /// </summary>
        /// <returns>An array of all descriptor IDs.</returns>
        public static string[] GetAllIds()
        {
            if (s_Descriptors == null)
                return Array.Empty<string>();
            return s_Descriptors.Keys.ToArray();
        }

        /// <summary>
        /// Registers a descriptor with the library.
        /// </summary>
        /// <param name="id">The descriptor ID.</param>
        /// <param name="descriptor">The descriptor to register.</param>
        /// <returns>True if a descriptor with the same ID was already registered.</returns>
        public static bool RegisterDescriptor(string id, Descriptor descriptor)
        {
            if (s_Descriptors == null)
                s_Descriptors = new Dictionary<string, Descriptor>();

            var alreadyFound = s_Descriptors.ContainsKey(id);
            // Only warn if this is a genuine duplicate (different descriptor object)
            if (alreadyFound && !ReferenceEquals(s_Descriptors[id], descriptor))
            {
                var existingDescriptor = s_Descriptors[id];
                Debug.LogWarning($"[{SmartAuditor.CompactName}] Descriptor ID clash detected: '{id}' is already registered. " +
                    $"Existing: '{existingDescriptor.Title}', New: '{descriptor.Title}'. " +
                    $"The new descriptor will overwrite the existing one.");
            }

            s_Descriptors[id] = descriptor;
            return alreadyFound;
        }

        public static Descriptor GetDescriptor(string id)
        {
            return s_Descriptors[id];
        }

        public static bool TryGetDescriptor(string id, out Descriptor descriptor)
        {
            descriptor = default;
            if (s_Descriptors == null || string.IsNullOrEmpty(id))
                return false;
            return s_Descriptors.TryGetValue(id, out descriptor);
        }

        /// <summary>
        /// Returns a list of descriptors, optionally filtered to only those matching the supplied IDs.
        /// </summary>
        /// <param name="ids">
        /// When <c>null</c>, all registered descriptors are returned.
        /// Otherwise only descriptors whose ID appears in this sequence are returned.
        /// </param>
        internal static List<Descriptor> GetDescriptors(IEnumerable<string> ids = null)
        {
            if (s_Descriptors == null)
                return new List<Descriptor>();

            if (ids == null)
                return s_Descriptors.Values.ToList();

            return ids
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .Where(id => s_Descriptors.ContainsKey(id))
                .Select(id => s_Descriptors[id])
                .ToList();
        }

        public static string GetImpactString(Impact impact)
        {
            if (s_ImpactStrings == null)
                s_ImpactStrings = new Dictionary<Impact, string>();

            if (s_ImpactStrings.TryGetValue(impact, out string desc))
                return desc;

            desc = impact.ToString();
            s_ImpactStrings[impact] = desc;
            return desc;
        }

        public void OnBeforeSerialize()
        {
            // update list from dictionary

            // TODO: Serialization is needed to survive domain reload, and when writing a Report out to file.
            // In both cases the list only really needs to contain the Descriptors that correspond to ProjectIssues
            // actually found in the report, so if we had the report object we could potentially do some filtering here.
            m_SerializedDescriptors = s_Descriptors.Values.ToList();
        }

        public void OnAfterDeserialize()
        {
            // update dictionary from list

            // TODO: _Hypothetically_, if we're here after loading an old report from JSON, and if we're in a newer
            // version of the tool with updated descriptors, we might want to keep those descriptors in the library
            // rather than overwrite them with the ones that were saved alongside the issues. Right now it's such an
            // edge case that it doesn't really seem worth spending time on.
            if (m_SerializedDescriptors != null)
            {
                s_Descriptors = m_SerializedDescriptors.ToDictionary(m => m.Id, m => m);
                m_SerializedDescriptors = null;
            }
        }

        // For testing purposes only
        internal static void Reset()
        {
            s_Descriptors?.Clear();
            s_Initialized = false;
        }
    }
}
