using RepositoryAnalyticsApi.ServiceModel;
using RestSharp;
using Serilog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace RepositoryAnaltyicsOrchestrator
{
    public class RepositoryAnalysisOrchestrator
    {
        private readonly ILogger logger;
        private readonly IRestClient restClient;
        private readonly bool runningInContainer;

        public RepositoryAnalysisOrchestrator(ILogger logger, IRestClient restClient, bool runningInContainer)
        {
            this.logger = logger;
            this.restClient = restClient;
            this.runningInContainer = runningInContainer;
        }

        public async Task OrchestrateAsync(RunTimeConfiguration config)
        {
            bool moreRepostoriesToRead = false;
            var sourceRepositoriesRead = 0;
            var sourceRepositoriesAnalyzed = 0;
            var repositoryAnalysisErrors = new List<(string repoName, string errorMessage, string errorStackTrace)>();

            try
            {
                var userOrOranizationNameQueryStringKey = string.Empty;
                var userOrOganziationNameQueryStringValue = string.Empty;

                if (!string.IsNullOrWhiteSpace(config.User))
                {
                    userOrOranizationNameQueryStringKey = "user";
                    userOrOganziationNameQueryStringValue = config.User;
                }
                else
                {
                    userOrOranizationNameQueryStringKey = "organization";
                    userOrOganziationNameQueryStringValue = config.Organization;
                }

                restClient.BaseUrl = new Uri(config.Url);

                string endCursor = null;

                var stopWatch = Stopwatch.StartNew();

                do
                {
                    logger.Information($"Reading next batch of {config.BatchSize} repositories for login {config.User ?? config.Organization}");

                    CursorPagedResults<RepositorySummary> results = null;

                    if (endCursor != null)
                    {
                        var request = new RestRequest("/api/repositorysource/repositories");
                        request.AddQueryParameter("owner", userOrOganziationNameQueryStringValue);
                        request.AddQueryParameter("take", config.BatchSize.ToString());
                        request.AddQueryParameter("endCursor", endCursor);

                        var response = await restClient.ExecuteTaskAsync<CursorPagedResults<RepositorySummary>>(request);

                        if (!response.IsSuccessful)
                        {
                            throw new ArgumentException($"{response.StatusDescription} - {response.ErrorMessage}");
                        }

                        results = response.Data;
                    }
                    else
                    {
                        var request = new RestRequest("/api/repositorysource/repositories");
                        request.AddQueryParameter("owner", userOrOganziationNameQueryStringValue);
                        request.AddQueryParameter("take", config.BatchSize.ToString());

                        var response = await restClient.ExecuteTaskAsync<CursorPagedResults<RepositorySummary>>(request);

                        if (!response.IsSuccessful)
                        {
                            throw new ArgumentException($"{response.StatusDescription} - {response.ErrorMessage}");
                        }

                        results = response.Data;
                    }

                    sourceRepositoriesRead += results.Results.Count();

                    endCursor = results.EndCursor;
                    moreRepostoriesToRead = results.MoreToRead;

                    var repositoryAnalysisTasks = new List<Task>();

                    using (var semaphore = new SemaphoreSlim(config.Concurrency))
                    {
                        foreach (var result in results.Results)
                        {
                            // await here until there is a room for this task
                            await semaphore.WaitAsync();

                            repositoryAnalysisTasks.Add(SendAnalysisRequest(semaphore, result));
                        }

                        await Task.WhenAll(repositoryAnalysisTasks);
                    }

                    logger.Information($"Finished analyizing batch of {config.BatchSize} repositories.  {sourceRepositoriesAnalyzed} respositories analyzed thus far");

                } while (moreRepostoriesToRead);

                stopWatch.Stop();


                logger.Information($"\nAnalyized {sourceRepositoriesAnalyzed} out of {sourceRepositoriesRead} repositories in {stopWatch.Elapsed.TotalMinutes} minutes");

                logger.Information($"\nThere were {repositoryAnalysisErrors.Count} analyisis errors");
                foreach (var repositoryAnalysisError in repositoryAnalysisErrors)
                {
                    logger.Error($"{repositoryAnalysisError.repoName} - {repositoryAnalysisError.errorMessage}");
                }

                Console.WriteLine("\nExecution complete!");

                if (!runningInContainer)
                {
                    Console.WriteLine("\nPress any key to exit");
                    Console.ReadKey();
                }
            }
            catch (Exception ex)
            {
                logger.Error($"FATAL EXCEPTION OCCURRED! {ex.Message}");
                
                if (!runningInContainer)
                {
                    Console.WriteLine("\nPress any key to exit");
                    Console.ReadKey();
                }
            }

            async Task SendAnalysisRequest(SemaphoreSlim semaphore, RepositorySummary repositorySummary)
            {
                try
                {
                    if (config.AsOf.HasValue)
                    {
                        logger.Information($"Starting analysis of {repositorySummary.Url} as of {config.AsOf.Value.ToString("F")}");
                    }
                    else
                    {
                        logger.Information($"Starting analysis of {repositorySummary.Url}");
                    }

                    var repositoryAnalysis = new RepositoryAnalysis
                    {
                        ForceCompleteRefresh = config.RefreshAll,
                        RepositoryLastUpdatedOn = repositorySummary.UpdatedAt,
                        RepositoryId = repositorySummary.Url,
                        AsOf = config.AsOf
                    };

                    var request = new RestRequest("/api/repositoryanalysis/", Method.POST);
                    request.AddJsonBody(repositoryAnalysis);

                    if (sourceRepositoriesAnalyzed == 0)
                    {
                        // If no requests have been completed then set the timeout to be higher as if an organization
                        // is being targeted then the reading of all the team information can take a few minutes.
                        request.Timeout = config.FirstApiCallTimeout;
                    }

                    var response = await restClient.ExecuteTaskAsync(request);

                    if (!response.IsSuccessful)
                    {
                        if (response.ErrorMessage != null && response.ErrorMessage.StartsWith("No connection could be made because the target machine actively refused it"))
                        {
                            logger.Error($"UNABLE TO REACH API!!");

                            if (!runningInContainer)
                            {
                                Console.WriteLine("\nPress any key to exit");
                                Console.ReadKey();
                            }

                            return;
                        }
                        else
                        {
                            logger.Error(response?.ErrorMessage);
                            repositoryAnalysisErrors.Add((repositorySummary.Url, response.StatusDescription, null));
                        }
                    }
                }
                catch (Exception ex)
                {
                    repositoryAnalysisErrors.Add((repositorySummary.Url, ex.Message, ex.StackTrace));
                }
                finally
                {
                    semaphore.Release();
                    Interlocked.Increment(ref sourceRepositoriesAnalyzed);
                }
            }
        }
    }
}
