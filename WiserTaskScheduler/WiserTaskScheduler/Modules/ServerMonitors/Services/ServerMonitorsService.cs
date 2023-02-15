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
using HelperLibrary;
using Microsoft.Extensions.DependencyInjection;
using GeeksCoreLibrary.Modules.Databases.Interfaces;
using GeeksCoreLibrary.Modules.Objects.Interfaces;
using GeeksCoreLibrary.Modules.GclReplacements.Interfaces;
using Microsoft.Extensions.Options;
using GeeksCoreLibrary.Core.Models;
using GeeksCoreLibrary.Core.Services;
using GeeksCoreLibrary.Modules.Communication.Services;
using GeeksCoreLibrary.Modules.DataSelector.Services;
using GeeksCoreLibrary.Modules.Templates.Services;

namespace WiserTaskScheduler.Modules.ServerMonitors.Services
{
    internal class ServerMonitorsService : IServerMonitorsService, IActionsService, IScopedService
    {
        private readonly IServiceProvider serviceProvider;
        private readonly ILogService logService;
        private readonly ILogger<ServerMonitorsService> logger;

        private string connectionString;

        private bool diskThresholdReached;

        DriveInfo[] allDrives = DriveInfo.GetDrives();
        string receiver;
        string subject;
        string body;


        Dictionary<string, bool> diskThresholds = new Dictionary<string, bool>();

        public ServerMonitorsService(IServiceProvider serviceProvider, ILogService logService, ILogger<ServerMonitorsService> logger)
        {
            this.serviceProvider = serviceProvider;
            this.logService = logService;
            this.logger = logger;
        }


        public Task InitializeAsync(ConfigurationModel configuration, HashSet<string> tablesToOptimize)
        {
            connectionString = configuration.ConnectionString;
            return Task.CompletedTask;
        }

        public async Task<JObject> Execute(ActionModel action, JObject resultSets, string configurationServiceName)
        {
            var monitorItem = (ServerMonitorModel)action;
            using var scope = serviceProvider.CreateScope();
            using var databaseConnection = scope.ServiceProvider.GetRequiredService<IDatabaseConnection>();

            await databaseConnection.ChangeConnectionStringsAsync(connectionString, connectionString);

            // Wiser Items Service requires dependency injection that results in the need of MVC services that are unavailable.
            // Get all other services and create the Wiser Items Service with one of the services missing.
            var objectService = scope.ServiceProvider.GetRequiredService<IObjectsService>();
            var stringReplacementsService = scope.ServiceProvider.GetRequiredService<IStringReplacementsService>();
            var databaseHelpersService = scope.ServiceProvider.GetRequiredService<IDatabaseHelpersService>();
            var gclSettings = scope.ServiceProvider.GetRequiredService<IOptions<GclSettings>>();
            var wiserItemsServiceLogger = scope.ServiceProvider.GetRequiredService<ILogger<WiserItemsService>>();
            var gclCommunicationsServiceLogger = scope.ServiceProvider.GetRequiredService<ILogger<CommunicationsService>>();
            var dataSelectorsServiceLogger = scope.ServiceProvider.GetRequiredService<ILogger<DataSelectorsService>>();

            var templatesService = new TemplatesService(null, gclSettings, databaseConnection, stringReplacementsService, null, null, null, null, null, null, null, null, null, databaseHelpersService);
            var dataSelectorsService = new DataSelectorsService(gclSettings, databaseConnection, stringReplacementsService, templatesService, null, null, dataSelectorsServiceLogger, null);
            var wiserItemsService = new WiserItemsService(databaseConnection, objectService, stringReplacementsService, dataSelectorsService, databaseHelpersService, gclSettings, wiserItemsServiceLogger);
            var gclCommunicationsService = new CommunicationsService(gclSettings, gclCommunicationsServiceLogger, wiserItemsService, databaseConnection, databaseHelpersService);

            int threshold = monitorItem.Threshold;

            //create the right performanceCounter variable types.
            PerformanceCounter cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            PerformanceCounter ramCounter = new PerformanceCounter("Memory", "Available MBytes");


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
                        //set the right values for the email.
                        receiver = monitorItem.EmailAddressForWarning;
                        subject = "Low disk space";
                        body = $"Disk {disk.Name} has low space:";

                        await logService.LogInformation(logger, LogScopes.RunStartAndStop, monitorItem.LogSettings, $"Disk {disk} has {disk.TotalFreeSpace}Bytes of free space", configurationServiceName, monitorItem.TimeId, monitorItem.Order);

                        //check if the threshold is higher then the free space Available.
                        if (percentage < threshold)
                        {
                            await logService.LogInformation(logger, LogScopes.RunStartAndStop, monitorItem.LogSettings, $"Disk {disk.Name} doesn't have much space left", configurationServiceName, monitorItem.TimeId, monitorItem.Order);
                            //only send an email if the disk threshold hasn't already been reached.
                            if(!diskThresholdReached)
                            {
                                diskThresholdReached = true;
                                await gclCommunicationsService.SendEmailAsync("nielsmoone@happyhorizon.com", "Low disk Usage", $"Disk {disk.Name} is low on space");
                            }
                        }   
                        else
                        {
                            diskThresholdReached = false;
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
