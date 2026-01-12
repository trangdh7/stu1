using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Webkho_20241021.Models;
using Webkho_20241021.Services;
using Webkho_20241021.Areas.TruongBPKho.Services;
using System;
using System.Linq;

namespace Webkho_20241021.Controllers
{
    
    [Authorize]
    public abstract class BaseYeucauController : Controller
    {
        protected readonly ApplicationDbContext _context;
        protected readonly YeucauService _yeucauService;
        protected readonly PhieuService _phieuService;
        protected readonly ThongbaoService _thongbaoService;

        public BaseYeucauController(ApplicationDbContext context)
        {
            _context = context;
            _yeucauService = new YeucauService(context);
            _phieuService = new PhieuService(context);
            _thongbaoService = new ThongbaoService(context);
        }

        
        [HttpGet]
        public virtual IActionResult GetDulieuThongbao()
        {
            var chucVu = HttpContext.Session.GetString("Chucvu");
            var boPhan = HttpContext.Session.GetString("Bophan");
            var maNv = HttpContext.Session.GetString("MaNguoidung");
            var result = _thongbaoService.GetThongBao(chucVu, boPhan, maNv);
            return Json(result);
        }

        
        [HttpPost]
        public virtual IActionResult DongsBoTrangThaiVatTu(string MaXuatkho)
        {
            try
            {
                var phieu = _context.phieuxuatkho.FirstOrDefault(p => p.MaXuatkho == MaXuatkho);
                
                if (phieu != null && phieu.TrangThai == "Đã nhận hàng")
                {
                    var VTphieuxuatkhoList = _context.vtphieuxuatkho
                        .Where(vt => vt.MaXuatkho == MaXuatkho)
                        .ToList();

                    foreach (var vt in VTphieuxuatkhoList)
                    {
                        // Chỉ cập nhật nếu trạng thái chưa đúng
                        if (vt.TrangThai != "Đã nhận hàng" && vt.TrangThai != "Đã xuất kho")
                        {
                            vt.TrangThai = "Đã nhận hàng";
                            _context.vtphieuxuatkho.Update(vt);
                        }
                    }

                    _context.SaveChanges();
                    return Json(new { success = true, message = "Đã đồng bộ trạng thái vật tư!" });
                }

                return Json(new { success = false, message = "Phiếu không hợp lệ!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

       
        [HttpGet]
        public virtual IActionResult AutoDongBoTrangThai()
        {
            try
            {
                var currentUserId = HttpContext.Session.GetString("MaNguoidung");
                
                // Lấy các yêu cầu mà người dùng này đã tạo
                var yeuCauList = _context.yeucau
                    .Where(y => y.YCMaNguoidung == currentUserId)
                    .Select(y => y.MaYeucau)
                    .ToList();

                // Lấy các phiếu đã xác nhận nhận hàng
                var phieuxuatkhoList = _context.phieuxuatkho
                    .Where(p => yeuCauList.Contains(p.MaYeucau)
                             && p.TrangThai == "Đã nhận hàng")
                    .ToList();

                int updatedCount = 0;
                foreach (var phieu in phieuxuatkhoList)
                {
                    var VTphieuxuatkhoList = _context.vtphieuxuatkho
                        .Where(vt => vt.MaXuatkho == phieu.MaXuatkho
                                 && vt.TrangThai != "Đã nhận hàng"
                                 && vt.TrangThai != "Đã xuất kho")
                        .ToList();

                    foreach (var vt in VTphieuxuatkhoList)
                    {
                        vt.TrangThai = "Đã nhận hàng";
                        _context.vtphieuxuatkho.Update(vt);
                        updatedCount++;
                    }
                }

                _context.SaveChanges();
                return Json(new { success = true, message = $"Đã đồng bộ {updatedCount} vật tư!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        /// <summary>
        /// In phiếu trả hàng - Method chung cho tất cả Areas
        /// </summary>
        [HttpGet]
        public virtual IActionResult InPhietrahang(string MaNhapkho)
        {
            if (string.IsNullOrEmpty(MaNhapkho))
            {
                return NotFound();
            }

            var phieunhapkho = _context.phieunhapkho
                .FirstOrDefault(p => p.MaNhapkho == MaNhapkho);

            if (phieunhapkho == null)
            {
                return NotFound();
            }

            var vtphieunhapkho = _context.vtphieunhapkho
                .Where(vt => vt.MaNhapkho == MaNhapkho)
                .ToList();

            var yeucau = _context.yeucau
                .FirstOrDefault(y => y.MaYeucau == phieunhapkho.MaYeucau);

            var nguoiBanGiao = !string.IsNullOrEmpty(phieunhapkho.MaNguoidung)
                ? _context.nguoidungs.FirstOrDefault(n => n.MaNguoidung == phieunhapkho.MaNguoidung)
                : null;

            string tenNguoiBanGiao = "";
            string bophanNguoiBanGiao = "";
            if (yeucau != null)
            {
                tenNguoiBanGiao = yeucau.NguoiYeucau ?? "";
                var nguoiYeuCau = !string.IsNullOrEmpty(yeucau.YCMaNguoidung)
                    ? _context.nguoidungs.FirstOrDefault(n => n.MaNguoidung == yeucau.YCMaNguoidung)
                    : null;
                bophanNguoiBanGiao = nguoiYeuCau?.Bophan ?? "";
            }
            else if (nguoiBanGiao != null)
            {
                tenNguoiBanGiao = nguoiBanGiao.TenNguoidung ?? "";
                bophanNguoiBanGiao = nguoiBanGiao.Bophan ?? "";
            }

            // Lấy thông tin dự án
            var duan = !string.IsNullOrEmpty(phieunhapkho.MaDuan)
                ? _context.duans.FirstOrDefault(d => d.MaDuan == phieunhapkho.MaDuan)
                : null;
            string tenDuan = duan?.TenDuan ?? "";
            string maDuan = duan?.MaDuan ?? "";

            // Lấy thông tin Trưởng BP Kho làm người nhận
            var nguoiNhan = _context.nguoidungs
                .FirstOrDefault(n => n.Chucvu == "Trưởng BP" && n.Bophan == "BP kho");

            ViewBag.Phieunhapkho = phieunhapkho;
            ViewBag.VTPhieunhapkho = vtphieunhapkho;
            ViewBag.Yeucau = yeucau;
            ViewBag.TenNguoiBanGiao = tenNguoiBanGiao;
            ViewBag.BophanNguoiBanGiao = bophanNguoiBanGiao;
            ViewBag.TenDuan = tenDuan;
            ViewBag.MaDuan = maDuan;
            ViewBag.NguoiNhan = nguoiNhan;

            return View();
        }
    }
}
