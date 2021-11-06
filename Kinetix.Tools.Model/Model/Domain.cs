﻿using Kinetix.Tools.Model.Types;
using YamlDotNet.Serialization;

namespace Kinetix.Tools.Model
{
    public class Domain
    {
#nullable disable
        public string Name { get; set; }

        public string Label { get; set; }
#nullable enable

        public int? Length { get; set; }

        public int? Scale { get; set; }

        [YamlMember(Alias = "csharp")]
        public CSharpType? CSharp { get; set; }

        [YamlMember(Alias = "ts")]
        public TSType? TS { get; set; }

        public JavaType? Java { get; set; }

        public string? SqlType { get; set; }

        public string CSharpName => Name.Replace("DO_", string.Empty).ToPascalCase();

        public bool ShouldQuoteSqlValue =>
            (SqlType ?? string.Empty).Contains("varchar")
            || SqlType == "text"
            || CSharp?.Type == "string";
    }
}
