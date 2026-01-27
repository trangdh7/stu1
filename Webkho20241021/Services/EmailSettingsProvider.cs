using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Webkho_20241021.Models;

namespace Webkho_20241021.Services
{
    public class EmailSettingsProvider : IEmailSettingsProvider
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly ILogger<EmailSettingsProvider> _logger;

        private const string CacheKey = "email-settings-cache";
        private static bool _tableEnsured;

        public EmailSettingsProvider(
            ApplicationDbContext context,
            IConfiguration configuration,
            IMemoryCache cache,
            ILogger<EmailSettingsProvider> logger)
        {
            _context = context;
            _configuration = configuration;
            _cache = cache;
            _logger = logger;
        }

        public async Task<EmailSetting> GetAsync()
        {
            if (_cache.TryGetValue<EmailSetting>(CacheKey, out var cached))
            {
                return cached;
            }

            await EnsureTableAsync();

            var entity = await _context.EmailSettings.FirstOrDefaultAsync();
            if (entity == null)
            {
                entity = SeedFromConfiguration();
                _context.EmailSettings.Add(entity);
                await _context.SaveChangesAsync();
            }

            _cache.Set(CacheKey, entity, TimeSpan.FromMinutes(5));
            return entity;
        }

        public async Task<EmailSetting> UpdateAsync(EmailSetting input, string? updatedBy = null)
        {
            await EnsureTableAsync();

            var entity = await _context.EmailSettings.FirstOrDefaultAsync();
            if (entity == null)
            {
                entity = SeedFromConfiguration();
                _context.EmailSettings.Add(entity);
            }

            entity.SmtpServer = input.SmtpServer?.Trim() ?? entity.SmtpServer;
            entity.SmtpPort = input.SmtpPort != 0 ? input.SmtpPort : entity.SmtpPort;
            entity.FromEmail = input.FromEmail?.Trim() ?? entity.FromEmail;
            entity.FromName = input.FromName?.Trim() ?? entity.FromName;

            if (!string.IsNullOrWhiteSpace(input.FromPassword))
            {
                entity.FromPassword = input.FromPassword;
            }

            entity.UpdatedAt = DateTime.UtcNow;
            entity.UpdatedBy = updatedBy;

            await _context.SaveChangesAsync();
            ClearCache();

            return entity;
        }

        public void ClearCache()
        {
            _cache.Remove(CacheKey);
        }

        private EmailSetting SeedFromConfiguration()
        {
            var smtpServer = _configuration["EmailSettings:StuEmailSettings:SmtpServer"]
                             ?? _configuration["EmailSettings:SmtpServer"]
                             ?? "pro01.emailserver.vn";
            var smtpPort = int.TryParse(_configuration["EmailSettings:StuEmailSettings:SmtpPort"]
                                        ?? _configuration["EmailSettings:SmtpPort"], out var port)
                           ? port
                           : 465;
            var fromEmail = _configuration["EmailSettings:FromEmail"] ?? "no-reply@localhost";
            var fromPassword = _configuration["EmailSettings:StuEmailSettings:FromPassword"]
                               ?? _configuration["EmailSettings:FromPassword"];
            var fromName = _configuration["EmailSettings:StuEmailSettings:FromName"]
                           ?? _configuration["EmailSettings:FromName"]
                           ?? "stu jsc";

            _logger.LogInformation("Seed EmailSettings from configuration");

            return new EmailSetting
            {
                SmtpServer = smtpServer,
                SmtpPort = smtpPort,
                FromEmail = fromEmail,
                FromPassword = fromPassword,
                FromName = fromName,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = "system-seed"
            };
        }

        private async Task EnsureTableAsync()
        {
            if (_tableEnsured) return;

            const string createSql = @"
                CREATE TABLE IF NOT EXISTS `emailsettings` (
                    `Id` INT NOT NULL AUTO_INCREMENT,
                    `SmtpServer` VARCHAR(255) NOT NULL,
                    `SmtpPort` INT NOT NULL,
                    `FromEmail` VARCHAR(255) NOT NULL,
                    `FromPassword` VARCHAR(512) NULL,
                    `FromName` VARCHAR(255) NULL,
                    `UpdatedBy` VARCHAR(255) NULL,
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
                _logger.LogError(ex, "Failed to ensure emailsettings table exists");
                throw;
            }
        }
    }
}
