using System;
using System.Collections.Generic;
using Kinetix.ClassGenerator.Model;
using Kinetix.ClassGenerator.NVortex;
using Kinetix.ComponentModel.ListFactory;

namespace Kinetix.ClassGenerator.Checker
{
    /// <summary>
    /// Vérifie la cohérence des données issus de la factory d'initialisation des listes statiques.
    /// </summary>
    internal sealed class ReferenceListChecker : InitListChecker
    {
        /// <summary>
        /// Pattern Singleton.
        /// </summary>
        public static readonly ReferenceListChecker Instance = new ReferenceListChecker();

        /// <summary>
        /// Check for reference list.
        /// </summary>
        /// <param name="item">Data of the table.</param>
        /// <param name="classe">Table.</param>
        /// <param name="messageList">List of the error message.</param>
        protected override void CheckSpecific(TableInit item, ModelClass classe, ICollection<NVortexMessage> messageList)
        {
            CheckStereotype(item, classe, messageList);
            CheckPropertyExists(item, classe, messageList);
        }

        /// <summary>
        /// Retourne le descripteur de propriété pour la clef unique de la classe.
        /// </summary>
        /// <param name="classe">La classe en question.</param>
        /// <returns>Descripteur de la PK.</returns>
        protected override string GetReferenceKeyName(ModelClass classe)
        {
            foreach (ModelProperty property in classe.PropertyList)
            {
                if (property.IsUnique)
                {
                    return property.Name;
                }
            }

            throw new NotSupportedException("Add \"Unique\" annotation property for " + classe.Name + ".");
        }

        /// <summary>
        /// Get the factory stereotype waited.
        /// </summary>
        /// <returns>The factory stereotype waited.</returns>
        protected override string GetStereotype()
        {
            return Stereotype.Reference;
        }
    }
}
