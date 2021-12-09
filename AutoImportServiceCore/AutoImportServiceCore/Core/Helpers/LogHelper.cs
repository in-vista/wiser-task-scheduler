using System;
using AutoImportServiceCore.Core.Enums;
using AutoImportServiceCore.Core.Models;
using Microsoft.Extensions.Logging;

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
        public static void LogDebug<T>(ILogger<T> logger, LogScopes logScope, LogSettings logSettings, string message)
        {
            Log(logger, LogLevel.Debug, logScope, logSettings, message);
        }

        /// <summary>
        /// Log information.
        /// </summary>
        /// <typeparam name="T">The type of the caller.</typeparam>
        /// <param name="logger">The logger to use.</param>
        /// <param name="logScope">The scope of the message of log.</param>
        /// <param name="logSettings">The log settings of the caller.</param>
        /// <param name="message">The message to log.</param>
        public static void LogInformation<T>(ILogger<T> logger, LogScopes logScope, LogSettings logSettings, string message)
        {
            Log(logger, LogLevel.Information, logScope, logSettings, message);
        }

        /// <summary>
        /// Log a warning.
        /// </summary>
        /// <typeparam name="T">The type of the caller.</typeparam>
        /// <param name="logger">The logger to use.</param>
        /// <param name="logScope">The scope of the message of log.</param>
        /// <param name="logSettings">The log settings of the caller.</param>
        /// <param name="message">The message to log.</param>
        public static void LogWarning<T>(ILogger<T> logger, LogScopes logScope, LogSettings logSettings, string message)
        {
            Log(logger, LogLevel.Warning, logScope, logSettings, message);
        }

        /// <summary>
        /// Log an error.
        /// </summary>
        /// <typeparam name="T">The type of the caller.</typeparam>
        /// <param name="logger">The logger to use.</param>
        /// <param name="logScope">The scope of the message of log.</param>
        /// <param name="logSettings">The log settings of the caller.</param>
        /// <param name="message">The message to log.</param>
        public static void LogError<T>(ILogger<T> logger, LogScopes logScope, LogSettings logSettings, string message)
        {
            Log(logger, LogLevel.Error, logScope, logSettings, message);
        }

        /// <summary>
        /// Log critical.
        /// </summary>
        /// <typeparam name="T">The type of the caller.</typeparam>
        /// <param name="logger">The logger to use.</param>
        /// <param name="logScope">The scope of the message of log.</param>
        /// <param name="logSettings">The log settings of the caller.</param>
        /// <param name="message">The message to log.</param>
        public static void LogCritical<T>(ILogger<T> logger, LogScopes logScope, LogSettings logSettings, string message)
        {
            Log(logger, LogLevel.Critical, logScope, logSettings, message);
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
        public static void Log<T>(ILogger<T> logger, LogLevel logLevel, LogScopes logScope, LogSettings logSettings, string message)
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
                    logger.Log(logLevel, message);
                    break;

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
