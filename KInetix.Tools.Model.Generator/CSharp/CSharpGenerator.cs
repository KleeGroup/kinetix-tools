﻿using System;
using System.IO;
using System.Linq;
using Kinetix.Tools.Model.Config;
using Kinetix.Tools.Model.FileModel;
using Microsoft.Extensions.Logging;

namespace Kinetix.Tools.Model.Generator.CSharp
{
    using static CSharpUtils;

    /// <summary>
    /// Générateur de code C#.
    /// </summary>
    public class CSharpGenerator : IGenerator
    {
        private readonly CSharpConfig _config;
        private readonly ILogger<CSharpGenerator> _logger;
        private readonly ModelStore _modelStore;

        private readonly string _rootNamespace;
        private readonly CSharpClassGenerator _classGenerator;
        private readonly DbContextGenerator _dbContextGenerator;
        private readonly ReferenceAccessorGenerator _referenceAccessorGenerator;

        public CSharpGenerator(ModelStore modelStore, ILogger<CSharpGenerator> logger, CSharpConfig? config = null)
        {
            _config = config!;
            _logger = logger;
            _modelStore = modelStore;

            _rootNamespace = _modelStore.RootNamespace;
            _classGenerator = new CSharpClassGenerator(_rootNamespace, _config);
            _dbContextGenerator = new DbContextGenerator(_rootNamespace, _config, _logger);
            _referenceAccessorGenerator = new ReferenceAccessorGenerator(_rootNamespace, _config, _logger);
        }

        public bool CanGenerate => _config != null;

        public string Name => "du modèle C#";

        public void GenerateAll()
        {
            GenerateDbContext();

            foreach (var file in _modelStore.Files)
            {
                GenerateFromFile(file);
            }

            foreach (var ns in _modelStore.Classes.GroupBy(c => c.Namespace))
            {
                GenerateForReferences(ns);
            }
        }

        private void GenerateDbContext()
        {
            if (_config.DbContextProjectPath != null)
            {
                _dbContextGenerator.Generate(_modelStore.Classes);
            }
        }

        public void GenerateFromFile(ModelFile file)
        {
            if (_config.OutputDirectory == null)
            {
                return;
            }

            _logger.LogInformation($"Génération des classes pour le fichier {file}...");

            var currentDirectory = GetDirectoryForModelClass(
                _config.LegacyProjectPaths,
                _config.OutputDirectory,
                file.Descriptor.Kind == Kind.Data,
                _rootNamespace,
                file.Descriptor.Namespace.CSharpName);

            Directory.CreateDirectory(currentDirectory);
            foreach (var classe in file.Classes)
            {
                _classGenerator.Generate(classe);
            }

            _logger.LogInformation($"{file.Classes.Count()} classes générées.");
        }

        private void GenerateForReferences(IGrouping<Namespace, Class> module)
        {
            _referenceAccessorGenerator.Generate(module);
        }
    }
}
