using System.Collections.Generic;

namespace Kinetix.Tools.Model
{
    public class Endpoint
    {
#nullable disable
        public string Name { get; set; }

        public string Method { get; set; }

        public string Route { get; set; }

        public string Description { get; set; }

#nullable enable
        public IProperty? Returns { get; set; }

        public string? Body { get; set; }

#nullable disable
        public IList<IProperty> Params { get; set; } = new List<IProperty>();
#nullable enable
    }
}
