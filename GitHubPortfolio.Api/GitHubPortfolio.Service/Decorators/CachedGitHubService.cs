using GitHubPortfolio.Service.Interfaces;
using GitHubPortfolio.Service.Models;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GitHubPortfolio.Service.Decorators
{
    public class CachedGitHubService : IGitHubService
    {
        private readonly IGitHubService _gitHubService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<CachedGitHubService> _logger;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(10);
        private DateTime _lastCacheRefresh = DateTime.MinValue;

        private const string PortfolioCacheKey = "Portfolio";
        private const string LastActivityCacheKey = "LastActivity";

        public CachedGitHubService(IGitHubService gitHubService, IMemoryCache cache, ILogger<CachedGitHubService> logger)
        {
            _gitHubService = gitHubService;
            _cache = cache;
            _logger = logger;
        }

        public async Task<List<RepositoryInfo>> GetPortfolio()
        {
            try
            {
                // בדיקה אם יש עדכונים חדשים
                await CheckForUpdates();

                // ניסיון לקבל מידע מהמטמון
                if (_cache.TryGetValue(PortfolioCacheKey, out List<RepositoryInfo> cachedPortfolio))
                {
                    _logger.LogInformation("מחזיר פורטפוליו מהמטמון. {Count} מאגרים.", cachedPortfolio.Count);
                    return cachedPortfolio;
                }

                _logger.LogInformation("אין פורטפוליו במטמון. מושך מידע חדש.");
                // אם אין מידע במטמון, קבל מידע מהשירות
                var portfolio = await _gitHubService.GetPortfolio();

                // שמור במטמון
                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(_cacheDuration);

                _cache.Set(PortfolioCacheKey, portfolio, cacheOptions);
                _lastCacheRefresh = DateTime.Now;
                _logger.LogInformation("פורטפוליו נשמר במטמון. {Count} מאגרים.", portfolio.Count);

                return portfolio;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "שגיאה בקבלת פורטפוליו עם מטמון");
                throw;
            }
        }

        public async Task<SearchResult> SearchRepositories(string repositoryName = null, string language = null, string username = null)
        {
            try
            {
                // חיפוש תמיד מבוצע בזמן אמת ללא מטמון
                _logger.LogInformation("מבצע חיפוש מאגרים בזמן אמת (ללא מטמון)");
                return await _gitHubService.SearchRepositories(repositoryName, language, username);
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
                if (_cache.TryGetValue(LastActivityCacheKey, out DateTime? cachedDate))
                {
                    _logger.LogInformation("מחזיר תאריך פעילות אחרונה מהמטמון: {Date}", cachedDate);
                    return cachedDate;
                }

                _logger.LogInformation("אין תאריך פעילות אחרונה במטמון. מושך מידע חדש.");
                var lastActivity = await _gitHubService.GetLastUserActivityDate();

                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(_cacheDuration);

                _cache.Set(LastActivityCacheKey, lastActivity, cacheOptions);
                _logger.LogInformation("תאריך פעילות אחרונה נשמר במטמון: {Date}", lastActivity);

                return lastActivity;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "שגיאה בקבלת תאריך פעילות אחרונה");
                throw;
            }
        }

        private async Task CheckForUpdates()
        {
            try
            {
                // אם עברו פחות מדקה מהבדיקה האחרונה, דלג
                if (DateTime.Now - _lastCacheRefresh < TimeSpan.FromMinutes(1))
                {
                    _logger.LogDebug("דילוג על בדיקת עדכונים - נבדק לאחרונה לפני פחות מדקה");
                    return;
                }

                _logger.LogInformation("בודק אם יש עדכונים חדשים מאז הרענון האחרון");
                // בדוק אם יש פעילות חדשה של המשתמש
                var lastActivity = await _gitHubService.GetLastUserActivityDate();

                // אם אין פעילות אחרונה, דלג
                if (!lastActivity.HasValue)
                {
                    _logger.LogInformation("לא נמצאה פעילות אחרונה. שומר על המטמון הקיים.");
                    return;
                }

                // אם הפעילות האחרונה היא אחרי הרענון האחרון של המטמון, נקה את המטמון
                if (lastActivity.Value > _lastCacheRefresh)
                {
                    _logger.LogInformation("נמצאה פעילות חדשה ({ActivityDate}) אחרי הרענון האחרון ({RefreshDate}). מנקה מטמון.",
                        lastActivity.Value, _lastCacheRefresh);

                    _cache.Remove(PortfolioCacheKey);
                    _cache.Remove(LastActivityCacheKey);
                    _lastCacheRefresh = DateTime.MinValue; // איפוס כדי לאלץ טעינה מחדש
                }
                else
                {
                    _logger.LogInformation("לא נמצאה פעילות חדשה מאז הרענון האחרון. שומר על המטמון הקיים.");
                }
            }
            catch (Exception ex)
            {
                // במקרה של שגיאה, נמשיך להשתמש במטמון הקיים
                _logger.LogWarning(ex, "שגיאה בבדיקת עדכונים. ממשיך להשתמש במטמון הקיים.");
            }
        }
    }
}