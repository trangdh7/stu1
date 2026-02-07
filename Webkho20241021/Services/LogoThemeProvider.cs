using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Webkho_20241021.Models;

namespace Webkho_20241021.Services
{
    public class LogoThemeProvider : ILogoThemeProvider
    {
        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<LogoThemeProvider> _logger;

        private const string CacheKey = "logo-theme-settings-cache";
        private static bool _tableEnsured;

        public static readonly string ThemeNormal = "Normal";
        public static readonly string ThemeTet = "Tet";
        public static readonly string ThemeNationalDay = "NationalDay";

        public LogoThemeProvider(
            ApplicationDbContext context,
            IMemoryCache cache,
            ILogger<LogoThemeProvider> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        public async Task<LogoThemeSetting> GetAsync()
        {
            if (_cache.TryGetValue<LogoThemeSetting>(CacheKey, out var cached))
            {
                return cached;
            }

            await EnsureTableAsync();

            var entity = await _context.LogoThemeSettings.FirstOrDefaultAsync();
            if (entity == null)
            {
                entity = new LogoThemeSetting
                {
                    Theme = ThemeNormal,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = "system-seed"
                };
                _context.LogoThemeSettings.Add(entity);
                await _context.SaveChangesAsync();
            }

            _cache.Set(CacheKey, entity, TimeSpan.FromMinutes(10));
            return entity;
        }

        public async Task<LogoThemeSetting> UpdateAsync(string theme, string? updatedBy = null)
        {
            await EnsureTableAsync();

            var normalized = theme?.Trim() ?? ThemeNormal;
            if (normalized != ThemeNormal && normalized != ThemeTet && normalized != ThemeNationalDay)
            {
                normalized = ThemeNormal;
            }

            var entity = await _context.LogoThemeSettings.FirstOrDefaultAsync();
            if (entity == null)
            {
                entity = new LogoThemeSetting
                {
                    Theme = normalized,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = updatedBy
                };
                _context.LogoThemeSettings.Add(entity);
            }
            else
            {
                entity.Theme = normalized;
                entity.UpdatedAt = DateTime.UtcNow;
                entity.UpdatedBy = updatedBy;
            }

            await _context.SaveChangesAsync();
            ClearCache();
            return entity;
        }

        public void ClearCache()
        {
            _cache.Remove(CacheKey);
        }

        private async Task EnsureTableAsync()
        {
            if (_tableEnsured) return;

            const string createSql = @"
                CREATE TABLE IF NOT EXISTS `logothemesettings` (
                    `Id` INT NOT NULL AUTO_INCREMENT,
                    `Theme` VARCHAR(32) NOT NULL DEFAULT 'Normal',
                    `UpdatedBy` VARCHAR(128) NULL,
                    `UpdatedAt` DATETIME NULL,
                    PRIMARY KEY (`Id`)
                ) CHARACTER SET utf8mb4;";

            try
            {
                await _context.Database.ExecuteSqlRawAsync(createSql);
                _tableEnsured = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ensure logothemesettings table exists");
                throw;
            }
        }
    }
}
