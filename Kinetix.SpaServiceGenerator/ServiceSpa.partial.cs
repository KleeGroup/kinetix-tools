using System.Collections.Generic;
using System.Linq;
using Kinetix.SpaServiceGenerator.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Kinetix.SpaServiceGenerator
{
    /// <summary>
    /// Template de contrôlleur.
    /// </summary>
    public partial class ServiceSpa
    {
        /// <summary>
        /// Le chemin vers le répertoire de définitions.
        /// </summary>
        public int FolderCount { get; set; }

        /// <summary>
        /// Le nom du projet, namespace global (exemple : "Chaine").
        /// </summary>
        public string ProjectName { get; set; }

        public string ServerPath => GetModulePathPrefix("server", FolderCount);

        /// <summary>
        /// La liste des services.
        /// </summary>
        public ICollection<ServiceDeclaration> Services { get; set; }

        /// <summary>
        /// Récupère le type Typescript correspondant à un type C#.
        /// </summary>
        /// <param name="type">Le type C#.</param>
        /// <returns>Le type TS.</returns>
        private static string GetTSType(INamedTypeSymbol type)
        {
            if (type.IsGenericType)
            {
                if (type.Name == "ICollection" || type.Name == "IEnumerable")
                {
                    return $"{GetTSType(type.TypeArguments.First() as INamedTypeSymbol)}[]";
                }

                if (type.Name == "Nullable")
                {
                    return GetTSType(type.TypeArguments.First() as INamedTypeSymbol);
                }

                if (type.Name == "Nullable")
                {
                    return GetTSType(type.TypeArguments.First() as INamedTypeSymbol);
                }

                if (type.Name == "IDictionary")
                {
                    return $"{{[key: string]: {GetTSType(type.TypeArguments.Last() as INamedTypeSymbol)}}}";
                }

                if (type.Name == "QueryInput")
                {
                    return $"QueryInput<{GetTSType(type.TypeArguments.First() as INamedTypeSymbol)}>";
                }

                if (type.Name == "QueryOutput")
                {
                    return $"QueryOutput<{GetTSType(type.TypeArguments.First() as INamedTypeSymbol)}>";
                }

                return $"{type.Name}<{GetTSType(type.TypeArguments.First() as INamedTypeSymbol)}>";
            }

            if (type.Name == "QueryInput")
            {
                return "QueryInput";
            }

            if (type.Name == "QueryOutput")
            {
                return "QueryOutput";
            }

            if (type.Name == "IActionResult")
            {
                return "any";
            }

            switch (type.SpecialType)
            {
                case SpecialType.None:
                    return type.Name;
                case SpecialType.System_Int32:
                case SpecialType.System_Decimal:
                    return "number";
                case SpecialType.System_DateTime:
                case SpecialType.System_String:
                    return "string";
                case SpecialType.System_Boolean:
                    return "boolean";
                case SpecialType.System_Void:
                    return "void";
            }

            return "any";
        }

        /// <summary>
        /// Vérifie que le type est un array.
        /// </summary>
        /// <param name="type">Type.</param>
        /// <returns>Oui / Non.</returns>
        private static bool IsArray(INamedTypeSymbol type)
        {
            return type.IsGenericType && (type.Name == "ICollection" || type.Name == "IEnumerable");
        }

        /// <summary>
        /// Vérifie que le type est une primitive.
        /// </summary>
        /// <param name="type">Type.</param>
        /// <returns>Oui / Non.</returns>
        private static bool IsPrimitive(INamedTypeSymbol type)
        {
            return type.SpecialType != SpecialType.None;
        }

        /// <summary>
        /// Récupère la liste d'imports de types pour les services.
        /// </summary>
        /// <returns>La liste d'imports (type, chemin du module).</returns>
        private ICollection<(string import, string path)> GetImportList()
        {
            var definitionPath = GetModulePathPrefix("model", FolderCount + 1);

            var returnTypes = Services.SelectMany(service => GetTypes(service.ReturnType));
            var parameterTypes = Services.SelectMany(service => service.Parameters.SelectMany(parameter => GetTypes(parameter.Type)));

            var types = returnTypes.Concat(parameterTypes)
                .Where(type =>
                    !type.ContainingNamespace.ToString().Contains("Kinetix")
                 && !type.ContainingNamespace.ToString().Contains("System")
                 && !type.ContainingNamespace.ToString().Contains("Microsoft"));

            var referenceTypes = types.Where(type =>
                type.DeclaringSyntaxReferences.Any(s =>
                {
                    var classDecl = s.SyntaxTree
                        .GetRoot()
                        .DescendantNodes()
                        .OfType<ClassDeclarationSyntax>()
                        .First();
                    var hasRefAttribute = classDecl
                        .AttributeLists
                        .FirstOrDefault()
                        ?.Attributes
                        .Any(attr => attr.Name.ToString() == "Reference") ?? false;

                    if (!hasRefAttribute)
                    {
                        return false;
                    }
                    else
                    {
                        return !classDecl
                            .Members
                            .OfType<PropertyDeclarationSyntax>()
                            .Any(p => p.Identifier.ToString() == "Id");
                    }
                }));

            var imports = new List<(string import, string path)>();

            if (returnTypes.Any(type => type?.Name == "QueryOutput"))
            {
                imports.Add(("QueryInput, QueryOutput", "focus4/collections"));
            }
            else if (parameterTypes.Any(type => type?.Name == "QueryInput"))
            {
                imports.Add(("QueryInput", "focus4/collections"));
            }

            if (returnTypes.Any(type => type?.Name == "AutocompleteResult"))
            {
                imports.Add(("AutocompleteResult", "focus4/components/autocomplete"));
            }

            var localImports = types.Except(referenceTypes).Select(type =>
            {
                var module = type.ContainingNamespace.ToString()
                    .Replace($"{ProjectName}.", string.Empty)
                    .Replace("DataContract", string.Empty)
                    .Replace("Contract", string.Empty)
                    .Replace(".", "/")
                    .ToDashCase();

                return (type.Name, Path: $"{definitionPath}/{module}/{type.Name.ToDashCase()}");
            }).Distinct();

            if (referenceTypes.Any())
            {
                localImports = localImports.Concat(new[] { (string.Join(", ", referenceTypes.Select(t => t.Name).OrderBy(x => x)), $"{definitionPath}/references") });
            }

            imports.AddRange(localImports.OrderBy(i => i.Path));
            return imports;
        }

        /// <summary>
        /// Récupère tous les types et sous-types constitutants d'un type donné (génériques).
        /// </summary>
        /// <param name="type">le type d'entrée.</param>
        /// <returns>Les types de sorties.</returns>
        private IEnumerable<INamedTypeSymbol> GetTypes(INamedTypeSymbol type)
        {
            if (type != null && type.SpecialType == SpecialType.None)
            {
                yield return type;
                if (type.IsGenericType)
                {
                    foreach (var typeArg in type.TypeArguments)
                    {
                        if (typeArg is INamedTypeSymbol namedTypeArg)
                        {
                            foreach (var subTypeArg in GetTypes(namedTypeArg))
                            {
                                yield return subTypeArg;
                            }
                        }
                    }
                }
            }
        }

        private string GetModulePathPrefix(string module, int count)
        {
            if (count == 0)
            {
                return $"./{module}";
            }

            return $"{string.Join(string.Empty, Enumerable.Range(0, count).Select(_ => "../"))}{module}";
        }
    }
}
