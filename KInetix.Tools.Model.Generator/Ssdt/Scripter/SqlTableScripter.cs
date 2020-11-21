﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Kinetix.Tools.Model.Generator.Ssdt.Scripter
{
    /// <summary>
    /// Scripter permettant d'écrire les scripts de création d'une table SQL avec :
    /// - sa structure
    /// - sa contrainte PK
    /// - ses contraintes FK
    /// - ses indexes FK
    /// - ses contraintes d'unicité sur colonne unique.
    /// </summary>
    public class SqlTableScripter : ISqlScripter<Class>
    {
        private readonly SsdtConfig _config;

        public SqlTableScripter(SsdtConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// Calcul le nom du script pour la table.
        /// </summary>
        /// <param name="item">Table à scripter.</param>
        /// <returns>Nom du fichier de script.</returns>
        public string GetScriptName(Class item)
        {
            return item == null
                ? throw new ArgumentNullException("item")
                : item.SqlName + ".sql";
        }

        /// <summary>
        /// Ecrit dans un flux le script de création pour la table.
        /// </summary>
        /// <param name="writer">Flux d'écriture.</param>
        /// <param name="item">Table à scripter.</param>
        public void WriteItemScript(TextWriter writer, Class item)
        {
            if (writer == null)
            {
                throw new ArgumentNullException("writer");
            }

            if (item == null)
            {
                throw new ArgumentNullException("item");
            }

            // TODO : rendre paramétrable.
            var useCompression = false;

            // Entête du fichier.
            WriteHeader(writer, item.SqlName);

            // Ouverture du create table.
            WriteCreateTableOpening(writer, item);

            // Intérieur du create table.
            var properties = WriteInsideInstructions(writer, item);

            // Fin du create table.
            WriteCreateTableClosing(writer, item, useCompression);

            // Indexes sur les clés étrangères.
            GenerateIndexForeignKey(writer, item.SqlName, properties);

            // Définition
            WriteTableDescriptionProperty(writer, item);
        }

        /// <summary>
        /// Génère les indexes portant sur les FK.
        /// </summary>
        /// <param name="writer">Flux d'écriture.</param>
        /// <param name="tableName">Nom de la table.</param>
        /// <param name="properties">Champs.</param>
        private void GenerateIndexForeignKey(TextWriter writer, string tableName, IList<IFieldProperty> properties)
        {
            var fkList = properties.OfType<AssociationProperty>().ToList();
            foreach (var property in fkList)
            {
                var propertyName = ((IFieldProperty)property).SqlName;
                var indexName = "IDX_" + tableName + "_" + propertyName + "_FK";

                writer.WriteLine("/* Index on foreign key column for " + tableName + "." + propertyName + " */");
                writer.WriteLine("create nonclustered index [" + indexName + "]");
                writer.Write("\ton [dbo].[" + tableName + "] (");
                var propertyConcat = "[" + propertyName + "] ASC";

                writer.Write(propertyConcat);
                writer.Write(")");

                writer.WriteLine();
                writer.WriteLine("go");
                writer.WriteLine();
            }
        }

        /// <summary>
        /// Ecrit le SQL pour une colonne.
        /// </summary>
        /// <param name="sb">Flux.</param>
        /// <param name="property">Propriété.</param>
        private void WriteColumn(StringBuilder sb, IFieldProperty property)
        {
            var persistentType = property.Domain.SqlType;
            sb.Append("[").Append(property.SqlName).Append("] ").Append(persistentType);
            if (property.PrimaryKey && property.Domain.Name == "DO_ID" && !_config.DisableIdentity)
            {
                sb.Append(" identity");
            }

            if (property.Required && !property.PrimaryKey)
            {
                sb.Append(" not null");
            }

            if (property is { Domain: var domain, DefaultValue: var dv } && !string.IsNullOrWhiteSpace(dv))
            {
                if (domain.ShouldQuoteSqlValue)
                {
                    sb.Append($" default '{dv}'");
                }
                else
                {
                    sb.Append($" default {dv}");
                }
            }
        }

        /// <summary>
        /// Génère la contrainte de clef étrangère.
        /// </summary>
        /// <param name="sb">Flux d'écriture.</param>
        /// <param name="property">Propriété portant la clef étrangère.</param>
        private void WriteConstraintForeignKey(StringBuilder sb, AssociationProperty property)
        {
            var tableName = property.Class.SqlName;

            var propertyName = ((IFieldProperty)property).SqlName;
            var referenceClass = property.Association;

            var constraintName = "FK_" + tableName + "_" + referenceClass.SqlName + "_" + propertyName;
            var propertyConcat = "[" + propertyName + "]";
            sb.Append("constraint [").Append(constraintName).Append("] foreign key (").Append(propertyConcat).Append(") ");
            sb.Append("references [dbo].[").Append(referenceClass.SqlName).Append("] (");
            sb.Append("[").Append(referenceClass.PrimaryKey!.SqlName).Append("]");
            sb.Append(")");
        }

        /// <summary>
        /// Ecrit le pied du script.
        /// </summary>
        /// <param name="writer">Flux.</param>
        /// <param name="classe">Classe de la table.</param>
        /// <param name="useCompression">Indique si on utilise la compression.</param>
        private void WriteCreateTableClosing(TextWriter writer, Class classe, bool useCompression)
        {
            if (writer == null)
            {
                throw new ArgumentNullException("writer");
            }

            if (classe == null)
            {
                throw new ArgumentNullException("classe");
            }

            writer.WriteLine(")");

            if (useCompression)
            {
                writer.WriteLine("WITH (DATA_COMPRESSION=PAGE)");
            }

            writer.WriteLine("go");
            writer.WriteLine();
        }

        /// <summary>
        /// Ecrit l'ouverture du create table.
        /// </summary>
        /// <param name="writer">Flux.</param>
        /// <param name="table">Table.</param>
        private void WriteCreateTableOpening(TextWriter writer, Class table)
        {
            writer.WriteLine("create table [dbo].[" + table.SqlName + "] (");
        }

        /// <summary>
        /// Ecrit l'entête du fichier.
        /// </summary>
        /// <param name="writer">Flux.</param>
        /// <param name="tableName">Nom de la table.</param>
        private void WriteHeader(TextWriter writer, string tableName)
        {
            writer.WriteLine("-- ===========================================================================================");
            writer.WriteLine("--   Description		:	Création de la table " + tableName + ".");
            writer.WriteLine("-- ===========================================================================================");
            writer.WriteLine();
        }

        /// <summary>
        /// Ecrit les instructions à l'intérieur du create table.
        /// </summary>
        /// <param name="writer">Flux.</param>
        /// <param name="table">Table.</param>
        private IList<IFieldProperty> WriteInsideInstructions(TextWriter writer, Class table)
        {
            // Construction d'une liste de toutes les instructions.
            var definitions = new List<string>();
            var sb = new StringBuilder();

            // Colonnes
            var properties = table.Properties.OfType<IFieldProperty>().ToList();
            if (table.Extends != null)
            {
                properties.Add(new AssociationProperty
                {
                    Association = table.Extends,
                    Class = table,
                    Required = true
                });
            }

            foreach (var property in properties)
            {
                sb.Clear();
                WriteColumn(sb, property);
                definitions.Add(sb.ToString());
            }

            // Primary Key
            sb.Clear();
            WritePkLine(sb, table);

            definitions.Add(sb.ToString());

            // Foreign key constraints
            var fkList = properties.OfType<AssociationProperty>().ToList();
            foreach (var property in fkList)
            {
                sb.Clear();
                WriteConstraintForeignKey(sb, property);
                definitions.Add(sb.ToString());
            }

            // Unique constraints
            definitions.AddRange(WriteUniqueConstraints(table));

            // Ecriture de la liste concaténée.
            var separator = "," + Environment.NewLine;
            writer.Write(string.Join(separator, definitions.Select(x => "\t" + x)));

            return properties;
        }

        /// <summary>
        /// Ecrit la ligne de création de la PK.
        /// </summary>
        /// <param name="sb">Flux.</param>
        /// <param name="classe">Classe.</param>
        private void WritePkLine(StringBuilder sb, Class classe)
        {
            var pkCount = 0;

            if (!classe.Properties.Any(p => p.PrimaryKey) && !classe.Properties.All(p => p is AssociationProperty))
            {
                return;
            }

            sb.Append("constraint [PK_").Append(classe.SqlName).Append("] primary key clustered (");

            if (classe.Properties.All(p => p is AssociationProperty))
            {
                foreach (var fkProperty in classe.Properties.OfType<IFieldProperty>())
                {
                    ++pkCount;
                    sb.Append($"[{fkProperty.SqlName}] ASC");
                    if (pkCount < classe.Properties.Count)
                    {
                        sb.Append(",");
                    }
                    else
                    {
                        sb.Append(")");
                    }
                }
            }
            else
            {
                sb.Append(string.Join(", ", classe.Properties.OfType<IFieldProperty>().Where(p => p.PrimaryKey).Select(p => $"[{p.SqlName}] ASC")));
                sb.Append(")");
            }
        }

        /// <summary>
        /// Ecrit la création de la propriété de description de la table.
        /// </summary>
        /// <param name="writer">Writer.</param>
        /// <param name="classe">Classe de la table.</param>
        private void WriteTableDescriptionProperty(TextWriter writer, Class classe)
        {
            writer.WriteLine("/* Description property. */");
            writer.WriteLine("EXECUTE sp_addextendedproperty 'Description', '" + ScriptUtils.PrepareDataToSqlDisplay(classe.Label) + "', 'SCHEMA', 'dbo', 'TABLE', '" + classe.SqlName + "';");
        }

        /// <summary>
        /// Calcule la liste des déclarations de contraintes d'unicité.
        /// </summary>
        /// <param name="classe">Classe de la table.</param>
        /// <returns>Liste des déclarations de contraintes d'unicité.</returns>
        private IList<string> WriteUniqueConstraints(Class classe)
        {
            return classe.UniqueKeys == null
                ? new List<string>()
                : classe.UniqueKeys.Select(uk =>
                    $"constraint [UK_{classe.SqlName}_{string.Join("_", uk.Select(p => p.SqlName))}] unique nonclustered ({string.Join(", ", uk.Select(p => $"[{p.SqlName}] ASC"))})")
                .ToList();
        }
    }
}
