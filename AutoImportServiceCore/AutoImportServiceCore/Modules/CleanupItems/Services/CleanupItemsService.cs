using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
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
        databaseConnection.AddParameter("cleanupDate", DateTime.Now.AddDays(-cleanupItem.NumberOfDaysToStore));
        var dataTable = await databaseConnection.GetAsync($"SELECT id FROM {tablePrefix}{WiserTableNames.WiserItem}{(cleanupItem.FromArchive ? WiserTableNames.ArchiveSuffix : "")} WHERE entity_type = ?entityName AND {(cleanupItem.SinceLastChange ? "changed_on" : "added_on")} < ?cleanupDate");

        if (dataTable.Rows.Count == 0)
        {
            return null;
        }

        var ids = new List<ulong>();
        foreach (DataRow row in dataTable.Rows)
        {
            ids.Add(row.Field<ulong>("id"));
        }
        
        return null;
    }
}