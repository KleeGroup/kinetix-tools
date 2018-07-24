using System;
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
        private const string ReferenceAccessorAttribute = "[ReferenceAccessor]";
        private const string ReferenceAccessorAttributeExt = "[OperationContract]";

        private readonly IDictionary<string, (string ServiceBehaviorAttribute, string ServiceContractAttribute)> ServiceAttributes = new Dictionary<string, (string ServiceBehaviorAttribute, string ServiceContractAttribute)>
        {
            { "Core", (ServiceBehaviorAttribute: "[RegisterImpl]" , ServiceContractAttribute: "[RegisterContract]") },
            { "Framework", (ServiceBehaviorAttribute: "[ServiceBehavior(ConcurrencyMode = ConcurrencyMode.Multiple, InstanceContextMode = InstanceContextMode.PerCall, IncludeExceptionDetailInFaults = true)]", ServiceContractAttribute: "[ServiceContract]") }
        };

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
            GenerateReferenceAccessorsImplementation(classList.Where(x => x.DisableAccessorImplementation == false), nameSpace.Name);
        }

        /// <summary>
        /// Génère l'implémentation des ReferenceAccessors.
        /// </summary>
        /// <param name="classList">Liste de ModelClass.</param>
        /// <param name="nameSpaceName">Namespace.</param>
        private void GenerateReferenceAccessorsImplementation(IEnumerable<ModelClass> classList, string nameSpaceName)
        {
            var projectName = _parameters.DbContextProjectPath.Split('/').Last();
            var implementationName = $"{nameSpaceName.Replace("DataContract", string.Empty)}AccessorsDal";
            var interfaceName = $"I{implementationName}";

            var projectDir = $"{_parameters.OutputDirectory}\\{_parameters.DbContextProjectPath}";

            Console.WriteLine("Generating class " + implementationName + " implementing " + interfaceName);

            var implementationFileName = Path.Combine(projectDir, "generated\\Reference", implementationName + ".cs");
            using (var w = new CSharpWriter(implementationFileName))
            {
                if (_parameters.Kinetix == "Core")
                {
                    w.WriteUsings(
                        "System.Collections.Generic",
                        "System.Linq",
                        $"{_rootNamespace}.{nameSpaceName}",
                        "Kinetix.Services.Annotations");
                }
                else
                {
                    w.WriteUsings(
                        "System.Collections.Generic",
                        "System.Linq",
                        "System.ServiceModel",
                        $"{_rootNamespace}.{nameSpaceName}");
                }

                w.WriteLine();
                w.WriteNamespace(projectName);

                w.WriteSummary(1, "This interface was automatically generated. It contains all the operations to load the reference lists declared in namespace " + nameSpaceName + ".");
                w.WriteLine(1, ServiceAttributes[_parameters.Kinetix].ServiceBehaviorAttribute);
                w.WriteClassDeclaration(implementationName, null, new List<string> { interfaceName });

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

                foreach (ModelClass classe in classList)
                {
                    var serviceName = "Load" + Pluralize(classe.Name);
                    w.WriteLine();
                    w.WriteLine(2, "/// <inheritdoc cref=\"" + interfaceName + "." + serviceName + "\" />");
                    w.WriteLine(2, "public ICollection<" + classe.Name + "> Load" + Pluralize(classe.Name) + "()\r\n{");
                    w.WriteLine(3, LoadReferenceAccessorBody(classe.Name, classe.DefaultOrderModelProperty));
                    w.WriteLine(2, "}");
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
            var projectName = _parameters.DbContextProjectPath.Split('/').Last();
            var interfaceName = $"I{nameSpaceName.Replace("DataContract", string.Empty)}AccessorsDal";

            Console.WriteLine("Generating interface " + interfaceName + " containing reference accessors for namespace " + nameSpaceName);

            var projectDir = $"{_parameters.OutputDirectory}\\{_parameters.DbContextProjectPath}";
            var interfaceFileName = Path.Combine(projectDir, "generated\\Reference", $"{interfaceName}.cs");

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
                        "Kinetix.ServiceModel");
                }

                w.WriteLine();
                w.WriteNamespace(projectName);
                w.WriteSummary(1, "This interface was automatically generated. It contains all the operations to load the reference lists declared in namespace " + nameSpaceName + ".");
                w.WriteLine(1, ServiceAttributes[_parameters.Kinetix].ServiceContractAttribute);
                w.WriteLine(1, "public partial interface " + interfaceName + "\r\n{");

                var count = 0;
                foreach (var classe in classList)
                {
                    count++;
                    w.WriteSummary(2, "Reference accessor for type " + classe.Name);
                    w.WriteReturns(2, "List of " + classe.Name);
                    w.WriteLine(2, ReferenceAccessorAttribute);
                    if (_parameters.Kinetix == "Framework")
                    {
                        w.WriteLine(2, ReferenceAccessorAttributeExt);
                    }
                    w.WriteLine(2, "ICollection<" + classe.Name + "> Load" + Pluralize(classe.Name) + "();");

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
        /// <param name="className">Nom du type chargé par le ReferenceAccessor.</param>
        /// <param name="defaultProperty">Propriété par defaut de la classe.</param>
        /// <returns>Code généré.</returns>
        private string LoadReferenceAccessorBody(string className, ModelProperty defaultProperty)
        {
            string queryParameter = string.Empty;

            if (defaultProperty != null)
            {
                queryParameter = $".OrderBy(row => row.{defaultProperty.Name})";
            }

            return $"return _dbContext.{Pluralize(className)}{queryParameter}.ToList();";
        }
    }
}
