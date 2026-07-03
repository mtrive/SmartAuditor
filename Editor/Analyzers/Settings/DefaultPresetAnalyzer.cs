using System;
using System.Collections.Generic;
using SmartAuditor.Editor.Core;
using UnityEditor;
using UnityEditor.Presets;

namespace SmartAuditor.Editor.Analyzers
{
    sealed class DefaultPresetAnalyzer : SettingsAnalyzer
    {
        internal const string IMP0000 = nameof(IMP0000);
        internal const string IMP0001 = nameof(IMP0001);
        internal const string ImporterTypeEvidenceKey = "Importer type";
        internal const string PresetFilterEvidenceKey = "Preset filter";

        static readonly Descriptor MissingImporterDefaultPresetDescriptor = new Descriptor(
            IMP0000,
            "Default Presets: Importer Has No Enabled Default Preset",
            Impact.Quality,
            "The project has no enabled default <b>Preset</b> configured for one or more importer types (Audio, Texture, Model, VideoClip). New assets handled by those importers cannot match a project default Preset, so Unity falls back to its hardcoded import defaults.",
            "Open <b>Project Settings &gt; Preset Manager</b>, add a default Preset for each reported importer type, assign a Preset asset, and enable it."
        )
        {
            MessageFormat = "Importer type '{0}' has no enabled default Preset configured"
        };

        static readonly Descriptor UnassignedDefaultPresetDescriptor = new Descriptor(
            IMP0001,
            "Default Presets: Importer Default Preset Has No Asset Assigned",
            Impact.Quality,
            "An importer has an enabled default <b>Preset</b> configured in the Preset Manager, but no Preset asset is assigned. New assets matching that importer and filter fall back to Unity's hardcoded defaults instead of the intended Preset.",
            "Open <b>Project Settings &gt; Preset Manager</b>, assign a Preset asset to the reported importer and filter, or remove that default Preset if it isn't needed."
        )
        {
            MessageFormat = "Importer '{0}' has an enabled default Preset without a Preset asset assigned (filter '{1}')"
        };

        // Using actual importer types for strong typing
        static readonly string[] s_AuditedImporterTypes = new[]
        {
            typeof(AudioImporter).ToString(),
            typeof(ModelImporter).ToString(),
            typeof(TextureImporter).ToString(),
            typeof(VideoClipImporter).ToString(),
        };

        static readonly string PresetManagerLocation = "Project/Preset Manager";

        internal sealed class DefaultPresetRow
        {
            internal DefaultPresetRow(string importerTypeName, string filter, bool enabled, bool hasPresetAsset)
            {
                ImporterTypeName = importerTypeName;
                Filter = filter;
                Enabled = enabled;
                HasPresetAsset = hasPresetAsset;
            }

            internal string ImporterTypeName { get; }
            internal string Filter { get; }
            internal bool Enabled { get; }
            internal bool HasPresetAsset { get; }
        }

        internal sealed class DefaultPresetIssue
        {
            internal DefaultPresetIssue(string descriptorId, string importerTypeName, string filter)
            {
                DescriptorId = descriptorId;
                ImporterTypeName = importerTypeName;
                Filter = filter;
            }

            internal string DescriptorId { get; }
            internal string ImporterTypeName { get; }
            internal string Filter { get; }
        }

        public override void Analyze(SettingsAnalysisContext context)
        {
            var checkUnassigned = context.IsDescriptorEnabled(UnassignedDefaultPresetDescriptor);
            var checkMissing = context.IsDescriptorEnabled(MissingImporterDefaultPresetDescriptor);
            if (!checkUnassigned && !checkMissing)
                return;

            var rows = new List<DefaultPresetRow>();

            foreach (var type in Preset.GetAllDefaultTypes())
            {
                var importerTypeName = type.GetManagedTypeName();
                var defaultPresets = Preset.GetDefaultPresetsForType(type);

                foreach (var preset in defaultPresets)
                {
                    rows.Add(new DefaultPresetRow(
                        importerTypeName,
                        preset.filter,
                        preset.enabled,
                        preset.preset != null));
                }
            }

            foreach (var issue in InspectDefaultPresetRows(
                rows,
                s_AuditedImporterTypes,
                checkUnassigned,
                checkMissing))
            {
                if (issue.DescriptorId == UnassignedDefaultPresetDescriptor.Id)
                {
                    var diagnostic = Diagnostic.Create(
                        AnalysisCategory.ProjectSetting,
                        UnassignedDefaultPresetDescriptor.Id,
                        issue.ImporterTypeName,
                        issue.Filter)
                        .WithLocation(new Location(PresetManagerLocation))
                        .WithEvidence(ImporterTypeEvidenceKey, issue.ImporterTypeName)
                        .WithEvidence(PresetFilterEvidenceKey, issue.Filter);
                    context.ReportIssue(diagnostic);
                }
                else
                {
                    var diagnostic = Diagnostic.Create(
                        AnalysisCategory.ProjectSetting,
                        MissingImporterDefaultPresetDescriptor.Id,
                        issue.ImporterTypeName)
                        .WithLocation(new Location(PresetManagerLocation))
                        .WithEvidence(ImporterTypeEvidenceKey, issue.ImporterTypeName);
                    context.ReportIssue(diagnostic);
                }
            }
        }

        internal static List<DefaultPresetIssue> InspectDefaultPresetRows(
            IEnumerable<DefaultPresetRow> rows,
            IEnumerable<string> auditedImporterTypes,
            bool checkUnassigned,
            bool checkMissing)
        {
            var issues = new List<DefaultPresetIssue>();
            var typesWithEnabledDefaultRow = new HashSet<string>(StringComparer.Ordinal);

            foreach (var row in rows)
            {
                if (row == null || string.IsNullOrEmpty(row.ImporterTypeName) || !row.Enabled)
                    continue;

                typesWithEnabledDefaultRow.Add(row.ImporterTypeName);

                if (checkUnassigned && !row.HasPresetAsset)
                {
                    issues.Add(new DefaultPresetIssue(
                        UnassignedDefaultPresetDescriptor.Id,
                        row.ImporterTypeName,
                        row.Filter));
                }
            }

            if (!checkMissing)
                return issues;

            foreach (var importerType in auditedImporterTypes)
            {
                if (typesWithEnabledDefaultRow.Contains(importerType))
                    continue;

                issues.Add(new DefaultPresetIssue(
                    MissingImporterDefaultPresetDescriptor.Id,
                    importerType,
                    null));
            }

            return issues;
        }
    }
}
