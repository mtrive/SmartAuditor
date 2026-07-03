// Copyright (c) 2026 Marco Trivellato
// Licensed under the MIT License. See LICENSE file in the project root.

using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartAuditor.Editor
{
    /// <summary>
    /// Represents a weighted project health score derived from report issue severities.
    /// Score = 100 - (Critical×8 + Major×4 + Moderate×1), clamped to [0, 100].
    /// </summary>
    public sealed class HealthScore
    {
        /// <summary>The maximum possible score before any penalties are applied.</summary>
        public const int MaxScore = 100;

        /// <summary>The computed health score in the range [0, 100].</summary>
        public int Score { get; }

        /// <summary>Number of critical/error-severity issues included in the score.</summary>
        public int CriticalCount { get; }

        /// <summary>Number of major-severity issues included in the score.</summary>
        public int MajorCount { get; }

        /// <summary>Number of moderate-severity issues included in the score.</summary>
        public int ModerateCount { get; }

        /// <summary>Number of minor-severity issues observed (no penalty applied).</summary>
        public int MinorCount { get; }

        /// <summary>Number of ignored or suppressed issues observed (no penalty applied).</summary>
        public int IgnoredCount { get; }

        /// <summary>Total number of diagnostic issues included in this score.</summary>
        public int TotalIssueCount => CriticalCount + MajorCount + ModerateCount + MinorCount + IgnoredCount;

        /// <summary>Penalty contribution grouped by issue category.</summary>
        public IReadOnlyDictionary<AnalysisCategory, int> CategoryPenalty { get; }

        HealthScore(
            int score,
            int criticalCount,
            int majorCount,
            int moderateCount,
            int minorCount,
            int ignoredCount,
            IReadOnlyDictionary<AnalysisCategory, int> categoryPenalty)
        {
            Score = score;
            CriticalCount = criticalCount;
            MajorCount = majorCount;
            ModerateCount = moderateCount;
            MinorCount = minorCount;
            IgnoredCount = ignoredCount;
            CategoryPenalty = categoryPenalty ?? throw new ArgumentNullException(nameof(categoryPenalty));
        }

        /// <summary>
        /// Computes a health score from a sequence of report items.
        /// Only items with a non-null severity contribute to the score.
        /// </summary>
        /// <param name="issues">The collection of report items to evaluate.</param>
        /// <returns>A computed <see cref="HealthScore"/> snapshot.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="issues"/> is null.</exception>
        public static HealthScore FromIssues(IEnumerable<ReportItem> issues)
        {
            if (issues == null)
                throw new ArgumentNullException(nameof(issues));

            var criticalCount = 0;
            var majorCount = 0;
            var moderateCount = 0;
            var minorCount = 0;
            var ignoredCount = 0;
            var totalPenalty = 0;
            var categoryPenalty = new Dictionary<AnalysisCategory, int>();

            foreach (var issue in issues.Where(i => i != null))
            {
                var penalty = GetPenalty(issue.Severity);
                if (penalty > 0)
                {
                    totalPenalty += penalty;
                    categoryPenalty.TryGetValue(issue.Category, out var existing);
                    categoryPenalty[issue.Category] = existing + penalty;
                }

                switch (issue.Severity)
                {
                    case Severity.Error:
                    case Severity.Critical:
                        criticalCount++;
                        break;
                    case Severity.Major:
                        majorCount++;
                        break;
                    case Severity.Moderate:
                        moderateCount++;
                        break;
                    case Severity.Minor:
                        minorCount++;
                        break;
                    default:
                        ignoredCount++;
                        break;
                }
            }

            var score = Math.Max(0, Math.Min(MaxScore, MaxScore - totalPenalty));
            return new HealthScore(score, criticalCount, majorCount, moderateCount, minorCount, ignoredCount, categoryPenalty);
        }

        /// <summary>
        /// Returns a transparent explanation of the weighted formula and computed result.
        /// </summary>
        public string ExplainCalculation()
        {
            var penalty = CriticalCount * 8 + MajorCount * 4 + ModerateCount * 1;
            return $"Score = 100 - (Critical×8 + Major×4 + Moderate×1) " +
                   $"= 100 - ({CriticalCount}×8 + {MajorCount}×4 + {ModerateCount}×1) = {Score}";
        }

        /// <summary>
        /// Returns a compact label describing the overall health state.
        /// </summary>
        public string GetHealthStateLabel()
        {
            if (Score >= 90)
                return "Excellent";
            if (Score >= 75)
                return "Good";
            if (Score >= 50)
                return "At Risk";
            return "Critical";
        }

        static int GetPenalty(Severity severity)
        {
            switch (severity)
            {
                case Severity.Error:
                case Severity.Critical:
                    return 8;
                case Severity.Major:
                    return 4;
                case Severity.Moderate:
                    return 1;
                default:
                    return 0;
            }
        }
    }
}
