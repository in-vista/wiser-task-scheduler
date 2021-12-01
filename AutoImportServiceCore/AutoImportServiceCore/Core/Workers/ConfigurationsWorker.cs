using System.Threading.Tasks;
using AutoImportServiceCore.Modules.RunSchemes.Models;

namespace AutoImportServiceCore.Core.Workers
{
    /// <summary>
    /// The <see cref="ConfigurationsWorker"/> is used to run a run scheme from a configuration from Wiser.
    /// </summary>
    public class ConfigurationsWorker : BaseWorker
    {
        /// <summary>
        /// Creates a new instance of <see cref="ConfigurationsWorker"/>.
        /// </summary>
        /// <param name="name">The name of the worker.</param>
        /// <param name="runScheme">The run scheme of the worker.</param>
        public ConfigurationsWorker(string name, RunSchemeModel runScheme) : base(name, runScheme) {}

        /// <inheritdoc />
        protected override async Task ExecuteActionAsync()
        {

        }
    }
}
