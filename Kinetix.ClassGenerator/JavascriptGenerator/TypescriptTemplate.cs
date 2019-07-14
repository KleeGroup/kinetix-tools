using System;
using System.Collections.Generic;
using System.Linq;
using Kinetix.Tools.Common;
using Kinetix.Tools.Common.Model;

namespace Kinetix.ClassGenerator.JavascriptGenerator
{
    /// <summary>
    /// Class to produce the template output
    /// </summary>
    public partial class TypescriptTemplate : TemplateBase
    {
        /// <summary>
        /// Objet de modèle.
        /// </summary>
        public ModelClass Model { get; set; }

        /// <summary>
        /// Namespace de base de l'application.
        /// </summary>
        public string RootNamespace { get; set; }

        /// <summary>
        /// Create the template output
        /// </summary>
        public virtual string TransformText()
        {
            Write("/*\r\n    Ce fichier a été généré automatiquement.\r\n    Toute modification sera per" +
                    "due.\r\n*/\r\n\r\n");

            Write("import {EntityToType, StoreNode} from \"focus4/entity\";");
            Write("\r\nimport {");
            Write(string.Join(", ", GetDomainList()));
            Write("} from \"../../domains\";\r\n");

            var imports = GetImportList();
            foreach (var import in imports)
            {
                Write("\r\nimport {");
                Write(import.import);
                Write("} from \"");
                Write(import.path);
                Write("\";");
            }
            if (imports.Any())
            {
                Write("\r\n");
            }

            var properties = Model.PropertyList.Where(p => !p.IsParentId);

            Write("\r\nexport type ");
            Write(Model.Name);
            Write(" = EntityToType<typeof ");
            Write(Model.Name);
            Write("Entity>;\r\nexport type ");
            Write(Model.Name);
            Write("Node = StoreNode<typeof ");
            Write(Model.Name);
            Write("Entity>;\r\n\r\n");

            Write("export const ");
            Write(Model.Name);
            Write("Entity = {\r\n    name: \"");
            Write(Model.Name.ToFirstLower());
            Write("\",\r\n    fields: {\r\n");

            if (Model.ParentClass != null)
            {
                Write("        ...");
                Write(Model.ParentClass.Name);
                Write("Entity.fields,\r\n");
            }

            foreach (var property in properties)
            {
                Write("        ");
                Write(property.Name.ToFirstLower());
                Write(": {\r\n");
                Write("            type: ");
                if (IsArray(property))
                {
                    Write("\"list\" as \"list\"");
                }
                else if (property.IsFromComposition)
                {
                    Write("\"object\" as \"object\"");
                }
                else
                {
                    Write("\"field\" as \"field\"");
                }

                Write(",\r\n");

                if (GetDomain(property) != null)
                {
                    Write("            name: \"");
                    Write(property.Name.ToFirstLower());
                    Write("\",\r\n            fieldType: {} as ");
                    Write(TSUtils.CSharpToTSType(property));
                    Write(",\r\n");
                    Write("            domain: ");
                    Write(GetDomain(property));
                    Write(",\r\n            isRequired: ");
                    Write((property.DataMember.IsRequired && (!property.IsPrimaryKey || property.DataType != "int?")).ToString().ToFirstLower());
                    Write(",\r\n            label: \"");
                    Write(TSUtils.ToNamespace(Model.Namespace.Name));
                    Write(".");
                    Write(Model.Name.ToFirstLower());
                    Write(".");
                    Write(property.Name.ToFirstLower());
                    Write("\"\r\n");
                }
                else
                {
                    Write("            entity: ");
                    Write(GetReferencedType(property));
                    Write("Entity");
                    Write("\r\n");
                }

                Write("        }");

                if (property != properties.Last())
                {
                    Write(",");
                }

                Write("\r\n");
            }

            Write("    }\r\n};\r\n");

            if (Model.IsReference)
            {
                Write("\r\nexport const ");
                Write(Model.Name.ToFirstLower());
                Write(" = {type: {} as ");
                Write(Model.Name);
                Write(", valueKey: \"");
                Write(Model.PrimaryKey.First().Name.ToFirstLower());
                Write("\", labelKey: \"");
                Write(Model.DefaultProperty?.ToFirstLower() ?? "libelle");
                Write("\"};\r\n");
            }

            return GenerationEnvironment.ToString();
        }

        private string GetDomain(ModelProperty property)
        {
            return property?.DataDescription?.Domain?.Code;
        }

        private IEnumerable<string> GetDomainList()
        {
            return Model.PropertyList
                .Select(property => property?.DataDescription?.Domain?.Code)
                .Where(domain => domain != null)
                .Distinct()
                .OrderBy(x => x);
        }

        /// <summary>
        /// Récupère la liste d'imports de types pour les services.
        /// </summary>
        /// <returns>La liste d'imports (type, chemin du module, nom du fichier).</returns>
        private IEnumerable<(string import, string path)> GetImportList()
        {
            var types = Model.PropertyList
                .Where(property =>
                    (property.DataDescription?.ReferenceClass?.FullyQualifiedName.StartsWith(RootNamespace, StringComparison.Ordinal) ?? false)
                 && property.DataType != "string" && property.DataType != "int?")
                .Select(property => property.DataDescription?.ReferenceClass?.FullyQualifiedName);

            string parentClassName = null;
            if (Model.ParentClass != null)
            {
                parentClassName = Model.ParentClass.FullyQualifiedName;
                types = types.Concat(new[] { parentClassName });
            }

            var currentModule = GetModuleName(Model.FullyQualifiedName);

            var imports = types.Select(type =>
            {
                var module = GetModuleName(type);
                var name = type.Split('.').Last();

                module = module == currentModule
                    ? $"."
                    : $"../{module}";

                return (
                    import: $"{name}Entity",
                    path: $"{module}/{name.ToDashCase()}");
            }).Distinct().ToList();

            var references = Model.PropertyList
                .Where(property => property.DataDescription?.ReferenceClass != null && property.DataType == "string" && property.DataDescription.ReferenceClass.IsReference)
                .Select(property => (Code: $"{property.DataDescription.ReferenceClass.Name}Code", property.DataDescription.ReferenceClass.FullyQualifiedName))
                .Distinct();

            if (references.Any())
            {
                var referenceTypeMap = references.GroupBy(t => GetModuleName(t.FullyQualifiedName));
                foreach (var refModule in referenceTypeMap)
                {
                    var module = refModule.Key == currentModule
                    ? $"."
                    : $"../{refModule.Key}";

                    imports.Add((string.Join(", ", refModule.Select(r => r.Code).OrderBy(x => x)), $"{module}/references"));
                }
            }

            return imports.OrderBy(i => i.path);
        }

        /// <summary>
        /// Récupère le nom du module à partir du nom complet.
        /// </summary>
        /// <param name="fullyQualifiedName">Nom complet.</param>
        /// <returns>Le nom du module.</returns>
        private string GetModuleName(string fullyQualifiedName)
        {
            return fullyQualifiedName.Split('.')[1]
                .Replace("DataContract", string.Empty)
                .Replace("Contract", string.Empty)
                .ToLower();
        }

        private string GetReferencedType(ModelProperty property)
        {
            return GetDomain(property) != null
                ? null
                : property?.DataDescription?.ReferenceClass?.Name;
        }

        private bool IsArray(ModelProperty property)
        {
            return property.IsCollection && property.DataDescription?.Domain?.DataType == null;
        }
    }
}