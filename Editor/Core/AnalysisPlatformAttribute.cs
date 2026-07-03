using System;
using UnityEditor;

namespace SmartAuditor.Editor.Core
{
    internal class AnalysisPlatformAttribute : Attribute
    {
        public BuildTarget Platform { get;}

        public AnalysisPlatformAttribute(BuildTarget platform)
        {
            this.Platform = platform;
        }
    }
}
