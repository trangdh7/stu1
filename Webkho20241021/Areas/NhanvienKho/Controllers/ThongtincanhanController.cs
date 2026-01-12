using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Webkho_20241021.Controllers.Base;
using Webkho_20241021.Models;

namespace Webkho_20241021.Areas.NhanvienKho.Controllers
{
    [Area("NhanvienKho")]
    [Authorize(Roles = "Nhân viên-BP kho,Nhân viên kho")]
    public class ThongtincanhanController : BaseThongtincanhanController
    {
        public ThongtincanhanController(ApplicationDbContext context) : base(context)
        {
        }
    }
}

