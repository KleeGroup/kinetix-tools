using System.Collections.Generic;
using Kinetix.ClassGenerator.NVortex;
using Kinetix.Tools.Common.Model;

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
        public static ICollection<NVortexMessage> Check(ICollection<ModelRoot> modelList, bool keepOriginalNames)
        {
            foreach (var model in modelList)
            {
                ModelRootChecker.Instance.KeepOriginalNames = keepOriginalNames;
                ModelRootChecker.Instance.Check(model);
            }

            return AbstractModelChecker.NVortexMessageList;
        }
    }
}
