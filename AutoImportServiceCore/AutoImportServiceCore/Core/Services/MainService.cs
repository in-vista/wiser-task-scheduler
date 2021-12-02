using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AutoImportServiceCore.Core.Interfaces;
using AutoImportServiceCore.Core.Models;
using AutoImportServiceCore.Core.Workers;
using GeeksCoreLibrary.Core.DependencyInjection.Interfaces;
using Newtonsoft.Json;

namespace AutoImportServiceCore.Core.Services
{
    /// <summary>
    /// A service to manage all AIS configurations that are provided by Wiser.
    /// </summary>
    public class MainService : IMainService, ISingletonService
    {
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, ConfigurationsWorker>> activeConfigurations;

        /// <summary>
        /// Creates a new instance of <see cref="MainService"/>.
        /// </summary>
        public MainService()
        {
            activeConfigurations = new ConcurrentDictionary<string, ConcurrentDictionary<int, ConfigurationsWorker>>();
        }

        /// <inheritdoc />
        public async Task ManageConfigurations()
        {
            var configurations = await GetConfigurations();

            foreach (var configuration in configurations)
            {
                if (activeConfigurations.ContainsKey(configuration.ServiceName))
                {
                    continue;
                }

                activeConfigurations.TryAdd(configuration.ServiceName, new ConcurrentDictionary<int, ConfigurationsWorker>());

                foreach (var runScheme in configuration.RunSchemes)
                {
                    var worker = new ConfigurationsWorker($"{configuration.ServiceName} (Time id: {runScheme.TimeId})", runScheme);
                    var cancellationToken = new CancellationToken();
                    activeConfigurations[configuration.ServiceName].TryAdd(runScheme.TimeId, worker);
                    await worker.StartAsync(cancellationToken);
                }
            }
        }

        /// <inheritdoc />
        public async Task StopAllConfigurations()
        {
            List<Task> configurationStopTasks = new List<Task>();
            var cancellationToken = new CancellationToken();

            foreach (var configuration in activeConfigurations)
            {
                foreach (var worker in configuration.Value)
                {
                    configurationStopTasks.Add(worker.Value.StopAsync(cancellationToken));
                }
            }
            
            for(var i = 0; i < configurationStopTasks.Count; i++)
            {
                await configurationStopTasks[i];
                Console.WriteLine($"Stopped {i + 1}/{configurationStopTasks.Count} configurations workers.");
            }
        }

        /// <summary>
        /// Retrieve all configurations.
        /// </summary>
        /// <returns>Returns the configurations</returns>
        private async Task<IEnumerable<ConfigurationModel>> GetConfigurations()
        {
            var configurations = new List<ConfigurationModel>();

            var configuration = JsonConvert.DeserializeObject<ConfigurationModel>(await File.ReadAllTextAsync(@"C:\Ontwikkeling\Intern\autoimportservice_core\AISCoreTestSettings.json"));
            configurations.Add(configuration);

            return configurations;
        }
    }
}
