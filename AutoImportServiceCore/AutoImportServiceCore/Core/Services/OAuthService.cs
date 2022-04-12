using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoImportServiceCore.Core.Interfaces;
using AutoImportServiceCore.Core.Models.OAuth;
using GeeksCoreLibrary.Core.DependencyInjection.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace AutoImportServiceCore.Core.Services
{
    public class OAuthService : IOAuthService, ISingletonService
    {
        private readonly ILogger<OAuthService> logger;
        private readonly IServiceProvider serviceProvider;

        private OAuthConfigurationModel configuration;

        public OAuthService(ILogger<OAuthService> logger, IServiceProvider serviceProvider)
        {
            this.logger = logger;
            this.serviceProvider = serviceProvider;
        }

        /// <inheritdoc />
        public async Task SetConfigurationAsync(OAuthConfigurationModel configuration)
        {
            this.configuration = configuration;

            using var scope = serviceProvider.CreateScope();
            var databaseConnection = scope.ServiceProvider.GetRequiredService<AisDatabaseConnection>();

            var query = @"SELECT accessToken.`value` AS accessToken, tokenType.`value` AS tokenType, refreshToken.`value` AS refreshToken, expireTime.`value` AS expireTime
FROM DUAL AS temp
LEFT JOIN easy_objects AS accessToken ON accessToken.`key` = ?accessToken
LEFT JOIN easy_objects AS tokenType ON tokenType.`key` = ?tokenType
LEFT JOIN easy_objects AS refreshToken ON refreshToken.`key` = ?refreshToken
LEFT JOIN easy_objects AS expireTime ON expireTime.`key` = ?expireTime";

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
                oAuth.AccessToken = (string) result["accessToken"];
                oAuth.TokenType = (string) result["tokenType"];
                oAuth.RefreshToken = (string) result["refreshToken"];
                oAuth.ExpireTime = result["expireTime"] == null ? DateTime.MinValue : (DateTime) result["expireTime"];
            }
        }

        /// <inheritdoc />
        public Task<string> GetAccessTokenAsync(string apiName)
        {
            throw new NotImplementedException();
        }
    }
}
