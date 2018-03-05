using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Kinetix.ClassGenerator
{
    /// <summary>
    /// Classe utilitaire destinée à la génération de code.
    /// </summary>
    public static class CodeUtils
    {
        private static readonly Regex RegExChar = new Regex("^A[0-9]*$");
        private static readonly Regex RegExDecimal = new Regex("^DC[0-9]*,[0-9]*$");
        private static readonly Regex RegExNumeric = new Regex("^N[0-9]*,[0-9]*$");
        private static readonly Regex RegExVarChar = new Regex("^VA[0-9]*$");
        private static readonly Regex RegNotNullableType = new Regex(@"^((u)?int|(u)?long|(s)?byte|(u)?short|bool|System.DateTime|System.TimeSpan|decimal|System.Guid|NpgsqlTypes.NpgsqlPoint|NpgsqlTypes.NpgsqlPolygon)$");
        private static IDictionary<string, string> regType;

        /// <summary>
        /// Retourne le nom du module métier depuis un namespace.
        /// </summary>
        /// <param name="nameSpace">Le namespace</param>
        /// <returns></returns>
        public static string ExtractModuleMetier(string nameSpace)
        {
            const string DataContractSuffix = "DataContract";
            const string ContractSuffix = "Contract";
            if (nameSpace.EndsWith(DataContractSuffix, StringComparison.InvariantCultureIgnoreCase))
            {
                return nameSpace.Substring(0, nameSpace.Length - DataContractSuffix.Length);
            }

            if (nameSpace.EndsWith(ContractSuffix, StringComparison.InvariantCultureIgnoreCase))
            {
                return nameSpace.Substring(0, nameSpace.Length - ContractSuffix.Length);
            }

            return nameSpace;
        }

        /// <summary>
        /// Donne la valeur par défaut d'un type de base C#.
        /// Renvoie null si le type n'est pas un type par défaut.
        /// </summary>
        /// <param name="name">Nom du type à définir.</param>
        /// <returns>Vrai si le type est un type C#.</returns>
        public static string GetCSharpDefaultValueBaseType(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException("name");
            }

            if (regType == null)
            {
                InitializeRegType();
            }

            regType.TryGetValue(name, out string res);
            return res;
        }

        /// <summary>
        /// Retourne le nom du répertoire dans lequel placer la classe générée à partir du ModelClass fourni.
        /// </summary>
        /// <param name="isPersistent">Trie s'il s'agit du domaine persistant.</param>
        /// <param name="projectName">Nom du projet.</param>
        /// <param name="nameSpace">Namespace de la classe.</param>
        /// <returns>Emplacement dans lequel placer la classe générée à partir du ModelClass fourni.</returns>
        public static string GetDirectoryForModelClass(bool isPersistent, string projectName, string nameSpace)
        {
            var basePath = Singletons.GeneratorParameters.CSharp.OutputDirectory;
            var moduleMetier = ExtractModuleMetier(nameSpace);
            var localPath = Path.Combine(moduleMetier, projectName + "." + nameSpace);
            string path = isPersistent ? Path.Combine(basePath, localPath) : Path.Combine(basePath, localPath, "Dto");
            return Path.Combine(path, "generated");
        }

        /// <summary>
        /// Retourne le nom du répertoire du projet d'une classe.
        /// </summary>
        /// <param name="isPersistent">Trie s'il s'agit du domaine persistant.</param>
        /// <param name="projectName">Nom du projet.</param>
        /// <param name="nameSpace">Namespace de la classe.</param>
        /// <returns>Nom du répertoire contenant le csproj.</returns>
        public static string GetDirectoryForProject(bool isPersistent, string projectName, string nameSpace)
        {
            var moduleMetier = ExtractModuleMetier(nameSpace);
            var localPath = Path.Combine(moduleMetier, projectName + "." + nameSpace);
            return Path.Combine(Singletons.GeneratorParameters.CSharp.OutputDirectory, localPath);
        }

        /// <summary>
        /// Détermine si le type est un type de base C#.
        /// </summary>
        /// <param name="name">Nom du type à définir.</param>
        /// <returns>Vrai si le type est un type C#.</returns>
        public static bool IsCSharpBaseType(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException("name");
            }

            if (regType == null)
            {
                InitializeRegType();
            }

            return regType.ContainsKey(name);
        }

        /// <summary>
        /// Détermine si le type est un type de base C#
        /// non nullable.
        /// </summary>
        /// <param name="name">Nom du type à définir.</param>
        /// <returns>Vrai si le type est un type C#.</returns>
        public static bool IsNonNullableCSharpBaseType(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException("name");
            }

            return RegNotNullableType.Match(name).Success;
        }

        /// <summary>
        /// Retourne le type contenu dans la collection.
        /// </summary>
        /// <param name="dataType">Type de données qualifié.</param>
        /// <returns>Nom du type de données contenu.</returns>
        public static string LoadInnerDataType(string dataType)
        {
            int beginIdx = dataType.LastIndexOf('<');
            int endIdx = dataType.LastIndexOf('>');
            if (beginIdx == -1 || endIdx == -1)
            {
                throw new NotSupportedException();
            }

            return dataType.Substring(beginIdx + 1, (endIdx - 1) - beginIdx);
        }

        /// <summary>
        /// Retourne le nom de la variable membre a générer à partir du nom de la propriété.
        /// </summary>
        /// <param name="propertyName">Nom de la propriété.</param>
        /// <returns>Nom de la variable membre privée.</returns>
        public static string LoadPrivateFieldName(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                throw new ArgumentNullException("propertyName");
            }

            return propertyName.Substring(0, 1).ToLowerInvariant() + propertyName.Substring(1);
        }

        /// <summary>
        /// Retourne le type de l'objet en notation minimaliste (suppression du namespace).
        /// </summary>
        /// <param name="dataType">Type de données.</param>
        /// <returns>Nom court.</returns>
        public static string LoadShortDataType(string dataType)
        {
            int idx = dataType.LastIndexOf('.');
            return idx != -1 ? dataType.Substring(idx + 1) : dataType;
        }

        /// <summary>
        /// Mets au pluriel le nom.
        /// </summary>
        /// <param name="className">Le nom de la classe.</param>
        /// <returns></returns>
        public static string Pluralize(string className)
        {
            return className.EndsWith("s") ? className : className + "s";
        }

        /// <summary>
        /// Retourne le type SQL à partir d'un type persistent PowerDesigner.
        /// </summary>
        /// <param name="persistentDataType">Type persistent PowerDesigner.</param>
        /// <returns>Le type SQL associé.</returns>
        public static string PowerDesignerPersistentDataTypeToSqlDatType(string persistentDataType)
        {
            var isPostgre = Singletons.GeneratorParameters.ProceduralSql?.TargetDBMS?.ToLower() == "postgre";

            if (string.IsNullOrEmpty(persistentDataType))
            {
                throw new ArgumentNullException(nameof(persistentDataType));
            }

            if (persistentDataType == "I")
            {
                return "int";
            }

            if (persistentDataType == "D")
            {
                if (isPostgre)
                {
                    return "date";
                }
                else
                {
                    return "datetime2";
                }
            }

            if (persistentDataType == "DT")
            {
                if (isPostgre)
                {
                    return "timestamp";
                }
                else
                {
                    return "datetime2";
                }
            }

            if (persistentDataType == "BL")
            {
                if (isPostgre)
                {
                    return "bool";
                }
                else
                {
                    return "bit";
                }
            }

            if (persistentDataType == "SI")
            {
                return "smallint";
            }

            if (persistentDataType == "T")
            {
                return "time";
            }

            if (persistentDataType == "TXT")
            {
                return "text";
            }

            if (persistentDataType == "PIC")
            {
                return "image";
            }

            if (persistentDataType == "VBIN")
            {
                return "jsonb";
            }

            if (persistentDataType == "MN")
            {
                return "polygon";
            }

            if (persistentDataType == "LA")
            {
                return "point";
            }

            if (persistentDataType == "VBIN")
            {
                return "jsonb";
            }

            if (RegExNumeric.IsMatch(persistentDataType))
            {
                return "numeric";
            }

            if (RegExVarChar.IsMatch(persistentDataType))
            {
                if (isPostgre)
                {
                    return "varchar";
                }
                else
                {
                    return "nvarchar";
                }
            }

            if (RegExChar.IsMatch(persistentDataType))
            {
                return "nchar";
            }

            if (RegExDecimal.IsMatch(persistentDataType))
            {
                return "decimal";
            }

            return persistentDataType;
        }


        /// <summary>
        /// Supprime les points de la chaîne.
        /// </summary>
        /// <param name="dottedString">Chaîne avec points.</param>
        /// <returns>Chaîne sans points.</returns>
        public static string RemoveDots(string dottedString)
        {
            if (string.IsNullOrEmpty(dottedString))
            {
                return dottedString;
            }

            return dottedString.Replace(".", string.Empty);
        }

        /// <summary>
        /// Initialisation des types.
        /// </summary>
        private static void InitializeRegType()
        {
            regType = new Dictionary<string, string>
            {
                { "int", "0" },
                { "uint", "0" },
                { "float", "0.0f" },
                { "double", "0.0" },
                { "bool", "false" },
                { "short", "0" },
                { "ushort", "0" },
                { "long", "0" },
                { "ulong", "0" },
                { "decimal", "0" },
                { "byte", "0" },
                { "sbyte", "0" },
                { "string", "\"\"" }
            };
        }
    }
}
