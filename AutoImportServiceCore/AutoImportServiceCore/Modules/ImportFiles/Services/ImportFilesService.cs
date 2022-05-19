using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AutoImportServiceCore.Core.Helpers;
using AutoImportServiceCore.Core.Interfaces;
using AutoImportServiceCore.Core.Models;
using AutoImportServiceCore.Modules.ImportFiles.Interfaces;
using AutoImportServiceCore.Modules.ImportFiles.Models;
using GeeksCoreLibrary.Core.DependencyInjection.Interfaces;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace AutoImportServiceCore.Modules.ImportFiles.Services
{
    public class ImportFilesService : IImportFilesService, IActionsService, IScopedService
    {
        private readonly ILogger<ImportFilesService> logger;

        public ImportFilesService(ILogger<ImportFilesService> logger)
        {
            this.logger = logger;
        }

        /// <inheritdoc />
        public async Task Initialize(ConfigurationModel configuration) { }

        /// <inheritdoc />
        public async Task<JObject> Execute(ActionModel action, JObject resultSets, string configurationServiceName)
        {
            //TODO load multiple files
            var importFile = (ImportFileModel) action;

            if (importFile.Separator.Equals("\\t"))
            {
                importFile.Separator = '\t'.ToString();
            }

            return await ImportFile(importFile, ReplacementHelper.EmptyRows, resultSets, configurationServiceName, importFile.UseResultSet);
        }

        public async Task<JObject> ImportFile(ImportFileModel importFile, List<int> rows, JObject resultSets, string configurationServiceName, string useResultSet)
        {
            var filePath = importFile.FilePath;

            //TODO file path replacements.

            if (!File.Exists(filePath))
            {
                //TODO return result set.
                return null;
            }

            try
            {
                var lines = await File.ReadAllLinesAsync(filePath);
                var jArray = new JArray();

                if (!importFile.HasFieldNames)
                {
                    //TODO check if result set replacements can handle [0][1] combinations, otherwise row in object before array.
                    foreach (var line in lines)
                    {
                        jArray.Add(new JArray(line.Split(importFile.Separator)));
                    }

                    return new JObject
                    {
                        {"Results", jArray}
                    };
                }
                
                var fieldNames = lines[0].Split(importFile.Separator);

                for (var i = 1; i < lines.Length; i++)
                {
                    var row = new JObject();

                    var columns = lines[i].Split(importFile.Separator);
                    for (var j = 0; j < fieldNames.Length; j++)
                    {
                        row.Add(fieldNames[j], columns[j]);
                    }

                    jArray.Add(row);
                }

                //TODO add field names to result set.
                return new JObject
                {
                    {"Results", jArray}
                };
            }
            catch (Exception e)
            {
                //TODO log exception and return result set.

                return null;
            }
        }
    }
}
