using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace Kinetix.ClassGenerator
{
    /// <summary>
    /// Point d'entrée du générateur de classes.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Composant de parsing des arguments du programme.
        /// </summary>
        private static readonly ModelConfigurationLoader ConfigLoader = new ModelConfigurationLoader();

        /// <summary>
        /// Composant contenant le générateur principal.
        /// </summary>
        private static readonly MainGenerator MainGenerator = new MainGenerator();

        /// <summary>
        /// Lance la construction du modèle puis la génération des classes.
        /// </summary>
        /// <param name="args">
        /// Paramètres de ligne de commande.
        /// </param>
        /// <remarks>
        /// Génération des classes et du SQL, options -G et -S.
        /// </remarks>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Interception de toutes les exceptions pour écriture sur flux de sortie d'erreur.")]
        [STAThread]
        public static void Main(string[] args)
        {
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

                Console.WriteLine("******************************************************");
                Console.WriteLine("*                Kinetix.ClassGenerator                  *");
                Console.WriteLine("******************************************************");
                Console.WriteLine();

                // Lecture des paramètres d'entrée.
                var configPath = args[0];
                if (!File.Exists(configPath))
                {
                    throw new FileNotFoundException("Le fichier de configuration n'existe pas");
                }

                var parameters = ConfigLoader.LoadModelConfiguration(configPath);

                // Exécution de la génération
                MainGenerator.Generate(parameters);

                Console.WriteLine();
                Console.WriteLine("Fin de la génération");
            }
            catch (ReflectionTypeLoadException ex)
            {
                Console.Error.WriteLine("Une erreur est arrivée durant la génération des classes : ");
                Console.Error.WriteLine(ex.ToString());

                Console.ReadKey();
                Environment.Exit(-1);
            }
        }
    }
}
