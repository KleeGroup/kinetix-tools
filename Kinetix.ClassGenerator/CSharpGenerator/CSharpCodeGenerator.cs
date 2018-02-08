using System;
using System.Collections.Generic;
using System.IO;
using Kinetix.ClassGenerator.Model;

namespace Kinetix.ClassGenerator.CSharpGenerator
{
    using static CodeUtils;

    /// <summary>
    /// Générateur de code C#.
    /// </summary>
    public static class CSharpCodeGenerator
    {
        /// <summary>
        /// Génère le code des classes.
        /// </summary>
        /// <param name="modelRootList">Liste des modeles.</param>
        public static void Generate(ICollection<ModelRoot> modelRootList)
        {
            if (modelRootList == null)
            {
                throw new ArgumentNullException(nameof(modelRootList));
            }

            DbContextGenerator.Generate(modelRootList);

            foreach (ModelRoot model in modelRootList)
            {
                if (model.Namespaces != null && model.Namespaces.Values.Count > 0)
                {
                    foreach (ModelNamespace ns in model.Namespaces.Values)
                    {
                        if (!Directory.Exists(ns.Name))
                        {
                            var directoryForModelClass = GetDirectoryForModelClass(ns.HasPersistentClasses, model.Name, ns.Name);
                            var projectDirectory = GetDirectoryForProject(ns.HasPersistentClasses, model.Name, ns.Name);
                            var csprojFileName = Path.Combine(projectDirectory, model.Name + "." + ns.Name + ".csproj");

                            foreach (ModelClass item in ns.ClassList)
                            {
                                var currentDirectory = GetDirectoryForModelClass(item.DataContract.IsPersistent, model.Name, item.Namespace.Name);
                                Directory.CreateDirectory(currentDirectory);
                                CSharpClassGenerator.Generate(item, ns);
                            }
                        }
                    }
                }
            }

            ReferenceAccessorGenerator.Generate(modelRootList);
        }
    }
}
