namespace SmartAuditor.Editor.CodeAnalysis.Compilation
{
    internal class CompiledAssemblyInfo
    {
        public const string DefaultAssemblyFileName = "Assembly-CSharp.dll";
        public static string DefaultAssemblyName => System.IO.Path.GetFileNameWithoutExtension(DefaultAssemblyFileName);

        public string Name { get; set; }            // assembly name without extension
        public string Path { get; set; }            // absolute path
        public string AsmDefPath { get; set; }
        public string RelativePath { get; set; }    // asmdef containing folder, relative to the project

        public bool IsPackageReadOnly { get; set; }
        public string PackageResolvedPath { get; set; }
    }
}
