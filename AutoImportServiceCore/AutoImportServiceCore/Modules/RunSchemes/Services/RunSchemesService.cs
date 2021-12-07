using System;
using AutoImportServiceCore.Modules.RunSchemes.Enums;
using AutoImportServiceCore.Modules.RunSchemes.Interfaces;
using AutoImportServiceCore.Modules.RunSchemes.Models;
using GeeksCoreLibrary.Core.DependencyInjection.Interfaces;

namespace AutoImportServiceCore.Modules.RunSchemes.Services
{
    public class RunSchemesService : IRunSchemesService, IScopedService
    {
        /// <inheritdoc />
        public TimeSpan GetTimeTillNextRun(RunSchemeModel runScheme)
        {
            switch (runScheme.Type)
            {
                case RunSchemeTypes.Continuous:
                    return TimeByDelay(runScheme.Delay);
                default:
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Calculate the time till the next run based on the delay of the run scheme.
        /// </summary>
        /// <param name="delay">The delay between two runs.</param>
        /// <returns></returns>
        private TimeSpan TimeByDelay(TimeSpan delay)
        {
            var nextDateTime = DateTime.Now.Date;

            while (nextDateTime < DateTime.Now)
                nextDateTime += delay;

            return nextDateTime - DateTime.Now;
        }
    }
}
