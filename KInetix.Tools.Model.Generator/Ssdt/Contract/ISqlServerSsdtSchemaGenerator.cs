﻿using System.Collections.Generic;
using Kinetix.Tools.Model;

namespace Kinetix.Tools.Model.Generator.Ssdt.Contract
{
    /// <summary>
    /// Contrat du générateur de Transact-SQL (structure) visant une structure de fichiers SSDT.
    /// </summary>
    public interface ISqlServerSsdtSchemaGenerator
    {
        /// <summary>
        /// Génère le script SQL.
        /// </summary>
        /// <param name="classes">Liste des toutes les classes.</param>
        /// <param name="tableScriptFolder">Dossier contenant les fichiers de script des tables.</param>
        /// <param name="tableTypeScriptFolder">Dossier contenant les fichiers de script des types de table.</param>
        void GenerateSchemaScript(IEnumerable<Class> classes, string tableScriptFolder, string tableTypeScriptFolder);
    }
}
