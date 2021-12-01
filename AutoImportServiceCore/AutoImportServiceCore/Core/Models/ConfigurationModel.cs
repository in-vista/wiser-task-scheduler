using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
        /// Gets or sets the run scheme.
        /// </summary>
        [Required]
        public IEnumerable<RunSchemeModel> RunSchemes { get; set; }
    }
}
