using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Kinetix.SpaServiceGenerator.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace Kinetix.SpaServiceGenerator
{
    /// <summary>
    /// Programme.
    /// </summary>
    internal class Program
    {
        /// <summary>
        /// Point d'entrée.
        /// </summary>
        /// <param name="args">
        /// Premier argument : chemin de la solution.
        /// Deuxième argument : racine de la SPA.
        /// Troisième argument : namespace du projet (exemple : "Kinetix").
        /// </param>
        public static async Task Main(string[] args)
        {
            var solutionPath = args[0];
            var spaRoot = args[1];
            var projectName = args[2];

            var msWorkspace = MSBuildWorkspace.Create();
            var solution = await msWorkspace.OpenSolutionAsync(args[0]);

            var outputPath = $"{spaRoot}/app/services";

            var frontEnds = solution.Projects.Where(projet => projet.AssemblyName.StartsWith(projectName) && projet.AssemblyName.EndsWith("FrontEnd"));
            var controllers = frontEnds.SelectMany(f => f.Documents).Where(document => document.Name.Contains("Controller"));

            foreach (var controller in controllers)
            {
                var syntaxTree = await controller.GetSyntaxTreeAsync();
                var controllerClass = GetClassDeclaration(syntaxTree);

                var firstFolder = frontEnds.Count() > 1 ? $"/{controller.Project.Name.Split('.')[1].ToDashCase()}" : string.Empty;
                var secondFolder = controller.Folders.Count > 1 ? $"/{string.Join("/", controller.Folders.Skip(1).Select(f => f.ToDashCase()))}" : string.Empty;
                var folderCount = (frontEnds.Count() > 1 ? 1 : 0) + controller.Folders.Count - 1;

                var controllerName = $"{firstFolder}{secondFolder}/{controllerClass.Identifier.ToString().Replace("Controller", string.Empty).ToDashCase()}.ts".Substring(1);

                var model = await solution.GetDocument(syntaxTree).GetSemanticModelAsync();

                Console.WriteLine($"Generating {controllerName}");

                var methods = GetMethodDeclarations(controllerClass, model);

                var modelClass = model.GetDeclaredSymbol(controllerClass);
                if (modelClass.BaseType.Name != "Controller")
                {
                    var baseClassSyntaxTree = modelClass.BaseType.DeclaringSyntaxReferences.First().SyntaxTree;
                    methods = methods.Concat(GetMethodDeclarations(
                        GetClassDeclaration(baseClassSyntaxTree),
                        await solution.GetDocument(baseClassSyntaxTree).GetSemanticModelAsync()));
                }

                var serviceList = methods
                    .Where(m => m.method.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)))
                    .Select(GetService)
                    .ToList();

                var fileName = $"{outputPath}/{controllerName}";
                var fileInfo = new FileInfo(fileName);

                var directoryInfo = fileInfo.Directory;
                if (!directoryInfo.Exists)
                {
                    Directory.CreateDirectory(directoryInfo.FullName);
                }

                var template = new ServiceSpa { ProjectName = projectName, FolderCount = folderCount, Services = serviceList };
                var output = template.TransformText();
                File.WriteAllText(fileName, output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
        }

        private static ClassDeclarationSyntax GetClassDeclaration(SyntaxTree syntaxTree) => syntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        private static IEnumerable<(MethodDeclarationSyntax method, SemanticModel model)> GetMethodDeclarations(ClassDeclarationSyntax controllerClass, SemanticModel model) => controllerClass.ChildNodes().OfType<MethodDeclarationSyntax>().Select(method => (method, model));

        private static ServiceDeclaration GetService((MethodDeclarationSyntax method, SemanticModel model) m)
        {
            var (method, model) = m;

            var documentation = method.GetLeadingTrivia()
                .First(i => i.GetStructure() is DocumentationCommentTriviaSyntax)
                .GetStructure() as DocumentationCommentTriviaSyntax;

            var summary = (documentation.Content.First(content =>
                content.ToString().StartsWith("<summary>", StringComparison.Ordinal)) as XmlElementSyntax).Content.ToString()
                .Replace("/// ", string.Empty).Replace("\r\n       ", string.Empty).Trim();

            var parameters = documentation.Content.Where(content => content.ToString().StartsWith("<param", StringComparison.Ordinal))
                .Select(param => Tuple.Create(
                    ((param as XmlElementSyntax).StartTag.Attributes.First() as XmlNameAttributeSyntax).Identifier.ToString(),
                    (param as XmlElementSyntax).Content.ToString()));

            var verbRouteAttribute = method.AttributeLists
                .SelectMany(list => list.Attributes)
                .Single(attr => attr.ToString().StartsWith("Http"));

            var verb = verbRouteAttribute.Name.ToString();

            var parameterList = method.ParameterList.ChildNodes().OfType<ParameterSyntax>().Select(parameter => new Parameter
            {
                Type = model.GetSymbolInfo(parameter.Type).Symbol as INamedTypeSymbol,
                Name = parameter.Identifier.ToString(),
                IsOptional = parameter.Default != null,
                IsFromBody = parameter.AttributeLists.SelectMany(list => list.Attributes).Any(attr => attr.ToString() == "FromBody"),
                IsFromUri = parameter.AttributeLists.SelectMany(list => list.Attributes).Any(attr => attr.ToString() == "FromUrl")
            }).ToList();

            var routeParameters = new List<string>();
            var route = ((verbRouteAttribute.ArgumentList.ChildNodes().First() as AttributeArgumentSyntax)
                    .Expression as LiteralExpressionSyntax).Token.ValueText;
            var matches = Regex.Matches(route, "(?s){.+?}");
            foreach (Match match in matches)
            {
                routeParameters.Add(match.Value.Replace("{", "").Replace("}", ""));
            }

            var uriParameters = parameterList.Where(param => !param.IsFromBody && routeParameters.Contains(param.Name));
            var queryParameters = parameterList.Where(param => !param.IsFromBody && !routeParameters.Contains(param.Name));

            var bodyParameters = new List<Parameter>();
            if ((verb == "HttpPost" || verb == "HttpPut") && parameterList.Except(uriParameters).Any())
            {
                var bodyParams = parameterList
                    .Except(uriParameters)
                    .Where(param => param.IsFromBody)
                    // Concat here as a fallback (use of first below)
                    .Concat(parameterList.Where(param => !param.IsFromUri));

                if (bodyParams.Any())
                {
                    bodyParameters.Add(bodyParams.First());
                }

                queryParameters = queryParameters
                    .Where(param => !bodyParameters.Select(body => body.Name).Contains(param.Name));
            }

            return new ServiceDeclaration
            {
                Verb = verb,
                Route = route,
                Name = method.Identifier.ToString(),
                ReturnType = model.GetSymbolInfo(method.ReturnType).Symbol as INamedTypeSymbol,
                Parameters = parameterList,
                UriParameters = uriParameters.ToList(),
                QueryParameters = queryParameters.ToList(),
                BodyParameters = bodyParameters,
                Documentation = new Documentation { Summary = summary, Parameters = parameters.ToList() },
                IsPostPutMethod = verb == "HttpPost" || verb == "HttpPut"
            };
        }
    }
}
