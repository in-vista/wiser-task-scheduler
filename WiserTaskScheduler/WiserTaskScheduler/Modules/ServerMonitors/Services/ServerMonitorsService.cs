using GeeksCoreLibrary.Core.DependencyInjection.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using WiserTaskScheduler.Core.Interfaces;
using WiserTaskScheduler.Core.Models;
using WiserTaskScheduler.Core.Enums;
using WiserTaskScheduler.Modules.ServerMonitors.Interfaces;
using WiserTaskScheduler.Modules.ServerMonitors.Models;
using WiserTaskScheduler.Modules.Body.Interfaces;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Logging;
using WiserTaskScheduler.Modules.Queries.Services;
using System.IO;

namespace WiserTaskScheduler.Modules.ServerMonitors.Services
{
    internal class ServerMonitorsService : IServerMonitorsService, IActionsService, IScopedService
    {
        private readonly IServiceProvider serviceProvider;
        private readonly ILogService logService;
        private readonly ILogger<ServerMonitorsService> logger;

        private string connectionString;

        public ServerMonitorsService(IServiceProvider serviceProvider, ILogService logService, ILogger<ServerMonitorsService> logger)
        {
            this.serviceProvider = serviceProvider;
            this.logService = logService;
            this.logger = logger;
        }


        public Task InitializeAsync(ConfigurationModel configuration)
        {
            connectionString = configuration.ConnectionString;
            return Task.CompletedTask;
        }

        public async Task<JObject> Execute(ActionModel action, JObject resultSets, string configurationServiceName)
        {
            var monitorItem = (ServerMonitorModel)action;

            //create the right variable types
            PerformanceCounter cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            PerformanceCounter ramCounter = new PerformanceCounter("Memory", "Available MBytes");
            DriveInfo[] allDrives = DriveInfo.GetDrives();

            //get the next value
            //NextValue has to be run twice for cpu because the first one is always 0
            double firstValue = cpuCounter.NextValue();
            double cpuValue = cpuCounter.NextValue();
            double ramValue = ramCounter.NextValue();

                
            await logService.LogInformation(logger, LogScopes.RunStartAndStop, monitorItem.LogSettings, $"CPU is: {cpuValue}%", configurationServiceName, monitorItem.TimeId, monitorItem.Order);
            await logService.LogInformation(logger, LogScopes.RunStartAndStop, monitorItem.LogSettings, $"RAM is: {ramValue}%", configurationServiceName, monitorItem.TimeId, monitorItem.Order);

            foreach (var disk in allDrives)
            {
                await logService.LogInformation(logger, LogScopes.RunStartAndStop, monitorItem.LogSettings, $"Disk {disk} has {disk.TotalFreeSpace}Bytes of free space", configurationServiceName, monitorItem.TimeId, monitorItem.Order);

                //calculate the percentage of free space availible and see if it matches with the given threshold
                double freeSpace = disk.AvailableFreeSpace;
                double fullSpace = disk.TotalSize;
                double thresholdFull = 20;
                double percentage = freeSpace / fullSpace * 100;
                if (percentage < thresholdFull)
                {
                    //if there is not enough space
                    //TODO send email to company informing about space issue
                }
            }



            return new JObject
            {
                {"Results", cpuValue}
            };
        }
    }
}
