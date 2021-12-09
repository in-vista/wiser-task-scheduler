using System.Threading.Tasks;
using AutoImportServiceCore.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace AutoImportServiceCore.Core.Workers
{
    /// <summary>
    /// The <see cref="ConfigurationsWorker"/> is used to run a run scheme from a configuration from Wiser.
    /// </summary>
    public class ConfigurationsWorker : BaseWorker
    {
        private readonly ILogger<ConfigurationsWorker> logger;

        /// <summary>
        /// Creates a new instance of <see cref="ConfigurationsWorker"/>.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="baseWorkerDependencyAggregate"></param>
        public ConfigurationsWorker(ILogger<ConfigurationsWorker> logger, IBaseWorkerDependencyAggregate baseWorkerDependencyAggregate) : base(baseWorkerDependencyAggregate)
        {
            this.logger = logger;
        }

        /// <inheritdoc />
        protected override async Task ExecuteActionAsync()
        {

        }
    }
}
