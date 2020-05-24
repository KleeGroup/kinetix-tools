﻿using System.Collections.Immutable;
using Kinetix.RoslynCop.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kinetix.RoslynCop.Diagnostics.Design
{
    /// <summary>
    /// Vérifie que les classes de contrats de service sont décorées avec RegisterContract.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class FRC1109_RegisterContractClassDecorationAnalyser : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "FRC1109";
        private const string Category = "Design";
        private static readonly string Description = "Les contrats de service doivent être décorés avec les attributs Kinetix.";
        private static readonly string MessageFormat = "Le contrat de service {0} doit être décoré avec l'attribut Kinetix RegisterContract.";

        private static readonly DiagnosticDescriptor Rule = DiagnosticRuleUtils.CreateRule(DiagnosticId, Title, MessageFormat, Category, Description);

        private static readonly string Title = "Les contrats de service doivent être décorés avec les attributs Kinetix.";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            /* Analyse pour les déclaration de classes. */
            context.RegisterSyntaxNodeAction(AnalyzeSyntaxNode, SyntaxKind.InterfaceDeclaration);
        }

        private static void AnalyzeSyntaxNode(SyntaxNodeAnalysisContext context)
        {
            /* Vérifie que la classe est candidate pour être un contrat de service WCF  */
            if (!(context.Node is InterfaceDeclarationSyntax interfaceNode))
            {
                return;
            }

            if (!interfaceNode.SyntaxTree.IsServiceContractFile())
            {
                return;
            }

            /* Vérifie que la classe n'est pas déjà un contrat de service décoré WCF. */
            var interfaceSymbol = context.SemanticModel.GetDeclaredSymbol(interfaceNode, context.CancellationToken);
            if (interfaceSymbol == null)
            {
                return;
            }

            if (interfaceSymbol.IsServiceContract())
            {
                return;
            }

            /* Créé le diagnostic. */
            var location = interfaceNode.GetNameDeclarationLocation();
            var diagnostic = Diagnostic.Create(Rule, location, interfaceSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }
}
