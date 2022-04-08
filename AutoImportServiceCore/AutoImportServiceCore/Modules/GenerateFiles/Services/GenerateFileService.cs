using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AutoImportServiceCore.Core.Enums;
using AutoImportServiceCore.Core.Helpers;
using AutoImportServiceCore.Core.Interfaces;
using AutoImportServiceCore.Core.Models;
using AutoImportServiceCore.Modules.Body.Interfaces;
using AutoImportServiceCore.Modules.GenerateFiles.Interfaces;
using AutoImportServiceCore.Modules.GenerateFiles.Models;
using GeeksCoreLibrary.Core.DependencyInjection.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace AutoImportServiceCore.Modules.GenerateFiles.Services
{
    /// <summary>
    /// A service for a generate file action.
    /// </summary>
    public class GenerateFileService : IGenerateFileService, IActionsService, IScopedService
    {
        private readonly IBodyService bodyService;
        private readonly ILogger<GenerateFileService> logger;

        /// <summary>
        /// Create a new instance of <see cref="GenerateFileService"/>.
        /// </summary>
        /// <param name="bodyService"></param>
        /// <param name="logger"></param>
        public GenerateFileService(IBodyService bodyService, ILogger<GenerateFileService> logger)
        {
            this.bodyService = bodyService;
            this.logger = logger;
        }

        /// <inheritdoc />
        public async Task Initialize(ConfigurationModel configuration) {}

        /// <inheritdoc />
        public async Task<JObject> Execute(ActionModel action, JObject resultSets)
        {
            var generateFile = (GenerateFileModel) action;
            return await GenerateFile(generateFile, ReplacementHelper.EmptyRows, resultSets);
        }

        /// <summary>
        /// Generate a file.
        /// </summary>
        /// <param name="generateFile">THe information for the file to be generated.</param>
        /// <param name="rows">The indexes/rows of the array, passed to be used if '[i]' is used in the key.</param>
        /// <param name="resultSets">The result sets from previous actions in the same run.</param>
        /// <returns></returns>
        private async Task<JObject> GenerateFile(GenerateFileModel generateFile, List<int> rows, JObject resultSets)
        {
            var fileLocation = generateFile.FileLocation;
            var fileName = generateFile.FileName;

            // Replace the file location and name if a result set is used.
            if (!String.IsNullOrWhiteSpace(generateFile.UseResultSet))
            {
                var keyParts = generateFile.UseResultSet.Split('.');
                var usingResultSet = ResultSetHelper.GetCorrectObject<JObject>(generateFile.UseResultSet, ReplacementHelper.EmptyRows, resultSets);
                var remainingKey = keyParts.Length > 1 ? generateFile.UseResultSet.Substring(keyParts[0].Length + 1) : "";

                var fileLocationTuple = ReplacementHelper.PrepareText(fileLocation, usingResultSet, remainingKey);
                var fileNameTuple = ReplacementHelper.PrepareText(fileName, usingResultSet, remainingKey);

                fileLocation = ReplacementHelper.ReplaceText(fileLocationTuple.Item1, rows, fileLocationTuple.Item2, usingResultSet);
                fileName = ReplacementHelper.ReplaceText(fileNameTuple.Item1, rows, fileNameTuple.Item2, usingResultSet);
            }

            LogHelper.LogInformation(logger, LogScopes.RunStartAndStop, generateFile.LogSettings, $"Generating file '{fileName}' at '{fileLocation}'.");

            var body = bodyService.GenerateBody(generateFile.Body, rows, resultSets);

            var fileGenerated = false;
            try
            {
                await File.WriteAllTextAsync($"{fileLocation}{(fileLocation.EndsWith('/') || fileLocation.EndsWith('\\') ? "" : "/")}{fileName}", body);
                fileGenerated = true;
                LogHelper.LogInformation(logger, LogScopes.RunStartAndStop, generateFile.LogSettings, $"File '{fileName}' generated at '{fileLocation}'.");
            }
            catch (Exception e)
            {
                LogHelper.LogError(logger, LogScopes.RunStartAndStop, generateFile.LogSettings, $"Failed to generate file '{fileName}' at '{fileLocation}'.\n{e}");
            }

            var result = $"{{ 'FileName': '{fileName}', 'FileLocation': '{fileLocation}', 'FileGenerated': {fileGenerated.ToString().ToLower()} }}";
            return JObject.Parse(result);
        }
    }
}
