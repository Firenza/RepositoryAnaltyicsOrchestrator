using Newtonsoft.Json;
using RepositoryAnalyticsApi.ServiceModel;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace RepositoryAnaltyicsDataRefresher
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var repositoryAnaltyicsApiUrl = "http://xizor-desktop:32771";
            var userName = "Firenza";
            var batchSize = 10;

            var httpClient = new HttpClient();
            httpClient.Timeout = new TimeSpan(0, 0, 0, 10);

            string endCursor = null;
            bool moreRepostoriesToRead = false;

            do
            {
                HttpResponseMessage getSourceReposResponse = null;

                if (endCursor != null)
                {
                    getSourceReposResponse = await httpClient.GetAsync($"{repositoryAnaltyicsApiUrl}/api/repositorysource/repositories?user={userName}&take={batchSize}&endCursor={endCursor}");
                }
                else
                {
                    getSourceReposResponse = await httpClient.GetAsync($"{repositoryAnaltyicsApiUrl}/api/repositorysource/repositories?user={userName}&take={batchSize}");
                }

                var responseBodyString = await getSourceReposResponse.Content.ReadAsStringAsync();
                var results = JsonConvert.DeserializeObject<CursorPagedResults<RepositorySourceRepository>>(responseBodyString);

                endCursor = results.EndCursor;
                moreRepostoriesToRead = results.MoreToRead;

                foreach (var result in results.Results)
                {
                    var requestContent = new StringContent($"{{\"repositoryUrl\": \"{result.Url}\"}}", Encoding.UTF8, "application/json");
                    await httpClient.PostAsync($"{repositoryAnaltyicsApiUrl}/api/repositoryanalysis/", requestContent);
                }

            } while (moreRepostoriesToRead);
        }
    }
}
