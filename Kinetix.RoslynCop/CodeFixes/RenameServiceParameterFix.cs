﻿using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using Kinetix.RoslynCop.Common;
using Kinetix.RoslynCop.Diagnostics.Design;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;

namespace Kinetix.RoslynCop.CodeFixes
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ReplaceImplementationWithContractFix)), Shared]
    public class RenameServiceParameterFix : CodeFixProvider
    {
        private const string title = "Renommer en {0}";

        public sealed override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(FRC1501_ServiceCtrParameterNamingAnalyser.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            /* Récupère le node de field. */
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var node = root.FindNode(diagnosticSpan).AncestorsAndSelf().OfType<ParameterSyntax>().FirstOrDefault();
            if (node == null)
            {
                return;
            }

            var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken);
            if (!(semanticModel.GetTypeInfo(node.Type, context.CancellationToken).Type is INamedTypeSymbol namedTypeSymbol))
            {
                return;
            }
            if (!namedTypeSymbol.IsServiceContract())
            {
                return;
            }

            var typeName = namedTypeSymbol.Name;
            var isContract = typeName.IsServiceContractName();
            if (!isContract)
            {
                return;
            }

            /* Symbole à renommer. */
            var parameterSymbol = semanticModel.GetDeclaredSymbol(node, context.CancellationToken);

            /* Nouveau nom. */
            var newName = typeName.GetServiceContractParameterName();

            var titleFormat = string.Format(title, newName);
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: titleFormat,
                    createChangedSolution: c => RenameParameter(context.Document, parameterSymbol, newName),
                    equivalenceKey: titleFormat),
                diagnostic);
        }

        private static async Task<Solution> RenameParameter(Document document, ISymbol symbol, string newName)
        {
            var solution = document.Project.Solution;
            var options = solution.Workspace.Options;
            return await Renamer.RenameSymbolAsync(solution, symbol, newName, options);
        }
    }
}