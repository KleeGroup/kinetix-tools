﻿using System;
using System.Collections.Generic;
using System.IO;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Kinetix.Tools.Model.Loaders
{
    public class DomainFileLoader
    {
        private readonly FileChecker _fileChecker;

        public DomainFileLoader(FileChecker fileChecker)
        {
            _fileChecker = fileChecker;
        }

        public IEnumerable<Domain> LoadDomains(string domainFilePath)
        {
            _fileChecker.CheckDomainFile(domainFilePath);

            var parser = new Parser(new StringReader(File.ReadAllText(domainFilePath)));
            parser.Consume<StreamStart>();

            while (parser.TryConsume<DocumentStart>(out var _))
            {
                parser.Consume<MappingStart>();
                var scalar = parser.Consume<Scalar>();
                if (scalar.Value != "domain")
                {
                    throw new Exception("Seuls des domaines peuvent être définis dans le fichier de domaines");
                }

                yield return _fileChecker.Deserialize<Domain>(parser);

                parser.Consume<MappingEnd>();
                parser.Consume<DocumentEnd>();
            }
        }
    }
}
