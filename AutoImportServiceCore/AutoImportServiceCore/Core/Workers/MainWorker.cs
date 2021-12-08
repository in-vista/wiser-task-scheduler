using System;
using System.Threading;
using System.Threading.Tasks;
using AutoImportServiceCore.Core.Interfaces;
using AutoImportServiceCore.Core.Models;
using AutoImportServiceCore.Modules.RunSchemes.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AutoImportServiceCore.Core.Workers
{
    /// <summary>
    /// The <see cref="MainWorker"/> manages all AIS configurations that are provided by Wiser.
    /// </summary>
    public class MainWorker : BaseWorker
    {
        private readonly IMainService mainService;

        /// <summary>
        /// Creates a new instance of <see cref="MainWorker"/>.
        /// </summary>
        /// <param name="aisSettings">The settings of the AIS for the run scheme.</param>
        /// <param name="mainService"></param>
        public MainWorker(IOptions<AisSettings> aisSettings, IMainService mainService, ILogger<BaseWorker> logger, IRunSchemesService runSchemesService) : base(logger, runSchemesService)
        {
            Initialize("Main", aisSettings.Value.MainRunScheme, true);

            this.mainService = mainService;
        }

        /// <inheritdoc />
        protected override async Task ExecuteActionAsync()
        {
            await mainService.ManageConfigurations();
        }

        /// <inheritdoc />
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("Stopping all configuration workers.");
            await mainService.StopAllConfigurations();
            Console.WriteLine("All configuration workers have stopped.");
            await base.StopAsync(cancellationToken);
        }
    }
}
