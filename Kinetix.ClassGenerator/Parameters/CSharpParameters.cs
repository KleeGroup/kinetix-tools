namespace Kinetix.ClassGenerator.Parameters
{
    /// <summary>
    /// Paramètres pour la génération du C#.
    /// </summary>
    public class CSharpParameters
    {
        /// <summary>
        /// Obtient ou définit le répertoire de génération.
        /// </summary>
        public string OutputDirectory
        {
            get;
            set;
        }

        /// <summary>
        /// Nom du projet dans lequel mettre le DbContext.
        /// </summary>
        public string DbContextProjectPath
        {
            get;
            set;
        }

        /// <summary>
        /// Utilise des types spécifiques pour les valeurs de listes statiques, au lieu de string.
        /// </summary>
        public bool? UseTypeSafeConstValues
        {
            get;
            set;
        }
    }
}
