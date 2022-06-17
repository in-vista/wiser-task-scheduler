using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using AutoImportServiceCore.Core.Enums;
using AutoImportServiceCore.Core.Interfaces;
using AutoImportServiceCore.Core.Models;
using AutoImportServiceCore.Modules.CleanupItems.Interfaces;
using AutoImportServiceCore.Modules.CleanupItems.Models;
using GeeksCoreLibrary.Core.DependencyInjection.Interfaces;
using GeeksCoreLibrary.Core.Models;
using GeeksCoreLibrary.Core.Services;
using GeeksCoreLibrary.Modules.Databases.Interfaces;
using GeeksCoreLibrary.Modules.GclReplacements.Interfaces;
using GeeksCoreLibrary.Modules.Objects.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace AutoImportServiceCore.Modules.CleanupItems.Services;

public class CleanupItemsService : ICleanupItemsService, IActionsService, IScopedService
{
    private readonly IServiceProvider serviceProvider;
    private readonly ILogService logService;
    private readonly ILogger<CleanupItemsService> logger;

    private string connectionString;

    public CleanupItemsService(IServiceProvider serviceProvider, ILogService logService, ILogger<CleanupItemsService> logger)
    {
        this.serviceProvider = serviceProvider;
        this.logService = logService;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task Initialize(ConfigurationModel configuration)
    {
        connectionString = configuration.ConnectionString;
    }

    /// <inheritdoc />
    public async Task<JObject> Execute(ActionModel action, JObject resultSets, string configurationServiceName)
    {
        var cleanupItem = (CleanupItemModel) action;

        await logService.LogInformation(logger, LogScopes.RunStartAndStop, cleanupItem.LogSettings, $"Starting cleanup for items of entity '{cleanupItem.EntityName}' that are older than '{cleanupItem.TimeToStore}'.", configurationServiceName, cleanupItem.TimeId, cleanupItem.Order);

        using var scope = serviceProvider.CreateScope();
        using var databaseConnection = scope.ServiceProvider.GetRequiredService<IDatabaseConnection>();

        var connectionStringToUse = cleanupItem.ConnectionString ?? connectionString;
        await databaseConnection.ChangeConnectionStringsAsync(connectionStringToUse, connectionStringToUse);
        databaseConnection.ClearParameters();
        
        // Wiser Items Service requires dependency injection that results in the need of MVC services that are unavailable.
        // Get all other services and create the Wiser Items Service with one of the services missing.
        var objectService = scope.ServiceProvider.GetRequiredService<IObjectsService>();
        var stringReplacementsService = scope.ServiceProvider.GetRequiredService<IStringReplacementsService>();
        var databaseHelpersService = scope.ServiceProvider.GetRequiredService<IDatabaseHelpersService>();
        var gclSettings = scope.ServiceProvider.GetRequiredService<IOptions<GclSettings>>();
        var wiserItemsServiceLogger = scope.ServiceProvider.GetRequiredService<ILogger<WiserItemsService>>();
        
        var wiserItemsService = new WiserItemsService(databaseConnection, objectService, stringReplacementsService, null, databaseHelpersService, gclSettings, wiserItemsServiceLogger);
        var tablePrefix = await wiserItemsService.GetTablePrefixForEntityAsync(cleanupItem.EntityName);
        
        databaseConnection.AddParameter("entityName", cleanupItem.EntityName);
        var entityDataTable = await databaseConnection.GetAsync($"SELECT delete_action FROM {WiserTableNames.WiserEntity} WHERE `name` = ?entityName LIMIT 1");
        var deleteAction = entityDataTable.Rows[0].Field<string>("delete_action");
        
        var cleanupDate = DateTime.Now.Subtract(cleanupItem.TimeToStore);
        databaseConnection.AddParameter("cleanupDate", cleanupDate);

        var query = $@"SELECT id
FROM {tablePrefix}{WiserTableNames.WiserItem}
WHERE entity_type = ?entityName
AND TIMEDIFF({(cleanupItem.SinceLastChange ? "changed_on" : "added_on")}, ?cleanupDate) <= 0";
        
        var itemsDataTable = await databaseConnection.GetAsync(query);

        if (itemsDataTable.Rows.Count == 0)
        {
            await logService.LogInformation(logger, LogScopes.RunStartAndStop, cleanupItem.LogSettings, $"Finished cleanup for items of entity '{cleanupItem.EntityName}', no items found to cleanup.", configurationServiceName, cleanupItem.TimeId, cleanupItem.Order);

            return new JObject()
            {
                {"Success", true},
                {"Entity", cleanupItem.EntityName},
                {"CleanupDate", cleanupDate},
                {"ItemsToCleanup", 0},
                {"DeleteAction", deleteAction}
            };
        }

        var ids = (from DataRow row in itemsDataTable.Rows
                   select row.Field<ulong>("id")).ToList();

        var success = true;
        
        try
        {
            await wiserItemsService.DeleteAsync(ids, username: "AIS Cleanup", saveHistory: cleanupItem.SaveHistory, skipPermissionsCheck: true);
            await logService.LogInformation(logger, LogScopes.RunStartAndStop, cleanupItem.LogSettings, $"Finished cleanup for items of entity '{cleanupItem.EntityName}', delete action: '{deleteAction}'.", configurationServiceName, cleanupItem.TimeId, cleanupItem.Order);
        }
        catch (Exception e)
        {
            await logService.LogInformation(logger, LogScopes.RunStartAndStop, cleanupItem.LogSettings, $"Failed cleanup for items of entity '{cleanupItem.EntityName}' due to exception:\n{e}", configurationServiceName, cleanupItem.TimeId, cleanupItem.Order);
            success = false;
        }
        
        return new JObject()
        {
            {"Success", success},
            {"Entity", cleanupItem.EntityName},
            {"CleanupDate", cleanupDate},
            {"ItemsToCleanup", itemsDataTable.Rows.Count},
            {"DeleteAction", deleteAction}
        };
    }
}