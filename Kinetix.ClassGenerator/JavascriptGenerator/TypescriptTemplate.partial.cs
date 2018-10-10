﻿using Kinetix.ClassGenerator.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Kinetix.ClassGenerator.JavascriptGenerator
{
    /// <summary>
    /// Partial du template de génération de code Typescript.
    /// </summary>
    public partial class TypescriptTemplate
    {
        /// <summary>
        /// Objet de modèle.
        /// </summary>
        public ModelClass Model { get; set; }

        /// <summary>
        /// Namespace de base de l'application.
        /// </summary>
        public string RootNamespace { get; set; }

        private string GetDomain(ModelProperty property)
        {
            return property?.DataDescription?.Domain?.Code;
        }

        private IEnumerable<string> GetDomainList()
        {
            return Model.PropertyList
                .Select(property => property?.DataDescription?.Domain?.Code)
                .Where(domain => domain != null)
                .Distinct()
                .OrderBy(x => x);
        }

        /// <summary>
        /// Récupère la liste d'imports de types pour les services.
        /// </summary>
        /// <returns>La liste d'imports (type, chemin du module, nom du fichier).</returns>
        private IEnumerable<(string import, string path)> GetImportList()
        {
            var types = Model.PropertyList
                .Where(property =>
                    (property.DataDescription?.ReferenceClass?.FullyQualifiedName.StartsWith(RootNamespace, StringComparison.Ordinal) ?? false)
                    && property.DataType != "string" && property.DataType != "int?")
                .Select(property => property.DataDescription?.ReferenceClass?.FullyQualifiedName);

            string parentClassName = null;
            if (Model.ParentClass != null)
            {
                parentClassName = Model.ParentClass.FullyQualifiedName;
                types = types.Concat(new[] { parentClassName });
            }

            var currentModule = GetModuleName(Model.FullyQualifiedName);

            var imports = types.Select(type =>
            {
                var module = GetModuleName(type);
                var name = type.Split('.').Last();

                if (module == currentModule)
                {
                    module = $".";
                }
                else
                {
                    module = $"../{module}";
                }

                return (import: $"{name}Entity", path: $"{module}/{name.ToDashCase()}");
            }).Distinct().ToList();

            var references = Model.PropertyList
                .Where(property => property.DataDescription?.ReferenceClass != null && property.DataType == "string" && property.DataDescription.ReferenceClass.IsReference)
                .Select(property => $"{property.DataDescription.ReferenceClass.Name}Code")
                .Distinct()
                .OrderBy(x => x);

            if (references.Any())
            {
                imports.Add((string.Join(", ", references), "./references"));
            }

            return imports.OrderBy(i => i.path);
        }

        /// <summary>
        /// Récupère le nom du module à partir du nom complet.
        /// </summary>
        /// <param name="fullyQualifiedName">Nom complet.</param>
        /// <returns>Le nom du module.</returns>
        private string GetModuleName(string fullyQualifiedName) =>
            fullyQualifiedName.Split('.')[1]
                .Replace("DataContract", string.Empty)
                .Replace("Contract", string.Empty)
                .ToLower();

        private string GetReferencedType(ModelProperty property)
        {
            if (GetDomain(property) != null)
            {
                return null;
            }

            return property?.DataDescription?.ReferenceClass?.Name;
        }

        private bool IsArray(ModelProperty property)
        {
            return property.IsCollection && property.DataDescription?.Domain?.DataType == null;
        }

        /// <summary>
        /// Transforme une liste de constantes en type Typescript.
        /// </summary>
        /// <param name="constValues">La liste de constantes.</param>
        /// <returns>Le type de sorte.</returns>
        private string ToTSType(IEnumerable<string> constValues)
        {
            return string.Join(" | ", constValues);
        }

        /// <summary>
        /// Transforme le type en type Typescript.
        /// </summary>
        /// <param name="property">La propriété dont on cherche le type.</param>
        /// <param name="removeBrackets">Retire les brackets sur les types de liste.</param>
        /// <returns>Le type en sortie.</returns>
        private string ToTSType(ModelProperty property, bool removeBrackets = false)
        {
            var type = property.DataDescription?.Domain?.DataType;
            switch (type)
            {
                case null:
                    type = property.DataDescription?.ReferenceClass?.FullyQualifiedName;
                    if (property.IsCollection)
                    {
                        type = $"ICollection<{type}>";
                    }

                    break;
                case "ICollection<string>":
                    return "string[]";
                case "ICollection<int>":
                    return "number[]";
            }

            if (type == "string" && property.DataDescription.ReferenceClass != null && property.DataDescription.ReferenceClass.IsReference)
            {
                return $"{property.DataDescription.ReferenceClass.Name}Code";
            }
            else if (type == "string" && (property.DataDescription?.Domain?.PersistentDataType?.Contains("json") ?? false))
            {
                return "{}";
            }

            return ToTSType(type, removeBrackets);
        }

        /// <summary>
        /// Transforme le type en type Typescript.
        /// </summary>
        /// <param name="type">Le type d'entrée.</param>
        /// <param name="removeBrackets">Retire les brackets sur les types de liste.</param>
        /// <returns>Le type en sortie.</returns>
        private string ToTSType(string type, bool removeBrackets = false)
        {
            switch (type)
            {
                case "int?":
                case "decimal?":
                case "short?":
                case "TimeSpan?":
                    return "number";
                case "DateTime?":
                case "Guid?":
                case "string":
                    return "string";
                case "bool?":
                    return "boolean";
                default:
                    if (type?.StartsWith("ICollection") ?? false)
                    {
                        var typeName = $"{ToTSType(Regex.Replace(type, ".+<(.+)>", "$1"))}";
                        if (!removeBrackets)
                        {
                            typeName += "[]";
                        }

                        return typeName;
                    }

                    if (type?.StartsWith(RootNamespace) ?? false)
                    {
                        return type.Split('.').Last();
                    }

                    return "any";
            }
        }
    }
}