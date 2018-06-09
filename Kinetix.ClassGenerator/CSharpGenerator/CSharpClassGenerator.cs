using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kinetix.Tools.Common.Model;
using Kinetix.Tools.Common.Parameters;

namespace Kinetix.ClassGenerator.CSharpGenerator
{
    using static CSharpUtils;

    public class CSharpClassGenerator
    {
        private readonly string _rootNamespace;
        private readonly CSharpParameters _parameters;

        public CSharpClassGenerator(string rootNamespace, CSharpParameters parameters)
        {
            _rootNamespace = rootNamespace;
            _parameters = parameters;
        }

        /// <summary>
        /// Méthode générant le code d'une classe.
        /// </summary>
        /// <param name="item">Classe concernée.</param>
        /// <param name="ns">Namespace.</param>
        public void Generate(ModelClass item, ModelNamespace ns)
        {
            var fileName = Path.Combine(GetDirectoryForModelClass(_parameters.OutputDirectory, item.DataContract.IsPersistent, _rootNamespace, item.Namespace.Name), item.Name + ".cs");
            using (var w = new CSharpWriter(fileName))
            {
                Console.WriteLine("Generating class " + ns.Name + "." + item.Name);

                GenerateUsings(w, item);
                w.WriteLine();
                w.WriteNamespace($"{_rootNamespace}.{ns.Name}");
                w.WriteSummary(1, item.Comment);
                GenerateClassDeclaration(w, item);
                w.WriteLine("}");
            }
        }

        /// <summary>
        /// Génère le constructeur par recopie d'un type base.
        /// </summary>
        /// <param name="w">Writer.</param>
        /// <param name="item">Classe générée.</param>
        private void GenerateBaseCopyConstructor(CSharpWriter w, ModelClass item)
        {
            w.WriteLine();
            w.WriteSummary(2, "Constructeur par base class.");
            w.WriteParam("bean", "Source.");
            w.WriteLine(2, "public " + item.Name + "(" + item.ParentClass.Name + " bean)");
            w.WriteLine(3, ": base(bean)");
            w.WriteLine(2, "{");
            w.WriteLine(3, "OnCreated();");
            w.WriteLine(2, "}");
        }

        /// <summary>
        /// Génération de la déclaration de la classe.
        /// </summary>
        /// <param name="w">Writer</param>
        /// <param name="item">Classe à générer.</param>
        private void GenerateClassDeclaration(CSharpWriter w, ModelClass item)
        {
            if (item.Stereotype == Stereotype.Reference)
            {
                w.WriteAttribute(1, "Reference");
            }
            else if (item.Stereotype == Stereotype.Statique)
            {
                w.WriteAttribute(1, "Reference", "true");
            }

            if (!string.IsNullOrEmpty(item.DefaultProperty))
            {
                w.WriteAttribute(1, "DefaultProperty", $@"""{item.DefaultProperty}""");
            }

            if (item.DataContract.IsPersistent && !item.IsView)
            {
                if (_parameters.DbSchema != null)
                {
                    w.WriteAttribute(1, "Table", $@"""{item.DataContract.Name}""", $@"Schema = ""{_parameters.DbSchema}""");
                }
                else
                {
                    w.WriteAttribute(1, "Table", $@"""{item.DataContract.Name}""");
                }
            }

            w.WriteClassDeclaration(item.Name, item.ParentClass?.Name, new List<string>());

            GenerateConstProperties(w, item);
            GenerateConstructors(w, item);
            GenerateProperties(w, item);
            GenerateExtensibilityMethods(w, item);
            w.WriteLine(1, "}");

            if (_parameters.UseTypeSafeConstValues.Value)
            {
                GenerateConstPropertiesClass(w, item);
            }
        }

