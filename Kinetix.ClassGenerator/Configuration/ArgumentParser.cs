using System.IO;

namespace Kinetix.ClassGenerator.Configuration
{
    /// <summary>
    /// Parseur des arguments du programme.
    /// </summary>
    public class ArgumentParser
    {
        /// <summary>
        /// Composant de chargement des paramètres du programme.
        /// </summary>
        private readonly ModelConfigurationLoader _modelConfigurationLoader = new ModelConfigurationLoader();

        /// <summary>
        /// Parse les arguments d'entrée du programme.
        /// </summary>
        /// <param name="args">Arguments de la commande.</param>
        public void Parse(string[] args)
        {
            var configPath = args[0];
            if (!File.Exists(configPath))
            {
                throw new FileNotFoundException("Le fichier de configuration n'existe pas");
            }

            _modelConfigurationLoader.LoadModelConfiguration(configPath);
        }
    }
}
