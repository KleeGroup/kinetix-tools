﻿using System.Collections.Generic;

#nullable disable
namespace Kinetix.Tools.Model.Generator
{
    public abstract class GeneratorConfigBase
    {
        public IList<string> Kinds { get; set; }
    }
}
