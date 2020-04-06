using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Kinetix.SpaServiceGenerator.Model;
using Kinetix.Tools.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Kinetix.SpaServiceGenerator
{
    /// <summary>
    /// Class to produce the template output
    /// </summary>
    public partial class ServiceSpa : TemplateBase
    {
        public string ModelRoot { get; set; }
        public string FetchPath { get; set; }

        /// <summary>
        /// Le chemin vers le répertoire de définitions.
        /// </summary>
        public int FolderCount { get; set; }

        /// <summary>
        /// Le nom du projet, namespace global (exemple : "Chaine").
        /// </summary>
        public string ProjectName { get; set; }


        /// <summary>
        /// La liste des services.
        /// </summary>
        public ICollection<ServiceDeclaration> Services { get; set; }

        /// <summary>
        /// Create the template output
        /// </summary>
        public virtual string TransformText()
        {
            Write("/*\r\n    Ce fichier a été généré automatiquement.\r\n    Toute modification sera per" +
                    "due.\r\n*/\r\n\r\nimport {fetch} from \"");

            Write(GetModulePathPrefix(FetchPath, FolderCount + 1));
            Write("\";\r\n");

            if (GetImportList().Any())
            {
                Write("\r\n");
            }

            foreach (var import in GetImportList())
            {
                Write("import {");
                Write(import.import);
                Write("} from \"");
                Write(import.path);
                Write("\";\r\n");
            }

            foreach (var service in Services)
            {
                Write("\r\n/**\r\n * ");
                Write(service.Documentation.Summary);
                Write("\r\n");

                foreach (var param in service.Documentation.Parameters)
                {
                    Write(" * @param ");
                    Write(param.Item1);
                    Write(" ");
                    Write(param.Item2);
                    Write("\r\n");
                }

                Write(" * @param options Fetch options.\r\n */\r\nexport function ");
                Write(service.Name.ToFirstLower());
                Write("(");

                foreach (var parameter in service.Parameters)
                {
                    Write(parameter.Name);
                    Write(parameter.IsOptional ? "?" : "");
                    Write(": ");
                    Write(TSUtils.CSharpToTSType(parameter.Type));

                    if (parameter.Name != service.Parameters.Last().Name)
                    {
                        Write(", ");
                    }
                }
                if (service.Parameters.Count() > 0)
                {
                    Write(", ");
                }

                Write("options: RequestInit = {}): Promise<");
                Write(TSUtils.CSharpToTSType(service.ReturnType));
                Write("> {\r\n");

                if (service.IsFormData)
                {
                    Write("    const body = new FormData();\r\n");
                    foreach (var parameter in service.Parameters)
                    {
                        if (parameter.Type.Name == "IFormFile")
                        {
                            Write($@"    body.append(""{parameter.Name}"", {parameter.Name})");
                        }
                        else
                        {
                            Write($@"    for (const key in {parameter.Name}) {{
        body.append(key, ({parameter.Name} as any)[key]);
    }}");
                        }

                        Write("\r\n");
                    }
                }

                Write("    return fetch(\"");
                Write(service.Verb);
                Write("\", `./");
                Write(Regex.Replace(service.Route.Replace("{", "${"), ":([a-z]+)", string.Empty));
                Write("`, {");

                if (service.IsFormData)
                {
                    Write("body");
                }
                else if (service.BodyParameter != null)
                {
                    Write("body: ");
                    Write(service.BodyParameter.Name);
                }

                if (service.BodyParameter != null && service.QueryParameters.Any())
                {
                    Write(", ");
                }

                if (!service.IsFormData && service.QueryParameters.Any())
                {
                    Write("query: {");

                    foreach (var qParam in service.QueryParameters)
                    {
                        Write(qParam.Name);

                        if (qParam.Name != service.QueryParameters.Last().Name)
                        {
                            Write(", ");
                        }
                    }

                    Write("}");
                }

                Write("}, options);\r\n}\r\n");
            }

            return GenerationEnvironment.ToString();
        }

        /// <summary>
        /// Récupère la liste d'imports de types pour les services.
        /// </summary>
        /// <returns>La liste d'imports (type, chemin du module).</returns>
        private ICollection<(string import, string path)> GetImportList()
        {
            var definitionPath = GetModulePathPrefix(ModelRoot, FolderCount + 1);

            var returnTypes = Services.SelectMany(service => GetTypes(service.ReturnType));
            var parameterTypes = Services.SelectMany(service => service.Parameters.SelectMany(parameter => GetTypes(parameter.Type)));

            var types = returnTypes.Concat(parameterTypes)
                .Where(type =>
                    !type.ContainingNamespace.ToString().Contains("Kinetix")
                 && !type.ContainingNamespace.ToString().Contains("Fmk")
                 && !type.ContainingNamespace.ToString().Contains("System")
                 && !type.ContainingNamespace.ToString().Contains("Microsoft")
                 && type.Name != "AutocompleteResult");

            var referenceTypes = types
                .Where(type =>
                    type.DeclaringSyntaxReferences.Any(s =>
                    {
                        var classDecl = s.SyntaxTree
                            .GetRoot()
                            .DescendantNodes()
                            .OfType<ClassDeclarationSyntax>()
                            .First();

                        var hasRefAttribute = classDecl
                            .AttributeLists.SelectMany(l => l.Attributes)
                            .Any(attr => attr.Name.ToString() == "Reference");

                        return !hasRefAttribute
                            ? false
                            : !classDecl
                                .Members
                                .OfType<PropertyDeclarationSyntax>()
                                .Any(p => p.Identifier.ToString() == "Id");
                    }))
                .Distinct();

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
                imports.Add(("AutocompleteResult", "focus4/components"));
            }

            var localImports = types.Except(referenceTypes).Select(type =>
                 (type.Name, Path: $"{definitionPath}/{GetModuleName(type)}/{type.Name.ToDashCase()}"))
            .Distinct();

            if (referenceTypes.Any())
            {
                var referenceTypeMap = referenceTypes.GroupBy(t => GetModuleName(t));
                foreach (var refModule in referenceTypeMap)
                {
                    localImports = localImports.Concat(new[] { (string.Join(", ", refModule.Select(t => t.Name).OrderBy(x => x)), $"{definitionPath}/{refModule.Key}/references") });
                }
            }

            imports.AddRange(localImports.OrderBy(i => i.Path));
            return imports;
        }

        private string GetModuleName(INamedTypeSymbol type)
        {
            var fixedProjectName = $"{ProjectName.Substring(0, 1).ToUpper()}{ProjectName.Substring(1).ToLowerInvariant()}";

            var module = type.ContainingNamespace.ToString()
                .Replace($"{fixedProjectName}.", string.Empty)
                .Replace("DataContract", string.Empty)
                .Replace("Contract", string.Empty)
                .Replace(".", "/")
                .ToDashCase();
            return module;
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
            return count == 0 ? $"./{module}" : $"{string.Join(string.Empty, Enumerable.Range(0, count).Select(_ => "../"))}{module}";
        }
    }
}
