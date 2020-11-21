﻿using System;
using System.Collections.Generic;
using System.IO;
using Kinetix.Tools.Model.FileModel;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Kinetix.Tools.Model.Loaders
{
    public class ModelFileLoader
    {
        private readonly ClassLoader _classLoader;
        private readonly ModelConfig _config;
        private readonly FileChecker _fileChecker;

        public ModelFileLoader(ModelConfig config, ClassLoader classLoader, FileChecker fileChecker)
        {
            _classLoader = classLoader;
            _config = config;
            _fileChecker = fileChecker;
        }

        public ModelFile LoadModelFile(string filePath)
        {
            _fileChecker.CheckModelFile(filePath);

            var parser = new Parser(new StringReader(File.ReadAllText(filePath)));
            parser.Consume<StreamStart>();

            var file = _fileChecker.Deserialize<ModelFile>(parser);
            file.Path = filePath.ToRelative();
            file.Name = Path.GetRelativePath(Path.Combine(Directory.GetCurrentDirectory(), _config.ModelRoot), filePath)
                .Replace(".yml", string.Empty)
                .Replace("\\", "/");
            file.Domains = new List<Domain>();
            file.Classes = new List<Class>();

            while (parser.TryConsume<DocumentStart>(out var _))
            {
                parser.Consume<MappingStart>();
                var scalar = parser.Consume<Scalar>();

                if (scalar.Value == "domain")
                {
                    file.Domains.Add(_fileChecker.Deserialize<Domain>(parser));
                }
                else if (scalar.Value == "class")
                {
                    file.Classes.Add(_classLoader.LoadClass(parser, file.Relationships, filePath));
                }
                else
                {
                    throw new Exception("Type de document inconnu.");
                }

                parser.Consume<MappingEnd>();
                parser.Consume<DocumentEnd>();
            }

            var ns = new Namespace { App = _config.App, Module = file.Module };
            foreach (var classe in file.Classes)
            {
                classe.ModelFile = file;
                classe.Namespace = ns;
            }

            return file;
        }
    }
}
