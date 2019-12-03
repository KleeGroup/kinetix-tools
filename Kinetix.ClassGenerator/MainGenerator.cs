using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Kinetix.ClassGenerator.Checker;
using Kinetix.ClassGenerator.CSharpGenerator;
using Kinetix.ClassGenerator.JavascriptGenerator;
using Kinetix.ClassGenerator.NVortex;
using Kinetix.ClassGenerator.SchemaGenerator;
using Kinetix.ClassGenerator.SsdtSchemaGenerator;
using Kinetix.ClassGenerator.XmlParser;
using Kinetix.ClassGenerator.XmlParser.OomReader;
using Kinetix.Tools.Common.Model;
using Kinetix.Tools.Common.Parameters;
using Newtonsoft.Json;

namespace Kinetix.ClassGenerator
{
    /// <summary>
    /// Générateur principal fédérant les autres générateurs.
    /// </summary>
    public class MainGenerator
    {
        private ICollection<ModelRoot> _modelList;
        private GeneratorParameters _parameters;

        /// <summary>
        /// Exécute la génération.
        /// <paramref name="parameters">Configuration.</paramref>
        /// </summary>
        public void Generate(GeneratorParameters parameters)
        {
            _parameters = parameters;

            // Vérification.
            CheckModelFiles();
            CheckOutputDirectory();

            // Chargement des modèles objet en mémoire.
            LoadObjectModel();

            // Génération.
            GenerateCSharp();
            GenerateSqlSchema();
            GenerateJavascript();

            // Pause.
            if (parameters.Pause.Value)
            {
                Console.WriteLine();
                Console.Write("Traitement terminé, veuillez appuyer sur une touche pour fermer cette fenêtre...");
                Console.ReadKey();
            }
        }

