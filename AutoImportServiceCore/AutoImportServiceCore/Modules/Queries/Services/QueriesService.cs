using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoImportServiceCore.Core.Enums;
using AutoImportServiceCore.Core.Helpers;
using AutoImportServiceCore.Core.Interfaces;
using AutoImportServiceCore.Core.Models;
using AutoImportServiceCore.Core.Services;
using AutoImportServiceCore.Modules.Queries.Interfaces;
using AutoImportServiceCore.Modules.Queries.Models;
using GeeksCoreLibrary.Core.DependencyInjection.Interfaces;
using GeeksCoreLibrary.Core.Helpers;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

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
        public async Task<JObject> Execute(ActionModel action, JObject resultSets)
        {
            var query = (QueryModel)action;

            LogHelper.LogInformation(logger, LogScopes.RunStartAndStop, query.LogSettings, $"Executing query in time id: {query.TimeId}, order: {query.Order}");

            // If not using a result set execute the query as given.
            if (String.IsNullOrWhiteSpace(query.UseResultSet))
            {
                LogHelper.LogInformation(logger, LogScopes.RunBody, query.LogSettings, $"Query: {query.Query}");
                return await databaseConnection.ExecuteQuery(connectionString, query.Query);
            }

            var keyParts = query.UseResultSet.Split('.');
            var remainingKey = keyParts.Length > 1 ? query.UseResultSet.Substring(keyParts[0].Length + 1) : "";
            var tuple = ReplacementHelper.PrepareText(query.Query, (JObject)resultSets[keyParts[0]], remainingKey, insertValues: false);
            var queryString = tuple.Item1;
            var parameterKeys = tuple.Item2;
            var insertedParameters = tuple.Item3;

            LogHelper.LogInformation(logger, LogScopes.RunBody, query.LogSettings, $"Query: {queryString}");

            // Perform the query when there are no parameters. Either no values are used from the using result set or all values have been combined and a single query is sufficient.
            if (parameterKeys.Count == 0)
            {
                return await databaseConnection.ExecuteQuery(connectionString, queryString, insertedParameters);
            }

            var jArray = new JArray();

            // Perform the query for each row in the result set that is being used.
            var usingResultSet = ResultSetHelper.GetCorrectObject<JArray>(query.UseResultSet, 0, resultSets);
            for (var i = 0; i < usingResultSet.Count; i++)
            {
                //var queryStringWithValues = ReplacementHelper.ReplaceText(queryString, i, parameterKeys, (JObject)usingResultSet[i], mySqlSafe: true);
                var parameters = new List<KeyValuePair<string, string>>(insertedParameters);

                foreach (var key in parameterKeys)
                {
                    var parameterName = DatabaseHelpers.CreateValidParameterName(key);
                    var value = ReplacementHelper.GetValue(key, i, (JObject)usingResultSet[i], false);
                    parameters.Add(new KeyValuePair<string, string>(parameterName, value));
                }

                jArray.Add(await databaseConnection.ExecuteQuery(connectionString, queryString, parameters));
            }

            return new JObject
            {
                {"Results", jArray}
            };
        }
    }
}
