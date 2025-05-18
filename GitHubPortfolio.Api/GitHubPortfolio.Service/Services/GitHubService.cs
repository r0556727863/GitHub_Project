
using GitHubPortfolio.Service.Interfaces;
using GitHubPortfolio.Service.Models;
using GitHubPortfolio.Service.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Octokit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GitHubPortfolio.Service.Services
{
    public class GitHubService : IGitHubService
    {
        private readonly GitHubClient _client;
        private readonly string _username;
        private readonly ILogger<GitHubService> _logger;

        public GitHubService(IOptions<GitHubOptions> options, ILogger<GitHubService> logger)
        {
            _username = options.Value.Username;
            _logger = logger;

            _logger.LogInformation("יוצר לקוח GitHub עבור משתמש {Username}", _username);
            _client = new GitHubClient(new ProductHeaderValue("GitHubPortfolio"));

            if (!string.IsNullOrEmpty(options.Value.PersonalAccessToken))
            {
                _client.Credentials = new Credentials(options.Value.PersonalAccessToken);
                _logger.LogInformation("הוגדרו הרשאות לקוח GitHub עם טוקן");
            }
            else
            {
                _logger.LogWarning("לא הוגדר טוקן גישה אישי. חלק מהפונקציות עלולות להיות מוגבלות.");
            }
        }

        public async Task<List<RepositoryInfo>> GetPortfolio()
        {
            try
            {
                _logger.LogInformation("מושך רשימת מאגרים עבור משתמש {Username}", _username);
                var repositories = await _client.Repository.GetAllForUser(_username);
                _logger.LogInformation("התקבלו {Count} מאגרים", repositories.Count);

                var result = new List<RepositoryInfo>();

                foreach (var repo in repositories)
                {
                    try
                    {
                        _logger.LogDebug("מושך מידע נוסף עבור מאגר {RepoName}", repo.Name);

                        // תיקון: הוספת פרמטר סטטוס לקבלת כל ה-Pull Requests (פתוחים וסגורים)
                        var pullRequestRequest = new PullRequestRequest
                        {
                            State = ItemStateFilter.All
                        };
                        var pullRequests = await _client.PullRequest.GetAllForRepository(repo.Owner.Login, repo.Name, pullRequestRequest);
                        _logger.LogDebug("נמצאו {Count} pull requests עבור מאגר {RepoName}", pullRequests.Count, repo.Name);

                        var languages = await _client.Repository.GetAllLanguages(repo.Owner.Login, repo.Name);

                        // נביא רק את הקומיט האחרון כדי לא להעמיס על ה-API
                        var commitRequest = new CommitRequest
                        {
                            Since = DateTimeOffset.Now.AddYears(-1) // טווח זמן ריאלי
                        };
                        var commits = await _client.Repository.Commit.GetAll(repo.Owner.Login, repo.Name, commitRequest, new ApiOptions { PageSize = 1 });

                        var lastCommit = commits.OrderByDescending(c => c.Commit.Author.Date).FirstOrDefault();

                        var repoInfo = new RepositoryInfo
                        {
                            Name = repo.Name,
                            Description = repo.Description,
                            Url = repo.HtmlUrl,
                            Homepage = repo.Homepage,
                            Stars = repo.StargazersCount,
                            Forks = repo.ForksCount,
                            OpenIssues = repo.OpenIssuesCount,
                            PullRequests = pullRequests.Count,
                            LastCommitDate = lastCommit?.Commit?.Author?.Date.DateTime ?? DateTime.MinValue,
                            Languages = languages.ToDictionary(l => l.Name, l => l.NumberOfBytes),
                            OwnerLogin = repo.Owner.Login,
                            OwnerAvatarUrl = repo.Owner.AvatarUrl
                        };

                        result.Add(repoInfo);
                        _logger.LogDebug("נוסף מידע עבור מאגר {RepoName}", repo.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "שגיאה במשיכת מידע נוסף עבור מאגר {RepoName}. ממשיך למאגר הבא.", repo.Name);
                        // ממשיך למאגר הבא במקרה של שגיאה
                    }
                }

                _logger.LogInformation("הושלמה משיכת מידע עבור {Count} מאגרים", result.Count);
                return result;
            }
            catch (RateLimitExceededException ex)
            {
                _logger.LogError(ex, "חריגה ממגבלת קצב הבקשות של GitHub API");
                throw;
            }
            catch (AuthorizationException ex)
            {
                _logger.LogError(ex, "שגיאת הרשאה בגישה ל-GitHub API");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "שגיאה במשיכת פורטפוליו");
                throw;
            }
        }

        public async Task<SearchResult> SearchRepositories(string repositoryName = null, string language = null, string username = null)
        {
            try
            {
                _logger.LogInformation("מתחיל חיפוש מאגרים. שם: {Name}, שפה: {Language}, משתמש: {Username}",
                    repositoryName ?? "ללא", language ?? "ללא", username ?? "ללא");

                var result = new SearchResult
                {
                    TotalCount = 0,
                    Repositories = new List<RepositoryInfo>()
                };

                if (!string.IsNullOrEmpty(username))
                {
                    _logger.LogDebug("מבצע חיפוש לפי שם משתמש: {Username}", username);
                    // חיפוש לפי שם משתמש - אמין ומדויק
                    var allRepos = await _client.Repository.GetAllForUser(username);
                    var filtered = allRepos
                        .Where(r =>
                            (string.IsNullOrEmpty(repositoryName) || r.Name.Contains(repositoryName, StringComparison.OrdinalIgnoreCase)) &&
                            (string.IsNullOrEmpty(language) || r.Language?.Equals(language, StringComparison.OrdinalIgnoreCase) == true)
                        )
                        .ToList();

                    result.TotalCount = filtered.Count;
                    _logger.LogDebug("נמצאו {Count} מאגרים למשתמש {Username}", filtered.Count, username);

                    foreach (var repo in filtered)
                    {
                        try
                        {
                            var languages = await _client.Repository.GetAllLanguages(repo.Owner.Login, repo.Name);

                            // תיקון: הוספת משיכת Pull Requests גם בחיפוש
                            var pullRequestRequest = new PullRequestRequest
                            {
                                State = ItemStateFilter.All
                            };
                            var pullRequests = await _client.PullRequest.GetAllForRepository(repo.Owner.Login, repo.Name, pullRequestRequest);
                            _logger.LogDebug("נמצאו {Count} pull requests עבור מאגר {RepoName}", pullRequests.Count, repo.Name);

                            var repoInfo = new RepositoryInfo
                            {
                                Name = repo.Name,
                                Description = repo.Description,
                                Url = repo.HtmlUrl,
                                Homepage = repo.Homepage,
                                Stars = repo.StargazersCount,
                                Forks = repo.ForksCount,
                                OpenIssues = repo.OpenIssuesCount,
                                PullRequests = pullRequests.Count, // תיקון: הוספת מספר ה-Pull Requests
                                Languages = languages.ToDictionary(l => l.Name, l => l.NumberOfBytes),
                                OwnerLogin = repo.Owner.Login,
                                OwnerAvatarUrl = repo.Owner.AvatarUrl
                            };

                            result.Repositories.Add(repoInfo);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "שגיאה במשיכת מידע נוסף עבור מאגר {RepoName}. ממשיך למאגר הבא.", repo.Name);
                            // ממשיך למאגר הבא במקרה של שגיאה
                        }
                    }
                }
                else
                {
                    _logger.LogDebug("מבצע חיפוש כללי");
                    // חיפוש כללי לפי repository name ו-language בלבד
                    var query = "";

                    if (!string.IsNullOrEmpty(repositoryName))
                        query += $"{repositoryName} ";

                    if (!string.IsNullOrEmpty(language))
                        query += $"language:{language} ";

                    var request = new SearchRepositoriesRequest(query.Trim());

                    var searchResult = await _client.Search.SearchRepo(request);

                    result.TotalCount = searchResult.TotalCount;
                    _logger.LogDebug("נמצאו {Count} מאגרים בחיפוש כללי", searchResult.TotalCount);

                    foreach (var repo in searchResult.Items)
                    {
                        try
                        {
                            var languages = await _client.Repository.GetAllLanguages(repo.Owner.Login, repo.Name);

                            // תיקון: הוספת משיכת Pull Requests גם בחיפוש כללי
                            var pullRequestRequest = new PullRequestRequest
                            {
                                State = ItemStateFilter.All
                            };
                            var pullRequests = await _client.PullRequest.GetAllForRepository(repo.Owner.Login, repo.Name, pullRequestRequest);
                            _logger.LogDebug("נמצאו {Count} pull requests עבור מאגר {RepoName}", pullRequests.Count, repo.Name);

                            var repoInfo = new RepositoryInfo
                            {
                                Name = repo.Name,
                                Description = repo.Description,
                                Url = repo.HtmlUrl,
                                Homepage = repo.Homepage,
                                Stars = repo.StargazersCount,
                                Forks = repo.ForksCount,
                                OpenIssues = repo.OpenIssuesCount,
                                PullRequests = pullRequests.Count, // תיקון: הוספת מספר ה-Pull Requests
                                Languages = languages.ToDictionary(l => l.Name, l => l.NumberOfBytes),
                                OwnerLogin = repo.Owner.Login,
                                OwnerAvatarUrl = repo.Owner.AvatarUrl
                            };

                            result.Repositories.Add(repoInfo);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "שגיאה במשיכת מידע נוסף עבור מאגר {RepoName}. ממשיך למאגר הבא.", repo.Name);
                            // ממשיך למאגר הבא במקרה של שגיאה
                        }
                    }
                }

                _logger.LogInformation("הושלם חיפוש מאגרים. נמצאו {Count} תוצאות", result.TotalCount);
                return result;
            }
            catch (RateLimitExceededException ex)
            {
                _logger.LogError(ex, "חריגה ממגבלת קצב הבקשות של GitHub API");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "שגיאה בחיפוש מאגרים");
                throw;
            }
        }

        public async Task<DateTime?> GetLastUserActivityDate()
        {
            try
            {
                _logger.LogInformation("מושך פעילות אחרונה עבור משתמש {Username}", _username);
                var events = await _client.Activity.Events.GetAllUserPerformed(_username);
                var lastEvent = events.FirstOrDefault();

                if (lastEvent != null)
                {
                    _logger.LogInformation("נמצאה פעילות אחרונה בתאריך {Date}", lastEvent.CreatedAt);
                    return lastEvent.CreatedAt.DateTime;
                }

                _logger.LogInformation("לא נמצאה פעילות אחרונה");
                return null;
            }
            catch (RateLimitExceededException ex)
            {
                _logger.LogError(ex, "חריגה ממגבלת קצב הבקשות של GitHub API");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "שגיאה במשיכת פעילות אחרונה");
                throw;
            }
        }
    }
}