﻿using System;
using System.Collections.Generic;
using Kinetix.ClassGenerator.Model;
using Kinetix.ClassGenerator.NVortex;

namespace Kinetix.ClassGenerator.XmlParser
{
    /// <summary>
    /// Classe abstraite définissant le comportement des parsers.
    /// </summary>
    internal abstract class AbstractParser : IModelParser
    {
        /// <summary>
        /// Cardinalité des associations 0-1.
        /// </summary>
        internal const string Multiplicity01 = "0..1";

        /// <summary>
        /// Cardinalité des associations 1-1.
        /// </summary>
        internal const string Multiplicity11 = "1..1";

        /// <summary>
        /// Cardinalité des associations 0-n.
        /// </summary>
        internal const string Multiplicty0N = "0..*";

        /// <summary>
        /// Cardinalité des associations 1-n.
        /// </summary>
        internal const string Multiplicty1N = "1..*";

#pragma warning disable SA1401

        /// <summary>
        /// Liste des modeles power designer.
        /// </summary>
        protected readonly ICollection<string> ModelFiles;

#pragma warning restore SA1401

        /// <summary>
        /// Constructeur.
        /// </summary>
        protected AbstractParser()
        {
            ErrorList = new List<NVortexMessage>();
        }

        /// <summary>
        /// Constructeur.
        /// </summary>
        /// <param name="modelFiles">Liste des modèles à analyser.</param>
        /// <param name="domainList">Liste des domaines.</param>
        protected AbstractParser(ICollection<string> modelFiles)
            : this()
        {
            ModelFiles = modelFiles ?? throw new ArgumentNullException("modelFiles");
        }

        /// <summary>
        /// Collection d'erreurs au format NVortex.
        /// </summary>
        public ICollection<NVortexMessage> ErrorList
        {
            get;
            set;
        }

        /// <summary>
        /// Retourne le résultat d'analyse du modele parsé.
        /// Les erreurs sont consultables via la propriété ErrorList.
        /// </summary>
        /// <returns>La collection de message d'erreurs au format NVortex.</returns>
        public abstract ICollection<ModelRoot> Parse();

        /// <summary>
        /// Affiche un message du déroulement.
        /// </summary>
        /// <param name="trace">Texte à afficher.</param>
        protected static void DisplayMessage(string trace)
        {
            Console.Out.WriteLine(trace);
        }

        /// <summary>
        /// Indexe une association et une propriété.
        /// </summary>
        /// <param name="property">Propriété.</param>
        /// <param name="comment">Commentaire de l'association.</param>
        protected static void IndexAssociation(ModelProperty property, string comment)
        {
            int index = comment.IndexOf("Metadata:", StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                property.Metadata = comment.Substring(index + 9);
            }
        }

