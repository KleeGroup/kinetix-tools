using System.Collections.Generic;
using Kinetix.Tools.Model.Generator.CSharp;
using Kinetix.Tools.Model.Generator.Javascript;
using Kinetix.Tools.Model.Generator.Kasper;
using Kinetix.Tools.Model.Generator.ProceduralSql;
using Kinetix.Tools.Model.Generator.Ssdt;

namespace Kinetix.Tools.Model.Generator
{
    public class FullConfig : ModelConfig
    {
        public IList<ProceduralSqlConfig>? ProceduralSql { get; set; }

        public IList<SsdtConfig>? Ssdt { get; set; }

        public IList<JavascriptConfig>? Javascript { get; set; }

        public IList<CSharpConfig>? Csharp { get; set; }

        public IList<KasperConfig>? Kasper { get; set; }
    }
}
