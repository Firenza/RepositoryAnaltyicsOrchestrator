using Microsoft.Extensions.Configuration;
using RestSharp;
using Serilog;
using Serilog.Events;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace RepositoryAnaltyicsOrchestrator
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var logFileName = "RepositoryAnaltyicsOrchestrator.txt";

            string logFilePath = null;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                logFilePath = $@"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}\RepositoryAnaltyics\{logFileName}";
            }
            else
            {
                logFilePath = $"/var/logs/repository_analtyics/{logFileName}";
            }
        
            Log.Logger = new LoggerConfiguration()
             .MinimumLevel.Debug()
             .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
             .Enrich.FromLogContext()
             .WriteTo.Console()
             .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day)
             .CreateLogger();

            var configuration = new ConfigurationBuilder()
              .AddJsonFile("defaultSettings.json")
              .AddCommandLine(args)
              .AddEnvironmentVariables()
              .Build();

            Log.Logger.Information($"Writing log files to {Path.GetDirectoryName(logFilePath)}");

            // Print out all config data
            foreach (var child in configuration.GetChildren())
            {
                Log.Logger.Information($"{child.Path} ({child.Key}) = {child.Value ?? "(null)"}");
            }

            var config = new RunTimeConfiguration();
            configuration.Bind(config);

            Log.Logger.Information("Config is {@Config}", config);

            if (config.InitialDelayDuration > 0)
            {
                System.Threading.Thread.Sleep(1000 * config.InitialDelayDuration);
            }

            if (string.IsNullOrWhiteSpace(config.Url))
            {
                Log.Logger.Error("You must specify a url for the Repository Analytics API");
                return;
            }

            if (string.IsNullOrWhiteSpace(config.User) && string.IsNullOrWhiteSpace(config.Organization))
            {
                Log.Logger.Error("You must specify a user or an origanization");
                return;
            }
            else if (!string.IsNullOrWhiteSpace(config.User) && !string.IsNullOrWhiteSpace(config.Organization))
            {
                Log.Logger.Error("You can not specify both a user and an origanization");
                return;
            }

            var inDockerEnvVar = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER");
            var runningInContainer = inDockerEnvVar == null || inDockerEnvVar == "true";

            var repositoryAnalyticsOrchestrator = new RepositoryAnalysisOrchestrator(Log.Logger, new RestClient(), runningInContainer);
            await repositoryAnalyticsOrchestrator.OrchestrateAsync(config);

            return;
        }
    }
}
