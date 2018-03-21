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

        public RepositoryAnalysisOrchestrator(ILogger logger, IRestClient restClient)
        {
            this.logger = logger;
            this.restClient = restClient;
        }

        public async Task OrchestrateAsync(string repositoryAnalyticsApiUrl, string userName, string organizationName, int? sourceReadBatchSize, bool refreshAllInformation, int? maxConcurrentRequests)
        {
            bool moreRepostoriesToRead = false;
            var sourceRepositoriesRead = 0;
            var sourceRepositoriesAnalyzed = 0;
            var repositoryAnalysisErrors = new List<(string repoName, string errorMessage, string errorStackTrace)>();

            try
            {
                var userOrOranizationNameQueryStringKey = string.Empty;
                var userOrOganziationNameQueryStringValue = string.Empty;

                if (!string.IsNullOrWhiteSpace(userName))
                {
                    userOrOranizationNameQueryStringKey = "user";
                    userOrOganziationNameQueryStringValue = userName;
                }
                else
                {
                    userOrOranizationNameQueryStringKey = "organization";
                    userOrOganziationNameQueryStringValue = organizationName;
                }

                int batchSize;

                if (sourceReadBatchSize.HasValue)
                {
                    batchSize = sourceReadBatchSize.Value;
                }
                else
                {
                    batchSize = 50;
                }

                int maxConcurrenyLevel;

                if (maxConcurrentRequests.HasValue)
                {
                    maxConcurrenyLevel = maxConcurrentRequests.Value;
                }
                else
                {
                    maxConcurrenyLevel = 1;
                }

                restClient.BaseUrl = new Uri(repositoryAnalyticsApiUrl);

                string endCursor = null;


                var stopWatch = Stopwatch.StartNew();

                do
                {
                    logger.Information($"Reading next batch of {batchSize} repositories for login {userName ?? organizationName}");

                    CursorPagedResults<RepositorySourceRepository> results = null;

                    if (endCursor != null)
                    {
                        var request = new RestRequest("/api/repositorysource/repositories");
                        request.AddQueryParameter(userOrOranizationNameQueryStringKey, userOrOganziationNameQueryStringValue);
                        request.AddQueryParameter("take", batchSize.ToString());
                        request.AddQueryParameter("endCursor", endCursor);

                        var response = await restClient.ExecuteTaskAsync<CursorPagedResults<RepositorySourceRepository>>(request);

                        if (!response.IsSuccessful)
                        {
                            throw new ArgumentException($"{response.StatusDescription} - {response.ErrorMessage}");
                        }

                        results = response.Data;
                    }
                    else
                    {
                        var request = new RestRequest("/api/repositorysource/repositories");
                        request.AddQueryParameter(userOrOranizationNameQueryStringKey, userOrOganziationNameQueryStringValue);
                        request.AddQueryParameter("take", batchSize.ToString());

                        var response = await restClient.ExecuteTaskAsync<CursorPagedResults<RepositorySourceRepository>>(request);

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

                    using (var semaphore = new SemaphoreSlim(maxConcurrenyLevel))
                    {
                        foreach (var result in results.Results)
                        {
                            // await here until there is a room for this task
                            await semaphore.WaitAsync();

                            repositoryAnalysisTasks.Add(SendAnalysisRequest(semaphore, result));
                        }

                        await Task.WhenAll(repositoryAnalysisTasks);
                    }

                    logger.Information($"Finished analyizing batch of {batchSize} repositories.  {sourceRepositoriesAnalyzed} respositories analyzed thus far");

                } while (moreRepostoriesToRead);

                stopWatch.Stop();

                logger.Information($"\nAnalyized {sourceRepositoriesAnalyzed} out of {sourceRepositoriesRead} repositories in {stopWatch.Elapsed.Minutes} minutes and {stopWatch.Elapsed.Seconds} seconds");

                logger.Information($"\nThere were {repositoryAnalysisErrors.Count} analyisis errors");
                foreach (var repositoryAnalysisError in repositoryAnalysisErrors)
                {
                    logger.Error($"{repositoryAnalysisError.repoName} - {repositoryAnalysisError.errorMessage}");
                }

                Console.WriteLine("\nExecution complete ... press any key to exit");
                Console.ReadKey();
            }
            catch (Exception ex)
            {
                logger.Error($"FATAL EXCEPTION OCCURRED! {ex.Message}");
                Console.WriteLine("\nPress any key to exit");
                Console.ReadKey();
            }

            async Task SendAnalysisRequest(SemaphoreSlim semaphore, RepositorySourceRepository sourceRepository)
            {
                try
                {
                    logger.Information($"Starting analysis of {sourceRepository.Url}");

                    var repositoryAnalysis = new RepositoryAnalysis
                    {
                        ForceCompleteRefresh = refreshAllInformation,
                        LastUpdatedOn = sourceRepository.UpdatedAt,
                        RepositoryUrl = sourceRepository.Url
                    };

                    var request = new RestRequest("/api/repositoryanalysis/", Method.POST);
                    request.AddJsonBody(repositoryAnalysis);

                    var response = await restClient.ExecuteTaskAsync(request);

                    if (!response.IsSuccessful)
                    {
                        repositoryAnalysisErrors.Add((sourceRepository.Url, response.StatusDescription, null));
                    }
                }
                catch (Exception ex)
                {
                    repositoryAnalysisErrors.Add((sourceRepository.Url, ex.Message, ex.StackTrace));
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
