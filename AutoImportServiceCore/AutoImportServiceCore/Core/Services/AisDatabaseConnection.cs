using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;

namespace AutoImportServiceCore.Core.Services
{
    /// <summary>
    /// The database the AIS uses to perform queries to a database.
    /// </summary>
    public class AisDatabaseConnection
    {
        private readonly ILogger<AisDatabaseConnection> logger;

        /// <summary>
        /// Creates a new instance of <see cref="AisDatabaseConnection"/>.
        /// </summary>
        public AisDatabaseConnection(ILogger<AisDatabaseConnection> logger)
        {
            this.logger = logger;
        }

        /// <summary>
        /// Execute a query and get the results.
        /// </summary>
        /// <param name="connectionString">The connection string to use.</param>
        /// <param name="query">The query to execute.</param>
        /// <returns></returns>
        public async Task<JObject> ExecuteQuery(string connectionString, string query)
        {
            var resultSet = new JObject();

            await using var connection = new MySqlConnection(connectionString);
            try
            {
                await connection.OpenAsync();

                await using var command = connection.CreateCommand();
                command.CommandText = query;

                await using (var reader = await command.ExecuteReaderAsync())
                {
                    if (!reader.HasRows)
                    {
                        return resultSet;
                    }

                    var jArray = new JArray();

                    // Add rows to result set.
                    while (await reader.ReadAsync())
                    {
                        var jObject = new JObject();

                        for (var i = 0; i < reader.FieldCount; i++)
                        {
                            var columnName = reader.GetName(i);
                            jObject.Add(columnName, reader[i].ToString());
                        }

                        jArray.Add(jObject);
                    }

                    resultSet.Add("Results", jArray);
                }

                return resultSet;
            }
            finally
            {
                await connection.CloseAsync();
            }
        }
    }
}