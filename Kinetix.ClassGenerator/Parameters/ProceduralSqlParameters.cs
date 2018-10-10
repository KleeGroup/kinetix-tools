﻿namespace Kinetix.ClassGenerator.Parameters
{
    /// <summary>
    /// Paramètres pour la génération de SQL procédural.
    /// </summary>
    public class ProceduralSqlParameters
    {
        /// <summary>
        /// SGBD cible ("postgre" ou "sqlserver").
        /// </summary>
        public string TargetDBMS
        {
            get;
            set;
        }

        /// <summary>
        /// Retourne ou définit l'emplacement du fichier de création de base (SQL).
        /// </summary>
        public string CrebasFile
        {
            get;
            set;
        }

        /// <summary>
        /// Retourne ou définit l'emplacement du fichier de création des index uniques (SQL).
        /// </summary>
        public string UKFile
        {
            get;
            set;
        }

        /// <summary>
        /// Retourne ou définit l'emplacement du fichier de création des clés étrangères (SQL).
        /// </summary>
        public string IndexFKFile
        {
            get;
            set;
        }

        /// <summary>
        /// Retourne ou définit l'emplacement du fichier de création des types (SQL).
        /// </summary>
        public string TypeFile
        {
            get;
            set;
        }

        /// <summary>
        /// Retourne ou définit l'emplacement du script d'insertion des données des listes statiques (SQL).
        /// </summary>
        public string StaticListFile
        {
            get;
            set;
        }

        /// <summary>
        /// Retourne ou définit l'emplacement du script d'insertion des données des listes administrables (SQL).
        /// </summary>
        public string ReferenceListFile
        {
            get;
            set;
        }
    }
}
