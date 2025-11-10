using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using Webkho_20241021.Areas.NhanvienKythuat.Data;
using Webkho_20241021.Models;

namespace Webkho_20241021.Areas.NhanvienKythuat.Controllers
{
    [Area("NhanvienKythuat")]
    [Authorize(Roles = "Nhân viên-BP kỹ thuật")]
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
            // Không cần load KhoDuan từ khoduans nữa vì sẽ load từ vtphieuxuatkho qua AJAX khi click vào mã dự án
            var model = new Duanviewmodel
            {
                Duan = Duanlist,
                KhoDuan = new List<khoduans>() // Trả về list rỗng, sẽ được load qua GetVTDuan
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
            // Đọc từ vtphieuxuatkho - vật tư đã được xuất kho cho dự án
            // Join với phieuxuatkho để lấy vật tư của dự án này
            var vatTuList = from vt in _context.vtphieuxuatkho
                           join px in _context.phieuxuatkho on vt.MaXuatkho equals px.MaXuatkho
                           where px.MaDuan == MaDuan
                                 && (vt.TrangThai == "Đã xác nhận nhận hàng" 
                                     || vt.TrangThai == "Đã xuất kho" 
                                     || px.TrangThai == "Đã xác nhận nhận hàng" 
                                     || px.TrangThai == "Hoàn thành")
                           select new {
                               TenSanpham = vt.TenSanpham,
                               MaSanpham = vt.MaSanpham,
                               DAMakho = vt.Makho,
                               HangSX = vt.HangSX,
                               NhaCC = vt.NhaCC,
                               SL = vt.SL,
                               DonVi = vt.DonVi,
                               NgayNhapkho = vt.NgayNhapkho,
                               NgayBaohanh = vt.NgayBaohanh,
                               ThoiGianBH = vt.ThoiGianBH,
                               TrangThai = vt.TrangThai ?? "Đã xác nhận nhận hàng"
                           };

            // Remove duplicates nếu có (cùng vật tư từ nhiều phiếu)
            var result = vatTuList.GroupBy(v => new { v.MaSanpham, v.DAMakho })
                                 .Select(g => g.First())
                                 .ToList();

            return Json(result); // Trả về JSON
        }

        [HttpPost]
        public IActionResult ThemDuanSQL(duans duans, nguoidungs nguoidungs)
        {
            duans.TrangThai = "Chờ";

            _context.duans.Add(duans);
            _context.SaveChanges();

            return RedirectToAction("Duan", "Duan", new { area = "NhanvienKythuat" });
        }

        [HttpPost]
        public async Task<IActionResult> Xuliduan(string MaDuan, string action)
        {

            var duan = await _context.duans.FirstOrDefaultAsync(d => d.MaDuan == MaDuan);
            if (duan == null)
            {
                return NotFound();
            }
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

            _context.Update(duan);
            await _context.SaveChangesAsync();

            return RedirectToAction("Duan", "Duan", new { area = "NhanvienKythuat" });
        }
    }
}
