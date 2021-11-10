﻿using System.Collections.Generic;
using Kinetix.Tools.Model.FileModel;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Kinetix.Tools.Model.Loaders
{
    public static class PropertyLoader
    {
        public static IEnumerable<IProperty> LoadProperty(Parser parser, IDictionary<object, Relation> relationships)
        {
            parser.Consume<MappingStart>();
            switch (parser.Current)
            {
                case Scalar { Value: "name" }:
                    var rp = new RegularProperty();

                    while (parser.Current is not MappingEnd)
                    {
                        var prop = parser.Consume<Scalar>().Value;
                        var value = parser.Consume<Scalar>();

                        switch (prop)
                        {
                            case "name":
                                rp.Name = value.Value;
                                break;
                            case "label":
                                rp.Label = value.Value;
                                break;
                            case "primaryKey":
                                rp.PrimaryKey = value.Value == "true";
                                break;
                            case "required":
                                rp.Required = value.Value == "true";
                                break;
                            case "domain":
                                relationships.Add(rp, new Relation(value));
                                break;
                            case "defaultValue":
                                rp.DefaultValue = value.Value;
                                break;
                            case "comment":
                                rp.Comment = value.Value;
                                break;
                            default:
                                throw new ModelException($"Propriété ${prop} inconnue pour une propriété");
                        }
                    }

                    if (rp.PrimaryKey)
                    {
                        rp.Required = true;
                    }

                    yield return rp;
                    break;

                case Scalar { Value: "association" }:
                    var ap = new AssociationProperty();

                    while (parser.Current is not MappingEnd)
                    {
                        var prop = parser.Consume<Scalar>().Value;
                        var value = parser.Consume<Scalar>();

                        switch (prop)
                        {
                            case "association":
                                relationships.Add(ap, new Relation(value));
                                break;
                            case "asAlias":
                                ap.AsAlias = value.Value == "true";
                                break;
                            case "role":
                                ap.Role = value.Value;
                                break;
                            case "type":
                                ap.Type = value.Value switch
                                {
                                    "oneToOne" => AssociationType.OneToOne,
                                    "manyToOne" => AssociationType.ManyToOne,
                                    "manyToMany" => AssociationType.ManyToMany,
                                    _ => AssociationType.OneToMany
                                };
                                break;
                            case "label":
                                ap.Label = value.Value;
                                break;
                            case "required":
                                ap.Required = value.Value == "true";
                                break;
                            case "defaultValue":
                                ap.DefaultValue = value.Value;
                                break;
                            case "comment":
                                ap.Comment = value.Value;
                                break;
                            default:
                                throw new ModelException($"Propriété ${prop} inconnue pour une propriété");
                        }
                    }

                    yield return ap;
                    break;

                case Scalar { Value: "composition" }:
                    var cp = new CompositionProperty();

                    while (parser.Current is not MappingEnd)
                    {
                        var prop = parser.Consume<Scalar>().Value;
                        var value = parser.Consume<Scalar>();

                        switch (prop)
                        {
                            case "composition":
                                relationships.Add(cp, new Relation(value));
                                break;
                            case "name":
                                cp.Name = value.Value;
                                break;
                            case "kind":
                                cp.Kind = value.Value;
                                if (cp.Kind != "object" && cp.Kind != "list" && cp.Kind != "async-list")
                                {
                                    relationships.Add((cp, "kind"), new Relation(value));
                                }

                                break;
                            case "comment":
                                cp.Comment = value.Value;
                                break;
                            default:
                                throw new ModelException($"Propriété ${prop} inconnue pour une propriété");
                        }
                    }

                    yield return cp;
                    break;

                case Scalar { Value: "alias" }:
                    var alps = new List<(AliasProperty Alp, Scalar AliasProp)>();
                    Scalar? aliasClass = null;

                    parser.Consume<Scalar>();
                    parser.Consume<MappingStart>();

                    while (parser.Current is not MappingEnd)
                    {
                        var prop = parser.Consume<Scalar>().Value;
                        var next = parser.Consume<ParsingEvent>();

                        if (next is Scalar value)
                        {
                            switch (prop)
                            {
                                case "property":
                                    alps.Add((new AliasProperty(), value));
                                    break;
                                case "class":
                                    aliasClass = value;
                                    break;
                                default:
                                    throw new ModelException($"Propriété ${prop} inconnue pour un alias");
                            }
                        }
                        else if (next is SequenceStart)
                        {
                            while (parser.Current is not SequenceEnd)
                            {
                                alps.Add((new AliasProperty(), parser.Consume<Scalar>()));
                            }

                            parser.Consume<SequenceEnd>();
                        }
                    }

                    foreach (var (alp, aliasProp) in alps)
                    {
                        relationships.Add(alp, new Relation(aliasProp) { Peer = new Relation(aliasClass!) });
                    }

                    parser.Consume<MappingEnd>();

                    while (parser.Current is not MappingEnd)
                    {
                        var prop = parser.Consume<Scalar>().Value;
                        var value = parser.Consume<Scalar>();

                        foreach (var (alp, _) in alps)
                        {
                            switch (prop)
                            {
                                case "prefix":
                                    alp.Prefix = value.Value == "true" ? aliasClass!.Value : value.Value == "false" ? null : value.Value;
                                    break;
                                case "suffix":
                                    alp.Suffix = value.Value == "true" ? aliasClass!.Value : value.Value == "false" ? null : value.Value;
                                    break;
                                case "label":
                                    alp.Label = value.Value;
                                    break;
                                case "required":
                                    alp.Required = value.Value == "true";
                                    break;
                                case "comment":
                                    alp.Comment = value.Value;
                                    break;
                                case "asListWithDomain":
                                    relationships.Add((alp, "listDomain"), new Relation(value));
                                    break;
                                default:
                                    throw new ModelException($"Propriété ${prop} inconnue pour une propriété");
                            }
                        }
                    }

                    foreach (var (alp, _) in alps)
                    {
                        yield return alp;
                    }

                    break;

                default:
                    throw new ModelException($"Type de propriété inconnu.");
            }

            parser.Consume<MappingEnd>();
        }
    }
}
