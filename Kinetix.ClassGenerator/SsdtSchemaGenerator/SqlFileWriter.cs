using Kinetix.Tools.Common;

namespace Kinetix.ClassGenerator.SsdtSchemaGenerator
{
    /// <summary>
    /// Writer pour l'écriture de fichier.
    /// Spécifique pour les fichiers SQL (usage du token commentaire SQL).
    /// </summary>
    internal class SqlFileWriter : FileWriter
    {
        private readonly string _sqlprojFileName;

        /// <summary>
        /// Crée une nouvelle instance.
        /// </summary>
        /// <param name="fileName">Nom du fichier à écrire.</param>
        /// <param name="sqlprojFileName">Nom du fichier sqlproj.</param>
        public SqlFileWriter(string fileName, string sqlprojFileName = null)
            : base(fileName)
        {
            _sqlprojFileName = sqlprojFileName;
        }

        /// <summary>
        /// Renvoie le token de début de ligne de commentaire dans le langage du fichier.
        /// </summary>
        /// <returns>Toket de début de ligne de commentaire.</returns>
        protected override string StartCommentToken => "----";
    }
}
