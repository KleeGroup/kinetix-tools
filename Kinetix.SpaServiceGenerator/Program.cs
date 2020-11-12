using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Kinetix.SpaServiceGenerator.Model;
using Kinetix.Tools.Common;
using Kinetix.Tools.Common.Parameters;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using Newtonsoft.Json;

namespace Kinetix.SpaServiceGenerator
{
    /// <summary>
    /// Programme.
    /// </summary>
    internal class Program
    {
        public static ServiceParameters Parameters { get; set; }

        /// <summary>
        /// Point d'entrée.
        /// </summary>
        /// <param name="args">Le fichier de config.</param>
        public static async Task Main(string[] args)
        {
            // Lecture des paramètres d'entrée.
            var configPath = args[0];
            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException("Le fichier de configuration n'existe pas");
            }

            var configText = File.ReadAllText(configPath);
            Parameters = JsonConvert.DeserializeObject<ServiceParameters>(configText);

            if (Parameters.SolutionPath == null)
            {
                throw new ArgumentNullException(nameof(ServiceParameters.SolutionPath));
            }

            if (Parameters.RootNamespace == null)
            {
                throw new ArgumentNullException(nameof(ServiceParameters.RootNamespace));
            }

            if (Parameters.OutputDirectory == null)
            {
                throw new ArgumentNullException(nameof(ServiceParameters.OutputDirectory));
            }

            Parameters.ModelRoot ??= "model";
            Parameters.ProjectsSuffix ??= "FrontEnd";
            Parameters.FetchPath ??= "services/server";
            Parameters.SplitIntoApps ??= false;
            Parameters.Kinetix = Parameters.Kinetix?.ToLower() ?? "core";

            var instance = MSBuildLocator.QueryVisualStudioInstances().First();
            Console.WriteLine($"Using MSBuild at '{instance.MSBuildPath}' to load projects.");
            MSBuildLocator.RegisterInstance(instance);
            var msWorkspace = MSBuildWorkspace.Create();

            msWorkspace.WorkspaceFailed += MsWorkspace_WorkspaceFailed;
            var solution = await msWorkspace.OpenSolutionAsync(Parameters.SolutionPath);

            var frontEnds = solution.Projects.Where(projet => projet.AssemblyName.StartsWith(Parameters.RootNamespace) && projet.AssemblyName.EndsWith(Parameters.ProjectsSuffix));
            var controllers = frontEnds.SelectMany(f => f.Documents).Where(document =>
                document.Name.Contains("Controller")
                && !document.Folders.Contains("Transverse"));

            Console.WriteLine($"{controllers.Count()} contrôleurs trouvés");

