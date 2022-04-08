using System;
using System.IO;
using System.Threading.Tasks;
using AutoImportServiceCore.Core.Enums;
using AutoImportServiceCore.Core.Helpers;
using AutoImportServiceCore.Core.Interfaces;
using AutoImportServiceCore.Core.Models;
using GeeksCoreLibrary.Core.DependencyInjection.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoImportServiceCore.Core.Services
{
    /// <summary>
    /// A service to manage all AIS configurations that are provided by Wiser.
    /// </summary>
    public class CleanupService : ICleanupService, ISingletonService
    {
        private readonly CleanupServiceSettings cleanupServiceSettings;
        private readonly ILogger<CleanupService> logger;

        /// <inheritdoc />
        public LogSettings LogSettings { get; set; }

        public CleanupService(IOptions<AisSettings> aisSettings, ILogger<CleanupService> logger)
        {
            cleanupServiceSettings = aisSettings.Value.CleanupService;
            this.logger = logger;
        }

        /// <inheritdoc />
        public async Task CleanupAsync()
        {
            CleanupFiles();
        }

        /// <summary>
        /// Cleanup files older than the set number of days in the given folders.
        /// </summary>
        private void CleanupFiles()
        {
            if (cleanupServiceSettings.FileFolderPaths == null || cleanupServiceSettings.FileFolderPaths.Length == 0)
            {
                return;
            }

            foreach (var folderPath in cleanupServiceSettings.FileFolderPaths)
            {
                var files = Directory.GetFiles(folderPath);
                LogHelper.LogInformation(logger, LogScopes.RunStartAndStop, LogSettings, $"Found {files.Length} files in '{folderPath}' to perform cleanup on.");
                var filesDeleted = 0;

                foreach (var file in files)
                {
                    if (File.GetLastWriteTime(file) >= DateTime.Now.AddDays(-cleanupServiceSettings.NumberOfDaysToStore))
                    {
                        continue;
                    }

                    File.Delete(file);
                    filesDeleted++;
                    LogHelper.LogInformation(logger, LogScopes.RunBody, LogSettings, $"Deleted file: {file}");
                }

                LogHelper.LogInformation(logger, LogScopes.RunStartAndStop, LogSettings, $"Cleaned up {filesDeleted} files in '{folderPath}'.");
            }
        }
    }
}
