using System.Threading.Tasks;
using AutoImportServiceCore.Core.Models.OAuth;

namespace AutoImportServiceCore.Core.Interfaces
{
    public interface IOAuthService
    {
        /// <summary>
        /// Set the configuration to be used for OAuth calls.
        /// </summary>
        /// <param name="configuration">The configuration to set.</param>
        /// <returns></returns>
        Task SetConfigurationAsync(OAuthConfigurationModel configuration);

        /// <summary>
        /// Get the access token of the specified API.
        /// </summary>
        /// <param name="apiName">The name of the API to get the access token from.</param>
        /// <returns>Returns the access token to the API.</returns>
        Task<string> GetAccessTokenAsync(string apiName);
    }
}
