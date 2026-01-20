using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Webkho_20241021.Areas.Admin.Models;
using Webkho_20241021.Models;
using Webkho_20241021.Services;

namespace Webkho_20241021.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class EmailSettingsController : Controller
    {
        private readonly IEmailSettingsProvider _provider;

        public EmailSettingsController(IEmailSettingsProvider provider)
        {
            _provider = provider;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var settings = await _provider.GetAsync();
            var vm = new EmailSettingsViewModel
            {
                SmtpServer = settings.SmtpServer,
                SmtpPort = settings.SmtpPort,
                FromEmail = settings.FromEmail,
                FromName = settings.FromName,
                FromPassword = settings.FromPassword // Trả lại mật khẩu để hiển thị
            };

            ViewBag.HasPassword = !string.IsNullOrWhiteSpace(settings.FromPassword);
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(EmailSettingsViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var update = new EmailSetting
            {
                SmtpServer = model.SmtpServer,
                SmtpPort = model.SmtpPort,
                FromEmail = model.FromEmail,
                FromName = model.FromName,
                FromPassword = string.IsNullOrWhiteSpace(model.FromPassword) ? null : model.FromPassword
            };

            await _provider.UpdateAsync(update, User?.Identity?.Name);
            TempData["Success"] = "Cập nhật cấu hình email thành công.";

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ClearCache()
        {
            _provider.ClearCache();
            TempData["Success"] = "Đã làm mới cache cấu hình email.";
            return RedirectToAction(nameof(Index));
        }
    }
}
