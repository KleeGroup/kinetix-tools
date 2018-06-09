using System.Collections.Generic;
using Kinetix.Tools.Common.Model;
using Kinetix.ClassGenerator.NVortex;

namespace Kinetix.ClassGenerator.XmlParser
{
    /// <summary>
    /// Contrat des parser de modèle.
    /// </summary>
    public interface IModelParser
    {
        /// <summary>
        /// Collection d'erreurs au format NVortex.
        /// </summary>
        ICollection<NVortexMessage> ErrorList
        {
            get;
        }

        /// <summary>
        /// Retourne le résultat d'analyse du modele parsé.
        /// Les erreurs sont consultables via la propriété ErrorList.
        /// </summary>
        /// <returns>La collection de message d'erreurs au format NVortex.</returns>
        ICollection<ModelRoot> Parse();
    }
}
