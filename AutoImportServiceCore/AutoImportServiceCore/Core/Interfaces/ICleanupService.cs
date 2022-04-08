using System.Threading.Tasks;
using AutoImportServiceCore.Core.Models;

namespace AutoImportServiceCore.Core.Interfaces
{
    /// <summary>
    /// A service to cleanup after the application.
    /// </summary>
    public interface ICleanupService
    {
        /// <summary>
        /// Gets or sets the log settings that the Main service needs to use.
        /// </summary>
        LogSettings LogSettings { get; set; }

        /// <summary>
        /// Cleanup after the application.
        /// </summary>
        /// <returns></returns>
        Task CleanupAsync();
    }
}
