using System.Collections.Generic;
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
            var httpApi = action as HttpApiModel;

            LogHelper.LogInformation(logger, LogScopes.RunStartAndStop, httpApi.LogSettings, $"Executing HTTP API in time id: {httpApi.TimeId}, order: {httpApi.Order}");

            return null;
        }
    }
}
