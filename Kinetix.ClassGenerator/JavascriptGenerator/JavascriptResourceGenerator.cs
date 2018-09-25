using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kinetix.Tools.Common;
using Kinetix.Tools.Common.Model;
using Kinetix.Tools.Common.Parameters;

namespace Kinetix.ClassGenerator.JavascriptGenerator
{
    /// <summary>
    /// Générateur des objets de traduction javascripts.
    /// </summary>
    public static class JavascriptResourceGenerator
    {
        /// <summary>
        /// Génère le code des classes.
        /// </summary>
        /// <param name="parameters">Paramètres.</param>
        /// <param name="modelRootList">Liste des modeles.</param>
        public static void Generate(JavascriptParameters parameters, ICollection<ModelRoot> modelRootList)
        {
            if (parameters.ResourceOutputDirectory == null)
            {
                return;
            }

            if (modelRootList == null)
            {
                throw new ArgumentNullException(nameof(modelRootList));
            }

            var nameSpaceMap = new Dictionary<string, List<ModelClass>>();
            foreach (var model in modelRootList)
            {
                foreach (var modelNameSpace in model.Namespaces.Values)
                {
                    string namespaceName = modelNameSpace.Name;

                    if (namespaceName.EndsWith("DataContract", StringComparison.Ordinal))
                    {
                        namespaceName = namespaceName.Substring(0, namespaceName.Length - 12);
                    }
                    else if (namespaceName.EndsWith("Contract", StringComparison.Ordinal))
                    {
                        namespaceName = namespaceName.Substring(0, namespaceName.Length - 8);
                    }

                    if (!nameSpaceMap.ContainsKey(namespaceName))
                    {
                        nameSpaceMap.Add(namespaceName, new List<ModelClass>());
                    }

                    nameSpaceMap[namespaceName].AddRange(modelNameSpace.ClassList);
                }
            }

            foreach (KeyValuePair<string, List<ModelClass>> entry in nameSpaceMap)
            {
                var dirInfo = Directory.CreateDirectory(parameters.ResourceOutputDirectory);
                var fileName = FirstToLower(entry.Key);

                Console.WriteLine($"Ecriture du fichier de ressource du module {entry.Key}.");
                WriteNameSpaceNode(dirInfo.FullName + "/" + fileName + ".ts", entry.Key, entry.Value);
            }
        }

        /// <summary>
        /// Set the first character to lower.
        /// </summary>
        /// <param name="value">String to edit.</param>
        /// <returns>Parser string.</returns>
        private static string FirstToLower(string value)
        {
            return value.Substring(0, 1).ToLowerInvariant() + value.Substring(1);
        }

        /// <summary>
        /// Formate le nom en javascript.
        /// </summary>
        /// <param name="name">Nom a formatter.</param>
        /// <returns>Nom formatté.</returns>
        private static string FormatJsName(string name)
        {
            return FirstToLower(name);
        }

        /// <summary>
        /// Formate le nom en javascript.
        /// </summary>
        /// <param name="name">Nom a formatter.</param>
        /// <returns>Nom formatté.</returns>
        private static string FormatJsPropertyName(string name)
        {
            return name.Substring(0, 1).ToLowerInvariant() + name.Substring(1);
        }

        /// <summary>
        /// Générère le noeus de classe.
        /// </summary>
        /// <param name="writer">Flux de sortie.</param>
        /// <param name="classe">Classe.</param>
        /// <param name="isLast">True s'il s'agit de al dernière classe du namespace.</param>
        private static void WriteClasseNode(TextWriter writer, ModelClass classe, bool isLast)
        {
            writer.WriteLine("    " + FormatJsName(classe.Name) + ": {");
            int i = 1;

            var properties = classe.PropertyList.Where(p => !p.IsParentId).ToList();
            foreach (ModelProperty property in properties)
            {
                WritePropertyNode(writer, property, properties.Count == i++);
            }

            WriteCloseBracket(writer, 1, isLast);
        }

        /// <summary>
        /// Ecrit dans le flux de sortie la fermeture du noeud courant.
        /// </summary>
        /// <param name="writer">Flux de sortie.</param>
        /// <param name="indentionLevel">Idention courante.</param>
        /// <param name="isLast">Si true, on n'ajoute pas de virgule à la fin.</param>
        private static void WriteCloseBracket(TextWriter writer, int indentionLevel, bool isLast)
        {
            for (int i = 0; i < indentionLevel; i++)
            {
                writer.Write("    ");
            }

            writer.Write("}");
            writer.WriteLine(!isLast ? "," : string.Empty);
        }

        /// <summary>
        /// Générère le noeud de namespace.
        /// </summary>
        /// <param name="outputFileNameJavascript">Nom du fichier de sortie..</param>
        /// <param name="namespaceName">Nom du namespace.</param>
        /// <param name="modelClassList">Liste des classe du namespace.</param>
        private static void WriteNameSpaceNode(string outputFileNameJavascript, string namespaceName, ICollection<ModelClass> modelClassList)
        {
            using (var writerJs = new FileWriter(outputFileNameJavascript, encoderShouldEmitUTF8Identifier: false))
            {
                writerJs.WriteLine($"export const {FirstToLower(namespaceName)} = {{");
                int i = 1;
                foreach (var classe in modelClassList)
                {
                    WriteClasseNode(writerJs, classe, modelClassList.Count == i++);
                }

                writerJs.WriteLine("};");
            }
        }

        /// <summary>
        /// Génère le noeud de la proprité.
        /// </summary>
        /// <param name="writer">Flux de sortie.</param>
        /// <param name="property">Propriété.</param>
        /// <param name="isLast">True s'il s'agit du dernier noeud de la classe.</param>
        private static void WritePropertyNode(TextWriter writer, ModelProperty property, bool isLast)
        {
            writer.WriteLine("        " + FormatJsPropertyName(property.Name) + @": """ + property.DataDescription.Libelle + @"""" + (isLast ? string.Empty : ","));
        }
    }
}
