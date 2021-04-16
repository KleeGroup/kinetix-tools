﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kinetix.Tools.Model.FileModel;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace Kinetix.Tools.Model.Generator.CSharp
{
    public class CSharpApiClientGenerator : GeneratorBase
    {
        private readonly CSharpConfig _config;
        private readonly ILogger<CSharpApiClientGenerator> _logger;

        public CSharpApiClientGenerator(ILogger<CSharpApiClientGenerator> logger, CSharpConfig config)
            : base(logger, config)
        {
            _config = config;
            _logger = logger;
        }

        public override string Name => "CSharpApiClientGen";

        protected override void HandleFiles(IEnumerable<ModelFile> files)
        {
            foreach (var file in files)
            {
                HandleFile(file);
            }
        }

        private void HandleFile(ModelFile file)
        {
            if (!file.Endpoints.Any())
            {
                return;
            }

            var fileSplit = file.Name.Split("/");
            var path = $"/{string.Join("/", fileSplit.Skip(fileSplit.Length > 1 ? 1 : 0).SkipLast(1))}";
            var className = $"{fileSplit.Last()}Client";
            var apiPath = _config.ApiPath.Replace("{app}", file.Endpoints.First().Namespace.App).Replace("{module}", file.Module);
            var filePath = $"{_config.OutputDirectory}/{apiPath}{path}/generated/{className}.cs";

            using var fw = new CSharpWriter(filePath, _logger);

            var hasBody = file.Endpoints.Any(e => e.GetBodyParam() != null);
            var hasJson = file.Endpoints.Any(e => e.Returns != null) || hasBody;

            var usings = new List<string> { "System.Net.Http", "System.Threading.Tasks" };

            if (hasBody)
            {
                usings.Add("System.Text");
            }

            if (hasJson)
            {
                usings.Add("System.Text.Json");
            }

            if (file.Endpoints.Any(e => e.GetQueryParams().Any()))
            {
                usings.AddRange(new[] { "System.Collections.Generic", "System.Linq" });

                if (file.Endpoints.Any(e => e.GetQueryParams().Any(qp => GetPropertyTypeName(qp) != "string")))
                {
                    usings.Add("System.Globalization");
                }
            }

            foreach (var property in file.Endpoints.SelectMany(e => e.Params.Concat(new[] { e.Returns }).Where(p => p != null)))
            {
                if (property is IFieldProperty fp)
                {
                    foreach (var @using in fp.Domain.CSharp!.Usings)
                    {
                        usings.Add(@using);
                    }
                }

                switch (property)
                {
                    case AssociationProperty ap when !ap.AsAlias:
                        usings.Add(_config.GetNamespace(ap.Association));
                        break;
                    case AliasProperty { Property: AssociationProperty ap2 }:
                        usings.Add(_config.GetNamespace(ap2.Association));
                        break;
                    case AliasProperty { PrimaryKey: false, Property: RegularProperty { PrimaryKey: true } rp }:
                        usings.Add(_config.GetNamespace(rp.Class));
                        break;
                    case CompositionProperty cp:
                        usings.Add(_config.GetNamespace(cp.Composition));

                        if (cp.DomainKind != null)
                        {
                            usings.AddRange(cp.DomainKind.CSharp!.Usings);
                        }

                        if (cp.Kind == "list")
                        {
                            usings.Add("System.Collections.Generic");
                        }

                        break;
                }
            }

            fw.WriteUsings(usings.Distinct().ToArray());
            fw.WriteLine();

            fw.WriteNamespace(apiPath.Replace("/", "."));

            fw.WriteSummary(1, $"Client {file.Module}");
            fw.WriteClassDeclaration(className, null);

            fw.WriteLine(2, "private readonly HttpClient _client;");
            if (hasJson)
            {
                fw.WriteLine(2, "private readonly JsonSerializerOptions _jsOptions = new() { IgnoreNullValues = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };");
            }

            fw.WriteLine();
            fw.WriteSummary(2, "Constructeur");
            fw.WriteParam("client", "HttpClient injecté.");
            fw.WriteLine(2, $"public {className}(HttpClient client)");
            fw.WriteLine(2, "{");
            fw.WriteLine(3, "_client = client;");
            fw.WriteLine(2, "}");

            foreach (var endpoint in file.Endpoints)
            {
                fw.WriteLine();
                fw.WriteSummary(2, endpoint.Description);

                foreach (var param in endpoint.Params)
                {
                    fw.WriteParam(param.GetParamName(), param.Comment);
                }

                fw.WriteReturns(2, endpoint.Returns?.Comment ?? "Task.");

                fw.Write("        public async Task");

                if (endpoint.Returns != null)
                {
                    fw.Write($"<{GetPropertyTypeName(endpoint.Returns)}>");
                }

                fw.Write($" {endpoint.Name}(");

                foreach (var param in endpoint.Params)
                {
                    fw.Write($"{GetPropertyTypeName(param, param.IsRouteParam())} {param.GetParamName()}");

                    if (param.IsQueryParam())
                    {
                        fw.Write(" = null");
                    }

                    if (endpoint.Params.Last() != param)
                    {
                        fw.Write(", ");
                    }
                }

                fw.WriteLine(")");
                fw.WriteLine(2, "{");

                var bodyParam = endpoint.GetBodyParam();

                fw.WriteLine(3, $"await EnsureAuthentication();");

                if (endpoint.GetQueryParams().Any())
                {
                    fw.WriteLine(3, "var query = await new FormUrlEncodedContent(new Dictionary<string, string>");
                    fw.WriteLine(3, "{");

                    foreach (var queryParam in endpoint.GetQueryParams())
                    {
                        var toString = GetPropertyTypeName(queryParam) switch
                        {
                            "string" => string.Empty,
                            _ => $"?.ToString(CultureInfo.InvariantCulture)"
                        };

                        fw.WriteLine(4, $@"[""{queryParam.GetParamName()}""] = {queryParam.GetParamName()}{toString},");
                    }

                    fw.WriteLine(3, "}.Where(kv => kv.Value != null)).ReadAsStringAsync();");
                }

                fw.WriteLine(3, $"var res = await _client.{endpoint.Method.ToLower().ToFirstUpper()}Async($\"{endpoint.Route}{(endpoint.GetQueryParams().Any() ? "?{query}" : string.Empty)}\"{(bodyParam != null ? $", GetBody({bodyParam.Name})" : endpoint.Method == "POST" || endpoint.Method == "PUT" ? ", new StringContent(string.Empty)" : string.Empty)});");
                fw.WriteLine(3, $"await EnsureSuccess(res);");

                if (endpoint.Returns != null)
                {
                    fw.WriteLine(3, $"return await Deserialize<{GetPropertyTypeName(endpoint.Returns)}>(res);");
                }

                fw.WriteLine(2, "}");
            }

            if (file.Endpoints.Any(e => e.Returns != null))
            {
                fw.WriteLine();
                fw.WriteSummary(2, "Déserialize le contenu d'une réponse HTTP.");
                fw.WriteTypeParam("T", "Type de destination.");
                fw.WriteParam("response", "Réponse HTTP");
                fw.WriteReturns(2, "Contenu.");
                fw.WriteLine(2, "private async Task<T> Deserialize<T>(HttpResponseMessage response)");
                fw.WriteLine(2, "{");
                fw.WriteLine(3, "var res = await response.Content.ReadAsStringAsync();");
                fw.WriteLine(3, "return JsonSerializer.Deserialize<T>(res == string.Empty ? \"{}\" : res, _jsOptions);");
                fw.WriteLine(2, "}");
            }

            fw.WriteLine();
            fw.WriteSummary(2, "Assure que l'authentification est configurée.");
            fw.WriteLine(2, "private partial Task EnsureAuthentication();");

            fw.WriteLine();
            fw.WriteSummary(2, "Gère les erreurs éventuelles retournées par l'API appelée.");
            fw.WriteParam("response", "Réponse HTTP");
            fw.WriteLine(2, "private partial Task EnsureSuccess(HttpResponseMessage response);");

            if (hasBody)
            {
                fw.WriteLine();
                fw.WriteSummary(2, "Récupère le body d'une requête pour l'objet donné.");
                fw.WriteTypeParam("T", "Type source.");
                fw.WriteParam("input", "Entrée");
                fw.WriteReturns(2, "Contenu.");
                fw.WriteLine(2, "private StringContent GetBody<T>(T input)");
                fw.WriteLine(2, "{");
                fw.WriteLine(3, "return new StringContent(JsonSerializer.Serialize(input, _jsOptions), Encoding.UTF8, \"application/json\");");
                fw.WriteLine(2, "}");
            }

            fw.WriteLine(1, "}");
            fw.WriteLine("}");
        }

        private string GetPropertyTypeName(IProperty prop, bool nonNullable = false)
        {
            var type = prop switch
            {
                IFieldProperty fp => fp.Domain.CSharp?.Type ?? string.Empty,
                CompositionProperty cp => cp.Kind switch
                {
                    "object" => cp.Composition.Name,
                    "list" => $"IEnumerable<{cp.Composition.Name}>",
                    string _ => $"{cp.DomainKind!.CSharp!.Type}<{cp.Composition.Name}>"
                },
                _ => string.Empty
            };

            return nonNullable && type.EndsWith("?") ? type[0..^1] : type;
        }
    }
}
