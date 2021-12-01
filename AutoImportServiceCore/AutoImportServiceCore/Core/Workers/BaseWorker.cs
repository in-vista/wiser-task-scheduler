using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AutoImportServiceCore.Core.Helpers;
using AutoImportServiceCore.Modules.RunSchemes.Models;
using Microsoft.Extensions.Hosting;

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
        public string Name { get; }

        /// <summary>
        /// Gets the delay between two runs of the worker.
        /// </summary>
        public RunSchemeModel RunScheme { get; }

        private readonly bool runImmediately;

        /// <summary>
        /// Assigns the base values from a derived class.
        /// </summary>
        /// <param name="name">The name of the worker.</param>
        /// <param name="runScheme">The run scheme of the worker.</param>
        /// <param name="runImmediately">True to run the action immediately, false to run at first delayed time.</param>
        protected BaseWorker(string name, RunSchemeModel runScheme, bool runImmediately = false)
        {
            Name = name;
            RunScheme = runScheme;
            this.runImmediately = runImmediately;
        }

        /// <summary>
        /// Handles when the worker is allowed to execute its action.
        /// </summary>
        /// <param name="stoppingToken"></param>
        /// <returns></returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!runImmediately)
            {
                await Task.Delay(RunTimeHelpers.GetTimeTillNextRun(RunScheme.Delay), stoppingToken);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                Console.WriteLine($"{Name} ran at: {DateTime.Now}");
                var stopWatch = new Stopwatch();
                stopWatch.Start();

                await ExecuteActionAsync();

                stopWatch.Stop();

                Console.WriteLine($"{Name} finished at: {DateTime.Now}, time taken: {stopWatch.Elapsed}");

                await Task.Delay(RunTimeHelpers.GetTimeTillNextRun(RunScheme.Delay), stoppingToken);
            }
        }

        /// <summary>
        /// Execute the action of the derived worker.
        /// </summary>
        /// <returns></returns>
        protected abstract Task ExecuteActionAsync();
    }
}
