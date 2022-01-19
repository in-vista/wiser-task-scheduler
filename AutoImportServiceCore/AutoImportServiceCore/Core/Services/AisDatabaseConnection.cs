using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;

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
        public async Task<Dictionary<string, SortedDictionary<int, string>>> ExecuteQuery(string connectionString, string query)
        {
            var resultSet = new Dictionary<string, SortedDictionary<int, string>>();

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

                    var row = 1;

                    // Add rows to result set.
                    while (await reader.ReadAsync())
                    {
                        for (var i = 0; i < reader.FieldCount; i++)
                        {
                            var columnName = reader.GetName(i);
                            resultSet.TryAdd(columnName, new SortedDictionary<int, string>());
                            resultSet[columnName].Add(row, reader[i].ToString());
                        }

                        row++;
                    }
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