using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using Kinetix.ClassGenerator.Checker;
using Kinetix.ClassGenerator.Configuration;
using Kinetix.ClassGenerator.CSharpGenerator;
using Kinetix.ClassGenerator.JavascriptGenerator;
using Kinetix.ClassGenerator.Model;
using Kinetix.ClassGenerator.NVortex;
using Kinetix.ClassGenerator.SchemaGenerator;
using Kinetix.ClassGenerator.SsdtSchemaGenerator;
using Kinetix.ClassGenerator.SsdtSchemaGenerator.Contract;
using Kinetix.ClassGenerator.XmlParser;
using Kinetix.ClassGenerator.XmlParser.OomReader;
using Kinetix.ComponentModel;
using Kinetix.ComponentModel.Annotations;
using Kinetix.ComponentModel.ListFactory;

namespace Kinetix.ClassGenerator.Main
{
    /// <summary>
    /// Générateur principal fédérant les autres générateurs.
    /// </summary>
    public class MainGenerator
    {
        /// <summary>
        /// Composant de chargement de la configuration.
        /// </summary>
        private readonly ConfigurationLoader _configurationLoader = new ConfigurationLoader();

        /// <summary>
        /// Composant de génération des scripts de schéma pour SSDT.
        /// </summary>
        private readonly ISqlServerSsdtSchemaGenerator _ssdtSchemaGenerator = new SqlServerSsdtSchemaGenerator();

        /// <summary>
        /// Composant de génération des scripts d'insertion pour SSDT.
        /// </summary>
        private readonly ISqlServerSsdtInsertGenerator _ssdtInsertGenerator = new SqlServerSsdtInsertGenerator();

        /// <summary>
        /// Liste des domaines.
        /// </summary>
        private ICollection<IDomain> _domainList;

        /// <summary>
        /// Liste des modèles objet en mémoire.
        /// </summary>
        private ICollection<ModelRoot> _modelList;

        /// <summary>
        /// Exécute la génération.
        /// </summary>
        public void Generate()
        {
            // Vérification.
            CheckModelFiles();
            CheckOutputDirectory();

            // Chargements des domaines.
            _domainList = LoadDomain();

            // Chargement des modèles objet en mémoire.
            LoadObjectModel();

            // Génération.
            GenerateCSharp();
            GenerateSqlSchema();
            GenerateJavascript();

            // Pause.
            if (Singletons.GeneratorParameters.Pause.Value)
            {
                Console.WriteLine();
                Console.Write("Traitement terminé, veuillez appuyer sur une touche pour fermer cette fenêtre...");
                Console.ReadKey();
            }
        }

        /// <summary>
        /// Vérifie la capacité de traduire les fichiers modeles.
        /// </summary>
        private static void CheckModelFiles()
        {
            foreach (string file in Singletons.GeneratorParameters.ModelFiles)
            {
                if (!File.Exists(file))
                {
                    Console.Error.WriteLine("Le fichier " + file + " n'existe pas dans le dossier courant " + System.IO.Directory.GetCurrentDirectory() + ".");
                    Environment.Exit(-1);
                }
            }
        }

        /// <summary>
        /// Charge le générateur de schéma SQL.
        /// </summary>
        /// <returns>Générateur de schéma SQL.</returns>
        private static AbstractSchemaGenerator LoadSchemaGenerator()
        {
            if (Singletons.GeneratorParameters.ProceduralSql.TargetDBMS?.ToLower() == "postgre")
            {
                return new PostgreSchemaGenerator();
            }
            else
            {
                return new SqlServerSchemaGenerator();
            }
        }

