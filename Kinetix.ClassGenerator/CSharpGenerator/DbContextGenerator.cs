using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kinetix.ClassGenerator.Model;

namespace Kinetix.ClassGenerator.CSharpGenerator
{
    using static CodeUtils;
    using static Singletons;

    public static class DbContextGenerator
    {
        /// <summary>
        /// Génère l'objectcontext spécialisé pour le schéma.
        /// </summary>
        /// <remarks>Support de Linq2Sql.</remarks>
        /// <param name="modelRootList">Liste des modeles.</param>
        public static void Generate(IEnumerable<ModelRoot> modelRootList)
        {
            Console.WriteLine("Generating DbContext");

            var projectName = GeneratorParameters.CSharp.DbContextProjectPath.Split('/').Last();
            var rootName = GeneratorParameters.RootNamespace;
            var strippedProjectName = RemoveDots(rootName);
            var dbContextName = $"{strippedProjectName}DbContext";
            var destDirectory = $"{GeneratorParameters.CSharp.OutputDirectory}\\{GeneratorParameters.CSharp.DbContextProjectPath}";

            Directory.CreateDirectory(destDirectory);

            var targetFileName = Path.Combine(destDirectory, "generated", strippedProjectName + "DbContext.cs");
            using (var w = new CSharpWriter(targetFileName))
            {
                var usings = new List<string> { "Microsoft.EntityFrameworkCore" };
                foreach (ModelRoot model in modelRootList)
                {
                    foreach (ModelNamespace ns in model.Namespaces.Values)
                    {
                        var shouldImport = ns.ClassList
                            .Where(cl => cl.DataContract.IsPersistent)
                            .Select(cl => cl.Namespace)
                            .Distinct()
                            .Any();

                        if (shouldImport)
                        {
                            usings.Add($"{rootName}.{ns.Name}");
                        }
                    }
                }

                var isPostgre = GeneratorParameters.ProceduralSql?.TargetDBMS.ToLower() == "postgre";

                if (isPostgre)
                {
                    usings.Add("Npgsql");
                }

                foreach (var us in usings.OrderBy(x => x))
                {
                    w.WriteLine($"using {us};");
                }

                w.WriteLine();
                w.WriteLine($"namespace {projectName}");
                w.WriteLine("{");
                w.WriteSummary(1, "DbContext généré pour Entity Framework Core.");
                w.WriteLine(1, $"public partial class {dbContextName} : DbContext");
                w.WriteLine(1, "{");
                w.WriteSummary(2, "Constructeur par défaut.");
                w.WriteParam("options", "Options du DbContext.");
                w.WriteParam("transaction", "Transaction en cours.");

                var transactionType = isPostgre ? "Npgsql" : "Sql";

                w.WriteLine(2, $"public {strippedProjectName}DbContext(DbContextOptions<{dbContextName}> options, {transactionType}Transaction transaction)");
                w.WriteLine(3, ": base(options)");
                w.WriteLine(2, "{");
                w.WriteLine(3, "Database.UseTransaction(transaction);");
                w.WriteLine(2, "}");

                foreach (ModelRoot model in modelRootList)
                {
                    foreach (ModelNamespace ns in model.Namespaces.Values)
                    {
                        foreach (ModelClass classe in ns.ClassList)
                        {
                            if (classe.DataContract.IsPersistent)
                            {
                                w.WriteLine();
                                w.WriteSummary(2, "Accès à l'entité " + classe.Name);
                                w.WriteLine(2, "public DbSet<" + classe.Name + "> " + Pluralize(classe.Name) + " { get; set; }");
                            }
                        }
                    }
                }

                w.WriteLine(1, "}");
                w.WriteLine("}");
            }
        }
    }
}