        /// <summary>
        /// Génération des constantes statiques.
        /// </summary>
        /// <param name="w">Writer.</param>
        /// <param name="item">La classe générée.</param>
        private void GenerateConstProperties(CSharpWriter w, ModelClass item)
        {
            int nbConstValues = item.ConstValues.Count;
            if (nbConstValues != 0)
            {
                int i = 0;
                foreach (string constFieldName in item.ConstValues.Keys.OrderBy(x => x, StringComparer.Ordinal))
                {
                    ++i;
                    var valueLibelle = item.ConstValues[constFieldName];
                    ModelProperty property = null;
                    if (item.Stereotype == Stereotype.Reference)
                    {
                        foreach (var prop in item.PropertyList)
                        {
                            if (prop.IsUnique)
                            {
                                property = prop;
                                break;
                            }
                        }
                    }
                    else
                    {
                        property = ((IList<ModelProperty>)item.PrimaryKey)[0];
                    }

                    w.WriteSummary(2, valueLibelle.Libelle);

                    if (_parameters.UseTypeSafeConstValues.Value)
                    {
                        w.WriteLine(2, string.Format("public readonly {2}Code {0} = new {2}Code({1});", constFieldName, valueLibelle.Code, item.Name));
                    }
                    else
                    {
                        w.WriteLine(2, string.Format("public const string {0} = {1};", constFieldName, valueLibelle.Code));
                    }

                    w.WriteLine();
                }
            }
        }

        /// <summary>
        /// Génération des constantes statiques.
        /// </summary>
        /// <param name="w">Writer.</param>
        /// <param name="item">La classe générée.</param>
        private void GenerateConstPropertiesClass(CSharpWriter w, ModelClass item)
        {
            int nbConstValues = item.ConstValues.Count;
            if (nbConstValues != 0)
            {
                w.WriteLine();
                w.WriteLine("#pragma warning disable SA1402");
                w.WriteLine();
                w.WriteSummary(1, $"Type des valeurs pour {item.Name}");
                w.WriteLine(1, $"public sealed class {item.Name}Code : TypeSafeEnum {{");
                w.WriteLine();

                w.WriteLine(2, $"private readonly Dictionary<string, {item.Name}Code> Instance = new Dictionary<string, {item.Name}Code>();");
                w.WriteLine();

                w.WriteSummary(2, "Constructeur");
                w.WriteParam("value", "Valeur");
                w.WriteLine(2, $"public {item.Name}Code(string value)");
                w.WriteLine(3, ": base(value) {");
                w.WriteLine(3, "Instance[value] = this;");
                w.WriteLine(2, "}");
                w.WriteLine();

                w.WriteLine(2, $"public explicit operator {item.Name}Code(string value) {{");
                w.WriteLine(3, $"System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof({item.Name}).TypeHandle);");
                w.WriteLine(3, "if (Instance.TryGetValue(value, out var result)) {");
                w.WriteLine(4, "return result;");
                w.WriteLine(3, "} else {");
                w.WriteLine(4, "throw new InvalidCastException();");
                w.WriteLine(3, "}");
                w.WriteLine(2, "}");
                w.WriteLine(1, "}");
            }
        }

        /// <summary>
        /// Génère les constructeurs.
        /// </summary>
        /// <param name="w">Writer.</param>
        /// <param name="item">La classe générée.</param>
        private void GenerateConstructors(CSharpWriter w, ModelClass item)
        {
            GenerateDefaultConstructor(w, item);
            GenerateCopyConstructor(w, item);
            if (item.ParentClass != null)
            {
                GenerateBaseCopyConstructor(w, item);
            }
        }

        /// <summary>
        /// Génère le constructeur par recopie.
        /// </summary>
        /// <param name="w">Writer.</param>
        /// <param name="item">Classe générée.</param>
        private void GenerateCopyConstructor(CSharpWriter w, ModelClass item)
        {
            w.WriteLine();
            w.WriteSummary(2, "Constructeur par recopie.");
            w.WriteParam("bean", "Source.");
            if (item.ParentClass != null)
            {
                w.WriteLine(2, "public " + item.Name + "(" + item.Name + " bean)");
                w.WriteLine(3, ": base(bean)");
                w.WriteLine(2, "{");
            }
            else
            {
                w.WriteLine(2, "public " + item.Name + "(" + item.Name + " bean)");
                w.WriteLine(2, "{");
            }

            w.WriteLine(3, "if (bean == null)");
            w.WriteLine(3, "{");
            w.WriteLine(4, "throw new ArgumentNullException(nameof(bean));");
            w.WriteLine(3, "}");
            w.WriteLine();

            foreach (var property in item.PropertyList.Where(p => !p.IsPrimitive && !p.IsCollection))
            {
                w.WriteLine(3, property.Name + " = new " + property.DataType + "(bean." + property.Name + ");");
            }

            foreach (var property in item.PropertyList.Where(p => p.IsCollection))
            {
                w.WriteLine(3, property.Name + " = new List<" + LoadInnerDataType(property.DataType) + ">(bean." + property.Name + ");");
            }

            foreach (var property in item.PropertyList.Where(p => p.IsPrimitive))
            {
                w.WriteLine(3, property.Name + " = bean." + property.Name + ";");
            }

            w.WriteLine();
            w.WriteLine(3, "OnCreated(bean);");
            w.WriteLine(2, "}");
        }

