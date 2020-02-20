using System.Collections.Immutable;
using Kinetix.RoslynCop.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kinetix.RoslynCop.Diagnostics.Design
{
    /// <summary>
    /// V�rifie que le param�tre de constructeur pour un service inject� est nomm� comme le service.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class FRC1501_ServiceCtrParameterNamingAnalyser : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "FRC1501";

        private static readonly string Title = "Nommage du param�tre de service";
        private static readonly string MessageFormat = "Le param�tre {1} du constructeur la classe {0} doit �tre nomm� {2}.";
        private static readonly string Description = "Renommer le param�tre comme le service.";
        private const string Category = "Naming";

        private static readonly DiagnosticDescriptor Rule = DiagnosticRuleUtils.CreateRule(DiagnosticId, Title, MessageFormat, Category, Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            /* Analyse pour les d�claration de fields. */
            context.RegisterSyntaxNodeAction(AnalyzeSyntaxNode, ImmutableArray.Create(SyntaxKind.ClassDeclaration));
        }

        private static void AnalyzeSyntaxNode(SyntaxNodeAnalysisContext context)
        {
            if (!(context.Node is ClassDeclarationSyntax node))
            {
                return;
            }

            new ConstructorWalker(context, node.GetClassName()).Visit(node);
        }

        private class ConstructorWalker : CSharpSyntaxWalker
        {
            private readonly SyntaxNodeAnalysisContext _context;
            private readonly string _className;

            public ConstructorWalker(SyntaxNodeAnalysisContext context, string className)
            {
                _context = context;
                _className = className;
            }

            public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
            {
                foreach (var paramNode in node.ParameterList.Parameters)
                {
                    VisitCtrParameter(paramNode);
                }
            }

            private void VisitCtrParameter(ParameterSyntax paramNode)
            {
                var namedTypeSymbol = _context.GetNamedSymbol(paramNode.Type);
                if (namedTypeSymbol == null)
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
                var expectedFieldName = typeName.GetServiceContractParameterName();
                var actualFieldName = paramNode.GetParameterName();
                if (expectedFieldName == actualFieldName)
                {
                    return;
                }
                var diagnostic = Diagnostic.Create(Rule, paramNode.GetParameterNameLocation(), _className, actualFieldName, expectedFieldName);
                _context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
