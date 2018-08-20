namespace Kinetix.Tools.Common.Parameters
{
    /// <summary>
    /// Parameters for the service generator.
    /// </summary>
    public class ServiceParameters
    {
        /// <summary>
        /// Path to the *.sln file.
        /// </summary>
        public string SolutionPath { get; set; }

        /// <summary>
        /// Directory where to generate the services.
        /// </summary>
        public string OutputDirectory { get; set; }

        /// <summary>
        /// Root namespace of the project.
        /// </summary>
        public string RootNamespace { get; set; }

        /// <summary>
        /// Root path to the generated model files.
        /// </summary>
        public string ModelRoot { get; set; }

        /// <summary>
        /// Path to the fetch method import.
        /// </summary>
        public string FetchPath { get; set; }

        /// <summary>
        /// Kinetix version (Core or Framework).
        /// </summary>
        public string Kinetix { get; set; }

        /// <summary>
        /// Considers the first separation (different frontends or else the first folder) as a seperate app.
        /// Generates "/{firstFolder}/services/{otherFolders}/controller.ts" instead of "/services/{folders}/controller.ts"
        /// </summary>
        public bool? SplitIntoApps { get; set; }
    }
}
