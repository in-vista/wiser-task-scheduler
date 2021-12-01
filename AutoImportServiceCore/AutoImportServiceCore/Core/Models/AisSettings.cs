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
        /// Gets or sets the run scheme for the <see cref="MainWorker"/>.
        /// </summary>
        public RunSchemeModel MainRunScheme { get; set; }
    }
}
