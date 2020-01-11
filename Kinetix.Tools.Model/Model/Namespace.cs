using Kinetix.Tools.Model.FileModel;

namespace Kinetix.Tools.Model
{
    public struct Namespace
    {
        public string Module { get; set; }
        public Kind Kind { get; set; }
        public string CSharpName => Module + (Kind == Kind.Data ? "DataContract" : "Contract");
    }
}
