﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kinetix.Tools.Model.FileModel;
using Microsoft.Extensions.Logging;

namespace Kinetix.Tools.Model.Generator.Javascript
{
    /// <summary>
    /// Générateur des objets de traduction javascripts.
    /// </summary>
    public class JavascriptApiClientGenerator : GeneratorBase
    {
        private readonly JavascriptConfig _config;
        private readonly ILogger<JavascriptApiClientGenerator> _logger;

        public JavascriptApiClientGenerator(ILogger<JavascriptApiClientGenerator> logger, JavascriptConfig config)
            : base(logger, config)
        {
            _config = config;
            _logger = logger;
        }

        public override string Name => "JSApiClientGen";

        protected override void HandleFiles(IEnumerable<ModelFile> files)
        {
            foreach (var file in files)
            {
                GenerateClientFile(file);
            }
        }

        private void GenerateClientFile(ModelFile file)
        {
            if (!file.Endpoints.Any())
            {
                return;
            }

            var fileSplit = file.Name.Split("/");
            var filePath = (fileSplit.Length > 1 ? string.Join("/", fileSplit[1..]) : file.Name)
                .ToDashCase();

            var fileName = $"{_config.ApiClientOutputDirectory}/{filePath}.ts";
            using var fw = new FileWriter(fileName, _logger, false);

            var fetch = _config.FetchImportPath != null ? "fetch" : "coreFetch";

            fw.WriteLine($@"import {{{fetch}}} from ""{_config.FetchImportPath ?? "@focus4/core"}"";");

            var imports = GetImports(file);
            if (imports.Any())
            {
                fw.WriteLine();

                foreach (var (import, path) in imports)
                {
                    fw.WriteLine($@"import {{{import}}} from ""{path}"";");
                }
            }

            foreach (var endpoint in file.Endpoints)
            {
                fw.WriteLine();
                fw.WriteLine("/**");
                fw.WriteLine($" * {endpoint.Description}");

                foreach (var param in endpoint.Params)
                {
                    fw.WriteLine($" * @param {param.GetParamName()} {param.Comment}");
                }

                fw.WriteLine(" * @param options Options pour 'fetch'.");

                if (endpoint.Returns != null)
                {
                    fw.WriteLine($" * @returns {endpoint.Returns.Comment}");
                }

                fw.WriteLine(" */");
                fw.Write($"export function {endpoint.Name.ToFirstLower()}(");

                foreach (var param in endpoint.Params)
                {
                    fw.Write($"{param.GetParamName()}{(param.IsQueryParam() ? "?" : string.Empty)}");

                    if (param is IFieldProperty fp)
                    {
                        fw.Write($": {fp.TS.Type}");
                    }
                    else if (param is CompositionProperty cp)
                    {
                        fw.Write(": ");
                        WriteCompositionType(fw, cp);
                    }

                    fw.Write(", ");
                }

                fw.Write("options: RequestInit = {}): Promise<");
                if (endpoint.Returns == null)
                {
                    fw.Write("void");
                }
                else if (endpoint.Returns is IFieldProperty fp)
                {
                    fw.Write(fp.TS.Type);
                }
                else if (endpoint.Returns is CompositionProperty cp)
                {
                    WriteCompositionType(fw, cp);
                }

                fw.WriteLine("> {");

                /* TODO FormData ? */

                fw.Write($@"    return {fetch}(""{endpoint.Method}"", `./{endpoint.Route.Replace("{", "${")}`, {{");

                if (endpoint.GetBodyParam() != null)
                {
                    fw.Write($"body: {endpoint.GetBodyParam()!.GetParamName()}");
                }

                if (endpoint.GetBodyParam() != null && endpoint.GetQueryParams().Any())
                {
                    fw.Write(", ");
                }

                if (endpoint.GetQueryParams().Any())
                {
                    fw.Write("query: {");

                    foreach (var qParam in endpoint.GetQueryParams())
                    {
                        fw.Write(qParam.GetParamName());

                        if (qParam != endpoint.GetQueryParams().Last())
                        {
                            fw.Write(", ");
                        }
                    }

                    fw.Write("}");
                }

                fw.WriteLine("}, options);");
                fw.WriteLine("}");
            }
        }

        private void WriteCompositionType(FileWriter fw, CompositionProperty cp)
        {
            if (cp.DomainKind != null)
            {
                fw.Write($"{cp.DomainKind.TS!.Type}<");
            }

            fw.Write(cp.Composition.Name);
            if (cp.Kind == "list" || cp.Kind == "async-list")
            {
                fw.Write("[]");
            }

            if (cp.DomainKind != null)
            {
                fw.Write($">");
            }
        }

        private IList<(string Import, string Path)> GetImports(ModelFile file)
        {
            var properties = file.Endpoints.SelectMany(endpoint => endpoint.Params.Concat(new[] { endpoint.Returns }));

            var types = properties.OfType<CompositionProperty>().Select(property => property.Composition);

            var modelPath = Path.GetRelativePath(_config.ApiClientOutputDirectory!, _config.ModelOutputDirectory!).Replace("\\", "/");

            var imports = types.Select(type =>
            {
                var name = type.Name;
                var module = $"{modelPath}/{type.Namespace.Module.ToLower()}";
                return (Import: name, Path: $"{module}/{name.ToDashCase()}");
            }).Distinct().ToList();

            var references = file.Endpoints.SelectMany(p => p.Params.Concat(new[] { p.Returns }).Where(p => p != null))
                .Select(p => p is AliasProperty alp ? alp.Property : p)
                .OfType<IFieldProperty>()
                .Select(prop => (prop, classe: prop is AssociationProperty ap ? ap.Association : prop.Class))
                .Where(pc => pc.prop.TS.Type != pc.prop.Domain.TS!.Type && pc.prop.Domain.TS.Type == "string" && pc.classe.Reference)
                .Select(pc => (Code: pc.prop.TS.Type, pc.classe.Namespace.Module))
                .Distinct();

            if (references.Any())
            {
                var referenceTypeMap = references.GroupBy(t => t.Module);
                foreach (var refModule in referenceTypeMap)
                {
                    var module = $"{modelPath}/{refModule.Key.ToLower()}";

                    imports.Add((string.Join(", ", refModule.Select(r => r.Code).OrderBy(x => x)), $"{module}/references"));
                }
            }

            imports.AddRange(
                properties.OfType<IFieldProperty>()
                    .Where(p => p.Domain.TS?.Import != null)
                    .Select(p => (p.Domain.TS!.Type, p.Domain.TS.Import!))
                    .Distinct());

            imports.AddRange(
                properties.OfType<CompositionProperty>()
                    .Where(p => p.DomainKind != null)
                    .Select(p => (p.DomainKind!.TS!.Type, p.DomainKind.TS.Import!))
                    .Distinct());

            return imports
                .GroupBy(i => i.Path)
                .Select(i => (import: string.Join(", ", i.Select(l => l.Import)), path: i.Key))
                .OrderBy(i => i.path.StartsWith(".") ? i.path : $"...{i.path}")
                .ToList();
        }
    }
}
