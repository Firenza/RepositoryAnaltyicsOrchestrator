using Microsoft.Extensions.CommandLineUtils;
using RestSharp;
using Serilog;
using Serilog.Events;
using System;

namespace RepositoryAnaltyicsDataRefresher
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
             .MinimumLevel.Debug()
             .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
             .Enrich.FromLogContext()
             .WriteTo.Console()
             .CreateLogger();

            CommandLineApplication commandLineApplication = new CommandLineApplication(throwOnUnexpectedArg: false);

            CommandOption repositoryAnaltycisApiUrlOption = commandLineApplication.Option(
               "-url | <url>",
               "The url of the Repository Analytics API",
               CommandOptionType.SingleValue);

            CommandOption userNameOption = commandLineApplication.Option(
              "-u | --user <user>",
              "The user under which to pull repository information from",
              CommandOptionType.SingleValue);

            CommandOption organizationNameOption = commandLineApplication.Option(
               "-o | --organization <organization>",
               "The organization under which to pull repository information from",
               CommandOptionType.SingleValue);

            CommandOption refreshAllOption = commandLineApplication.Option(
              "-ra | --refreshall",
              "Refresh all repository information even if that have been no changes since last update",
              CommandOptionType.NoValue);

            CommandOption sourceReadBatchSizeOption = commandLineApplication.Option(
                "-b | --sourcereadbatchsize",
                "Refresh all repository information even if that have been no changes since last update. Defaults to 50",
                CommandOptionType.SingleValue);

            CommandOption maxConcurrentRequestsOption = commandLineApplication.Option(
                "-c | --maxConcurrentRequests",
                "The number of allowed concurrent requests to the API. Defaults to 1",
                CommandOptionType.SingleValue);

            commandLineApplication.OnExecute(async () =>
            {
                if (!repositoryAnaltycisApiUrlOption.HasValue())
                {
                    Console.WriteLine("You must specify a url for the Repository Analytics API");
                    return 1;
                }

                if (!userNameOption.HasValue() && !organizationNameOption.HasValue())
                {
                    Console.WriteLine("You must specify a user or an origanization");
                    return 1;
                }
                else if (userNameOption.HasValue() && organizationNameOption.HasValue())
                {
                    Console.WriteLine("You can not specify both a user and an origanization");
                    return 1;
                }
                else
                {
                    int? sourceReadBatchSize = null;

                    if (sourceReadBatchSizeOption.HasValue())
                    {
                        var batchSizeIsInteger = Int32.TryParse(sourceReadBatchSizeOption.Value(), out int batchSize);

                        if (batchSizeIsInteger)
                        {
                            sourceReadBatchSize = batchSize;
                        }
                        else
                        {
                            Console.WriteLine("Source Read Batch Size must be an integer");
                            return 1;
                        }
                    }

                    int? maxConcurrentRequests = null;

                    if (maxConcurrentRequestsOption.HasValue())
                    {
                        var maxConcurrentRequestsIsInteger = Int32.TryParse(maxConcurrentRequestsOption.Value(), out int maxConcurrencyLevel);

                        if (maxConcurrentRequestsIsInteger)
                        {
                            maxConcurrentRequests = maxConcurrencyLevel;
                        }
                        else
                        {
                            Console.WriteLine("Maxiumum Concurrent Requets must be an integer");
                            return 1;
                        }
                    }

                    var repositoryAnalyticsOrchestrator = new RepositoryAnalysisOrchestrator(Log.Logger, new RestClient());
                    await repositoryAnalyticsOrchestrator.OrchestrateAsync(repositoryAnaltycisApiUrlOption.Value(), userNameOption?.Value(), organizationNameOption?.Value(), sourceReadBatchSize, refreshAllOption.HasValue(), maxConcurrentRequests);

                    return 0;
                }
            });

            commandLineApplication.Execute(args);
        }
    }
}
