using System.Collections.Generic;

namespace GitHubPortfolio.Service.Models
{
    public class SearchResult
    {
        public int TotalCount { get; set; }
        public List<RepositoryInfo> Repositories { get; set; }
    }
}