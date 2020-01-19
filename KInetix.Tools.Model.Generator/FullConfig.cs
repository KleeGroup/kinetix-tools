using Kinetix.Tools.Model.Generator.CSharp;
using Kinetix.Tools.Model.Generator.Javascript;
using Kinetix.Tools.Model.Generator.ProceduralSql;
using Kinetix.Tools.Model.Generator.Ssdt;

namespace Kinetix.Tools.Model.Generator
{
    public class FullConfig : ModelConfig
    {
        public ProceduralSqlConfig? ProceduralSql { get; set; }

        public SsdtConfig? Ssdt { get; set; }

        public JavascriptConfig? Javascript { get; set; }

        public CSharpConfig? Csharp { get; set; }
    }
}
