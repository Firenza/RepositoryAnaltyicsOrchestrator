using Microsoft.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using RepositoryAnalyticsApi.ServiceModel;
using System;
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

            CommandOption repositoryAnaltycisApiUrl = commandLineApplication.Option(
               "-url | <url>",
               "The url of the Repository Analytics API",
               CommandOptionType.SingleValue);

            CommandOption userName = commandLineApplication.Option(
              "-u | --user <user>",
              "The user under which to pull repository information from",
              CommandOptionType.SingleValue);

            CommandOption organizationName = commandLineApplication.Option(
               "-o | --organization <organization>",
               "The organization under which to pull repository information from",
               CommandOptionType.SingleValue);

            CommandOption refreshAll = commandLineApplication.Option(
              "-ra | --refreshall",
              "Refresh all repository information even if that have been no changes since last update",
              CommandOptionType.NoValue);

            commandLineApplication.OnExecute(async () =>
            {
                if (!repositoryAnaltycisApiUrl.HasValue())
                {
                    Console.WriteLine("You must specify a url for the Repository Analytics API");
                    return 1;
                }
                
                if (!userName.HasValue() && !organizationName.HasValue())
                {
                    Console.WriteLine("You must specify a user or an origanization");
                    return 1;
                }
                else if (userName.HasValue() && organizationName.HasValue())
                {
                    Console.WriteLine("You can not specify both a user and an origanization");
                    return 1;
                }
                else
                {
                    await ExecuteProgram(repositoryAnaltycisApiUrl.Value(), userName?.Value(), organizationName?.Value(), refreshAll.HasValue());
                    return 0;
                }
            });

            commandLineApplication.Execute(args);
        }

        public static async Task ExecuteProgram(string repositoryAnalyticsApiUrl, string userName, string organizationName, bool refreshAllInformation)
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

            var batchSize = 10;

            var httpClient = new HttpClient();

            string endCursor = null;
            bool moreRepostoriesToRead = false;
            var sourceRepositoriesRead = 0;
            var sourceRepositoriesAnalyzed = 0;

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

                    var requestContent = new StringContent($"{{\"repositoryUrl\": \"{result.Url}\"}}", Encoding.UTF8, "application/json");
                    await httpClient.PostAsync($"{repositoryAnalyticsApiUrl}/api/repositoryanalysis/", requestContent);

                    sourceRepositoriesAnalyzed += 1;
                }

            } while (moreRepostoriesToRead);

            stopWatch.Stop();

            Console.WriteLine($"\nAnalyized {sourceRepositoriesAnalyzed} out of {sourceRepositoriesRead} repositories in {stopWatch.Elapsed.Minutes} minutes and {stopWatch.Elapsed.Seconds} seconds");

            Console.WriteLine("\nExecution complete ... press any key to exit");
            Console.ReadKey();
        }
    }
}
