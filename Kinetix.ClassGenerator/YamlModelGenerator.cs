using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Kinetix.Tools.Common.Model;

namespace Kinetix.ClassGenerator
{
    public static class YamlModelGenerator
    {
        internal static void Generate(ICollection<ModelRoot> modelList)
        {
            // conversion types PowerDesigner
            var domainTypeMapperCs = (string type) =>
            {
                switch (type)
                {
                    case "bool":
                        return "bool?";

                    case "System.DateTime":
                        return "DateTime?";

                    case "decimal":
                        return "decimal?";

                    case "int":
                        return "int?";

                    default:
                        return type;
                }
            };

            // conversion types domaines TypeScript
            var domainTypeMapperTypeScript = (string type) =>
            {
                switch (type)
                {
                    case "bool":
                        return "boolean";

                    case "byte[]":
                        return "object";

                    case "decimal":
                    case "int":
                    case "long":
                    case "short":
                    case "System.Int64":
                    case "System.TimeSpan":
                        return "number";

                    case "System.DateTime":
                        return "string";

                    default:
                        return type;
                }
            };

            // conversion types domaines Sql
            var regExNumeric = new Regex("^N[0-9]*,[0-9]*$");
            var regExVarChar = new Regex("^VA[0-9]*$");
            var regExDecimal = new Regex("^DC[0-9]*,[0-9]*$");
            var domainTypeMapperSql = (string persistentDataType, int? persistentLength, int? persistantPrecision) =>
            {
                if (persistentDataType == "I")
                {
                    return "int";
                }

                if (persistentDataType == "DT")
                {
                    return "datetime2(0)";
                }

                if (persistentDataType == "BL")
                {
                    return "bit";
                }

                if (persistentDataType == "SI")
                {
                    return "smallint";
                }

                if (persistentDataType == "T")
                {
                    return "time";
                }

                if (persistentDataType == "TXT")
                {
                    return "text";
                }

                if (persistentDataType == "PIC")
                {
                    return "image";
                }

                if (persistentDataType == "BI")
                {
                    return "bigint";
                }

                if (regExNumeric.IsMatch(persistentDataType) && persistentLength.HasValue && persistantPrecision.HasValue)
                {
                    return $"numeric({persistentLength},{persistantPrecision})";
                }

                if (regExVarChar.IsMatch(persistentDataType) && persistentLength.HasValue)
                {
                    return $"nvarchar({persistentLength})";
                }

                if (regExDecimal.IsMatch(persistentDataType))
                {
                    return "decimal";
                }

                return persistentDataType;
            };

            // conversion nom du module
            var getModuleName = (string m) =>
            {
                var name = m.Replace("Contract", string.Empty).Replace("Data", string.Empty);
                if (name.ToLower() == "export")
                {
                    name = "Exports";
                }
                return name;
            };

            var domains = modelList.SelectMany(m => m.Namespaces)
                .SelectMany(n => n.Value.ClassList)
                .SelectMany(c => c.PropertyList)
                .Select(p => p.DataDescription?.Domain)
                .Where(d => d != null)
                .DistinctBy(d => d.Code)
                .OrderBy(d => d.Code);

            using (var fw = File.CreateText($"domains.tmd"))
            {
                fw.WriteLine("---");
                Write(fw, 0, "module", "domains");
                Write(fw, 0, "tags");
                Write(fw, 1, null, "- domains");

                foreach (var domain in domains)
                {
                    fw.WriteLine("---");
                    Write(fw, 0, "domain");
                    Write(fw, 1, "name", domain.Code);
                    Write(fw, 1, "label", domain.Name);
                    if (domain.Code == "DO_ID")
                    {
                        Write(fw, 1, "autoGeneratedValue", "true");
                    }
                    Write(fw, 1, "csharp");
                    Write(fw, 2, "type", domainTypeMapperCs(domain.DataType));
                    Write(fw, 1, "ts");
                    Write(fw, 2, "type", domainTypeMapperTypeScript(domain.DataType));
                    Write(fw, 1, "sqlType", domainTypeMapperSql(domain.PersistentDataType, domain.PersistentLength, domain.PersistentPrecision), !string.IsNullOrWhiteSpace(domain.PersistentDataType));
                }
            }

            foreach (var model in modelList)
            {
                var ns = model.Namespaces.First();
                var nsName = ns.Key;
                var type = nsName.Contains("Data") ? "Data" : "Metier";
                var moduleName = getModuleName(nsName);

                Directory.CreateDirectory($"{moduleName}/{type}");

                foreach (var file in ns.Value.ClassList.GroupBy(c => c.ClassDiagramsList.OrderBy(x => x).FirstOrDefault()))
                {
                    var fileName = file.Key ?? "00 Missing";
                    var fullPath = $"{moduleName}/{type}/{fileName}.tmd";

                    using var fw = File.CreateText(fullPath);

                    fw.WriteLine("---");
                    Write(fw, 0, "module", moduleName);
                    Write(fw, 0, "tags");
                    Write(fw, 1, null, $"- {type}");

                    var references = file
                        .SelectMany(c => c.PropertyList)
                        .Select(p => p.DataDescription?.ReferenceClass ?? p.AliasedProperty?.Class)
                        .Where(rc => rc != null && !file.Any(c => c.Name == rc.Name))
                        .Distinct()
                        .ToList();

                    if (references.Any())
                    {
                        Write(fw, 0, "uses");

                        foreach (var module in references.GroupBy(c => c.Namespace.Name))
                        {
                            var rType = module.Key.Contains("Data") ? "Data" : "Metier";
                            var rModuleName = getModuleName(module.Key);

                            foreach (var rFile in module.GroupBy(c => c.ClassDiagramsList.OrderBy(x => x).FirstOrDefault()).OrderBy(f => f.Key))
                            {
                                Write(fw, 1, null, $"- {rModuleName}/{rType}/{rFile.Key ?? "00 Missing"}");
                            }
                        }
                    }

                    fw.WriteLine();

                    foreach (var classe in file.OrderBy(f => f.Name))
                    {
                        var defaultProperty = classe.PropertyList.SingleOrDefault(p => p.Stereotype == "DefaultProperty");
                        var orderProperty = classe.PropertyList.SingleOrDefault(p => p.Annotations.Any(e => e.Name == "Ordre"));

                        fw.WriteLine("---");
                        Write(fw, 0, "class");
                        Write(fw, 1, "trigram", classe.Trigram, !string.IsNullOrWhiteSpace(classe.Trigram));
                        Write(fw, 1, "name", classe.Name);
                        Write(fw, 1, "extends", classe.ParentClass?.Name, classe.ParentClass != null);
                        Write(fw, 1, "label", classe.Label, classe.Name != classe.Label);
                        Write(fw, 1, "reference", "true", !string.IsNullOrWhiteSpace(classe.Stereotype));
                        Write(fw, 1, "orderProperty", orderProperty?.Name, orderProperty != null);
                        Write(fw, 1, "defaultProperty", defaultProperty?.Name, defaultProperty != null);
                        Write(fw, 1, "comment", string.IsNullOrWhiteSpace(classe.Comment) ? "N/A" : classe.Comment);

                        fw.WriteLine();
                        Write(fw, 1, "properties");
                        foreach (var property in classe.PropertyList)
                        {
                            if (classe.ParentClass != null && classe.ParentClass.PropertyList.Any(p => p.Name == property.Name))
                            {
                                continue;
                            }

                            if (property.AliasedProperty != null)
                            {
                                Write(fw, 2, "- alias");
                                Write(fw, 4, "property", property.AliasedProperty.Name);
                                Write(fw, 4, "class", property.AliasedProperty.Class.Name);
                                Write(fw, 3, "prefix", property.AliasPrefix, !string.IsNullOrWhiteSpace(property.AliasPrefix) && !property.AliasedProperty.Name.StartsWith(property.AliasPrefix));
                                Write(fw, 3, "suffix", property.AliasSuffix, !string.IsNullOrWhiteSpace(property.AliasSuffix));
                            }
                            else if (property.IsFromAssociation)
                            {
                                var role = property.Role;

                                // if role is a number, make it a string
                                if (Regex.Match(role, @"^\d+$").Success)
                                {
                                    role = "\"" + property.Role + "\"";
                                }

                                Write(fw, 2, "- association", property.DataDescription.ReferenceClass.Name);
                                Write(fw, 3, "role", role, !string.IsNullOrWhiteSpace(property.Role));
                                Write(fw, 3, "label", property.DataDescription.Libelle);
                                Write(fw, 3, "required", $"{property.DataMember.IsRequired}".ToLower(), !property.IsPrimaryKey);
                                Write(fw, 3, "comment", string.IsNullOrWhiteSpace(property.Comment) ? "N/A" : property.Comment);
                            }
                            else if (property.IsFromComposition)
                            {
                                Write(fw, 2, "- composition", property.DataDescription.ReferenceClass.Name);
                                Write(fw, 3, "name", property.Name);
                                Write(fw, 3, "kind", property.IsCollection ? "list" : "object");
                                Write(fw, 3, "comment", string.IsNullOrWhiteSpace(property.Comment) ? "N/A" : property.Comment);
                            }
                            else
                            {
                                Write(fw, 2, "- name", property.Name);
                                Write(fw, 3, "label", property.DataDescription.Libelle);
                                Write(fw, 3, "primaryKey", "true", property.IsPrimaryKey);
                                Write(fw, 3, "required", $"{property.DataMember.IsRequired}".ToLower(), !property.IsPrimaryKey);
                                Write(fw, 3, "domain", property.DataDescription?.Domain?.Code);
                                Write(fw, 3, "defaultValue", property.DefaultValue, !string.IsNullOrWhiteSpace(property.DefaultValue));
                                Write(fw, 3, "comment", string.IsNullOrWhiteSpace(property.Comment) ? "N/A" : property.Comment);
                            }

                            if (classe.PropertyList.Last() != property)
                            {
                                fw.WriteLine();
                            }
                        }

                        if (classe.PropertyList.Any(p => p.IsUnique))
                        {
                            fw.WriteLine();
                            fw.WriteLine("  unique:");
                            foreach (var p in classe.PropertyList.Where(p => p.IsUnique).OrderBy(p => p.Name))
                            {
                                fw.WriteLine($"    - [{p.Name}]");
                            }
                        }

                        if (classe.ConstValues != null && classe.ConstValues.Any())
                        {
                            fw.WriteLine();
                            fw.WriteLine("  values:");
                            var keyLength = classe.ConstValues.Select(v => v.Key).Max(n => n.Length);

                            var maxLengths = classe.ConstValues.SelectMany(rv => rv.Value.Values).GroupBy(p => p.Key)
                                .ToDictionary(p => p.Key, p => p.Max(v => Escape(v.Value?.ToString())?.Length ?? 0));

                            foreach (var value in classe.ConstValues)
                            {
                                fw.Write("    ");
                                fw.Write($"{value.Key}:{string.Join(string.Empty, Enumerable.Range(0, keyLength - value.Key.Length).Select(_ => " "))} {{");

                                var props = value.Value.Values.ToList();
                                foreach (var prop in props.Where(e => e.Value?.ToString() != "__NULL__"))
                                {
                                    var v = prop.Value?.ToString();
                                    if (v == null)
                                    {
                                        continue;
                                    }

                                    fw.Write($" {prop.Key}: ");
                                    v = v == "true" ? "1" : v;
                                    v = v == "false" ? "0" : v;
                                    fw.Write(
                                        Regex.Match(v, @"^\d+[_]*$").Success
                                            ? $"\"{v}\""
                                            : Escape(v)
                                    );

                                    if (props.IndexOf(prop) < props.Count - 1)
                                    {
                                        fw.Write(",");
                                        fw.Write(string.Join(string.Empty, Enumerable.Range(0, maxLengths[prop.Key] - (Escape(v)?.Length ?? 0)).Select(_ => " ")));
                                    }
                                }

                                fw.WriteLine(" }");
                            }
                        }
                    }

                    Console.WriteLine($"Ecriture du fichier {fullPath}");
                }
            }
        }

