using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using Kinetix.RoslynCop.Common;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Kinetix.RoslynCop.Diagnostics.Design
{
    /// <summary>
    /// V�rifie que le nommage des contr�leurs WebAPI.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class FRC1503_ControllerNamingAnalyser : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "FRC1503";

        private static readonly string Title = "Nommage des contr�leurs Web API";
        private static readonly string MessageFormat = "Le contr�leur {0} n'est pas nomm� selon le service inject� {1}.";
        private static readonly string Description = "Nommage des contr�leurs Web API.";
        private const string Category = "Naming";

        private static readonly Regex ServiceContractNamePattern = new Regex(@"^IService(.*)$");

        private static readonly DiagnosticDescriptor Rule = DiagnosticRuleUtils.CreateRule(DiagnosticId, Title, MessageFormat, Category, Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSymbolAction(AnalyseSymbol, SymbolKind.NamedType);
        }

        private void AnalyseSymbol(SymbolAnalysisContext context)
        {
            /* V�rifie qu'on est dans un contr�leur Web API. */
            if (!(context.Symbol is INamedTypeSymbol namedTypedSymbol))
            {
                return;
            }
            if (!namedTypedSymbol.IsApiController())
            {
                return;
            }

            /* V�rifie qu'on a un unique constructeur. */
            var ctrList = namedTypedSymbol.Constructors;
            if (ctrList.Length != 1)
            {
                return;
            }

            /* V�rifie que le constructeur n'a qu'un seul param�tre. */
            var ctr = ctrList.First();
            var paramList = ctr.Parameters;
            if (paramList.Length != 1)
            {
                return;
            }

            /* V�rifie que le param�tre est un contrat de service. */
            if (!(paramList.First().Type is INamedTypeSymbol namedParamType))
            {
                return;
            }

            if (!namedParamType.IsServiceContract())
            {
                return;
            }

            /* V�rification du nommage */
            var contractName = namedParamType.Name;
            if (!ServiceContractNamePattern.IsMatch(contractName))
            {
                return;
            }
            var expectedControllerName = ServiceContractNamePattern.Replace(contractName, "$1Controller");
            var actualControllerName = namedTypedSymbol.Name;
            if (actualControllerName == expectedControllerName)
            {
                return;
            }

            /* Cr�� le diagnostic. */
            var diagnostic = Diagnostic.Create(Rule, namedTypedSymbol.Locations.First(), actualControllerName, contractName);
            context.ReportDiagnostic(diagnostic);
        }
    }
}