        /// <summary>
        /// Génère le constructeur par défaut.
        /// </summary>
        /// <param name="w">Writer.</param>
        /// <param name="item">Classe générée.</param>
        private void GenerateDefaultConstructor(CSharpWriter w, ModelClass item)
        {
            w.WriteSummary(2, "Constructeur.");
            w.WriteLine(2, $@"public {item.Name}()");

            if (item.ParentClass != null)
            {
                w.WriteLine(3, ": base()");
            }

            w.WriteLine(2, "{");

            if (item.NeedsInitialization)
            {
                foreach (var property in item.PropertyList.Where(p => !p.IsPrimitive && !p.IsCollection))
                {
                    w.WriteLine(3, LoadPropertyInit(property.Name, property.DataType));
                }

                foreach (var property in item.PropertyList.Where(p => p.IsCollection))
                {
                    w.WriteLine(3, LoadPropertyInit(property.Name, "List<" + LoadInnerDataType(property.DataType) + ">"));
                }

                w.WriteLine();
            }

            w.WriteLine(3, "OnCreated();");
            w.WriteLine(2, "}");
        }

        /// <summary>
        /// Génère les méthodes d'extensibilité.
        /// </summary>
        /// <param name="w">Writer.</param>
        /// <param name="item">Classe générée.</param>
        private void GenerateExtensibilityMethods(CSharpWriter w, ModelClass item)
        {
            w.WriteLine();
            w.WriteSummary(2, "Methode d'extensibilité possible pour les constructeurs.");
            w.WriteLine(2, "partial void OnCreated();");
            w.WriteLine();
            w.WriteSummary(2, "Methode d'extensibilité possible pour les constructeurs par recopie.");
            w.WriteParam("bean", "Source.");
            w.WriteLine(2, $"partial void OnCreated({item.Name} bean);");
        }

        /// <summary>
        /// Génère les propriétés.
        /// </summary>
        /// <param name="w">Writer.</param>
        /// <param name="item">La classe générée.</param>
        private void GenerateProperties(CSharpWriter w, ModelClass item)
        {
            if (item.PropertyList.Count > 0)
            {
                foreach (var property in item.PersistentPropertyList.Where(prop => !prop.IsReprise))
                {
                    w.WriteLine();
                    GenerateProperty(w, property);
                }

                foreach (var propertyNonPersistent in item.NonPersistentPropertyList.Where(prop => !prop.IsReprise))
                {
                    w.WriteLine();
                    GenerateProperty(w, propertyNonPersistent);
                }
            }
        }

