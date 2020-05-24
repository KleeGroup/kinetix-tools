﻿using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kinetix.RoslynCop.Common;
using Kinetix.RoslynCop.Diagnostics.Design;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Kinetix.RoslynCop.CodeFixes
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(AddRegisterContractAttributeAttributeFix))]
    [Shared]
    public class AddRegisterContractAttributeAttributeFix : CodeFixProvider
    {
        private const string Title = "Décorer avec RegisterContract";

        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(
            FRC1109_RegisterContractClassDecorationAnalyser.DiagnosticId,
            FRC1110_DalContractClassDecorationAnalyser.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;

            var interfaceDecl = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<InterfaceDeclarationSyntax>().FirstOrDefault();
            if (interfaceDecl == null)
            {
                return;
            }

            // Register a code action that will invoke the fix.
            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Title,
                    createChangedDocument: c => AddAttributeAsync(context.Document, interfaceDecl, c),
                    equivalenceKey: Title),
                diagnostic);
        }

        private static async Task<Document> AddAttributeAsync(Document document, InterfaceDeclarationSyntax interfaceDecl, CancellationToken cancellationToken)
        {
            /* Créé l'attribut. */
            var newAttrList = SyntaxFactory.AttributeList(
                SyntaxFactory.SeparatedList(new[] { SyntaxFactory.Attribute(
                        SyntaxFactory.IdentifierName(
                            SyntaxFactory.Identifier(FrameworkNames.RegisterContract)))
                }));

            /* Récupère le trivia du premier token. */
            var initFirstToken = interfaceDecl.GetFirstToken();
            var initLeadingTrivia = initFirstToken.LeadingTrivia;

            /* Enlève le trivia du premier token. */
            var newInterfaceSyntax = interfaceDecl.ReplaceToken(
                initFirstToken,
                initFirstToken.WithLeadingTrivia(SyntaxFactory.Whitespace("\n")));

            /* Injecte le trivia sur le nouvel attribut. */
            newAttrList = newAttrList.WithLeadingTrivia(initLeadingTrivia);

            /* Ajoute l'attribut à la classe. */
            var newAttrLists = newInterfaceSyntax.AttributeLists.Insert(0, newAttrList);
            newInterfaceSyntax = newInterfaceSyntax.WithAttributeLists(newAttrLists);

            /* Remplace la classe. */
            var oldRoot = await document.GetSyntaxRootAsync(cancellationToken);
            var newRoot = oldRoot.ReplaceNode(interfaceDecl, newInterfaceSyntax);

            /* Ajoute le using. */
            newRoot = newRoot.AddUsing(FrameworkNames.KinetixServicesAnnotations);

            return document.WithSyntaxRoot(newRoot);
        }
    }
}