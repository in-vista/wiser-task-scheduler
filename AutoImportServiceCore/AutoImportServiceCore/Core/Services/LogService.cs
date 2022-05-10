using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AutoImportServiceCore.Core.Enums;
using AutoImportServiceCore.Core.Interfaces;
using AutoImportServiceCore.Core.Models;
using GeeksCoreLibrary.Core.DependencyInjection.Interfaces;
using GeeksCoreLibrary.Core.Models;
using GeeksCoreLibrary.Modules.Databases.Interfaces;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace AutoImportServiceCore.Core.Services
{
    public class LogService : ILogService, ISingletonService
    {
        //private readonly IDatabaseHelpersService databaseHelpersService;

        public LogService(/*IDatabaseHelpersService databaseHelpersService*/)
        {
            //this.databaseHelpersService = databaseHelpersService;
        }

        /// <inheritdoc />
        public async Task LogDebug<T>(ILogger<T> logger, LogScopes logScope, LogSettings logSettings, string message, string configurationName, int timeId = -1, int order = -1)
        {
            await Log(logger, LogLevel.Debug, logScope, logSettings, message, configurationName, timeId, order);
        }

        /// <inheritdoc />
        public async Task LogInformation<T>(ILogger<T> logger, LogScopes logScope, LogSettings logSettings, string message, string configurationName, int timeId = -1, int order = -1)
        {
            await Log(logger, LogLevel.Information, logScope, logSettings, message, configurationName, timeId, order);
        }

        /// <inheritdoc />
        public async Task LogWarning<T>(ILogger<T> logger, LogScopes logScope, LogSettings logSettings, string message, string configurationName, int timeId = -1, int order = -1)
        {
            await Log(logger, LogLevel.Warning, logScope, logSettings, message, configurationName, timeId, order);
        }

        /// <inheritdoc />
        public async Task LogError<T>(ILogger<T> logger, LogScopes logScope, LogSettings logSettings, string message, string configurationName, int timeId = -1, int order = -1)
        {
            await Log(logger, LogLevel.Error, logScope, logSettings, message, configurationName, timeId, order);
        }

        /// <inheritdoc />
        public async Task LogCritical<T>(ILogger<T> logger, LogScopes logScope, LogSettings logSettings, string message, string configurationName, int timeId = -1, int order = -1)
        {
            await Log(logger, LogLevel.Critical, logScope, logSettings, message, configurationName, timeId, order);
        }

        /// <inheritdoc />
        public async Task Log<T>(ILogger<T> logger, LogLevel logLevel, LogScopes logScope, LogSettings logSettings, string message, string configurationName, int timeId = -1, int order = -1)
        {
            if (logLevel < logSettings.LogMinimumLevel)
            {
                return;
            }

            switch (logScope)
            {
                // Log the message if the scope is allowed to log or if log is at least a warning.
                case LogScopes.StartAndStop when logSettings.LogStartAndStop || logLevel > LogLevel.Information:
                case LogScopes.RunStartAndStop when logSettings.LogRunStartAndStop || logLevel > LogLevel.Information:
                case LogScopes.RunBody when logSettings.LogRunBody || logLevel > LogLevel.Information:
                {
                    //await databaseHelpersService.CheckAndUpdateTablesAsync(new List<string> {WiserTableNames.AisLogs});

                    using var configurationServiceNameDisposable = LogContext.PushProperty("configuration_service_name", configurationName);
                    using var timeIdDisposable = LogContext.PushProperty("time_id", timeId);
                    using var orderDisposable = LogContext.PushProperty("order", order);
                    logger.Log(logLevel, message);
                    break;
                }

                // Stop when the scope is evaluated above but is not allowed to log, to prevent the default exception to be thrown.
                case LogScopes.StartAndStop:
                case LogScopes.RunStartAndStop:
                case LogScopes.RunBody:
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(logScope), logScope.ToString());
            }
        }
    }
}
