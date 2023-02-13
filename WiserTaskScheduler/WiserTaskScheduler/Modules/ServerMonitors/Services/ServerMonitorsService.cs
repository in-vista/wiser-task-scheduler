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
using WiserTaskScheduler.Modules.ServerMonitors.Enums;
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

        private bool AboveThreshold;

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
            int threshold = monitorItem.Threshold;

            //create the right performanceCounter variable types.
            PerformanceCounter cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            PerformanceCounter ramCounter = new PerformanceCounter("Memory", "Available MBytes");
            DriveInfo[] allDrives = DriveInfo.GetDrives();

            double cpuValue;
            double firstValue;

            //Check which type of server monitor is used.
            //Cpu is default Value.
            switch (monitorItem.ServerMonitorType)
            {
                case ServerMonitorTypes.Disk:
                    foreach (var disk in allDrives)
                    {
                        //calculate the percentage of free space availible and see if it matches with the given threshold.
                        double freeSpace = disk.TotalFreeSpace;
                        double fullSpace = disk.TotalSize;
                        double percentage = freeSpace / fullSpace * 100;

                        await logService.LogInformation(logger, LogScopes.RunStartAndStop, monitorItem.LogSettings, $"Disk {disk} has {disk.TotalFreeSpace}Bytes of free space", configurationServiceName, monitorItem.TimeId, monitorItem.Order);

                        if (percentage < threshold)
                        {
                            await logService.LogInformation(logger, LogScopes.RunStartAndStop, monitorItem.LogSettings, $"Disk {disk.Name} doesn't have much space left", configurationServiceName, monitorItem.TimeId, monitorItem.Order);
                            //if there is not enough space.
                            //TODO send email to company informing about space issue.
                        }
                    }
                    break;
                case ServerMonitorTypes.Ram:
                    double ramValue = ramCounter.NextValue();
                    await logService.LogInformation(logger, LogScopes.RunStartAndStop, monitorItem.LogSettings, $"RAM is: {ramValue}MB available", configurationServiceName, monitorItem.TimeId, monitorItem.Order);
                    break;
                case ServerMonitorTypes.Cpu:
                    //NextValue has to be run twice for cpu because the first one is always 0.
                    firstValue = cpuCounter.NextValue();
                    cpuValue = cpuCounter.NextValue();
                    await logService.LogInformation(logger, LogScopes.RunStartAndStop, monitorItem.LogSettings, $"CPU is: {cpuValue}%", configurationServiceName, monitorItem.TimeId, monitorItem.Order);
                    break;
                default:
                    break;
            }


            return new JObject
            {
                {"Results", 0}
            };
        }
    }
}
