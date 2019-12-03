using System.Collections.Generic;

namespace Kinetix.Tools.Common.Parameters
{
    /// <summary>
    /// Paramètres de lancement du générateur de classes.
    /// </summary>
    public class GeneratorParameters
    {
        /// <summary>
        /// Namespace de base de l'application.
        /// </summary>
        public string RootNamespace
        {
            get;
            set;
        }

        /// <summary>
        /// Liste des fichiers de modélisation.
        /// </summary>
        public ICollection<string> ModelFiles
        {
            get;
            set;
        }

        /// <summary>
        /// Liste des fichiers de modélisation pour les dépendances.
        /// </summary>
        public ICollection<string> ExtModelFiles
        {
            get;
            set;
        }

        /// <summary>
        /// Fichiers contenant les domaines.
        /// </summary>
        public string DomainModelFile
        {
            get;
            set;
        }

        /// <summary>
        /// Nom de la factory contenant les listes statiques.
        /// </summary>
        public string StaticListFactoryFileName
        {
            get;
            set;
        }

        /// <summary>
        /// Nom de la factory contenant les listes de références.
        /// </summary>
        public string ReferenceListFactoryFileName
        {
            get;
            set;
        }

        /// <summary>
        /// Paramètres pour la génération du C#.
        /// </summary>
        public CSharpParameters CSharp
        {
            get;
            set;
        }

        /// <summary>
        /// Paramètres pour la génération SDL procédurale.
        /// </summary>
        public ProceduralSqlParameters ProceduralSql
        {
            get;
            set;
        }

        /// <summary>
        /// Paramètres pour la génération SSDT.
        /// </summary>
        public SsdtParameters Ssdt
        {
            get;
            set;
        }

        /// <summary>
        /// Paramètres pour la génération du Javascript.
        /// </summary>
        public JavascriptParameters Javascript
        {
            get;
            set;
        }

        /// <summary>
        /// Obtient ou définit si le générateur doit être mis en pause en sortie.
        /// </summary>
        public bool? Pause
        {
            get;
            set;
        }

        /// <summary>
        /// Retourne ou définit le nom du fichier vortex pour CruiseControl.
        /// </summary>
        public string VortexFile
        {
            get;
            set;
        }

        /// <summary>
        /// Garde les noms dans les OOMs pour les noms de tables/colonnes...
        /// </summary>
        public bool KeepOriginalNames
        {
            get;
            set;
        }
    }
}