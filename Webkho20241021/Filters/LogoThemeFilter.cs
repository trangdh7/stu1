using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Webkho_20241021.Services;

namespace Webkho_20241021.Filters
{
    /// <summary>
    /// Gán ViewBag.LogoTheme cho mọi view để layout hiển thị logo theo chế độ (Normal / Tet / NationalDay).
    /// </summary>
    public class LogoThemeFilter : IAsyncActionFilter
    {
        private readonly ILogoThemeProvider _logoThemeProvider;

        public LogoThemeFilter(ILogoThemeProvider logoThemeProvider)
        {
            _logoThemeProvider = logoThemeProvider;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            try
            {
                var settings = await _logoThemeProvider.GetAsync();
                if (context.Controller is Controller controller)
                {
                    controller.ViewBag.LogoTheme = settings?.Theme ?? "Normal";
                }
            }
            catch
            {
                if (context.Controller is Controller controller)
                {
                    controller.ViewBag.LogoTheme = "Normal";
                }
            }

            await next();
        }
    }
}
