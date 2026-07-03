
namespace SmartAuditor.Editor
{
    public static class RuleExtensions
    {
        public static Rule WithSeverity(this Rule rule, Severity severity)
        {
            rule.Severity = severity;
            return rule;
        }

        public static Rule WithPattern(this Rule rule, string filter)
        {
            rule.Pattern = filter;
            return rule;
        }
    }
}
