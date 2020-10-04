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
        /// Directory where to generate the services (should be the "app" folder of the SPA).
        /// </summary>
        public string OutputDirectory { get; set; }

        /// <summary>
        /// Root namespace of the project.
        /// </summary>
        public string RootNamespace { get; set; }

        /// <summary>
        /// Suffix of the projects to search for controllers.
        /// </summary>
        public string ProjectsSuffix { get; set; }

        /// <summary>
        /// Root path to the generated model files, relative to the output directory.
        /// </summary>
        public string ModelRoot { get; set; }

        /// <summary>
        /// Path to the fetch method import, relative to the output directory.
        /// </summary>
        public string FetchPath { get; set; }

        /// <summary>
        /// Kinetix version (Core or Framework).
        /// </summary>
        public string Kinetix { get; set; }

        /// <summary>
        /// Splits the generation of services from different frontends into separate top folders.
        /// Generates "/{frontEnd}/services/{folders}/controller.ts" instead of "/services/{frontEnd}/{folders}/controller.ts"
        /// </summary>
        public bool? SplitIntoApps { get; set; }
    }
}
