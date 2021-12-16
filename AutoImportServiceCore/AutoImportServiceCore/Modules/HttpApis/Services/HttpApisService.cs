using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using AutoImportServiceCore.Core.Enums;
using AutoImportServiceCore.Core.Helpers;
using AutoImportServiceCore.Core.Interfaces;
using AutoImportServiceCore.Core.Models;
using AutoImportServiceCore.Modules.HttpApis.Interfaces;
using AutoImportServiceCore.Modules.HttpApis.Models;
using GeeksCoreLibrary.Core.DependencyInjection.Interfaces;
using Microsoft.Extensions.Logging;

namespace AutoImportServiceCore.Modules.HttpApis.Services
{
    /// <summary>
    /// A service for a HTTP API action.
    /// </summary>
    public class HttpApisService : IHttpApisService, IActionsService, IScopedService
    {
        private readonly ILogger<HttpApisService> logger;

        /// <summary>
        /// Creates a new instance of <see cref="HttpApisService"/>.
        /// </summary>
        /// <param name="logger"></param>
        public HttpApisService(ILogger<HttpApisService> logger)
        {
            this.logger = logger;
        }

        /// <inheritdoc />
        public async Task Initialize(ConfigurationModel configuration)
        {

        }

        /// <inheritdoc />
        public async Task<Dictionary<string, SortedDictionary<int, string>>> Execute(ActionModel action, Dictionary<string, SortedDictionary<int, string>> usingResultSet)
        {
            var resultSet = new Dictionary<string, SortedDictionary<int, string>>();

            var httpApi = action as HttpApiModel;

            LogHelper.LogInformation(logger, LogScopes.RunStartAndStop, httpApi.LogSettings, $"Executing HTTP API in time id: {httpApi.TimeId}, order: {httpApi.Order}, url: {httpApi.Url}, method: {httpApi.Method}");

            var request = new HttpRequestMessage(new HttpMethod(httpApi.Method), httpApi.Url);

            foreach (var header in httpApi.Headers)
            {
                request.Headers.Add(header.Name, header.Value);
            }

            if (httpApi.Body != null)
            {
                var body = httpApi.Body.Body;
                var parameterKeys = new List<string>();

                while (body.Contains("[{") && body.Contains("}]"))
                {
                    var startIndex = body.IndexOf("[{") + 2;
                    var endIndex = body.IndexOf("}]");

                    var key = body.Substring(startIndex, endIndex - startIndex);

                    if (key.Contains("[]"))
                    {
                        key = key.Replace("[]", "");

                        var values = new List<string>();

                        for (var i = 1; i <= usingResultSet[key].Count; i++)
                        {
                            values.Add(usingResultSet[key][i]);
                        }

                        body = body.Replace($"[{{{key}[]}}]", String.Join(",", values));
                    }
                    else
                    {
                        body = body.Replace($"[{{{key}}}]", $"?{key}");
                        parameterKeys.Add(key);
                    }
                }

                if (usingResultSet != null && parameterKeys.Count > 0)
                {
                    if (httpApi.Body.SingleItem)
                    {
                        if (usingResultSet.First().Value.Count > 0)
                        {
                            // Replacing the parameters with the required values of the first row.
                            foreach (var key in parameterKeys)
                            {
                                body = body.Replace($"?{key}", usingResultSet[key][1]);
                            }
                        }
                    }
                    else
                    {
                        var bodyCollection = new StringBuilder();

                        // Perform the query for each row in the result set that is being used.
                        for (var i = 1; i <= usingResultSet.First().Value.Count; i++)
                        {
                            var bodyWithValues = body;

                            // Replacing the parameters with the required values of the current row. Replacing with database safe string to allow parameters in strings.
                            foreach (var key in parameterKeys)
                            {
                                bodyWithValues = bodyWithValues.Replace($"?{key}", usingResultSet[key][i]);
                            }

                            bodyCollection.Append($"{(i > 1 ? "," : "")}{bodyWithValues}");
                        }

                        body = $"[{bodyCollection.ToString()}]";
                    }
                }

                request.Content = new StringContent(body)
                {
                    Headers = {ContentType = new MediaTypeHeaderValue(httpApi.Body.ContentType)}
                };
            }

            using var client = new HttpClient();
            var response = await client.SendAsync(request);
            Console.WriteLine($"Status: {response.StatusCode}, Body: {await response.Content.ReadAsStringAsync()}");

            resultSet.Add("StatusCode", new SortedDictionary<int, string>() {{1, ((int) response.StatusCode).ToString()}});

            foreach (var header in response.Headers)
            {
                resultSet.Add(header.Key, new SortedDictionary<int, string>());

                var row = 1;
                foreach (var value in header.Value)
                {
                    resultSet[header.Key].Add(row, value);
                    row++;
                }
            }

            foreach (var header in response.Content.Headers)
            {
                resultSet.Add(header.Key, new SortedDictionary<int, string>());

                var row = 1;
                foreach (var value in header.Value)
                {
                    resultSet[header.Key].Add(row, value);
                    row++;
                }
            }

            
            resultSet.Add("Body", new SortedDictionary<int, string>() {{1, await response.Content.ReadAsStringAsync()}});

            return resultSet;
        }
    }
}
