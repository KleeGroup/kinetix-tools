﻿namespace Kinetix.Tools.Model.Config
{
    /// <summary>
    /// Paramètres pour la génération du C#.
    /// </summary>
    public class CSharpConfig
    {
        /// <summary>
        /// Obtient ou définit le répertoire de génération.
        /// </summary>
        public string? OutputDirectory { get; set; }

        /// <summary>
        /// Nom du projet dans lequel mettre le DbContext.
        /// </summary>
        public string? DbContextProjectPath { get; set; }

        /// <summary>
        /// Utilise des types spécifiques pour les valeurs de listes statiques, au lieu de string.
        /// </summary>
        public bool UseTypeSafeConstValues { get; set; }

        /// <summary>
        /// Le nom du schéma de base de données à cibler (si non renseigné, EF utilise 'dbo').
        /// </summary>
        public string? DbSchema { get; set; }

        /// <summary>
        /// Version de kinetix utilisée: Core, Framework ou Fmk.
        /// </summary>
        public KinetixVersion Kinetix { get; set; }

        /// <summary>
        /// Définit un mode legacy pour Identité en .NET Framework
        /// </summary>
        public bool LegacyIdentity { get; set; }

        /// <summary>
        /// Retire les attributs de colonnes sur les alias.
        /// </summary>
        public bool NoColumnOnAlias { get; set; }

        /// <summary>
        /// Définit si les interfaces IEntity sont ajoutée à la génération.
        /// </summary>
        public bool IsWithEntityInterface { get; set; }

        /// <summary>
        /// Utilise la structure de projet legacy.
        /// </summary>
        public bool LegacyProjectPaths { get; set; }
    }
}
