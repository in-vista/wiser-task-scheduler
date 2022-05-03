using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using AutoImportServiceCore.Core.Enums;
using AutoImportServiceCore.Core.Helpers;
using AutoImportServiceCore.Core.Interfaces;
using AutoImportServiceCore.Core.Models.OAuth;
using GeeksCoreLibrary.Core.DependencyInjection.Interfaces;
using GeeksCoreLibrary.Core.Extensions;
using GeeksCoreLibrary.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace AutoImportServiceCore.Core.Services
{
    public class OAuthService : IOAuthService, ISingletonService
    {
        private const string TableName = "easy_objects";

        private readonly GclSettings gclSettings;
        private readonly ILogger<OAuthService> logger;
        private readonly IServiceProvider serviceProvider;

        private OAuthConfigurationModel configuration;

        public OAuthService(IOptions<GclSettings> gclSettings, ILogger<OAuthService> logger, IServiceProvider serviceProvider)
        {
            this.gclSettings = gclSettings.Value;
            this.logger = logger;
            this.serviceProvider = serviceProvider;
        }

        /// <inheritdoc />
        public async Task SetConfigurationAsync(OAuthConfigurationModel configuration)
        {
            this.configuration = configuration;

            using var scope = serviceProvider.CreateScope();
            var databaseConnection = scope.ServiceProvider.GetRequiredService<AisDatabaseConnection>();

            var query = @$"SELECT accessToken.`value` AS accessToken, tokenType.`value` AS tokenType, refreshToken.`value` AS refreshToken, expireTime.`value` AS expireTime
FROM (SELECT 1) AS temp
LEFT JOIN {TableName} AS accessToken ON accessToken.`key` = ?accessToken
LEFT JOIN {TableName} AS tokenType ON tokenType.`key` = ?tokenType
LEFT JOIN {TableName} AS refreshToken ON refreshToken.`key` = ?refreshToken
LEFT JOIN {TableName} AS expireTime ON expireTime.`key` = ?expireTime";

            // Check if there is already information stored in the database to use.
            foreach (var oAuth in this.configuration.OAuths)
            {
                var parameters = new List<KeyValuePair<string, string>>
                {
                    new("accessToken", $"AIS_{oAuth.ApiName}_AccessToken"),
                    new("tokenType", $"AIS_{oAuth.ApiName}_TokenType"),
                    new("refreshToken", $"AIS_{oAuth.ApiName}_RefreshToken"),
                    new("expireTime", $"AIS_{oAuth.ApiName}_ExpireTime")
                };
                
                JObject result = await databaseConnection.ExecuteQuery(this.configuration.ConnectionString, query, parameters);
                oAuth.AccessToken = ((string)ResultSetHelper.GetCorrectObject<JValue>("Results[0].accessToken", ReplacementHelper.EmptyRows, result))?.DecryptWithAes(gclSettings.DefaultEncryptionKey);
                oAuth.TokenType = (string)ResultSetHelper.GetCorrectObject<JValue>("Results[0].tokenType", ReplacementHelper.EmptyRows, result);
                oAuth.RefreshToken = ((string)ResultSetHelper.GetCorrectObject<JValue>("Results[0].refreshToken", ReplacementHelper.EmptyRows, result))?.DecryptWithAes(gclSettings.DefaultEncryptionKey);
                var expireTime = (string) ResultSetHelper.GetCorrectObject<JValue>("Results[0].expireTime", ReplacementHelper.EmptyRows, result);
                oAuth.ExpireTime = String.IsNullOrWhiteSpace(expireTime) ? DateTime.MinValue : Convert.ToDateTime(expireTime);

                oAuth.LogSettings ??= configuration.LogSettings;
            }
        }

        /// <inheritdoc />
        public async Task<string> GetAccessTokenAsync(string apiName, bool retryAfterWrongRefreshToken = true)
        {
            var oAuthApi = configuration.OAuths.SingleOrDefault(oAuth => oAuth.ApiName.Equals(apiName));

            if (oAuthApi == null)
            {
                return null;
            }

            // Check if a new access token needs to be requested and request it.
            var result = await Task.Run(() =>
            {
                lock (oAuthApi)
                {
                    if (!String.IsNullOrWhiteSpace(oAuthApi.AccessToken) && oAuthApi.ExpireTime > DateTime.Now)
                    {
                        return OAuthState.CurrentToken;
                    }

                    var formData = new List<KeyValuePair<string, string>>();

                    // Setup correct authentication.
                    OAuthState failState;
                    if (String.IsNullOrWhiteSpace(oAuthApi.AccessToken) || String.IsNullOrWhiteSpace(oAuthApi.RefreshToken))
                    {
                        LogHelper.LogInformation(logger, LogScopes.RunBody, oAuthApi.LogSettings, $"Requesting new access token for '{apiName}' using username and password.");

                        failState = OAuthState.FailedLogin;
                        formData.Add(new KeyValuePair<string, string>("grant_type", "password"));
                        formData.Add(new KeyValuePair<string, string>("username", oAuthApi.Username));
                        formData.Add(new KeyValuePair<string, string>("password", oAuthApi.Password));
                    }
                    else
                    {
                        LogHelper.LogInformation(logger, LogScopes.RunBody, oAuthApi.LogSettings, $"Requesting new access token for '{apiName}' using refresh token.");

                        failState = OAuthState.FailedRefreshToken;
                        formData.Add(new KeyValuePair<string, string>("grant_type", "refresh_token"));
                        formData.Add(new KeyValuePair<string, string>("refresh_token", oAuthApi.RefreshToken));
                    }

                    if (oAuthApi.FormKeyValues != null)
                    {
                        foreach (var keyValue in oAuthApi.FormKeyValues)
                        {
                            formData.Add(new KeyValuePair<string, string>(keyValue.Key, keyValue.Value));
                        }
                    }

                    var request = new HttpRequestMessage(HttpMethod.Post, oAuthApi.Endpoint)
                    {
                        Content = new FormUrlEncodedContent(formData)
                    };
                    request.Headers.Add("Accept", "application/json");

                    using var client = new HttpClient();
                    var response = client.Send(request);

                    using var reader = new StreamReader(response.Content.ReadAsStream());
                    var json = reader.ReadToEnd();

                    if (!response.IsSuccessStatusCode)
                    {
                        LogHelper.LogError(logger, LogScopes.RunBody, oAuthApi.LogSettings, $"Failed to get access token for {oAuthApi.ApiName}. Received: {response.StatusCode}\n{json}");
                        return failState;
                    }

                    var body = JObject.Parse(json);

                    oAuthApi.AccessToken = (string)body["access_token"];
                    oAuthApi.TokenType = (string) body["token_type"];
                    oAuthApi.RefreshToken = (string) body["refresh_token"];

                    if (body["expires_in"].Type == JTokenType.Integer)
                    {
                        oAuthApi.ExpireTime = DateTime.Now.AddSeconds((int)body["expires_in"]);
                    }
                    else
                    {
                        oAuthApi.ExpireTime = DateTime.Now.AddSeconds(Convert.ToInt32((string)body["expires_in"]));
                    }

                    oAuthApi.ExpireTime -= oAuthApi.ExpireTimeOffset;

                    LogHelper.LogInformation(logger, LogScopes.RunBody, oAuthApi.LogSettings, $"A new access token has been retrieved for {oAuthApi.ApiName} and is valid till {oAuthApi.ExpireTime}");

                    return OAuthState.NewToken;
                }
            });

            if (result == OAuthState.FailedLogin)
            {
                return null;
            }

            if (result == OAuthState.FailedRefreshToken)
            {
                if (!retryAfterWrongRefreshToken)
                {
                    return null;
                }

                //Retry to get the token with the login credentials if the refresh token was invalid.
                await RequestWasUnauthorizedAsync(apiName);
                return await GetAccessTokenAsync(apiName, false);
            }

            if (result == OAuthState.CurrentToken)
            {
                return $"{oAuthApi.TokenType} {oAuthApi.AccessToken}";
            }

            await SaveToDatabaseAsync(oAuthApi);

            return $"{oAuthApi.TokenType} {oAuthApi.AccessToken}";
        }

        public async Task RequestWasUnauthorizedAsync(string apiName)
        {
            var oAuthApi = configuration.OAuths.SingleOrDefault(oAuth => oAuth.ApiName.Equals(apiName));

            if (oAuthApi == null)
            {
                return;
            }

            oAuthApi.AccessToken = null;
            oAuthApi.TokenType = null;
            oAuthApi.RefreshToken = null;
            oAuthApi.ExpireTime = DateTime.MinValue;

            await SaveToDatabaseAsync(oAuthApi);
        }

        private async Task SaveToDatabaseAsync(OAuthModel oAuthApi)
        {
            using var scope = serviceProvider.CreateScope();
            var databaseConnection = scope.ServiceProvider.GetRequiredService<AisDatabaseConnection>();

            var query = $@"INSERT INTO {TableName} (typenr, `key`, `value`)
VALUES
    (-1, ?accessTokenKey, ?accessTokenValue),
    (-1, ?tokenTypeKey, ?tokenTypeValue),
    (-1, ?refreshTokenKey, ?refreshTokenValue),
    (-1, ?expireTimeKey, ?expireTimeValue)
ON DUPLICATE KEY UPDATE `value` = VALUES(`value`)";

            var parameters = new List<KeyValuePair<string, string>>
            {
                new("accessTokenKey", $"AIS_{oAuthApi.ApiName}_AccessToken"),
                new("accessTokenValue", oAuthApi.AccessToken.EncryptWithAes(gclSettings.DefaultEncryptionKey)),
                new("tokenTypeKey", $"AIS_{oAuthApi.ApiName}_TokenType"),
                new("tokenTypeValue", oAuthApi.TokenType),
                new("refreshTokenKey", $"AIS_{oAuthApi.ApiName}_RefreshToken"),
                new("refreshTokenValue", oAuthApi.RefreshToken.EncryptWithAes(gclSettings.DefaultEncryptionKey)),
                new("expireTimeKey", $"AIS_{oAuthApi.ApiName}_ExpireTime"),
                new("expireTimeValue", oAuthApi.ExpireTime.ToString())
            };

            await databaseConnection.ExecuteQuery(configuration.ConnectionString, query, parameters);
        }
    }
}
