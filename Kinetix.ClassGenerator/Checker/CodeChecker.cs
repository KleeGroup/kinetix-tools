using System.Collections.Generic;
using Kinetix.Tools.Common.Model;
using Kinetix.ClassGenerator.NVortex;

namespace Kinetix.ClassGenerator.Checker
{
    /// <summary>
    /// Classe chargée de la vérification des différentes règles sur les classes.
    /// </summary>
    internal static class CodeChecker
    {
        /// <summary>
        /// Methode de controle du modele.
        /// </summary>
        /// <param name="modelList">Liste des modeles parsés.</param>
        /// <returns>La liste des erreurs.</returns>
        public static ICollection<NVortexMessage> Check(ICollection<ModelRoot> modelList)
        {
            foreach (var model in modelList)
            {
                ModelRootChecker.Instance.Check(model);
            }

            return AbstractModelChecker.NVortexMessageList;
        }
    }
}
