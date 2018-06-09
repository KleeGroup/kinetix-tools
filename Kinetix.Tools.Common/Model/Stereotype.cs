namespace Kinetix.Tools.Common.Model
{
    /// <summary>
    /// Represente un stéréotype.
    /// </summary>
    public abstract class Stereotype
    {
        /// <summary>
        /// Nom du stéréotype associé aux listes de référence statiques.
        /// </summary>
        public const string Statique = "Statique";

        /// <summary>
        /// Nom du stéréotype associé aux listes de référence administrables.
        /// </summary>
        public const string Reference = "Reference";

        /// <summary>
        /// Nom du stéréotype associé à la propriété par défaut d'un DTO.
        /// </summary>
        public const string DefaultProperty = "DefaultProperty";

        /// <summary>
        /// Nom du stéréotype associé aux propriétés qui sont persistés mais qui ne sont pas dans le DTO.
        /// </summary>
        public const string DatabaseOnly = "DatabaseOnly";
    }
}