        /// <summary>
        /// Vérifie la capacité de traduire les fichiers modeles.
        /// </summary>
        private void CheckModelFiles()
        {
            foreach (string file in _parameters.ModelFiles)
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
        private AbstractSchemaGenerator LoadSchemaGenerator()
        {
            if (_parameters.ProceduralSql.TargetDBMS?.ToLower() == "postgre")
            {
                return new PostgreSchemaGenerator(_parameters.RootNamespace, _parameters.ProceduralSql);
            }
            else
            {
                return new SqlServerSchemaGenerator(_parameters.RootNamespace, _parameters.ProceduralSql);
            }
        }

        /// <summary>
        /// Vérifie la validité du répertoire de génération.
        /// </summary>
        private void CheckOutputDirectory()
        {
            if (_parameters.CSharp != null)
            {
                var outputDir = _parameters.CSharp.OutputDirectory;
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
        /// Returns collection of TableInit from a file.
        /// </summary>
        /// <param name="listFactoryFileName">Fichier de factory.</param>
        /// <returns>Collection of TableInit.</returns>
        private ICollection<TableInit> LoadTableInitListFromFile(string listFactoryFileName)
        {
            IDictionary<string, TableInit> dictionary = null;

            if (!string.IsNullOrEmpty(listFactoryFileName))
            {
                dictionary = new Dictionary<string, TableInit>();

                string fileContent = File.ReadAllText(listFactoryFileName);

                if (!string.IsNullOrEmpty(fileContent))
                {
                    var factory = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, Dictionary<string, object>>>>(fileContent);

                    foreach (KeyValuePair<string, Dictionary<string, Dictionary<string, object>>> tableFactory in factory)
                    {
                        string tableName = tableFactory.Key;

                        if (string.IsNullOrEmpty(tableName))
                        {
                            throw new ArgumentNullException("tableName");
                        }

                        if (dictionary.ContainsKey(tableName))
                        {
                            throw new NotSupportedException();
                        }

                        foreach (string itemTableName in dictionary.Keys.Where(itemTableName => string.Compare(itemTableName, tableName, StringComparison.OrdinalIgnoreCase) > 0))
                        {
                            throw new NotSupportedException("L'initialisation des listes statiques/références doit être effectuée dans l'ordre alphabétique, l'élément " + itemTableName + " précède l'élément " + tableName + ".");
                        }

                        TableInit table = new TableInit { ClassName = tableName, FactoryName = this.GetType().Name };

                        foreach (KeyValuePair<string, Dictionary<string, object>> value in tableFactory.Value)
                        {
                            table.AddItem(value.Key, value.Value);
                        }

                        dictionary.Add(tableName, table);
                    }
                }
            }

            return dictionary?.Values;
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
            return new OomParser(_parameters.ModelFiles, _parameters.DomainModelFile, _parameters.ExtModelFiles, _parameters.KeepOriginalNames);
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
            ICollection<TableInit> staticTableInitList = LoadTableInitListFromFile(_parameters.StaticListFactoryFileName);
            ICollection<TableInit> referenceTableInitList = LoadTableInitListFromFile(_parameters.ReferenceListFactoryFileName);

            // Génère les warnings pour le modèle.
            List<NVortexMessage> messageList = new List<NVortexMessage>(modelParser.ErrorList);
            messageList.AddRange(CodeChecker.Check(_modelList, _parameters.KeepOriginalNames));
            messageList.AddRange(StaticListChecker.Instance.Check(_modelList, staticTableInitList));
            messageList.AddRange(ReferenceListChecker.Instance.Check(_modelList, referenceTableInitList));
            messageList.AddRange(AbstractSchemaGenerator.CheckAllIdentifiersNames(_modelList));

            NVortexGenerator.Generate(messageList, _parameters.VortexFile, "ClassGenerator");

            if (!CanGenerate(messageList))
            {
                Environment.Exit(-NbErrorMessage(messageList));
            }
        }

        private void GenerateCSharp()
        {
            if (_parameters.CSharp != null)
            {
                Console.WriteLine("***** Génération du modèle C# *****");
                CSharpCodeGenerator.Generate(_parameters.RootNamespace, _parameters.CSharp, _modelList);
            }
        }

        /// <summary>
        /// Génère le schéma SQL.
        /// </summary>
        private void GenerateSqlSchema()
        {
            if (_parameters.Ssdt != null)
            {
                // Charge la configuration de génération (default values, no table, historique de l'ordre de création des colonnes).
                new SsdtConfigurationLoader().LoadConfigurationFiles(_modelList, _parameters.Ssdt);

                // Génération pour déploiement SSDT.
                new SqlServerSsdtSchemaGenerator(_parameters.Ssdt).GenerateSchemaScript(
                    _modelList,
                    _parameters.Ssdt.TableScriptFolder,
                    _parameters.Ssdt.TableTypeScriptFolder);

                var ssdtInsertGenerator = new SqlServerSsdtInsertGenerator(_parameters.Ssdt);

                if (StaticListChecker.Instance.DictionaryItemInit != null)
                {
                    ssdtInsertGenerator.GenerateListInitScript(
                        StaticListChecker.Instance.DictionaryItemInit,
                        _parameters.Ssdt.InitStaticListScriptFolder,
                        _parameters.Ssdt.InitStaticListMainScriptName,
                        "delta_static_lists.sql",
                        true);
                }

                if (ReferenceListChecker.Instance.DictionaryItemInit != null)
                {
                    ssdtInsertGenerator.GenerateListInitScript(
                        ReferenceListChecker.Instance.DictionaryItemInit,
                        _parameters.Ssdt.InitReferenceListScriptFolder,
                        _parameters.Ssdt.InitReferenceListMainScriptName,
                        "delta_reference_lists.sql",
                        false);
                }
            }

            if (_parameters.ProceduralSql != null)
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
            if (_parameters.Javascript != null)
            {
                Console.WriteLine("***** Génération du modèle et des ressources JS *****");
                TypescriptDefinitionGenerator.Generate(_parameters.RootNamespace, _parameters.Javascript, _modelList);
                JavascriptResourceGenerator.Generate(_parameters.Javascript, _modelList);
            }
        }
    }
}