        /// <summary>
        /// Vérifie la validité du répertoire de génération.
        /// </summary>
        private static void CheckOutputDirectory()
        {
            if (Singletons.GeneratorParameters.CSharp != null)
            {
                var outputDir = Singletons.GeneratorParameters.CSharp.OutputDirectory;
                if (!Directory.Exists(outputDir))
                {
                    Console.Error.WriteLine("Le répertoire de génération " + outputDir + " n'existe pas.");
                }

                DirectoryInfo dirInfo = new DirectoryInfo(outputDir);
                if ((dirInfo.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    Console.Error.WriteLine("Le répertoire de génération " + outputDir + " est en lecture seule.");
                }
            }
        }

        /// <summary>
        /// Charge les domaines utilisés par l'application.
        /// </summary>
        /// <returns>Liste des domaines de l'application.</returns>
        private static ICollection<IDomain> LoadDomain()
        {
            List<IDomain> domainList = new List<IDomain>();
            Assembly assembly = Assembly.LoadFile(Path.Combine(Directory.GetCurrentDirectory(), Singletons.GeneratorParameters.DomainFactoryAssembly));
            foreach (Module module in assembly.GetModules())
            {
                foreach (Type type in module.GetTypes())
                {
                    if (type.GetCustomAttributes(typeof(DomainMetadataTypeAttribute), false).Length > 0)
                    {
                        domainList.AddRange(Singletons.DomainManager.RegisterDomainMetadataType(type));
                    }
                }
            }

            return domainList;
        }

        /// <summary>
        /// Returns collection of TableInit.
        /// </summary>
        /// <param name="isStatic">Indique si la table est statique.</param>
        /// <returns>Collection of TableInit.</returns>
        private static ICollection<TableInit> LoadTableInitList(bool isStatic)
        {
            ICollection<TableInit> list = null;

            if (Singletons.GeneratorParameters.ListFactoryAssembly != null)
            {
                Assembly assembly = Assembly.LoadFile(Path.Combine(Directory.GetCurrentDirectory(), Singletons.GeneratorParameters.ListFactoryAssembly));
                foreach (Module module in assembly.GetModules())
                {
                    foreach (Type type in module.GetTypes())
                    {
                        if (typeof(AbstractListFactory).IsAssignableFrom(type))
                        {
                            AbstractListFactory factory = (AbstractListFactory)Activator.CreateInstance(type);
                            factory.Init();
                            if ((isStatic && factory.IsStatic) || (!isStatic && !factory.IsStatic))
                            {
                                list = factory.Items;
                            }
                        }
                    }
                }
            }

            return list;
        }

        /// <summary>
        /// Ecrit les erreurs sur la sortie standard 
        /// et retourne <code>True</code> si aucune erreur bloquante n'a été trouvée, <code>False</code> sinon.
        /// </summary>
        /// <param name="msgList">Liste des messages.</param>
        /// <returns><code>True</code> si les classes sont générables, <code>False</code> sinon.</returns>
        private static bool CanGenerate(List<NVortexMessage> msgList)
        {
            if (msgList == null)
            {
                throw new ArgumentNullException("msgList");
            }

            bool hasError = false;
            foreach (NVortexMessage msg in msgList)
            {
                if (msg.IsError)
                {
                    hasError = true;
                }

                Console.Out.WriteLine(string.Format(CultureInfo.InvariantCulture, "[{0}] - {1} : {2}", msg.Category, msg.Code, msg.Description));
            }

            return !hasError;
        }

        /// <summary>
        /// Retourne le nombre de messages de type erreur de la pile de message en paramètres.
        /// </summary>
        /// <param name="msgList">La liste des messages.</param>
        /// <returns>Le nombre de message de type erreur.</returns>
        private static int NbErrorMessage(List<NVortexMessage> msgList)
        {
            int i = 0;
            foreach (NVortexMessage msg in msgList)
            {
                if (msg.IsError)
                {
                    ++i;
                }
            }

            return i;
        }

        /// <summary>
        /// Charge le parser de modèle objet.
        /// </summary>
        /// <returns>Parser de modèle objet.</returns>
        private IModelParser LoadModelParser()
        {
            return new OomParser(Singletons.GeneratorParameters.ModelFiles, Singletons.GeneratorParameters.DomainModelFile, Singletons.GeneratorParameters.ExtModelFiles, _domainList);
        }

        /// <summary>
        /// Charge en mémoire les modèles objet et génère les warnings.
        /// </summary>
        private void LoadObjectModel()
        {
            // Charge le parser.
            IModelParser modelParser = LoadModelParser();

            // Parse le modèle.
            _modelList = modelParser.Parse();

            // Charge les listes de références.
            ICollection<TableInit> staticTableInitList = LoadTableInitList(true);
            ICollection<TableInit> referenceTableInitList = LoadTableInitList(false);

            // Génère les warnings pour le modèle.
            List<NVortexMessage> messageList = new List<NVortexMessage>(modelParser.ErrorList);
            messageList.AddRange(CodeChecker.Check(_modelList, _domainList));
            messageList.AddRange(StaticListChecker.Instance.Check(_modelList, staticTableInitList));
            messageList.AddRange(ReferenceListChecker.Instance.Check(_modelList, referenceTableInitList));
            messageList.AddRange(AbstractSchemaGenerator.CheckAllIdentifiersNames(_modelList));

            NVortexGenerator.Generate(messageList, Singletons.GeneratorParameters.VortexFile, "ClassGenerator");

            if (!CanGenerate(messageList))
            {
                Environment.Exit(-NbErrorMessage(messageList));
            }
        }

        private void GenerateCSharp()
        {
            if (Singletons.GeneratorParameters.CSharp != null)
            {
                Console.WriteLine("***** Génération du modèle C# *****");
                CSharpCodeGenerator.Generate(_modelList);
            }
        }


        /// <summary>
        /// Génère le schéma SQL.
        /// </summary>
        private void GenerateSqlSchema()
        {
            if (Singletons.GeneratorParameters.Ssdt != null)
            {
                // Charge la configuration de génération (default values, no table, historique de l'ordre de création des colonnes).
                _configurationLoader.LoadConfigurationFiles(_modelList);

                // Génération pour déploiement SSDT.
                _ssdtSchemaGenerator.GenerateSchemaScript(
                    _modelList,
                    Singletons.GeneratorParameters.Ssdt.TableScriptFolder,
                    Singletons.GeneratorParameters.Ssdt.TableTypeScriptFolder);

                if (StaticListChecker.Instance.DictionaryItemInit != null)
                {
                    _ssdtInsertGenerator.GenerateListInitScript(
                    StaticListChecker.Instance.DictionaryItemInit,
                    Singletons.GeneratorParameters.Ssdt.InitStaticListScriptFolder,
                    Singletons.GeneratorParameters.Ssdt.InitStaticListMainScriptName,
                    "delta_static_lists.sql",
                    true);
                }

                if (ReferenceListChecker.Instance.DictionaryItemInit != null)
                {
                    _ssdtInsertGenerator.GenerateListInitScript(
                    ReferenceListChecker.Instance.DictionaryItemInit,
                    Singletons.GeneratorParameters.Ssdt.InitReferenceListScriptFolder,
                    Singletons.GeneratorParameters.Ssdt.InitReferenceListMainScriptName,
                    "delta_reference_lists.sql",
                    false);
                }
            }

            if (Singletons.GeneratorParameters.ProceduralSql != null)
            {
                var schemaGenerator = LoadSchemaGenerator();
                schemaGenerator.GenerateSchemaScript(_modelList);

                if (StaticListChecker.Instance.DictionaryItemInit != null)
                {
                    schemaGenerator.GenerateListInitScript(StaticListChecker.Instance.DictionaryItemInit, isStatic: true);
                }

                if (ReferenceListChecker.Instance.DictionaryItemInit != null)
                {
                    schemaGenerator.GenerateListInitScript(ReferenceListChecker.Instance.DictionaryItemInit, isStatic: false);
                }
            }
        }

        /// <summary>
        /// Génère les fichiers Javascript.
        /// </summary>
        private void GenerateJavascript()
        {
            if (Singletons.GeneratorParameters.Javascript != null)
            {
                Console.WriteLine("***** Génération du modèle et des ressources JS *****");
                TypescriptDefinitionGenerator.Generate(_modelList);
                JavascriptResourceGenerator.Generate(_modelList);
            }
        }
    }
}
