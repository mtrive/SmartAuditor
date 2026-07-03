namespace SmartAuditor.Editor.Core
{
    static class Documentation
    {
        internal const string baseURL = "https://github.com/mtrive/SmartAuditor/blob/";
        internal const string subURL = "/Documentation~/";
        internal const string endURL = ".md";

        internal static string GetPageUrl(string pageName)
        {
            return baseURL + PackageInfo.Version + subURL + pageName + endURL;
        }
    }
}
