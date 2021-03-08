﻿namespace Kinetix.Tools.Model.Generator.CSharp
{
    /// <summary>
    /// Version de Kinetix.
    /// </summary>
    public enum ApiGeneration
    {
        /// <summary>
        /// Pas de génération.
        /// </summary>
        None,

        /// <summary>
        /// Génération d'un serveur.
        /// </summary>
        Server,

        /// <summary>
        /// Gébération d'un client.
        /// </summary>
        Client
    }
}