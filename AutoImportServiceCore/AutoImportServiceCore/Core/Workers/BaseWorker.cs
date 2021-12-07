using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AutoImportServiceCore.Core.Helpers;
using AutoImportServiceCore.Modules.RunSchemes.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AutoImportServiceCore.Core.Workers
{
    /// <summary>
    /// The <see cref="BaseWorker"/> performs shared functionality for all workers.
    /// </summary>
    public abstract class BaseWorker : BackgroundService
    {
        /// <summary>
        /// Gets the name of the worker.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Gets the delay between two runs of the worker.
        /// </summary>
        public RunSchemeModel RunScheme { get; private set; }

        private bool RunImmediately { get; set; }

        private readonly ILogger<BaseWorker> logger;

        protected BaseWorker(ILogger<BaseWorker> logger)
        {
            this.logger = logger;
        }

        /// <summary>
        /// Assigns the base values from a derived class.
        /// </summary>
        /// <param name="name">The name of the worker.</param>
        /// <param name="runScheme">The run scheme of the worker.</param>
        /// <param name="runImmediately">True to run the action immediately, false to run at first delayed time.</param>
        public void Initialize(string name, RunSchemeModel runScheme, bool runImmediately = false)
        {
            if (!String.IsNullOrWhiteSpace(Name))
                return;

            Name = name;
            RunScheme = runScheme;
            RunImmediately = runImmediately;
        }

        /// <summary>
        /// Handles when the worker is allowed to execute its action.
        /// </summary>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                if (!RunImmediately)
                {
                    await Task.Delay(RunTimeHelpers.GetTimeTillNextRun(RunScheme.Delay), stoppingToken);
                }

                while (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogInformation($"{Name} ran at: {DateTime.Now}");
                    var stopWatch = new Stopwatch();
                    stopWatch.Start();

                    await ExecuteActionAsync();

                    stopWatch.Stop();

                    logger.LogInformation($"{Name} finished at: {DateTime.Now}, time taken: {stopWatch.Elapsed}");

                    await Task.Delay(RunTimeHelpers.GetTimeTillNextRun(RunScheme.Delay), stoppingToken);
                }
            }
            catch (TaskCanceledException)
            {
                logger.LogInformation($"{Name} has been stopped after cancel was called.");
            }
            catch (Exception e)
            {
                logger.LogError($"{Name} stopped with exception {e}");
            }
        }

        /// <summary>
        /// Execute the action of the derived worker.
        /// </summary>
        /// <returns></returns>
        protected abstract Task ExecuteActionAsync();
    }
}
