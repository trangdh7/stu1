using System.Threading.Tasks;
using Webkho_20241021.Models;

namespace Webkho_20241021.Services
{
    public interface ILogoThemeProvider
    {
        Task<LogoThemeSetting> GetAsync();
        Task<LogoThemeSetting> UpdateAsync(string theme, string? updatedBy = null);
        void ClearCache();
    }
}
