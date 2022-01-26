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
using Newtonsoft.Json.Linq;

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
        public async Task Initialize(ConfigurationModel configuration) { }

        /// <inheritdoc />
        public async Task<JObject> Execute(ActionModel action, JObject resultSets)
        {
            var resultSet = new Dictionary<string, SortedDictionary<int, string>>();

            var httpApi = (HttpApiModel)action;

            LogHelper.LogInformation(logger, LogScopes.RunStartAndStop, httpApi.LogSettings, $"Executing HTTP API in time id: {httpApi.TimeId}, order: {httpApi.Order}");

            var url = httpApi.Url;

            // If a result set needs to be used apply it on the url.
            if (!String.IsNullOrWhiteSpace(httpApi.UseResultSet))
            {
                var tuple = ReplacementHelper.PrepareText(url, (JObject)resultSets[httpApi.UseResultSet], htmlEncode: true);
                url = tuple.Item1;
                var parameterKeys = tuple.Item2;
                url = ReplacementHelper.ReplaceText(url, 1, parameterKeys, (JObject)resultSets[httpApi.UseResultSet], htmlEncode: true);
            }

            LogHelper.LogInformation(logger, LogScopes.RunBody, httpApi.LogSettings, $"Url: {url}, method: {httpApi.Method}");
            var request = new HttpRequestMessage(new HttpMethod(httpApi.Method), url);

            foreach (var header in httpApi.Headers)
            {
                request.Headers.Add(header.Name, header.Value);
            }
            LogHelper.LogInformation(logger, LogScopes.RunBody, httpApi.LogSettings, $"Headers: {request.Headers}");
            
            if (httpApi.Body != null)
            {
                var finalBody = new StringBuilder();

                foreach (var bodyPart in httpApi.Body.BodyParts)
                {
                    var body = bodyPart.Text;
                    
                    // If the part needs a result set, apply it.
                    if (!String.IsNullOrWhiteSpace(bodyPart.UseResultSet))
                    {
                        var tuple = ReplacementHelper.PrepareText(bodyPart.Text, (JObject)resultSets[bodyPart.UseResultSet]);
                        body = tuple.Item1;
                        var parameterKeys = tuple.Item2;

                        if (parameterKeys.Count > 0)
                        {
                            // Replace body with values from first row.
                            if (bodyPart.SingleItem)
                            {
                                body = ReplacementHelper.ReplaceText(body, 0, parameterKeys, (JObject)resultSets[bodyPart.UseResultSet]);
                            }
                            // Replace and combine body with values for each row.
                            else
                            {
                                var resultSetParts = bodyPart.UseResultSet.Split('.');
                                body = GenerateBodyCollection(body, httpApi.Body.ContentType, parameterKeys, (JArray)resultSets[resultSetParts[0]][resultSetParts[1]]);
                            }
                        }
                    }

                    finalBody.Append(body);
                }

                LogHelper.LogInformation(logger, LogScopes.RunBody, httpApi.LogSettings, $"Body:\n{finalBody}");
                request.Content = new StringContent(finalBody.ToString())
                {
                    Headers = {ContentType = new MediaTypeHeaderValue(httpApi.Body.ContentType)}
                };
            }

            using var client = new HttpClient();
            var response = await client.SendAsync(request);

            resultSet.Add("StatusCode", new SortedDictionary<int, string>() {{1, ((int) response.StatusCode).ToString()}});

            // Add all headers to the result set.
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

            // Add all content headers to the result set.
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

            LogHelper.LogInformation(logger, LogScopes.RunBody, httpApi.LogSettings, $"Status: {resultSet["StatusCode"][1]}, Result body:\n{resultSet["Body"][1]}");

            return new JObject();// resultSet;
        }

        /// <summary>
        /// Replace values in the body for each row and return the combined result.
        /// </summary>
        /// <param name="body">The body text to use for each row.</param>
        /// <param name="contentType">The content type that is being send in the request.</param>
        /// <param name="parameterKeys">The keys of the parameters that need to be replaced.</param>
        /// <param name="usingResultSet">The result set to get the values from.</param>
        /// <returns></returns>
        private string GenerateBodyCollection(string body, string contentType, List<string> parameterKeys, JArray usingResultSet)
        {
            var separator = String.Empty;

            // Add a separator between each row result based on content type.
            switch (contentType)
            {
                case "application/json":
                    separator = ",";
                    break;
            }

            var bodyCollection = new StringBuilder();

            // Perform the query for each row in the result set that is being used.
            for (var i = 0; i < usingResultSet.Count; i++)
            {
                var bodyWithValues = ReplacementHelper.ReplaceText(body, i, parameterKeys, (JObject)usingResultSet[i]);
                bodyCollection.Append($"{(i > 0 ? separator : "")}{bodyWithValues}");
            }

            // Add collection syntax based on content type.
            switch (contentType)
            {
                case "application/json":
                    return $"[{bodyCollection}]";
                default:
                    return bodyCollection.ToString();
            }
        }
    }
}
