﻿#nullable disable
using System.Collections.Generic;

namespace Kinetix.Tools.Model.FileModel
{
    public class DependencyDescriptor
    {
        public string Module { get; set; }

        public Kind Kind { get; set; }

        public IList<string> Files { get; set; }
    }
}
