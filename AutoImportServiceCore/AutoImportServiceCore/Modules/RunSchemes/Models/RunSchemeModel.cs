using System;
using System.ComponentModel.DataAnnotations;
using AutoImportServiceCore.Modules.RunSchemes.Enums;

namespace AutoImportServiceCore.Modules.RunSchemes.Models
{
    /// <summary>
    /// A model for the run scheme.
    /// </summary>
    public class RunSchemeModel
    {
        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        [Required]
        public RunSchemeTypes Type { get; set; }

        /// <summary>
        /// Gets or sets the time ID.
        /// </summary>
        [Required]
        public int TimeId { get; set; }

        /// <summary>
        /// Gets or sets the delay.
        /// </summary>
        public TimeSpan Delay { get; set; }
    }
}
