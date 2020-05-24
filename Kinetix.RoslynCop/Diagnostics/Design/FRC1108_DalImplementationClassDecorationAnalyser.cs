﻿using System.Collections.Immutable;
using Kinetix.RoslynCop.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kinetix.RoslynCop.Diagnostics.Design
{
    /// <summary>
    /// Vérifie que les classes d'implémentations de DAL sont décorées avec RegisterImpl.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class FRC1108_DalImplementationClassDecorationAnalyser : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "FRC1108";
        private const string Category = "Design";
        private static readonly string Description = "Les DAL doivent être décorés avec les attributs Kinetix.";
        private static readonly string MessageFormat = "La DAL {0} doit être décoré avec l'attribut Kinetix RegisterImpl.";

        private static readonly DiagnosticDescriptor Rule = DiagnosticRuleUtils.CreateRule(DiagnosticId, Title, MessageFormat, Category, Description);

        private static readonly string Title = "Les DAL doivent être décorés avec les attributs Kinetix.";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            /* Analyse pour les déclaration de classes. */
            context.RegisterSyntaxNodeAction(AnalyzeSyntaxNode, SyntaxKind.ClassDeclaration);
        }

        private static void AnalyzeSyntaxNode(SyntaxNodeAnalysisContext context)
        {
            /* Vérifie que la classe est candidate pour être une DAL WCF  */
            if (!(context.Node is ClassDeclarationSyntax classNode))
            {
                return;
            }

            if (!classNode.SyntaxTree.IsDalImplementationFile())
            {
                return;
            }

            /* Vérifie que la classe n'est pas déjà un service décoré WCF. */
            var classSymbol = context.SemanticModel.GetDeclaredSymbol(classNode, context.CancellationToken);
            if (classSymbol == null)
            {
                return;
            }

            if (classSymbol.IsServiceImplementation())
            {
                return;
            }

            /* Créé le diagnostic. */
            var location = classNode.GetNameDeclarationLocation();
            var diagnostic = Diagnostic.Create(Rule, location, classSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }
}
