using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Webkho_20241021.Controllers.Base;
using Webkho_20241021.Models;

namespace Webkho_20241021.Areas.NhanvienKythuat.Controllers
{
    [Area("NhanvienKythuat")]
    [Authorize(Roles = "Nhân viên-BP kỹ thuật")]
    public class ThongtincanhanController : BaseThongtincanhanController
    {
        public ThongtincanhanController(ApplicationDbContext context) : base(context)
        {
        }
    }
}
