using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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
            ViewBag.CurrentUserId = HttpContext.Session.GetString("MaNguoidung");
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
            // Join với nguoidungs để lấy thông tin người nhận
            var vatTuList = from vt in _context.vtphieuxuatkho
                           join px in _context.phieuxuatkho on vt.MaXuatkho equals px.MaXuatkho
                           join nd in _context.nguoidungs on px.MaNguoidung equals nd.MaNguoidung into ndGroup
                           from nd in ndGroup.DefaultIfEmpty()
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
                               NgayNhapkho = vt.NgayNhapkho ?? px.NgayXacNhanNhan,
                               NgayBaohanh = vt.NgayBaohanh,
                               ThoiGianBH = vt.ThoiGianBH,
                               TrangThai = vt.TrangThai ?? "Đã xác nhận nhận hàng",
                               MaNguoiNhan = px.MaNguoidung,
                               TenNguoiNhan = nd != null ? nd.TenNguoidung : (px.MaNguoidung ?? "")
                           };

            // Không group để giữ thông tin người nhận cho từng lần cấp phát
            var result = vatTuList
                .OrderByDescending(v => v.NgayNhapkho)
                .ThenBy(v => v.TenSanpham)
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
            var currentUserId = HttpContext.Session.GetString("MaNguoidung");
            if (string.IsNullOrWhiteSpace(currentUserId) ||
                string.IsNullOrWhiteSpace(duan.MaNguoiQLDA) ||
                !string.Equals(currentUserId.Trim(), duan.MaNguoiQLDA.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Chỉ người quản lý dự án được phép cập nhật tiến trình dự án này.";
                return RedirectToAction("Duan", "Duan", new { area = "NhanvienKythuat" });
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
