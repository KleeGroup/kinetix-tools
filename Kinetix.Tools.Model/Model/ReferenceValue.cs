using System.Collections.Generic;

#nullable disable
namespace Kinetix.Tools.Model
{
    public class ReferenceValue
    {
        public string Name { get; set; }

        public IDictionary<IFieldProperty, object> Value { get; set; }
    }
}
