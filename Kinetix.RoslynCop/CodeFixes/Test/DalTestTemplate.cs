using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kinetix.RoslynCop.CodeFixes.Test
{
    /// <summary>
    /// Template de test de DAL.
    /// </summary>
    public partial class DalTestTemplate
    {
        private static readonly IComparer<string> _usingComparer = new UsingComparer();

        /// <summary>
        /// Créé une nouvelle instance de DalTestTemplate.
        /// </summary>
        /// <param name="item">Méthode de DAL.</param>
        public DalTestTemplate(DalMethodItem item)
        {
            Item = item;
        }

        /// <summary>
        /// Méthode de DAL.
        /// </summary>
        public DalMethodItem Item
        {
            get;
            private set;
        }

        public string Render(DalTestStrategy strategy = DalTestStrategy.Semantic)
        {
            var sb = new StringBuilder();
            foreach (var usingDirective in GetUsings())
            {
                sb.AppendLine($@"using {usingDirective};");
            }

            var methodCall = $@"{Item.DalMethodName}({Item.FlatParams})";
            var methodTest =
                strategy == DalTestStrategy.Semantic ?
                $@"this.CheckDalSyntax<{Item.DalClassName}>(dal => dal.{methodCall});" : // Test sémantique : on enveloppe l'appel pour attraper les exceptions liées aux données.
                $@"Provider.GetService<{Item.DalClassName}>().{methodCall};"; // Test standard

            sb.Append(
            $@"
namespace {Item.DalAssemblyName}.Test.{Item.DalClassName}Test
{{
    [TestClass]
    public class {Item.DalMethodName}Test : DalTest
    {{
        [TestMethod]
        public void Check_{Item.DalMethodName}_Ok()
        {{
            // Act
            {methodTest}
        }}
    }}
}}");
            return sb.ToString();
        }

        /// <summary>
        /// Renvoie les usings triés.
        /// </summary>
        /// <returns>Liste des usings.</returns>
        private ICollection<string> GetUsings()
        {
            /* Construit la liste de using triés. */
            var usings = new SortedSet<string>(_usingComparer);
            foreach (var usingDirective in Item.SpecificUsings)
            {
                usings.Add(usingDirective);
            }

            foreach (var usingDirective in Item.Params.SelectMany(x => x.SpecificUsings))
            {
                usings.Add(usingDirective);
            }

            usings.Add(Item.DalNamespace);
            usings.Add("Microsoft.VisualStudio.TestTools.UnitTesting");

            return usings;
        }

        /// <summary>
        /// Comparateur de string pour les namespace d'using.
        /// Les namespace System sont prioritaires sur l'ordre alphabétique.
        /// </summary>
        private class UsingComparer : IComparer<string>
        {

            /// <summary>
            /// Compare x et y.
            /// Renvoie 1 si x gt y.
            /// Renvoie 0 si x eq y.
            /// Renvoie -1 si x lt y.
            /// </summary>
            /// <param name="x">Opérande de gauche.</param>
            /// <param name="y">Opérande de droite.</param>
            /// <returns>Comparaison.</returns>
            public int Compare(string x, string y)
            {
                var xSystem = x.StartsWith("System", System.StringComparison.Ordinal);
                var ySystem = y.StartsWith("System", System.StringComparison.Ordinal);
                /* Si les deux usings sont System, où les deux usings ne sont pas System, on compare nativement. */
                return xSystem == ySystem
                    ? string.Compare(x, y, System.StringComparison.Ordinal)
                    : xSystem
                    ? 1
                    : ySystem
                    ? -1
                    : 0;
            }
        }
    }
}