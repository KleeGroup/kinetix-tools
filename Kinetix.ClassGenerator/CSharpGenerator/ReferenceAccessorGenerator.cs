﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kinetix.Tools.Common.Model;
using Kinetix.Tools.Common.Parameters;

namespace Kinetix.ClassGenerator.CSharpGenerator
{
    using static CSharpUtils;

    public class ReferenceAccessorGenerator
    {
        private readonly string _rootNamespace;
        private readonly CSharpParameters _parameters;

        public ReferenceAccessorGenerator(string rootNamespace, CSharpParameters parameters)
        {
            _rootNamespace = rootNamespace;
            _parameters = parameters;
        }

        /// <summary>
        /// Génère les ReferenceAccessor.
        /// </summary>
        /// <param name="modelRootList">Liste de ModelRoot.</param>
        public void Generate(ICollection<ModelRoot> modelRootList)
        {
            foreach (var model in modelRootList)
            {
                foreach (var ns in model.Namespaces.Values)
                {
                    GenerateReferenceAccessor(ns);
                }
            }
        }

        /// <summary>
        /// Génère les ReferenceAccessor pour un namespace.
        /// </summary>
        /// <param name="nameSpace">Namespace.</param>
        private void GenerateReferenceAccessor(ModelNamespace nameSpace)
        {
            if (!nameSpace.HasPersistentClasses)
            {
                return;
            }

            var classList = nameSpace.ClassList
                .Where(x => x.DataContract.IsPersistent)
                .Where(x => x.Stereotype == Stereotype.Reference || x.Stereotype == Stereotype.Statique)
                .OrderBy(x => Pluralize(x.Name), StringComparer.Ordinal);

            if (!classList.Any())
            {
                return;
            }

            GenerateReferenceAccessorsInterface(classList, nameSpace.Name);
            GenerateReferenceAccessorsImplementation(classList.Where(x => x.DisableAccessorImplementation == false).ToList(), nameSpace.Name);
        }

        /// <summary>
        /// Génère l'implémentation des ReferenceAccessors.
        /// </summary>
        /// <param name="classList">Liste de ModelClass.</param>
        /// <param name="nameSpaceName">Namespace.</param>
        private void GenerateReferenceAccessorsImplementation(List<ModelClass> classList, string nameSpaceName)
        {
            var isBroker = _parameters.DbContextProjectPath == null;
            var nameSpacePrefix = nameSpaceName.Replace("DataContract", string.Empty);

            string projectDir;
            string projectName;
            string implementationName;
            if (!isBroker)
            {
                projectDir = $"{_parameters.OutputDirectory}\\{_parameters.DbContextProjectPath}";
                projectName = _parameters.DbContextProjectPath.Split('/').Last();
                implementationName = $"{nameSpacePrefix}AccessorsDal";
            }
            else
            {
                projectName = $"{_rootNamespace}.{nameSpacePrefix}Implementation";
                projectDir = Path.Combine(GetImplementationDirectoryName(_parameters.OutputDirectory, _rootNamespace), _rootNamespace + "." + nameSpacePrefix + "Implementation\\Service.Implementation");
                implementationName = $"Service{nameSpacePrefix}Accessors";
            }

            var interfaceName = $"I{implementationName}";

            Console.WriteLine("Generating class " + implementationName + " implementing " + interfaceName);
            var implementationFileName = Path.Combine(projectDir, isBroker ? "generated" : "generated\\Reference", $"{implementationName}.cs");

            using (var w = new CSharpWriter(implementationFileName))
            {
                var usings = new[]
                {
                    "System.Collections.Generic",
                    "System.Linq",
                    $"{_rootNamespace}.{nameSpaceName}"
                }.ToList();

                if (_parameters.Kinetix == "Core")
                {
                    usings.Add("Kinetix.Services.Annotations");
                }
                else
                {
                    usings.Add("System.ServiceModel");

                    if (_parameters.DbContextProjectPath == null)
                    {
                        usings.Add($"{_rootNamespace}.{nameSpacePrefix}Contract");
                        usings.Add(_parameters.Kinetix == "Framework"
                            ? "Kinetix.Broker"
                            : "Fmk.Broker");

                        if (classList.Any(classe => classe.DefaultOrderModelProperty != null))
                        {
                            usings.Add(_parameters.Kinetix == "Framework"
                                ? "Kinetix.Data.SqlClient"
                                : "Fmk.Data.SqlClient");
                        }
                    }
                }

                w.WriteUsings(usings.ToArray());

                w.WriteLine();
                w.WriteNamespace(projectName);

                w.WriteSummary(1, "This interface was automatically generated. It contains all the operations to load the reference lists declared in namespace " + nameSpaceName + ".");

                if (_parameters.Kinetix == "Core")
                {
                    w.WriteLine(1, "[RegisterImpl]");
                }
                else
                {
                    w.WriteLine(1, "[ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Multiple, InstanceContextMode = InstanceContextMode.PerCall, IncludeExceptionDetailInFaults = true)]");
                }

                w.WriteClassDeclaration(implementationName, null, new List<string> { interfaceName });

                if (!isBroker)
                {
                    var dbContextName = $"{_rootNamespace}DbContext";
                    var schema = _parameters.DbSchema;
                    if (schema != null)
                    {
                        dbContextName = $"{schema.First().ToString().ToUpper() + schema.Substring(1)}DbContext";
                    }

                    w.WriteLine(2, $"private readonly {dbContextName} _dbContext;");
                    w.WriteLine();
                    w.WriteSummary(2, "Constructeur");
                    w.WriteParam("dbContext", "DbContext");
                    w.WriteLine(2, $"public {implementationName}({dbContextName} dbContext)");
                    w.WriteLine(2, "{");
                    w.WriteLine(3, "_dbContext = dbContext;");
                    w.WriteLine(2, "}");
                    w.WriteLine();
                }

                foreach (var classe in classList)
                {
                    var serviceName = "Load" + (isBroker ? $"{classe.Name}List" : Pluralize(classe.Name));
                    w.WriteLine(2, "/// <inheritdoc cref=\"" + interfaceName + "." + serviceName + "\" />");
                    w.WriteLine(2, "public ICollection<" + classe.Name + "> " + serviceName + "()\r\n{");
                    w.WriteLine(3, LoadReferenceAccessorBody(isBroker, classe.Name, classe.DefaultOrderModelProperty));
                    w.WriteLine(2, "}");

                    if (classList.IndexOf(classe) != classList.Count - 1)
                    {
                        w.WriteLine();
                    }
                }

                w.WriteLine(1, "}");
                w.WriteLine("}");
            }
        }

