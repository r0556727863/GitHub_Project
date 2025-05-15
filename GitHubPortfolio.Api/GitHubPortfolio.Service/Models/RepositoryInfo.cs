using System;
using System.Collections.Generic;

namespace GitHubPortfolio.Service.Models
{
    public class RepositoryInfo
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Url { get; set; }
        public string Homepage { get; set; }
        public int Stars { get; set; }
        public int Forks { get; set; }
        public int OpenIssues { get; set; }
        public int PullRequests { get; set; }
        public DateTime LastCommitDate { get; set; }
        public Dictionary<string, long> Languages { get; set; }
        public string OwnerLogin { get; set; }
        public string OwnerAvatarUrl { get; set; }

     
    }
}