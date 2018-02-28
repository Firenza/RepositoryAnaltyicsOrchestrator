using Microsoft.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using RepositoryAnalyticsApi.ServiceModel;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace RepositoryAnaltyicsDataRefresher
{
    public class Program
    {
        public static void Main(string[] args)
        {
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
                "Refresh all repository information even if that have been no changes since last update. Defaults to 10",
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
                            Console.WriteLine("Soruce Read Batch Size must be an integer");
                            return 1;
                        }
                    }

                    await ExecuteProgram(repositoryAnaltycisApiUrlOption.Value(), userNameOption?.Value(), organizationNameOption?.Value(), sourceReadBatchSize ,refreshAllOption.HasValue());
                    return 0;
                }
            });

            commandLineApplication.Execute(args);
        }

        public static async Task ExecuteProgram(string repositoryAnalyticsApiUrl, string userName, string organizationName, int? sourceReadBatchSize, bool refreshAllInformation)
        {
            var userOrOganziationNameQueryString = string.Empty;

            if (!string.IsNullOrWhiteSpace(userName))
            {
                userOrOganziationNameQueryString = $"user={userName}";
            }
            else
            {
                userOrOganziationNameQueryString = $"organization={organizationName}";
            }

            int batchSize;

            if (sourceReadBatchSize.HasValue)
            {
                batchSize = sourceReadBatchSize.Value;
            }
            else
            {
                batchSize = 10;
            }

            var httpClient = new HttpClient();

            string endCursor = null;
            bool moreRepostoriesToRead = false;
            var sourceRepositoriesRead = 0;
            var sourceRepositoriesAnalyzed = 0;
            var repositoryAnalysisErrors = new List<(string repoName, string errorMessage, string errorStackTrace)>();

            var stopWatch = Stopwatch.StartNew();

            do
            {
                HttpResponseMessage getSourceReposResponse = null;

                Console.WriteLine($"Reading next batch of {batchSize} repositories for login {userName ?? organizationName}");

                if (endCursor != null)
                {
                    getSourceReposResponse = await httpClient.GetAsync($"{repositoryAnalyticsApiUrl}/api/repositorysource/repositories?{userOrOganziationNameQueryString}&take={batchSize}&endCursor={endCursor}");
                }
                else
                {
                    getSourceReposResponse = await httpClient.GetAsync($"{repositoryAnalyticsApiUrl}/api/repositorysource/repositories?{userOrOganziationNameQueryString}&take={batchSize}");
                }

                var responseBodyString = await getSourceReposResponse.Content.ReadAsStringAsync();
                var results = JsonConvert.DeserializeObject<CursorPagedResults<RepositorySourceRepository>>(responseBodyString);

                sourceRepositoriesRead += results.Results.Count();

                endCursor = results.EndCursor;
                moreRepostoriesToRead = results.MoreToRead;

                foreach (var result in results.Results)
                {
                    Console.WriteLine($"Starting analysis of {result.Url}");

                    var repositoryAnalysis = new RepositoryAnalysis
                    {
                        ForceCompleteRefresh = refreshAllInformation,
                        LastUpdatedOn = result.UpdatedAt,
                        RepositoryUrl = result.Url
                    };

                    var jsonBody = JsonConvert.SerializeObject(repositoryAnalysis);

                    var requestContent = new StringContent(jsonBody, Encoding.UTF8, "application/json");

                    try
                    {
                        await httpClient.PostAsync($"{repositoryAnalyticsApiUrl}/api/repositoryanalysis/", requestContent);
                    }
                    catch (Exception ex)
                    {
                        repositoryAnalysisErrors.Add((result.Url, ex.Message, ex.StackTrace));
                    }

                    sourceRepositoriesAnalyzed += 1;
                }

            } while (moreRepostoriesToRead);

            stopWatch.Stop();

            Console.WriteLine($"\nAnalyized {sourceRepositoriesAnalyzed} out of {sourceRepositoriesRead} repositories in {stopWatch.Elapsed.Minutes} minutes and {stopWatch.Elapsed.Seconds} seconds");

            Console.WriteLine($"\nThere were {repositoryAnalysisErrors.Count} analyisis errors");
            foreach (var repositoryAnalysisError in repositoryAnalysisErrors)
            {
                Console.WriteLine($"{repositoryAnalysisError.repoName} - {repositoryAnalysisError.errorMessage}");
            }

            Console.WriteLine("\nExecution complete ... press any key to exit");
            Console.ReadKey();
        }
    }
}
