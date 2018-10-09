namespace Kinetix.Tools.Common.Model
{
    /// <summary>
    /// Contrat des objets ayant des données de persistance.
    /// </summary>
    public interface IPersistenceData
    {
        /// <summary>
        /// Obtient ou définit le type persistant.
        /// </summary>
        string PersistentDataType { get; set; }
    }
}
