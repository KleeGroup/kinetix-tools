using System.Text.RegularExpressions;

namespace Kinetix.RoslynCop.Common
{
    /// <summary>
    /// Extensions des chaînes de caractères.
    /// </summary>
    public static class StringExtensions
    {
        private static readonly Regex ServiceContractPattern = new Regex(@"^I((?:Service|Dal).*)$");

        /// <summary>
        /// Transforme une chaîne en camelCase.
        /// </summary>
        /// <param name="raw">Chaîne brute.</param>
        /// <returns>Chaîne en camelCase.</returns>
        public static string ToCamelCase(this string raw)
        {
            return string.IsNullOrEmpty(raw)
                ? raw
                : char.ToLowerInvariant(raw[0]) + raw.Substring(1);
        }

        /// <summary>
        /// Indique si une chaîne est un nom de contrat de service/dal.
        /// </summary>
        /// <param name="candidate">Chaîne brute.</param>
        /// <returns><code>True</code> si c'est un contrat.</returns>
        public static bool IsServiceContractName(this string candidate)
        {
            return string.IsNullOrEmpty(candidate)
                ? false
                : ServiceContractPattern.IsMatch(candidate);
        }

        /// <summary>
        /// Indique si une chaîne est un nom de contrat de service/dal.
        /// </summary>
        /// <param name="contractName">Chaîne brute.</param>
        /// <returns><code>True</code> si c'est un contrat.</returns>
        public static string GetServiceContractFieldName(this string contractName)
        {
            return string.IsNullOrEmpty(contractName)
                ? contractName
                : $"_{contractName.GetServiceContractParameterName()}";
        }

        /// <summary>
        /// Indique si une chaîne est un nom de contrat de service/dal.
        /// </summary>
        /// <param name="contractName">Chaîne brute.</param>
        /// <returns><code>True</code> si c'est un contrat.</returns>
        public static string GetServiceContractParameterName(this string contractName)
        {
            if (string.IsNullOrEmpty(contractName))
            {
                return contractName;
            }

            var match = ServiceContractPattern.Match(contractName);
            return match.Groups[1].Value.ToCamelCase();
        }
    }
}
