using System.Collections.Generic;
using Kinetix.Tools.Model.FileModel;

namespace Kinetix.Tools.Model
{
    public interface IModelWatcher
    {
        string Name { get; }

        int Number { get; set; }

        string FullName => $"{Name}@{Number}";

        void OnFilesChanged(IEnumerable<ModelFile> files);
    }
}
