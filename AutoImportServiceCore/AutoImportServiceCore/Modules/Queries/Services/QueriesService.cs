using System;
using System.Collections.Generic;
using System.Linq;
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

namespace AutoImportServiceCore.Modules.Queries.Services
{
    /// <summary>
    /// A service for a query action.
    /// </summary>
    public class QueriesService : IQueriesService, IActionsService, IScopedService
    {
        private readonly ILogger<QueriesService> logger;
        private readonly AisDatabaseConnection databaseConnection;

        private string connectionString;

        /// <summary>
        /// Creates a new instance of <see cref="QueriesService"/>.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="databaseConnection"></param>
        public QueriesService(ILogger<QueriesService> logger, AisDatabaseConnection databaseConnection)
        {
            this.logger = logger;
            this.databaseConnection = databaseConnection;
        }

        /// <inheritdoc />
        public async Task Initialize(ConfigurationModel configuration)
        {
            connectionString = configuration.ConnectionString;
        }

        /// <inheritdoc />
        public async Task<Dictionary<string, SortedDictionary<int, string>>> Execute(ActionModel action, Dictionary<string, Dictionary<string, SortedDictionary<int, string>>> resultSets)
        {
            var query = action as QueryModel;

            LogHelper.LogInformation(logger, LogScopes.RunStartAndStop, query.LogSettings, $"Executing query in time id: {query.TimeId}, order: {query.Order}");

            // If not using a result set execute the query as given.
            if (String.IsNullOrWhiteSpace(query.UseResultSet))
            {
                LogHelper.LogInformation(logger, LogScopes.RunBody, query.LogSettings, $"Query: {query.Query}");
                return await databaseConnection.ExecuteQuery(connectionString, query.Query);
            }

            var tuple = ReplacementHelper.PrepareText(query.Query, resultSets[query.UseResultSet], mySqlSafe: true);
            var queryString = tuple.Item1;
            var parameterKeys = tuple.Item2;

            LogHelper.LogInformation(logger, LogScopes.RunBody, query.LogSettings, $"Query: {queryString}");

            // Perform the query when there are no parameters. Either no values are used from the using result set or all values have been combined and a single query is sufficient.
            if (parameterKeys.Count == 0)
            {
                return await databaseConnection.ExecuteQuery(connectionString, queryString);
            }

            // Perform the query for each row in the result set that is being used.
            for (var i = 1; i <= resultSets[query.UseResultSet].First().Value.Count; i++)
            {
                var queryStringWithValues = ReplacementHelper.ReplaceText(queryString, i, parameterKeys, resultSets[query.UseResultSet], mySqlSafe: true);

                await databaseConnection.ExecuteQuery(connectionString, queryStringWithValues);
            }

            // TODO? combine result sets from each row.
            return new Dictionary<string, SortedDictionary<int, string>>();
        }
    }
}
