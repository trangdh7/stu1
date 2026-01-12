using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Webkho_20241021.Controllers.Base;
using Webkho_20241021.Models;

namespace Webkho_20241021.Areas.NhanvienMuahang.Controllers
{
    [Area("NhanvienMuahang")]
    [Authorize(Roles = "Nhân viên-BP mua hàng,Nhân viên mua hàng")]
    public class ThongtincanhanController : BaseThongtincanhanController
    {
        public ThongtincanhanController(ApplicationDbContext context) : base(context)
        {
        }
    }
}
