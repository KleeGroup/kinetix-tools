using System.Collections.Generic;
using Kinetix.Tools.Model.FileModel;

namespace Kinetix.Tools.Model.UI
{
    public delegate void ModelChangeEventHandler(object sender, IDictionary<FileName, ModelFile> files);
}