using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using AutoImportServiceCore.Core.Enums;
using AutoImportServiceCore.Core.Helpers;
using AutoImportServiceCore.Core.Interfaces;
using AutoImportServiceCore.Core.Models;
using AutoImportServiceCore.Modules.HttpApis.Interfaces;
using AutoImportServiceCore.Modules.HttpApis.Models;
using GeeksCoreLibrary.Core.DependencyInjection.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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
            var httpApi = (HttpApiModel) action;

            LogHelper.LogInformation(logger, LogScopes.RunStartAndStop, httpApi.LogSettings, $"Executing HTTP API in time id: {httpApi.TimeId}, order: {httpApi.Order}");

            if (httpApi.SingleRequest)
            {
                return await ExecuteRequest(httpApi, resultSets, httpApi.UseResultSet, 0);
            }

            var jArray = new JArray();
            var rows = ResultSetHelper.GetCorrectObject<JArray>(httpApi.UseResultSet, 0, resultSets);

            for (var i = 0; i < rows.Count; i++)
            {
                jArray.Add(await ExecuteRequest(httpApi, resultSets, $"{httpApi.UseResultSet}[{i}]", i));
            }

            return new JObject
            {
                {"Results", jArray}
            };
        }

        /// <summary>
        /// Execute the HTTP API request.
        /// </summary>
        /// <param name="httpApi">The HTTP API action to execute.</param>
        /// <param name="resultSets">The result sets from previous actions in the same run.</param>
        /// <param name="useResultSet">The result set to use for this execution.</param>
        /// <param name="row">The index/row of the array, passed to be used if '[i]' is used in the key.</param>
        /// <returns></returns>
        private async Task<JObject> ExecuteRequest(HttpApiModel httpApi, JObject resultSets, string useResultSet, int row)
        {
            var url = httpApi.Url;

            // If a result set needs to be used apply it on the url.
            if (!String.IsNullOrWhiteSpace(useResultSet))
            {
                var keyParts = useResultSet.Split('.');
                var usingResultSet = ResultSetHelper.GetCorrectObject<JObject>(httpApi.SingleRequest ? keyParts[0] : useResultSet, 0, resultSets);
                var remainingKey = keyParts.Length > 1 ? useResultSet.Substring(keyParts[0].Length + 1) : "";
                var tuple = ReplacementHelper.PrepareText(url, usingResultSet, remainingKey, htmlEncode: true);
                url = tuple.Item1;
                var parameterKeys = tuple.Item2;
                url = ReplacementHelper.ReplaceText(url, row, parameterKeys, usingResultSet, htmlEncode: true);
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
                        var keyParts = bodyPart.UseResultSet.Split('.');
                        var remainingKey = keyParts.Length > 1 ? bodyPart.UseResultSet.Substring(keyParts[0].Length + 1) : "";
                        var tuple = ReplacementHelper.PrepareText(bodyPart.Text, (JObject)resultSets[keyParts[0]], remainingKey);
                        body = tuple.Item1;
                        var parameterKeys = tuple.Item2;

                        if (parameterKeys.Count > 0)
                        {
                            // Replace body with values from first row.
                            if (bodyPart.SingleItem)
                            {
                                body = ReplacementHelper.ReplaceText(body, row, parameterKeys, (JObject)resultSets[bodyPart.UseResultSet]);
                            }
                            // Replace and combine body with values for each row.
                            else
                            {
                                body = GenerateBodyCollection(body, httpApi.Body.ContentType, parameterKeys, ResultSetHelper.GetCorrectObject<JArray>(bodyPart.UseResultSet, row, resultSets));
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

            var resultSet = new JObject
            {
                { "StatusCode", ((int)response.StatusCode).ToString() }
            };

            // Add all headers to the result set.
            ExtractHeadersIntoResultSet(resultSet, response.Headers);
            // Add all content headers to the result set.
            ExtractHeadersIntoResultSet(resultSet, response.Content.Headers);

            var responseBody = await response.Content.ReadAsStringAsync();
            if (resultSet.ContainsKey("Content-Type") && ((string)resultSet["Content-Type"][0]).Contains("json"))
            {
                resultSet.Add("Body", JObject.Parse(responseBody));
            }
            else if (resultSet.ContainsKey("Content-Type") && ((string)resultSet["Content-Type"][0]).Contains("xml"))
            {
                var xml = new XmlDocument();
                xml.LoadXml(responseBody);
                resultSet.Add("Body", JObject.Parse(JsonConvert.SerializeXmlNode(xml)));
            }
            else
            {
                resultSet.Add("Body", responseBody);
            }

            LogHelper.LogInformation(logger, LogScopes.RunBody, httpApi.LogSettings, $"Status: {resultSet["StatusCode"]}, Result body:\n{responseBody}");

            return resultSet;
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

        private void ExtractHeadersIntoResultSet(JObject resultSet, HttpHeaders headers)
        {
            foreach (var header in headers)
            {
                var headerProperties = new JArray();

                foreach (var value in header.Value)
                {
                    headerProperties.Add(value);
                }

                resultSet.Add(header.Key, headerProperties);
            }
        }
    }
}
