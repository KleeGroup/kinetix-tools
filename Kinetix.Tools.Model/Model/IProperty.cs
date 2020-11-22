﻿namespace Kinetix.Tools.Model
{
    public interface IProperty
    {
        string Name { get; }

        string? Label { get; }

        bool PrimaryKey { get; }

        string Comment { get; }

        Class Class { get; set; }
    }
}