            foreach (var controller in controllers)
            {
                var syntaxTree = await controller.GetSyntaxTreeAsync();
                var controllerClass = GetClassDeclaration(syntaxTree);
                var model = await solution.GetDocument(syntaxTree).GetSemanticModelAsync();
                var modelClass = model.GetDeclaredSymbol(controllerClass);

                // Skip if we extend a MVC controller
                if (Parameters.Kinetix == "framework" && modelClass.BaseType.Name == "Controller")
                {
                    continue;
                }

                var folders = controller.Folders;

                var frontEndName = frontEnds.Count() > 1 ? $"/{controller.Project.Name.Split('.')[1].ToDashCase()}" : string.Empty;
                var folderNames = folders.Count > 1 ? $"{string.Join("/", folders.Skip(1).Select(f => f.ToDashCase()))}/" : string.Empty;
                var folderCount = (frontEnds.Count() > 1 ? 1 : 0) + folders.Count - 1;

                var controllerName = $"{folderNames}{controllerClass.Identifier.ToString().Replace("Controller", string.Empty).ToDashCase()}.ts";

                var fileName = Parameters.SplitIntoApps == true
                    ? $"{Parameters.OutputDirectory}{frontEndName}/services/{controllerName}"
                    : $"{Parameters.OutputDirectory}/services{frontEndName}/{controllerName}";

                Console.WriteLine($"Generating {fileName}");

                var methods = GetMethodDeclarations(controllerClass, model);

                // If controller is not a direct Controller extender, ie it extends a base class
                var aspControllerClass = Parameters.Kinetix == "framework" ? "ApiController" : "Controller";
                if (modelClass.BaseType.Name != aspControllerClass)
                {
                    var baseClassSyntaxTree = modelClass.BaseType.DeclaringSyntaxReferences.First().SyntaxTree;
                    methods = methods.Concat(GetMethodDeclarations(
                        GetClassDeclaration(baseClassSyntaxTree),
                        await solution.GetDocument(baseClassSyntaxTree).GetSemanticModelAsync()));
                }

                var serviceList = methods
                    .Where(m => m.method.Modifiers.Any(mod => mod.IsKind(SyntaxKind.PublicKeyword)))
                    .Select(GetService)
                    .Where(s => s != null)
                    .ToList();

                var fileInfo = new FileInfo(fileName);

                var directoryInfo = fileInfo.Directory;
                if (!directoryInfo.Exists)
                {
                    Directory.CreateDirectory(directoryInfo.FullName);
                }

                var template = new ServiceSpa
                {
                    ProjectName = Parameters.RootNamespace,
                    ModelRoot = Parameters.ModelRoot,
                    FetchPath = Parameters.FetchPath,
                    FolderCount = folderCount,
                    Services = serviceList
                };
                var output = template.TransformText();
                File.WriteAllText(fileName, output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
        }

        private static void AdhocWorkspace_WorkspaceFailed(object sender, WorkspaceDiagnosticEventArgs e)
        {
            Console.WriteLine(e.Diagnostic.Message);
        }

        private static void MsWorkspace_WorkspaceFailed(object sender, WorkspaceDiagnosticEventArgs e)
        {
            Console.WriteLine(e.Diagnostic.Message);
        }

        private static ClassDeclarationSyntax GetClassDeclaration(SyntaxTree syntaxTree)
        {
            return syntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        }

        private static IEnumerable<(MethodDeclarationSyntax method, SemanticModel model)> GetMethodDeclarations(ClassDeclarationSyntax controllerClass, SemanticModel model)
        {
            return controllerClass.ChildNodes().OfType<MethodDeclarationSyntax>().Select(method => (method, model));
        }

        private static ServiceDeclaration GetService((MethodDeclarationSyntax method, SemanticModel model) m)
        {
            var (method, model) = m;

            var returnType = model.GetSymbolInfo(method.ReturnType).Symbol as INamedTypeSymbol;

            if (returnType.Name == "Task")
            {
                returnType = returnType.TypeArguments.FirstOrDefault() as INamedTypeSymbol;
            }

            if ((returnType?.Name.Contains("Redirect") ?? false) || (returnType?.Name.Contains("FileContentResult") ?? false))
            {
                return null;
            }

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

            var verb = verbRouteAttribute.Name.ToString().Substring(4).ToUpper();

            var parameterList = method.ParameterList.ChildNodes().OfType<ParameterSyntax>().Select(parameter => new Parameter
            {
                Type = model.GetSymbolInfo(parameter.Type).Symbol as INamedTypeSymbol,
                Name = parameter.Identifier.ToString(),
                IsOptional = parameter.Default != null,
                IsFromBody = parameter.AttributeLists.SelectMany(list => list.Attributes).Any(attr => attr.ToString() == "FromBody"),
                IsFormData = parameter.AttributeLists.SelectMany(list => list.Attributes).Any(attr => attr.ToString() == "FromForm")
            }).ToList();

            var routeParameters = new List<string>();

            string route;
            if (Parameters.Kinetix != "framework")
            {
                route = ((verbRouteAttribute.ArgumentList.ChildNodes().First() as AttributeArgumentSyntax)
                        .Expression as LiteralExpressionSyntax)
                        .Token.ValueText;
            }
            else
            {
                var routeAttribute = method.AttributeLists
                    .SelectMany(list => list.Attributes)
                    .Single(attr => attr.ToString().StartsWith("Route"));

                route = ((routeAttribute.ArgumentList.ChildNodes().First() as AttributeArgumentSyntax)
                    .Expression as LiteralExpressionSyntax)
                    .Token.ValueText;
            }

            var matches = Regex.Matches(route, "(?s){.+?}");
            foreach (Match match in matches)
            {
                routeParameters.Add(match.Value.Replace("{", "").Replace("}", ""));
            }

            return new ServiceDeclaration
            {
                Verb = verb,
                Route = route,
                Name = method.Identifier.ToString(),
                ReturnType = returnType,
                Parameters = parameterList,
                UriParameters = parameterList.Where(param => !param.IsFromBody && routeParameters.Contains(param.Name)).ToList(),
                QueryParameters = parameterList.Where(param => !param.IsFromBody && !routeParameters.Contains(param.Name)).ToList(),
                BodyParameter = parameterList.SingleOrDefault(param => param.IsFromBody),
                Documentation = new Documentation { Summary = summary, Parameters = parameters.ToList() }
            };
        }
    }
}
