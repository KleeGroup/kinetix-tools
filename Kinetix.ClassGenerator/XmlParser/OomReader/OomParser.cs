using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using Kinetix.ClassGenerator.Checker;
using Kinetix.ClassGenerator.NVortex;
using Kinetix.Tools.Common.Model;

namespace Kinetix.ClassGenerator.XmlParser.OomReader
{
    /// <summary>
    /// Cette classe représente l'implémentation d'un parser Oom (modèle objet PowerDesigner).
    /// </summary>
    internal class OomParser : AbstractParser
    {
        private const string DomainCustomAnnotation = "a:ClientCheckExpression";
        private const string DomainCustomUsings = "a:ServerCheckExpression";
        private const string DomainStereotype = "a:Stereotype";

        private const string NodeModel = "/Model/o:RootObject/c:Children/o:Model";
        private const string NodeNamespace = "/Model/o:RootObject/c:Children/o:Model/c:Packages";
        private const string NodeDomains = "/Model/o:RootObject/c:Children/o:Model/c:Domains";
        private const string NodeClasses = "c:Classes";
        private const string NodeAssociations = "c:Associations";
        private const string NodeGeneralizations = "c:Generalizations";
        private const string NodeObject1 = "c:Object1";
        private const string NodeObject2 = "c:Object2";
        private const string NodeClass = "o:Class";
        private const string NodeShortcut = "o:Shortcut";
        private const string NodeAttributeDomain = "c:Domain";
        private const string NodeAttributes = "c:Attributes";
        private const string NodeIdentifiers = "c:Identifiers";
        private const string NodeIdentifiersAttributes = "c:Identifier.Attributes";
        private const string NodePrimaryKeys = "c:PrimaryIdentifier";

        private const string PropertyName = "a:Name";
        private const string PropertyCode = "a:Code";
        private const string PropertyComment = "a:Comment";
        private const string PropertyClassPersitence = "a:Persistence";
        private const string PropertyPersistentCode = "a:PersistentCode";
        private const string PropertyPersistent = "a:Persistent";
        private const string PropertyDataType = "a:DataType";
        private const string PropertyMultiplicity = "a:Multiplicity";
        private const string PropertyPersistentDataType = "a:PersistentDataType";
        private const string PropertyPersistentLength = "a:PersistentLength";
        private const string PropertyPersistentPrecision = "a:PersistentPrecision";
        private const string PropertyDerived = "a:Derived";
        private const string PropertyCreator = "a:Creator";
        private const string PropertyStereotype = "a:Stereotype";
        private const string PropertyIndicatorA = "a:RoleAIndicator";
        private const string PropertyIndicatorB = "a:RoleBIndicator";
        private const string PropertyRoleMultiplicityA = "a:RoleAMultiplicity";
        private const string PropertyRoleMultiplicityB = "a:RoleBMultiplicity";
        private const string PropertyRoleNameA = "a:RoleAName";
        private const string PropertyRoleNameB = "a:RoleBName";
        private const string PropertyNavigabilityA = "a:RoleANavigability";
        private const string PropertyNavigabilityB = "a:RoleBNavigability";
        private const string PropertyInitialValueB = "a:RoleBInitialValue";
        private const string PropertyTargetId = "a:TargetID";
        private const string PropertyObjectId = "a:ObjectID";
        private const string PropertyDefaultValue = "a:DefaultValue";
        private const string DomaineIdCode = "DO_ID";
        private const string DomaineHorodatageCode = "DO_TIMESTAMP";
        private const string DomaineEntierCode = "DO_ENTIER";
        private const string DomaineCodeCode = "DO_CODE_10";

        private readonly Regex _regexICollection = new Regex(@"^ICollection<[A-Za-z0-9]+>$");
        private readonly Regex _regexICollectionGenericClasse = new Regex(@"<[A-Za-z0-9]+>$");

        private readonly IDictionary<string, ModelClass> _classByNameMap = new Dictionary<string, ModelClass>(); // identifiants de type oXXX
        private readonly IDictionary<string, ModelClass> _classByIdMap = new Dictionary<string, ModelClass>(); // identifiants de type oXXX
        private readonly IDictionary<string, ModelClass> _classByObjectIdMap = new Dictionary<string, ModelClass>(); // identifiants de type XXXXX-XXXXX-XXXX-XXXX
        private readonly IDictionary<string, string> _shortcutClassIdMap = new Dictionary<string, string>(); // map de correspondance id du raccourci / object id de la classe.

        private XmlDocument _currentOom;
        private XmlNamespaceManager _currentNsManager;
        private ModelRoot _currentModelRoot;
        private ICollection<ModelRoot> _modelRootList;
        private ModelRoot _domainModelRoot;

        private readonly string _domainModelFile;
        private readonly ICollection<string> _extModelFiles;

        /// <summary>
        /// Constructeur.
        /// </summary>
        /// <param name="modelFiles">Liste des modèles à analyser.</param>
        /// <param name="domainModelFile">Modele contenant les domaines.</param>
        /// <param name="extModelFiles">Liste des modèles de base de données externes à analyser.</param>
        public OomParser(ICollection<string> modelFiles, string domainModelFile, ICollection<string> extModelFiles)
            : base(modelFiles)
        {
            _domainModelFile = domainModelFile;
            _extModelFiles = extModelFiles;
        }

        /// <summary>
        /// Vérifie le code des associations.
        /// </summary>
        /// <param name="primaryClass">Classe référencée.</param>
        /// <param name="foreignClass">Classe référençant.</param>
        /// <param name="code">Code de l'association.</param>
        /// <param name="role">Rôle sur l'association.</param>
        /// <param name="pClassComposeFClass">True si la classe référencée compose la classe référençant.</param>
        /// <param name="fClassComposePClass">True si la classe référençant compose la classe référencée.</param>
        /// <returns>True en cas d'erreur.</returns>
        [SuppressMessage("Microsoft.Globalization", "CA1303:DoNotPassLiteralsAsLocalizedParameters", Justification = "Non internationalisé")]
        [SuppressMessage("Microsoft.Globalization", "CA1308:NormalizeStringsToUppercase", Justification = "Convertion en notation Pascale")]
        public bool CheckAssociationCode(ModelClass primaryClass, ModelClass foreignClass, string code, string role, bool pClassComposeFClass, bool fClassComposePClass)
        {
            string associationCode;

            if (!primaryClass.IsTable || !foreignClass.IsTable)
            {
                return false;
            }

            if (!pClassComposeFClass && !fClassComposePClass)
            {
                associationCode = primaryClass.Trigram.Substring(0, 1).ToUpperInvariant() + primaryClass.Trigram.Substring(1).ToLowerInvariant() + foreignClass.Trigram.Substring(0, 1).ToUpperInvariant() + foreignClass.Trigram.Substring(1).ToLowerInvariant();
            }
            else if (pClassComposeFClass)
            {
                associationCode = primaryClass.Name + "List";
            }
            else
            {
                associationCode = foreignClass.Name;
            }

            if (string.IsNullOrEmpty(role))
            {
                if (code != associationCode)
                {
                    RegisterError(Category.Error, "L'association [" + code + "] devrait avoir pour code [" + associationCode + "].");
                    return true;
                }
            }
            else
            {
                if (!code.StartsWith(associationCode, StringComparison.Ordinal) || code == associationCode)
                {
                    RegisterError(Category.Error, "L'association [" + code + "] devrait avoir un code commençant par [" + associationCode + "] suivi du rôle " + role + " en majuscule.");
                    return true;
                }
            }

            return false;
        }

