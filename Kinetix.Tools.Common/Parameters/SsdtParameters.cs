namespace Kinetix.Tools.Common.Parameters
{
    /// <summary>
    /// Paramètres pour la génération SSDT.
    /// </summary>
    public class SsdtParameters
    {
        /// <summary>
        /// Chemin du fichier sqlproj du projet .
        /// </summary>
        public string ProjFileName
        {
            get;
            set;
        }

        /// <summary>
        /// Dossier du projet pour les scripts de déclaration de table.
        /// </summary>
        public string TableScriptFolder
        {
            get;
            set;
        }

        /// <summary>
        /// Dossier du projet pour les scripts de déclaration de type table.
        /// </summary>
        public string TableTypeScriptFolder
        {
            get;
            set;
        }

        /// <summary>
        /// Nom du fichier de configuration des colonnes avec default value.
        /// </summary>
        public string DefaultValuesFile
        {
            get;
            set;
        }

        /// <summary>
        /// Nom du fichier de configuration tables à ne pas générer.
        /// </summary>
        public string NoTableFile
        {
            get;
            set;
        }

        /// <summary>
        /// Nom du fichier de configuration de l'historique de création des colonnes.
        /// </summary>
        public string HistoriqueCreationFile
        {
            get;
            set;
        }

        /// <summary>
        /// Dossier du projet pour les scripts d'initialisation des listes de références administrables.
        /// </summary>
        public string InitReferenceListScriptFolder
        {
            get;
            set;
        }

        /// <summary>
        /// Dossier du projet pour les scripts d'initialisation des listes statiques.
        /// </summary>
        public string InitStaticListScriptFolder
        {
            get;
            set;
        }

        /// <summary>
        /// Fichier du projet référençant les scripts d'initialisation de références administrables.
        /// </summary>
        public string InitReferenceListMainScriptName
        {
            get;
            set;
        }

        /// <summary>
        /// Fichier du projet référençant les scripts d'initialisation des listes statiques.
        /// </summary>
        public string InitStaticListMainScriptName
        {
            get;
            set;
        }

        /// <summary>
        /// Obtient ou définit le nom de la table où stocker l'historique de passage des scripts.
        /// </summary>
        public string LogScriptTableName
        {
            get;
            set;
        } = "SCRIPT_HISTORIQUE";

        /// <summary>
        /// Obtient ou définit le nom du champ où stocker le nom des scripts exécutés.
        /// </summary>
        public string LogScriptVersionField
        {
            get;
            set;
        } = "SHI_VERSION";

        /// <summary>
        /// Obtient ou définit le nom du champ où stocker la date d'exécution des scripts.
        /// </summary>
        public string LogScriptDateField
        {
            get;
            set;
        } = "SHI_DATE";
    }
}
