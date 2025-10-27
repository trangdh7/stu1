using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Webkho_20241021.Areas.TruongBPMuahang.Data;
using Webkho_20241021.Models;

namespace Webkho_20241021.Areas.TruongBPMuahang.Controllers
{
    [Area("TruongBPMuahang")]
    [Authorize(Roles = "Trưởng BP-BP mua hàng")]
    public class DuanController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DuanController(ApplicationDbContext context)
        {
            _context = context;
        }
        public IActionResult Duan()
        {
            var Duanlist = _context.duans.ToList();
            var Khoduanlist = _context.khoduans.ToList();
            var model = new Duanviewmodel
            {
                Duan = Duanlist,
                KhoDuan = Khoduanlist
            };
            return View(model);
        }
        public IActionResult ThemDuan()
        {
            var Tennguoidunglist = _context.nguoidungs
                              .Select(n => new { n.TenNguoidung, n.MaNguoidung })  // Lấy cả MaNguoidung
                              .ToList();

            ViewBag.Tennguoidunglist = Tennguoidunglist;
            return View();
        }

        public IActionResult GetVTDuan(string MaDuan)
        {
            var vatTuList = _context.khoduans
                                 .Where(v => v.DAMaDuan == MaDuan)
                                 .Select(v => new {
                                     v.TenSanpham,
                                     v.MaSanpham,
                                     v.DAMakho,
                                     v.HangSX,
                                     v.NhaCC,
                                     v.SL,
                                     v.DonVi,
                                     v.NgayNhapkho,
                                     v.NgayBaohanh,
                                     v.ThoiGianBH,
                                     v.TrangThai
                                 }).ToList();

            return Json(vatTuList); // Trả về JSON
        }

        [HttpPost]
        public IActionResult ThemDuanSQL(duans duans)
        {
            duans.TrangThai = "Chờ";

            _context.duans.Add(duans);
            _context.SaveChanges();

            return RedirectToAction("Duan", "Duan", new { area = "TruongBPMuahang" });
        }

        [HttpPost]
        public async Task<IActionResult> Xuliduan(string MaDuan, string action)
        {

            var duan = await _context.duans.FirstOrDefaultAsync(d => d.MaDuan == MaDuan);
            if (duan == null)
            {
                return NotFound();
            }
            // Xử lý các hành động dựa trên trạng thái hiện tại và giá trị action
            if (action == "start" && duan.TrangThai == "Chờ")
            {
                duan.NgayBatdau = DateTime.Now;
                duan.TrangThai = "Đang triển khai";
            }
            else if (action == "end" && duan.TrangThai == "Đang triển khai")
            {
                duan.NgayKetthuc = DateTime.Now;
                duan.TrangThai = "Đã hoàn thành";
            }

            // Lưu thay đổi vào cơ sở dữ liệu
            _context.Update(duan);
            await _context.SaveChangesAsync();

            // Quay lại trang danh sách dự án
            return RedirectToAction("Duan", "Duan", new { area = "TruongBPMuahang" });
        }
    }
}
