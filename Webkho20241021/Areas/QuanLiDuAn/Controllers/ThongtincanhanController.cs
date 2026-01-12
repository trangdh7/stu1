using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Webkho_20241021.Controllers.Base;
using Webkho_20241021.Models;

namespace Webkho_20241021.Areas.QuanLiDuAn.Controllers
{
    [Area("QuanLiDuAn")]
    [Authorize(Roles = "Quản lí dự án")]
    public class ThongtincanhanController : BaseThongtincanhanController
    {
        public ThongtincanhanController(ApplicationDbContext context) : base(context)
        {
        }
    }
}
