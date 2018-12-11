using System;
using System.Collections.Generic;
using System.IO;
using Kinetix.Tools.Common.Model;
using Kinetix.Tools.Common.Parameters;

namespace Kinetix.ClassGenerator.CSharpGenerator
{
    using static CSharpUtils;

    /// <summary>
    /// Générateur de code C#.
    /// </summary>
    public static class CSharpCodeGenerator
    {
        /// <summary>
        /// Génère le code des classes.
        /// </summary>
        /// <param name="rootNamespace">Namespace de l'application.</param>
        /// <param name="parameters">Paramètres génération C#</param>
        /// <param name="modelRootList">Liste des modeles.</param>
        public static void Generate(string rootNamespace, CSharpParameters parameters, ICollection<ModelRoot> modelRootList)
        {
            if (modelRootList == null)
            {
                throw new ArgumentNullException(nameof(modelRootList));
            }

            new DbContextGenerator(rootNamespace, parameters).Generate(modelRootList);

            var classGenerator = new CSharpClassGenerator(rootNamespace, parameters);

            foreach (ModelRoot model in modelRootList)
            {
                if (model.Namespaces != null && model.Namespaces.Values.Count > 0)
                {
                    foreach (ModelNamespace ns in model.Namespaces.Values)
                    {
                        if (!Directory.Exists(ns.Name))
                        {
                            var directoryForModelClass = GetDirectoryForModelClass(parameters.OutputDirectory, ns.HasPersistentClasses, model.Name, ns.Name);
                            var projectDirectory = GetDirectoryForProject(parameters.OutputDirectory, ns.HasPersistentClasses, model.Name, ns.Name);
                            var csprojFileName = Path.Combine(projectDirectory, model.Name + "." + ns.Name + ".csproj");

                            foreach (ModelClass item in ns.ClassList)
                            {
                                var currentDirectory = GetDirectoryForModelClass(parameters.OutputDirectory, item.DataContract.IsPersistent, model.Name, item.Namespace.Name);
                                Directory.CreateDirectory(currentDirectory);
                                classGenerator.Generate(item, ns);
                            }
                        }
                    }
                }
            }

            new ReferenceAccessorGenerator(rootNamespace, parameters).Generate(modelRootList);
        }
    }
}
