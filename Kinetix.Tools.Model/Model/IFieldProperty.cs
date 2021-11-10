﻿using Kinetix.Tools.Model.Types;

namespace Kinetix.Tools.Model
{
    public interface IFieldProperty : IProperty
    {
        bool Required { get; }

        Domain Domain { get; }

        string? DefaultValue { get; }

        TSType TS
        {
            get
            {
                if (Domain.TS == null)
                {
                    throw new ModelException($"Le type Typescript du domaine {Domain.Name} doit être renseigné.");
                }

                var fixedType = new TSType { Type = Domain.TS.Type, Import = Domain.TS.Import };
                if (Domain.TS.Type == "string")
                {
                    var prop = this is AliasProperty alp ? alp.Property : this;

                    if (prop is AssociationProperty ap && ap.Association.Reference && ap.Association.PrimaryKey!.Domain.Name != "DO_ID")
                    {
                        fixedType.Type = $"{ap.Association.Name}{ap.Association.PrimaryKey!.Name}";
                    }
                    else if (prop.PrimaryKey && prop.Class.Reference && prop.Class.PrimaryKey!.Domain.Name != "DO_ID")
                    {
                        fixedType.Type = $"{prop.Class.Name}{prop.Name}";
                    }
                }

                if (this is AliasProperty { ListDomain: not null })
                {
                    fixedType.Type += "[]";
                }

                return fixedType;
            }
        }

        string SqlName
        {
            get
            {
                var prop = this is AliasProperty alp ? alp.Property : this;

                return prop.Class.Extends != null && prop.PrimaryKey && Class.Trigram != null
                    ? $"{Class.Trigram}_{ModelUtils.ConvertCsharp2Bdd(Name).Replace(prop.Class.SqlName + "_", string.Empty)}"
                    : prop is AssociationProperty ap
                    ? ap.Association.PrimaryKey!.SqlName + (ap.Role != null ? $"_{ap.Role.Replace(" ", "_").ToUpper()}" : string.Empty)
                    : prop.Class.Trigram != null
                    ? $"{prop.Class.Trigram}_{ModelUtils.ConvertCsharp2Bdd(prop.Name)}"
                    : ModelUtils.ConvertCsharp2Bdd(prop.Name);
            }
        }

        string JavaName => this is AssociationProperty ap
            ? (ap.Association.Trigram?.ToLower().ToFirstUpper() ?? string.Empty) + ap.Association.PrimaryKey!.Name + (ap.Role?.Replace(" ", string.Empty) ?? string.Empty)
            : (Class.Trigram?.ToLower().ToFirstUpper() ?? string.Empty) + Name;
    }
}
