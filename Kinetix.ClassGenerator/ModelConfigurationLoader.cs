using System;
using System.Collections.Generic;
using System.IO;
using Kinetix.Tools.Common.Parameters;
using Newtonsoft.Json;

namespace Kinetix.ClassGenerator
{
    /// <summary>
    /// Chargeur du fichier de configuration du modèle.
    /// </summary>
    public class ModelConfigurationLoader
    {
        /// <summary>
        /// Charge la configuration du fichier JSON.
        /// </summary>
        /// <param name="jsonPath">Chemin du fichier JSON de configuration.</param>
        public GeneratorParameters LoadModelConfiguration(string jsonPath)
        {
            var configText = File.ReadAllText(jsonPath);
            var parameters = JsonConvert.DeserializeObject<GeneratorParameters>(configText);

            if (parameters.RootNamespace == null)
            {
                throw new ArgumentNullException(nameof(GeneratorParameters.RootNamespace));
            }

            if (parameters.ModelFiles == null)
            {
                throw new ArgumentNullException(nameof(GeneratorParameters.ModelFiles));
            }

            parameters.ExtModelFiles = parameters.ExtModelFiles ?? new List<string>();
            parameters.Pause = parameters.Pause ?? true;
            parameters.VortexFile = parameters.VortexFile ?? "Kinetix.ClassGenerator.log";

            var cSharp = parameters.CSharp;
            if (cSharp != null)
            {
                if (cSharp.OutputDirectory == null)
                {
                    throw new ArgumentNullException(nameof(cSharp.OutputDirectory));
                }

                cSharp.UseTypeSafeConstValues = cSharp.UseTypeSafeConstValues ?? false;
                cSharp.NoColumnOnAlias = cSharp.NoColumnOnAlias ?? false;
                cSharp.Kinetix = cSharp.Kinetix ?? "Core";
            }

            var pSql = parameters.ProceduralSql;
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

            return parameters;
        }
    }
}
