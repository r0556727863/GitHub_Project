using GitHubPortfolio.Service.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GitHubPortfolio.Service.Interfaces
{
    public interface IGitHubService
    {
        Task<List<RepositoryInfo>> GetPortfolio();
        Task<SearchResult> SearchRepositories(string repositoryName = null, string language = null, string username = null);
        Task<DateTime?> GetLastUserActivityDate();
    }
}