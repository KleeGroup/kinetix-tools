using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Kinetix.Tools.Common;
using Kinetix.Tools.Common.Model;
using Kinetix.Tools.Common.Parameters;

namespace Kinetix.ClassGenerator.JavascriptGenerator
{
    /// <summary>
    /// Générateur de définitions Typescript.
    /// </summary>
    public static class TypescriptDefinitionGenerator
    {
        /// <summary>
        /// Génère les définitions Typescript.
        /// </summary>
        /// <param name="rootNamespace">Namespace de l'application.</param>
        /// <param name="parameters">Paramètres.</param>
        /// <param name="modelRootList">La liste des modèles.</param>
        public static void Generate(string rootNamespace, JavascriptParameters parameters, ICollection<ModelRoot> modelRootList)
        {
            if (parameters.ModelOutputDirectory == null)
            {
                return;
            }

            var nameSpaceMap = new Dictionary<string, List<ModelClass>>();
            foreach (var model in modelRootList)
            {
                foreach (var modelNameSpace in model.Namespaces.Values)
                {
                    var namespaceName = TSUtils.ToNamespace(modelNameSpace.Name);

                    if (!nameSpaceMap.ContainsKey(namespaceName))
                    {
                        nameSpaceMap.Add(namespaceName, new List<ModelClass>());
                    }

                    nameSpaceMap[namespaceName].AddRange(modelNameSpace.ClassList);
                }
            }

            var staticLists = new List<ModelClass>();

            foreach (var entry in nameSpaceMap)
            {
                foreach (var model in entry.Value)
                {
                    if (!model.IsStatique)
                    {
                        if (!parameters.IsGenerateEntities && model.DataContract.IsPersistent)
                        {
                            continue;
                        }

                        var fileName = model.Name.ToDashCase();
                        Console.Out.WriteLine($"Generating Typescript file: {fileName}.ts ...");

                        fileName = $"{parameters.ModelOutputDirectory}/{entry.Key.ToDashCase(false)}/{fileName}.ts";
                        var fileInfo = new FileInfo(fileName);

                        var isNewFile = !fileInfo.Exists;

                        var directoryInfo = fileInfo.Directory;
                        if (!directoryInfo.Exists)
                        {
                            Directory.CreateDirectory(directoryInfo.FullName);
                        }

                        var template = new TypescriptTemplate { RootNamespace = rootNamespace, Model = model, Focus4v8 = parameters.Focus4v8 };
                        var result = template.TransformText();
                        File.WriteAllText(fileName, result, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    }
                    else
                    {
                        staticLists.Add(model);
                    }
                }

                if (!parameters.Focus4v8)
                {
                    GenerateReferenceLists(parameters, staticLists, entry.Key);
                    staticLists.Clear();
                }
            }

            if (parameters.Focus4v8)
            {
                GenerateReferenceLists(parameters, staticLists);
            }
        }

        private static void GenerateReferenceLists(JavascriptParameters parameters, IList<ModelClass> staticLists, string namespaceName = null)
        {
            if (staticLists.Any())
            {
                Console.Out.WriteLine($"Generating Typescript file: references.ts ...");

                var fileName = namespaceName != null
                    ? $"{parameters.ModelOutputDirectory}/{namespaceName.ToDashCase(false)}/references.ts"
                    : $"{parameters.ModelOutputDirectory}/references.ts";

                var fileInfo = new FileInfo(fileName);

                var isNewFile = !fileInfo.Exists;

                var directoryInfo = fileInfo.Directory;
                if (!directoryInfo.Exists)
                {
                    Directory.CreateDirectory(directoryInfo.FullName);
                }

                var template = new ReferenceTemplate { References = staticLists.OrderBy(r => r.Name) , Focus4v8 = parameters.Focus4v8 };
                var result = template.TransformText();
                File.WriteAllText(fileName, result, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
        }
    }
}
