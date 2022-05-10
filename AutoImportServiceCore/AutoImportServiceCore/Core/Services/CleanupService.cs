using System;
using System.IO;
using System.Threading.Tasks;
using AutoImportServiceCore.Core.Enums;
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
        private const string LogName = "CleanupService";

        private readonly CleanupServiceSettings cleanupServiceSettings;
        private readonly ILogService logService;
        private readonly ILogger<CleanupService> logger;

        /// <inheritdoc />
        public LogSettings LogSettings { get; set; }

        public CleanupService(IOptions<AisSettings> aisSettings, ILogService logService, ILogger<CleanupService> logger)
        {
            cleanupServiceSettings = aisSettings.Value.CleanupService;
            this.logService = logService;
            this.logger = logger;
        }

        /// <inheritdoc />
        public async Task CleanupAsync()
        {
            await CleanupFiles();
        }

        /// <summary>
        /// Cleanup files older than the set number of days in the given folders.
        /// </summary>
        private async Task CleanupFiles()
        {
            if (cleanupServiceSettings.FileFolderPaths == null || cleanupServiceSettings.FileFolderPaths.Length == 0)
            {
                return;
            }

            foreach (var folderPath in cleanupServiceSettings.FileFolderPaths)
            {
                try
                {
                    var files = Directory.GetFiles(folderPath);
                    await logService.LogInformation(logger, LogScopes.RunStartAndStop, LogSettings, $"Found {files.Length} files in '{folderPath}' to perform cleanup on.", LogName);
                    var filesDeleted = 0;

                    foreach (var file in files)
                    {
                        try
                        {
                            if (File.GetLastWriteTime(file) >= DateTime.Now.AddDays(-cleanupServiceSettings.NumberOfDaysToStore))
                            {
                                continue;
                            }

                            File.Delete(file);
                            filesDeleted++;
                            await logService.LogInformation(logger, LogScopes.RunBody, LogSettings, $"Deleted file: {file}", LogName);
                        }
                        catch (Exception e)
                        {
                            await logService.LogError(logger, LogScopes.RunBody, LogSettings, $"Could not delete file: {file} due to exception {e}", LogName);
                        }
                    }

                    await logService.LogInformation(logger, LogScopes.RunStartAndStop, LogSettings, $"Cleaned up {filesDeleted} files in '{folderPath}'.", LogName);
                }
                catch (Exception e)
                {
                    await logService.LogError(logger, LogScopes.RunStartAndStop, LogSettings, $"Could not delete files in folder: {folderPath} due to exception {e}", LogName);
                }
            }
        }
    }
}
