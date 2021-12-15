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
using GeeksCoreLibrary.Core.Extensions;
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
        public async Task<Dictionary<string, SortedDictionary<int, string>>> Execute(ActionModel action, Dictionary<string, SortedDictionary<int, string>> usingResultSet)
        {
            var query = action as QueryModel;

            LogHelper.LogInformation(logger, LogScopes.RunStartAndStop, query.LogSettings, $"Executing query in time id: {query.TimeId}, order: {query.Order}");

            // If not using a result set execute the query as given.
            if (usingResultSet == null)
            {
                return await databaseConnection.ExecuteQuery(connectionString, query.Query);
            }

            var queryString = query.Query;
            var parameterKeys = new List<string>();

            // Find all the replacement keys and replace them with ?key.
            while (queryString.Contains("{") && queryString.Contains("}"))
            {
                var startIndex = queryString.IndexOf("{") + 1;
                var endIndex = queryString.IndexOf("}");

                var key = queryString.Substring(startIndex, endIndex - startIndex);
                queryString = queryString.Replace($"{{{key}}}", $"?{key}");
                parameterKeys.Add(key);
            }

            // Perform the query for each row in the result set that is being used.
            for (var i = 1; i <= usingResultSet.First().Value.Count; i++)
            {
                var queryStringWithValues = queryString;

                // Replacing the parameters with the required values of the current row. Replacing with database safe string to allow parameters in strings.
                foreach (var key in parameterKeys)
                {
                    queryStringWithValues = queryStringWithValues.Replace($"?{key}", usingResultSet[key][i].ToMySqlSafeValue());
                }

                await databaseConnection.ExecuteQuery(connectionString, queryStringWithValues);
            }

            // TODO combine result sets from each row.
            return new Dictionary<string, SortedDictionary<int, string>>();
        }
    }
}
