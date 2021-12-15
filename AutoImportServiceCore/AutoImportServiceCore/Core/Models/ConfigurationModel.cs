using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AutoImportServiceCore.Modules.HttpApis.Models;
using AutoImportServiceCore.Modules.Queries.Models;
using AutoImportServiceCore.Modules.RunSchemes.Models;

namespace AutoImportServiceCore.Core.Models
{
    /// <summary>
    /// A model for the configuration.
    /// </summary>
    public class ConfigurationModel
    {
        /// <summary>
        /// Gets or sets the service name.
        /// </summary>
        [Required]
        public string ServiceName { get; set; }

        /// <summary>
        /// Gets or sets the connection string that is used for queries.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// Gets or sets the run scheme.
        /// </summary>
        [Required]
        public IEnumerable<RunSchemeModel> RunSchemes { get; set; }

        /// <summary>
        /// Gets or sets the log settings.
        /// </summary>
        public LogSettings LogSettings { get; set; }

        /// <summary>
        /// Gets or sets the queries.
        /// </summary>
        public QueryModel[] Queries { get; set; }

        /// <summary>
        /// Gets or sets the HTTP APIs.
        /// </summary>
        public HttpApiModel[] HttpApis { get; set; }
    }
}