        /// <summary>
        /// Génère l'interface déclarant les ReferenceAccessors d'un namespace.
        /// </summary>
        /// <param name="classList">Liste de ModelClass.</param>
        /// <param name="nameSpaceName">Namespace.</param>
        private void GenerateReferenceAccessorsInterface(IEnumerable<ModelClass> classList, string nameSpaceName)
        {
            var isBroker = _parameters.DbContextProjectPath == null;
            var nameSpacePrefix = nameSpaceName.Replace("DataContract", string.Empty);

            string projectDir;
            string projectName;
            string interfaceName;
            if (!isBroker)
            {
                projectDir = $"{_parameters.OutputDirectory}\\{_parameters.DbContextProjectPath}";
                projectName = _parameters.DbContextProjectPath.Split('/').Last();
                interfaceName = $"I{nameSpacePrefix}AccessorsDal";
            }
            else
            {
                projectName = $"{_rootNamespace}.{nameSpacePrefix}Contract";
                projectDir = Path.Combine(GetDirectoryForProject(_parameters.LegacyProjectPaths, _parameters.OutputDirectory, false, _rootNamespace, $"{nameSpacePrefix}Contract"));
                interfaceName = $"IService{nameSpacePrefix}Accessors";
            }

            Console.WriteLine("Generating interface " + interfaceName + " containing reference accessors for namespace " + nameSpaceName);
            var interfaceFileName = Path.Combine(projectDir, isBroker ? "generated" : "generated\\Reference", $"{interfaceName}.cs");

            using (var w = new CSharpWriter(interfaceFileName))
            {
                if (_parameters.Kinetix == "Core")
                {
                    w.WriteUsings(
                        "System.Collections.Generic",
                        $"{_rootNamespace}.{nameSpaceName}",
                        "Kinetix.Services.Annotations");
                }
                else
                {
                    w.WriteUsings(
                        "System.Collections.Generic",
                        "System.ServiceModel",
                        $"{_rootNamespace}.{nameSpaceName}",
                        _parameters.Kinetix == "Fmk" ? "Fmk.ServiceModel" : "Kinetix.ServiceModel");
                }

                w.WriteLine();
                w.WriteNamespace(projectName);
                w.WriteSummary(1, "This interface was automatically generated. It contains all the operations to load the reference lists declared in namespace " + nameSpaceName + ".");

                if (_parameters.Kinetix == "Core")
                {
                    w.WriteLine(1, "[RegisterContract]");
                }
                else
                {
                    w.WriteLine(1, "[ServiceContract]");
                }

                w.WriteLine(1, "public partial interface " + interfaceName + "\r\n{");

                var count = 0;
                foreach (var classe in classList)
                {
                    count++;
                    w.WriteSummary(2, "Reference accessor for type " + classe.Name);
                    w.WriteReturns(2, "List of " + classe.Name);
                    w.WriteLine(2, "[ReferenceAccessor]");
                    if (_parameters.Kinetix != "Core")
                    {
                        w.WriteLine(2, "[OperationContract]");
                    }
                    w.WriteLine(2, "ICollection<" + classe.Name + "> Load" + (isBroker ? $"{classe.Name}List" : Pluralize(classe.Name)) + "();");

                    if (count != classList.Count())
                    {
                        w.WriteLine();
                    }
                }

                w.WriteLine(1, "}");
                w.WriteLine("}");
            }
        }

        /// <summary>
        /// Retourne le code associé au cors de l'implémentation d'un service de type ReferenceAccessor.
        /// </summary>
        /// <param name="isBroker">Broker.</param>
        /// <param name="className">Nom du type chargé par le ReferenceAccessor.</param>
        /// <param name="defaultProperty">Propriété par defaut de la classe.</param>
        /// <returns>Code généré.</returns>
        private string LoadReferenceAccessorBody(bool isBroker, string className, ModelProperty defaultProperty)
        {
            var queryParameter = string.Empty;
            if (!isBroker)
            {
                if (defaultProperty != null)
                {
                    queryParameter = $".OrderBy(row => row.{defaultProperty.Name})";
                }

                return $"return _dbContext.{Pluralize(className)}{queryParameter}.ToList();";
            }
            else
            {
                if (defaultProperty != null)
                {
                    queryParameter = "new QueryParameter(" + className + ".Cols." + defaultProperty.DataMember.Name + ", SortOrder.Asc)";
                }

                return "return BrokerManager.GetStandardBroker<" + className + ">().GetAll(" + queryParameter + ");";
            }
        }
    }
}
