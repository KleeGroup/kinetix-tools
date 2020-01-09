﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kinetix.NewGenerator.Config;
using Kinetix.NewGenerator.FileModel;
using Kinetix.NewGenerator.Model;

namespace Kinetix.NewGenerator.CSharp
{
    using static CSharpUtils;

    public class DbContextGenerator
    {
        private readonly string _rootNamespace;
        private readonly CSharpConfig _config;

        public DbContextGenerator(string rootNamespace, CSharpConfig config)
        {
            _rootNamespace = rootNamespace;
            _config = config;
        }

        /// <summary>
        /// Génère l'objectcontext spécialisé pour le schéma.
        /// </summary>
        /// <remarks>Support de Linq2Sql.</remarks>
        /// <param name="classes">Classes.</param>
        public void Generate(IEnumerable<Class> classes)
        {
            if (_config.DbContextProjectPath == null)
            {
                return;
            }

            Console.WriteLine("Generating DbContext");

            var projectName = _config.DbContextProjectPath.Split('/').Last();
            var strippedProjectName = RemoveDots(_rootNamespace);

            var dbContextName = $"{strippedProjectName}DbContext";
            var schema = _config.DbSchema;
            if (schema != null)
            {
                dbContextName = $"{schema.First().ToString().ToUpper() + schema.Substring(1)}DbContext";
            }

            var destDirectory = $"{_config.OutputDirectory}\\{_config.DbContextProjectPath}";

            Directory.CreateDirectory(destDirectory);

            var targetFileName = Path.Combine(destDirectory, "generated", $"{dbContextName}.cs");
            using var w = new CSharpWriter(targetFileName);

            var usings = new List<string>();
            if (_config.Kinetix == KinetixVersion.Core)
            {
                usings.Add("Microsoft.EntityFrameworkCore");
            }
            else
            {
                usings.Add("System.Data.Entity");
                usings.Add("System.Transactions");
                usings.Add("Kinetix.Data.SqlClient");
            }

            foreach (var ns in classes
                .Where(cl => cl.Namespace.Kind == Kind.Data)
                .Select(cl => cl.Namespace.CSharpName)
                .Distinct())
            {
                usings.Add($"{_rootNamespace}.{ns}");
            }

            w.WriteUsings(usings.ToArray());

            w.WriteLine();
            w.WriteLine($"namespace {projectName}");
            w.WriteLine("{");

            if (_config.Kinetix == KinetixVersion.Core)
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
                var inheritance = _config.LegacyIdentity ? "" : " : DbContext";

                w.WriteSummary(1, "DbContext généré pour Entity Framework 6.");
                w.WriteLine(1, $"public partial class {dbContextName}{inheritance}");
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


            foreach (var classe in classes.Where(c => c.Trigram != null).OrderBy(c => c.Name))
            {
                w.WriteLine();
                w.WriteSummary(2, "Accès à l'entité " + classe.Name);

                if (_config.LegacyIdentity && new[] { "User", "Role" }.Contains(classe.Name))
                {
                    w.WriteLine(2, "public override IDbSet<" + classe.Name + "> " + Pluralize(classe.Name) + " { get; set; }");
                }
                else
                {
                    w.WriteLine(2, "public DbSet<" + classe.Name + "> " + Pluralize(classe.Name) + " { get; set; }");
                }
            }

            if (_config.Kinetix == KinetixVersion.Framework)
            {
                w.WriteLine();
                w.WriteSummary(2, "Hook pour l'ajout de configuration su EF (précision des champs, etc).");
                w.WriteParam("modelBuilder", "L'objet de construction du modèle.");
                w.WriteLine(2, "protected override void OnModelCreating(DbModelBuilder modelBuilder)");
                w.WriteLine(2, "{");
                w.WriteLine(3, "base.OnModelCreating(modelBuilder);");
                w.WriteLine();

                foreach (var classe in classes.Where(c => c.Trigram != null).OrderBy(c => c.Name))
                {
                    foreach (var property in classe.Properties.OfType<IFieldProperty>())
                    {
                        if (property.Domain.SqlTypePrecision.HasValue)
                        {
                            w.WriteLine(3, string.Format("modelBuilder.Entity<{0}>().Property(x => x.{1}).HasPrecision({2}, {3});",
                                classe.Name,
                                property.Name,
                                property.Domain.SqlTypePrecision.Value.length,
                                property.Domain.SqlTypePrecision.Value.precision
                            ));
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