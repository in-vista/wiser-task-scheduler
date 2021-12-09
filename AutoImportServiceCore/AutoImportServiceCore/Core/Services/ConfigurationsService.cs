using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoImportServiceCore.Core.Enums;
using AutoImportServiceCore.Core.Helpers;
using AutoImportServiceCore.Core.Interfaces;
using AutoImportServiceCore.Core.Models;
using GeeksCoreLibrary.Core.DependencyInjection.Interfaces;
using Microsoft.Extensions.Logging;

namespace AutoImportServiceCore.Core.Services
{
    public class ConfigurationsService : IConfigurationsService, IScopedService
    {
        private readonly ILogger<ConfigurationsService> logger;
        private readonly IActionsServiceFactory actionsServiceFactory;

        private readonly SortedList<int, ActionModel> actions;
        private readonly Dictionary<string, IActionsService> actionsServices;

        /// <inheritdoc />
        public LogSettings LogSettings { get; set; }

        /// <inheritdoc />
        public string Name { get; set; }

        /// <summary>
        /// Creates a new instance of <see cref="ConfigurationsService"/>.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="actionsServiceFactory"></param>
        public ConfigurationsService(ILogger<ConfigurationsService> logger, IActionsServiceFactory actionsServiceFactory)
        {
            this.logger = logger;
            this.actionsServiceFactory = actionsServiceFactory;

            actions = new SortedList<int, ActionModel>();
            actionsServices = new Dictionary<string, IActionsService>();
        }

        /// <inheritdoc />
        public void ExtractActionsFromConfiguration(int timeId, ConfigurationModel configuration)
        {
            var allActions = GetAllActionsFromConfiguration(
                configuration.Queries.ToArray<ActionModel>(),
                configuration.HttpApis.ToArray<ActionModel>()
                );

            foreach (ActionModel action in allActions.Where(action => action.TimeId == timeId))
            {
                action.LogSettings ??= LogSettings;
                actions.Add(action.Order, action);

                if (!actionsServices.ContainsKey(action.GetType().ToString()))
                {
                    actionsServices.Add(action.GetType().ToString(), actionsServiceFactory.GetActionsServiceForAction(action));
                }
            }

            LogHelper.LogInformation(logger, LogScopes.StartAndStop, LogSettings, $"{Name} has {actions.Count} action(s).");
        }

        /// <summary>
        /// Get all the provided action sets if they exist in a single list.
        /// </summary>
        /// <param name="actionSets">The action sets to combine.</param>
        /// <returns></returns>
        private List<ActionModel> GetAllActionsFromConfiguration(params ActionModel[][] actionSets)
        {
            var allActions = new List<ActionModel>();

            if (actions == null)
            {
                return allActions;
            }

            foreach (var actionSet in actionSets)
            {
                if (actionSet != null)
                {
                    allActions.AddRange(actionSet);
                }
            }

            return allActions;
        }

        /// <inheritdoc />
        public async Task ExecuteAsync()
        {
            foreach (var action in actions)
            {
                await actionsServices[action.Value.GetType().ToString()].Execute(action.Value);
            }
        }
    }
}
