using System;
using System.Threading.Tasks;
using AutoImportServiceCore.Core.Enums;
using AutoImportServiceCore.Core.Helpers;
using AutoImportServiceCore.Core.Interfaces;
using AutoImportServiceCore.Core.Models;
using AutoImportServiceCore.Core.Services;
using AutoImportServiceCore.Modules.Queries.Interfaces;
using AutoImportServiceCore.Modules.Queries.Models;
using GeeksCoreLibrary.Core.DependencyInjection.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace AutoImportServiceCore.Modules.Queries.Services
{
    /// <summary>
    /// A service for a query action.
    /// </summary>
    public class QueriesService : IQueriesService, IActionsService, IScopedService
    {
        private readonly ILogService logService;
        private readonly ILogger<QueriesService> logger;
        private readonly AisDatabaseConnection databaseConnection;

        private string connectionString;

        /// <summary>
        /// Creates a new instance of <see cref="QueriesService"/>.
        /// </summary>
        /// <param name="logService">The service to use for logging.</param>
        /// <param name="logger"></param>
        /// <param name="databaseConnection"></param>
        public QueriesService(ILogService logService, ILogger<QueriesService> logger, AisDatabaseConnection databaseConnection)
        {
            this.logService = logService;
            this.logger = logger;
            this.databaseConnection = databaseConnection;
        }

        /// <inheritdoc />
        public async Task Initialize(ConfigurationModel configuration)
        {
            connectionString = configuration.ConnectionString;
        }

        /// <inheritdoc />
        public async Task<JObject> Execute(ActionModel action, JObject resultSets, string configurationServiceName)
        {
            var query = (QueryModel)action;

            logService.LogInformation(logger, LogScopes.RunStartAndStop, query.LogSettings, $"Executing query in time id: {query.TimeId}, order: {query.Order}", configurationServiceName, query.TimeId, query.Order);

            // If not using a result set execute the query as given.
            if (String.IsNullOrWhiteSpace(query.UseResultSet))
            {
                logService.LogInformation(logger, LogScopes.RunBody, query.LogSettings, $"Query: {query.Query}", configurationServiceName, query.TimeId, query.Order);
                return await databaseConnection.ExecuteQuery(connectionString, query.Query);
            }

            var keyParts = query.UseResultSet.Split('.');
            var remainingKey = keyParts.Length > 1 ? query.UseResultSet.Substring(keyParts[0].Length + 1) : "";
            var tuple = ReplacementHelper.PrepareText(query.Query, (JObject)resultSets[keyParts[0]], remainingKey, mySqlSafe: true);
            var queryString = tuple.Item1;
            var parameterKeys = tuple.Item2;

            logService.LogInformation(logger, LogScopes.RunBody, query.LogSettings, $"Query: {queryString}", configurationServiceName, query.TimeId, query.Order);

            // Perform the query when there are no parameters. Either no values are used from the using result set or all values have been combined and a single query is sufficient.
            if (parameterKeys.Count == 0)
            {
                return await databaseConnection.ExecuteQuery(connectionString, queryString);
            }

            var jArray = new JArray();

            // Perform the query for each row in the result set that is being used.
            var usingResultSet = ResultSetHelper.GetCorrectObject<JArray>(query.UseResultSet, 0, resultSets);
            for (var i = 0; i < usingResultSet.Count; i++)
            {
                var queryStringWithValues = ReplacementHelper.ReplaceText(queryString, i, parameterKeys, (JObject)usingResultSet[i], mySqlSafe: true);

                jArray.Add(await databaseConnection.ExecuteQuery(connectionString, queryStringWithValues));
            }

            return new JObject
            {
                {"Results", jArray}
            };
        }
    }
}
