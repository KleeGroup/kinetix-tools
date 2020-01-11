using Kinetix.Tools.Model.Generator.CSharp;
using Kinetix.Tools.Model.Generator.Javascript;
using Kinetix.Tools.Model.Generator.ProceduralSql;
using Kinetix.Tools.Model.Generator.Ssdt;
using Kinetix.Tools.Model;
using Kinetix.Tools.Model.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kinetix.Tools.Model.Generator
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var generators = Host.CreateDefaultBuilder(args)
                .ConfigureServices((_, services) =>
                {
                    services
                        .AddConfig(args[0])
                        .AddSingleton<ModelStore>()
                        .AddSingleton<IGenerator, SsdtGenerator>()
                        .AddSingleton<IGenerator, ProceduralSqlGenerator>()
                        .AddSingleton<IGenerator, CSharpGenerator>()
                        .AddSingleton<IGenerator, TypescriptDefinitionGenerator>()
                        .AddSingleton<IGenerator, JavascriptResourceGenerator>();
                })
                .Build()
                .Services
                .GetServices<IGenerator>();

            foreach (var generator in generators)
            {
                generator.Generate();
            }
        }
    }
}