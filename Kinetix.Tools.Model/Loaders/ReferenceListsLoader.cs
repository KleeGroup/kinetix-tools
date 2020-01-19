﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Kinetix.Tools.Model.Loaders
{
    public static class ReferenceListsLoader
    {
        public static IEnumerable<(string ClassName, IEnumerable<(string Key, IDictionary<string, object> Values)> Items)> LoadReferenceLists(string? referenceListsFile)
        {
            if (referenceListsFile == null)
            {
                return new List<(string, IEnumerable<(string, IDictionary<string, object>)>)>();
            }

            var file = File.ReadAllText(referenceListsFile);
            return JsonConvert.DeserializeObject<IDictionary<string, IDictionary<string, IDictionary<string, object>>>>(file)
                .Select(classe => (
                    classe.Key,
                    classe.Value
                        .Select(item => (item.Key, item.Value))))
                .OrderBy(r => r.Key);
        }

        public static void AddReferenceValues(Class classe, IEnumerable<(string Key, IDictionary<string, object> Values)> values)
        {
            classe.ReferenceValues = values.Select(reference => new ReferenceValue
            {
                Name = reference.Key,
                Value = classe.Properties.OfType<IFieldProperty>().Select(prop =>
                {
                    reference.Values.TryGetValue(prop.Name, out var propValue);
                    if (propValue == null && prop.Required && (!prop.PrimaryKey || prop.Domain.CsharpType == "string"))
                    {
                        throw new Exception($"L'initilisation {reference.Key} de la classe {classe.Name} n'initialise pas la propriété obligatoire {prop.Name}.");
                    }

                    return (prop, propValue);
                }).ToDictionary(v => v.prop, v => v.propValue)
            }).ToList();
        }
    }
}
