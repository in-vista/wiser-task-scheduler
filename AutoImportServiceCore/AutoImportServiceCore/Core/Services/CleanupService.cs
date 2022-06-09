using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Serialization;
using AutoImportServiceCore.Core.Enums;
using AutoImportServiceCore.Core.Interfaces;
using AutoImportServiceCore.Core.Models;
using AutoImportServiceCore.Core.Models.Cleanup;
using GeeksCoreLibrary.Core.DependencyInjection.Interfaces;
using GeeksCoreLibrary.Core.Models;
using GeeksCoreLibrary.Core.Services;
using GeeksCoreLibrary.Modules.Databases.Interfaces;
using GeeksCoreLibrary.Modules.GclReplacements.Interfaces;
using GeeksCoreLibrary.Modules.Objects.Interfaces;
using Microsoft.Extensions.DependencyInjection;
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
        private readonly IServiceProvider serviceProvider;
        private readonly ILogService logService;
        private readonly ILogger<CleanupService> logger;

        private CleanupConfigurationModel configuration;

        /// <inheritdoc />
        public LogSettings LogSettings { get; set; }

        public CleanupService(IOptions<AisSettings> aisSettings, IServiceProvider serviceProvider, ILogService logService, ILogger<CleanupService> logger)
        {
            cleanupServiceSettings = aisSettings.Value.CleanupService;
            this.serviceProvider = serviceProvider;
            this.logService = logService;
            this.logger = logger;
        }

        /// <inheritdoc />
        public async Task CleanupAsync()
        {
            if (configuration == null && !String.IsNullOrWhiteSpace(cleanupServiceSettings.LocalCleanupConfiguration))
            {
                await LoadConfigurationAsync();
            }
            
            await CleanupFilesAsync();
            await CleanupDatabaseLogsAsync();
            await CleanupEntities();
        }
        
        private async Task LoadConfigurationAsync()
        {
            var serializer = new XmlSerializer(typeof(CleanupConfigurationModel));
            using var reader = new StringReader(await File.ReadAllTextAsync(cleanupServiceSettings.LocalCleanupConfiguration));
            configuration = (CleanupConfigurationModel)serializer.Deserialize(reader);

            configuration.LogSettings ??= LogSettings;
        }

        /// <summary>
        /// Cleanup files older than the set number of days in the given folders.
        /// </summary>
        private async Task CleanupFilesAsync()
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

        /// <summary>
        /// Cleanup logs in the database older than the set number of days in the AIS logs.
        /// </summary>
        private async Task CleanupDatabaseLogsAsync()
        {
            using var scope = serviceProvider.CreateScope();
            using var databaseConnection = scope.ServiceProvider.GetRequiredService<IDatabaseConnection>();
            
            databaseConnection.AddParameter("cleanupDate", DateTime.Now.AddDays(-cleanupServiceSettings.NumberOfDaysToStore));
            var rowsDeleted = await databaseConnection.ExecuteAsync($"DELETE FROM {WiserTableNames.AisLogs} WHERE added_on < ?cleanupDate", cleanUp: true);

            await logService.LogInformation(logger, LogScopes.RunStartAndStop, LogSettings, $"Cleaned up {rowsDeleted} rows in '{WiserTableNames.AisLogs}'.", LogName);
        }

        /// <summary>
        /// Cleanup items of given entities in the database older than the set number of days in the given entity.
        /// </summary>
        private async Task CleanupEntities()
        {
            if (configuration == null)
            {
                return;
            }
            
            using var scope = serviceProvider.CreateScope();
            using var databaseConnection = scope.ServiceProvider.GetRequiredService<IDatabaseConnection>();
            
            // Wiser Items Service requires dependency injection that results in the need of MVC services that are unavailable.
            // Get all other services and create the Wiser Items Service with one of the services missing.
            var objectService = scope.ServiceProvider.GetRequiredService<IObjectsService>();
            var stringReplacementsService = scope.ServiceProvider.GetRequiredService<IStringReplacementsService>();
            var databaseHelpersService = scope.ServiceProvider.GetRequiredService<IDatabaseHelpersService>();
            var gclSettings = scope.ServiceProvider.GetRequiredService<IOptions<GclSettings>>();
            var wiserItemsServiceLogger = scope.ServiceProvider.GetRequiredService<ILogger<WiserItemsService>>();
            
            var wiserItemsService = new WiserItemsService(databaseConnection, objectService, stringReplacementsService, null, databaseHelpersService, gclSettings, wiserItemsServiceLogger);

            foreach (var entity in configuration.Entities)
            {
                var tablePrefix = await wiserItemsService.GetTablePrefixForEntityAsync(entity.Name);
            
                databaseConnection.AddParameter("entityName", entity.Name);
                databaseConnection.AddParameter("cleanupDate", DateTime.Now.AddDays(-entity.NumberOfDaysToStore));
                var dataTable = await databaseConnection.GetAsync($"SELECT id FROM {tablePrefix}{WiserTableNames.WiserItem}{(entity.FromArchive ? WiserTableNames.ArchiveSuffix : "")} WHERE entity_type = ?entityName AND {(entity.SinceLastChange ? "changed_on" : "added_on")} < ?cleanupDate");

                if (dataTable.Rows.Count == 0)
                {
                    continue;
                }

                var ids = new List<ulong>();
                foreach (DataRow row in dataTable.Rows)
                {
                    ids.Add(row.Field<ulong>("id"));
                }
            }
        }
    }
}
