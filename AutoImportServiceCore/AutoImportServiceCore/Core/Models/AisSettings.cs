using AutoImportServiceCore.Core.Workers;
using AutoImportServiceCore.Modules.RunSchemes.Models;

namespace AutoImportServiceCore.Core.Models
{
    /// <summary>
    /// The settings for the AIS.
    /// </summary>
    public class AisSettings
    {
        /// <summary>
        /// Gets or sets the settings of the <see cref="MainService"/>.
        /// </summary>
        public MainServiceSettings MainService { get; set; }
    }
}
