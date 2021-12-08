using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AutoImportServiceCore.Core.Interfaces;
using AutoImportServiceCore.Core.Models;
using AutoImportServiceCore.Core.Workers;
using AutoImportServiceCore.Modules.RunSchemes.Interfaces;
using AutoImportServiceCore.Modules.RunSchemes.Models;
using GeeksCoreLibrary.Core.DependencyInjection.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AutoImportServiceCore.Core.Services
{
    /// <summary>
    /// A service to manage all AIS configurations that are provided by Wiser.
    /// </summary>
    public class MainService : IMainService, ISingletonService
    {
        private readonly IServiceProvider serviceProvider;
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, ConfigurationsWorker>> activeConfigurations;

        /// <summary>
        /// Creates a new instance of <see cref="MainService"/>.
        /// </summary>
        public MainService(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
            //var a = this.serviceProvider.GetRequiredService<ILogger<BaseWorker>>();
            //var a = this.serviceProvider.GetRequiredService<IRunSchemesService>();

            using (var scope = serviceProvider.CreateScope())
            {
                var a = scope.ServiceProvider.GetRequiredService<IRunSchemesService>();
            }

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

                // TODO check if time id already is used, do nothing if time ids are double.
                foreach (var runScheme in configuration.RunSchemes)
                {
                    var thread = new Thread(() => StartConfiguration(configuration.ServiceName, runScheme));
                    thread.Start();
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
                //TODO try catch voor dispose
                await configurationStopTasks[i];
                configurationStopTasks[i].Dispose();
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

        /// <summary>
        /// Starts a new <see cref="ConfigurationsWorker"/> for the specified configuration and run scheme.
        /// </summary>
        /// <param name="name">The name of the worker.</param>
        /// <param name="runScheme">The run scheme of the worker.</param>
        private async void StartConfiguration(string name, RunSchemeModel runScheme)
        {
            using (var scope = serviceProvider.CreateScope())
            {
                var worker = scope.ServiceProvider.GetRequiredService<ConfigurationsWorker>();
                worker.Initialize($"{name} (Time id: {runScheme.TimeId})", runScheme);
                activeConfigurations[name].TryAdd(runScheme.TimeId, worker);
                await worker.StartAsync(new CancellationToken());
                await worker.ExecuteTask; // Keep scope alive until worker stops.
            }
        }
    }
}
