﻿using System.Collections.Generic;

namespace Kinetix.Tools.Model.FileModel
{
    public class FileDescriptor
    {
#nullable disable
        public string App { get; set; }
        public string Module { get; set; }
        public Kind Kind { get; set; }
        public string File { get; set; }
#nullable enable
        public IList<DependencyDescriptor>? Uses { get; set; }

        public Namespace Namespace => new Namespace { Kind = Kind, Module = Module };
    }
}