        /// <summary>
        /// Génère la propriété concernée.
        /// </summary>
        /// <param name="w">Writer.</param>
        /// <param name="property">La propriété générée.</param>
        private void GenerateProperty(CSharpWriter w, ModelProperty property)
        {
            w.WriteSummary(2, property.Comment);

            if (!property.Class.IsView && property.IsPersistent && property.DataMember != null)
            {
                if (property.DataDescription.Domain.PersistentDataType.Contains("json"))
                {
                    w.WriteAttribute(2, "Column", $@"""{property.DataMember.Name}""", $@"TypeName = ""{property.DataDescription.Domain.PersistentDataType}""");
                }
                else
                {
                    w.WriteAttribute(2, "Column", $@"""{property.DataMember.Name}""");
                }
            }

            if (property.DataMember.IsRequired && !property.DataDescription.IsPrimaryKey)
            {
                w.WriteAttribute(2, "Required");
            }

            if (property.DataDescription != null)
            {
                if (!string.IsNullOrEmpty(property.DataDescription.ReferenceType) && !property.DataDescription.ReferenceClass.IsExternal)
                {
                    w.WriteAttribute(2, "ReferencedType", $"typeof({property.DataDescription.ReferenceClass.Name})");
                }

                if (property.DataDescription.Domain != null)
                {
                    w.WriteAttribute(2, "Domain", $@"""{property.DataDescription.Domain.Code}""");

                    if (!string.IsNullOrEmpty(property.DataDescription.Domain.CustomAnnotation))
                    {
                        w.WriteLine(2, property.DataDescription.Domain.CustomAnnotation);
                    }
                }
            }

            if (property.DataDescription.IsPrimaryKey)
            {
                w.WriteAttribute(2, "Key");
                if (property.IsIdManuallySet)
                {
                    w.WriteAttribute(2, "DatabaseGenerated", "DatabaseGeneratedOption.None");
                }
            }

            if (!property.IsPrimitive)
            {
                w.WriteAttribute(2, "NotMapped");
            }

            w.WriteLine(2, $"public {LoadShortDataType(property.DataType)} {property.Name} {{ get; set; }}");
        }

        /// <summary>
        /// Génération des imports.
        /// </summary>
        /// <param name="w">Writer.</param>
        /// <param name="item">Classe concernée.</param>
        private void GenerateUsings(CSharpWriter w, ModelClass item)
        {
            var usings = new List<string> { "System" };

            if (item.HasCollection || (_parameters.UseTypeSafeConstValues == true && item.ConstValues != null && item.ConstValues.Count > 0))
            {
                usings.Add("System.Collections.Generic");
            }

            if (!string.IsNullOrEmpty(item.DefaultProperty))
            {
                usings.Add("System.ComponentModel");
            }

            if (item.PropertyList.Any(prop => prop.IsPrimaryKey || (prop.DataMember?.IsRequired ?? false)))
            {
                usings.Add("System.ComponentModel.DataAnnotations");
            }

            if (item.PropertyList.Any(prop => prop.IsPersistent && prop.DataMember != null) || item.PropertyList.Any(p => !p.IsPrimitive))
            {
                usings.Add("System.ComponentModel.DataAnnotations.Schema");
            }

            if (item.HasDomainAttribute || item.ParentClass == null)
            {
                if (_parameters.Kinetix == "Core")
                {
                    usings.Add("Kinetix.ComponentModel.Annotations");
                }
                else
                {
                    usings.Add("Kinetix.ComponentModel");
                }
            }

            foreach (string value in item.UsingList)
            {
                usings.Add(value);
            }

            foreach (var property in item.PropertyList)
            {
                if (!string.IsNullOrEmpty(property.DataDescription?.Domain?.CustomUsings))
                {
                    usings.AddRange(property.DataDescription?.Domain?.CustomUsings.Split(',').Select(u => u.Trim()));
                }

                if (!string.IsNullOrEmpty(property.DataDescription.ReferenceClass?.Namespace?.Name))
                {
                    usings.Add($"{_rootNamespace}.{property.DataDescription.ReferenceClass.Namespace.Name}");
                }
            }

            w.WriteUsings(usings
                .Where(u => u != $"{_rootNamespace}.{item.Namespace.Name}")
                .Distinct()
                .ToArray());
        }

        /// <summary>
        /// Retourne le code associé à l'instanciation d'une propriété.
        /// </summary>
        /// <param name="fieldName">Nom de la variable membre privée.</param>
        /// <param name="dataType">Type de données.</param>
        /// <returns>Code généré.</returns>
        private string LoadPropertyInit(string fieldName, string dataType)
        {
            var res = $"{fieldName} = ";
            if (IsCSharpBaseType(dataType))
            {
                res += GetCSharpDefaultValueBaseType(dataType) + ";";
            }
            else
            {
                res += $"new {dataType}();";
            }

            return res;
        }
    }
}
