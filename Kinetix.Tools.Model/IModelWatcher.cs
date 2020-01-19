using System.Collections.Generic;
using Kinetix.Tools.Model.FileModel;

namespace Kinetix.Tools.Model
{
    public interface IModelWatcher
    {
        void OnFilesChanged(IEnumerable<ModelFile> files);
    }
}
