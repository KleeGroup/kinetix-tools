using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kinetix.Tools.Common.Model;

namespace Kinetix.ClassGenerator
{
    public static class YamlModelGenerator
    {
        internal static void Generate(ICollection<ModelRoot> modelList)
        {
            var domains = modelList.SelectMany(m => m.Namespaces)
                .SelectMany(n => n.Value.ClassList)
                .SelectMany(c => c.PropertyList)
                .Select(p => p.DataDescription?.Domain)
                .Where(d => d != null)
                .Distinct()
                .OrderBy(d => d.Code);

            using (var fw = File.CreateText("yaml/domains.yml"))
            {
                foreach (var domain in domains)
                {
                    fw.WriteLine("---");
                    Write(fw, 0, "domain");
                    Write(fw, 1, "name", domain.Code);
                    Write(fw, 1, "label", domain.Name);
                    Write(fw, 1, "csharpType", domain.DataType);
                    Write(fw, 1, "sqlType", domain.PersistentDataType, !string.IsNullOrWhiteSpace(domain.PersistentDataType));
                    Write(fw, 1, "customAnnotation", domain.CustomAnnotation, !string.IsNullOrWhiteSpace(domain.CustomAnnotation));
                    Write(fw, 1, "customUsings", domain.CustomUsings, !string.IsNullOrWhiteSpace(domain.CustomUsings));
                    Write(fw, 1, "useTypeName", "true", domain.Stereotype == "TypeName");
                }
            }

            foreach (var model in modelList)
            {
                var ns = model.Namespaces.First();
                var nsName = ns.Key;
                var type = nsName.Contains("Data") ? "Data" : "Metier";
                var moduleName = nsName.Replace("Contract", string.Empty).Replace("Data", string.Empty);

                Directory.CreateDirectory($"yaml/{moduleName}/{type}");

                foreach (var file in ns.Value.ClassList.GroupBy(c => c.ClassDiagramsList.OrderBy(x => x).FirstOrDefault()))
                {
                    var fileName = file.Key ?? "00 Missing";
                    var fullPath = $"yaml/{moduleName}/{type}/{fileName}.yml";

                    using var fw = File.CreateText(fullPath);

                    fw.WriteLine("---");
                    Write(fw, 0, "app", model.Name);
                    Write(fw, 0, "module", moduleName);
                    Write(fw, 0, "kind", type);
                    Write(fw, 0, "file", fileName);

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
                            var rModuleName = module.Key.Replace("Contract", string.Empty).Replace("Data", string.Empty);
                            Write(fw, 1, "- module", rModuleName);
                            Write(fw, 2, "kind", rType);
                            Write(fw, 2, "files");

                            foreach (var rFile in module.GroupBy(c => c.ClassDiagramsList.OrderBy(x => x).FirstOrDefault()).OrderBy(f => f.Key))
                            {
                                Write(fw, 3, null, $"- {rFile.Key ?? "00 Missing"}");
                            }
                        }
                    }

                    fw.WriteLine();

                    foreach (var classe in file.OrderBy(f => f.Name))
                    {
                        var defaultProperty = classe.PropertyList.SingleOrDefault(p => p.Stereotype == "DefaultProperty");
                        var orderProperty = classe.PropertyList.SingleOrDefault(p => p.Stereotype == "Order");

                        fw.WriteLine("---");
                        Write(fw, 0, "class");
                        Write(fw, 1, "trigram", classe.Trigram, !string.IsNullOrWhiteSpace(classe.Trigram));
                        Write(fw, 1, "name", classe.Name);
                        Write(fw, 1, "extends", classe.ParentClass?.Name, classe.ParentClass != null);
                        Write(fw, 1, "label", classe.Label, classe.Name != classe.Label);
                        Write(fw, 1, "reference", "true", !string.IsNullOrWhiteSpace(classe.Stereotype));
                        Write(fw, 1, "orderProperty", orderProperty?.Name, orderProperty != null);
                        Write(fw, 1, "defaultProperty", defaultProperty?.Name, defaultProperty != null);
                        Write(fw, 1, "comment", classe.Comment);

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
                                Write(fw, 2, "- association", property.DataDescription.ReferenceClass.Name);
                                Write(fw, 3, "role", property.Role, !string.IsNullOrWhiteSpace(property.Role));
                                Write(fw, 3, "label", property.DataDescription.Libelle);
                                Write(fw, 3, "required", $"{property.DataMember.IsRequired}".ToLower(), !property.IsPrimaryKey);
                                Write(fw, 3, "comment", property.Comment);
                            }
                            else if (property.IsFromComposition)
                            {
                                Write(fw, 2, "- composition", property.DataDescription.ReferenceClass.Name);
                                Write(fw, 3, "name", property.Name);
                                Write(fw, 3, "kind", property.IsCollection ? "list" : "object");
                                Write(fw, 3, "comment", property.Comment);
                            }
                            else
                            {
                                Write(fw, 2, "- name", property.Name);
                                Write(fw, 3, "label", property.DataDescription.Libelle);
                                Write(fw, 3, "primaryKey", "true", property.IsPrimaryKey);
                                Write(fw, 3, "unique", "true", property.IsUnique);
                                Write(fw, 3, "required", $"{property.DataMember.IsRequired}".ToLower(), !property.IsPrimaryKey);
                                Write(fw, 3, "domain", property.DataDescription?.Domain?.Code);
                                Write(fw, 3, "defaultValue", property.DefaultValue, !string.IsNullOrWhiteSpace(property.DefaultValue));
                                Write(fw, 3, "comment", property.Comment);
                            }

                            if (classe.PropertyList.Last() != property)
                            {
                                fw.WriteLine();
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
                                foreach (var prop in props)
                                {
                                    var v = prop.Value?.ToString();
                                    if (v == null)
                                    {
                                        continue;
                                    }

                                    fw.Write($" {prop.Key}: ");
                                    fw.Write(Escape(v));

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

        private static string Escape(string v, bool forRef = true)
        {
            return v == null
                ? null
                : v.Contains(":") || v.Contains("[") || forRef && v.Contains(",") || v == string.Empty || forRef && v.EndsWith(" ")
                ? $@"""{v}"""
                : v;
        }
    }
}