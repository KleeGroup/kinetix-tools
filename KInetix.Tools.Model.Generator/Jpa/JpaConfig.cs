﻿#nullable disable

namespace Kinetix.Tools.Model.Generator.Jpa
{
    public class JpaConfig : GeneratorConfigBase
    {
        /// <summary>
        /// Dossier de sortie pour le modèle.
        /// </summary>
        public string ModelOutputDirectory { get; set; }

        /// <summary>
        /// Précise le nom du package dans lequel générer les classes du modèle.
        /// </summary>
        public string DaoPackageName { get; set; }

        /// <summary>
        /// Précise le nom du package dans lequel générer les controllers.
        /// </summary>
        public string ApiPackageName { get; set; }

#nullable enable

        /// <summary>
        /// Dossier de sortie pour les Controllers.
        /// </summary>
        public string? ApiOutputDirectory { get; set; }
    }
}