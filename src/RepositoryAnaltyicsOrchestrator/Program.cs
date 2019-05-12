using Microsoft.Extensions.Configuration;
using RestSharp;
using Serilog;
using Serilog.Events;
using System;
using System.Threading.Tasks;

namespace RepositoryAnaltyicsOrchestrator
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
             .MinimumLevel.Debug()
             .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
             .Enrich.FromLogContext()
             .WriteTo.Console()
             .CreateLogger();

            var configuration = new ConfigurationBuilder()
              .AddJsonFile("defaultSettings.json")
              .AddCommandLine(args)
              .AddEnvironmentVariables()
              .Build();

            var config = new RunTimeConfiguration();
            configuration.Bind(config);

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

            var repositoryAnalyticsOrchestrator = new RepositoryAnalysisOrchestrator(Log.Logger, new RestClient());
            await repositoryAnalyticsOrchestrator.OrchestrateAsync(config.Url, config.User, config.Organization, config.AsOf, config.BatchSize, config.RefreshAll, config.Concurrency);

            return;
        }
    }
}
