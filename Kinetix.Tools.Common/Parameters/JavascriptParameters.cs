namespace Kinetix.Tools.Common.Parameters
{
    /// <summary>
    /// Paramètres pour la génération du Javascript.
    /// </summary>
    public class JavascriptParameters
    {
        /// <summary>
        /// Dossier de sortie pour le modèle.
        /// </summary>
        public string ModelOutputDirectory { get; set; }

        /// <summary>
        /// Dossier de sortie pour les ressources.
        /// </summary>
        public string ResourceOutputDirectory { get; set; }

        /// <summary>
        /// If should generate entities in JS.
        /// </summary>
        public bool IsGenerateEntities { get; set; } = true;

        /// <summary>
        /// Génération de modèle pour Focus4 v8.x.
        /// </summary>
        public bool Focus4v8 { get; set; } = false;
    }
}
