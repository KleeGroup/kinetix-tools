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
        /// <summary>
        /// Calcul le nom du script pour la table.
        /// </summary>
        /// <param name="item">Table à scripter.</param>
        /// <returns>Nom du fichier de script.</returns>
        public string GetScriptName(Class item)
        {
            if (item == null)
            {
                throw new ArgumentNullException("item");
            }

            return item.SqlName + ".sql";
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
        private static void GenerateIndexForeignKey(TextWriter writer, string tableName, IList<IFieldProperty> properties)
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
        private static void WriteColumn(StringBuilder sb, IFieldProperty property)
        {
            var persistentType = property.Domain.SqlType;
            sb.Append("[").Append(property.SqlName).Append("] ").Append(persistentType);
            if (property.PrimaryKey && property.Domain.Name == "DO_ID")
            {
                sb.Append(" identity");
            }
            if (property.Required && !property.PrimaryKey)
            {
                sb.Append(" not null");
            }

            if (property is { Domain: { CsharpType: var type }, DefaultValue: var dv } && !string.IsNullOrWhiteSpace(dv))
            {
                if (type == "string")
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
        private static void WriteConstraintForeignKey(StringBuilder sb, AssociationProperty property)
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
        private static void WriteCreateTableClosing(TextWriter writer, Class classe, bool useCompression)
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
        private static void WriteCreateTableOpening(TextWriter writer, Class table)
        {
            writer.WriteLine("create table [dbo].[" + table.SqlName + "] (");
        }

        /// <summary>
        /// Ecrit l'entête du fichier.
        /// </summary>
        /// <param name="writer">Flux.</param>
        /// <param name="tableName">Nom de la table.</param>
        private static void WriteHeader(TextWriter writer, string tableName)
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
        private static IList<IFieldProperty> WriteInsideInstructions(TextWriter writer, Class table)
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
            definitions.AddRange(WriteUniqueConstraint(table));

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
        private static void WritePkLine(StringBuilder sb, Class classe)
        {
            sb.Append("constraint [PK_").Append(classe.SqlName).Append("] primary key clustered (");
            sb.Append("[").Append(classe.PrimaryKey!.SqlName).Append("] ASC");
            sb.Append(")");
        }

        /// <summary>
        /// Ecrit la création de la propriété de description de la table.
        /// </summary>
        /// <param name="writer">Writer.</param>
        /// <param name="classe">Classe de la table.</param>
        private static void WriteTableDescriptionProperty(TextWriter writer, Class classe)
        {
            writer.WriteLine("/* Description property. */");
            writer.WriteLine("EXECUTE sp_addextendedproperty 'Description', '" + ScriptUtils.PrepareDataToSqlDisplay(classe.Label) + "', 'SCHEMA', 'dbo', 'TABLE', '" + classe.SqlName + "';");
        }

        /// <summary>
        /// Calcule la liste des déclarations de contraintes d'unicité.
        /// </summary>        
        /// <param name="classe">Classe de la table.</param>
        /// <returns>Liste des déclarations de contraintes d'unicité.</returns>
        private static IList<string> WriteUniqueConstraint(Class classe)
        {

            var constraintList = new List<string>();

            var uniqueCount = classe.Properties.OfType<RegularProperty>().Count(p => p.Unique);

            // Contrainte d'unicité sur une seule colonne.
            if (uniqueCount == 1)
            {
                foreach (var columnProperty in classe.Properties.OfType<IFieldProperty>())
                {
                    if (columnProperty is RegularProperty { Unique: true, PrimaryKey: false })
                    {
                        constraintList.Add("constraint [UK_" + classe.SqlName + '_' + columnProperty.Name.ToUpperInvariant() + "] unique nonclustered ([" + columnProperty.SqlName + "] ASC)");
                    }
                }
            }
            else
            {
                // Contrainte d'unicité sur plusieurs colonnes.
                var columnList = classe.Properties.OfType<RegularProperty>().Where(p => p.Unique);
                if (columnList.Any())
                {
                    constraintList.Add("constraint [UK_" + classe.SqlName + "_MULTIPLE] unique (" + string.Join(", ", columnList.Select(x => "[" + ((IFieldProperty)x).SqlName + "]")) + ")");
                }
            }

            return constraintList;
        }
    }
}
