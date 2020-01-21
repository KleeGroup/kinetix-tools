﻿using System.Collections.Generic;
using Kinetix.Tools.Model.FileModel;

namespace Kinetix.Tools.Model.UI
{
    public class ModelWatcherHandler : IModelWatcher
    {
        public event ModelChangeEventHandler? FilesChanged;

        public IDictionary<FileName, ModelFile> Files { get; set; } = new Dictionary<FileName, ModelFile>();

        public string Name => "UI";

        public void OnFilesChanged(IEnumerable<ModelFile> files)
        {
            foreach (var file in files)
            {
                Files[file.Name] = file;
            }

            FilesChanged?.Invoke(this, Files);
        }
    }
}