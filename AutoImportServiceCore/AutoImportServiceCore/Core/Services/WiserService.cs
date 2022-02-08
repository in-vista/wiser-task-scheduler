using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using AutoImportServiceCore.Core.Enums;
using AutoImportServiceCore.Core.Helpers;
using AutoImportServiceCore.Core.Interfaces;
using AutoImportServiceCore.Core.Models;
using GeeksCoreLibrary.Core.DependencyInjection.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace AutoImportServiceCore.Core.Services
{
    /// <inheritdoc cref="IWiserService"/> />
    public class WiserService : IWiserService, ISingletonService
    {
        private readonly WiserSettings wiserSettings;
        private readonly ILogger<WiserService> logger;
        private readonly LogSettings logSettings;

        [Browsable(false)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        private string accessToken;
        private DateTime accessTokenExpireTime;
        private string refreshToken;

        public string AccesToken => GetAccessToken();

        public WiserService(IOptions<AisSettings> aisSettings, ILogger<WiserService> logger)
        {
            wiserSettings = aisSettings.Value.Wiser;
            this.logger = logger;
            logSettings = wiserSettings.LogSettings ?? new LogSettings();

            accessToken = "";
            accessTokenExpireTime = DateTime.MinValue;
            refreshToken = "";
        }

        /// <summary>
        /// Get the access token and gets a new token if none is available or if it has expired.
        /// </summary>
        /// <returns></returns>
        private string GetAccessToken()
        {
            lock (accessToken)
            {
                if (String.IsNullOrWhiteSpace(accessToken))
                {
                    Login();
                }
                else if (accessTokenExpireTime < DateTime.Now)
                {
                    if (String.IsNullOrWhiteSpace(refreshToken))
                    {
                        Login();
                    }
                    else
                    {
                        Login(true);
                    }
                }

                return accessToken;
            }
        }

        /// <summary>
        /// DO NOT CALL THIS BY YOURSELF!
        /// Login to the Wiser API.
        /// This method is called when using <see cref="AccesToken"/> or <see cref="GetAccessToken"/>.
        /// </summary>
        private void Login(bool useRefreshToken = false)
        {
            var formData = new List<KeyValuePair<string, string>>()
            {
                new("subDomain", $"{wiserSettings.Subdomain}"),
                new("client_id", $"{wiserSettings.ClientId}"),
                new("client_secret", $"{wiserSettings.ClientSecret}"),
                new("isTestEnvironment", $"{wiserSettings.TestEnvironment}")
            };

            if (useRefreshToken)
            {
                formData.Add(new KeyValuePair<string, string>("grant_type", "refresh_token"));
                formData.Add(new KeyValuePair<string, string>("refresh_token", refreshToken));
            }
            else
            {
                formData.Add(new KeyValuePair<string, string>("grant_type", "password"));
                formData.Add(new KeyValuePair<string, string>("username", $"{wiserSettings.Username}"));
                formData.Add(new KeyValuePair<string, string>("password", $"{wiserSettings.Password}"));
            }

            var request = new HttpRequestMessage(HttpMethod.Post, $"{wiserSettings.WiserApiUrl}connect/token")
            {
                Content = new FormUrlEncodedContent(formData)
            };
            request.Headers.Add("Accept", "application/json");
            
            LogHelper.LogInformation(logger, LogScopes.RunBody, logSettings, $"URL: {request.RequestUri}\nHeaders: {request.Headers}\nBody: {String.Join(' ', formData)}");
            
            using var client = new HttpClient();
            var response = client.Send(request);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                LogHelper.LogCritical(logger, LogScopes.RunStartAndStop, logSettings, "Failed to login to the Wiser API.");
                return;
            }
            
            using var reader = new StreamReader(response.Content.ReadAsStream());
            var body = reader.ReadToEnd();
            LogHelper.LogInformation(logger, LogScopes.RunBody, logSettings, $"Response body: {body}");
            var wiserLoginResponse = JsonConvert.DeserializeObject<WiserLoginResponseModel>(body);
            
            accessToken = wiserLoginResponse.AccessToken;
            accessTokenExpireTime = DateTime.Now.AddSeconds(wiserLoginResponse.ExpiresIn);
            refreshToken = wiserLoginResponse.RefreshToken;
        }

        /// <inheritdoc />
        public async Task<List<string>> RequestConfigurations()
        {
            return await Task.Run(() =>
            {
                var a = AccesToken;
                return new List<string>();
            });
        }
    }
}
