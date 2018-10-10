﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
#if NETCOREAPP2_1
using Buildalyzer;
using Buildalyzer.Workspaces;
#endif
using Kinetix.SpaServiceGenerator.Model;
using Kinetix.Tools.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
#if NET471
using Microsoft.CodeAnalysis.MSBuild;
#endif

namespace Kinetix.SpaServiceGenerator
{
    /// <summary>
    /// Programme.
    /// </summary>
    internal class Program
    {
        private static string _solutionPath;
        private static string _serviceRoot;
        private static string _projectName;
        private static string _kinetix;

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
            _solutionPath = args[0];
            _serviceRoot = args[1];
            _projectName = args[2];
            _kinetix = args[3];


            Solution solution = null;
#if NET471
            var msWorkspace = MSBuildWorkspace.Create(); 
            msWorkspace.WorkspaceFailed += MsWorkspace_WorkspaceFailed;
            solution = await msWorkspace.OpenSolutionAsync(_solutionPath);           
#endif

#if NETCOREAPP2_1
            var adhocWorkspace = new AnalyzerManager(_solutionPath).GetWorkspace();
            adhocWorkspace.WorkspaceFailed += AdhocWorkspace_WorkspaceFailed;
            solution = adhocWorkspace.CurrentSolution;

            // Weirdly, I have a lot of duplicate project references after loading a solution, so this is a quick hack to fix that.
            foreach (var project in solution.Projects)
            {
                solution = solution.WithProjectReferences(project.Id, project.ProjectReferences.Distinct());
            }
#endif

            // If path is not to services add "standard" path 
            var outputPath = _serviceRoot.EndsWith("services") ? _serviceRoot : $"{_serviceRoot}/app/services";

            var frontEnds = solution.Projects.Where(projet => projet.AssemblyName.StartsWith(_projectName) && projet.AssemblyName.EndsWith("FrontEnd"));
            var controllers = frontEnds.SelectMany(f => f.Documents).Where(document =>
                document.Name.Contains("Controller")
                && !document.Folders.Contains("Transverse"));

            foreach (var controller in controllers)
            {
                var syntaxTree = await controller.GetSyntaxTreeAsync();
                var controllerClass = GetClassDeclaration(syntaxTree);
                var model = await solution.GetDocument(syntaxTree).GetSemanticModelAsync();
                var modelClass = model.GetDeclaredSymbol(controllerClass);

                // Skip if we extend a MVC controller
                if (_kinetix == "framework" && modelClass.BaseType.Name == "Controller")
                {
                    continue;
                }

                IReadOnlyList<string> folders = null;
#if NET471
                folders = controller.Folders;
#endif
#if NETCOREAPP2_1
                var parts = Path.GetRelativePath(controller.Project.FilePath, controller.FilePath).Split(@"\");
                folders = parts.Skip(1).Take(parts.Length - 2).ToList();
#endif

                var firstFolder = frontEnds.Count() > 1 ? $"/{controller.Project.Name.Split('.')[1].ToDashCase()}" : string.Empty;
                var secondFolder = folders.Count > 1 ? $"/{string.Join("/", folders.Skip(1).Select(f => f.ToDashCase()))}" : string.Empty;
                var folderCount = (frontEnds.Count() > 1 ? 1 : 0) + folders.Count - 1;

                var controllerName = $"{firstFolder}{secondFolder}/{controllerClass.Identifier.ToString().Replace("Controller", string.Empty).ToDashCase()}.ts".Substring(1);

                Console.WriteLine($"Generating {controllerName}");

                var methods = GetMethodDeclarations(controllerClass, model);

                // If controller is not a direct Controller extender, ie it extends a base class
                string aspControllerClass = _kinetix == "Core" ? "Controller" : "ApiController";
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

                var fileName = $"{outputPath}/{controllerName}";
                var fileInfo = new FileInfo(fileName);

                var directoryInfo = fileInfo.Directory;
                if (!directoryInfo.Exists)
                {
                    Directory.CreateDirectory(directoryInfo.FullName);
                }

                var template = new ServiceSpa { ProjectName = _projectName, FolderCount = folderCount, Services = serviceList };
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

        private static ClassDeclarationSyntax GetClassDeclaration(SyntaxTree syntaxTree) => syntaxTree.GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>().First();
        private static IEnumerable<(MethodDeclarationSyntax method, SemanticModel model)> GetMethodDeclarations(ClassDeclarationSyntax controllerClass, SemanticModel model) => controllerClass.ChildNodes().OfType<MethodDeclarationSyntax>().Select(method => (method, model));

        private static ServiceDeclaration GetService((MethodDeclarationSyntax method, SemanticModel model) m)
        {
            var (method, model) = m;

            var returnType = model.GetSymbolInfo(method.ReturnType).Symbol as INamedTypeSymbol;

            if (returnType.Name == "Task")
            {
                returnType = returnType.TypeArguments.First() as INamedTypeSymbol;
            }

            if (returnType.Name.Contains("Redirect"))
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
                IsFromBody = parameter.AttributeLists.SelectMany(list => list.Attributes).Any(attr => attr.ToString() == "FromBody")
            }).ToList();

            var routeParameters = new List<string>();

            string route;
            if (_kinetix == "Core")
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
