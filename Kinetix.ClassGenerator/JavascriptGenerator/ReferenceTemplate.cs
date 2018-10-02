﻿using System;
using System.Collections.Generic;
using System.Linq;
using Kinetix.Tools.Common;
using Kinetix.Tools.Common.Model;

namespace Kinetix.ClassGenerator.JavascriptGenerator
{
    /// <summary>
    /// Class to produce the template output
    /// </summary>
    public partial class ReferenceTemplate : TemplateBase
    {
        /// <summary>
        /// Références.
        /// </summary>
        public IEnumerable<ModelClass> References { get; set; }

        /// <summary>
        /// Create the template output
        /// </summary>
        public string TransformText()
        {
            Write("/*\r\n    Ce fichier a été généré automatiquement.\r\n    Toute modification sera perdue.\r\n*/\r\n");

            foreach (var reference in References)
            {
                Write("\r\nexport type ");
                Write(reference.Name);
                Write("Code = ");
                Write(GetConstValues(reference));
                Write(";\r\nexport interface ");
                Write(reference.Name);
                Write(" {\r\n");

                foreach (var property in reference.PropertyList)
                {
                    Write("    ");
                    Write(property.Name.ToFirstLower());
                    Write(property.DataMember.IsRequired || property.IsPrimaryKey ? string.Empty : "?");
                    Write(": ");
                    Write(CSharpToTSType(property));
                    Write(";\r\n");
                }

                Write("}\r\nexport const ");
                Write(reference.Name.ToFirstLower());
                Write(" = {type: {} as ");
                Write(reference.Name);
                Write(", valueKey: \"");
                Write(reference.PrimaryKey.First().Name.ToFirstLower());
                Write("\", labelKey: \"");
                Write(reference.DefaultProperty?.ToFirstLower() ?? "libelle");
                Write("\"};\r\n");
            }
            return GenerationEnvironment.ToString();
        }

        /// <summary>
        /// Transforme une liste de constantes en type Typescript.
        /// </summary>
        /// <param name="reference">La liste de constantes.</param>
        /// <returns>Le type de sorte.</returns>
        private string GetConstValues(ModelClass reference)
        {
            var constValues = string.Join(" | ", reference.ConstValues.Values.Select(value => value.Code));
            if (constValues == string.Empty)
            {
                return "string";
            }
            else
            {
                return constValues;
            }
        }

        /// <summary>
        /// Transforme le type en type Typescript.
        /// </summary>
        /// <param name="property">La propriété dont on cherche le type.</param>
        /// <returns>Le type en sortie.</returns>
        private string CSharpToTSType(ModelProperty property)
        {
            if (property.Name == "Code")
            {
                return $"{property.Class.Name}Code";
            }
            else if (property.Name.EndsWith("Code", StringComparison.Ordinal))
            {
                return property.Name.ToFirstUpper();
            }

            return TSUtils.CSharpToTSType(property);
        }
    }
}
