using System.Threading.Tasks;
using GeeksCoreLibrary.Core.DependencyInjection.Interfaces;
using Microsoft.Extensions.Logging;

namespace AutoImportServiceCore.Core.Workers
{
    /// <summary>
    /// The <see cref="ConfigurationsWorker"/> is used to run a run scheme from a configuration from Wiser.
    /// </summary>
    public class ConfigurationsWorker : BaseWorker
    {
        /// <inheritdoc />
        public ConfigurationsWorker(ILogger<BaseWorker> logger) : base(logger)
        {
        }

        /// <inheritdoc />
        protected override async Task ExecuteActionAsync()
        {

        }
    }
}
