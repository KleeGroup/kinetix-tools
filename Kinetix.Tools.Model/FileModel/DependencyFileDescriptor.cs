﻿using System.Collections.Generic;

#nullable disable
namespace Kinetix.Tools.Model.FileModel
{
    public class DependencyFileDescriptor
    {
        public string File { get; set; }
        public IList<string> Classes { get; set; }
    }
}