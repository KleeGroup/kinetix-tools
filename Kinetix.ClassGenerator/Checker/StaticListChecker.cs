﻿using System;
using System.Collections.Generic;
using Kinetix.ClassGenerator.Model;
using Kinetix.ClassGenerator.NVortex;

namespace Kinetix.ClassGenerator.Checker
{
    /// <summary>
    /// Vérifie la cohérence des données issus de la factory d'initialisation des listes statiques.
    /// </summary>
    internal sealed class StaticListChecker : InitListChecker
    {
        /// <summary>
        /// Pattern Singleton.
        /// </summary>
        public static readonly StaticListChecker Instance = new StaticListChecker();

        /// <summary>
        /// Vérifie les éléments d'initialisation statiques.
        /// </summary>
        /// <param name="item">Data of the table.</param>
        /// <param name="classe">Table.</param>
        /// <param name="messageList">List of the error message.</param>
        protected override void CheckSpecific(TableInit item, ModelClass classe, ICollection<NVortexMessage> messageList)
        {
            CheckStereotype(item, classe, messageList);
            CheckPropertyExists(item, classe, messageList);
            CheckFkNoBoucle(item, classe, messageList);
        }

        /// <summary>
        /// Retourne le descripteur de propriété pour la primary key de la classe.
        /// </summary>
        /// <param name="classe">La classe en question.</param>
        /// <returns>Descripteur de la PK.</returns>
        protected override string GetReferenceKeyName(ModelClass classe)
        {
            if (classe.PrimaryKey.Count != 1)
            {
                throw new NotSupportedException();
            }

            ModelProperty pkProperty = ((IList<ModelProperty>)classe.PrimaryKey)[0];
            return pkProperty.Name;
        }

        /// <summary>
        /// Get the factory stereotype waited.
        /// </summary>
        /// <returns>The factory stereotype waited.</returns>
        protected override string GetStereotype()
        {
            return Stereotype.Statique;
        }

        /// <summary>
        /// Vérifie que la classe ne pointe pas sur elle même (directement ou indirectement).
        /// </summary>
        /// <param name="item">Model class.</param>
        /// <param name="classe">La classe considérée.</param>
        /// <param name="messageList">Liste des potentiels messages d'erreur.</param>
        private static void CheckFkNoBoucle(TableInit item, ModelClass classe, ICollection<NVortexMessage> messageList)
        {
            if (IsLinked(classe, classe))
            {
                messageList.Add(new NVortexMessage()
                {
                    Category = Category.Error,
                    IsError = true,
                    Description = "La liste de référence statique de type " + item.ClassName + " pointe sur elle même (directement ou indirectement).",
                    FileName = classe.Namespace.Model.ModelFile
                });
            }
        }

        /// <summary>
        /// Méthode récursive retournant true si la classe de départ est reliée à la classe d'arrivée.
        /// </summary>
        /// <param name="classeDepart">Classe de départ. </param>
        /// <param name="classeArrivee">Classe d'arrivée. </param>
        /// <returns>True or false.</returns>
        private static bool IsLinked(ModelClass classeDepart, ModelClass classeArrivee)
        {
            Queue<ModelClass> untreatedClasses = new Queue<ModelClass>();
            List<ModelClass> treatedClasses = new List<ModelClass>();

            //initialisation des queues
            untreatedClasses.Enqueue(classeDepart);

            //variables du loop
            ModelClass currentClasse;
            bool result = false;

            while ((untreatedClasses.Count != 0) || result)
            {
                //Next in line
                currentClasse = untreatedClasses.Dequeue();

                //Treatment
                treatedClasses.Add(currentClasse);

                foreach (ModelProperty property in currentClasse.PropertyList)
                {
                    if (property.IsFromAssociation)
                    {
                        ModelClass pointedClass = property.DataDescription.ReferenceClass;
                        result = result || pointedClass.Equals(classeArrivee);
                        if (!treatedClasses.Contains(pointedClass)) //Add children if not treated before.
                        {
                            untreatedClasses.Enqueue(pointedClass);
                        }
                    }

                }
            }
            return result;
        }
    }
}
