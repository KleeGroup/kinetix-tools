using System.Collections.Immutable;
using System.Linq;
using Kinetix.RoslynCop.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kinetix.RoslynCop.Diagnostics.Design
{
    /// <summary>
    /// Vérifie que les implémentations de service WCF sont sans état.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class FRC1103_ServiceShouldBeStatelessAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "FRC1103";
        private const string Category = "Design";
        private static readonly string Description = "Supprimer l'état du service : les champs non const/readonly sont interdits.";
        private static readonly string MessageFormat = "Le champ {1} du service {0} doit être const ou readonly.";

        private static readonly string Title = "Un service doit être sans état";

        private static readonly DiagnosticDescriptor Rule = DiagnosticRuleUtils.CreateRule(DiagnosticId, Title, MessageFormat, Category, Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            /* Analyse pour les déclaration de fields. */
            context.RegisterSyntaxNodeAction(AnalyzeSyntaxNode, ImmutableArray.Create(SyntaxKind.FieldDeclaration));
        }

        private static void AnalyzeSyntaxNode(SyntaxNodeAnalysisContext context)
        {
            // TODO refactoriser en semantic

            /* 1. Vérifier qu'on est dans une classe de service. */
            if (!(context.Node is FieldDeclarationSyntax node) || !(node.Parent is ClassDeclarationSyntax serviceClass))
            {
                return;
            }

            var classInfo = context.SemanticModel.GetDeclaredSymbol(serviceClass, context.CancellationToken);
            var isServiceImpl = classInfo.IsServiceImplementation();
            if (!isServiceImpl)
            {
                return;
            }

            /* 2. Vérifier si le field déclaré est un état. */
            if (IsStatelessField(node))
            {
                // Champ sans état => OK pas de warning.
                return;
            }

            /* 3. Création du warning. */
            var serviceClassName = serviceClass.GetClassName();
            var fieldName = node.GetFieldName();
            var diagnostic = Diagnostic.Create(Rule, node.GetLocation(), serviceClassName, fieldName);
            context.ReportDiagnostic(diagnostic);
        }

        private static bool IsStatelessField(FieldDeclarationSyntax node)
        {
            return node.Modifiers.Any(x =>
                x.Kind() == SyntaxKind.ReadOnlyKeyword ||
                x.Kind() == SyntaxKind.ConstKeyword);
        }
    }
}
