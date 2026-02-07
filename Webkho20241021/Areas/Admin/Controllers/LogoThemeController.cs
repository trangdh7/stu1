using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Webkho_20241021.Services;

namespace Webkho_20241021.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class LogoThemeController : Controller
    {
        private readonly ILogoThemeProvider _provider;

        public LogoThemeController(ILogoThemeProvider provider)
        {
            _provider = provider;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var settings = await _provider.GetAsync();
            ViewBag.CurrentTheme = settings?.Theme ?? LogoThemeProvider.ThemeNormal;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(string theme)
        {
            var normalized = theme?.Trim() ?? LogoThemeProvider.ThemeNormal;
            if (normalized != LogoThemeProvider.ThemeNormal &&
                normalized != LogoThemeProvider.ThemeTet &&
                normalized != LogoThemeProvider.ThemeNationalDay)
            {
                normalized = LogoThemeProvider.ThemeNormal;
            }

            await _provider.UpdateAsync(normalized, User?.Identity?.Name);
            _provider.ClearCache();
            TempData["Success"] = "Đã cập nhật chế độ giao diện logo.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ClearCache()
        {
            _provider.ClearCache();
            TempData["Success"] = "Đã làm mới cache giao diện logo.";
            return RedirectToAction(nameof(Index));
        }
    }
}
