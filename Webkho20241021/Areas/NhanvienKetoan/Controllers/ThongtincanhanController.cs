using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Webkho_20241021.Controllers.Base;
using Webkho_20241021.Models;

namespace Webkho_20241021.Areas.NhanvienKetoan.Controllers
{
    [Area("NhanvienKetoan")]
    [Authorize(Roles = "Nhân viên-BP kế toán,Nhân viên kế toán")]
    public class ThongtincanhanController : BaseThongtincanhanController
    {
        public ThongtincanhanController(ApplicationDbContext context) : base(context)
        {
        }
    }
}
