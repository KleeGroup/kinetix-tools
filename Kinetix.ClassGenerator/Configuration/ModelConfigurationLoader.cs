using System;
using System.Collections.Generic;
using System.IO;
using Kinetix.ClassGenerator.Parameters;
using Newtonsoft.Json;

namespace Kinetix.ClassGenerator.Configuration
{
    using static Singletons;

    /// <summary>
    /// Chargeur du fichier de configuration du modèle.
    /// </summary>
    public class ModelConfigurationLoader
    {
        /// <summary>
        /// Charge la configuration du fichier XML.
        /// </summary>
        /// <param name="xmlPath">Chemin du fichier XML de configuration.</param>
        public void LoadModelConfiguration(string xmlPath)
        {
            var configText = File.ReadAllText(xmlPath);
            GeneratorParameters = JsonConvert.DeserializeObject<GeneratorParameters>(configText);

            if (GeneratorParameters.RootNamespace == null)
            {
                throw new ArgumentNullException(nameof(GeneratorParameters.RootNamespace));
            }

            if (GeneratorParameters.ModelFiles == null)
            {
                throw new ArgumentNullException(nameof(GeneratorParameters.ModelFiles));
            }

            GeneratorParameters.ExtModelFiles = GeneratorParameters.ExtModelFiles ?? new List<string>();
            GeneratorParameters.Pause = GeneratorParameters.Pause ?? true;
            GeneratorParameters.VortexFile = GeneratorParameters.VortexFile ?? "Kinetix.ClassGenerator.log";

            var cSharp = GeneratorParameters.CSharp;
            if (cSharp != null)
            {
                if (cSharp.OutputDirectory == null)
                {
                    throw new ArgumentNullException(nameof(cSharp.OutputDirectory));
                }

                if (cSharp.DbContextProjectPath == null)
                {
                    throw new ArgumentNullException(nameof(cSharp.DbContextProjectPath));
                }

                cSharp.UseTypeSafeConstValues = cSharp.UseTypeSafeConstValues ?? false;
            }

            var pSql = GeneratorParameters.ProceduralSql;
            if (pSql != null)
            {
                if (pSql.CrebasFile == null)
                {
                    throw new ArgumentNullException(nameof(pSql.CrebasFile));
                }

                if (pSql.IndexFKFile == null)
                {
                    throw new ArgumentNullException(nameof(pSql.IndexFKFile));
                }
            }
        }
    }
}
