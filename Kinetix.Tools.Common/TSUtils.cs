using System;
using System.Linq;
using System.Text.RegularExpressions;
using Kinetix.Tools.Common.Model;
using Microsoft.CodeAnalysis;

namespace Kinetix.Tools.Common
{
    /// <summary>
    /// Regroupe quelques utilitaires pour la génération TS.
    /// </summary>
    public static class TSUtils
    {
        /// <summary>
        /// Transforme le type en type Typescript.
        /// </summary>
        /// <param name="property">La propriété dont on cherche le type.</param>
        /// <returns>Le type en sortie.</returns>
        public static string CSharpToTSType(ModelProperty property)
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

            return CSharpToTSType(type);
        }

        /// <summary>
        /// Récupère le type Typescript correspondant à un type C#.
        /// </summary>
        /// <param name="type">Le type C#.</param>
        /// <returns>Le type TS.</returns>
        public static string CSharpToTSType(INamedTypeSymbol type)
        {
            if (type.IsGenericType)
            {
                if (type.Name == "IDictionary")
                {
                    return $"{{[key: string]: {CSharpToTSType(type.TypeArguments.Last() as INamedTypeSymbol)}}}";
                }

                if (type.AllInterfaces.Any(i => i.Name == "IEnumerable"))
                {
                    return $"{CSharpToTSType(type.TypeArguments.First() as INamedTypeSymbol)}[]";
                }

                if (type.Name == "Nullable" || type.Name == "ActionResult")
                {
                    return CSharpToTSType(type.TypeArguments.First() as INamedTypeSymbol);
                }

                if (type.Name == "QueryInput")
                {
                    return $"QueryInput<{CSharpToTSType(type.TypeArguments.First() as INamedTypeSymbol)}>";
                }

                if (type.Name == "QueryOutput")
                {
                    return $"QueryOutput<{CSharpToTSType(type.TypeArguments.First() as INamedTypeSymbol)}>";
                }

                return $"{type.Name}<{CSharpToTSType(type.TypeArguments.First() as INamedTypeSymbol)}>";
            }

            if (type.Name == "QueryInput")
            {
                return "QueryInput";
            }

            if (type.Name == "QueryOutput")
            {
                return "QueryOutput";
            }

            if (type.Name.Contains("IActionResult"))
            {
                return "void";
            }

            switch (type.SpecialType)
            {
                case SpecialType.None:
                    return type.Name;
                case SpecialType.System_Int32:
                case SpecialType.System_Decimal:
                case SpecialType.System_Double:
                    return "number";
                case SpecialType.System_DateTime:
                case SpecialType.System_String:
                    return "string";
                case SpecialType.System_Boolean:
                    return "boolean";
                case SpecialType.System_Void:
                    return "void";
            }

            return CSharpToTSType(type.Name);
        }

        /// <summary>
        /// Transforme le type en type Typescript.
        /// </summary>
        /// <param name="type">Le type d'entrée.</param>
        /// <param name="removeBrackets">Supprime la liste.</param>
        /// <returns>Le type en sortie.</returns>
        private static string CSharpToTSType(string type)
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
                        return $"{CSharpToTSType(Regex.Replace(type, ".+<(.+)>", "$1"))}[]";
                    }

                    return "any";
            }
        }

        /// <summary>
        /// Convertit un text en dash-case.
        /// </summary>
        /// <param name="text">Le texte en entrée.</param>
        /// <param name="upperStart">Texte commençant par une majuscule.</param>
        /// <returns>Le texte en sortie.</returns>
        public static string ToDashCase(this string text, bool upperStart = true)
        {
            return Regex.Replace(text, @"\p{Lu}", m => "-" + m.Value)
                .ToLowerInvariant()
                .Substring(upperStart ? 1 : 0)
                .Replace("/-", "/");
        }

        /// <summary>
        /// Met la première lettre d'un string en minuscule.
        /// </summary>
        /// <param name="text">Le texte en entrée.</param>
        /// <returns>Le texte en sortie.</returns>
        public static string ToFirstLower(this string text)
        {
            return char.ToLower(text[0]) + text.Substring(1);
        }

        /// <summary>
        /// Met la première lettre d'un string en majuscule.
        /// </summary>
        /// <param name="text">Le texte en entrée.</param>
        /// <returns>Le texte en sortie.</returns>
        public static string ToFirstUpper(this string text)
        {
            return char.ToUpper(text[0]) + text.Substring(1);
        }

        /// <summary>
        /// Passe le texte donnée en camelCase.
        /// </summary>
        /// <param name="namespaceName">Le texte d'entrée.</param>
        /// <returns>Le texte en camelCase.</returns>
        public static string ToNamespace(string namespaceName)
        {
            return namespaceName.EndsWith("DataContract", StringComparison.Ordinal)
                ? namespaceName.Substring(0, namespaceName.Length - 12).ToFirstLower()
                : namespaceName.EndsWith("Contract", StringComparison.Ordinal)
                    ? namespaceName.Substring(0, namespaceName.Length - 8).ToFirstLower()
                    : namespaceName.ToFirstLower();
        }
    }
}
