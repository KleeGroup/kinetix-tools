using System.Collections.Generic;

namespace Kinetix.Tools.Model.FileModel
{
    public delegate void ModelFileChangeEventHandler(object sender, IEnumerable<ModelFile> modelFile);
}