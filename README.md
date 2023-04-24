# Wiser Task Scheduler (WTS)
The Wiser Task scheduler (WTS in short) is a .net core service which can be used to run specific tasks in various time-schedules. The WTS is configured with JSON-based configuration files. These configuration files can also be read directly from a Wiser account if Wiser is in place.

## Prerequisites
[.NET Desktop runtime 7](https://dotnet.microsoft.com/en-us/download/dotnet/7.0) (x64) needs to be installed on the server

# Installation on a Windows server
1. Build the project (**not** as single file)
2. Copy all build files to the desired folder on the server
3. Setup the Appsettings.json file (see documentation)
4. Open the command prompt (CMD) or PowerShell with administrator rights
5. Run this command: `sc.exe create "<Service name>" binpath="<Service exe location>";`<br>- change `<Service name>` with a name you prefer ('WiserTaskScheduler' for example)<br>- change `<Service exe location>` to the location of the .exe file (for example: 'c:\wts\WiserTaskScheduler.exe')
6. Open the services panel in Windows (you can run 'msc' via the Start menu)
7. Find the newly created service and open its properties
8. Change the startup type to 'automatic (delayed start)'
9. Start the service
10. Wait untill the service is started and check the log files for a succesfull startup

## Updating the Wiser Task Scheduler
The Wiser Task Scheduler comes with a full automatic updater. This updater can alos be build and deployed on the server. In that case you don't need to update the WTS manually. This can come in handy if you have multiple servers running the WTS. You then only need to create a new build and releaes it on Github.

If you want to update manually follow these steps:
1. Build a new version of the WTS
2. Open the services panel in Windows (you can run 'msc' via the Start menu)
3. Find the service (usually called 'Wiser Task Scheduler') and stop it
4. Copy the newly built files (without the appsettings.json) to the folder on the server
5. Start the service via the services panel
6. Wait untill the service is started and check the log files for a succesfull startup

## De-installing the service
1. Open the command prompt (CMD) or PowerShell with administrator rights
2. Run this command: `sc.exe delete "<Service naam>"`<br>- change `<Service name>` with the correct name of the service
3. If you want to completely remove the WTS then also remove the files from the server 
