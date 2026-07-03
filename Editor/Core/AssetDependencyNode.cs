
namespace SmartAuditor.Editor.Core
{
    internal class AssetDependencyNode : DependencyNode
    {
        public override string GetName()
        {
            return Location.Filename;
        }

        public override string GetPrettyName()
        {
            return Location.Path;
        }
    }
}
