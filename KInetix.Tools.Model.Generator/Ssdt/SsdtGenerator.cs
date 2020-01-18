﻿using System.Linq;
using Kinetix.Tools.Model.Config;
using Microsoft.Extensions.Logging;

namespace Kinetix.Tools.Model.Generator.Ssdt
{
    public class SsdtGenerator : IGenerator
    {
        private readonly SsdtConfig? _config;
        private readonly ILogger<SsdtGenerator> _logger;
        private readonly ModelStore _modelStore;

        public SsdtGenerator(ModelStore modelStore, ILogger<SsdtGenerator> logger, SsdtConfig? config = null)
        {
            _config = config;
            _logger = logger;
            _modelStore = modelStore;
        }

        public bool CanGenerate => _config != null;

        public string Name => "du modèle SSDT";

        public void Generate()
        {
            if (_config == null)
            {
                return;
            }

            if (_config.TableScriptFolder != null && _config.TableTypeScriptFolder != null)
            {
                // Génération pour déploiement SSDT.
                SsdtSchemaGenerator.GenerateSchemaScript(
                    _logger,
                    _modelStore.Classes,
                    _config.TableScriptFolder,
                    _config.TableTypeScriptFolder);
            }

            if (_config.InitStaticListMainScriptName != null && _config.InitStaticListScriptFolder != null)
            {
                SsdtInsertGenerator.GenerateListInitScript(
                    _config,
                    _logger,
                    _modelStore.Classes.Where(c => c.Stereotype == Stereotype.Statique && c.ReferenceValues != null),
                    _config.InitStaticListScriptFolder,
                    _config.InitStaticListMainScriptName,
                    true);
            }

            if ( _config.InitReferenceListMainScriptName != null && _config.InitReferenceListScriptFolder != null)
            {
                SsdtInsertGenerator.GenerateListInitScript(
                    _config,
                    _logger,
                    _modelStore.Classes.Where(c => c.Stereotype == Stereotype.Reference && c.ReferenceValues != null),
                    _config.InitReferenceListScriptFolder,
                    _config.InitReferenceListMainScriptName,
                    false);
            }
        }
    }
}
