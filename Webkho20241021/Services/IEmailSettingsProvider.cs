using System.Threading.Tasks;
using Webkho_20241021.Models;

namespace Webkho_20241021.Services
{
    public interface IEmailSettingsProvider
    {
        Task<EmailSetting> GetAsync();
        Task<EmailSetting> UpdateAsync(EmailSetting input, string? updatedBy = null);
        void ClearCache();
    }
}
