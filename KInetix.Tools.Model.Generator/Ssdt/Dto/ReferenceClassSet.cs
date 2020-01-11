using System.Collections.Generic;
using Kinetix.Tools.Model;

#nullable disable
namespace Kinetix.Tools.Model.Generator.Ssdt.Dto
{
    /// <summary>
    /// Bean d'entrée du scripter qui pilote l'appel des scripts d'insertions des valeurs de listes de référence.
    /// </summary>
    public class ReferenceClassSet
    {
        /// <summary>
        /// Liste des classe de référence ordonnée.
        /// </summary>
        public IList<Class> ClassList
        {
            get;
            set;
        }

        /// <summary>
        /// Nom du script à générer.
        /// </summary>
        public string ScriptName
        {
            get;
            set;
        }
    }
}
