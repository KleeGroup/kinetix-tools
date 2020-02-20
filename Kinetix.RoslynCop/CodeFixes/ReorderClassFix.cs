﻿using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kinetix.RoslynCop.Common.Ordering;
using Kinetix.RoslynCop.Diagnostics.Maintainability;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Kinetix.RoslynCop.CodeFixes
{
    /// <summary>
    /// Correcteur pour OrdreConstructeur.
    /// Permet d'enregistrer les corrections liées aux diagnostics.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ReorderClassFix))]
    [Shared]
    public class ReorderClassFix : CodeFixProvider
    {
        public sealed override ImmutableArray<string> FixableDiagnosticIds =>
            ImmutableArray.Create(FRC1201_ClassMembersShouldBeOrdered.DiagnosticId);

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        /// <summary>
        /// Enregistre les corrections de codes.
        /// </summary>
        /// <param name="context">Le contexte.</param>
        /// <returns>Peut être attendu.</returns>
        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var type = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().First();

            context.RegisterCodeFix(
                CodeAction.Create(
                    "Réordonner les membres",
                    c => OrdonnerMembres(context.Document, type, c),
                    "Réordonner les membres"),
                diagnostic);
        }

        /// <summary>
        /// Réordonne les membres d'un type.
        /// </summary>
        /// <param name="document">Le document.</param>
        /// <param name="type">Le type.</param>
        /// <param name="jetonAnnulation">Le jeton d'annulation.</param>
        /// <returns>Le nouveau document.</returns>
        private async Task<Document> OrdonnerMembres(Document document, TypeDeclarationSyntax type, CancellationToken jetonAnnulation)
        {
            // On récupère la racine.
            var racine = await document
                .GetSyntaxRootAsync(jetonAnnulation)
                .ConfigureAwait(false);
            var modèleSémantique = await document.GetSemanticModelAsync(jetonAnnulation);

            var nouveauType = type.WithMembers(SyntaxFactory.List(ClassOrdering.OrdonnerMembres(type.Members, modèleSémantique)));

            // Et on met à jour la racine.
            var nouvelleRacine = racine.ReplaceNode(type, nouveauType);

            return document.WithSyntaxRoot(nouvelleRacine);
        }
    }
}
