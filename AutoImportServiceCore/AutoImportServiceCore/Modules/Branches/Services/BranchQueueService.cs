using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using AutoImportServiceCore.Core.Enums;
using AutoImportServiceCore.Core.Interfaces;
using AutoImportServiceCore.Core.Models;
using AutoImportServiceCore.Modules.Branches.Interfaces;
using AutoImportServiceCore.Modules.Branches.Models;
using GeeksCoreLibrary.Core.DependencyInjection.Interfaces;
using GeeksCoreLibrary.Core.Models;
using GeeksCoreLibrary.Modules.Databases.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AutoImportServiceCore.Modules.Branches.Services
{
    /// <inheritdoc cref="IBranchQueueService" />
    public class BranchQueueService : IBranchQueueService, IActionsService, IScopedService
    {
        private readonly ILogService logService;
        private readonly ILogger<BranchQueueService> logger;
        private readonly IServiceProvider serviceProvider;

        private string connectionString;

        /// <summary>
        /// Creates a new instance of <see cref="BranchQueueService"/>.
        /// </summary>
        public BranchQueueService(ILogService logService, ILogger<BranchQueueService> logger, IServiceProvider serviceProvider)
        {
            this.logService = logService;
            this.logger = logger;
            this.serviceProvider = serviceProvider;
        }

        /// <inheritdoc />
        public async Task Initialize(ConfigurationModel configuration)
        {
            connectionString = configuration.ConnectionString;
        }

        /// <inheritdoc />
        public async Task<JObject> Execute(ActionModel action, JObject resultSets, string configurationServiceName)
        {
            using var scope = serviceProvider.CreateScope();
            var databaseConnection = scope.ServiceProvider.GetRequiredService<IDatabaseConnection>();
            var databaseHelpersService = scope.ServiceProvider.GetRequiredService<IDatabaseHelpersService>();
            
            var branchQueue = (BranchQueueModel) action;
            
            await databaseConnection.ChangeConnectionStringsAsync(connectionString, connectionString);
            databaseConnection.ClearParameters();

            await logService.LogInformation(logger, LogScopes.RunStartAndStop, branchQueue.LogSettings, $"Executing HTTP API in time id: {branchQueue.TimeId}, order: {branchQueue.Order}", configurationServiceName, branchQueue.TimeId, branchQueue.Order);

            await databaseHelpersService.CheckAndUpdateTablesAsync(new List<string> {WiserTableNames.WiserBranchesQueue});
            
            // Use .NET time and not database time, because we often use DigitalOcean and they have their timezone set to UTC by default.
            databaseConnection.AddParameter("now", DateTime.Now);
            var dataTable = await databaseConnection.GetAsync($@"SELECT * 
FROM {WiserTableNames.WiserBranchesQueue}
WHERE started_on IS NULL
AND start_on <= ?now
ORDER BY start_on ASC, id ASC");

            foreach (DataRow dataRow in dataTable.Rows)
            {
                var branchAction = dataRow.Field<string>("action");
                switch (branchAction)
                {
                    case "create":
                        await HandleCreateBranchActionAsync(dataRow, databaseConnection, databaseHelpersService);
                        break;
                    case "merge":
                        await HandleMergeBranchActionAsync(dataRow, databaseConnection, databaseHelpersService);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(branchAction), branchAction);
                }
            }

            // TODO: Fill object results with status.
            return new JObject();
        }

        private async Task HandleCreateBranchActionAsync(DataRow dataRowWithSettings, IDatabaseConnection databaseConnection, IDatabaseHelpersService databaseHelpersService)
        {
            // Add the new customer environment to easy_customers.
            var newCustomer = new CustomerModel
            {
                CustomerId = currentCustomer.CustomerId,
                Name = newCustomerName,
                EncryptionKey = SecurityHelpers.GenerateRandomPassword(20),
                SubDomain = subDomain,
                WiserTitle = newCustomerTitle,
                Database = new ConnectionInformationModel
                {
                    Host = currentCustomer.Database.Host,
                    Password = currentCustomer.Database.Password,
                    Username = currentCustomer.Database.Username,
                    DatabaseName = databaseName
                }
            };
            
            try
            {
                await wiserDatabaseConnection.BeginTransactionAsync();
                await clientDatabaseConnection.BeginTransactionAsync();

                await wiserCustomersService.CreateOrUpdateCustomerAsync(newCustomer);

                // Create the database in the same server/cluster.
                await databaseHelpersService.CreateDatabaseAsync(databaseName);

                // Create tables in new database.
                var query = @"SELECT TABLE_NAME 
                            FROM INFORMATION_SCHEMA.TABLES
                            WHERE TABLE_SCHEMA = ?currentSchema
                            AND TABLE_TYPE = 'BASE TABLE'
                            AND TABLE_NAME NOT LIKE '\_%'";

                clientDatabaseConnection.AddParameter("currentSchema", currentCustomer.Database.DatabaseName);
                clientDatabaseConnection.AddParameter("newSchema", newCustomer.Database.DatabaseName);
                var dataTable = await clientDatabaseConnection.GetAsync(query);
                var tablesToAlwaysLeaveEmpty = new List<string>
                {
                    WiserTableNames.WiserHistory, 
                    WiserTableNames.WiserImport, 
                    WiserTableNames.WiserImportLog, 
                    WiserTableNames.WiserUsersAuthenticationTokens, 
                    WiserTableNames.WiserCommunicationGenerated, 
                    WiserTableNames.AisLogs, 
                    "ais_serilog", 
                    "jcl_email"
                };
                
                // Create the tables in a new connection, because these cause implicit commits.
                await using (var mysqlConnection = new MySqlConnection(wiserCustomersService.GenerateConnectionStringFromCustomer(newCustomer)))
                {
                    await mysqlConnection.OpenAsync();
                    await using (var command = mysqlConnection.CreateCommand())
                    {
                        foreach (DataRow dataRow in dataTable.Rows)
                        {
                            var tableName = dataRow.Field<string>("TABLE_NAME");

                            command.CommandText = $"CREATE TABLE `{newCustomer.Database.DatabaseName.ToMySqlSafeValue(false)}`.`{tableName.ToMySqlSafeValue(false)}` LIKE `{currentCustomer.Database.DatabaseName.ToMySqlSafeValue(false)}`.`{tableName.ToMySqlSafeValue(false)}`";
                            await command.ExecuteNonQueryAsync();
                        }
                    }
                }

                // Fill the tables with data.
                var entityTypesString = String.Join(",", entityTypesToSkipWhenSynchronisingEnvironments.Select(x => $"'{x}'"));
                foreach (DataRow dataRow in dataTable.Rows)
                {
                    var tableName = dataRow.Field<string>("TABLE_NAME");

                    // For Wiser tables, we don't want to copy customer data, so copy everything except data of certain entity types.
                    if (tableName!.EndsWith(WiserTableNames.WiserItem, StringComparison.OrdinalIgnoreCase))
                    {
                        await clientDatabaseConnection.ExecuteAsync($@"INSERT INTO `{newCustomer.Database.DatabaseName}`.`{tableName}` 
                                                                    SELECT * FROM `{currentCustomer.Database.DatabaseName}`.`{tableName}` 
                                                                    WHERE entity_type NOT IN ('{String.Join("','", entityTypesToSkipWhenSynchronisingEnvironments)}')");
                        continue;
                    }

                    if (tableName!.EndsWith(WiserTableNames.WiserItemDetail, StringComparison.OrdinalIgnoreCase))
                    {
                        var prefix = tableName.Replace(WiserTableNames.WiserItemDetail, "");
                        await clientDatabaseConnection.ExecuteAsync($@"INSERT INTO `{newCustomer.Database.DatabaseName}`.`{tableName}` 
                                                                    SELECT detail.* FROM `{currentCustomer.Database.DatabaseName}`.`{tableName}` AS detail
                                                                    JOIN `{currentCustomer.Database.DatabaseName}`.`{prefix}{WiserTableNames.WiserItem}` AS item ON item.id = detail.item_id AND item.entity_type NOT IN ({entityTypesString})");
                        continue;
                    }

                    if (tableName!.EndsWith(WiserTableNames.WiserItemFile, StringComparison.OrdinalIgnoreCase))
                    {
                        var prefix = tableName.Replace(WiserTableNames.WiserItemFile, "");
                        await clientDatabaseConnection.ExecuteAsync($@"INSERT INTO `{newCustomer.Database.DatabaseName}`.`{tableName}` 
                                                                    SELECT file.* FROM `{currentCustomer.Database.DatabaseName}`.`{tableName}` AS file
                                                                    JOIN `{currentCustomer.Database.DatabaseName}`.`{prefix}{WiserTableNames.WiserItem}` AS item ON item.id = file.item_id AND item.entity_type NOT IN ({entityTypesString})");
                        continue;
                    }

                    // Don't copy data from certain tables, such as log and archive tables.
                    if (tablesToAlwaysLeaveEmpty.Any(t => String.Equals(t, tableName, StringComparison.OrdinalIgnoreCase))
                        || tableName!.StartsWith("log_", StringComparison.OrdinalIgnoreCase)
                        || tableName.EndsWith("_log", StringComparison.OrdinalIgnoreCase)
                        || tableName.EndsWith(WiserTableNames.ArchiveSuffix))
                    {
                        continue;
                    }

                    await clientDatabaseConnection.ExecuteAsync($"INSERT INTO `{newCustomer.Database.DatabaseName}`.`{tableName}` SELECT * FROM `{currentCustomer.Database.DatabaseName}`.`{tableName}`");
                }
                
                // Add triggers (and stored procedures) to database, after inserting all data, so that the wiser_history table will still be empty.
                // We use wiser_history to later synchronise all changes to production, so it needs to be empty before the user starts to make changes in the new environment.
                query = $@"SELECT 
                            TRIGGER_NAME,
                            EVENT_MANIPULATION,
                            EVENT_OBJECT_TABLE,
	                        ACTION_STATEMENT,
	                        ACTION_ORIENTATION,
	                        ACTION_TIMING
                        FROM information_schema.TRIGGERS
                        WHERE TRIGGER_SCHEMA = ?currentSchema
                        AND EVENT_OBJECT_TABLE NOT LIKE '\_%'";
                dataTable = await clientDatabaseConnection.GetAsync(query);
                
                var createdStoredProceduresQuery = await ResourceHelpers.ReadTextResourceFromAssemblyAsync("Api.Core.Queries.WiserInstallation.StoredProcedures.sql");
                await using (var mysqlConnection = new MySqlConnection(wiserCustomersService.GenerateConnectionStringFromCustomer(newCustomer)))
                {
                    await mysqlConnection.OpenAsync();
                    await using (var command = mysqlConnection.CreateCommand())
                    {
                        foreach (DataRow dataRow in dataTable.Rows)
                        {
                            query = $@"CREATE TRIGGER `{dataRow.Field<string>("TRIGGER_NAME")}` {dataRow.Field<string>("ACTION_TIMING")} {dataRow.Field<string>("EVENT_MANIPULATION")} ON `{newCustomer.Database.DatabaseName.ToMySqlSafeValue(false)}`.`{dataRow.Field<string>("EVENT_OBJECT_TABLE")}` FOR EACH {dataRow.Field<string>("ACTION_ORIENTATION")} {dataRow.Field<string>("ACTION_STATEMENT")}";
                            
                            command.CommandText = query;
                            await command.ExecuteNonQueryAsync();
                        }

                        command.CommandText = createdStoredProceduresQuery;
                        await command.ExecuteNonQueryAsync();
                    }
                }

                // Remove passwords from response.
                newCustomer.Database.Password = null;

                await clientDatabaseConnection.CommitTransactionAsync();
                await wiserDatabaseConnection.CommitTransactionAsync();

                return new ServiceResult<CustomerModel>(newCustomer);
            }
            catch
            {
                await wiserDatabaseConnection.RollbackTransactionAsync();
                await clientDatabaseConnection.RollbackTransactionAsync();

                // Drop the new database it something went wrong, so that we can start over again later.
                // We can safely do this, because this method will return an error if the database already exists,
                // so we can be sure that this database was created here and we can drop it again it something went wrong.
                if (await databaseHelpersService.DatabaseExistsAsync(databaseName))
                {
                    await databaseHelpersService.DropDatabaseAsync(databaseName);
                }

                throw;
            }
        }

        private async Task HandleMergeBranchActionAsync(DataRow dataRowWithSettings, IDatabaseConnection databaseConnection, IDatabaseHelpersService databaseHelpersService)
        {
            var result = new JObject(new JProperty("SuccessfulChanges", 0), new JProperty("Errors", new JArray()));

            var selectedEnvironmentCustomer = (await wiserCustomersService.GetSingleAsync(settings.Id, true)).ModelObject;
            var productionCustomer = (await wiserCustomersService.GetSingleAsync(currentCustomer.CustomerId, true)).ModelObject;
            var productionConnection = new MySqlConnection(wiserCustomersService.GenerateConnectionStringFromCustomer(productionCustomer));
            var branchConnection = new MySqlConnection(wiserCustomersService.GenerateConnectionStringFromCustomer(selectedEnvironmentCustomer));
            await productionConnection.OpenAsync();
            await branchConnection.OpenAsync();
            var productionTransaction = await productionConnection.BeginTransactionAsync();
            var branchTransaction = await branchConnection.BeginTransactionAsync();
            var sqlParameters = new Dictionary<string, object>();
            
            try
            {
                // Create the wiser_id_mappings table, in the selected branch, if it doesn't exist yet.
                // We need it to map IDs of the selected environment to IDs of the production environment, because they are not always the same.
                await databaseHelpersService.CheckAndUpdateTablesAsync(new List<string> {WiserTableNames.WiserIdMappings}, selectedEnvironmentCustomer.Database.DatabaseName);
                
                // Get all history since last synchronisation.
                var dataTable = new DataTable();
                await using (var environmentCommand = branchConnection.CreateCommand())
                {
                    environmentCommand.CommandText = $"SELECT * FROM `{WiserTableNames.WiserHistory}` ORDER BY id ASC";
                    using var environmentAdapter = new MySqlDataAdapter(environmentCommand);
                    await environmentAdapter.FillAsync(dataTable);
                }

                // Srt saveHistory and username parameters for all queries.
                var queryPrefix = @"SET @saveHistory = TRUE; SET @_username = ?username; ";
                var username = $"{IdentityHelpers.GetUserName(identity, true)} (Sync from {selectedEnvironmentCustomer.Name})";
                if (username.Length > 50)
                {
                    username = $"{IdentityHelpers.GetUserName(identity)} (Sync from {selectedEnvironmentCustomer.Name})";

                    if (username.Length > 50)
                    {
                        username = IdentityHelpers.GetUserName(identity);
                    }
                }

                sqlParameters.Add("username", username);

                // We need to lock all tables we're going to use, to make sure no other changes can be done while we're busy synchronising.
                var tablesToLock = new List<string> { WiserTableNames.WiserHistory };
                foreach (DataRow dataRow in dataTable.Rows)
                {
                    var tableName = dataRow.Field<string>("tablename");
                    if (String.IsNullOrWhiteSpace(tableName) || tablesToLock.Contains(tableName))
                    {
                        continue;
                    }
                    
                    tablesToLock.Add(tableName);
                    if (WiserTableNames.TablesWithArchive.Any(table => tableName.EndsWith(table, StringComparison.OrdinalIgnoreCase)))
                    {
                        tablesToLock.Add($"{tableName}{WiserTableNames.ArchiveSuffix}");
                    }

                    // If we have a table that has an ID from wiser_item, then always lock wiser_item as well, because we will read from it later.
                    var originalItemId = Convert.ToUInt64(dataRow["item_id"]);
                    var (tablePrefix, isWiserItemChange) = GetTablePrefix(tableName, originalItemId);
                    var wiserItemTableName = $"{tablePrefix}{WiserTableNames.WiserItem}";
                    if (isWiserItemChange && originalItemId > 0 && !tablesToLock.Contains(wiserItemTableName))
                    {
                        tablesToLock.Add(wiserItemTableName);
                        tablesToLock.Add($"{wiserItemTableName}{WiserTableNames.ArchiveSuffix}");
                    }
                }

                // Add tables from wiser_id_mappings to tables to lock.
                await using (var command = branchConnection.CreateCommand())
                {
                    command.CommandText = $@"SELECT DISTINCT table_name FROM `{WiserTableNames.WiserIdMappings}`";
                    var mappingDataTable = new DataTable();
                    using var adapter = new MySqlDataAdapter(command);
                    await adapter.FillAsync(mappingDataTable);
                    foreach (DataRow dataRow in mappingDataTable.Rows)
                    {
                        var tableName = dataRow.Field<string>("table_name");
                        if (String.IsNullOrWhiteSpace(tableName) || tablesToLock.Contains(tableName))
                        {
                            continue;
                        }
                        
                        tablesToLock.Add(tableName);
                        
                        if (WiserTableNames.TablesWithArchive.Any(table => tableName.EndsWith(table, StringComparison.OrdinalIgnoreCase)))
                        {
                            tablesToLock.Add($"{tableName}{WiserTableNames.ArchiveSuffix}");
                        }
                    }
                }

                // Lock the tables we're going to use, to be sure that other processes don't mess up our synchronisation.
                await using (var productionCommand = productionConnection.CreateCommand())
                {
                    productionCommand.CommandText = $"LOCK TABLES {String.Join(", ", tablesToLock.Select(table => $"{table} WRITE"))}";
                    await productionCommand.ExecuteNonQueryAsync();
                }
                await using (var environmentCommand = branchConnection.CreateCommand())
                {
                    environmentCommand.CommandText = $"LOCK TABLES {WiserTableNames.WiserIdMappings} WRITE, {String.Join(", ", tablesToLock.Select(table => $"{table} WRITE"))}";
                    await environmentCommand.ExecuteNonQueryAsync();
                }

                // This is to cache the entity types for all changed items, so that we don't have to execute a query for every changed detail of the same item.
                var entityTypes = new Dictionary<ulong, string>();

                // This is to map one item ID to another. This is needed because when someone creates a new item in the other environment, that ID could already exist in the production environment.
                // So we need to map the ID that is saved in wiser_history to the new ID of the item that we create in the production environment.
                var idMapping = new Dictionary<string, Dictionary<ulong, ulong>>();
                await using (var environmentCommand = branchConnection.CreateCommand())
                {
                    environmentCommand.CommandText = $@"SELECT table_name, our_id, production_id FROM `{WiserTableNames.WiserIdMappings}`";
                    using var environmentAdapter = new MySqlDataAdapter(environmentCommand);
                    
                    var idMappingDatatable = new DataTable();
                    await environmentAdapter.FillAsync(idMappingDatatable);
                    foreach (DataRow dataRow in idMappingDatatable.Rows)
                    {
                        var tableName = dataRow.Field<string>("table_name");
                        var ourId = dataRow.Field<ulong>("our_id");
                        var productionId = dataRow.Field<ulong>("production_id");

                        if (!idMapping.ContainsKey(tableName!))
                        {
                            idMapping.Add(tableName, new Dictionary<ulong, ulong>());
                        }

                        idMapping[tableName][ourId] = productionId;
                    }
                }

                // Start synchronising all history items one by one.
                var historyItemsSynchronised = new List<ulong>();
                foreach (DataRow dataRow in dataTable.Rows)
                {
                    var historyId = Convert.ToUInt64(dataRow["id"]);
                    var action = dataRow.Field<string>("action").ToUpperInvariant();
                    var tableName = dataRow.Field<string>("tablename") ?? "";
                    var originalObjectId = Convert.ToUInt64(dataRow["item_id"]);
                    var objectId = originalObjectId;
                    var originalItemId = originalObjectId;
                    var itemId = originalObjectId;
                    var field = dataRow.Field<string>("field");
                    var oldValue = dataRow.Field<string>("oldvalue");
                    var newValue = dataRow.Field<string>("newvalue");
                    var languageCode = dataRow.Field<string>("language_code") ?? "";
                    var groupName = dataRow.Field<string>("groupname") ?? "";
                    ulong? linkId = null;
                    ulong? originalLinkId;
                    ulong? originalFileId = null;
                    ulong? fileId = null;
                    var entityType = "";
                    int? linkType = null;
                    int? linkOrdering = null;

                    // Variables for item link changes.
                    var destinationItemId = 0UL;
                    ulong? oldItemId = null;
                    ulong? oldDestinationItemId = null;

                    try
                    {
                        // Make sure we have the correct item ID. For some actions, the item id is saved in a different column.
                        switch (action)
                        {
                            case "REMOVE_LINK":
                            {
                                destinationItemId = itemId;
                                itemId = Convert.ToUInt64(oldValue);
                                originalItemId = itemId;
                                linkType = Int32.Parse(field);

                                break;
                            }
                            case "UPDATE_ITEMLINKDETAIL":
                            case "CHANGE_LINK":
                            {
                                linkId = itemId;
                                originalLinkId = linkId;

                                // When a link has been changed, it's possible that the ID of one of the items is changed.
                                // It's also possible that this is a new link that the production database didn't have yet (and so the ID of the link will most likely be different).
                                // Therefor we need to find the original item and destination IDs, so that we can use those to update the link in the production database.
                                sqlParameters["linkId"] = itemId;
                                
                                await using (var branchCommand = branchConnection.CreateCommand())
                                {
                                    AddParametersToCommand(sqlParameters, branchCommand);
                                    
                                    // Replace wiser_itemlinkdetail with wiser_itemlink because we need to get the source and destination from [prefix]wiser_itemlink, even if this is an update for [prefix]wiser_itemlinkdetail.
                                    branchCommand.CommandText = $@"SELECT type, item_id, destination_item_id FROM `{tableName.ReplaceCaseInsensitive(WiserTableNames.WiserItemLinkDetail, WiserTableNames.WiserItemLink)}` WHERE id = ?linkId";
                                    var linkDataTable = new DataTable();
                                    using var branchAdapter = new MySqlDataAdapter(branchCommand);
                                    await branchAdapter.FillAsync(linkDataTable);
                                    if (linkDataTable.Rows.Count == 0)
                                    {
                                        branchCommand.CommandText = $@"SELECT type, item_id, destination_item_id FROM `{tableName.ReplaceCaseInsensitive(WiserTableNames.WiserItemLinkDetail, WiserTableNames.WiserItemLink)}{WiserTableNames.ArchiveSuffix}` WHERE id = ?linkId";
                                        await branchAdapter.FillAsync(linkDataTable);
                                        if (linkDataTable.Rows.Count == 0)
                                        {
                                            // This should never happen, but just in case the ID somehow doesn't exist anymore, log a warning and continue on to the next item.
                                            logger.LogWarning($"Could not find link with id '{itemId}' in database '{selectedEnvironmentCustomer.Database.DatabaseName}'. Skipping this history record in synchronisation to production.");
                                            continue;
                                        }
                                    }

                                    itemId = Convert.ToUInt64(linkDataTable.Rows[0]["item_id"]);
                                    originalItemId = itemId;
                                    destinationItemId = Convert.ToUInt64(linkDataTable.Rows[0]["destination_item_id"]);
                                    linkType = Convert.ToInt32(linkDataTable.Rows[0]["type"]);
                                }

                                switch (field)
                                {
                                    case "destination_item_id":
                                        oldDestinationItemId = Convert.ToUInt64(oldValue);
                                        destinationItemId = Convert.ToUInt64(newValue);
                                        oldItemId = itemId;
                                        break;
                                    case "item_id":
                                        oldItemId = Convert.ToUInt64(oldValue);
                                        itemId = Convert.ToUInt64(newValue);
                                        originalItemId = itemId;
                                        oldDestinationItemId = destinationItemId;
                                        break;
                                }

                                break;
                            }
                            case "ADD_LINK":
                            {
                                destinationItemId = itemId;
                                itemId = Convert.ToUInt64(newValue);
                                originalItemId = itemId;

                                var split = field.Split(',');
                                linkType = Int32.Parse(split[0]);
                                linkOrdering = split.Length > 1 ? Int32.Parse(split[1]) : 0;

                                break;
                            }
                            case "ADD_FILE":
                            case "DELETE_FILE":
                            {
                                fileId = itemId;
                                originalFileId = fileId;
                                itemId = String.Equals(oldValue, "item_id", StringComparison.OrdinalIgnoreCase) ? UInt64.Parse(newValue) : 0;
                                originalItemId = itemId;
                                linkId = String.Equals(oldValue, "itemlink_id", StringComparison.OrdinalIgnoreCase) ? UInt64.Parse(newValue) : 0;
                                originalLinkId = linkId;

                                break;
                            }
                            case "UPDATE_FILE":
                            {
                                fileId = itemId;
                                originalFileId = fileId;
                                sqlParameters["fileId"] = fileId;

                                await using var branchCommand = branchConnection.CreateCommand();
                                AddParametersToCommand(sqlParameters, branchCommand);
                                branchCommand.CommandText = $@"SELECT item_id, itemlink_id FROM `{tableName}` WHERE id = ?fileId
UNION ALL
SELECT item_id, itemlink_id FROM `{tableName}{WiserTableNames.ArchiveSuffix}` WHERE id = ?fileId
LIMIT 1";
                                var fileDataTable = new DataTable();
                                using var adapter = new MySqlDataAdapter(branchCommand);
                                await adapter.FillAsync(fileDataTable);
                                itemId = fileDataTable.Rows[0].Field<ulong>("item_id");
                                originalItemId = itemId;
                                linkId = fileDataTable.Rows[0].Field<ulong>("itemlink_id");
                                originalLinkId = linkId;

                                break;
                            }
                            case "DELETE_ITEM":
                            case "UNDELETE_ITEM":
                            {
                                entityType = field;
                                break;
                            }
                        }

                        // Did we map the item ID to something else? Then use that new ID.
                        var originalDestinationItemId = destinationItemId;
                        itemId = GetMappedId(tableName, idMapping, itemId).Value;
                        destinationItemId = GetMappedId(tableName, idMapping, destinationItemId).Value;
                        oldItemId = GetMappedId(tableName, idMapping, oldItemId);
                        oldDestinationItemId = GetMappedId(tableName, idMapping, oldDestinationItemId);
                        linkId = GetMappedId(tableName, idMapping, linkId);
                        fileId = GetMappedId(tableName, idMapping, fileId);
                        objectId = GetMappedId(tableName, idMapping, objectId) ?? 0;

                        var isWiserItemChange = true;

                        // Figure out the entity type of the item that was updated, so that we can check if we need to do anything with it.
                        // We don't want to synchronise certain entity types, such as users, relations and baskets.
                        if (entityTypes.ContainsKey(itemId))
                        {
                            entityType = entityTypes[itemId];
                        }
                        else if (String.IsNullOrWhiteSpace(entityType))
                        {
                            if (action == "ADD_LINK")
                            {
                                var linkData = await GetEntityTypesOfLinkAsync(itemId, destinationItemId, linkType.Value, branchConnection);
                                if (linkData.HasValue)
                                {
                                    entityType = linkData.Value.SourceType;
                                    entityTypes.Add(itemId, entityType);
                                }
                            }
                            else
                            {
                                // Check if this item is saved in a dedicated table with a certain prefix.
                                var (tablePrefix, wiserItemChange) = GetTablePrefix(tableName, originalItemId);
                                isWiserItemChange = wiserItemChange;

                                if (isWiserItemChange && originalItemId > 0)
                                {
                                    sqlParameters["itemId"] = originalItemId;
                                    var itemDataTable = new DataTable();
                                    await using var environmentCommand = branchConnection.CreateCommand();
                                    AddParametersToCommand(sqlParameters, environmentCommand);
                                    environmentCommand.CommandText = $"SELECT entity_type FROM `{tablePrefix}{WiserTableNames.WiserItem}` WHERE id = ?itemId";
                                    using var environmentAdapter = new MySqlDataAdapter(environmentCommand);
                                    await environmentAdapter.FillAsync(itemDataTable);
                                    if (itemDataTable.Rows.Count == 0)
                                    {
                                        // If item doesn't exist, check the archive table, it might have been deleted.
                                        environmentCommand.CommandText = $"SELECT entity_type FROM `{tablePrefix}{WiserTableNames.WiserItem}{WiserTableNames.ArchiveSuffix}` WHERE id = ?itemId";
                                        await environmentAdapter.FillAsync(itemDataTable);
                                        if (itemDataTable.Rows.Count == 0)
                                        {
                                            logger.LogWarning($"Could not find item with ID '{originalItemId}', so skipping it...");
                                            result.Errors.Add($"Item met ID '{originalItemId}' kon niet gevonden worden.");
                                            continue;
                                        }
                                    }

                                    entityType = itemDataTable.Rows[0].Field<string>("entity_type");
                                    entityTypes.Add(itemId, entityType);
                                }
                            }
                        }

                        // We don't want to synchronise certain entity types, such as users, relations and baskets.
                        if (isWiserItemChange && entityTypesToSkipWhenSynchronisingEnvironments.Any(x => String.Equals(x, entityType, StringComparison.OrdinalIgnoreCase)))
                        {
                            continue;
                        }

                        // Update the item in the production environment.
                        switch (action)
                        {
                            case "CREATE_ITEM":
                            {
                                var newItemId = await GenerateNewId(tableName, productionConnection, branchConnection);
                                sqlParameters["newId"] = newItemId;
                                
                                await using var productionCommand = productionConnection.CreateCommand();
                                AddParametersToCommand(sqlParameters, productionCommand);
                                productionCommand.CommandText = $@"{queryPrefix}
INSERT INTO `{tableName}` (id, entity_type) VALUES (?newId, '')";
                                await productionCommand.ExecuteNonQueryAsync();

                                // Map the item ID from wiser_history to the ID of the newly created item, locally and in database.
                                await AddIdMapping(idMapping, tableName, originalItemId, newItemId, branchConnection);

                                break;
                            }
                            case "UPDATE_ITEM" when tableName.EndsWith(WiserTableNames.WiserItemDetail, StringComparison.OrdinalIgnoreCase):
                            {
                                sqlParameters["itemId"] = itemId;
                                sqlParameters["key"] = field;
                                sqlParameters["languageCode"] = languageCode;
                                sqlParameters["groupName"] = groupName;

                                await using var productionCommand = productionConnection.CreateCommand();
                                AddParametersToCommand(sqlParameters, productionCommand);
                                productionCommand.CommandText = queryPrefix;
                                if (String.IsNullOrWhiteSpace(newValue))
                                {
                                    productionCommand.CommandText += $@"DELETE FROM `{tableName}`
WHERE item_id = ?itemId
AND `key` = ?key
AND language_code = ?languageCode
AND groupname = ?groupName";
                                }
                                else
                                {
                                    var useLongValue = newValue.Length > 1000;
                                    sqlParameters["value"] = useLongValue ? "" : newValue;
                                    sqlParameters["longValue"] = useLongValue ? newValue : "";

                                    AddParametersToCommand(sqlParameters, productionCommand);
                                    productionCommand.CommandText += $@"INSERT INTO `{tableName}` (language_code, item_id, groupname, `key`, value, long_value)
VALUES (?languageCode, ?itemId, ?groupName, ?key, ?value, ?longValue)
ON DUPLICATE KEY UPDATE groupname = VALUES(groupname), value = VALUES(value), long_value = VALUES(long_value)";
                                }

                                await productionCommand.ExecuteNonQueryAsync();

                                break;
                            }
                            case "UPDATE_ITEM" when tableName.EndsWith(WiserTableNames.WiserItem, StringComparison.OrdinalIgnoreCase):
                            {
                                sqlParameters["itemId"] = itemId;
                                sqlParameters["newValue"] = newValue;
                                
                                await using var productionCommand = productionConnection.CreateCommand();
                                AddParametersToCommand(sqlParameters, productionCommand);
                                productionCommand.CommandText = $@"{queryPrefix}
UPDATE `{tableName}` 
SET `{field.ToMySqlSafeValue(false)}` = ?newValue
WHERE id = ?itemId";
                                await productionCommand.ExecuteNonQueryAsync();

                                break;
                            }
                            case "DELETE_ITEM":
                            {
                                sqlParameters["itemId"] = itemId;
                                sqlParameters["entityType"] = entityType;
                                
                                await using var productionCommand = productionConnection.CreateCommand();
                                AddParametersToCommand(sqlParameters, productionCommand);
                                productionCommand.CommandText = $@"{queryPrefix} CALL DeleteWiser2Item(?itemId, ?entityType);";
                                await productionCommand.ExecuteNonQueryAsync();

                                break;
                            }
                            case "UNDELETE_ITEM":
                            {
                                throw new NotImplementedException("Undelete items not supported yet.");
                            }
                            case "ADD_LINK":
                            {
                                sqlParameters["itemId"] = itemId;
                                sqlParameters["originalItemId"] = originalItemId;
                                sqlParameters["ordering"] = linkOrdering;
                                sqlParameters["destinationItemId"] = destinationItemId;
                                sqlParameters["originalDestinationItemId"] = originalDestinationItemId;
                                sqlParameters["type"] = linkType;

                                // Get the original link ID, so we can map it to the new one.
                                await using (var environmentCommand = branchConnection.CreateCommand())
                                {
                                    AddParametersToCommand(sqlParameters, environmentCommand);
                                    environmentCommand.CommandText = $@"SELECT id FROM `{tableName}` WHERE item_id = ?originalItemId AND destination_item_id = ?originalDestinationItemId AND type = ?type";
                                    var getLinkIdDataTable = new DataTable();
                                    using var environmentAdapter = new MySqlDataAdapter(environmentCommand);
                                    await environmentAdapter.FillAsync(getLinkIdDataTable);
                                    if (getLinkIdDataTable.Rows.Count == 0)
                                    {
                                        logger.LogWarning($"Could not find link ID with itemId = {originalItemId}, destinationItemId = {originalDestinationItemId} and type = {linkType}");
                                        result.Errors.Add($"Kan koppeling-ID met itemId = {originalItemId}, destinationItemId = {originalDestinationItemId} and type = {linkType} niet vinden");
                                        continue;
                                    }

                                    originalLinkId = Convert.ToUInt64(getLinkIdDataTable.Rows[0]["id"]);
                                    linkId = await GenerateNewId(tableName, productionConnection, branchConnection);
                                }

                                sqlParameters["newId"] = linkId;
                                await using var productionCommand = productionConnection.CreateCommand();
                                AddParametersToCommand(sqlParameters, productionCommand);
                                productionCommand.CommandText = $@"{queryPrefix}
INSERT IGNORE INTO `{tableName}` (id, item_id, destination_item_id, ordering, type)
VALUES (?newId, ?itemId, ?destinationItemId, ?ordering, ?type);";
                                await productionCommand.ExecuteNonQueryAsync();

                                // Map the item ID from wiser_history to the ID of the newly created item, locally and in database.
                                await AddIdMapping(idMapping, tableName, originalLinkId.Value, linkId.Value, branchConnection);

                                break;
                            }
                            case "CHANGE_LINK":
                            {
                                sqlParameters["oldItemId"] = oldItemId;
                                sqlParameters["oldDestinationItemId"] = oldDestinationItemId;
                                sqlParameters["newValue"] = newValue;
                                sqlParameters["type"] = linkType.Value;
                                
                                await using var productionCommand = productionConnection.CreateCommand();
                                AddParametersToCommand(sqlParameters, productionCommand);
                                productionCommand.CommandText = $@"{queryPrefix}
UPDATE `{tableName}` 
SET `{field.ToMySqlSafeValue(false)}` = ?newValue
WHERE item_id = ?oldItemId
AND destination_item_id = ?oldDestinationItemId
AND type = ?type";
                                await productionCommand.ExecuteNonQueryAsync();
                                break;
                            }
                            case "REMOVE_LINK":
                            {
                                sqlParameters["oldItemId"] = oldItemId;
                                sqlParameters["oldDestinationItemId"] =  oldDestinationItemId;
                                sqlParameters["type"] = linkType.Value;
                                
                                await using var productionCommand = productionConnection.CreateCommand();
                                AddParametersToCommand(sqlParameters, productionCommand);
                                productionCommand.CommandText = $@"{queryPrefix}
DELETE FROM `{tableName}`
WHERE item_id = ?oldItemId
AND destination_item_id = ?oldDestinationItemId
AND type = ?type";
                                await productionCommand.ExecuteNonQueryAsync();
                                break;
                            }
                            case "UPDATE_ITEMLINKDETAIL":
                            {
                                sqlParameters["linkId"] = linkId;
                                sqlParameters["key"] = field;
                                sqlParameters["languageCode"] = languageCode;
                                sqlParameters["groupName"] = groupName;

                                await using var productionCommand = productionConnection.CreateCommand();
                                productionCommand.CommandText = queryPrefix;
                                if (String.IsNullOrWhiteSpace(newValue))
                                {
                                    productionCommand.CommandText += $@"DELETE FROM `{tableName}`
WHERE itemlink_id = ?linkId
AND `key` = ?key
AND language_code = ?languageCode
AND groupname = ?groupName";
                                }
                                else
                                {
                                    var useLongValue = newValue.Length > 1000;
                                    sqlParameters["value"] = useLongValue ? "" : newValue;
                                    sqlParameters["longValue"] = useLongValue ? newValue : "";

                                    productionCommand.CommandText += $@"INSERT INTO `{tableName}` (language_code, itemlink_id, groupname, `key`, value, long_value)
VALUES (?languageCode, ?linkId, ?groupName, ?key, ?value, ?longValue)
ON DUPLICATE KEY UPDATE groupname = VALUES(groupname), value = VALUES(value), long_value = VALUES(long_value)";
                                }

                                AddParametersToCommand(sqlParameters, productionCommand);
                                await productionCommand.ExecuteNonQueryAsync();

                                break;
                            }
                            case "ADD_FILE":
                            {
                                // oldValue contains either "item_id" or "itemlink_id", to indicate which of these columns is used for the ID that is saved in newValue.
                                var newFileId = await GenerateNewId(tableName, productionConnection, branchConnection);
                                sqlParameters["fileItemId"] = newValue;
                                sqlParameters["newId"] = newFileId;

                                await using var productionCommand = productionConnection.CreateCommand();
                                AddParametersToCommand(sqlParameters, productionCommand);
                                productionCommand.CommandText = $@"{queryPrefix}
INSERT INTO `{tableName}` (id, `{oldValue.ToMySqlSafeValue(false)}`) 
VALUES (?newId, ?fileItemId)";
                                await productionCommand.ExecuteReaderAsync();

                                // Map the item ID from wiser_history to the ID of the newly created item, locally and in database.
                                await AddIdMapping(idMapping, tableName, originalObjectId, newFileId, branchConnection);

                                break;
                            }
                            case "UPDATE_FILE":
                            {
                                sqlParameters["fileId"] = fileId;
                                sqlParameters["originalFileId"] = originalFileId;

                                if (String.Equals(field, "content_length", StringComparison.OrdinalIgnoreCase))
                                {
                                    // If the content length has been updated, we need to get the actual content from wiser_itemfile.
                                    // We don't save the content bytes in wiser_history, because then the history table would become too huge.
                                    byte[] file = null;
                                    await using (var environmentCommand = branchConnection.CreateCommand())
                                    {
                                        AddParametersToCommand(sqlParameters, environmentCommand);
                                        environmentCommand.CommandText = $"SELECT content FROM `{tableName}` WHERE id = ?originalFileId";
                                        await using var productionReader = await environmentCommand.ExecuteReaderAsync();
                                        if (await productionReader.ReadAsync())
                                        {
                                            file = (byte[]) productionReader.GetValue(0);
                                        }
                                    }

                                    sqlParameters["contents"] = file;
                                    
                                    await using var productionCommand = productionConnection.CreateCommand();
                                    AddParametersToCommand(sqlParameters, productionCommand);
                                    productionCommand.CommandText = $@"{queryPrefix}
UPDATE `{tableName}`
SET content = ?content
WHERE id = ?fileId";
                                    await productionCommand.ExecuteNonQueryAsync();
                                }
                                else
                                {
                                    sqlParameters["newValue"] = newValue;

                                    await using var productionCommand = productionConnection.CreateCommand();
                                    AddParametersToCommand(sqlParameters, productionCommand);
                                    productionCommand.CommandText = $@"{queryPrefix}
UPDATE `{tableName}` 
SET `{field.ToMySqlSafeValue(false)}` = ?newValue
WHERE id = ?fileId";
                                    await productionCommand.ExecuteNonQueryAsync();
                                }

                                break;
                            }
                            case "DELETE_FILE":
                            {
                                sqlParameters["itemId"] = newValue;

                                await using var productionCommand = productionConnection.CreateCommand();
                                AddParametersToCommand(sqlParameters, productionCommand);
                                productionCommand.CommandText = $@"{queryPrefix}
DELETE FROM `{tableName}`
WHERE `{oldValue.ToMySqlSafeValue(false)}` = ?itemId";
                                await productionCommand.ExecuteReaderAsync();

                                break;
                            }
                            case "INSERT_ENTITY":
                            case "INSERT_ENTITYPROPERTY":
                            case "INSERT_QUERY":
                            case "INSERT_MODULE":
                            case "INSERT_DATA_SELECTOR":
                            case "INSERT_PERMISSION":
                            case "INSERT_USER_ROLE":
                            case "INSERT_FIELD_TEMPLATE":
                            case "INSERT_LINK_SETTING":
                            case "INSERT_API_CONNECTION":
                            {
                                var newEntityId = await GenerateNewId(tableName, productionConnection, branchConnection);
                                sqlParameters["newId"] = newEntityId;
                                
                                await using var productionCommand = productionConnection.CreateCommand();
                                AddParametersToCommand(sqlParameters, productionCommand);

                                if (tableName.Equals(WiserTableNames.WiserEntity, StringComparison.OrdinalIgnoreCase))
                                {
                                    productionCommand.CommandText = $@"{queryPrefix}
INSERT INTO `{tableName}` (id, `name`) 
VALUES (?newId, '')";
                                }
                                else
                                {
                                    productionCommand.CommandText = $@"{queryPrefix}
INSERT INTO `{tableName}` (id) 
VALUES (?newId)";
                                }

                                await productionCommand.ExecuteNonQueryAsync();

                                // Map the item ID from wiser_history to the ID of the newly created item, locally and in database.
                                await AddIdMapping(idMapping, tableName, originalObjectId, newEntityId, branchConnection);

                                break;
                            }
                            case "UPDATE_ENTITY":
                            case "UPDATE_ENTITYPROPERTY":
                            case "UPDATE_QUERY":
                            case "UPDATE_DATA_SELECTOR":
                            case "UPDATE_MODULE":
                            case "UPDATE_PERMISSION":
                            case "UPDATE_USER_ROLE":
                            case "UPDATE_FIELD_TEMPLATE":
                            case "UPDATE_LINK_SETTING":
                            case "UPDATE_API_CONNECTION":
                            {
                                sqlParameters["id"] = objectId;
                                sqlParameters["newValue"] = newValue;

                                await using var productionCommand = productionConnection.CreateCommand();
                                AddParametersToCommand(sqlParameters, productionCommand);
                                productionCommand.CommandText = $@"{queryPrefix}
UPDATE `{tableName}` 
SET `{field.ToMySqlSafeValue(false)}` = ?newValue
WHERE id = ?id";
                                await productionCommand.ExecuteNonQueryAsync();

                                break;
                            }
                            case "DELETE_ENTITY":
                            case "DELETE_ENTITYPROPERTY":
                            case "DELETE_QUERY":
                            case "DELETE_DATA_SELECTOR":
                            case "DELETE_MODULE":
                            case "DELETE_PERMISSION":
                            case "DELETE_USER_ROLE":
                            case "DELETE_FIELD_TEMPLATE":
                            case "DELETE_LINK_SETTING":
                            case "DELETE_API_CONNECTION":
                            {
                                sqlParameters["id"] = objectId;

                                await using var productionCommand = productionConnection.CreateCommand();
                                AddParametersToCommand(sqlParameters, productionCommand);
                                productionCommand.CommandText = $@"{queryPrefix}
DELETE FROM `{tableName}`
WHERE `id` = ?id";
                                await productionCommand.ExecuteNonQueryAsync();

                                break;
                            }
                            default:
                                throw new ArgumentOutOfRangeException(nameof(action), action, $"Unsupported action for history synchronisation: '{action}'");
                        }

                        result.SuccessfulChanges++;
                        historyItemsSynchronised.Add(historyId);
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(exception, $"An error occurred while trying to synchronise history ID '{historyId}' from '{selectedEnvironmentCustomer.Database.DatabaseName}' to '{productionCustomer.Database.DatabaseName}'");
                        result.Errors.Add($"Het is niet gelukt om de wijziging '{action}' voor item '{originalItemId}' over te zetten. De fout was: {exception.Message}");
                    }
                }

                try
                {
                    // Clear wiser_history in the selected environment, so that next time we can just sync all changes again.
                    if (historyItemsSynchronised.Any())
                    {
                        await using var environmentCommand = branchConnection.CreateCommand();
                        environmentCommand.CommandText = $"DELETE FROM `{WiserTableNames.WiserHistory}` WHERE id IN ({String.Join(",", historyItemsSynchronised)})";
                        await environmentCommand.ExecuteNonQueryAsync();
                    }

                    await EqualizeMappedIds(branchConnection);
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, $"An error occurred while trying to clean up after synchronising from '{selectedEnvironmentCustomer.Database.DatabaseName}' to '{productionCustomer.Database.DatabaseName}'");
                    result.Errors.Add($"Er is iets fout gegaan tijdens het opruimen na de synchronisatie. Het wordt aangeraden om deze omgeving niet meer te gebruiken voor synchroniseren naar productie, anders kunnen dingen dubbel gescynchroniseerd worden. U kunt wel een nieuwe omgeving maken en vanuit daar weer verder werken. De fout was: {exception.Message}");
                }

                // Always commit, so we keep our progress.
                await branchTransaction.CommitAsync();
                await productionTransaction.CommitAsync();
            }
            finally
            {
                // Make sure we always unlock all tables when we're done, no matter what happens.
                await using (var environmentCommand = branchConnection.CreateCommand())
                {
                    environmentCommand.CommandText = "UNLOCK TABLES";
                    await environmentCommand.ExecuteNonQueryAsync();
                }

                await using (var productionCommand = productionConnection.CreateCommand())
                {
                    productionCommand.CommandText = "UNLOCK TABLES";
                    await productionCommand.ExecuteNonQueryAsync();
                }

                // Dispose and cleanup.
                await branchTransaction.DisposeAsync();
                await productionTransaction.DisposeAsync();
                
                await branchConnection.CloseAsync();
                await productionConnection.CloseAsync();
                
                await branchConnection.DisposeAsync();
                await productionConnection.DisposeAsync();
            }

            //return new ServiceResult<SynchroniseChangesToProductionResultModel>(result);
        }
    }
}