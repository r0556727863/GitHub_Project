using GitHubPortfolio.Service.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Octokit;
using System;
using System.Threading.Tasks;

namespace GitHubPortfolio.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GitHubController : ControllerBase
    {
        private readonly IGitHubService _gitHubService;
        private readonly ILogger<GitHubController> _logger;

        public GitHubController(IGitHubService gitHubService, ILogger<GitHubController> logger)
        {
            _gitHubService = gitHubService;
            _logger = logger;
        }

        [HttpGet("portfolio")]
        public async Task<IActionResult> GetPortfolio()
        {
            try
            {
                _logger.LogInformation("מתחיל בקשה לקבלת פורטפוליו");
                var portfolio = await _gitHubService.GetPortfolio();
                _logger.LogInformation("התקבל פורטפוליו עם {Count} מאגרים", portfolio.Count);
                return Ok(portfolio);
            }
            catch (RateLimitExceededException ex)
            {
                _logger.LogWarning(ex, "חריגה ממגבלת קצב הבקשות של GitHub API");
                return StatusCode(429, "חריגה ממגבלת קצב הבקשות של GitHub API. נסה שוב מאוחר יותר.");
            }
            catch (AuthorizationException ex)
            {
                _logger.LogError(ex, "שגיאת הרשאה בגישה ל-GitHub API");
                return StatusCode(401, "שגיאת הרשאה בגישה ל-GitHub API. בדוק את הטוקן שלך.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "שגיאה בקבלת פורטפוליו");
                return StatusCode(500, $"שגיאה בקבלת פורטפוליו: {ex.Message}");
            }
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchRepositories(
            [FromQuery] string repositoryName = null,
            [FromQuery] string language = null,
            [FromQuery] string username = null)
        {
            try
            {
                _logger.LogInformation("מתחיל חיפוש מאגרים. שם: {Name}, שפה: {Language}, משתמש: {Username}",
                    repositoryName ?? "ללא", language ?? "ללא", username ?? "ללא");

                var searchResult = await _gitHubService.SearchRepositories(repositoryName, language, username);

                _logger.LogInformation("התקבלו {Count} תוצאות חיפוש", searchResult.TotalCount);
                return Ok(searchResult);
            }
            catch (RateLimitExceededException ex)
            {
                _logger.LogWarning(ex, "חריגה ממגבלת קצב הבקשות של GitHub API");
                return StatusCode(429, "חריגה ממגבלת קצב הבקשות של GitHub API. נסה שוב מאוחר יותר.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "שגיאה בחיפוש מאגרים");
                return StatusCode(500, $"שגיאה בחיפוש מאגרים: {ex.Message}");
            }
        }

        [HttpGet("last-activity")]
        public async Task<IActionResult> GetLastActivity()
        {
            try
            {
                _logger.LogInformation("מתחיל בקשה לקבלת פעילות אחרונה");
                var lastActivity = await _gitHubService.GetLastUserActivityDate();
                _logger.LogInformation("התקבל תאריך פעילות אחרונה: {Date}", lastActivity);
                return Ok(lastActivity);
            }
            catch (RateLimitExceededException ex)
            {
                _logger.LogWarning(ex, "חריגה ממגבלת קצב הבקשות של GitHub API");
                return StatusCode(429, "חריגה ממגבלת קצב הבקשות של GitHub API. נסה שוב מאוחר יותר.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "שגיאה בקבלת פעילות אחרונה");
                return StatusCode(500, $"שגיאה בקבלת פעילות אחרונה: {ex.Message}");
            }
        }
    }
}