        private void ParseModelFile(string modelFile, bool domainsOnly = false, bool addToList = true)
        {
            DisplayMessage("Analyse du fichier " + modelFile);
            using (var stream = new FileStream(modelFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                _currentOom = new XmlDocument();
                _currentOom.Load(stream);
            }

            _currentNsManager = new XmlNamespaceManager(_currentOom.NameTable);
            _currentNsManager.AddNamespace("xsl", "http://www.w3.org/1999/XSL/Transform");
            _currentNsManager.AddNamespace("a", "attribute");
            _currentNsManager.AddNamespace("c", "collection");
            _currentNsManager.AddNamespace("o", "object");

            var modelNode = _currentOom.SelectSingleNode(NodeModel, _currentNsManager);
            var root = new ModelRoot()
            {
                ModelFile = modelFile,
                Label = ParserHelper.GetXmlValue(modelNode.SelectSingleNode(PropertyName, _currentNsManager)),
                Name = ConvertModelName(ParserHelper.GetXmlValue(modelNode.SelectSingleNode(PropertyCode, _currentNsManager)))
            };
            _currentModelRoot = root;

            DisplayMessage("==> Parsing du modèle " + root.Label + "(" + root.Name + ")");
            DisplayMessage("--> Lecture des domaines.");
            BuildModelDomains(_currentOom.SelectSingleNode(NodeDomains, _currentNsManager), modelFile);

            if (!domainsOnly)
            {
                DisplayMessage("--> Lecture des namespaces.");
                var nmspacesNode = _currentOom.SelectSingleNode(NodeNamespace, _currentNsManager);
                BuildModelNamespaces(nmspacesNode, modelFile);
                DisplayMessage("--> Lecture des héritages.");
                BuildModelNamespaceGeneralizations(nmspacesNode, modelFile);
                DisplayMessage("--> Lecture des associations.");
                BuildModelNamespaceAssociations(nmspacesNode, modelFile);
            }
            else
            {
                _domainModelRoot = root;
                // Hack relativement sale, pour ne pas avoir les warnings (Le domaine de la propriété [] n'existe pas) sans raison
                // FIXME
                foreach (var domain in root.CreatedDomains)
                {
                    ModelDomainChecker.Instance.Check(domain);
                }
            }

            if (addToList)
            {
                _modelRootList.Add(root);
            }
        }

        /// <summary>
        /// Peuple la structure du modèle objet en paramètre à partir d'un fichier OOM.
        /// </summary>
        /// <returns>La collection de message d'erreurs au format NVortex.</returns>
        [SuppressMessage("Microsoft.Globalization", "CA1303:DoNotPassLiteralsAsLocalizedParameters", Justification = "Non internationalisé")]
        public override ICollection<ModelRoot> Parse()
        {
            ErrorList.Clear();
            _modelRootList = new List<ModelRoot>();

            // Chargement des domaines
            if (_domainModelFile != null)
            {
                ParseModelFile(_domainModelFile, true, false);
            }

            // Chargement des tables d'autres bases (dépendances)
            foreach (var modelFile in _extModelFiles)
            {
                ParseModelFile(modelFile, false, false);
            }

            // Chargement des tables et autres fichiers
            foreach (var modelFile in ModelFiles)
            {
                ParseModelFile(modelFile);
            }

            return ParseAliasColumn(_modelRootList);
        }

        /// <summary>
        /// Convertit le nom du modèle de l'OOM.
        /// - Remplace les "_" par des points (".").
        /// </summary>
        /// <param name="modelName">Nom de modèle au format PowerDesigner.</param>
        /// <returns>Nom de modèle pour .Net.</returns>
        private static string ConvertModelName(string modelName)
        {
            if (string.IsNullOrEmpty(modelName))
            {
                return modelName;
            }

            return modelName.Replace('_', '.');
        }

        /// <summary>
        /// Construit les objets de domaine.
        /// </summary>
        /// <param name="domainsNode">Le noeud XML caractérisant les domaines.</param>
        /// <param name="modelFile">Fichier modèle.</param>
        private void BuildModelDomains(XmlNode domainsNode, string modelFile)
        {
            foreach (XmlNode domainNode in domainsNode.ChildNodes)
            {
                if (domainNode.Name == NodeShortcut)
                {
                    var domainCode = ParserHelper.GetXmlValue(domainNode.SelectSingleNode(PropertyCode, _currentNsManager));
                    var domain = GetDomainByCode(domainCode);
                    _currentModelRoot.AddDomainShortcut(domainNode.Attributes["Id"].Value, domain);
                }
                else
                {
                    var domaine = new ModelDomain()
                    {
                        Name = ParserHelper.GetXmlValue(domainNode.SelectSingleNode(PropertyName, _currentNsManager)),
                        ModelFile = modelFile,
                        Code = ParserHelper.GetXmlValue(domainNode.SelectSingleNode(PropertyCode, _currentNsManager)),
                        DataType = ParserHelper.GetXmlValue(domainNode.SelectSingleNode(PropertyDataType, _currentNsManager)),
                        PersistentDataType = ParserHelper.GetXmlValue(domainNode.SelectSingleNode(PropertyPersistentDataType, _currentNsManager)),
                        PersistentLength = ParserHelper.GetXmlInt(domainNode.SelectSingleNode(PropertyPersistentLength, _currentNsManager)),
                        PersistentPrecision = ParserHelper.GetXmlInt(domainNode.SelectSingleNode(PropertyPersistentPrecision, _currentNsManager)),
                        CustomAnnotation = ParserHelper.GetXmlValue(domainNode.SelectSingleNode(DomainCustomAnnotation, _currentNsManager)),
                        CustomUsings = ParserHelper.GetXmlValue(domainNode.SelectSingleNode(DomainCustomUsings, _currentNsManager)),
                        Stereotype = ParserHelper.GetXmlValue(domainNode.SelectSingleNode(DomainStereotype, _currentNsManager)),

                        Model = _currentModelRoot
                    };
                    _currentModelRoot.AddDomain(domainNode.Attributes["Id"].Value, domaine);
                }
            }
        }

        /// <summary>
        /// Retourne un domaine a partir de sa clef de raccourci.
        /// </summary>
        /// <param name="domainCode">Code du domaine recherché.</param>
        /// <returns>Le domaine.</returns>
        [SuppressMessage("Microsoft.Globalization", "CA1303:DoNotPassLiteralsAsLocalizedParameters", Justification = "Non internationalisé")]
        private ModelDomain GetDomainByCode(string domainCode)
        {
            if (_domainModelRoot != null && _domainModelRoot.HasDomainByCode(domainCode))
            {
                return _domainModelRoot.GetDomainByCode(domainCode);
            }

            foreach (var modelRoot in _modelRootList)
            {
                if (modelRoot.HasDomainByCode(domainCode))
                {
                    return modelRoot.GetDomainByCode(domainCode);
                }
            }

            throw new KeyNotFoundException("Le domaine lié au code " + domainCode + " est introuvable.");
        }

        /// <summary>
        /// Construit les namespaces.
        /// </summary>
        /// <param name="nmspacesNode">Noeud XML caractérisant les namespaces.</param>
        /// <param name="modelFile">Nom du modèle contenant la classe.</param>
        [SuppressMessage("Microsoft.Globalization", "CA1303:DoNotPassLiteralsAsLocalizedParameters", Justification = "Non internationalisé")]
        private void BuildModelNamespaces(XmlNode nmspacesNode, string modelFile)
        {
            foreach (XmlNode nmspaceNode in nmspacesNode.ChildNodes)
            {
                var nmspace = new ModelNamespace()
                {
                    Label = ParserHelper.GetXmlValue(nmspaceNode.SelectSingleNode(PropertyName, _currentNsManager)),
                    Name = ParserHelper.GetXmlValue(nmspaceNode.SelectSingleNode(PropertyCode, _currentNsManager)),
                    Comment = ParserHelper.GetXmlValue(nmspaceNode.SelectSingleNode(PropertyComment, _currentNsManager)),
                    Creator = ParserHelper.GetXmlValue(nmspaceNode.SelectSingleNode(PropertyCreator, _currentNsManager)),
                    Model = _currentModelRoot,
                    IsExternal = _extModelFiles.Contains(modelFile)
                };
                if (string.IsNullOrEmpty(nmspace.Comment))
                {
                    RegisterError(Category.Doc, "Le package [" + nmspace.Label + "] n'a pas de commentaire.");
                }

                BuildNamespaceClasses(nmspace, nmspaceNode, modelFile);
                _currentModelRoot.AddNamespace(nmspace);
            }

            foreach (var nsName in _currentModelRoot.Namespaces.Keys)
            {
                var nmsp = _currentModelRoot.Namespaces[nsName];
                foreach (var classe in nmsp.ClassList)
                {
                    foreach (var property in classe.PropertyList)
                    {
                        if (property.DataType != null && _regexICollection.IsMatch(property.DataType))
                        {
                            var match = _regexICollectionGenericClasse.Match(property.DataType);
                            if (match.Success)
                            {
                                var innerType = match.Value.Substring(1, match.Value.Length - 2);
                                if (_classByNameMap.ContainsKey(innerType))
                                {
                                    var usingClasse = _classByNameMap[innerType];
                                    classe.AddUsing(usingClasse.Namespace);
                                }
                            }
                            else
                            {
                                RegisterError(Category.Bug, "Impossible de récupérer le type générique de la collection pour la propriété [" + property.Name + "] de la classe [" + property.Class.Name + "].");
                            }
                        }

                        if (classe.ParentClass != null)
                        {
                            classe.AddUsing(classe.ParentClass.Namespace);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Ajoute les propriétés correspondantes aux clés étrangères dans les objets.
        /// </summary>
        /// <param name="nmspacesNode">Le namespace racine à parcourir.</param>
        /// <param name="modelFile">Nom du fichier modèle en cours de traitement.</param>
        [SuppressMessage("Microsoft.Globalization", "CA1303:DoNotPassLiteralsAsLocalizedParameters", Justification = "Non internationalisé")]
        private void BuildModelNamespaceAssociations(XmlNode nmspacesNode, string modelFile)
        {
            foreach (XmlNode nmspaceNode in nmspacesNode.ChildNodes)
            {
                var associationsNodeList = nmspaceNode.SelectSingleNode(NodeAssociations, _currentNsManager);
                if (associationsNodeList != null)
                {
                    foreach (XmlNode associationNode in associationsNodeList.ChildNodes)
                    {
                        if (associationNode.Name == NodeShortcut)
                        {
                            continue;
                        }

                        var name = ParserHelper.GetXmlValue(associationNode.SelectSingleNode(PropertyName, _currentNsManager));
                        var code = ParserHelper.GetXmlValue(associationNode.SelectSingleNode(PropertyCode, _currentNsManager));
                        var comment = ParserHelper.GetXmlValue(associationNode.SelectSingleNode(PropertyComment, _currentNsManager));
                        var multiplicityA = ParserHelper.GetXmlValue(associationNode.SelectSingleNode(PropertyRoleMultiplicityA, _currentNsManager));
                        var multiplicityB = ParserHelper.GetXmlValue(associationNode.SelectSingleNode(PropertyRoleMultiplicityB, _currentNsManager));
                        var roleAName = ParserHelper.GetXmlValue(associationNode.SelectSingleNode(PropertyRoleNameA, _currentNsManager));
                        var roleBName = ParserHelper.GetXmlValue(associationNode.SelectSingleNode(PropertyRoleNameB, _currentNsManager));
                        var navigabilityA = ParserHelper.GetXmlInt(associationNode.SelectSingleNode(PropertyNavigabilityA, _currentNsManager)).GetValueOrDefault(0); // By default A is set to 0 (won't be present)
                        var navigabilityB = ParserHelper.GetXmlInt(associationNode.SelectSingleNode(PropertyNavigabilityB, _currentNsManager)).GetValueOrDefault(1); // By default B is set to 1 (won't be present)
                        var initialValueB = ParserHelper.GetXmlValue(associationNode.SelectSingleNode(PropertyInitialValueB, _currentNsManager));
                        if (string.IsNullOrWhiteSpace(initialValueB))
                        {
                            initialValueB = null;
                        }

                        ModelClass[] classAssociationTab;
                        try
                        {
                            classAssociationTab = GetModelClassAssociation(associationNode, modelFile);
                        }
                        catch (Exception ex)
                        {
                            throw new NotSupportedException(
                                string.Format(
                                    CultureInfo.CurrentCulture,
                                    @"Impossible de retrouver les classes de l'association suivante : [Code]:{0}, [Nom]:{1}. 
                                    Vérifier que la classe avec la multiplicité la plus faible est bien définie avant l'autre classe.
                                    L'ordre de parsing des modèles est défini dans Build\class_generator.conf.",
                                    code,
                                    name),
                                ex);
                        }

                        var classA = classAssociationTab[0];
                        var classB = classAssociationTab[1];

                        var propertyIndicatorA = ParserHelper.GetXmlValue(associationNode.SelectSingleNode(PropertyIndicatorA, _currentNsManager));
                        var propertyIndicatorB = ParserHelper.GetXmlValue(associationNode.SelectSingleNode(PropertyIndicatorB, _currentNsManager));
                        var siAcomposeB = propertyIndicatorA != null && propertyIndicatorA == "C";
                        var siBcomposeA = propertyIndicatorA != null && propertyIndicatorB == "C";
                        var typeAssociation = propertyIndicatorA ?? propertyIndicatorB;

                        if (multiplicityA != Multiplicity01 && multiplicityA != Multiplicity11 && multiplicityA != Multiplicty0N && multiplicityA != Multiplicty1N)
                        {
                            RegisterError(Category.Error, "La multiplicité " + multiplicityA + " du lien [" + code + "] entre les classes [" + classA.Name + "] et [" + classB.Name + "] n'est pas gérée.");
                            continue;
                        }

                        if (multiplicityB != Multiplicity01 && multiplicityB != Multiplicity11 && multiplicityB != Multiplicty0N && multiplicityB != Multiplicty1N)
                        {
                            RegisterError(Category.Error, "La multiplicité " + multiplicityB + " du lien [" + code + "] entre les classes [" + classA.Name + "] et [" + classB.Name + "] n'est pas gérée.");
                            continue;
                        }

                        var multiplicityOk = false;
                        if (multiplicityA == Multiplicty0N && (multiplicityB == Multiplicity01 || multiplicityB == Multiplicity11))
                        {
                            multiplicityOk = true;
                        }

                        if (multiplicityB == Multiplicty0N && (multiplicityA == Multiplicity01 || multiplicityA == Multiplicity11))
                        {
                            multiplicityOk = true;
                        }

                        if (!multiplicityOk && classB.IsTable && classA.IsTable)
                        {
                            RegisterError(Category.Error, "L'association [" + code + "] (" + multiplicityA + "/" + multiplicityB + ") entre les classes [" + classA.Name + "] et [" + classB.Name + "] n'est pas gérée.");
                            continue;
                        }

                        if ("A".Equals(typeAssociation) && classA.Stereotype == "Statique" && multiplicityB == Multiplicty0N && classB.Stereotype != "Statique")
                        {
                            RegisterError(Category.Error, "L'association [" + code + "] (" + multiplicityA + "/" + multiplicityB + ") entre les classes [" + classA.Name + "] statique et [" + classB.Name + "] n'est pas gérée.");
                            continue;
                        }

                        if (classB.Stereotype == "Statique" && multiplicityA == Multiplicty0N && classA.Stereotype != "Statique")
                        {
                            RegisterError(Category.Error, "L'association [" + code + "] (" + multiplicityA + "/" + multiplicityB + ") entre les classes [" + classA.Name + "] et [" + classB.Name + "] statique n'est pas gérée.");
                            continue;
                        }

                        if (classA.Stereotype == "Reference" && multiplicityB == Multiplicty0N && classB.Stereotype != "Statique" && classB.Stereotype != "Reference" && classB.IsTable)
                        {
                            RegisterError(Category.Error, "L'association [" + code + "] (" + multiplicityA + "/" + multiplicityB + ") entre les classes [" + classA.Name + "] reference et [" + classB.Name + "] n'est pas gérée.");
                            continue;
                        }

                        if (classB.Stereotype == "Reference" && multiplicityA == Multiplicty0N && classA.Stereotype != "Statique" && classA.Stereotype != "Reference" && !classA.IgnoreReferenceToReference)
                        {
                            RegisterError(Category.Error, "L'association [" + code + "] (" + multiplicityA + "/" + multiplicityB + ") entre les classes [" + classA.Name + "] et [" + classB.Name + "] reference n'est pas gérée.");
                            continue;
                        }

                        if (multiplicityA == Multiplicty0N && !string.IsNullOrEmpty(roleAName))
                        {
                            RegisterError(Category.Error, "L'association [" + code + "] ne doit pas définir de rôle sur la table " + classA.Name + " (cardinalité 0..*).");
                            continue;
                        }

                        if (multiplicityB == Multiplicty0N && !string.IsNullOrEmpty(roleBName))
                        {
                            RegisterError(Category.Error, "L'association [" + code + "] ne doit pas définir de rôle sur la table " + classB.Name + " (cardinalité 0..*).");
                            continue;
                        }

                        if (name.StartsWith("Association", StringComparison.OrdinalIgnoreCase))
                        {
                            RegisterError(Category.Error, "L'association [" + code + "] ne porte pas le libellé de la propriété associée à la clef étrangère.");
                            continue;
                        }

                        if (multiplicityA == Multiplicty0N)
                        {
                            if (CheckAssociationCode(classB, classA, code, roleBName, siBcomposeA, siAcomposeB))
                            {
                                continue;
                            }
                        }
                        else
                        {
                            if (CheckAssociationCode(classA, classB, code, roleAName, siAcomposeB, siBcomposeA))
                            {
                                continue;
                            }
                        }

                        if (!string.IsNullOrEmpty(typeAssociation) && "A".Equals(typeAssociation))
                        {
                            RegisterError(Category.Bug, "Le lien d'aggrégation [" + code + "] entre les classes [" + classA.Name + "] et [" + classB.Name + "] n'est pas géré.");
                            continue;
                        }

                        var isComposition = !string.IsNullOrEmpty(typeAssociation) && "C".Equals(typeAssociation);
                        if (isComposition)
                        {
                            // Si composition il faut traiter uniquement la cardinalité B et l'ajouter dans la classe A.
                            var property = ParserHelper.BuildClassCompositionProperty(classA, multiplicityB, roleAName, code, name);
                            property.Class = classB;
                            classB.AddProperty(property);
                            classB.AddUsing(classA.Namespace);
                            IndexAssociation(property, comment);
                        }
                        else
                        {
                            var isTreated = false;
                            var associateBIntoA = false;
                            var associateAIntoB = false;

                            // For 0..1/1..1 & 1..1/1..0 the FK will only be on the 1..1 side
                            // For 0..1 / 0..1 the FK will only be on the side with the navigability property set
                            if ((Multiplicity01.Equals(multiplicityA) && !Multiplicity11.Equals(multiplicityB)) || Multiplicity11.Equals(multiplicityA))
                            {
                                associateBIntoA = true;
                            }
                            if ((Multiplicity01.Equals(multiplicityB) && !Multiplicity11.Equals(multiplicityA)) || Multiplicity11.Equals(multiplicityB))
                            {
                                associateAIntoB = true;
                            }
                            if (Multiplicity01.Equals(multiplicityA) && Multiplicity01.Equals(multiplicityB))
                            {
                                associateBIntoA = navigabilityA == 1;
                                associateAIntoB = navigabilityB == 1;
                            }

                            if (associateBIntoA)
                            {
                                // On ajoute la clé primaire de la classe B dans la classe A
                                var property = ParserHelper.BuildClassAssociationProperty(classB, classA, multiplicityA, roleAName, name);
                                if (classA.DataContract.IsPersistent && !classB.DataContract.IsPersistent)
                                {
                                    RegisterError(Category.Error, "L'association [" + code + "] de multiplicité 0..1/1..1 entre la classe persistente [" + classA.Name + "] et la classe non persistente [" + classB.Name + "] n'est pas possible.");
                                    continue;
                                }

                                property.Class = classA;
                                classA.AddProperty(property);
                                IndexAssociation(property, comment);
                                isTreated = true;
                            }

                            if (associateAIntoB)
                            {
                                // On ajoute la clé primaire de la classe A dans la classe B
                                var property = ParserHelper.BuildClassAssociationProperty(classA, classB, multiplicityB, roleBName, name, initialValueB);
                                if (classB.DataContract.IsPersistent && !classA.DataContract.IsPersistent)
                                {
                                    RegisterError(Category.Error, "L'association [" + code + "] de multiplicité 0..1/1..1 entre la classe persistente [" + classB.Name + "] et la classe non persistente [" + classA.Name + "] n'est pas possible.");
                                    continue;
                                }

                                property.Class = classB;
                                classB.AddProperty(property);
                                IndexAssociation(property, comment);
                                isTreated = true;
                            }

                            if (!isTreated)
                            {
                                RegisterError(Category.Bug, "Le lien d'association [" + code + "] entre les classes [" + classA.Name + "] et [" + classB.Name + "] nommé " + name + " (code=" + code + ") n'est pas géré actuellement.");
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Ajoute les classes d'héritage.
        /// </summary>
        /// <param name="nmspacesNode">Le namespace racine à parcourir.</param>
        /// <param name="modelFile">Nom du fichier modèle en cours de traitement.</param>
        private void BuildModelNamespaceGeneralizations(XmlNode nmspacesNode, string modelFile)
        {
            if (nmspacesNode == null)
            {
                throw new ArgumentNullException("nmspacesNode");
            }

            foreach (XmlNode nmspaceNode in nmspacesNode.ChildNodes)
            {
                var generalizationsNode = nmspaceNode.SelectSingleNode(NodeGeneralizations, _currentNsManager);
                if (generalizationsNode != null)
                {
                    foreach (XmlNode generalizationNode in generalizationsNode.ChildNodes)
                    {
                        var classAssociationTab = GetModelClassAssociation(generalizationNode, modelFile);
                        classAssociationTab[1].ParentClass = classAssociationTab[0];
                        classAssociationTab[1].AddUsing(classAssociationTab[0].Namespace);
                    }
                }
            }
        }

        /// <summary>
        /// Retourne les 2 classes concernées par une association.
        /// </summary>
        /// <param name="associationNode">Le noeud association concerné.</param>
        /// <param name="modelFile">Nom du fichier modèle en cours de traitement.</param>
        /// <returns>Les 2 classes concernées par l'association.</returns>
        private ModelClass[] GetModelClassAssociation(XmlNode associationNode, string modelFile)
        {
            var object1NodeList = associationNode.SelectSingleNode(NodeObject1, _currentNsManager).ChildNodes;
            var object1Node = object1NodeList[0]; // 1 seul noeud normalement
            var object2NodeList = associationNode.SelectSingleNode(NodeObject2, _currentNsManager).ChildNodes;
            var object2Node = object2NodeList[0]; // 1 seul noeud normalement

            if (object1Node != null && object2Node != null)
            {
                var class1 = NodeShortcut.Equals(object1Node.Name) ? GetModelClassByShortcutId(object1Node.Attributes["Ref"].Value, modelFile) : _classByIdMap[modelFile + ":" + object1Node.Attributes["Ref"].Value];
                var class2 = NodeShortcut.Equals(object2Node.Name) ? GetModelClassByShortcutId(object2Node.Attributes["Ref"].Value, modelFile) : _classByIdMap[modelFile + ":" + object2Node.Attributes["Ref"].Value];
                return new ModelClass[] { class1, class2 };
            }

            return null;
        }

        /// <summary>
        /// Construit les objets de classe.
        /// </summary>
        /// <param name="nmspace">Namespace courant.</param>
        /// <param name="nmspaceNode">Le noeud XML caractérisant le namespace courant.</param>
        /// <param name="modelFile">Nom du modèle.</param>
        [SuppressMessage("Microsoft.Globalization", "CA1303:DoNotPassLiteralsAsLocalizedParameters", Justification = "Non internationalisé")]
        private void BuildNamespaceClasses(ModelNamespace nmspace, XmlNode nmspaceNode, string modelFile)
        {
            foreach (XmlNode classNode in nmspaceNode.SelectSingleNode(NodeClasses, _currentNsManager).SelectNodes(NodeClass, _currentNsManager))
            {
                var classe = new ModelClass()
                {
                    Label = ParserHelper.GetXmlValue(classNode.SelectSingleNode(PropertyName, _currentNsManager)),
                    Name = ParserHelper.GetXmlValue(classNode.SelectSingleNode(PropertyCode, _currentNsManager)),
                    Comment = ParserHelper.GetXmlValue(classNode.SelectSingleNode(PropertyComment, _currentNsManager)),
                    Stereotype = ParserHelper.GetXmlValue(classNode.SelectSingleNode(PropertyStereotype, _currentNsManager)),
                    Namespace = nmspace,
                    ModelFile = modelFile,
                    IsExternal = _extModelFiles.Contains(modelFile)
                };

                if (!string.IsNullOrEmpty(classe.Stereotype) && classe.Stereotype != "Reference" && classe.Stereotype != "Statique")
                {
                    RegisterError(Category.Error, "Classe " + classe.Name + " : seuls les stéréotypes 'Reference' et 'Statique' sont acceptés.");
                }

                var persistence = ParserHelper.GetXmlValue(classNode.SelectSingleNode(PropertyClassPersitence, _currentNsManager));
                var isPersistent = string.IsNullOrEmpty(persistence) ? true : "T".Equals(persistence) ? false : true;
                var persistentCode = ParserHelper.GetXmlValue(classNode.SelectSingleNode(PropertyPersistentCode, _currentNsManager));

                if (isPersistent)
                {
                    var tableName = ParserHelper.ConvertCsharp2Bdd(classe.Name);
                    if (string.IsNullOrEmpty(persistentCode))
                    {
                        persistentCode = tableName;
                    }
                    else if (persistentCode.Equals(tableName))
                    {
                        RegisterError(Category.Error, "Classe " + classe.Name + " : le nom de la table ne doit pas être saisie si il peut être déduit du nom de la classe.");
                    }
                }

                ReadAnnotations(classe, classNode);
                classe.DataContract = new ModelDataContract()
                {
                    Name = persistentCode,
                    Namespace = nmspace.Model.Name + "." + nmspace.Name,
                    IsPersistent = isPersistent
                };

                // Chargement et création des propriétés de la classe
                BuildClassProperties(classe, classNode, modelFile);

                // Détermine la DefaultProperty de la classe
                foreach (var property in classe.PropertyList)
                {
                    if (!string.IsNullOrEmpty(property.Stereotype))
                    {
                        if (property.Stereotype == "Order")
                        {
                            classe.DefaultOrderModelProperty = property;
                        }
                        else
                        {
                            if (!string.IsNullOrEmpty(classe.DefaultProperty))
                            {
                                RegisterError(Category.Bug, "La classe " + classe.Name + " a déjà une valeur pour la propriété DefaultProperty.");
                            }
                            else
                            {
                                classe.DefaultProperty = property.Name;
                                classe.DefaultModelProperty = property;
                                if (classe.DefaultOrderModelProperty == null)
                                {
                                    classe.DefaultOrderModelProperty = property;
                                }
                            }
                        }
                    }
                }

                nmspace.AddClass(classe);

                _classByIdMap.Add(modelFile + ":" + classNode.Attributes["Id"].Value, classe);
                _classByObjectIdMap.Add(ParserHelper.GetXmlValue(classNode.SelectSingleNode(PropertyObjectId, _currentNsManager)), classe);

                if (_classByNameMap.ContainsKey(classe.Name))
                {
                    RegisterError(Category.Bug, "Doublons dans le nommage des classes du modèle : la classe [" + classe.Name + "] existe déjà.");
                }
                else
                {
                    _classByNameMap.Add(classe.Name, classe);
                }

                if (classe.AuditEnabled)
                {
                    AddAuditProperties(classe, modelFile);
                }

                if (classe.ExportDeltaEnabled)
                {
                    AddExportDeltaProperties(classe, modelFile);
                }

                if (classe.IsOriginSap)
                {
                    AddSapProperties(classe, modelFile);
                }
            }

            // Mapping des racourcis vers les objets déclarés dans d'autres packages
            foreach (XmlNode shortcutNode in nmspaceNode.SelectSingleNode(NodeClasses, _currentNsManager).SelectNodes(NodeShortcut, _currentNsManager))
            {
                var shortCutId = string.Format(CultureInfo.InvariantCulture, "{0}\\{1}", modelFile, shortcutNode.Attributes["Id"].Value);
                var propertyValue = ParserHelper.GetXmlValue(shortcutNode.SelectSingleNode(PropertyTargetId, _currentNsManager));
                if (_shortcutClassIdMap.ContainsKey(shortCutId))
                {
                    Console.Error.WriteLine("Shortcut déja existant pour " + shortCutId + "/" + propertyValue);
                }
                else
                {
                    _shortcutClassIdMap.Add(shortCutId, propertyValue);
                }
            }
        }

        private void AddSapProperties(ModelClass classe, string modelFile)
        {
            classe.AddProperty(new ModelProperty()
            {
                Name = "SapId",
                Comment = "Sap identifier.",
                IsPersistent = true,
                DataType = "string",
                Stereotype = null,
                Class = classe,
                ModelFile = modelFile,
                DataDescription = new ModelDataDescription()
                {
                    Libelle = "SapId",
                    Domain = _currentModelRoot.GetDomainByCode(DomaineCodeCode),
                    IsPrimaryKey = false
                },
                DataMember = new ModelDataMember()
                {
                    Name = "SAP_ID",
                    IsRequired = false
                }
            });
        }

        /// <summary>
        /// Ajoute les propriétés d'audit à la classe.
        /// </summary>
        /// <param name="classe">Classe.</param>
        /// <param name="modelFile">Nom du fichier modèle.</param>
        private void AddAuditProperties(ModelClass classe, string modelFile)
        {
            classe.AddProperty(new ModelProperty()
            {
                Name = "UtilisateurIdCreation",
                Comment = "Identifiant de l'utilisateur ayant créé l'objet.",
                IsPersistent = true,
                DataType = "int",
                Stereotype = null,
                Class = classe,
                ModelFile = modelFile,
                DataDescription = new ModelDataDescription()
                {
                    Libelle = "Utilisateur créateur",
                    Domain = _currentModelRoot.GetDomainByCode(DomaineIdCode),
                    IsPrimaryKey = false
                },
                DataMember = new ModelDataMember()
                {
                    Name = "UTI_ID_CREATION",
                    IsRequired = true
                }
            });

            classe.AddProperty(new ModelProperty()
            {
                Name = "UtilisateurIdModificateur",
                Comment = "Identifiant de l'utilisateur ayant modifié l'objet.",
                IsPersistent = true,
                DataType = "int",
                Stereotype = null,
                Class = classe,
                ModelFile = modelFile,
                DataDescription = new ModelDataDescription()
                {
                    Libelle = "Utilisateur modificateur",
                    Domain = _currentModelRoot.GetDomainByCode(DomaineIdCode),
                    IsPrimaryKey = false
                },
                DataMember = new ModelDataMember()
                {
                    Name = "UTI_ID_MODIFICATION",
                    IsRequired = true
                }
            });

            classe.AddProperty(new ModelProperty()
            {
                Name = "DateCreation",
                Comment = "Date de création de l'objet.",
                IsPersistent = true,
                DataType = "DateTime?",
                Stereotype = null,
                Class = classe,
                ModelFile = modelFile,
                DataDescription = new ModelDataDescription()
                {
                    Libelle = "Date de création",
                    Domain = _currentModelRoot.GetDomainByCode(DomaineHorodatageCode),
                    IsPrimaryKey = false
                },
                DataMember = new ModelDataMember()
                {
                    Name = classe.Trigram + "_DATE_CREATION",
                    IsRequired = true
                }
            });

            classe.AddProperty(new ModelProperty()
            {
                Name = "DateModif",
                Comment = "Date de dernière modification de l'objet.",
                IsPersistent = true,
                DataType = "DateTime?",
                Stereotype = null,
                Class = classe,
                ModelFile = modelFile,
                DataDescription = new ModelDataDescription()
                {
                    Libelle = "Date de dernière modification",
                    Domain = _currentModelRoot.GetDomainByCode(DomaineHorodatageCode),
                    IsPrimaryKey = false
                },
                DataMember = new ModelDataMember()
                {
                    Name = classe.Trigram + "_DATE_MODIF",
                    IsRequired = true
                }
            });

            classe.AddProperty(new ModelProperty()
            {
                Name = "NumeroVersion",
                Comment = "Numéro de version",
                IsPersistent = true,
                DataType = "int?",
                Stereotype = null,
                Class = classe,
                ModelFile = modelFile,
                DataDescription = new ModelDataDescription()
                {
                    Libelle = "Numéro de version",
                    Domain = _currentModelRoot.GetDomainByCode(DomaineEntierCode),
                    IsPrimaryKey = false
                },
                DataMember = new ModelDataMember()
                {
                    Name = classe.Trigram + "_NUMERO_VERSION",
                    IsRequired = true
                }
            });
        }

        /// <summary>
        /// Ajoute les propriétés d'export en delta à la classe.
        /// </summary>
        /// <param name="classe">Classe.</param>
        /// <param name="modelFile">Nom du fichier modèle.</param>
        private void AddExportDeltaProperties(ModelClass classe, string modelFile)
        {
            classe.AddProperty(new ModelProperty()
            {
                Name = "DateModification",
                Comment = "Date de dernière modification de l'objet.",
                IsPersistent = true,
                DataType = "DateTime?",
                Stereotype = null,
                Class = classe,
                ModelFile = modelFile,
                DataDescription = new ModelDataDescription()
                {
                    Libelle = "Date de dernière modification",
                    Domain = _currentModelRoot.GetDomainByCode(DomaineHorodatageCode),
                    IsPrimaryKey = false
                },
                DataMember = new ModelDataMember()
                {
                    Name = classe.Trigram + "_DATE_MODIFICATION",
                    IsRequired = false
                }
            });

            classe.AddProperty(new ModelProperty()
            {
                Name = "DateEnvoi",
                Comment = "Date de dernièr export de l'objet.",
                IsPersistent = true,
                DataType = "DateTime?",
                Stereotype = null,
                Class = classe,
                ModelFile = modelFile,
                DataDescription = new ModelDataDescription()
                {
                    Libelle = "Date de dernièr export de l'objet",
                    Domain = _currentModelRoot.GetDomainByCode(DomaineHorodatageCode),
                    IsPrimaryKey = false
                },
                DataMember = new ModelDataMember()
                {
                    Name = classe.Trigram + "_DATE_ENVOI",
                    IsRequired = false
                }
            });
        }

        /// <summary>
        /// Retourne la liste des identifiants d'attributs oom marqués comme clés primaires.
        /// </summary>
        /// <param name="classNode">Le noeud caractérisant la classe.</param>
        /// <returns>La collection des identifiants d'attributs OOM identifiant les clés primaires de la classe.</returns>
        private ICollection<string> GetClassPrimaryKeysReferenceList(XmlNode classNode)
        {
            ICollection<string> ret = new Collection<string>();

            // Identifiant de l'objet reférencant les clés primaires
            ICollection<string> pkIdList = new Collection<string>();
            var pkNodes = classNode.SelectSingleNode(NodePrimaryKeys, _currentNsManager);
            if (pkNodes != null)
            {
                var pkNodeList = pkNodes.ChildNodes;
                foreach (XmlNode pkNode in pkNodeList)
                {
                    pkIdList.Add(pkNode.Attributes["Ref"].Value);
                }

                // Récupération des identifiants des objets oom clés primaires
                var identifiersNode = classNode.SelectSingleNode(NodeIdentifiers, _currentNsManager);
                var identifierNodeList = identifiersNode.ChildNodes;
                foreach (XmlNode identifierNode in identifierNodeList)
                {
                    if (pkIdList.Contains(identifierNode.Attributes["Id"].Value))
                    {
                        var identifierAttrbutesNode = identifierNode.SelectSingleNode(NodeIdentifiersAttributes, _currentNsManager);
                        if (identifierAttrbutesNode != null && identifierAttrbutesNode.HasChildNodes)
                        {
                            var identifierAttrbutesNodeList = identifierAttrbutesNode.ChildNodes;
                            foreach (XmlNode pkIdentifierNode in identifierAttrbutesNodeList)
                            {
                                ret.Add(pkIdentifierNode.Attributes["Ref"].Value);
                            }
                        }
                    }
                }
            }

            return ret;
        }

        /// <summary>
        /// Construit les propriétés d'une classe.
        /// </summary>
        /// <param name="classe">Classe considérée.</param>
        /// <param name="classNode">Le noeud caractérisant la classe parsée.</param>
        /// <param name="modelFile">Fichier modèle.</param>
        [SuppressMessage("Microsoft.Globalization", "CA1303:DoNotPassLiteralsAsLocalizedParameters", Justification = "Non internationalisé")]
        private void BuildClassProperties(ModelClass classe, XmlNode classNode, string modelFile)
        {
            var pkRefList = GetClassPrimaryKeysReferenceList(classNode);
            var attributesNode = classNode.SelectSingleNode(NodeAttributes, _currentNsManager);
            if (attributesNode != null)
            {
                foreach (XmlNode propertyNode in attributesNode.ChildNodes)
                {
                    var propertyId = propertyNode.Attributes["Id"].Value;
                    var persistent = ParserHelper.GetXmlValue(propertyNode.SelectSingleNode(PropertyPersistent, _currentNsManager));
                    var multiplicity = ParserHelper.GetXmlValue(propertyNode.SelectSingleNode(PropertyMultiplicity, _currentNsManager));
                    var defaultValue = ParserHelper.GetXmlValue(propertyNode.SelectSingleNode(PropertyDefaultValue, _currentNsManager));

                    var property = new ModelProperty()
                    {
                        Name = ParserHelper.GetXmlValue(propertyNode.SelectSingleNode(PropertyCode, _currentNsManager)),
                        Comment = ParserHelper.GetXmlValue(propertyNode.SelectSingleNode(PropertyComment, _currentNsManager)),
                        IsPersistent = !string.IsNullOrEmpty(persistent) && "0".Equals(persistent) ? false : true,
                        DataType = GetModelDomainByPropertyNode(propertyNode).DataType,
                        Stereotype = ParserHelper.GetXmlValue(propertyNode.SelectSingleNode(PropertyStereotype, _currentNsManager)),
                        Class = classe,
                        ModelFile = modelFile,
                        DefaultValue = string.IsNullOrWhiteSpace(defaultValue) ? null : defaultValue,
                        IsDerived = ParserHelper.GetXmlInt(propertyNode.SelectSingleNode(PropertyDerived, _currentNsManager)).GetValueOrDefault() == 0 ? false : true,
                        DataDescription = new ModelDataDescription()
                        {
                            Libelle = ParserHelper.GetXmlValue(propertyNode.SelectSingleNode(PropertyName, _currentNsManager)),
                            Domain = GetModelDomainByPropertyNode(propertyNode),
                            IsPrimaryKey = pkRefList.Contains(propertyId)
                        }
                    };

                    foreach (var annotation in ReadAnnotations(propertyNode))
                    {
                        if (annotation.Name == "Metadata")
                        {
                            property.Metadata = annotation.Value;
                        }

                        if (annotation.Name == "Unique")
                        {
                            property.IsUnique = true;
                        }
                        if (annotation.Name == "UniqueNullable")
                        {
                            property.IsUniqueNullable = true;
                        }

                        if (annotation.Name == "Reprise")
                        {
                            property.IsReprise = true;
                        }

                        if (annotation.Name == "UniqueMultiple")
                        {
                            property.IsUniqueMany = true;
                        }

                        if (annotation.Name == "IsIdManuallySet")
                        {
                            property.IsIdManuallySet = true;
                        }

                        property.AddAnnotation(annotation);
                    }

                    if (property.DataDescription.Domain == null)
                    {
                        RegisterError(Category.Error, "Propriété " + classe.Name + "." + property.Name + " : aucun domaine n'a été défini.");
                        continue;
                    }

                    var dataMemberName = ParserHelper.GetXmlValue(propertyNode.SelectSingleNode(PropertyPersistentCode, _currentNsManager));
                    if (property.IsPersistent)
                    {
                        var columnName = ParserHelper.ConvertCsharp2Bdd(property.Name);
                        if (string.IsNullOrEmpty(dataMemberName))
                        {
                            if (property.DataDescription.IsPrimaryKey)
                            {
                                classe.Trigram = property.DataDescription.Libelle;
                            }

                            dataMemberName = property.IsReprise ? columnName : classe.Trigram + "_" + columnName;
                        }
                        else if (dataMemberName.Equals(columnName))
                        {
                            RegisterError(Category.Error, "Propriété " + classe.Name + "." + property.Name + " : le nom de la colonne ne doit pas être saisie si il peut être déduit du nom de la classe.");
                        }
                    }

                    property.DataMember = new ModelDataMember()
                    {
                        Name = dataMemberName,
                        IsRequired = multiplicity != null && "1..1".Equals(multiplicity)
                    };
                    if (property.DataDescription.IsPrimaryKey || !string.IsNullOrEmpty(property.DataDescription.ReferenceType))
                    {
                        property.DataDescription.Libelle = property.Name;
                    }

                    classe.AddProperty(property);

                    if (!string.IsNullOrEmpty(property.Stereotype) && property.Stereotype != "DefaultProperty" && property.Stereotype != "Order")
                    {
                        RegisterError(Category.Error, "Propriété " + classe.Name + "." + property.Name + " : seuls les stéréotypes 'DefaultProperty' et 'Order' sont acceptés.");
                    }
                }
            }
        }

        /// <summary>
        /// Lit une annotations métadonnées sur un noeud.
        /// </summary>
        /// <param name="node">Noeud XML.</param>
        /// <returns>Métadonnées.</returns>
        private IEnumerable<ModelAnnotation> ReadAnnotations(XmlNode node)
        {
            var annotations = new List<ModelAnnotation>();
            var annotationsNode = node.SelectSingleNode("c:Annotations", _currentNsManager);
            if (annotationsNode != null)
            {
                foreach (XmlNode annotationNode in annotationsNode.SelectNodes("o:Annotation", _currentNsManager))
                {
                    var name = ParserHelper.GetXmlValue(annotationNode.SelectSingleNode("a:Annotation.Name", _currentNsManager));
                    annotations.Add(new ModelAnnotation()
                    {
                        Name = name,
                        Value = ParserHelper.GetXmlValue(annotationNode.SelectSingleNode("a:Annotation.Text", _currentNsManager))
                    });
                }
            }

            return annotations;
        }

        /// <summary>
        /// Lit les annotations sur un noeud.
        /// </summary>
        /// <param name="classe">Classe.</param>
        /// <param name="node">Noeud XML.</param>
        private void ReadAnnotations(ModelClass classe, XmlNode node)
        {
            foreach (var annotation in ReadAnnotations(node))
            {
                if (annotation.Name == "Metadata")
                {
                    classe.Metadata = annotation.Value;
                }
                else if (annotation.Name == "Storage")
                {
                    classe.Storage = annotation.Value;
                }
                else if (annotation.Name == "Audit")
                {
                    classe.AuditEnabled = true;
                }
                else if (annotation.Name == "ExportDelta")
                {
                    classe.ExportDeltaEnabled = true;
                }
                else if (annotation.Name == "IgnoreReferenceToReference")
                {
                    classe.IgnoreReferenceToReference = true;
                }
                else if (annotation.Name == "NonTranslatable")
                {
                    classe.IsNonTranslatable = true;
                }
                else if (annotation.Name == "Historized")
                {
                    classe.IsHistorized = true;
                }
                else if (annotation.Name == "OriginSap")
                {
                    classe.IsOriginSap = true;
                }
                else if (annotation.Name == "DisableAccessorImplementation")
                {
                    classe.DisableAccessorImplementation = true;
                }
            }
        }

        /// <summary>
        /// Retourne le domaine associé à la propriété.
        /// </summary>
        /// <param name="propertyNode">Noeud XML correspondant à la propriété.</param>
        /// <returns>Le domaine.</returns>
        private ModelDomain GetModelDomainByPropertyNode(XmlNode propertyNode)
        {
            if (propertyNode == null)
            {
                throw new ArgumentNullException("propertyNode");
            }

            var attributeDomainsNode = propertyNode.SelectSingleNode(NodeAttributeDomain, _currentNsManager);
            if (attributeDomainsNode != null)
            {
                var attributeDomainNodeList = attributeDomainsNode.ChildNodes;
                if (attributeDomainNodeList.Count == 1)
                {
                    return _currentModelRoot.UsableDomains[attributeDomainNodeList.Item(0).Attributes["Ref"].Value];
                }
            }

            return null;
        }

        /// <summary>
        /// Retourne la classe associée a l'identifiant de raccourci en paramètre.
        /// </summary>
        /// <param name="shortcutId">L'identifiant du racourci.</param>
        /// <param name="modelFile">Nom du fichier modèle.</param>
        /// <returns>La classe correspondante.</returns>
        private ModelClass GetModelClassByShortcutId(string shortcutId, string modelFile)
        {
            var classTargetId = _shortcutClassIdMap[modelFile + "\\" + shortcutId];
            return classTargetId == null ? null : _classByObjectIdMap[classTargetId];
        }
    }
}
