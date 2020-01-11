using System.Collections.Generic;

#nullable disable
namespace Kinetix.Tools.Model.FileModel
{
    public class DependencyDescriptor
    {
        public string Module { get; set; }
        public Kind Kind { get; set; }
        public IList<DependencyFileDescriptor> Files { get; set; }
    }
}
