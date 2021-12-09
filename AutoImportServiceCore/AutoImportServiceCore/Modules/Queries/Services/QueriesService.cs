using System.Threading.Tasks;
using AutoImportServiceCore.Core.Enums;
using AutoImportServiceCore.Core.Helpers;
using AutoImportServiceCore.Core.Interfaces;
using AutoImportServiceCore.Core.Models;
using AutoImportServiceCore.Modules.Queries.Interfaces;
using AutoImportServiceCore.Modules.Queries.Models;
using GeeksCoreLibrary.Core.DependencyInjection.Interfaces;
using Microsoft.Extensions.Logging;

namespace AutoImportServiceCore.Modules.Queries.Services
{
    /// <summary>
    /// A service for a query action.
    /// </summary>
    public class QueriesService : IQueriesService, IActionsService, IScopedService
    {
        private readonly ILogger<QueriesService> logger;

        /// <summary>
        /// Creates a new instance of <see cref="QueriesService"/>.
        /// </summary>
        /// <param name="logger"></param>
        public QueriesService(ILogger<QueriesService> logger)
        {
            this.logger = logger;
        }

        /// <inheritdoc />
        public async Task Execute(ActionModel action)
        {
            var query = action as QueryModel;

            LogHelper.LogInformation(logger, LogScopes.RunStartAndStop, query.LogSettings, $"Executing query in time id: {query.TimeId}, order: {query.Order}");
        }
    }
}
