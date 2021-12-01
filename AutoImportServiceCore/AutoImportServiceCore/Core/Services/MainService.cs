using System;
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
        private readonly Dictionary<string, Dictionary<int, ConfigurationsWorker>> activeConfigurations;

        /// <summary>
        /// Creates a new instance of <see cref="MainService"/>.
        /// </summary>
        public MainService()
        {
            activeConfigurations = new Dictionary<string, Dictionary<int, ConfigurationsWorker>>();
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

                activeConfigurations.Add(configuration.ServiceName, new Dictionary<int, ConfigurationsWorker>());

                foreach (var runScheme in configuration.RunSchemes)
                {
                    var worker = new ConfigurationsWorker($"{configuration.ServiceName} (Time id: {runScheme.TimeId})", runScheme);
                    var cancellationToken = new CancellationToken();
                    var thread = new Thread(() => StartConfiguration(worker, cancellationToken));
                    activeConfigurations[configuration.ServiceName].Add(runScheme.TimeId, worker);
                    thread.Start();
                }
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

        /// <summary>
        /// Starts a new <see cref="ConfigurationsWorker"/> for the specified configuration and run scheme.
        /// </summary>
        /// <param name="name">The name of the worker.</param>
        /// <param name="runScheme">The run scheme of the worker.</param>
        private async void StartConfiguration(ConfigurationsWorker worker, CancellationToken cancellationToken)
        {
            await worker.StartAsync(cancellationToken);
            await worker.ExecuteTask;
            Console.WriteLine("Ended");
        }
    }
}
