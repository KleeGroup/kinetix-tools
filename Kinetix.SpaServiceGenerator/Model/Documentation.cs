﻿using System;
using System.Collections.Generic;

namespace Kinetix.SpaServiceGenerator.Model
{
    /// <summary>
    /// Représente la documentation d'un service.
    /// </summary>
    public class Documentation
    {
        /// <summary>
        /// Le summary.
        /// </summary>
        public string Summary { get; set; }

        /// <summary>
        /// Returns.
        /// </summary>
        public string Returns { get; set; }

        /// <summary>
        /// La liste des documentations de paramètres (nom, description).
        /// </summary>
        public ICollection<Tuple<string, string>> Parameters { get; set; }
    }
}