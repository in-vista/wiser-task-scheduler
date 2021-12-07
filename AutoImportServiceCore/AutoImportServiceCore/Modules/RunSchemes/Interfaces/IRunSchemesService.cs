using System;
using AutoImportServiceCore.Modules.RunSchemes.Models;

namespace AutoImportServiceCore.Modules.RunSchemes.Interfaces
{
    public interface IRunSchemesService
    {
        /// <summary>
        /// Get the time the worker has to wait till it can start its next run.
        /// </summary>
        /// <param name="runScheme">The run scheme of the action.</param>
        /// <returns></returns>
        TimeSpan GetTimeTillNextRun(RunSchemeModel runScheme);
    }
}
