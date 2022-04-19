using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Xml;
using AutoImportServiceCore.Core.Enums;
using AutoImportServiceCore.Core.Helpers;
using AutoImportServiceCore.Core.Interfaces;
using AutoImportServiceCore.Core.Models;
using AutoImportServiceCore.Modules.Body.Interfaces;
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
        private readonly IOAuthService oAuthService;
        private readonly IBodyService bodyService;
        private readonly ILogger<HttpApisService> logger;

        private bool retryOAuthUnauthorizedResponse;

        /// <summary>
        /// Creates a new instance of <see cref="HttpApisService"/>.
        /// </summary>
        /// <param name="oAuthService"></param>
        /// <param name="bodyService"></param>
        /// <param name="logger"></param>
        public HttpApisService(IOAuthService oAuthService, IBodyService bodyService, ILogger<HttpApisService> logger)
        {
            this.oAuthService = oAuthService;
            this.bodyService = bodyService;
            this.logger = logger;

            retryOAuthUnauthorizedResponse = true;
        }

        /// <inheritdoc />
        public async Task Initialize(ConfigurationModel configuration) { }

        /// <inheritdoc />
        public async Task<JObject> Execute(ActionModel action, JObject resultSets)
        {
            var httpApi = (HttpApiModel) action;
            var jArray = new JArray();

            LogHelper.LogInformation(logger, LogScopes.RunStartAndStop, httpApi.LogSettings, $"Executing HTTP API in time id: {httpApi.TimeId}, order: {httpApi.Order}");

            if (httpApi.SingleRequest)
            {
                if (String.IsNullOrWhiteSpace(httpApi.NextUrlProperty))
                {
                    return await ExecuteRequest(httpApi, resultSets, httpApi.UseResultSet, ReplacementHelper.EmptyRows);
                }

                var url = httpApi.Url;
                do
                {
                    var result = await ExecuteRequest(httpApi, resultSets, httpApi.UseResultSet, ReplacementHelper.EmptyRows, url);
                    url = ReplacementHelper.GetValue($"Body.{httpApi.NextUrlProperty}", ReplacementHelper.EmptyRows, result, false);
                    jArray.Add(result);
                } while (!String.IsNullOrWhiteSpace(url));

                return new JObject
                {
                    {"Results", jArray}
                };
            }

            var rows = ResultSetHelper.GetCorrectObject<JArray>(httpApi.UseResultSet, ReplacementHelper.EmptyRows, resultSets);

            for (var i = 0; i < rows.Count; i++)
            {
                var indexRows = new List<int> {i};

                if (String.IsNullOrWhiteSpace(httpApi.NextUrlProperty))
                {
                    jArray.Add(await ExecuteRequest(httpApi, resultSets, $"{httpApi.UseResultSet}[{i}]", indexRows));
                }
                else
                {
                    var url = httpApi.Url;
                    do
                    {
                        var result = await ExecuteRequest(httpApi, resultSets, $"{httpApi.UseResultSet}[{indexRows[0]}]", indexRows, url);
                        url = ReplacementHelper.GetValue($"Body.{httpApi.NextUrlProperty}", ReplacementHelper.EmptyRows, result, false);
                        jArray.Add(result);
                    } while (!String.IsNullOrWhiteSpace(url));
                }
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
        /// <param name="rows">The indexes/rows of the array, passed to be used if '[i]' is used in the key.</param>
        /// <param name="overrideUrl">The url to use instead of the provided url, used for continuous calls with a next URL.</param>
        /// <returns></returns>
        private async Task<JObject> ExecuteRequest(HttpApiModel httpApi, JObject resultSets, string useResultSet, List<int> rows, string overrideUrl = "")
        {
            var url = String.IsNullOrWhiteSpace(overrideUrl) ? httpApi.Url : overrideUrl;

            // If a result set needs to be used apply it on the url.
            if (!String.IsNullOrWhiteSpace(useResultSet))
            {
                var keyParts = useResultSet.Split('.');
                var usingResultSet = ResultSetHelper.GetCorrectObject<JObject>(httpApi.SingleRequest ? keyParts[0] : useResultSet, ReplacementHelper.EmptyRows, resultSets);
                var remainingKey = keyParts.Length > 1 ? useResultSet.Substring(keyParts[0].Length + 1) : "";
                var tuple = ReplacementHelper.PrepareText(url, usingResultSet, remainingKey, htmlEncode: true);
                url = tuple.Item1;
                var parameterKeys = tuple.Item2;
                url = ReplacementHelper.ReplaceText(url, rows, parameterKeys, usingResultSet, htmlEncode: true);
            }

            LogHelper.LogInformation(logger, LogScopes.RunBody, httpApi.LogSettings, $"Url: {url}, method: {httpApi.Method}");
            var request = new HttpRequestMessage(new HttpMethod(httpApi.Method), url);

            foreach (var header in httpApi.Headers)
            {
                if (string.IsNullOrWhiteSpace(header.UseResultSet))
                {
                    request.Headers.Add(header.Name, header.Value);
                }
                // If a result set is used for the header apply it to the value.
                else
                {
                    var keyParts = header.UseResultSet.Split('.');
                    var usingResultSet = ResultSetHelper.GetCorrectObject<JObject>(httpApi.SingleRequest ? keyParts[0] : header.UseResultSet, ReplacementHelper.EmptyRows, resultSets);
                    var remainingKey = keyParts.Length > 1 ? header.UseResultSet.Substring(keyParts[0].Length + 1) : "";
                    var tuple = ReplacementHelper.PrepareText(header.Value, usingResultSet, remainingKey);
                    var headerValue = tuple.Item2.Count > 0 ? ReplacementHelper.ReplaceText(tuple.Item1, rows, tuple.Item2, usingResultSet) : tuple.Item1;
                    request.Headers.Add(header.Name, headerValue);
                }
            }

            if (!String.IsNullOrWhiteSpace(httpApi.OAuth))
            {
                var token = await oAuthService.GetAccessTokenAsync(httpApi.OAuth);
                if (token != null)
                {
                    request.Headers.Add("Authorization", token);
                }
            }

            LogHelper.LogInformation(logger, LogScopes.RunBody, httpApi.LogSettings, $"Headers: {request.Headers}");
            
            if (httpApi.Body != null)
            {
                var body = bodyService.GenerateBody(httpApi.Body, rows, resultSets);

                LogHelper.LogInformation(logger, LogScopes.RunBody, httpApi.LogSettings, $"Body:\n{body}");
                request.Content = new StringContent(body)
                {
                    Headers = {ContentType = new MediaTypeHeaderValue(httpApi.Body.ContentType)}
                };
            }

            using var client = new HttpClient();
            using var response = await client.SendAsync(request);

            // If the request was unauthorized retry the request if it has an OAuth API name set and it hasn't retried before.
            if (response.StatusCode == HttpStatusCode.Unauthorized && !String.IsNullOrWhiteSpace(httpApi.OAuth) && retryOAuthUnauthorizedResponse)
            {
                retryOAuthUnauthorizedResponse = false;
                await oAuthService.RequestWasUnauthorizedAsync(httpApi.OAuth);

                LogHelper.LogWarning(logger, LogScopes.RunBody, httpApi.LogSettings, $"Request to {url} return \"Unauthorized\" on OAuth token. Retrying once with new OAuth token.");
                return await ExecuteRequest(httpApi, resultSets, useResultSet, rows, overrideUrl);
            }

            retryOAuthUnauthorizedResponse = true;

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
                if (responseBody.StartsWith("{"))
                {
                    resultSet.Add("Body", JObject.Parse(responseBody));
                }
                else if(responseBody.StartsWith("["))
                {
                    resultSet.Add("Body", JArray.Parse(responseBody));
                }
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

            // Always add the body as plain text.
            resultSet.Add("BodyPlainText", responseBody);

            LogHelper.LogInformation(logger, LogScopes.RunBody, httpApi.LogSettings, $"Status: {resultSet["StatusCode"]}, Result body:\n{responseBody}");

            return resultSet;
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
