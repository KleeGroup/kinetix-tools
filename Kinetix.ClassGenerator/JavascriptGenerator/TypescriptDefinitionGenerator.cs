using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Kinetix.ClassGenerator.Model;

namespace Kinetix.ClassGenerator.JavascriptGenerator
{
    using static Singletons;
    using static Utils;

    /// <summary>
    /// Générateur de définitions Typescript.
    /// </summary>
    public static class TypescriptDefinitionGenerator
    {
        /// <summary>
        /// Génère les définitions Typescript.
        /// </summary>
        /// <param name="modelRootList">La liste des modèles.</param>
        public static void Generate(ICollection<ModelRoot> modelRootList)
        {
            if (GeneratorParameters.Javascript.ModelOutputDirectory == null)
            {
                return;
            }

            var nameSpaceMap = new Dictionary<string, List<ModelClass>>();
            foreach (var model in modelRootList)
            {
                foreach (var modelNameSpace in model.Namespaces.Values)
                {
                    var namespaceName = ToNamespace(modelNameSpace.Name);

                    if (!nameSpaceMap.ContainsKey(namespaceName))
                    {
                        nameSpaceMap.Add(namespaceName, new List<ModelClass>());
                    }

                    nameSpaceMap[namespaceName].AddRange(modelNameSpace.ClassList);
                }
            }

            foreach (var entry in nameSpaceMap)
            {
                var staticLists = new List<ModelClass>();

                foreach (var model in entry.Value)
                {
                    if (!model.IsStatique)
                    {
                        var fileName = model.Name.ToDashCase();
                        Console.Out.WriteLine($"Generating Typescript file: {fileName}.ts ...");

                        fileName = $"{GeneratorParameters.Javascript.ModelOutputDirectory}/{entry.Key.ToDashCase(false)}/{fileName}.ts";
                        var fileInfo = new FileInfo(fileName);

                        var isNewFile = !fileInfo.Exists;

                        var directoryInfo = fileInfo.Directory;
                        if (!directoryInfo.Exists)
                        {
                            Directory.CreateDirectory(directoryInfo.FullName);
                        }

                        var template = new TypescriptTemplate { RootNamespace = GeneratorParameters.RootNamespace, Model = model };
                        var result = template.TransformText();
                        File.WriteAllText(fileName, result, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                    }
                    else
                    {
                        staticLists.Add(model);
                    }
                }

                if (staticLists.Any())
                {
                    Console.Out.WriteLine($"Generating Typescript file: references.ts ...");
                    var fileName = $"{GeneratorParameters.Javascript.ModelOutputDirectory}/{entry.Key.ToDashCase(false)}/references.ts";
                    var fileInfo = new FileInfo(fileName);

                    var isNewFile = !fileInfo.Exists;

                    var directoryInfo = fileInfo.Directory;
                    if (!directoryInfo.Exists)
                    {
                        Directory.CreateDirectory(directoryInfo.FullName);
                    }

                    var template = new ReferenceTemplate { References = staticLists.OrderBy(r => r.Name) };
                    var result = template.TransformText();
                    File.WriteAllText(fileName, result, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                }
            }
        }
    }
}