        /// <summary>
        /// Parse the alias columns of the model.
        /// </summary>
        /// <param name="modelRootList">List of the models.</param>
        /// <returns>Parsed model.</returns>
        protected ICollection<ModelRoot> ParseAliasColumn(ICollection<ModelRoot> modelRootList)
        {
            ICollection<ModelProperty> objectToUpdate = new List<ModelProperty>();
            IDictionary<string, ModelProperty> modelPropertyMap = new Dictionary<string, ModelProperty>();
            foreach (ModelRoot model in modelRootList)
            {
                foreach (ModelNamespace ns in model.Namespaces.Values)
                {
                    foreach (ModelClass item in ns.ClassList)
                    {
                        foreach (ModelProperty mp in item.PropertyList)
                        {
                            if (!mp.IsFromComposition && mp.DataDescription.Domain.Code == "DO_ALIAS")
                            {
                                objectToUpdate.Add(mp);
                            }
                            else
                            {
                                try
                                {
                                    modelPropertyMap.Add(FormatAliasColumn(item.Name, mp.Name), mp);
                                }
                                catch (ArgumentException e)
                                {
                                    Console.WriteLine("Conflict on key :" + FormatAliasColumn(item.Name, mp.Name) + " Class:" + item.Name);
                                    ModelProperty other = modelPropertyMap[FormatAliasColumn(item.Name, mp.Name)];
                                    Console.WriteLine("And on Class:" + other.Class.Name);
                                    throw e;
                                }
                            }
                        }
                    }
                }
            }

            foreach (ModelProperty mp in objectToUpdate)
            {
                string[] splittedName = mp.Name.Split('_');
                if (splittedName.Length != 2 && splittedName.Length != 3 && splittedName.Length != 4)
                {
                    throw new Exception("La propriété d'alias '" + mp.Name + "' est mal formattée. Format Attendu : Table_Colonne ou Table_Colonne_Pre ou Table_Colonne__Post ou Table_Colonne_Pre_Post. Classe : " + mp.Class.FullyQualifiedName);
                }

                string key = FormatAliasColumn(splittedName[0], splittedName[1]);

                if (!modelPropertyMap.ContainsKey(key))
                {
                    throw new Exception("Il n'existe pas d'alias pour la propriété : " + mp.Name + " pour la classe : "+ mp.Class.FullyQualifiedName);
                }

                ModelProperty alias = modelPropertyMap[key];
                mp.Name = alias.Name;

                if (splittedName.Length >= 3)
                {
                    mp.Name = splittedName[2] + mp.Name;
                }

                if (splittedName.Length >= 4)
                {
                    mp.Name += splittedName[3];
                }

                if (mp.DataDescription.Libelle.StartsWith("[Override]"))
                {
                    mp.DataDescription.Libelle = mp.DataDescription.Libelle.Replace("[Override]", "");
                }
                else
                {
                    mp.DataDescription.Libelle = alias.DataDescription.Libelle;
                }

                mp.DataMember = alias.DataMember;
                mp.DataDescription.Domain = alias.DataDescription.Domain;
                mp.DataDescription.ReferenceClass = alias.DataDescription.ReferenceClass ?? (alias.Class.IsStatique && alias.IsPrimaryKey ? alias.Class : null);
                mp.DataDescription.ReferenceType = alias.DataDescription.ReferenceType ?? (alias.Class.IsStatique && alias.IsPrimaryKey ? alias.Class.FullyQualifiedName : null);
                mp.DataDescription.ResourceKey = alias.DataDescription.ResourceKey;
                mp.Role = alias.Role;

                mp.DataType = alias.DataType;
                mp.IsPersistent = alias.IsPersistent;
                mp.Comment = alias.Comment;
            }

            return modelRootList;
        }

        /// <summary>
        /// Enregistre un message de type erreur.
        /// </summary>
        /// <param name="category">La catégorie de l'erreur.</param>
        /// <param name="message">Le message d'erreur.</param>
        protected void RegisterError(Category category, string message)
        {
            RegisterError(category, null, message);
        }

        /// <summary>
        /// Enregistre un message de type erreur.
        /// </summary>
        /// <param name="category">La catégorie de l'erreur.</param>
        /// <param name="ex">Le message d'erreur.</param>
        /// <param name="message">Message additif.</param>
        protected void RegisterError(Category category, Exception ex, string message)
        {
            var nVortexMessage = new NVortexMessage
            {
                IsError = true,
                Category = category
            };

            if (ex == null)
            {
                nVortexMessage.Description = string.IsNullOrEmpty(message) ? string.Empty : " - " + message;
            }
            else
            {
                nVortexMessage.FileName = ex.Source;
                nVortexMessage.Description = ex.TargetSite + " : " + ex.Message + (string.IsNullOrEmpty(message) ? string.Empty : " - " + message);
            }

            ErrorList.Add(nVortexMessage);
        }

        /// <summary>
        /// Format the aliases column (TABLENAME_COLNAME).
        /// </summary>
        /// <param name="tableName">Table name.</param>
        /// <param name="colName">Column name.</param>
        /// <returns>Parser alias column.</returns>
        private static string FormatAliasColumn(string tableName, string colName)
        {
            return tableName + "_" + colName;
        }
    }
}
