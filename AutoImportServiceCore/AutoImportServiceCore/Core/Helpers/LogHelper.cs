using System;
using AutoImportServiceCore.Core.Enums;
using AutoImportServiceCore.Core.Models;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace AutoImportServiceCore.Core.Helpers
{
    public static class LogHelper
    {
        /// <summary>
        /// Log as debug message.
        /// </summary>
        /// <typeparam name="T">The type of the caller.</typeparam>
        /// <param name="logger">The logger to use.</param>
        /// <param name="logScope">The scope of the message of log.</param>
        /// <param name="logSettings">The log settings of the caller.</param>
        /// <param name="message">The message to log.</param>
        public static void LogDebug<T>(ILogger<T> logger, LogScopes logScope, LogSettings logSettings, string message, string configurationName = "", int timeId = -1, int order = -1)
        {
            Log(logger, LogLevel.Debug, logScope, logSettings, message, configurationName, timeId, order);
        }

        /// <summary>
        /// Log information.
        /// </summary>
        /// <typeparam name="T">The type of the caller.</typeparam>
        /// <param name="logger">The logger to use.</param>
        /// <param name="logScope">The scope of the message of log.</param>
        /// <param name="logSettings">The log settings of the caller.</param>
        /// <param name="message">The message to log.</param>
        public static void LogInformation<T>(ILogger<T> logger, LogScopes logScope, LogSettings logSettings, string message, string configurationName = "", int timeId = -1, int order = -1)
        {
            Log(logger, LogLevel.Information, logScope, logSettings, message, configurationName, timeId, order);
        }

        /// <summary>
        /// Log a warning.
        /// </summary>
        /// <typeparam name="T">The type of the caller.</typeparam>
        /// <param name="logger">The logger to use.</param>
        /// <param name="logScope">The scope of the message of log.</param>
        /// <param name="logSettings">The log settings of the caller.</param>
        /// <param name="message">The message to log.</param>
        public static void LogWarning<T>(ILogger<T> logger, LogScopes logScope, LogSettings logSettings, string message, string configurationName = "", int timeId = -1, int order = -1)
        {
            Log(logger, LogLevel.Warning, logScope, logSettings, message, configurationName, timeId, order);
        }

        /// <summary>
        /// Log an error.
        /// </summary>
        /// <typeparam name="T">The type of the caller.</typeparam>
        /// <param name="logger">The logger to use.</param>
        /// <param name="logScope">The scope of the message of log.</param>
        /// <param name="logSettings">The log settings of the caller.</param>
        /// <param name="message">The message to log.</param>
        public static void LogError<T>(ILogger<T> logger, LogScopes logScope, LogSettings logSettings, string message, string configurationName = "", int timeId = -1, int order = -1)
        {
            Log(logger, LogLevel.Error, logScope, logSettings, message, configurationName, timeId, order);
        }

        /// <summary>
        /// Log critical.
        /// </summary>
        /// <typeparam name="T">The type of the caller.</typeparam>
        /// <param name="logger">The logger to use.</param>
        /// <param name="logScope">The scope of the message of log.</param>
        /// <param name="logSettings">The log settings of the caller.</param>
        /// <param name="message">The message to log.</param>
        public static void LogCritical<T>(ILogger<T> logger, LogScopes logScope, LogSettings logSettings, string message, string configurationName = "", int timeId = -1, int order = -1)
        {
            Log(logger, LogLevel.Critical, logScope, logSettings, message, configurationName, timeId, order);
        }

        /// <summary>
        /// Log a message.
        /// </summary>
        /// <typeparam name="T">The type of the caller.</typeparam>
        /// <param name="logger">The logger to use.</param>
        /// <param name="logLevel">The level of the log to write to.</param>
        /// <param name="logScope">The scope of the message of log.</param>
        /// <param name="logSettings">The log settings of the caller.</param>
        /// <param name="message">The message to log.</param>
        public static void Log<T>(ILogger<T> logger, LogLevel logLevel, LogScopes logScope, LogSettings logSettings, string message, string configurationName = "", int timeId = -1, int order = -1)
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
