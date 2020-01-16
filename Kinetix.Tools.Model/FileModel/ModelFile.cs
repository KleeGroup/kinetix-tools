﻿
using System.Collections.Generic;
using System.Linq;

#nullable disable
namespace Kinetix.Tools.Model.FileModel
{
    public class ModelFile
    {
        public FileDescriptor Descriptor { get; set; }

        public IList<Class> Classes { get; set; }

        public IList<(object source, string target)> Relationships { get; set; }

        public IEnumerable<FileName> Dependencies => Descriptor.Uses?.SelectMany(u =>
            u.Files.Select(f => new FileName { Module = u.Module, Kind = u.Kind, File = f }))
            ?? new FileName[0];

        public override string ToString()
        {
            return $"{Descriptor.Module}/{Descriptor.Kind}/{Descriptor.File}";
        }
    }
}
