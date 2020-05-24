using System;
using System.Collections.Generic;
using System.Linq;
using Kinetix.RoslynCop.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Kinetix.RoslynCop.CodeFixes.Test
{
    public static class TestGenerator
    {
        public static (string fileName, string folder, string content) GenerateTest(IMethodSymbol methSymbol, ClassDeclarationSyntax classDecl, DalTestStrategy strategy)
        {
            /* Dossier du document. */
            var classTestDir = classDecl.GetClassName() + "Test";

            /* Nom du document. */
            var methTestFile = methSymbol.Name + "Test";

            /* Contenu du document. */
            var classSymbol = methSymbol.ContainingType;
            var applicationName = classSymbol.ContainingAssembly.GetApplicationName();
            var template = new DalTestTemplate(new DalMethodItem
            {
                DalClassName = classSymbol.Name,
                DalMethodName = methSymbol.Name,
                DalAssemblyName = classSymbol.ContainingAssembly.Name,
                DalNamespace = classDecl.GetNameSpaceFullName(),
                Params = methSymbol.Parameters.Select(x => GetParameter(x)).ToList(),
                SpecificUsings = new List<string> { $"{applicationName}.Business.Common.Test" }
            });

            var content = template.Render(strategy);

            return (methTestFile, classTestDir, content);
        }

        public static bool ShouldGenerateTest(IMethodSymbol methSymbol, INamedTypeSymbol classSymbol, Document document)
        {
            if (methSymbol == null || classSymbol == null)
            {
                return false;
            }

            if (!classSymbol.IsDalImplementation())
            {
                return false;
            }

            var classTestDir = classSymbol.Name + "Test";
            var methTestFile = methSymbol.Name + "Test.cs";

            /* Trouve le projet de test. */
            var testProjetName = document.Project.Name + ".Test";
            var testProject = document.Project.Solution.Projects.FirstOrDefault(x => x.Name == testProjetName);
            if (testProject == null)
            {
                return false;
            }

            /* Vérifie si le test n'existe pas déjà. */
            var folders = new List<string> { classTestDir };
            var hasTest = testProject.Documents.Any(x => x.Name == methTestFile && x.Folders.SequenceEqual(folders));

            return !hasTest;
        }

        /// <summary>
        /// Renvoie une expression de valeur factice pour un type donné.
        /// </summary>
        /// <param name="typeSymbol">Type.</param>
        /// <returns>Expression de la valeur factice.</returns>
        private static string GetDummyValue(ITypeSymbol typeSymbol)
        {
            var fullName = typeSymbol?.ToString();
            if (string.IsNullOrEmpty(fullName))
            {
                return "null";
            }

            switch (fullName)
            {
                case "System.Int32":
                case "int":
                case "int?":
                    return "Dum.Id";
                case "System.Int16":
                case "short":
                case "short?":
                    return "Dum.Short";
                case "System.Guid":
                case "Guid":
                case "Guid?":
                    return "Dum.Guid";
                case "System.String":
                case "string":
                    return "Dum.Code";
                case "System.DateTime":
                case "DateTime":
                case "DateTime?":
                    return "Dum.Date";
                case "System.Decimal":
                case "decimal":
                case "decimal?":
                    return "Dum.Montant";
                case "System.Boolean":
                case "bool":
                case "bool?":
                    return "Dum.Booleen";
                case "int[]":
                    return "Dum.IdArray";
                case "string[]":
                    return "Dum.StringArray";
                default:
                    /* Cas d'un nullable. */
                    if (fullName.StartsWith("System.Nullable", StringComparison.Ordinal))
                    {
                        var nullableType = (INamedTypeSymbol)typeSymbol;
                        var innerType = nullableType.TypeArguments.First();
                        return GetDummyValue(innerType);
                    }

                    /* Cas d'une collection. */
                    if (fullName.StartsWith("System.Collections.Generic.ICollection", StringComparison.Ordinal))
                    {
                        var collectionType = (INamedTypeSymbol)typeSymbol;
                        var innerType = collectionType.TypeArguments.First();
                        switch (innerType.ToString())
                        {
                            case "System.String":
                                return "Dum.StringList";
                            case "System.Int32":
                                return "Dum.IdList";
                        }

                        if (innerType.IsReferenceType)
                        {
                            return $"Dum.DumColl<{innerType.Name}>()";
                        }
                    }

                    /* Cas d'un bean. */
                    if (typeSymbol.IsReferenceType)
                    {
                        return $"Dum.Dum<{typeSymbol.Name}>()";
                    }

                    return "null";
            }
        }

        /// <summary>
        /// Renvoie les usings nécessaire pour l'appel du paramètre.
        /// </summary>
        /// <param name="typeSymbol">Type.</param>
        /// <returns>Expression de la valeur factice.</returns>
        private static ICollection<string> GetNameSpaces(ITypeSymbol typeSymbol)
        {
            var fullName = typeSymbol.ToString();
            switch (fullName)
            {
                case "System.Int32":
                case "System.String":
                case "System.DateTime":
                case "System.Boolean":
                    return new List<string>();
                default:
                    /* Cas d'une collection de beans. */
                    /* Cas d'une collection. */
                    if (fullName.StartsWith("System.Collections.Generic.ICollection", System.StringComparison.Ordinal))
                    {
                        var collectionType = (INamedTypeSymbol)typeSymbol;
                        var innerType = collectionType.TypeArguments.First();
                        switch (innerType.ToString())
                        {
                            case "System.String":
                            case "System.Int32":
                                return new List<string>();
                        }

                        if (innerType.IsReferenceType)
                        {
                            return new List<string> { innerType.ContainingNamespace.ToString() };
                        }
                    }

                    /* Cas d'un bean. */
                    if (typeSymbol.IsReferenceType && typeSymbol.ContainingNamespace != null)
                    {
                        return new List<string> { typeSymbol.ContainingNamespace.ToString() };
                    }

                    return new List<string>();
            }
        }

        /// <summary>
        /// Construit un paramètre de méthode de DAL.
        /// </summary>
        /// <param name="paramSymbol">Définition du paramètre.</param>
        /// <returns>Parémètre.</returns>
        private static DalMethodParam GetParameter(IParameterSymbol paramSymbol)
        {
            return new DalMethodParam
            {
                Name = paramSymbol.Name,
                Value = GetDummyValue(paramSymbol.Type),
                SpecificUsings = GetNameSpaces(paramSymbol.Type)
            };
        }
    }
}
