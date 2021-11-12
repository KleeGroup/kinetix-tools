﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Kinetix.Tools.Model.Generator.CSharp
{
    using static CSharpUtils;

    public class CSharpClassGenerator
    {
        private readonly CSharpConfig _config;
        private readonly ILogger _logger;

        public CSharpClassGenerator(CSharpConfig config, ILogger logger)
        {
            _config = config;
            _logger = logger;
        }

        /// <summary>
        /// Méthode générant le code d'une classe.
        /// </summary>
        /// <param name="item">Classe concernée.</param>
        public void Generate(Class item)
        {
            if (_config.OutputDirectory == null)
            {
                return;
            }

            if (item.Properties.OfType<IFieldProperty>().Any(p => p.Domain.CSharp == null))
            {
                throw new ModelException($"Le type C# de tous les domaines des propriétés de {item.Name} doit être défini.");
            }

            var directory = Path.Combine(_config.OutputDirectory, _config.GetModelPath(item), "generated");
            Directory.CreateDirectory(directory);

            var fileName = Path.Combine(directory, item.Name + ".cs");

            using var w = new CSharpWriter(fileName, _logger, _config.UseLatestCSharp);

            GenerateUsings(w, item);
            w.WriteNamespace(_config.GetNamespace(item));
            w.WriteSummary(1, item.Comment);
            GenerateClassDeclaration(w, item);
            w.WriteNamespaceEnd();
        }

        /// <summary>
        /// Génère le constructeur par recopie d'un type base.
        /// </summary>
        /// <param name="w">Writer.</param>
        /// <param name="item">Classe générée.</param>
        private static void GenerateBaseCopyConstructor(CSharpWriter w, Class item)
        {
            if (item.Extends != null)
            {
                w.WriteLine();
                w.WriteSummary(2, "Constructeur par base class.");
                w.WriteParam("bean", "Source.");
                w.WriteLine(2, "public " + item.Name + "(" + item.Extends.Name + " bean)");
                w.WriteLine(3, ": base(bean)");
                w.WriteLine(2, "{");
                w.WriteLine(3, "OnCreated();");
                w.WriteLine(2, "}");
            }
        }

        /// <summary>
        /// Génération des constantes statiques.
        /// </summary>
        /// <param name="w">Writer.</param>
        /// <param name="item">La classe générée.</param>
        private static void GenerateConstProperties(CSharpWriter w, Class item)
        {
            if (item.ReferenceValues?.Any() ?? false)
            {
                foreach (var refValue in item.ReferenceValues.OrderBy(x => x.Name, StringComparer.Ordinal))
                {
                    var code = item.PrimaryKey == null || item.PrimaryKey.Domain.CSharp!.Type == "string"
                        ? (string)refValue.Value[item.PrimaryKey ?? item.Properties.OfType<IFieldProperty>().First()]
                        : (string)refValue.Value[item.UniqueKeys!.First().First()];
                    var label = item.LabelProperty != null
                        ? (string)refValue.Value[item.LabelProperty]
                        : refValue.Name;

                    w.WriteSummary(2, label);
                    w.WriteLine(2, string.Format("public const string {0} = \"{1}\";", refValue.Name, code));
                    w.WriteLine();
                }
            }
        }

        /// <summary>
        /// Génère les constructeurs.
        /// </summary>
        /// <param name="w">Writer.</param>
        /// <param name="item">La classe générée.</param>
        private static void GenerateConstructors(CSharpWriter w, Class item)
        {
            GenerateDefaultConstructor(w, item);
            GenerateCopyConstructor(w, item);
            GenerateBaseCopyConstructor(w, item);
        }

        /// <summary>
        /// Génère le constructeur par recopie.
        /// </summary>
        /// <param name="w">Writer.</param>
        /// <param name="item">Classe générée.</param>
        private static void GenerateCopyConstructor(CSharpWriter w, Class item)
        {
            w.WriteLine();
            w.WriteSummary(2, "Constructeur par recopie.");
            w.WriteParam("bean", "Source.");
            if (item.Extends != null)
            {
                w.WriteLine(2, "public " + item.Name + "(" + item.Name + " bean)");
                w.WriteLine(3, ": base(bean)");
                w.WriteLine(2, "{");
            }
            else
            {
                w.WriteLine(2, "public " + item.Name + "(" + item.Name + " bean)");
                w.WriteLine(2, "{");
            }

            w.WriteLine(3, "if (bean == null)");
            w.WriteLine(3, "{");
            w.WriteLine(4, "throw new ArgumentNullException(nameof(bean));");
            w.WriteLine(3, "}");
            w.WriteLine();

            var initd = new List<string>();

            foreach (var property in item.Properties.OfType<IFieldProperty>().Where(t => t.Domain.CSharp!.Type.Contains("ICollection")))
            {
                initd.Add(property.Name);
                var strip = property.Domain.CSharp!.Type.Replace("ICollection<", string.Empty).Replace(">", string.Empty);
                w.WriteLine(3, property.Name + " = new List<" + strip + ">(bean." + property.Name + ");");
            }

            foreach (var property in item.Properties.OfType<CompositionProperty>().Where(p => p.Kind == "object"))
            {
                w.WriteLine(3, property.Name + " = new " + property.Composition.Name + "(bean." + property.Name + ");");
            }

            foreach (var property in item.Properties.OfType<CompositionProperty>().Where(p => p.Kind == "list"))
            {
                w.WriteLine(3, property.Name + " = new List<" + property.Composition.Name + ">(bean." + property.Name + ");");
            }

            foreach (var property in item.Properties.Where(p => p is not CompositionProperty && !initd.Contains(p.Name)))
            {
                w.WriteLine(3, property.Name + " = bean." + property.Name + ";");
            }

            w.WriteLine();
            w.WriteLine(3, "OnCreated(bean);");
            w.WriteLine(2, "}");
        }

        /// <summary>
        /// Génère le constructeur par défaut.
        /// </summary>
        /// <param name="w">Writer.</param>
        /// <param name="item">Classe générée.</param>
        private static void GenerateDefaultConstructor(CSharpWriter w, Class item)
        {
            w.WriteSummary(2, "Constructeur.");
            w.WriteLine(2, $@"public {item.Name}()");

            if (item.Extends != null)
            {
                w.WriteLine(3, ": base()");
            }

            w.WriteLine(2, "{");

            var line = false;
            foreach (var property in item.Properties.OfType<IFieldProperty>().Where(t => t.Domain.CSharp!.Type.Contains("ICollection")))
            {
                line = true;
                var strip = property.Domain.CSharp!.Type.Replace("ICollection<", string.Empty).Replace(">", string.Empty);
                w.WriteLine(3, LoadPropertyInit(property.Name, "List<" + strip + ">"));
            }

            foreach (var property in item.Properties.OfType<CompositionProperty>().Where(p => p.Kind == "object"))
            {
                line = true;
                w.WriteLine(3, LoadPropertyInit(property.Name, property.Composition.Name));
            }

            foreach (var property in item.Properties.OfType<CompositionProperty>().Where(p => p.Kind == "list"))
            {
                line = true;
                w.WriteLine(3, LoadPropertyInit(property.Name, "List<" + property.Composition.Name + ">"));
            }

            if (line)
            {
                w.WriteLine();
            }

            w.WriteLine(3, "OnCreated();");
            w.WriteLine(2, "}");
        }

        /// <summary>
        /// Génère les méthodes d'extensibilité.
        /// </summary>
        /// <param name="w">Writer.</param>
        /// <param name="item">Classe générée.</param>
        private static void GenerateExtensibilityMethods(CSharpWriter w, Class item)
        {
            w.WriteLine();
            w.WriteSummary(2, "Methode d'extensibilité possible pour les constructeurs.");
            w.WriteLine(2, "partial void OnCreated();");
            w.WriteLine();
            w.WriteSummary(2, "Methode d'extensibilité possible pour les constructeurs par recopie.");
            w.WriteParam("bean", "Source.");
            w.WriteLine(2, $"partial void OnCreated({item.Name} bean);");
        }

        /// <summary>
        /// Retourne le code associé à l'instanciation d'une propriété.
        /// </summary>
        /// <param name="fieldName">Nom de la variable membre privée.</param>
        /// <param name="dataType">Type de données.</param>
        /// <returns>Code généré.</returns>
        private static string LoadPropertyInit(string fieldName, string dataType)
        {
            var res = $"{fieldName} = ";
            if (IsCSharpBaseType(dataType))
            {
                res += GetCSharpDefaultValueBaseType(dataType) + ";";
            }
            else
            {
                res += $"new {dataType}();";
            }

            return res;
        }

        /// <summary>
        /// Génère le type énuméré présentant les colonnes persistentes.
        /// </summary>
        /// <param name="w">Writer.</param>
        /// <param name="item">La classe générée.</param>
        private static void GenerateEnumCols(CSharpWriter w, Class item)
        {
            w.WriteLine();
            w.WriteSummary(2, "Type énuméré présentant les noms des colonnes en base.");

            if (item.Extends == null)
            {
                w.WriteLine(2, "public enum Cols");
            }
            else
            {
                w.WriteLine(2, "public new enum Cols");
            }

            w.WriteLine(2, "{");

            var cols = item.Properties.OfType<IFieldProperty>().ToList();
            foreach (var property in cols)
            {
                w.WriteSummary(3, "Nom de la colonne en base associée à la propriété " + property.Name + ".");
                w.WriteLine(3, $"{property.SqlName},");
                if (cols.IndexOf(property) != cols.Count - 1)
                {
                    w.WriteLine();
                }
            }

            w.WriteLine(2, "}");
        }

        /// <summary>
        /// Génère l'enum pour les valeurs statiques de références.
        /// </summary>
        /// <param name="w">Writer.</param>
        /// <param name="item">La classe générée.</param>
        private static void GenerateEnumValues(CSharpWriter w, Class item)
        {
            w.WriteSummary(2, $"Valeurs possibles de la liste de référence {item}.");
            w.WriteLine(2, $"public enum {item.PrimaryKey!.Name}s");
            w.WriteLine(2, "{");

            var refs = item.ReferenceValues!.OrderBy(x => x.Name, StringComparer.Ordinal).ToList();
            foreach (var refValue in refs)
            {
                var code = (string)refValue.Value[item.PrimaryKey];
                if (Regex.IsMatch(code, "^\\d"))
                {
                    code = "_" + code;
                }

                var label = item.LabelProperty != null ? (string)refValue.Value[item.LabelProperty] : refValue.Name;

                w.WriteSummary(3, label);
                w.Write($"            {code}");

                if (refs.IndexOf(refValue) != refs.Count - 1)
                {
                    w.WriteLine(",");
                }

                w.WriteLine();
            }

            w.WriteLine(2, "}");
        }

        /// <summary>
        /// Génère les flags d'une liste de référence statique.
        /// </summary>
        /// <param name="w">Writer.</param>
        /// <param name="item">La classe générée.</param>
        private static void GenerateFlags(CSharpWriter w, Class item)
        {
            if (item.FlagProperty != null && item.ReferenceValues != null)
            {
                w.WriteLine();
                w.WriteLine(2, "#region Flags");
                w.WriteLine();
                w.WriteSummary(2, "Flags");
                w.WriteLine(2, "public enum Flags");
                w.WriteLine(2, "{");

                var flagProperty = item.Properties.OfType<IFieldProperty>().Single(rp => rp.Name == item.FlagProperty);
                var flagValues = item.ReferenceValues.Where(refValue => int.TryParse((string)refValue.Value[flagProperty], out var _)).ToList();
                foreach (var refValue in flagValues)
                {
                    var flag = int.Parse((string)refValue.Value[flagProperty]);
                    var label = item.LabelProperty != null
                        ? (string)refValue.Value[item.LabelProperty]
                        : refValue.Name;

                    w.WriteSummary(3, label);
                    w.WriteLine(3, $"{refValue.Name} = 0b{Convert.ToString(flag, 2)},");
                    if (flagValues.IndexOf(refValue) != flagValues.Count - 1)
                    {
                        w.WriteLine();
                    }
                }

                w.WriteLine(2, "}");
                w.WriteLine();
                w.WriteLine(2, "#endregion");
            }
        }

        /// <summary>
        /// Génération de la déclaration de la classe.
        /// </summary>
        /// <param name="w">Writer</param>
        /// <param name="item">Classe à générer.</param>
        private void GenerateClassDeclaration(CSharpWriter w, Class item)
        {
            if (item.Reference && _config.Kinetix != KinetixVersion.None)
            {
                var primaryKey = item.PrimaryKey ?? item.Properties.OfType<IFieldProperty>().First();
                if (primaryKey.Domain.CSharp?.Type == "string")
                {
                    w.WriteAttribute(1, "Reference", "true");
                }
                else
                {
                    w.WriteAttribute(1, "Reference");
                }
            }

            if (!string.IsNullOrEmpty(item.DefaultProperty))
            {
                w.WriteAttribute(1, "DefaultProperty", $@"""{item.DefaultProperty}""");
            }

            if (item.IsPersistent && !_config.NoPersistance)
            {
                var sqlName = _config.UseLowerCaseSqlNames ? item.SqlName.ToLower() : item.SqlName;
                if (_config.DbSchema != null)
                {
                    w.WriteAttribute(1, "Table", $@"""{sqlName}""", $@"Schema = ""{_config.DbSchema}""");
                }
                else
                {
                    w.WriteAttribute(1, "Table", $@"""{sqlName}""");
                }
            }

            w.WriteClassDeclaration(item.Name, item.Extends?.Name);

            if (!_config.CanClassUseEnums(item))
            {
                GenerateConstProperties(w, item);
            }

            GenerateConstructors(w, item);

            if (_config.DbContextPath == null && item.IsPersistent && !_config.NoPersistance)
            {
                w.WriteLine();
                w.WriteLine(2, "#region Meta données");
                GenerateEnumCols(w, item);
                w.WriteLine();
                w.WriteLine(2, "#endregion");
            }

            if (_config.CanClassUseEnums(item))
            {
                w.WriteLine();
                GenerateEnumValues(w, item);
            }

            GenerateFlags(w, item);
            GenerateProperties(w, item);
            GenerateExtensibilityMethods(w, item);
            w.WriteLine(1, "}");
        }

        /// <summary>
        /// Génère les propriétés.
        /// </summary>
        /// <param name="w">Writer.</param>
        /// <param name="item">La classe générée.</param>
        private void GenerateProperties(CSharpWriter w, Class item)
        {
            var sameColumnSet = new HashSet<string>(item.Properties.OfType<IFieldProperty>()
                .GroupBy(g => g.SqlName).Where(g => g.Count() > 1).Select(g => g.Key));
            foreach (var property in item.Properties)
            {
                w.WriteLine();
                GenerateProperty(w, property, sameColumnSet);
            }
        }

        /// <summary>
        /// Génère la propriété concernée.
        /// </summary>
        /// <param name="w">Writer.</param>
        /// <param name="property">La propriété générée.</param>
        /// <param name="sameColumnSet">Sets des propriétés avec le même nom de colonne, pour ne pas les gérerer (genre alias).</param>
        private void GenerateProperty(CSharpWriter w, IProperty property, HashSet<string> sameColumnSet)
        {
            w.WriteSummary(2, property.Comment);

            var type = _config.GetPropertyTypeName(property, useIEnumerable: false);

            if (property is IFieldProperty fp)
            {
                var domain = (fp as AliasProperty)?.ListDomain ?? fp.Domain;

                var prop = fp is AliasProperty alp ? alp.Property : fp;
                if ((!_config.NoColumnOnAlias || fp is not AliasProperty) && fp is not AliasProperty { ListDomain: not null } && prop.Class.IsPersistent && !_config.NoPersistance && !sameColumnSet.Contains(prop.SqlName))
                {
                    var sqlName = _config.UseLowerCaseSqlNames ? prop.SqlName.ToLower() : prop.SqlName;
                    if (domain.CSharp!.UseSqlTypeName)
                    {
                        w.WriteAttribute(2, "Column", $@"""{sqlName}""", $@"TypeName = ""{domain.SqlType}""");
                    }
                    else
                    {
                        w.WriteAttribute(2, "Column", $@"""{sqlName}""");
                    }
                }

                if (fp.Required && !fp.PrimaryKey || fp is AliasProperty { Property: { PrimaryKey: true } })
                {
                    w.WriteAttribute(2, "Required");
                }

                if (_config.Kinetix != KinetixVersion.None)
                {
                    if (prop is AssociationProperty ap && !ap.AsAlias)
                    {
                        w.WriteAttribute(2, "ReferencedType", $"typeof({ap.Association.Name})");
                    }
                    else if (fp is AliasProperty alp2 && !alp2.PrimaryKey && alp2.Property.PrimaryKey)
                    {
                        w.WriteAttribute(2, "ReferencedType", $"typeof({alp2.Property.Class.Name})");
                    }
                }

                if (_config.Kinetix == KinetixVersion.Core)
                {
                    w.WriteAttribute(2, "Domain", $@"Domains.{domain.CSharpName}");
                }
                else if (_config.Kinetix == KinetixVersion.Framework)
                {
                    w.WriteAttribute(2, "Domain", $@"""{domain.Name}""");
                }

                if (type == "string" && domain.Length != null)
                {
                    w.WriteAttribute(2, "StringLength", $"{domain.Length}");
                }

                foreach (var annotation in domain.CSharp!.Annotations)
                {
                    w.WriteLine(2, annotation);
                }

                if (fp.DefaultValue != null)
                {
                    w.WriteAttribute(2, "DatabaseGenerated", "DatabaseGeneratedOption.Identity");
                }
            }
            else
            {
                w.WriteAttribute(2, "NotMapped");
            }

            if (property.PrimaryKey && property is RegularProperty)
            {
                w.WriteAttribute(2, "Key");
            }

            w.WriteLine(2, $"public {type} {property.Name} {{ get; set; }}");
        }

        /// <summary>
        /// Génération des imports.
        /// </summary>
        /// <param name="w">Writer.</param>
        /// <param name="item">Classe concernée.</param>
        private void GenerateUsings(CSharpWriter w, Class item)
        {
            var usings = new List<string>();

            if (!_config.UseLatestCSharp)
            {
                usings.Add("System");

                if (item.Properties.Any(p => p is CompositionProperty { Kind: "list" }))
                {
                    usings.Add("System.Collections.Generic");
                }
            }

            if (!string.IsNullOrEmpty(item.DefaultProperty))
            {
                usings.Add("System.ComponentModel");
            }

            if (item.Properties.OfType<IFieldProperty>()
                .Select(p => p is AliasProperty alp ? alp.Property : p)
                .Any(p => p.Required || p.PrimaryKey))
            {
                usings.Add("System.ComponentModel.DataAnnotations");
            }

            if (item.Properties.Any(p => p is CompositionProperty) ||
                item.Properties.OfType<IFieldProperty>().Any(fp =>
                {
                    var prop = fp is AliasProperty alp ? alp.Property : fp;
                    return (!_config.NoColumnOnAlias || fp is not AliasProperty) && prop.Class.IsPersistent && !_config.NoPersistance;
                }))
            {
                usings.Add("System.ComponentModel.DataAnnotations.Schema");
            }

            if (item.Properties.OfType<IFieldProperty>().Any() || item.Extends == null)
            {
                if (_config.Kinetix == KinetixVersion.Core)
                {
                    usings.Add("Kinetix.ComponentModel.Annotations");
                    usings.Add($"{item.Namespace.App}.Common");
                }
                else if (_config.Kinetix == KinetixVersion.Framework)
                {
                    usings.Add("Kinetix.ComponentModel");
                }
            }

            if (item.Extends != null)
            {
                usings.Add(_config.GetNamespace(item.Extends));
            }

            foreach (var property in item.Properties)
            {
                if (property is IFieldProperty fp)
                {
                    foreach (var @using in (fp is AliasProperty { ListDomain: Domain ld } ? ld : fp.Domain).CSharp!.Usings)
                    {
                        usings.Add(@using);
                    }
                }

                switch (property)
                {
                    case AssociationProperty ap when !ap.AsAlias:
                        usings.Add(_config.GetNamespace(ap.Association));
                        break;
                    case AliasProperty { Property: AssociationProperty ap2 }:
                        usings.Add(_config.GetNamespace(ap2.Association));
                        break;
                    case AliasProperty { PrimaryKey: false, Property: RegularProperty { PrimaryKey: true } rp }:
                        usings.Add(_config.GetNamespace(rp.Class));
                        break;
                    case CompositionProperty cp:
                        usings.Add(_config.GetNamespace(cp.Composition));
                        if (cp.DomainKind != null)
                        {
                            usings.AddRange(cp.DomainKind.CSharp!.Usings);
                        }

                        break;
                }
            }

            w.WriteUsings(usings
                .Where(u => u != _config.GetNamespace(item))
                .Distinct()
                .ToArray());

            if (usings.Any())
            {
                w.WriteLine();
            }
        }
    }
}
