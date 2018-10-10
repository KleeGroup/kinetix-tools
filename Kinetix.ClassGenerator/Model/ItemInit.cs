﻿using System.Collections.Generic;

namespace Kinetix.ClassGenerator.Model
{
    /// <summary>
    /// Classe encapsulant les données d'une initialisation d'un élément de liste statique.
    /// </summary>
    public sealed class ItemInit
    {
        /// <summary>
        /// Nom de la constante statique d'accès.
        /// </summary>
        public string VarName
        {
            get;
            set;
        }

        /// <summary>
        /// Bean initialisé pour l'insert SQL.
        /// </summary>
        public IDictionary<string, object> Bean
        {
            get;
            set;
        }
    }
}
