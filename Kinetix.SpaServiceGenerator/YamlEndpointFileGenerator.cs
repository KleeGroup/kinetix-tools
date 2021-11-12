using System.Linq;
using Kinetix.Tools.Common;

namespace Kinetix.SpaServiceGenerator
{
    internal static class YamlEndpointFileGenerator
    {

        public static void GenerateYamlEndpointFile(string filePath, ServiceSpa template)
        {
            using var fw = new FileWriter(filePath, false) { EnableHeader = false };

            fw.WriteLine("---");
            fw.WriteLine("module: Web");
            fw.WriteLine("tags:");
            fw.WriteLine("  - Todo");
            fw.WriteLine();

            foreach (var service in template.Services)
            {
                fw.WriteLine("---");
                fw.WriteLine("endpoint:");
                fw.WriteLine($"  name: {service.Name}");
                fw.WriteLine($"  method: {service.Verb}");
                fw.WriteLine($"  route: {service.Route}");
                fw.WriteLine($"  description: {service.Documentation.Summary}");

                if (service.Parameters.Any())
                {
                    fw.WriteLine("  params:");
                    foreach (var param in service.Parameters)
                    {
                        fw.WriteLine($"    - name: {param.Name}");
                        fw.WriteLine($"      domain: {TSUtils.CSharpToTSType(param.Type)}");
                        fw.WriteLine($"      comment: {service.Documentation.Parameters.SingleOrDefault(p => p.Item1 == param.Name)?.Item2}");
                    }
                }

                if (service.ReturnType != null)
                {
                    var returnType = TSUtils.CSharpToTSType(service.ReturnType);
                    fw.WriteLine($"  returns:");
                    fw.WriteLine($"    composition: {returnType.Replace("[]", string.Empty)}");
                    fw.WriteLine($"    kind: {(returnType.EndsWith("[]") ? "list" : "object")}");
                    fw.WriteLine($"    name: result");
                    fw.WriteLine($"    comment: {service.Documentation.Returns}");
                }
            }
        }
    }
}
