using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kinetix.Tools.Common.Model;
using Kinetix.Tools.Common.Parameters;

namespace Kinetix.ClassGenerator.CSharpGenerator
{
    using static CSharpUtils;

    public class DbContextGenerator
    {
        private readonly string _rootNamespace;
        private readonly CSharpParameters _parameters;

        public DbContextGenerator(string rootNamespace, CSharpParameters parameters)
        {
            _rootNamespace = rootNamespace;
            _parameters = parameters;
        }

        /// <summary>
        /// Génère l'objectcontext spécialisé pour le schéma.
        /// </summary>
        /// <remarks>Support de Linq2Sql.</remarks>
        /// <param name="modelRootList">Liste des modeles.</param>
        public void Generate(IEnumerable<ModelRoot> modelRootList)
        {
            Console.WriteLine("Generating DbContext");

            var projectName = _parameters.DbContextProjectPath.Split('/').Last();
            var strippedProjectName = RemoveDots(_rootNamespace);

            var dbContextName = $"{strippedProjectName}DbContext";
            var schema = _parameters.DbSchema;
            if (schema != null)
            {
                dbContextName = $"{schema.First().ToString().ToUpper() + schema.Substring(1)}DbContext";
            }

            var destDirectory = $"{_parameters.OutputDirectory}\\{_parameters.DbContextProjectPath}";

            Directory.CreateDirectory(destDirectory);

            var targetFileName = Path.Combine(destDirectory, "generated", $"{dbContextName}.cs");
            using (var w = new CSharpWriter(targetFileName))
            {

                var usings = new List<string>();
                if (_parameters.Kinetix == "Core")
                {
                    usings.Add("Microsoft.EntityFrameworkCore");
                }
                else
                {
                    usings.Add("System.Data.Entity");
                    usings.Add("System.Transactions");
                    usings.Add("Kinetix.Data.SqlClient");
                }

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
                            usings.Add($"{_rootNamespace}.{ns.Name}");
                        }
                    }
                }

                w.WriteUsings(usings.ToArray());

                w.WriteLine();
                w.WriteLine($"namespace {projectName}");
                w.WriteLine("{");

                if (_parameters.Kinetix == "Core")
                {
                    w.WriteSummary(1, "DbContext généré pour Entity Framework Core.");
                    w.WriteLine(1, $"public partial class {dbContextName} : DbContext");
                    w.WriteLine(1, "{");

                    w.WriteSummary(2, "Constructeur par défaut.");
                    w.WriteParam("options", "Options du DbContext.");
                    w.WriteLine(2, $"public {dbContextName}(DbContextOptions<{dbContextName}> options)");
                    w.WriteLine(3, ": base(options)");
                    w.WriteLine(2, "{");
                    w.WriteLine(2, "}");
                }
                else
                {
                    w.WriteSummary(1, "DbContext généré pour Entity Framework 6.");
                    w.WriteLine(1, $"public partial class {dbContextName} : DbContext");
                    w.WriteLine(1, "{");

                    w.WriteSummary(2, "Constructeur par défaut.");
                    w.WriteLine(2, $"public {dbContextName}()");
                    w.WriteLine(3, ": base(SqlServerManager.Instance.ObtainConnection(\"default\"), false)");
                    w.WriteLine(2, "{");
                    w.WriteLine(2, "}");

                    w.WriteLine();
                    w.WriteSummary(2, "Constructeur par défaut.");
                    w.WriteParam("scope", "Transaction scope.");
                    w.WriteLine(2, $"public {dbContextName}(TransactionScope scope)");
                    w.WriteLine(3, ": this()");
                    w.WriteLine(2, "{");
                    w.WriteLine(2, "}");
                }

                foreach (ModelRoot model in modelRootList)
                {
                    foreach (ModelNamespace ns in model.Namespaces.Values)
                    {
                        foreach (ModelClass classe in ns.ClassList.OrderBy(c => c.Name))
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

                if (_parameters.Kinetix == "Framework")
                {
                    w.WriteLine();
                    w.WriteSummary(2, "Hook pour l'ajout de configuration su EF (précision des champs, etc).");
                    w.WriteParam("modelBuilder", "L'objet de construction du modèle.");
                    w.WriteLine(2, "protected override void OnModelCreating(DbModelBuilder modelBuilder)");
                    w.WriteLine(2, "{");
                    w.WriteLine(3, "base.OnModelCreating(modelBuilder);");
                    w.WriteLine();

                    foreach (ModelRoot model in modelRootList)
                    {
                        foreach (ModelNamespace ns in model.Namespaces.Values)
                        {
                            foreach (ModelClass classe in ns.ClassList.OrderBy(c => c.Name))
                            {
                                if (classe.DataContract.IsPersistent)
                                {
                                    foreach (ModelProperty property in classe.PropertyList)
                                    {
                                        if (property.DataType.StartsWith("decimal")
                                            && property.DataDescription.Domain.PersistentLength.HasValue
                                            && property.DataDescription.Domain.PersistentPrecision.HasValue)
                                        {
                                            w.WriteLine(3, string.Format("modelBuilder.Entity<{0}>().Property(x => x.{1}).HasPrecision({2}, {3});",
                                                classe.Name,
                                                property.Name,
                                                property.DataDescription.Domain.PersistentLength.Value,
                                                property.DataDescription.Domain.PersistentPrecision.Value
                                            ));
                                        }
                                    }
                                }
                            }
                        }
                    }

                    w.WriteLine();
                    w.WriteLine(3, "OnModelCreatingCustom(modelBuilder);");
                    w.WriteLine(2, "}");

                    w.WriteLine();
                    w.WriteSummary(2, "Hook pour l'ajout de configuration custom sur EF (view, etc).");
                    w.WriteParam("modelBuilder", "L'objet de construction du modèle");
                    w.WriteLine(2, "partial void OnModelCreatingCustom(DbModelBuilder modelBuilder);");
                }

                w.WriteLine(1, "}");
                w.WriteLine("}");
            }
        }
    }
}
