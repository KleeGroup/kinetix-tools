using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Kinetix.RoslynCop.CodeFixes.Test;
using Kinetix.RoslynCop.Common;
using Kinetix.RoslynCop.Diagnostics.Coverage;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Kinetix.RoslynCop.CodeFixes
{

    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ReplaceImplementationWithContractFix))]
    [Shared]
    public class AddDalUnitTestFix : CodeFixProvider
    {

        private const string TitleSemantic = "Ajouter un test unitaire sémantique";
        private const string TitleStandard = "Ajouter un test unitaire standard";

        public sealed override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(FRC1300_DalMethodWithSqlServerCommandAnalyser.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var node = root.FindNode(context.Span);

            /* Filtre sur les méthodes publiques. */
            if (!(node is MethodDeclarationSyntax methDecl) || !methDecl.IsPublic())
            {
                return;
            }

            /* Vérifie qu'on est dans une implémentation de DAL. */
            if (!(methDecl.Parent is ClassDeclarationSyntax classDecl))
            {
                return;
            }

            var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken);
            var classSymbol = semanticModel.GetDeclaredSymbol(classDecl, context.CancellationToken);
            var methSymbol = semanticModel.GetDeclaredSymbol(methDecl, context.CancellationToken);

            if (!TestGenerator.ShouldGenerateTest(methSymbol, classSymbol, context.Document))
            {
                return;
            }

            /* Ajoute une action pour l'ajout du test unitaire sémantique. */
            context.RegisterCodeFix(
                    CodeAction.Create(
                        title: TitleSemantic,
                        createChangedSolution: c => Task.FromResult(AddUnitTest(context.Document, methSymbol, classDecl, DalTestStrategy.Semantic)),
                        equivalenceKey: TitleSemantic),
                    context.Diagnostics.First());

            /* Ajoute une action pour l'ajout du test unitaire standard. */
            context.RegisterCodeFix(
                    CodeAction.Create(
                        title: TitleStandard,
                        createChangedSolution: c => Task.FromResult(AddUnitTest(context.Document, methSymbol, classDecl, DalTestStrategy.Standard)),
                        equivalenceKey: TitleStandard),
                    context.Diagnostics.First());
        }

        private static Solution AddUnitTest(Document document, IMethodSymbol methSymbol, ClassDeclarationSyntax classDecl, DalTestStrategy strategy)
        {
            var testProjetName = document.Project.Name + ".Test";
            var solution = document.Project.Solution;

            var testProject = solution.Projects.FirstOrDefault(x => x.Name == testProjetName);
            if (testProject == null)
            {
                return solution;
            }

            var (fileName, folder, content) = TestGenerator.GenerateTest(methSymbol, classDecl, strategy);

            var newDoc = testProject.AddDocument(fileName, content, new List<string> { folder });

            return newDoc.Project.Solution;
        }
    }
}