        private static string Escape(string v, bool forRef = true)
        {
            v = v?.Replace("{", "(").Replace("}", ")").Replace("\r\n", " ");

            if (v == null)
            {
                return null;
            }

            if (v.Contains(":") || v.Contains("[") || forRef &&
                v.Contains(",") || v == string.Empty || forRef &&
                v.EndsWith(" ")
            )
            {
                return $@"""{v}""";
            }

            return v;
        }

        private static void Write(StreamWriter fw, int indent, string property, string value = null, bool condition = true)
        {
            if (!condition)
            {
                return;
            }

            var spaces = string.Join(string.Empty, Enumerable.Range(0, indent).Select(_ => "  "));

            fw.Write(spaces);
            if (property != null)
            {
                fw.Write($"{property}:");
            }
            if (!string.IsNullOrWhiteSpace(value))
            {
                if (property != null)
                {
                    fw.Write(" ");
                }
                if (value.Contains("\n"))
                {
                    fw.Write("|\r\n");
                }
                var lines = value.Split("\r\n");
                foreach (var line in lines)
                {
                    if (value.Contains("\n"))
                    {
                        fw.Write($"{spaces}  ");
                    }

                    fw.Write(Escape(line, false));
                    fw.Write("\r\n");
                }
            }
            else
            {
                fw.Write("\r\n");
            }
        }
    }
}
