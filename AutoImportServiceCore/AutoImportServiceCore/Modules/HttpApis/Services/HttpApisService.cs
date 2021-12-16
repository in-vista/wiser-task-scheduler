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

            var tuple = ReplacementHelper.PrepareText(httpApi.Url, usingResultSet, htmlEncode: true);
            var url = tuple.Item1;
            var parameterKeys = tuple.Item2;

            var request = new HttpRequestMessage(new HttpMethod(httpApi.Method), ReplacementHelper.ReplaceText(url, 1, parameterKeys, usingResultSet, htmlEncode: true));

            foreach (var header in httpApi.Headers)
            {
                request.Headers.Add(header.Name, header.Value);
            }

            if (httpApi.Body != null)
            {
                tuple = ReplacementHelper.PrepareText(httpApi.Body.Body, usingResultSet);
                var body = tuple.Item1;
                parameterKeys = tuple.Item2;

                if (usingResultSet != null && parameterKeys.Count > 0)
                {
                    if (httpApi.Body.SingleItem)
                    {
                        if (usingResultSet.First().Value.Count > 0)
                        {
                            body = ReplacementHelper.ReplaceText(body, 1, parameterKeys, usingResultSet);
                        }
                    }
                    else
                    {
                        var bodyCollection = new StringBuilder();

                        // Perform the query for each row in the result set that is being used.
                        for (var i = 1; i <= usingResultSet.First().Value.Count; i++)
                        {
                            var bodyWithValues = ReplacementHelper.ReplaceText(body, i, parameterKeys, usingResultSet);
                            bodyCollection.Append($"{(i > 1 ? "," : "")}{bodyWithValues}");
                        }

                        body = $"[{bodyCollection}]";
                    }
                }

                request.Content = new StringContent(body)
                {
                    Headers = {ContentType = new MediaTypeHeaderValue(httpApi.Body.ContentType)}
                };
            }

            using var client = new HttpClient();
            var response = await client.SendAsync(request);

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
