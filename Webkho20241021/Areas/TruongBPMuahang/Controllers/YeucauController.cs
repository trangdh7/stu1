using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using Webkho_20241021.Areas.TruongBPMuahang.Data;
using Webkho_20241021.Services;
using Webkho_20241021.Models;
using Webkho_20241021.Services;
using Webkho_20241021.Helpers;
using OfficeOpenXml;
using Microsoft.Extensions.DependencyInjection;


namespace Webkho_20241021.Areas.TruongBPMuahang.Controllers
{
    [Area("TruongBPMuahang")]
    [Authorize(Roles = "Trưởng BP-BP mua hàng")]
    public class YeucauController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IYeucauCodeService _yeucauCodeService;
        private readonly IPhieuCodeService _phieuCodeService;
        public YeucauController(ApplicationDbContext context, EmailService emailService, IServiceScopeFactory serviceScopeFactory, IYeucauCodeService yeucauCodeService, IPhieuCodeService phieuCodeService)
        {
            _context = context;
            _emailService = emailService;
            _serviceScopeFactory = serviceScopeFactory;
            _yeucauCodeService = yeucauCodeService;
            _phieuCodeService = phieuCodeService;
        }

        private void SendRejectionEmailAsync(string maYeucau, string ghiChu = "")
        {
            // Tạo scope mới để tránh lỗi DbContext thread-safe
            _ = Task.Run(async () =>
            {
                try
                {
                    using (var scope = _serviceScopeFactory.CreateScope())
                    {
                        var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                        System.Diagnostics.Debug.WriteLine($"[TruongBPMuahang] Bắt đầu gửi email từ chối cho {maYeucau}");
                        await emailService.SendNotificationToRequesterOnRejectionAsync(maYeucau, ghiChu);
                        System.Diagnostics.Debug.WriteLine($"[TruongBPMuahang] Đã gửi email từ chối cho {maYeucau}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TruongBPMuahang] Lỗi gửi email từ chối cho {maYeucau}: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[TruongBPMuahang] Stack trace: {ex.StackTrace}");
                }
            });
        }

        private void SendWarehouseNotificationOnNhapKhoAsync(string maNhapkho)
        {
            // Tạo scope mới để tránh lỗi DbContext thread-safe khi gửi email song song với SaveChanges
            _ = Task.Run(async () =>
            {
                try
                {
                    using (var scope = _serviceScopeFactory.CreateScope())
                    {
                        var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                        await emailService.SendNotificationToWarehouseOnNhapKhoAsync(maNhapkho);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TruongBPMuahang/Taophieunhapkhobyphieumuahang] Lỗi gửi email nhập kho: {ex.Message}");
                }
            });
        }
        public IActionResult Yeucau(string search = "")
        {
            var userRole = HttpContext.Session.GetString("Chucvu");

            var Yeucaulist = _context.yeucau.ToList();

            var PhieuMuaHangList = _context.phieumuahang.ToList();

            foreach (var yeucau in Yeucaulist)
            {
                var phieus = PhieuMuaHangList.Where(p => p.MaYeucau == yeucau.MaYeucau).ToList();

                if (phieus.Any(p => p.TrangThai != "Đã nhận hàng"))
                {
                    yeucau.TrangThai = "Đang mua hàng";
                }
            }

            _context.SaveChanges();

            // Populate TenYeucau and Bophan từ nguoidungs nếu chưa có
            var nguoiDungDict = _context.nguoidungs.ToDictionary(n => n.MaNguoidung, n => new { n.TenNguoidung, n.Bophan });
            foreach (var yeucau in Yeucaulist)
            {
                // Populate Bophan từ nguoidungs nếu chưa có
                if (string.IsNullOrWhiteSpace(yeucau.Bophan) && !string.IsNullOrWhiteSpace(yeucau.YCMaNguoidung))
                {
                    if (nguoiDungDict.TryGetValue(yeucau.YCMaNguoidung, out var nguoiDung))
                    {
                        yeucau.Bophan = nguoiDung.Bophan ?? "";
                    }
                }

                // Populate TenYeucau nếu chưa có
                if (string.IsNullOrWhiteSpace(yeucau.TenYeucau))
                {
                    // Nếu là yêu cầu nhập kho đặc biệt (NHAPKHO_), set mặc định
                    if (!string.IsNullOrEmpty(yeucau.MaYeucau) && yeucau.MaYeucau.StartsWith("NHAPKHO_"))
                    {
                        yeucau.TenYeucau = "Yêu cầu nhập kho";
                    }
                    else
                    {
                        // Các yêu cầu khác, set mặc định là "Yêu cầu vật tư"
                        yeucau.TenYeucau = "Yêu cầu vật tư";
                    }
                }
            }

            var SortedYeucaulist = Yeucaulist
                .OrderByDescending(y => y.TrangThai == userRole)
                .ThenBy(y => YeucauUpdateHelper.GetBaseRequestCode(y.MaYeucau ?? "")) // Nhóm theo mã cơ bản
                .ThenByDescending(y => y.NgayYeucau) // Trong cùng nhóm, sắp xếp theo ngày giảm dần
                .ToList();

            // Áp dụng tìm kiếm nếu có
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchTerm = search.Trim().ToLower();
                SortedYeucaulist = SortedYeucaulist
                    .Where(y =>
                        (y.MaYeucau != null && y.MaYeucau.ToLower().Contains(searchTerm)) ||
                        (y.TenYeucau != null && y.TenYeucau.ToLower().Contains(searchTerm)) ||
                        (y.NguoiYeucau != null && y.NguoiYeucau.ToLower().Contains(searchTerm)) ||
                        (y.Bophan != null && y.Bophan.ToLower().Contains(searchTerm)) ||
                        (y.YCMaNguoidung != null && y.YCMaNguoidung.ToLower().Contains(searchTerm)) ||
                        (y.YCMaDuan != null && y.YCMaDuan.ToLower().Contains(searchTerm)) ||
                        (y.TrangThai != null && y.TrangThai.ToLower().Contains(searchTerm))
                    )
                    .ToList();
            }

            // Chỉ hiển thị các vật tư có SLMoi > 0
            var VTyeucaulist = _context.vtyeucau
                .Where(v => v.SLMoi.HasValue && v.SLMoi.Value > 0)
                .ToList();
            var Duans = _context.duans.ToList();

            var maSanphamList = VTyeucaulist.Where(v => !string.IsNullOrWhiteSpace(v.MaSanpham)).Select(v => v.MaSanpham!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var maSanphamSet = new HashSet<string>(maSanphamList, StringComparer.OrdinalIgnoreCase);
            var tonKhoByMaSanpham = _context.khotongs.Where(k => k.MaSanpham != null).Select(k => new { k.MaSanpham, k.SL }).ToList()
                .Where(k => maSanphamSet.Contains(k.MaSanpham!)).GroupBy(k => k.MaSanpham!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.SL ?? 0), StringComparer.OrdinalIgnoreCase);

            var model = new Yeucauviewmodel
            {
                Yeucau = SortedYeucaulist,
                VTyeucau = VTyeucaulist,
                Duans = Duans
            };

            ViewBag.Search = search;
            ViewBag.TonKhoByMaSanpham = tonKhoByMaSanpham;
            return View(model);
        }

        /// <summary>
        /// Xóa yêu cầu khi Trưởng BP chưa duyệt hoặc đã duyệt nhưng QLDA/Giám đốc chưa duyệt.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult XoaYeucau(string MaYeucau)
        {
            if (string.IsNullOrWhiteSpace(MaYeucau))
            {
                TempData["ErrorMessage"] = "Mã yêu cầu không hợp lệ.";
                return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPMuahang" });
            }
            var chucVu = HttpContext.Session.GetString("Chucvu");
            var boPhan = HttpContext.Session.GetString("Bophan");
            var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
            if (yeucau == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy yêu cầu.";
                return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPMuahang" });
            }
            if (!YeucauDeleteHelper.CoTheXoaYeucauTruongBP(yeucau, chucVu ?? "", boPhan ?? ""))
            {
                TempData["ErrorMessage"] = "Bạn chỉ được xóa yêu cầu khi chưa đến bước QLDA/Giám đốc duyệt.";
                return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPMuahang" });
            }
            try
            {
                YeucauDeleteHelper.XoaYeucauVaPhieuLienQuan(_context, MaYeucau);
                TempData["SuccessMessage"] = "Đã xóa yêu cầu thành công.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Lỗi khi xóa: " + ex.Message;
            }
            return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPMuahang" });
        }

        public IActionResult Phieuxuatkho(string search = "")
        {
            var Phieuxuatkholist = _context.phieuxuatkho
            .OrderByDescending(y => y.TrangThai == "Chờ lấy hàng")
            .ThenByDescending(y => y.TrangThai == "Đang chuẩn bị hàng")
            .ThenByDescending(y => y.NgayXuatkho)
            .ToList();

            // Áp dụng tìm kiếm nếu có
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchTerm = search.Trim().ToLower();
                Phieuxuatkholist = Phieuxuatkholist
                    .Where(p =>
                        (p.MaXuatkho != null && p.MaXuatkho.ToLower().Contains(searchTerm)) ||
                        (p.MaYeucau != null && p.MaYeucau.ToLower().Contains(searchTerm)) ||
                        (p.MaDuan != null && p.MaDuan.ToLower().Contains(searchTerm)) ||
                        (p.MaNguoidung != null && p.MaNguoidung.ToLower().Contains(searchTerm)) ||
                        (p.TrangThai != null && p.TrangThai.ToLower().Contains(searchTerm)) ||
                        (p.GhiChu != null && p.GhiChu.ToLower().Contains(searchTerm))
                    )
                    .ToList();
            }

            var VTphieuxuatkholist = _context.vtphieuxuatkho.ToList();
            var model = new Phieuxuatkhoviewmodel
            {
                Phieuxuatkho = Phieuxuatkholist,
                VTphieuxuatkho = VTphieuxuatkholist,
            };
            ViewBag.Search = search;
            return View(model);
        }

        public IActionResult Phieunhapkho(string search = "")
        {
            var Phieunhapkholist = _context.phieunhapkho
            .OrderByDescending(y => y.NgayNhapkho)
            .ToList();

            // Áp dụng tìm kiếm nếu có
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchTerm = search.Trim().ToLower();
                Phieunhapkholist = Phieunhapkholist
                    .Where(p =>
                        (p.MaNhapkho != null && p.MaNhapkho.ToLower().Contains(searchTerm)) ||
                        (p.MaYeucau != null && p.MaYeucau.ToLower().Contains(searchTerm)) ||
                        (p.MaDuan != null && p.MaDuan.ToLower().Contains(searchTerm)) ||
                        (p.MaNguoidung != null && p.MaNguoidung.ToLower().Contains(searchTerm)) ||
                        (p.TrangThai != null && p.TrangThai.ToLower().Contains(searchTerm))
                    )
                    .ToList();
            }

            var VTphieunhapkholist = _context.vtphieunhapkho.ToList();
            var model = new Phieunhapkhoviewmodel
            {
                Phieunhapkho = Phieunhapkholist,
                VTphieunhapkho = VTphieunhapkholist,
            };
            ViewBag.Search = search;
            return View(model);
        }

        public IActionResult Phieumuahang(string search = "")
        {
            var Phieumuahanglist = _context.phieumuahang
            .OrderByDescending(y => y.TrangThai == "Đã thanh toán")
            .ThenByDescending(y => y.TrangThai == "Đang chờ báo giá")
            .ThenByDescending(y => y.NgayMuahang)
            .ToList();
            // Gán tên Người yêu cầu cho từng phiếu mua hàng
            var nguoiDungDict = _context.nguoidungs.ToDictionary(n => n.MaNguoidung, n => n.TenNguoidung);
            // Lấy Ngày cần từ bảng vtyeucau (vật tư chi tiết) - lấy ngày sớm nhất
            var vtyeucauDict = _context.vtyeucau
                .Where(v => v.NgayCanHang != null)
                .GroupBy(v => v.VTMaYeucau)
                .ToDictionary(g => g.Key, g => g.Min(v => v.NgayCanHang));
            // Lấy Ngày yêu cầu từ bảng yeucau
            var yeucauDict = _context.yeucau
                .Where(y => y.NgayYeucau != null)
                .ToDictionary(y => y.MaYeucau, y => y.NgayYeucau);
            foreach (var phieu in Phieumuahanglist)
            {
                if (!string.IsNullOrEmpty(phieu.MaNguoidung) && nguoiDungDict.TryGetValue(phieu.MaNguoidung, out var ten))
                {
                    phieu.TenNguoiyeucau = ten;
                }
                // Gán Ngày cần từ vtyeucau (vật tư chi tiết) - lấy ngày sớm nhất
                if (!string.IsNullOrEmpty(phieu.MaYeucau) && vtyeucauDict.TryGetValue(phieu.MaYeucau, out var ngayCanHang))
                {
                    phieu.NgayCanHang = ngayCanHang;
                }
                // Gán Ngày yêu cầu từ yeucau
                if (!string.IsNullOrEmpty(phieu.MaYeucau) && yeucauDict.TryGetValue(phieu.MaYeucau, out var ngayYeucau))
                {
                    phieu.NgayYeucau = ngayYeucau;
                }
            }

            // Áp dụng tìm kiếm nếu có
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchTerm = search.Trim().ToLower();
                Phieumuahanglist = Phieumuahanglist
                    .Where(p =>
                        (p.MaMuahang != null && p.MaMuahang.ToLower().Contains(searchTerm)) ||
                        (p.MaYeucau != null && p.MaYeucau.ToLower().Contains(searchTerm)) ||
                        (p.MaDuan != null && p.MaDuan.ToLower().Contains(searchTerm)) ||
                        (p.MaNguoidung != null && p.MaNguoidung.ToLower().Contains(searchTerm)) ||
                        (p.TenNguoiyeucau != null && p.TenNguoiyeucau.ToLower().Contains(searchTerm)) ||
                        (p.TrangThai != null && p.TrangThai.ToLower().Contains(searchTerm)) ||
                        (p.GhiChu != null && p.GhiChu.ToLower().Contains(searchTerm))
                    )
                    .ToList();
            }

            var VTphieumuahanglist = _context.vtphieumuahang.ToList();
            var model = new Phieumuahangviewmodel
            {
                Phieumuahang = Phieumuahanglist,
                VTphieumuahang = VTphieumuahanglist,
            };
            ViewBag.Search = search;
            return View(model);
        }

        [HttpGet]
        public IActionResult GetDulieuThongbao()
        {
            var chucVu = HttpContext.Session.GetString("Chucvu");
            var boPhan = HttpContext.Session.GetString("Bophan");
            var maNv = HttpContext.Session.GetString("MaNguoidung");

            int thongbaomuahangcount = 0;
            if (boPhan == "BP mua hàng")
            {
                // Đếm phiếu mua hàng có trạng thái "Đang chờ báo giá" (cần báo giá), "Đã thanh toán" (cần xác nhận nhận hàng), 
                // hoặc "Giám đốc - Đã từ chối" (cần báo giá lại)
                thongbaomuahangcount = _context.phieumuahang.Count(p => 
                    p.TrangThai == "Đang chờ báo giá" || 
                    p.TrangThai == "Đã thanh toán" || 
                    (p.TrangThai != null && p.TrangThai.Contains("Đã từ chối")));
            }
            else if (boPhan == "BP kế toán")
            {
                thongbaomuahangcount = _context.phieumuahang.Count(p => p.TrangThai == "Chờ thanh toán");
            }

            // Xuất kho - chỉ đếm các trạng thái còn cần xử lý (không đếm "Hoàn thành" và "Đã xác nhận nhận hàng")
            int thongbaoxuatkhocount = 0;
            if (boPhan == "BP kho")
            {
                thongbaoxuatkhocount = _context.phieuxuatkho.Count(p => p.TrangThai != "Hoàn thành" && p.TrangThai != "Đã xác nhận nhận hàng");
            }

            int thongbaonhapkhocount = 0;
            if (boPhan == "BP kho")
            {
                thongbaonhapkhocount = _context.phieunhapkho.Count(p => p.TrangThai == "Chờ nhập kho" || p.TrangThai == "Sẵn sàng nhập kho");
            }

            var Maduanquanli = _context.duans
                .Where(d => d.MaNguoiQLDA == maNv)
                .Select(d => d.MaDuan)
                .ToList();
            int QLDAyeucaucount = _context.yeucau.Count(p => p.TrangThai == "Chờ quản lý dự án duyệt" && Maduanquanli.Contains(p.YCMaDuan));
            int Duyetyeucaucount = _context.yeucau.Count(p => p.TrangThai == ("Chờ Trưởng Phòng bộ phận " + boPhan + " duyệt"));
            int thongbaoyeucaucount = Duyetyeucaucount + QLDAyeucaucount;

            // Thông báo xác nhận nhận hàng - đếm phiếu xuất kho chờ xác nhận
            int thongbaoxacnhannhanhangcount = 0;
            var yeuCauList = _context.yeucau
                .Where(y => y.YCMaNguoidung == maNv)
                .Select(y => y.MaYeucau)
                .ToList();
            thongbaoxacnhannhanhangcount = _context.phieuxuatkho
                .Count(p => yeuCauList.Contains(p.MaYeucau) && p.TrangThai == "Chờ người yêu cầu xác nhận");

            return Json(new
            {
                thongbaoyeucaucount,
                thongbaomuahangcount,
                thongbaoxuatkhocount,
                thongbaonhapkhocount,
                thongbaoxacnhannhanhangcount
            });
        }

        [HttpGet]
        public IActionResult GetDulieuThongbaolayout()
        {
            return GetDulieuThongbao();
        }

        [HttpGet]
        public IActionResult GetDulieuThongbaotrangchu()
        {
            return GetDulieuThongbao();
        }

        [HttpGet]
        public IActionResult GetVTYeucau(string MaYeucau)
        {
            if (string.IsNullOrWhiteSpace(MaYeucau))
            {
                return Json(new List<object>());
            }

            // Nếu yêu cầu có dòng chi tiết trong vtyeucau thì ưu tiên hiển thị
            // danh sách đó (yêu cầu vật tư gốc), kể cả khi đã có phiếu nhập kho.
            bool hasVatTuYeuCau = _context.vtyeucau
                .Any(v => v.VTMaYeucau == MaYeucau);

            if (hasVatTuYeuCau)
            {
                var vatTuList = _context.vtyeucau.Where(v => v.VTMaYeucau == MaYeucau).ToList();
                var maSanphamList = vatTuList.Where(v => !string.IsNullOrWhiteSpace(v.MaSanpham)).Select(v => v.MaSanpham!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var maSanphamSet = new HashSet<string>(maSanphamList, StringComparer.OrdinalIgnoreCase);
                var tonKhoByMaSanpham = _context.khotongs.Where(k => k.MaSanpham != null).Select(k => new { k.MaSanpham, k.SL }).ToList()
                    .Where(k => maSanphamSet.Contains(k.MaSanpham!)).GroupBy(k => k.MaSanpham!, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.SL ?? 0), StringComparer.OrdinalIgnoreCase);
                var result = vatTuList.Select(v =>
                {
                    var slMoi = v.SLMoi ?? v.SL ?? 0;
                    var tonKho = !string.IsNullOrWhiteSpace(v.MaSanpham) && tonKhoByMaSanpham.TryGetValue(v.MaSanpham, out var tk) ? tk : 0;
                    // Thiếu = Yêu cầu - Đã xuất (khi đã xuất đủ thì thiếu = 0, không dùng tồn kho vì sau khi xuất tồn kho = 0)
                    var slDaXuatThucTe = !string.IsNullOrWhiteSpace(v.MaSanpham) ? YeucauUpdateHelper.TinhSoLuongDaCap(_context, MaYeucau, v.MaSanpham) : 0;
                    var slThieu = YeucauUpdateHelper.TinhSoLuongConThieuTheoMaYeuCauCoBan(_context, MaYeucau, v.MaSanpham ?? "");
                    var isDaXuatKho = (v.TrangThai ?? "").IndexOf("Đã xuất kho", StringComparison.OrdinalIgnoreCase) >= 0;
                    var slDaXuat = slDaXuatThucTe > 0 ? (int?)slDaXuatThucTe : (isDaXuatKho ? (v.SL ?? v.SLMoi) : (int?)null);
                    return new { v.ID, v.TT, v.VTMaYeucau, v.TenSanpham, v.MaSanpham, v.YCMakho, v.HangSX, v.NhaCC, v.SLCu, v.SLMoi, v.SL, v.DonVi, v.NgayCanHang, v.NgayCoHang, v.NgayNhapkho, v.NgayBaohanh, v.ThoiGianBH, v.NgayDuyet, v.TrangThai, v.GhiChu, TonKho = tonKho, SlThieu = slThieu, SlDaXuat = slDaXuat };
                }).ToList();
                return Json(result);
            }

            // Chỉ khi KHÔNG có vtyeucau, mới coi là yêu cầu nhập kho đặc biệt
            // và lấy dữ liệu từ vtphieunhapkho/phieunhapkho.
            bool isNhapKhoRequest =
                (!string.IsNullOrEmpty(MaYeucau) && MaYeucau.StartsWith("NHAPKHO_", StringComparison.OrdinalIgnoreCase)) ||
                _context.phieunhapkho.Any(p => p.MaYeucau == MaYeucau) ||
                _context.yeucau.Any(y => y.MaYeucau == MaYeucau && y.TenYeucau == "Yêu cầu nhập kho");

            if (isNhapKhoRequest)
            {
                var vatTuList = (from vtnk in _context.vtphieunhapkho
                                join pnk in _context.phieunhapkho on vtnk.MaNhapkho equals pnk.MaNhapkho
                                where pnk.MaYeucau == MaYeucau
                                select new
                                {
                                    ID = vtnk.ID,
                                    VTMaYeucau = MaYeucau,
                                    TenSanpham = vtnk.TenSanpham,
                                    MaSanpham = vtnk.MaSanpham,
                                    YCMakho = vtnk.Makho,
                                    HangSX = vtnk.HangSX,
                                    NhaCC = vtnk.NhaCC,
                                    SLCu = (int?)null,
                                    SLMoi = vtnk.SL,
                                    SL = vtnk.SL,
                                    DonVi = vtnk.DonVi,
                                    NgayCanHang = (DateTime?)null,
                                    NgayNhapkho = vtnk.NgayNhapkho,
                                    NgayBaohanh = vtnk.NgayBaohanh,
                                    ThoiGianBH = vtnk.ThoiGianBH,
                                    NgayDuyet = (DateTime?)null,
                                    TrangThai = vtnk.TrangThai,
                                    GhiChu = (string?)null
                                }).ToList();
                var maSanphamList2 = vatTuList.Where(v => !string.IsNullOrWhiteSpace(v.MaSanpham)).Select(v => v.MaSanpham!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var maSanphamSet2 = new HashSet<string>(maSanphamList2, StringComparer.OrdinalIgnoreCase);
                var tonKhoByMaSanpham2 = _context.khotongs.Where(k => k.MaSanpham != null).Select(k => new { k.MaSanpham, k.SL }).ToList()
                    .Where(k => maSanphamSet2.Contains(k.MaSanpham!)).GroupBy(k => k.MaSanpham!, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.SL ?? 0), StringComparer.OrdinalIgnoreCase);
                var result2 = vatTuList.Select(v =>
                {
                    var tonKho = !string.IsNullOrWhiteSpace(v.MaSanpham) && tonKhoByMaSanpham2.TryGetValue(v.MaSanpham, out var tk) ? tk : 0;
                    return new { v.ID, TT = (string?)null, v.VTMaYeucau, v.TenSanpham, v.MaSanpham, v.YCMakho, v.HangSX, v.NhaCC, v.SLCu, v.SLMoi, v.SL, v.DonVi, v.NgayCanHang, v.NgayNhapkho, v.NgayBaohanh, v.ThoiGianBH, v.NgayDuyet, v.TrangThai, v.GhiChu, TonKho = tonKho, SlThieu = Math.Max(0, (v.SLMoi ?? v.SL ?? 0) - tonKho), SlDaXuat = (int?)null };
                }).ToList();
                return Json(result2);
            }

            return Json(new List<object>());
        }

        [HttpGet]
        public IActionResult ExportYeucauVatTuExcel(string MaYeucau)
        {
            if (string.IsNullOrWhiteSpace(MaYeucau)) return NotFound();
            var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
            bool hasVatTuYeuCau = _context.vtyeucau.Any(v => v.VTMaYeucau == MaYeucau);
            List<dynamic> exportRows = new List<dynamic>();
            if (hasVatTuYeuCau)
            {
                var vatTuList = _context.vtyeucau.Where(v => v.VTMaYeucau == MaYeucau).ToList();
                var maSanphamSet = new HashSet<string>(vatTuList.Where(v => !string.IsNullOrWhiteSpace(v.MaSanpham)).Select(v => v.MaSanpham!), StringComparer.OrdinalIgnoreCase);
                var tonKhoByMaSanpham = _context.khotongs.Where(k => k.MaSanpham != null).Select(k => new { k.MaSanpham, k.SL }).ToList()
                    .Where(k => maSanphamSet.Contains(k.MaSanpham!)).GroupBy(k => k.MaSanpham!, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.SL ?? 0), StringComparer.OrdinalIgnoreCase);
                foreach (var v in vatTuList)
                {
                    var slMoi = v.SLMoi ?? v.SL ?? 0;
                    var tonKho = !string.IsNullOrWhiteSpace(v.MaSanpham) && tonKhoByMaSanpham.TryGetValue(v.MaSanpham, out var tk) ? tk : 0;
                    // Thiếu = Yêu cầu - Đã xuất (khi đã xuất đủ thì thiếu = 0)
                    var slDaXuatThucTe = !string.IsNullOrWhiteSpace(v.MaSanpham) ? YeucauUpdateHelper.TinhSoLuongDaCap(_context, MaYeucau, v.MaSanpham) : 0;
                    var slThieu = YeucauUpdateHelper.TinhSoLuongConThieuTheoMaYeuCauCoBan(_context, MaYeucau, v.MaSanpham ?? "");
                    var isDaXuatKho = (v.TrangThai ?? "").IndexOf("Đã xuất kho", StringComparison.OrdinalIgnoreCase) >= 0;
                    var slDaXuat = slDaXuatThucTe > 0 ? (int?)slDaXuatThucTe : (isDaXuatKho ? (v.SL ?? v.SLMoi) : (int?)null);
                    exportRows.Add(new { v.TT, v.TenSanpham, v.MaSanpham, v.HangSX, v.NhaCC, v.SLCu, v.SLMoi, SlThieu = slThieu, SlDaXuat = slDaXuat, TonKho = tonKho, v.DonVi, v.NgayCanHang, v.NgayCoHang, v.TrangThai, v.GhiChu, v.NgayDuyet });
                }
            }
            else
            {
                bool isNhapKhoRequest = (!string.IsNullOrEmpty(MaYeucau) && MaYeucau.StartsWith("NHAPKHO_", StringComparison.OrdinalIgnoreCase)) || _context.phieunhapkho.Any(p => p.MaYeucau == MaYeucau) || _context.yeucau.Any(y => y.MaYeucau == MaYeucau && y.TenYeucau == "Yêu cầu nhập kho");
                if (isNhapKhoRequest)
                {
                    var vatTuList = (from vtnk in _context.vtphieunhapkho join pnk in _context.phieunhapkho on vtnk.MaNhapkho equals pnk.MaNhapkho where pnk.MaYeucau == MaYeucau select new { vtnk.TenSanpham, vtnk.MaSanpham, vtnk.HangSX, vtnk.NhaCC, vtnk.SL, vtnk.DonVi, vtnk.TrangThai }).ToList();
                    var maSanphamSet = new HashSet<string>(vatTuList.Where(v => !string.IsNullOrWhiteSpace(v.MaSanpham)).Select(v => v.MaSanpham!), StringComparer.OrdinalIgnoreCase);
                    var tonKhoByMaSanpham = _context.khotongs.Where(k => k.MaSanpham != null).Select(k => new { k.MaSanpham, k.SL }).ToList()
                        .Where(k => maSanphamSet.Contains(k.MaSanpham!)).GroupBy(k => k.MaSanpham!, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => g.Sum(x => x.SL ?? 0), StringComparer.OrdinalIgnoreCase);
                    int stt = 1;
                    foreach (var v in vatTuList)
                    {
                        var slMoi = v.SL ?? 0;
                        var tonKho = !string.IsNullOrWhiteSpace(v.MaSanpham) && tonKhoByMaSanpham.TryGetValue(v.MaSanpham, out var tk) ? tk : 0;
                        var slThieu = Math.Max(0, slMoi - tonKho);
                        exportRows.Add(new { TT = (object)stt++, v.TenSanpham, v.MaSanpham, v.HangSX, v.NhaCC, SLCu = (int?)null, SLMoi = v.SL, SlThieu = slThieu, SlDaXuat = (int?)null, TonKho = tonKho, v.DonVi, NgayCanHang = (DateTime?)null, NgayCoHang = (DateTime?)null, v.TrangThai, GhiChu = (string?)null, NgayDuyet = (DateTime?)null });
                    }
                }
            }
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Danh sách vật tư");
                worksheet.Cells[1, 1, 1, 16].Merge = true;
                worksheet.Cells[1, 1].Value = $"YÊU CẦU VẬT TƯ {MaYeucau}" + (yeucau != null && !string.IsNullOrEmpty(yeucau.NguoiYeucau) ? $" - {yeucau.NguoiYeucau}" : "");
                worksheet.Cells[1, 1].Style.Font.Bold = true;
                worksheet.Cells[1, 1].Style.Font.Size = 14;
                worksheet.Cells[1, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                worksheet.Cells[1, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                worksheet.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(68, 114, 196));
                worksheet.Cells[1, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                worksheet.Row(1).Height = 25;
                int headerRow1 = 2, headerRow2 = 3;
                worksheet.Cells[headerRow1, 1].Value = "TT";
                worksheet.Cells[headerRow1, 2].Value = "Tên thiết bị / hàng hóa";
                worksheet.Cells[headerRow1, 3].Value = "Mã VT";
                worksheet.Cells[headerRow1, 4].Value = "Hãng SX";
                worksheet.Cells[headerRow1, 5].Value = "NCC";
                worksheet.Cells[headerRow1, 6, headerRow1, 10].Merge = true;
                worksheet.Cells[headerRow1, 6].Value = "SL";
                worksheet.Cells[headerRow1, 11].Value = "ĐV";
                worksheet.Cells[headerRow1, 12].Value = "Ngày cần";
                worksheet.Cells[headerRow1, 13].Value = "Ngày có hàng";
                worksheet.Cells[headerRow1, 14].Value = "Trạng thái";
                worksheet.Cells[headerRow1, 15].Value = "Ghi chú";
                worksheet.Cells[headerRow1, 16].Value = "Ngày duyệt";
                worksheet.Cells[headerRow2, 6].Value = "Cũ";
                worksheet.Cells[headerRow2, 7].Value = "Mới";
                worksheet.Cells[headerRow2, 8].Value = "Thiếu";
                worksheet.Cells[headerRow2, 9].Value = "Đã xuất";
                worksheet.Cells[headerRow2, 10].Value = "Tồn kho";
                worksheet.Cells[headerRow1, 1, headerRow2, 1].Merge = true;
                worksheet.Cells[headerRow1, 2, headerRow2, 2].Merge = true;
                worksheet.Cells[headerRow1, 3, headerRow2, 3].Merge = true;
                worksheet.Cells[headerRow1, 4, headerRow2, 4].Merge = true;
                worksheet.Cells[headerRow1, 5, headerRow2, 5].Merge = true;
                worksheet.Cells[headerRow1, 11, headerRow2, 11].Merge = true;
                worksheet.Cells[headerRow1, 12, headerRow2, 12].Merge = true;
                worksheet.Cells[headerRow1, 13, headerRow2, 13].Merge = true;
                worksheet.Cells[headerRow1, 14, headerRow2, 14].Merge = true;
                worksheet.Cells[headerRow1, 15, headerRow2, 15].Merge = true;
                worksheet.Cells[headerRow1, 16, headerRow2, 16].Merge = true;
                for (int r = headerRow1; r <= headerRow2; r++)
                {
                    using (var range = worksheet.Cells[r, 1, r, 16]) { range.Style.Font.Bold = true; range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid; range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(173, 216, 230)); range.Style.Border.Bottom.Style = range.Style.Border.Top.Style = range.Style.Border.Left.Style = range.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin; range.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center; range.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center; }
                }
                int row = headerRow2 + 1, stt = 1;
                foreach (var r in exportRows)
                {
                    worksheet.Cells[row, 1].Value = r.TT != null ? r.TT : stt++;
                    worksheet.Cells[row, 2].Value = r.TenSanpham ?? "";
                    worksheet.Cells[row, 3].Value = r.MaSanpham ?? "";
                    worksheet.Cells[row, 4].Value = r.HangSX ?? "";
                    worksheet.Cells[row, 5].Value = r.NhaCC ?? "";
                    worksheet.Cells[row, 6].Value = r.SLCu ?? 0;
                    worksheet.Cells[row, 7].Value = r.SLMoi ?? 0;
                    worksheet.Cells[row, 8].Value = r.SlThieu ?? 0;
                    worksheet.Cells[row, 9].Value = r.SlDaXuat != null ? r.SlDaXuat : "-";
                    worksheet.Cells[row, 10].Value = r.TonKho ?? 0;
                    worksheet.Cells[row, 11].Value = r.DonVi ?? "";
                    worksheet.Cells[row, 12].Value = r.NgayCanHang != null ? ((DateTime)r.NgayCanHang).ToString("dd/MM/yyyy") : "";
                    worksheet.Cells[row, 13].Value = r.NgayCoHang != null ? ((DateTime)r.NgayCoHang).ToString("dd/MM/yyyy") : "";
                    worksheet.Cells[row, 14].Value = r.TrangThai ?? "";
                    worksheet.Cells[row, 15].Value = r.GhiChu ?? "";
                    worksheet.Cells[row, 16].Value = r.NgayDuyet != null ? ((DateTime)r.NgayDuyet).ToString("dd/MM/yyyy HH:mm:ss") : "";
                    using (var range = worksheet.Cells[row, 1, row, 16]) { range.Style.Border.Bottom.Style = range.Style.Border.Top.Style = range.Style.Border.Left.Style = range.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin; }
                    row++;
                }
                worksheet.Cells.AutoFitColumns();
                return File(package.GetAsByteArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Yeu_cau_vat_tu_{MaYeucau?.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
        }

        [HttpGet]
        public IActionResult GetVTPhieuxuatkho(string MaXuatkho)
        {
            var PhieuxuatkhoList = _context.vtphieuxuatkho
                                 .Where(v => v.MaXuatkho == MaXuatkho).ToList();
            
            // Lấy thông tin phiếu xuất kho để lấy tên người yêu cầu
            var phieuxuatkho = _context.phieuxuatkho
                .FirstOrDefault(p => p.MaXuatkho == MaXuatkho);
            
            string tenNguoiYeuCau = "";
            if (phieuxuatkho != null && !string.IsNullOrEmpty(phieuxuatkho.MaNguoidung))
            {
                var nguoidung = _context.nguoidungs
                    .FirstOrDefault(n => n.MaNguoidung == phieuxuatkho.MaNguoidung);
                if (nguoidung != null)
                {
                    tenNguoiYeuCau = nguoidung.TenNguoidung ?? "";
                }
            }

            // Lấy tồn kho hiện tại theo mã sản phẩm từ bảng khotongs
            var maSanphamList = PhieuxuatkhoList
                .Where(v => !string.IsNullOrWhiteSpace(v.MaSanpham))
                .Select(v => v.MaSanpham!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var tonKhoByCode = _context.khotongs
                .Where(k => k.MaSanpham != null && maSanphamList.Contains(k.MaSanpham))
                .GroupBy(k => k.MaSanpham!)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(x => x.SL ?? 0),
                    StringComparer.OrdinalIgnoreCase
                );

            var items = PhieuxuatkhoList.Select(v =>
            {
                int tonKho = 0;
                if (!string.IsNullOrWhiteSpace(v.MaSanpham) &&
                    tonKhoByCode.TryGetValue(v.MaSanpham, out var ton))
                {
                    tonKho = ton;
                }

                return new
                {
                    v.ID,
                    v.MaXuatkho,
                    v.MaYeucau,
                    v.TenSanpham,
                    v.MaSanpham,
                    v.Makho,
                    v.HangSX,
                    v.NhaCC,
                    v.SL,
                    v.DonVi,
                    v.DonGia,
                    v.ThanhTien,
                    v.NgayNhapkho,
                    v.NgayBaohanh,
                    v.ThoiGianBH,
                    v.TrangThai,
                    v.LoaiCapPhat,
                    TonKho = tonKho
                };
            }).ToList();
            
            return Json(new
            {
                items = items,
                maXuatkho = MaXuatkho,
                tenNguoiYeuCau = tenNguoiYeuCau
            });
        }

        [HttpGet]
        public IActionResult GetVTPhieunhapkho(string MaNhapkho)
        {
            var PhieunhapkhoList = _context.vtphieunhapkho
                                 .Where(v => v.MaNhapkho == MaNhapkho).ToList();
            
            // Lấy thông tin phiếu nhập kho để lấy tên người yêu cầu
            var phieunhapkho = _context.phieunhapkho
                .FirstOrDefault(p => p.MaNhapkho == MaNhapkho);
            
            string tenNguoiYeuCau = "";
            if (phieunhapkho != null && !string.IsNullOrEmpty(phieunhapkho.MaNguoidung))
            {
                var nguoidung = _context.nguoidungs
                    .FirstOrDefault(n => n.MaNguoidung == phieunhapkho.MaNguoidung);
                if (nguoidung != null)
                {
                    tenNguoiYeuCau = nguoidung.TenNguoidung ?? "";
                }
            }
            
            return Json(new
            {
                items = PhieunhapkhoList,
                maNhapkho = MaNhapkho,
                tenNguoiYeuCau = tenNguoiYeuCau
            });
        }

        [HttpGet]
        public IActionResult GetVTPhieumuahang(string MaMuahang)
        {
            var PhieumuahangList = _context.vtphieumuahang
                                 .Where(v => v.MaMuahang == MaMuahang).ToList();
            
            // Lấy thông tin phiếu mua hàng để lấy tên người yêu cầu
            var phieumuahang = _context.phieumuahang
                .FirstOrDefault(p => p.MaMuahang == MaMuahang);
            
            string tenNguoiYeuCau = "";
            if (phieumuahang != null && !string.IsNullOrEmpty(phieumuahang.MaNguoidung))
            {
                var nguoidung = _context.nguoidungs
                    .FirstOrDefault(n => n.MaNguoidung == phieumuahang.MaNguoidung);
                if (nguoidung != null)
                {
                    tenNguoiYeuCau = nguoidung.TenNguoidung ?? "";
                }
            }
            
            // Trả về dữ liệu với các trường mới
            var itemsWithNewFields = PhieumuahangList.Select(vt => new
            {
                vt.ID,
                vt.MaMuahang,
                vt.MaYeucau,
                vt.TenSanpham,
                vt.MaSanpham,
                vt.Makho,
                vt.HangSX,
                vt.NhaCC,
                vt.SL,
                vt.DonVi,
                vt.DonGia,
                vt.ThanhTien,
                vt.NgayThanhToan,
                vt.NgayThanhToanBPMuahang,
                vt.NgayThanhToanGiamdoc,
                vt.NgayNhapkho,
                vt.NgayCoHang,
                vt.NgayBaohanh,
                vt.ThoiGianBH,
                vt.TrangThai,
                vt.GhiChu,
                vt.GhiChuBPMuahang,
                vt.GhiChuGiamdoc
            }).ToList();
            
            return Json(new
            {
                items = itemsWithNewFields,
                maMuahang = MaMuahang,
                tenNguoiYeuCau = tenNguoiYeuCau
            });
        }

        [HttpPost]
        public IActionResult XuLyVatTuYeucau(string MaYeucau, string MaSanpham, string action)
        {
            try
            {
                var vatTu = _context.vtyeucau
                    .FirstOrDefault(v => v.VTMaYeucau == MaYeucau && v.MaSanpham == MaSanpham);

                if (vatTu == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy vật tư." });
                }

                if (action == "approve")
                {
                    // Khi trưởng phòng duyệt, đặt trạng thái "Chờ giám đốc duyệt" thay vì "Đã duyệt"
                    var chucVu = HttpContext.Session.GetString("Chucvu");
                    if (chucVu == "Trưởng BP")
                    {
                        vatTu.TrangThai = "Chờ giám đốc duyệt";
                    }
                    else
                    {
                        vatTu.TrangThai = "Đã duyệt";
                    }
                }
                else if (action == "reject")
                {
                    vatTu.TrangThai = "Đã từ chối";
                }

                _context.vtyeucau.Update(vatTu);
                _context.SaveChanges();

                return Json(new { success = true, message = action == "approve" ? "Đã duyệt vật tư thành công." : "Đã từ chối vật tư." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        [HttpPost]
        public IActionResult XuLyVatTuYeucauWithCheckbox(string MaYeucau, string VatTuData)
        {
            try
            {
                if (string.IsNullOrEmpty(VatTuData))
                {
                    return Json(new { success = false, message = "Không có dữ liệu vật tư." });
                }

                // Parse JSON data
                var vatTuList = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(VatTuData);
                if (vatTuList == null || !vatTuList.Any())
                {
                    return Json(new { success = false, message = "Dữ liệu vật tư không hợp lệ." });
                }

                // Kiểm tra xem yêu cầu có mã dự án không
                var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
                bool hasMaDuan = yeucau != null && !string.IsNullOrWhiteSpace(yeucau.YCMaDuan);
                
                // Xác định trạng thái tiếp theo dựa trên việc có mã dự án hay không
                string nextTrangThaiVT = hasMaDuan ? "Chờ quản lý dự án duyệt" : "Chờ giám đốc duyệt";
                string nextTrangThaiYC = hasMaDuan ? "Chờ quản lý dự án duyệt" : "Chờ Giám đốc duyệt";

                int processedCount = 0;
                int skippedCount = 0;

                foreach (var item in vatTuList)
                {
                    var maSanpham = item.ContainsKey("MaSanpham") ? item["MaSanpham"]?.ToString() : null;
                    var isApproved = item.ContainsKey("IsApproved") && 
                                    item["IsApproved"] is System.Text.Json.JsonElement jsonElement && 
                                    jsonElement.GetBoolean();
                    var ghiChu = item.ContainsKey("GhiChu") ? item["GhiChu"]?.ToString() : null;

                    if (string.IsNullOrEmpty(maSanpham))
                    {
                        skippedCount++;
                        continue;
                    }

                    var vatTu = _context.vtyeucau
                        .FirstOrDefault(v => v.VTMaYeucau == MaYeucau && v.MaSanpham == maSanpham);

                    if (vatTu == null)
                    {
                        skippedCount++;
                        continue;
                    }

                    // Helper function để kiểm tra xem vật tư có đang chờ Trưởng BP mua hàng duyệt không
                    Func<string, bool> isAwaitingTruongBPStatus = status =>
                    {
                        if (string.IsNullOrWhiteSpace(status))
                        {
                            return true;
                        }
                        var normalized = status.Trim();
                        return normalized.Equals("Chờ Trưởng BP-BP mua hàng duyệt", StringComparison.OrdinalIgnoreCase)
                            || normalized.StartsWith("Chờ Trưởng BP", StringComparison.OrdinalIgnoreCase)
                            || normalized.Contains("chờ trưởng bp", StringComparison.OrdinalIgnoreCase);
                    };

                    // Helper function để kiểm tra xem vật tư đã được duyệt chưa
                    Func<string, bool> isAlreadyApproved = status =>
                    {
                        if (string.IsNullOrWhiteSpace(status))
                        {
                            return false;
                        }
                        var normalized = status.Trim();
                        return normalized == "Đã duyệt" ||
                               normalized == "Đang mua hàng" ||
                               normalized == "Đã xuất kho" ||
                               normalized == "Đã nhận hàng" ||
                               normalized == "Chờ giám đốc duyệt" ||
                               normalized == "Chờ quản lý dự án duyệt";
                    };

                    // Helper function để kiểm tra xem vật tư đã bị từ chối chưa
                    Func<string, bool> isAlreadyRejected = status =>
                    {
                        if (string.IsNullOrWhiteSpace(status))
                        {
                            return false;
                        }
                        return status.Contains("Đã từ chối", StringComparison.OrdinalIgnoreCase);
                    };

                    // Kiểm tra số lượng yêu cầu - nếu bằng 0 thì đặt trạng thái "Hoàn thành" và bỏ qua
                    int soLuongYeuCau = vatTu.SL ?? 0;
                    if (soLuongYeuCau == 0)
                    {
                        // Nếu số lượng = 0, không cần mua hàng, đặt trạng thái "Hoàn thành"
                        vatTu.NgayDuyet = DateTime.Now;
                        vatTu.TrangThai = "Hoàn thành";
                        vatTu.GhiChu = null;
                        _context.vtyeucau.Update(vatTu);
                        processedCount++;
                        continue;
                    }

                    // Chỉ xử lý các vật tư đang chờ Trưởng BP mua hàng duyệt và chưa được duyệt/từ chối
                    if (!isAwaitingTruongBPStatus(vatTu.TrangThai) || 
                        isAlreadyApproved(vatTu.TrangThai) || 
                        isAlreadyRejected(vatTu.TrangThai))
                    {
                        skippedCount++;
                        continue;
                    }

                    if (isApproved)
                    {
                        // Duyệt vật tư
                        vatTu.NgayDuyet = DateTime.Now;
                        vatTu.TrangThai = nextTrangThaiVT;
                        vatTu.GhiChu = null; // Xóa ghi chú khi duyệt
                    }
                    else
                    {
                        // Từ chối vật tư
                        vatTu.NgayDuyet = DateTime.Now;
                        vatTu.TrangThai = "Đã từ chối";
                        vatTu.GhiChu = ghiChu; // Lưu ghi chú khi từ chối
                    }

                    _context.vtyeucau.Update(vatTu);
                    processedCount++;
                }

                // Lưu thông tin người duyệt vào bảng yeucau (ghi đè mã người duyệt mới nhất)
                if (yeucau != null && processedCount > 0)
                {
                    var maNguoiDuyet = HttpContext.Session.GetString("MaNguoidung");
                    if (!string.IsNullOrWhiteSpace(maNguoiDuyet))
                    {
                        yeucau.NguoiDuyet = maNguoiDuyet;
                        yeucau.NgayDuyet = DateTime.Now;
                        _context.yeucau.Update(yeucau);
                    }
                }

                _context.SaveChanges();

                // Cập nhật trạng thái yêu cầu nếu tất cả vật tư đã được duyệt
                if (yeucau != null)
                {
                    var chucVu = HttpContext.Session.GetString("Chucvu");
                    var boPhan = HttpContext.Session.GetString("Bophan");
                    
                    // Kiểm tra xem tất cả vật tư đã được trưởng phòng duyệt chưa
                    var allVatTu = _context.vtyeucau.Where(v => v.VTMaYeucau == MaYeucau).ToList();
                    var allApprovedByTruongBP = allVatTu.All(v => v.TrangThai == nextTrangThaiVT || 
                                                                   v.TrangThai == "Đã duyệt" || 
                                                                   v.TrangThai == "Đang mua hàng" || 
                                                                   v.TrangThai == "Đã xuất kho" || 
                                                                   v.TrangThai == "Đã nhận hàng" ||
                                                                   v.TrangThai == "Chờ giám đốc duyệt" ||
                                                                   v.TrangThai == "Chờ quản lý dự án duyệt" ||
                                                                   (!string.IsNullOrEmpty(v.TrangThai) && v.TrangThai.Contains("Đã từ chối")));
                    
                    if (allApprovedByTruongBP && chucVu == "Trưởng BP" && boPhan == "BP mua hàng")
                    {
                        yeucau.TrangThai = nextTrangThaiYC;
                        _context.yeucau.Update(yeucau);
                        _context.SaveChanges();

                        // Sau khi Trưởng BP mua hàng duyệt xong bằng checkbox:
                        // - Gửi mail cho người yêu cầu
                        // - Gửi mail cho bước tiếp theo (QLDA nếu có dự án, hoặc Giám đốc nếu không có dự án)
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(yeucau.NguoiYeucau))
                            {
                                var trangThaiThongBao = hasMaDuan
                                    ? "Đã được Trưởng BP-BP mua hàng duyệt - chuyển quản lý dự án"
                                    : "Đã được Trưởng BP-BP mua hàng duyệt - chờ Giám đốc duyệt";

                                _ = _emailService.SendNotificationToEmployeeAsync(
                                    yeucau.MaYeucau,
                                    yeucau.NguoiYeucau,
                                    trangThaiThongBao
                                );
                            }

                            if (hasMaDuan && !string.IsNullOrWhiteSpace(yeucau.YCMaDuan))
                            {
                                _ = _emailService.SendNotificationToProjectManagerAsync(
                                    yeucau.MaYeucau,
                                    yeucau.YCMaDuan
                                );
                            }
                            else
                            {
                                _ = _emailService.SendNotificationToDirectorAsync(yeucau.MaYeucau);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[TruongBPMuahang/XuLyVatTuYeucauWithCheckbox] Lỗi gửi email sau duyệt: {ex.Message}");
                        }
                    }
                }

                string message = $"Đã xử lý {processedCount} vật tư thành công.";
                if (skippedCount > 0)
                {
                    message += $" ({skippedCount} vật tư đã được xử lý trước đó hoặc không ở trạng thái chờ duyệt)";
                }

                return Json(new { success = true, message = message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        [HttpPost]
        public IActionResult XuLyTatCaVatTuYeucau(string MaYeucau, string action)
        {
            try
            {
                var vatTuList = _context.vtyeucau
                    .Where(v => v.VTMaYeucau == MaYeucau).ToList();

                if (!vatTuList.Any())
                {
                    return Json(new { success = false, message = "Không tìm thấy vật tư nào." });
                }

                var chucVu = HttpContext.Session.GetString("Chucvu");
                var boPhan = HttpContext.Session.GetString("Bophan");
                
                foreach (var vatTu in vatTuList)
                {
                    if (action == "approve")
                    {
                        // Khi trưởng phòng duyệt, đặt trạng thái "Chờ giám đốc duyệt" thay vì "Đã duyệt"
                        if (chucVu == "Trưởng BP")
                        {
                            vatTu.TrangThai = "Chờ giám đốc duyệt";
                        }
                        else
                        {
                            vatTu.TrangThai = "Đã duyệt";
                        }
                    }
                    else if (action == "reject")
                    {
                        vatTu.TrangThai = "Đã từ chối";
                    }
                    _context.vtyeucau.Update(vatTu);
                }

                // Lưu thay đổi trạng thái vật tư
                _context.SaveChanges();

                // Cập nhật trạng thái yêu cầu chính nếu Trưởng BP duyệt tất cả vật tư
                if (action == "approve")
                {
                    var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
                    if (yeucau != null)
                    {
                        // Kiểm tra xem tất cả vật tư đã được trưởng phòng duyệt chưa (trạng thái "Chờ giám đốc duyệt")
                        var allApprovedByTruongBP = vatTuList.All(v => v.TrangThai == "Chờ giám đốc duyệt");
                        
                        if (allApprovedByTruongBP && chucVu == "Trưởng BP" && boPhan == "BP mua hàng")
                        {
                            // Kiểm tra trạng thái hiện tại của yêu cầu
                            if (yeucau.TrangThai == "Chờ Trưởng BP-BP mua hàng duyệt")
                            {
                                // Kiểm tra xem có phải là yêu cầu nhập kho không
                                bool isNhapKho = !string.IsNullOrEmpty(yeucau.MaYeucau) && 
                                                (yeucau.MaYeucau.StartsWith("NHAPKHO_DUAN_") || 
                                                 yeucau.MaYeucau.StartsWith("NHAPKHO_CANHAN_"));
                                
                                if (isNhapKho)
                                {
                                    // Nếu là nhập kho
                                    if (yeucau.MaYeucau.StartsWith("NHAPKHO_DUAN_"))
                                    {
                                        // Dự án: Chờ quản lý dự án duyệt
                                        yeucau.TrangThai = "Chờ quản lý dự án duyệt";
                                    }
                                    else if (yeucau.MaYeucau.StartsWith("NHAPKHO_CANHAN_"))
                                    {
                                        // Cá nhân: Chờ Giám đốc duyệt
                                        yeucau.TrangThai = "Chờ Giám đốc duyệt";
                                    }
                                }
                                else
                                {
                                    // Nếu là yêu cầu vật tư thông thường
                                    var duan = _context.duans.FirstOrDefault(d => d.MaDuan == yeucau.YCMaDuan);
                                    if (duan != null)
                                    {
                                        // Có dự án: Chờ quản lý dự án duyệt
                                        yeucau.TrangThai = "Chờ quản lý dự án duyệt";
                                    }
                                    else
                                    {
                                        // Cá nhân: Chờ Giám đốc duyệt
                                        yeucau.TrangThai = "Chờ Giám đốc duyệt";
                                    }
                                }
                            }
                            else
                            {
                                // Giữ logic cũ cho các trường hợp khác
                                yeucau.TrangThai = "Chờ Giám đốc duyệt";
                            }
                            _context.yeucau.Update(yeucau);
                            
                            // Đồng bộ trạng thái tất cả vật tư - đảm bảo tất cả vật tư đều có trạng thái "Chờ giám đốc duyệt"
                            var allVatTu = _context.vtyeucau.Where(v => v.VTMaYeucau == MaYeucau).ToList();
                            foreach (var vt in allVatTu)
                            {
                                // Chỉ cập nhật các vật tư chưa được duyệt hoàn toàn
                                if (vt.TrangThai != "Đã duyệt" && vt.TrangThai != "Đang mua hàng" && 
                                    vt.TrangThai != "Đã từ chối" && vt.TrangThai != "Đã xuất kho" && 
                                    vt.TrangThai != "Đã nhận hàng")
                                {
                                    vt.TrangThai = "Chờ giám đốc duyệt";
                                    _context.vtyeucau.Update(vt);
                                }
                            }
                            
                            _context.SaveChanges();

                            // Gửi thông báo khi trưởng phòng duyệt
                            if (action == "approve" && allApprovedByTruongBP && chucVu == "Trưởng BP")
                            {
                                var yeucauForNotif = _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
                                if (yeucauForNotif != null)
                                {
                                    // Thông báo cho nhân viên
                                    _ = Task.Run(async () =>
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[TruongBP MuaHang] Gửi email cho nhân viên. MaYeucau = {MaYeucau}");
                                        await _emailService.SendNotificationToEmployeeAsync(
                                            MaYeucau,
                                            yeucauForNotif.NguoiYeucau ?? "",
                                            yeucauForNotif.TrangThai ?? ""
                                        );
                                        System.Diagnostics.Debug.WriteLine($"[TruongBP MuaHang] Đã gọi xong email cho nhân viên. MaYeucau = {MaYeucau}");
                                    });

                                    // Thông báo cho QLDA nếu có dự án, hoặc Giám đốc nếu không có
                                    _ = Task.Run(async () =>
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[TruongBP MuaHang] Gửi email cho QLDA / Giám đốc. MaYeucau = {MaYeucau}");
                                        if (!string.IsNullOrEmpty(yeucauForNotif.YCMaDuan))
                                        {
                                            await _emailService.SendNotificationToProjectManagerAsync(
                                                MaYeucau,
                                                yeucauForNotif.YCMaDuan
                                            );
                                        }
                                        else
                                        {
                                            await _emailService.SendNotificationToDirectorAsync(MaYeucau);
                                        }
                                        System.Diagnostics.Debug.WriteLine($"[TruongBP MuaHang] Đã gọi xong email cho QLDA / Giám đốc. MaYeucau = {MaYeucau}");
                                    });
                                }
                            }
                        }
                    }
                }
                else if (action == "reject")
                {
                    var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
                    if (yeucau != null)
                    {
                        // Nếu có bất kỳ vật tư nào bị từ chối, yêu cầu chính cũng bị từ chối
                        yeucau.TrangThai = "Đã từ chối";
                        _context.yeucau.Update(yeucau);
                        _context.SaveChanges();

                        // Gửi email thông báo từ chối
                        SendRejectionEmailAsync(MaYeucau, "");
                    }
                }

                return Json(new { success = true, message = action == "approve" ? $"Đã duyệt {vatTuList.Count} vật tư thành công." : $"Đã từ chối {vatTuList.Count} vật tư." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        public IActionResult ThemYeucau()
        {
            var Duanlist = _context.duans
                          .Select(n => new { n.MaDuan, n.TrangThai })
                          .ToList();

            ViewBag.Duanlist = Duanlist;
            return View();
        }

        public IActionResult ThemPhieunhapkho()
        {
            var maNv = HttpContext.Session.GetString("MaNguoidung");

            var Yeucaulist = _context.yeucau
                          .Select(n => new { n.MaYeucau, n.TrangThai })
                          .ToList();

            ViewBag.Yeucaulist = Yeucaulist;

            var Phieumuahanglist = _context.phieumuahang
                                 .Select(n => new { n.MaYeucau, n.TrangThai })
                                 .ToList();
            ViewBag.Phieumuahanglist = Phieumuahanglist;

            var allowedProjectCodes = _context.phieuxuatkho
                .Where(px => px.MaNguoidung == maNv && !string.IsNullOrEmpty(px.MaDuan))
                .Select(px => px.MaDuan)
                .Distinct()
                .ToList();

            var Duanlist = allowedProjectCodes.Count > 0
                ? _context.duans
                    .Where(y => allowedProjectCodes.Contains(y.MaDuan))
                    .Select(y => (object)new { y.MaDuan, y.TrangThai })
                    .ToList()
                : new List<object>();

            ViewBag.Duanlist = Duanlist;
            ViewBag.MaNguoidung = maNv;
            return View();
        }

        [HttpGet]
        public IActionResult GetCurrentUser()
        {
            var maNv = HttpContext.Session.GetString("MaNguoidung");
            return Json(new { maNguoidung = maNv });
        }

        [HttpGet]
        public IActionResult GetDataKhoCaNhan()
        {
            var maNv = HttpContext.Session.GetString("MaNguoidung");
            if (string.IsNullOrEmpty(maNv))
            {
                return BadRequest("Không tìm thấy mã nhân viên");
            }

            var khoCaNhanItems = _context.khonguoidungs
                .Where(k => k.NDMaNguoidung == maNv
                         && (k.TrangThai == "Đang mượn" || k.TrangThai == "Đang sử dụng")
                         && k.SL > 0)
                .Select(k => new
                {
                    tenSanpham = k.TenSanpham,
                    maSanpham = k.MaSanpham,
                    makho = k.NDMakho,
                    hangSX = k.HangSX,
                    nhaCC = k.NhaCC,
                    sl = k.SL,
                    donVi = k.DonVi
                })
                .ToList();

            return Json(new
            {
                maNguoidung = maNv,
                vtKhoCaNhan = khoCaNhanItems
            });
        }

        [HttpGet]
        public IActionResult GetDataByMaDuan(string maduan)
        {
            if (string.IsNullOrWhiteSpace(maduan))
            {
                return Json(new
                {
                    maNguoidung = HttpContext.Session.GetString("MaNguoidung"),
                    maDuan = "",
                    vtPhieuMuaHang = new List<object>()
                });
            }

            var maNv = HttpContext.Session.GetString("MaNguoidung");

            var allowedProjectCodes = _context.phieuxuatkho
                .Where(px => px.MaNguoidung == maNv && !string.IsNullOrEmpty(px.MaDuan))
                .Select(px => px.MaDuan)
                .Distinct()
                .ToList();

            if (!allowedProjectCodes.Contains(maduan))
            {
                return Json(new
                {
                    maNguoidung = maNv,
                    maDuan = maduan,
                    vtPhieuMuaHang = new List<object>(),
                    error = "Bạn không có quyền trả kho cho dự án này."
                });
            }

            try
            {
                var khoDuanItems = (from vt in _context.vtphieuxuatkho
                                   join px in _context.phieuxuatkho on vt.MaXuatkho equals px.MaXuatkho
                                   join yc in _context.yeucau on vt.MaYeucau equals yc.MaYeucau
                                   where px.MaDuan == maduan
                                      && yc.YCMaNguoidung == maNv
                                      && (vt.TrangThai == "Đã xác nhận nhận hàng"
                                          || vt.TrangThai == "Đã lấy hàng"
                                          || vt.TrangThai == "Đã xuất kho")
                                      && (vt.SL ?? 0) > 0
                                   select new
                                   {
                                       tenSanpham = vt.TenSanpham,
                                       maSanpham = vt.MaSanpham,
                                       makho = vt.Makho,
                                       hangSX = vt.HangSX,
                                       nhaCC = vt.NhaCC,
                                       sl = vt.SL ?? 0,
                                       donVi = vt.DonVi,
                                       maXuatkho = vt.MaXuatkho,
                                       maYeucau = vt.MaYeucau,
                                       trangThai = vt.TrangThai
                                   })
                                   .Distinct()
                                   .ToList();

                var phieuxuatCount = _context.phieuxuatkho.Count(p => p.MaDuan == maduan);

                return Json(new
                {
                    maNguoidung = maNv,
                    maDuan = maduan,
                    vtPhieuMuaHang = khoDuanItems,
                    debug = new
                    {
                        phieuxuatCount,
                        returnedCount = khoDuanItems.Count
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetDataByMaDuan: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return Json(new
                {
                    maNguoidung = maNv,
                    maDuan = maduan,
                    vtPhieuMuaHang = new List<object>(),
                    error = ex.Message
                });
            }
        }

        [HttpGet]
        public IActionResult TimKiem(string timkiem)
        {
            if (string.IsNullOrEmpty(timkiem))
            {
                return Json(new List<object>());
            }

            var searchTerm = timkiem.Trim().ToLower();
            var products = _context.khotongs
                .Where(k => (k.TenSanpham != null && k.TenSanpham.ToLower().Contains(searchTerm)) || 
                           (k.MaSanpham != null && k.MaSanpham.ToLower().Contains(searchTerm)))
                .Take(10) // Giới hạn 10 kết quả để hiệu suất tốt hơn
                .ToList();

            var results = new List<object>();
            
            foreach (var product in products)
            {
                // Lấy tất cả nhà cung cấp cho sản phẩm này từ bảng SanPhamNhaCC
                var suppliers = _context.SanPhamNhaCC
                    .Where(s => s.MaSanpham == product.MaSanpham)
                    .Select(s => s.NhaCC)
                    .Distinct()
                    .ToList();

                // Nếu có nhà cung cấp trong bảng SanPhamNhaCC, sử dụng danh sách đó
                if (suppliers.Any())
                {
                    // Tạo một kết quả cho mỗi nhà cung cấp
                    foreach (var supplier in suppliers)
                    {
                        results.Add(new
                        {
                            tenSanpham = product.TenSanpham,
                            maSanpham = product.MaSanpham,
                            makho = product.Makho,
                            hangSX = product.HangSX,
                            nhaCC = supplier,
                            sl = product.SL,
                            donVi = product.DonVi
                        });
                    }
                }
                else
                {
                    // Nếu không có trong bảng SanPhamNhaCC, sử dụng nhà cung cấp từ khotongs (tương thích ngược)
                    results.Add(new
                    {
                        tenSanpham = product.TenSanpham,
                        maSanpham = product.MaSanpham,
                        makho = product.Makho,
                        hangSX = product.HangSX,
                        nhaCC = product.NhaCC,
                        sl = product.SL,
                        donVi = product.DonVi
                    });
                }
            }
            
            return Json(results);
        }

        [HttpPost]
        public IActionResult ThemyeucauSQL(yeucau yeucau, vtyeucau vtyeucau,
                                           duans duans, phieunhapkho phieunhapkho, vtphieunhapkho vtphieunhapkho, List<string> YCMaKho,
                                           List<string> TenSanpham, List<string> MaSanpham,
                                           List<string> HangSX, List<string> NhaCC, List<int?> SL,
                                           List<int?> SLCu, List<int?> SLMoi,
                                           List<string> DonVi, List<string> GhiChu, string MaYeucau, string action, phieuxuatkho phieuxuatkho, vtphieuxuatkho vtphieuxuatkho, phieumuahang phieumuahang, vtphieumuahang vtphieumuahang)
        {
            // Kiểm tra null để tránh lỗi khi upload file Excel lớn
            if (yeucau == null)
            {
                yeucau = new yeucau();
            }

            string? GetTTAt(int index)
            {
                if (Request.Form.TryGetValue("TT", out var ttValues))
                {
                    if (index >= 0 && index < ttValues.Count)
                    {
                        var raw = ttValues[index];
                        return string.IsNullOrWhiteSpace(raw) ? null : raw.Trim();
                    }
                }
                return null;
            }

            if (yeucau.TenYeucau != "Yêu cầu nhập kho")
            {
                yeucau.NgayYeucau = DateTime.Now;

                var chucVu2 = HttpContext.Session.GetString("Chucvu");
                var boPhan2 = HttpContext.Session.GetString("Bophan");
                var maNv2 = HttpContext.Session.GetString("MaNguoidung");

                yeucau.YCMaDuan = yeucau.YCMaDuan?.Trim();
                var duan = string.IsNullOrEmpty(yeucau.YCMaDuan)
                    ? null
                    : _context.duans.FirstOrDefault(d => d.MaDuan == yeucau.YCMaDuan);

                // Áp dụng quy tắc duyệt mới
                if (chucVu2 == "Giám đốc")
                {
                    // Quy tắc 4: Giám đốc → Đã duyệt
                    yeucau.TrangThai = "Đã duyệt";
                }
                else if (duan != null)
                {
                    // Có dự án
                    string maNguoiQLDA = duan.MaNguoiQLDA;
                    if (maNv2 == maNguoiQLDA)
                    {
                        // Quy tắc 3: Quản lý dự án (người trùng mã QLDA)
                        if (chucVu2 == "Trưởng BP")
                        {
                            yeucau.TrangThai = "Chờ Giám đốc duyệt";
                        }
                        else if (chucVu2 == "Giám đốc")
                        {
                            yeucau.TrangThai = "Đã duyệt";
                        }
                        else if (chucVu2 == "Nhân viên")
                        {
                            // Quy tắc 1: Nhân viên → Chờ Trưởng BP-BP {bộ phận} duyệt
                            if (boPhan2 == "BP kỹ thuật")
                            {
                                yeucau.TrangThai = "Chờ Trưởng BP-BP kỹ thuật duyệt";
                            }
                            else if (boPhan2 == "BP kho")
                            {
                                yeucau.TrangThai = "Chờ Trưởng BP-BP kho duyệt";
                            }
                            else if (boPhan2 == "BP mua hàng")
                            {
                                yeucau.TrangThai = "Chờ Trưởng BP-BP mua hàng duyệt";
                            }
                            else if (boPhan2 == "BP kế toán")
                            {
                                yeucau.TrangThai = "Chờ Trưởng BP-BP kế toán duyệt";
                            }
                        }
                    }
                    else
                    {
                        // Không phải quản lý dự án nhưng có dự án
                        if (chucVu2 == "Nhân viên")
                        {
                            // Quy tắc 1: Nhân viên → Chờ Trưởng BP-BP {bộ phận} duyệt
                            if (boPhan2 == "BP kỹ thuật")
                            {
                                yeucau.TrangThai = "Chờ Trưởng BP-BP kỹ thuật duyệt";
                            }
                            else if (boPhan2 == "BP kho")
                            {
                                yeucau.TrangThai = "Chờ Trưởng BP-BP kho duyệt";
                            }
                            else if (boPhan2 == "BP mua hàng")
                            {
                                yeucau.TrangThai = "Chờ Trưởng BP-BP mua hàng duyệt";
                            }
                            else if (boPhan2 == "BP kế toán")
                            {
                                yeucau.TrangThai = "Chờ Trưởng BP-BP kế toán duyệt";
                            }
                        }
                        else if (chucVu2 == "Trưởng BP")
                        {
                            // Quy tắc 2: Trưởng BP thuộc dự án → Chờ quản lý dự án duyệt
                            yeucau.TrangThai = "Chờ quản lý dự án duyệt";
                        }
                    }
                }
                else
                {
                    // Không có dự án
                    if (chucVu2 == "Nhân viên")
                    {
                        // Quy tắc 1: Nhân viên → Chờ Trưởng BP-BP {bộ phận} duyệt
                        if (boPhan2 == "BP kỹ thuật")
                        {
                            yeucau.TrangThai = "Chờ Trưởng BP-BP kỹ thuật duyệt";
                        }
                        else if (boPhan2 == "BP kho")
                        {
                            yeucau.TrangThai = "Chờ Trưởng BP-BP kho duyệt";
                        }
                        else if (boPhan2 == "BP mua hàng")
                        {
                            yeucau.TrangThai = "Chờ Trưởng BP-BP mua hàng duyệt";
                        }
                        else if (boPhan2 == "BP kế toán")
                        {
                            yeucau.TrangThai = "Chờ Trưởng BP-BP kế toán duyệt";
                        }
                    }
                    else if (chucVu2 == "Trưởng BP")
                    {
                        // Quy tắc 2: Trưởng BP không thuộc dự án → Chờ Giám đốc duyệt
                        yeucau.TrangThai = "Chờ Giám đốc duyệt";
                    }
                }
                
                // Đảm bảo trạng thái luôn được set đúng
                if (string.IsNullOrEmpty(yeucau.TrangThai))
                {
                    if (chucVu2 == "Giám đốc")
                    {
                        yeucau.TrangThai = "Đã duyệt";
                    }
                    else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP mua hàng")
                    {
                        if (duan != null)
                        {
                            yeucau.TrangThai = "Chờ quản lý dự án duyệt";
                        }
                        else
                        {
                            yeucau.TrangThai = "Chờ Giám đốc duyệt";
                        }
                    }
                }

                // ================== TẠO MÃ YÊU CẦU (DÙNG CHUNG 1 HÀM) ==================
                yeucau.MaYeucau = _yeucauCodeService.GenerateMaYeucauCommon(
                    yeucau.YCMaDuan,
                    MaSanpham,
                    Request.Form.Files,
                    DateTime.Now);
                // ======================================================================

                // Luôn tạo yêu cầu mới
                _context.yeucau.Add(yeucau);
                _context.SaveChanges();

                for (int i = 0; i < YCMaKho.Count; i++)
                {
                    if (string.IsNullOrEmpty(TenSanpham[i]))
                    {
                        continue;
                    }

                    var khoMatch = _context.khotongs.FirstOrDefault(p => p.Makho == YCMaKho[i]);
                    if (khoMatch != null)
                    {
                        var slCuValue = (SLCu != null && i < SLCu.Count) ? SLCu[i] : null;
                        var slMoiValue = (SLMoi != null && i < SLMoi.Count) ? SLMoi[i] : null;

                        // Bỏ qua dòng nếu số lượng mới không nhập (null) hoặc <= 0 (không cần lưu và hiển thị)
                        if (!slMoiValue.HasValue || slMoiValue.Value <= 0)
                        {
                            continue;
                        }

                        var ghiChuValue = (GhiChu != null && i < GhiChu.Count) ? GhiChu[i] : null;

                        // Tính số lượng mới (ưu tiên SLMoi, sau đó SLCu, cuối cùng là SL)
                        int slMoi = slMoiValue ?? slCuValue ?? ((SL != null && i < SL.Count) ? (SL[i] ?? 0) : 0);

                        // Kiểm tra xem vật tư này đã tồn tại trong yêu cầu chưa
                        var existingVTYeucau = _context.vtyeucau
                            .FirstOrDefault(vt => vt.VTMaYeucau == yeucau.MaYeucau
                                && string.Equals(vt.MaSanpham, MaSanpham[i], StringComparison.OrdinalIgnoreCase));

                        if (existingVTYeucau != null)
                        {
                            // Cập nhật vật tư yêu cầu hiện có
                            existingVTYeucau.TenSanpham = TenSanpham[i];
                            existingVTYeucau.TT = GetTTAt(i);
                            existingVTYeucau.HangSX = HangSX[i];
                            existingVTYeucau.NhaCC = NhaCC[i];
                            existingVTYeucau.SLCu = slCuValue;
                            existingVTYeucau.SLMoi = slMoiValue;
                            existingVTYeucau.SL = slMoi;
                            existingVTYeucau.DonVi = DonVi[i];
                            existingVTYeucau.GhiChu = ghiChuValue;
                            existingVTYeucau.YCMakho = khoMatch.Makho;
                            existingVTYeucau.NgayNhapkho = khoMatch.NgayNhapkho;
                            existingVTYeucau.NgayBaohanh = khoMatch.NgayBaohanh;
                            existingVTYeucau.ThoiGianBH = khoMatch.ThoiGianBH;
                            
                            // Xử lý cập nhật theo logic mới
                            int slThieu;
                            var updateResult = YeucauUpdateHelper.XuLyCapNhatYeuCau(
                                _context, 
                                yeucau, 
                                MaSanpham[i], 
                                slMoi, 
                                khoMatch.Makho, 
                                out slThieu);
                            
                            if (updateResult.Success)
                            {
                                // Set trạng thái cho vật tư: nếu có dự án thì "Chờ quản lý dự án duyệt", nếu không có dự án thì "Chờ Giám đốc duyệt"
                                if (duan != null)
                                {
                                    existingVTYeucau.TrangThai = "Chờ quản lý dự án duyệt";
                                }
                                else if (string.IsNullOrEmpty(existingVTYeucau.TrangThai))
                                {
                                    existingVTYeucau.TrangThai = "Chờ Giám đốc duyệt";
                                }
                                _context.vtyeucau.Update(existingVTYeucau);
                            }
                        }
                        else
                        {
                            // Tạo mới vật tư yêu cầu
                            var newVtyeucau = new vtyeucau();
                            newVtyeucau.VTMaYeucau = yeucau.MaYeucau;
                            newVtyeucau.TT = GetTTAt(i);
                            newVtyeucau.TenSanpham = TenSanpham[i];
                            newVtyeucau.MaSanpham = MaSanpham[i];
                            newVtyeucau.HangSX = HangSX[i];
                            newVtyeucau.NhaCC = NhaCC[i];
                            newVtyeucau.SLCu = slCuValue;
                            newVtyeucau.SLMoi = slMoiValue;
                            newVtyeucau.SL = slMoi;
                            newVtyeucau.DonVi = DonVi[i];
                            newVtyeucau.GhiChu = ghiChuValue;
                            newVtyeucau.YCMakho = khoMatch.Makho;
                            newVtyeucau.NgayNhapkho = khoMatch.NgayNhapkho;
                            newVtyeucau.NgayBaohanh = khoMatch.NgayBaohanh;
                            newVtyeucau.ThoiGianBH = khoMatch.ThoiGianBH;
                            // Set trạng thái cho vật tư: nếu có dự án thì "Chờ quản lý dự án duyệt", nếu không có dự án thì "Chờ Giám đốc duyệt"
                            if (duan != null)
                            {
                                newVtyeucau.TrangThai = "Chờ quản lý dự án duyệt";
                            }
                            else
                            {
                                newVtyeucau.TrangThai = "Chờ Giám đốc duyệt";
                            }
                            _context.vtyeucau.Add(newVtyeucau);
                        }
                    }
                    else
                    {
                        var slCuValue = (SLCu != null && i < SLCu.Count) ? SLCu[i] : null;
                        var slMoiValue = (SLMoi != null && i < SLMoi.Count) ? SLMoi[i] : null;
                        
                        // Bỏ qua dòng nếu số lượng mới không nhập (null) hoặc <= 0 (không cần lưu và hiển thị)
                        if (!slMoiValue.HasValue || slMoiValue.Value <= 0)
                        {
                            continue;
                        }
                        
                        var ghiChuValue = (GhiChu != null && i < GhiChu.Count) ? GhiChu[i] : null;
                        
                        // Tính số lượng mới (ưu tiên SLMoi, sau đó SLCu, cuối cùng là SL)
                        int slMoi = slMoiValue ?? slCuValue ?? ((SL != null && i < SL.Count) ? (SL[i] ?? 0) : 0);
                        
                        // Kiểm tra xem vật tư này đã tồn tại trong yêu cầu chưa
                        var existingVTYeucau = _context.vtyeucau
                            .FirstOrDefault(vt => vt.VTMaYeucau == yeucau.MaYeucau 
                                && string.Equals(vt.MaSanpham, MaSanpham[i], StringComparison.OrdinalIgnoreCase));
                        
                        if (existingVTYeucau != null)
                        {
                            // Cập nhật vật tư yêu cầu hiện có
                            existingVTYeucau.TenSanpham = TenSanpham[i];
                            existingVTYeucau.TT = GetTTAt(i);
                            existingVTYeucau.HangSX = HangSX[i];
                            existingVTYeucau.NhaCC = NhaCC[i];
                            existingVTYeucau.SLCu = slCuValue;
                            existingVTYeucau.SLMoi = slMoiValue;
                            existingVTYeucau.SL = slMoi;
                            existingVTYeucau.DonVi = DonVi[i];
                            existingVTYeucau.GhiChu = ghiChuValue;
                            
                            // Xử lý cập nhật theo logic mới
                            int slThieu;
                            var updateResult = YeucauUpdateHelper.XuLyCapNhatYeuCau(
                                _context, 
                                yeucau, 
                                MaSanpham[i], 
                                slMoi, 
                                "VT mới", 
                                out slThieu);
                            
                            if (updateResult.Success)
                            {
                                // Set trạng thái cho vật tư: nếu có dự án thì "Chờ quản lý dự án duyệt", nếu không có dự án thì "Chờ Giám đốc duyệt"
                                if (duan != null)
                                {
                                    existingVTYeucau.TrangThai = "Chờ quản lý dự án duyệt";
                                }
                                else if (string.IsNullOrEmpty(existingVTYeucau.TrangThai))
                                {
                                    existingVTYeucau.TrangThai = "Chờ Giám đốc duyệt";
                                }
                                _context.vtyeucau.Update(existingVTYeucau);
                            }
                        }
                        else
                        {
                            // Tạo mới vật tư yêu cầu
                            var newVtyeucau = new vtyeucau();
                            newVtyeucau.VTMaYeucau = yeucau.MaYeucau;
                            newVtyeucau.TT = GetTTAt(i);
                            newVtyeucau.TenSanpham = TenSanpham[i];
                            newVtyeucau.MaSanpham = MaSanpham[i];
                            newVtyeucau.HangSX = HangSX[i];
                            newVtyeucau.NhaCC = NhaCC[i];
                            newVtyeucau.SLCu = slCuValue;
                            newVtyeucau.SLMoi = slMoiValue;
                            // Cột SL lấy giá trị từ SLMoi (nếu có), nếu không thì lấy từ SLCu, cuối cùng mới lấy từ SL
                            newVtyeucau.SL = slMoiValue ?? slCuValue ?? ((SL != null && i < SL.Count) ? (SL[i] ?? 0) : 0);
                            newVtyeucau.DonVi = DonVi[i];
                            newVtyeucau.GhiChu = ghiChuValue;
                            newVtyeucau.YCMakho = "VT mới";
                            newVtyeucau.NgayNhapkho = null;
                            newVtyeucau.NgayBaohanh = null;
                            newVtyeucau.ThoiGianBH = null;
                            // Set trạng thái cho vật tư: nếu có dự án thì "Chờ quản lý dự án duyệt", nếu không có dự án thì "Chờ Giám đốc duyệt"
                            if (duan != null)
                            {
                                newVtyeucau.TrangThai = "Chờ quản lý dự án duyệt";
                            }
                            else
                            {
                                newVtyeucau.TrangThai = "Chờ Giám đốc duyệt";
                            }
                            _context.vtyeucau.Add(newVtyeucau);
                        }
                    }
                    _context.SaveChanges();
                }
                if (yeucau.TrangThai == "Đã duyệt")
                {
                    Xuliphieuyeucau(yeucau.MaYeucau, phieuxuatkho, vtphieuxuatkho, phieumuahang, vtphieumuahang, yeucau, vtyeucau);
                }

                // Gửi thông báo email cho QLDA hoặc Giám đốc khi Trưởng BP Mua hàng tạo yêu cầu
                if (chucVu2 == "Trưởng BP" && boPhan2 == "BP mua hàng")
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"[TruongBPMuahang/ThemyeucauSQL] ===== BẮT ĐẦU GỬI EMAIL SAU KHI TẠO YÊU CẦU =====");
                        System.Diagnostics.Debug.WriteLine($"[TruongBPMuahang/ThemyeucauSQL] MaYeucau = {yeucau.MaYeucau}");
                        System.Diagnostics.Debug.WriteLine($"[TruongBPMuahang/ThemyeucauSQL] TrangThai = {yeucau.TrangThai}");
                        System.Diagnostics.Debug.WriteLine($"[TruongBPMuahang/ThemyeucauSQL] YCMaDuan = {yeucau.YCMaDuan ?? "(null)"}");
                        System.Diagnostics.Debug.WriteLine($"[TruongBPMuahang/ThemyeucauSQL] hasMaDuan = {!string.IsNullOrWhiteSpace(yeucau.YCMaDuan)}");

                        var maYeucauForEmail = yeucau.MaYeucau;
                        var hasMaDuanForEmail = !string.IsNullOrWhiteSpace(yeucau.YCMaDuan);
                        var maDuanForEmail = yeucau.YCMaDuan;

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                System.Diagnostics.Debug.WriteLine($"[TruongBPMuahang/ThemyeucauSQL/Task] Bắt đầu gửi email trong Task.Run");
                                using (var scope = _serviceScopeFactory.CreateScope())
                                {
                                    var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                                    System.Diagnostics.Debug.WriteLine($"[TruongBPMuahang/ThemyeucauSQL/Task] Đã tạo scope và lấy EmailService");

                                    if (hasMaDuanForEmail && !string.IsNullOrWhiteSpace(maDuanForEmail))
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[TruongBPMuahang/ThemyeucauSQL/Task] Gửi email cho QLDA. MaYeucau = {maYeucauForEmail}, MaDuan = {maDuanForEmail}");
                                        await emailService.SendNotificationToProjectManagerAsync(maYeucauForEmail, maDuanForEmail);
                                        System.Diagnostics.Debug.WriteLine($"[TruongBPMuahang/ThemyeucauSQL/Task] ✅ Đã gửi email cho QLDA xong.");
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[TruongBPMuahang/ThemyeucauSQL/Task] Gửi email cho Giám đốc. MaYeucau = {maYeucauForEmail}");
                                        await emailService.SendNotificationToDirectorAsync(maYeucauForEmail);
                                        System.Diagnostics.Debug.WriteLine($"[TruongBPMuahang/ThemyeucauSQL/Task] ✅ Đã gửi email cho Giám đốc xong.");
                                    }
                                }
                            }
                            catch (Exception exInner)
                            {
                                System.Diagnostics.Debug.WriteLine($"[TruongBPMuahang/ThemyeucauSQL/Task] ❌ Lỗi trong Task.Run khi gửi email: {exInner.Message}");
                                System.Diagnostics.Debug.WriteLine($"[TruongBPMuahang/ThemyeucauSQL/Task] Stack trace: {exInner.StackTrace}");
                                if (exInner.InnerException != null)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[TruongBPMuahang/ThemyeucauSQL/Task] Inner exception: {exInner.InnerException.Message}");
                                }
                            }
                        });
                        System.Diagnostics.Debug.WriteLine($"[TruongBPMuahang/ThemyeucauSQL] ✅ Đã khởi tạo Task.Run để gửi email");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TruongBPMuahang/ThemyeucauSQL] ❌ Lỗi khi khởi tạo Task.Run: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"[TruongBPMuahang/ThemyeucauSQL] Stack trace: {ex.StackTrace}");
                    }
                }
            }
            else
            {
                // Tạo mã phiếu nhập kho duy nhất bằng service
                phieunhapkho.MaNhapkho = _phieuCodeService.GenerateMaNhapKho(phieunhapkho.MaDuan, phieunhapkho.MaYeucau);
            }

            return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPMuahang" });

        }
        [HttpPost]
        public IActionResult XuLyYeucau(string MaYeucau, string action, phieuxuatkho phieuxuatkho, vtphieuxuatkho vtphieuxuatkho, phieumuahang phieumuahang, vtphieumuahang vtphieumuahang, yeucau yeucau, vtyeucau vtyeucau)
        {
            var chucVu2 = HttpContext.Session.GetString("Chucvu");
            var boPhan2 = HttpContext.Session.GetString("Bophan");
            var maNv2 = HttpContext.Session.GetString("MaNguoidung");

            // Lấy yêu cầu hiện tại từ cơ sở dữ liệu
            var Yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
            if (Yeucau == null)
            {
                // Xử lý nếu không tìm thấy yêu cầu
                return NotFound();
            }

            var duan = _context.duans.FirstOrDefault(d => d.MaDuan == Yeucau.YCMaDuan);
            var nguoiYeuCau = _context.nguoidungs.FirstOrDefault(nd => nd.MaNguoidung == Yeucau.YCMaNguoidung);
            bool yeuCauTuGiamDoc = string.Equals(nguoiYeuCau?.Chucvu?.Trim(), "Giám đốc", StringComparison.OrdinalIgnoreCase);

            if (action == "approve")
            {
                // Xử lý khi trạng thái là "Chờ Trưởng BP-BP mua hàng duyệt"
                if (Yeucau.TrangThai == "Chờ Trưởng BP-BP mua hàng duyệt" && chucVu2 == "Trưởng BP" && boPhan2 == "BP mua hàng")
                {
                    if (yeuCauTuGiamDoc)
                    {
                        // Bỏ qua bước QLDA, duyệt trực tiếp như Giám đốc
                        Yeucau.TrangThai = "Đã duyệt";
                        Xuliphieuyeucau(Yeucau.MaYeucau, phieuxuatkho, vtphieuxuatkho, phieumuahang, vtphieumuahang, yeucau, vtyeucau);
                    }
                    else
                    {
                        // Kiểm tra xem có phải là yêu cầu nhập kho không
                        bool isNhapKho = !string.IsNullOrEmpty(Yeucau.MaYeucau) && 
                                        (Yeucau.MaYeucau.StartsWith("NHAPKHO_DUAN_") || 
                                         Yeucau.MaYeucau.StartsWith("NHAPKHO_CANHAN_"));
                        
                        if (isNhapKho)
                        {
                            // Nếu là nhập kho
                            if (Yeucau.MaYeucau.StartsWith("NHAPKHO_DUAN_"))
                            {
                                // Dự án: Chờ quản lý dự án duyệt
                                Yeucau.TrangThai = "Chờ quản lý dự án duyệt";
                            }
                            else if (Yeucau.MaYeucau.StartsWith("NHAPKHO_CANHAN_"))
                            {
                                // Cá nhân: Chờ Giám đốc duyệt
                                Yeucau.TrangThai = "Chờ Giám đốc duyệt";
                            }
                        }
                        else
                        {
                            // Nếu là yêu cầu vật tư thông thường
                            if (duan != null)
                            {
                                // Có dự án: Chờ quản lý dự án duyệt
                                Yeucau.TrangThai = "Chờ quản lý dự án duyệt";
                                
                                // Đồng bộ trạng thái cho tất cả vật tư (bao gồm cả null/empty)
                                var allVatTu = _context.vtyeucau.Where(v => v.VTMaYeucau == MaYeucau).ToList();
                                foreach (var vt in allVatTu)
                                {
                                    // Xử lý vật tư có TrangThai null hoặc rỗng
                                    if (string.IsNullOrEmpty(vt.TrangThai) || 
                                        (vt.TrangThai != "Đã duyệt" && vt.TrangThai != "Đang mua hàng" && 
                                         vt.TrangThai != "Đã từ chối" && vt.TrangThai != "Đã xuất kho" && 
                                         vt.TrangThai != "Đã nhận hàng"))
                                    {
                                        vt.TrangThai = "Chờ quản lý dự án duyệt";
                                        _context.vtyeucau.Update(vt);
                                    }
                                }
                            }
                            else
                            {
                                // Cá nhân: Chờ Giám đốc duyệt
                                Yeucau.TrangThai = "Chờ Giám đốc duyệt";
                                
                                // Đồng bộ trạng thái cho tất cả vật tư (bao gồm cả null/empty)
                                var allVatTu = _context.vtyeucau.Where(v => v.VTMaYeucau == MaYeucau).ToList();
                                foreach (var vt in allVatTu)
                                {
                                    // Xử lý vật tư có TrangThai null hoặc rỗng
                                    if (string.IsNullOrEmpty(vt.TrangThai) || 
                                        (vt.TrangThai != "Đã duyệt" && vt.TrangThai != "Đang mua hàng" && 
                                         vt.TrangThai != "Đã từ chối" && vt.TrangThai != "Đã xuất kho" && 
                                         vt.TrangThai != "Đã nhận hàng"))
                                    {
                                        vt.TrangThai = "Chờ Giám đốc duyệt";
                                        _context.vtyeucau.Update(vt);
                                    }
                                }
                            }
                        }
                    }
                }
                // Kiểm tra trạng thái hiện tại - chỉ xử lý nếu trạng thái phù hợp với vai trò
                // Nếu trạng thái đã là "Giám đốc" hoặc "Đã duyệt", không xử lý (để giám đốc xử lý)
                else if (Yeucau.TrangThai == "Giám đốc" || Yeucau.TrangThai == "Đã duyệt")
                {
                    // Trạng thái đã được xử lý bởi giám đốc, không làm gì
                    return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPMuahang" });
                }
                else if (duan != null)
                {
                    string maNguoiQLDA = duan.MaNguoiQLDA;
                    if (maNv2 == maNguoiQLDA)
                    {
                        if (chucVu2 != "Giám đốc")
                        {
                            Yeucau.TrangThai = "Giám đốc";
                            // Gửi thông báo đến Giám đốc sau khi QLDA duyệt
                            _ = _emailService.SendNotificationToDirectorAsync(Yeucau.MaYeucau);
                            
                            // Đồng bộ trạng thái tất cả vật tư khi chuyển sang "Giám đốc" (bao gồm cả null/empty)
                            var allVatTu = _context.vtyeucau.Where(v => v.VTMaYeucau == MaYeucau).ToList();
                            foreach (var vt in allVatTu)
                            {
                                // Xử lý vật tư có TrangThai null hoặc rỗng
                                if (string.IsNullOrEmpty(vt.TrangThai) || 
                                    (vt.TrangThai != "Đã duyệt" && vt.TrangThai != "Đang mua hàng" && 
                                     vt.TrangThai != "Đã từ chối" && vt.TrangThai != "Đã xuất kho" && 
                                     vt.TrangThai != "Đã nhận hàng"))
                                {
                                    vt.TrangThai = "Chờ giám đốc duyệt";
                                    _context.vtyeucau.Update(vt);
                                }
                            }
                        }
                        else
                        {
                            Yeucau.TrangThai = "Đã duyệt";
                            Xuliphieuyeucau(Yeucau.MaYeucau, phieuxuatkho, vtphieuxuatkho, phieumuahang, vtphieumuahang, yeucau, vtyeucau);
                        }
                    }
                    else
                    {
                        if (Yeucau.YCMaNguoidung != maNguoiQLDA)
                        {
                            if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kỹ thuật")
                            {
                                Yeucau.TrangThai = "Quản lí dự án";
                                // Gửi thông báo đến QLDA sau khi Trưởng BP duyệt
                                _ = _emailService.SendNotificationToProjectManagerAsync(Yeucau.MaYeucau, duan.MaDuan);
                            }
                            else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kho")
                            {
                                Yeucau.TrangThai = "Quản lí dự án";
                                // Gửi thông báo đến QLDA sau khi Trưởng BP duyệt
                                _ = _emailService.SendNotificationToProjectManagerAsync(Yeucau.MaYeucau, duan.MaDuan);
                            }
                            else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP mua hàng")
                            {
                                Yeucau.TrangThai = "Quản lí dự án";
                                // Gửi thông báo đến QLDA sau khi Trưởng BP duyệt
                                _ = _emailService.SendNotificationToProjectManagerAsync(Yeucau.MaYeucau, duan.MaDuan);
                            }
                            else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kế toán")
                            {
                                Yeucau.TrangThai = "Quản lí dự án";
                                // Gửi thông báo đến QLDA sau khi Trưởng BP duyệt
                                _ = _emailService.SendNotificationToProjectManagerAsync(Yeucau.MaYeucau, duan.MaDuan);
                            }
                            else if (chucVu2 == "Giám đốc")
                            {
                                Yeucau.TrangThai = "Đã duyệt";
                                Xuliphieuyeucau(Yeucau.MaYeucau, phieuxuatkho, vtphieuxuatkho, phieumuahang, vtphieumuahang, yeucau, vtyeucau);
                            }
                        }
                        else
                        {
                            if (chucVu2 != "Giám đốc")
                            {
                                Yeucau.TrangThai = "Giám đốc";
                                // Gửi thông báo đến Giám đốc sau khi QLDA duyệt
                                _ = _emailService.SendNotificationToDirectorAsync(Yeucau.MaYeucau);
                                
                                // Đồng bộ trạng thái tất cả vật tư khi chuyển sang "Giám đốc"
                                var allVatTu = _context.vtyeucau.Where(v => v.VTMaYeucau == MaYeucau).ToList();
                                foreach (var vt in allVatTu)
                                {
                                    if (vt.TrangThai != "Đã duyệt" && vt.TrangThai != "Đang mua hàng" && 
                                        vt.TrangThai != "Đã từ chối" && vt.TrangThai != "Đã xuất kho" && 
                                        vt.TrangThai != "Đã nhận hàng")
                                    {
                                        vt.TrangThai = "Chờ giám đốc duyệt";
                                        _context.vtyeucau.Update(vt);
                                    }
                                }
                            }
                            else
                            {
                                Yeucau.TrangThai = "Đã duyệt";
                                Xuliphieuyeucau(Yeucau.MaYeucau, phieuxuatkho, vtphieuxuatkho, phieumuahang, vtphieumuahang, yeucau, vtyeucau);
                            }
                        }
                    }
                }
                else
                {
                    // Chỉ xử lý nếu trạng thái hiện tại là "Trưởng BP-BP [tên bộ phận]"
                    if (Yeucau.TrangThai == "Trưởng BP-BP mua hàng" && chucVu2 == "Trưởng BP" && boPhan2 == "BP mua hàng")
                    {
                        Yeucau.TrangThai = "Giám đốc";
                        // Gửi thông báo đến Giám đốc sau khi Trưởng BP duyệt (không có dự án)
                        _ = _emailService.SendNotificationToDirectorAsync(Yeucau.MaYeucau);
                        
                        // Đồng bộ trạng thái tất cả vật tư khi chuyển sang "Giám đốc" (bao gồm cả null/empty)
                        var allVatTu = _context.vtyeucau.Where(v => v.VTMaYeucau == MaYeucau).ToList();
                        foreach (var vt in allVatTu)
                        {
                            // Xử lý vật tư có TrangThai null hoặc rỗng
                            if (string.IsNullOrEmpty(vt.TrangThai) || 
                                (vt.TrangThai != "Đã duyệt" && vt.TrangThai != "Đang mua hàng" && 
                                 vt.TrangThai != "Đã từ chối" && vt.TrangThai != "Đã xuất kho" && 
                                 vt.TrangThai != "Đã nhận hàng"))
                            {
                                vt.TrangThai = "Chờ giám đốc duyệt";
                                _context.vtyeucau.Update(vt);
                            }
                        }
                    }
                    else if (chucVu2 == "Nhân viên" && boPhan2 == "BP kỹ thuật")
                    {
                        Yeucau.TrangThai = "Trưởng BP-BP kỹ thuật";
                    }
                    else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kỹ thuật" && Yeucau.TrangThai == "Trưởng BP-BP kỹ thuật")
                    {
                        Yeucau.TrangThai = "Giám đốc";
                        // Gửi thông báo đến Giám đốc sau khi Trưởng BP duyệt (không có dự án)
                        _ = _emailService.SendNotificationToDirectorAsync(Yeucau.MaYeucau);
                    }
                    else if (chucVu2 == "Nhân viên" && boPhan2 == "BP kho")
                    {
                        Yeucau.TrangThai = "Trưởng BP-BP kho";
                    }
                    else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kho" && Yeucau.TrangThai == "Trưởng BP-BP kho")
                    {
                        Yeucau.TrangThai = "Giám đốc";
                        // Gửi thông báo đến Giám đốc sau khi Trưởng BP duyệt (không có dự án)
                        _ = _emailService.SendNotificationToDirectorAsync(Yeucau.MaYeucau);
                    }
                    else if (chucVu2 == "Nhân viên" && boPhan2 == "BP mua hàng")
                    {
                        Yeucau.TrangThai = "Trưởng BP-BP mua hàng";
                    }
                    else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP mua hàng" && Yeucau.TrangThai == "Trưởng BP-BP mua hàng")
                    {
                        Yeucau.TrangThai = "Giám đốc";
                        // Gửi thông báo đến Giám đốc sau khi Trưởng BP duyệt (không có dự án)
                        _ = _emailService.SendNotificationToDirectorAsync(Yeucau.MaYeucau);
                    }
                    else if (chucVu2 == "Giám đốc")
                    {
                        Yeucau.TrangThai = "Đã duyệt";
                        Xuliphieuyeucau(Yeucau.MaYeucau, phieuxuatkho, vtphieuxuatkho, phieumuahang, vtphieumuahang, yeucau, vtyeucau);
                    }
                }
            }
            else if (action == "reject")
            {
                Xulituchoiyeucau(MaYeucau, yeucau, vtyeucau, null, null, saveChanges: false);
            }
            _context.yeucau.Update(Yeucau);
            _context.SaveChanges();

            return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPMuahang" });
        }

        [HttpPost]
        public IActionResult Xuliphieuyeucau(
                                string Mayeucau,
                                phieuxuatkho phieuxuatkho,
                                vtphieuxuatkho vtphieuxuatkho,
                                phieumuahang phieumuahang,
                                vtphieumuahang vtphieumuahang,
                                yeucau yeucau,
                                vtyeucau vtyeucau)
        {
            var danhSachVatTuYC = _context.vtyeucau
                                          .Where(vt => vt.VTMaYeucau == Mayeucau)
                                          .ToList();

            var thongTinYeuCau = _context.yeucau
                                        .FirstOrDefault(yc => yc.MaYeucau == Mayeucau);

            if (thongTinYeuCau == null || danhSachVatTuYC == null || !danhSachVatTuYC.Any())
            {
                Console.WriteLine("Không tìm thấy yêu cầu hoặc danh sách vật tư.");
                return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPMuahang" });
            }

            var makhoList = danhSachVatTuYC.Select(vt => vt.YCMakho).ToList();

            var DanhsachVTYCkhotong = _context.khotongs
                                               .Where(kt => makhoList.Contains(kt.Makho))
                                               .ToList();


            // Tạo mã phiếu xuất kho và mua hàng bằng service
            string Maxuatkho = _phieuCodeService.GenerateMaXuatKho(thongTinYeuCau.YCMaDuan, thongTinYeuCau.MaYeucau);
            string Mamuahang = _phieuCodeService.GenerateMaMuaHang(thongTinYeuCau.YCMaDuan, thongTinYeuCau.MaYeucau);

            bool isPhieuXuatKhoCreated = false;
            bool isPhieuMuaHangCreated = false;
            foreach (var VattuYC in danhSachVatTuYC)
            {
                var khotong = DanhsachVTYCkhotong.FirstOrDefault(kt => kt.Makho == VattuYC.YCMakho && kt.MaSanpham == VattuYC.MaSanpham);
                
                if (khotong != null)
                {
                    // Tính số lượng hàng đã cam kết từ các phiếu xuất khác (chưa có phiếu hiện tại nên không cần loại trừ)
                    int soLuongDaCamKet = TinhSoLuongDaCamKet(khotong.Makho ?? "", khotong.MaSanpham ?? "", null);
                    
                    // Số lượng khả dụng = Tồn kho - Số lượng đã cam kết
                    int soLuongKhaDung = (khotong.SL ?? 0) - soLuongDaCamKet;

                    if (soLuongKhaDung > 0 && soLuongKhaDung < VattuYC.SL)
                    {
                        // Số lượng khả dụng nhỏ hơn số lượng yêu cầu
                        isPhieuMuaHangCreated = true;
                        isPhieuXuatKhoCreated = true;
                    }
                    else if (soLuongKhaDung == 0)
                    {
                        // Số lượng khả dụng bằng 0
                        isPhieuMuaHangCreated = true;
                    }
                    else if (soLuongKhaDung >= VattuYC.SL)
                    {
                        // Số lượng khả dụng đủ đáp ứng
                        isPhieuXuatKhoCreated = true;
                    }
                    else
                    {
                        // Số lượng khả dụng < 0 (tồn kho < đã cam kết) - cần mua hàng
                        isPhieuMuaHangCreated = true;
                    }
                }
                else
                {
                    // Không tìm thấy kho tổng - cần mua hàng
                    isPhieuMuaHangCreated = true;
                }
            }
            if (isPhieuMuaHangCreated == true && isPhieuXuatKhoCreated == true)
            {
                var Phieuxuatkho = new phieuxuatkho
                {
                    MaXuatkho = Maxuatkho,
                    MaYeucau = thongTinYeuCau.MaYeucau,
                    MaDuan = thongTinYeuCau.YCMaDuan,
                    MaNguoidung = thongTinYeuCau.YCMaNguoidung,
                    NgayXuatkho = DateTime.Now,
                    TrangThai = "Đang chuẩn bị hàng"
                };
                _context.Add(Phieuxuatkho);

                var Phieumuahang = new phieumuahang
                {
                    MaMuahang = Mamuahang,
                    MaYeucau = thongTinYeuCau.MaYeucau,
                    MaDuan = thongTinYeuCau.YCMaDuan,
                    NgayMuahang = DateTime.Now,
                    MaNguoidung = thongTinYeuCau.YCMaNguoidung,
                    TrangThai = "Đang chờ báo giá"
                };
                _context.Add(Phieumuahang);

            }
            else if (isPhieuMuaHangCreated == true)
            {

                var Phieumuahang = new phieumuahang
                {
                    MaMuahang = Mamuahang,
                    MaYeucau = thongTinYeuCau.MaYeucau,
                    MaDuan = thongTinYeuCau.YCMaDuan,
                    NgayMuahang = DateTime.Now,
                    MaNguoidung = thongTinYeuCau.YCMaNguoidung,
                    TrangThai = "Đang chờ báo giá"
                };
                _context.Add(Phieumuahang);

            }
            else if (isPhieuXuatKhoCreated == true)
            {
                var Phieuxuatkho = new phieuxuatkho
                {
                    MaXuatkho = Maxuatkho,
                    MaYeucau = thongTinYeuCau.MaYeucau,
                    MaDuan = thongTinYeuCau.YCMaDuan,
                    MaNguoidung = thongTinYeuCau.YCMaNguoidung,
                    NgayXuatkho = DateTime.Now,
                    TrangThai = "Đang chuẩn bị hàng"
                };
                _context.Add(Phieuxuatkho);
            }


            _context.SaveChanges();

            foreach (var VattuYC in danhSachVatTuYC)
            {
                var khotong = _context.khotongs.FirstOrDefault(yc => yc.Makho == VattuYC.YCMakho && yc.MaSanpham == VattuYC.MaSanpham);

                if (khotong != null && khotong.SL > 0)
                {
                    // Tính số lượng hàng đã cam kết (đã duyệt nhưng chưa giao)
                    int soLuongDaCamKet = TinhSoLuongDaCamKet(khotong.Makho ?? "", khotong.MaSanpham ?? "", Maxuatkho);
                    
                    // Số lượng khả dụng = Tồn kho - Số lượng đã cam kết
                    int soLuongKhaDung = (khotong.SL ?? 0) - soLuongDaCamKet;

                    var VTPhieuxuatkho = new vtphieuxuatkho
                    {
                        MaXuatkho = Maxuatkho,
                        MaYeucau = VattuYC.VTMaYeucau,
                        TenSanpham = khotong.TenSanpham,
                        MaSanpham = khotong.MaSanpham,
                        Makho = khotong.Makho,
                        HangSX = khotong.HangSX,
                        NhaCC = khotong.NhaCC,
                        DonVi = khotong.DonVi,
                        NgayBaohanh = khotong.NgayBaohanh,
                        ThoiGianBH = khotong.ThoiGianBH,
                        TrangThai = "Đang chuẩn bị hàng"
                    };

                    if (soLuongKhaDung >= VattuYC.SL)
                    {
                        VTPhieuxuatkho.SL = VattuYC.SL;
                        // KHÔNG trừ kho ở đây - chỉ trừ khi người nhận xác nhận đã nhận hàng
                        VattuYC.TrangThai = "Đã duyệt";
                        _context.vtyeucau.Update(VattuYC);
                    }
                    else
                    {
                        VTPhieuxuatkho.SL = soLuongKhaDung > 0 ? soLuongKhaDung : 0;
                        var SLThieu = VattuYC.SL - soLuongKhaDung;
                        VattuYC.TrangThai = "Đang mua hàng";
                        
                        // Đảm bảo vtyeucau được cập nhật trước để YCMakho tồn tại trong database
                        _context.vtyeucau.Update(VattuYC);
                        _context.SaveChanges(); // Lưu để đảm bảo YCMakho tồn tại trước khi tạo vtphieumuahang
                        
                        var VTPhieumuahang = new vtphieumuahang
                        {
                            MaMuahang = Mamuahang,
                            MaYeucau = VattuYC.VTMaYeucau,
                            TenSanpham = VattuYC.TenSanpham,
                            MaSanpham = VattuYC.MaSanpham,
                            Makho = VattuYC.YCMakho,
                            HangSX = VattuYC.HangSX,
                            NhaCC = VattuYC.NhaCC,
                            DonVi = VattuYC.DonVi,
                            SL = SLThieu,
                            NgayBaohanh = VattuYC.NgayBaohanh,
                            ThoiGianBH = VattuYC.ThoiGianBH,
                            TrangThai = "Đang chờ báo giá"
                        };

                        _context.Add(VTPhieumuahang);
                        // KHÔNG trừ kho ở đây - chỉ trừ khi người nhận xác nhận đã nhận hàng
                    }
                    // KHÔNG cập nhật khotong ở đây - chỉ cập nhật khi người nhận xác nhận đã nhận hàng
                    _context.Add(VTPhieuxuatkho);
                }
                else
                {
                    VattuYC.TrangThai = "Đang mua hàng";
                    
                    // Đảm bảo vtyeucau được cập nhật trước để YCMakho tồn tại trong database
                    _context.vtyeucau.Update(VattuYC);
                    _context.SaveChanges(); // Lưu để đảm bảo YCMakho tồn tại trước khi tạo vtphieumuahang
                    
                    var VTPhieumuahang = new vtphieumuahang
                    {
                        MaMuahang = Mamuahang,
                        MaYeucau = VattuYC.VTMaYeucau,
                        TenSanpham = VattuYC.TenSanpham,
                        MaSanpham = VattuYC.MaSanpham,
                        Makho = VattuYC.YCMakho,
                        HangSX = VattuYC.HangSX,
                        NhaCC = VattuYC.NhaCC,
                        DonVi = VattuYC.DonVi,
                        SL = VattuYC.SL,
                        NgayBaohanh = VattuYC.NgayBaohanh,
                        ThoiGianBH = VattuYC.ThoiGianBH,
                        TrangThai = "Đang chờ báo giá"
                    };

                    _context.Add(VTPhieumuahang);
                }
            }

            _context.SaveChanges();


            return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPMuahang" });
        }

        [HttpPost]
        public IActionResult Xuliphieuxuatkho(
                                string MaXuatkho,
                                phieuxuatkho phieuxuatkho,
                                vtphieuxuatkho vtphieuxuatkho,
                                khoduans khoduans)
        {
            var VTphieuxuatkho = _context.vtphieuxuatkho
                                          .Where(vt => vt.MaXuatkho == MaXuatkho)
                                          .ToList();

            var Phieuxuatkho = _context.phieuxuatkho
                                        .FirstOrDefault(yc => yc.MaXuatkho == MaXuatkho);


            if (Phieuxuatkho.TrangThai == "Đang chuẩn bị hàng")
            {
                Phieuxuatkho.TrangThai = "Chờ lấy hàng";
                _context.phieuxuatkho.Update(Phieuxuatkho);
                _context.SaveChanges();
            }
            else if (Phieuxuatkho.TrangThai == "Chờ lấy hàng")
            {
                if (!Phieuxuatkho.NgayXuatkho.HasValue)
                {
                    Phieuxuatkho.NgayXuatkho = DateTime.Now;
                }
                if (Phieuxuatkho.MaDuan != null)
                {
                    foreach (var VTxuatkho in VTphieuxuatkho)
                    {
                        var VTphieuxuatkhott = _context.vtphieuxuatkho.FirstOrDefault(vt => vt.MaXuatkho == VTxuatkho.MaXuatkho);
                        VTphieuxuatkhott.TrangThai = "Đã xuất kho";
                        _context.vtphieuxuatkho.Update(VTphieuxuatkhott);
                        var VTduan = new khoduans
                        {
                            DAMaDuan = Phieuxuatkho.MaDuan,
                            TenSanpham = VTxuatkho.TenSanpham,
                            MaSanpham = VTxuatkho.MaSanpham,
                            DAMakho = VTxuatkho.Makho,
                            HangSX = VTxuatkho.HangSX,
                            NhaCC = VTxuatkho.NhaCC,
                            DonVi = VTxuatkho.DonVi,
                            SL = VTxuatkho.SL,
                            NgayBaohanh = VTxuatkho.NgayBaohanh,
                            ThoiGianBH = VTxuatkho.ThoiGianBH,
                            TrangThai = "Đã xuất kho"
                        };
                        _context.Add(VTduan);
                    }
                    _context.SaveChanges();
                }
                else
                {
                    foreach (var VTxuatkho in VTphieuxuatkho)
                    {
                        var VTphieuxuatkhott = _context.vtphieuxuatkho.FirstOrDefault(vt => vt.MaXuatkho == VTxuatkho.MaXuatkho);
                        VTphieuxuatkhott.TrangThai = "Đã xuất kho";
                        _context.vtphieuxuatkho.Update(VTphieuxuatkhott);
                        var VTkhonguoidungtt = _context.khonguoidungs.FirstOrDefault(nd => nd.NDMakho == VTxuatkho.Makho && nd.NDMaNguoidung == Phieuxuatkho.MaNguoidung);
                        if (VTkhonguoidungtt != null)
                        {
                            VTkhonguoidungtt.SL = VTkhonguoidungtt.SL + VTxuatkho.SL;
                            _context.khonguoidungs.Update(VTkhonguoidungtt);
                        }
                        else
                        {
                            var VTkhonguoidung = new khonguoidungs
                            {
                                NDMaNguoidung = Phieuxuatkho.MaNguoidung,
                                TenSanpham = VTxuatkho.TenSanpham,
                                MaSanpham = VTxuatkho.MaSanpham,
                                NDMakho = VTxuatkho.Makho,
                                HangSX = VTxuatkho.HangSX,
                                NhaCC = VTxuatkho.NhaCC,
                                DonVi = VTxuatkho.DonVi,
                                SL = VTxuatkho.SL,
                                NgayBaohanh = VTxuatkho.NgayBaohanh,
                                ThoiGianBH = VTxuatkho.ThoiGianBH,
                                TrangThai = "Đang mượn"
                            };
                            _context.Add(VTkhonguoidung);
                        }

                    }
                }
                // Đồng bộ trạng thái vật tư yêu cầu và yêu cầu tổng khi đã xuất kho
                foreach (var VTxuatkho in VTphieuxuatkho)
                {
                    var vtYeucauList = _context.vtyeucau
                        .Where(v => v.VTMaYeucau == VTxuatkho.MaYeucau && v.MaSanpham == VTxuatkho.MaSanpham)
                        .ToList();

                    foreach (var vtYc in vtYeucauList)
                    {
                        if (vtYc.TrangThai != "Đã xuất kho")
                        {
                            vtYc.TrangThai = "Đã xuất kho";
                            _context.vtyeucau.Update(vtYc);
                        }
                    }
                }

                var maYeucauList = VTphieuxuatkho
                    .Select(v => v.MaYeucau)
                    .Where(ma => !string.IsNullOrEmpty(ma))
                    .Distinct()
                    .ToList();

                foreach (var maYc in maYeucauList)
                {
                    var yeuCau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == maYc);
                    if (yeuCau == null)
                    {
                        continue;
                    }

                    var vtList = _context.vtyeucau
                        .Where(v => v.VTMaYeucau == maYc)
                        .ToList();

                    // Sử dụng helper để đồng bộ trạng thái
                    yeuCau.TrangThai = YeucauUpdateHelper.TinhTrangThaiYeuCau(vtList);
                    _context.yeucau.Update(yeuCau);
                }
                Phieuxuatkho.TrangThai = "Đã lấy hàng";
                _context.phieuxuatkho.Update(Phieuxuatkho);
                _context.SaveChanges();
            }
            else if (Phieuxuatkho.TrangThai == "Đã lấy hàng")
            {
                Phieuxuatkho.TrangThai = "Hoàn thành";
                _context.phieuxuatkho.Update(Phieuxuatkho);
                _context.SaveChanges();
            }
            return RedirectToAction("Phieuxuatkho", "Yeucau", new { area = "TruongBPMuahang" });
        }

        [HttpPost]
        public IActionResult ThemPhieumuahangSQL([FromBody] Phieumuahangviewmodel model)
        {
            try
            {
                var MaMuahang = model.MaMuahang;
                Console.WriteLine($"MaMuahang nhận được: {MaMuahang}");

                var Phieumuahang = _context.phieumuahang
                                            .FirstOrDefault(y => y.MaMuahang == MaMuahang);
                if (Phieumuahang == null)
                {
                    Console.WriteLine("Không tìm thấy Phieumuahang.");
                    return Json(new { success = false, message = "Mã mua hàng không tồn tại!" });
                }

                var VTPhieumuahanglist = _context.vtphieumuahang
                                                  .Where(kt => kt.MaMuahang == MaMuahang)
                                                  .ToList();

                Console.WriteLine($"Số vật tư được tìm thấy: {VTPhieumuahanglist.Count}");
                Console.WriteLine($"Số lượng phần tử trong VTphieumuahang: {model.VTphieumuahang?.Count ?? 0}");

                // Tạo dictionary để tìm nhanh vật tư theo MaSanpham
                var vtmuahangDict = new Dictionary<string, vtphieumuahang>();
                foreach (var vt in VTPhieumuahanglist)
                {
                    if (!string.IsNullOrEmpty(vt.MaSanpham))
                    {
                        vtmuahangDict[vt.MaSanpham] = vt;
                    }
                }

                // Cập nhật vật tư theo dữ liệu gửi lên
                // - DonGia/ThanhTien: chỉ cập nhật khi có giá > 0 (báo giá)
                // - Các trường BP mua hàng (NCC/Ngày/Ghi chú): cập nhật khi có giá trị (để chỉ lưu DB khi bấm "Gửi báo giá")
                int updatedCount = 0;
                if (model.VTphieumuahang != null)
                {
                    foreach (var updatedVTmuahang in model.VTphieumuahang)
                    {
                        if (string.IsNullOrEmpty(updatedVTmuahang.MaSanpham))
                        {
                            continue;
                        }

                        if (!vtmuahangDict.TryGetValue(updatedVTmuahang.MaSanpham, out var VTmuahang))
                        {
                            continue;
                        }

                        bool changed = false;

                        // NCC (BP mua hàng)
                        if (!string.IsNullOrWhiteSpace(updatedVTmuahang.NhaCC))
                        {
                            VTmuahang.NhaCC = updatedVTmuahang.NhaCC.Trim();
                            changed = true;

                            // Đồng bộ NCC sang các bảng phiếu liên quan để các phiếu "lấy" đúng NCC
                            // - vtyeucau: chi tiết yêu cầu
                            // - vtphieuxuatkho: chi tiết phiếu xuất kho
                            // - vtphieunhapkho: chi tiết phiếu nhập kho
                            if (!string.IsNullOrWhiteSpace(VTmuahang.MaYeucau))
                            {
                                var maYc = VTmuahang.MaYeucau;
                                var maSp = VTmuahang.MaSanpham;
                                var ncc = VTmuahang.NhaCC;

                                var vtyeucauList = _context.vtyeucau
                                    .Where(v => v.VTMaYeucau == maYc && v.MaSanpham == maSp)
                                    .ToList();
                                foreach (var vty in vtyeucauList)
                                {
                                    vty.NhaCC = ncc;
                                }

                                var vtxuatList = _context.vtphieuxuatkho
                                    .Where(v => v.MaYeucau == maYc && v.MaSanpham == maSp)
                                    .ToList();
                                foreach (var vtx in vtxuatList)
                                {
                                    vtx.NhaCC = ncc;
                                }

                                var vtnhapList = _context.vtphieunhapkho
                                    .Where(v => v.MaYeucau == maYc && v.MaSanpham == maSp)
                                    .ToList();
                                foreach (var vtn in vtnhapList)
                                {
                                    vtn.NhaCC = ncc;
                                }
                            }
                        }

                        // Ghi chú (BP mua hàng)
                        if (!string.IsNullOrWhiteSpace(updatedVTmuahang.GhiChuBPMuahang))
                        {
                            VTmuahang.GhiChuBPMuahang = updatedVTmuahang.GhiChuBPMuahang.Trim();
                            changed = true;
                        }

                        // Ngày thanh toán (BP mua hàng)
                        if (updatedVTmuahang.NgayThanhToanBPMuahang != null)
                        {
                            VTmuahang.NgayThanhToanBPMuahang = updatedVTmuahang.NgayThanhToanBPMuahang;
                            changed = true;
                        }

                        // Ngày có hàng (đồng bộ sang vtyeucau như endpoint CapNhatNgayCoHang)
                        if (updatedVTmuahang.NgayCoHang != null)
                        {
                            VTmuahang.NgayCoHang = updatedVTmuahang.NgayCoHang;
                            changed = true;

                            if (!string.IsNullOrWhiteSpace(VTmuahang.MaYeucau))
                            {
                                var vtyeucauList = _context.vtyeucau
                                    .Where(v => v.VTMaYeucau == VTmuahang.MaYeucau && v.MaSanpham == VTmuahang.MaSanpham)
                                    .ToList();

                                foreach (var vty in vtyeucauList)
                                {
                                    vty.NgayCoHang = updatedVTmuahang.NgayCoHang;
                                }
                            }
                        }

                        // Báo giá (chỉ khi có giá hợp lệ)
                        if (updatedVTmuahang.DonGia != null && updatedVTmuahang.DonGia > 0)
                        {
                            Console.WriteLine($"Cập nhật VTmuahang: {updatedVTmuahang.MaSanpham}");

                            VTmuahang.DonGia = updatedVTmuahang.DonGia;
                            VTmuahang.ThanhTien = updatedVTmuahang.ThanhTien
                                ?? (updatedVTmuahang.DonGia.Value * (VTmuahang.SL ?? 0));

                            Console.WriteLine($"Đơn giá là: {updatedVTmuahang.DonGia}");
                            Console.WriteLine($"Thành tiền là: {VTmuahang.ThanhTien}");

                            VTmuahang.TrangThai = "Đã báo giá";
                            updatedCount++;
                            changed = true;
                        }

                        if (changed)
                        {
                            _context.vtphieumuahang.Update(VTmuahang);
                        }
                    }
                }

                // Kiểm tra xem tất cả vật tư (có SL > 0) đã có giá chưa
                bool allItemsHavePrice = true;
                foreach (var VTmuahang in VTPhieumuahanglist)
                {
                    // Bỏ qua các vật tư có số lượng = 0
                    if (VTmuahang.SL == 0)
                    {
                        continue;
                    }
                    
                    // Kiểm tra xem vật tư này đã có giá và trạng thái "Đã báo giá" chưa
                    if (VTmuahang.DonGia == null || VTmuahang.DonGia <= 0 || 
                        VTmuahang.TrangThai != "Đã báo giá")
                    {
                        allItemsHavePrice = false;
                        break;
                    }
                }

                // Cập nhật trạng thái phiếu mua hàng
                if (allItemsHavePrice)
                {
                    Phieumuahang.TrangThai = "Đã báo giá";
                }
                else
                {
                    // Giữ nguyên trạng thái "Đang chờ báo giá" nếu còn mục chưa có giá
                    // Hoặc có thể đặt trạng thái mới như "Đang báo giá một phần" nếu cần
                    if (Phieumuahang.TrangThai == "Đã báo giá")
                    {
                        // Nếu trước đó đã "Đã báo giá" nhưng giờ có mục mới chưa có giá, giữ nguyên
                        // (trường hợp này ít xảy ra, nhưng để an toàn)
                    }
                    // Nếu đang "Đang chờ báo giá", giữ nguyên
                }
                
                _context.phieumuahang.Update(Phieumuahang);
                _context.SaveChanges();

                // Gửi email thông báo khi có ít nhất một vật tư đã được báo giá
                // Chỉ cần kiểm tra xem có vật tư nào được cập nhật không
                if (updatedCount > 0)
                {
                    try
                    {
                        Debug.WriteLine($"[TruongBPMuahang/ThemPhieumuahangSQL] Bắt đầu gửi email báo giá cho {MaMuahang}");
                        Debug.WriteLine($"[TruongBPMuahang/ThemPhieumuahangSQL] Số vật tư đã cập nhật: {updatedCount}");
                        Debug.WriteLine($"[TruongBPMuahang/ThemPhieumuahangSQL] Trạng thái phiếu: {Phieumuahang.TrangThai}");
                        
                        // Gửi email cho giám đốc để phê duyệt
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                using (var scope = _serviceScopeFactory.CreateScope())
                                {
                                    var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                                    await emailService.SendNotificationToDirectorOnBaoGiaAsync(MaMuahang);
                                    Debug.WriteLine($"[TruongBPMuahang/ThemPhieumuahangSQL] Đã gửi email cho Giám đốc");
                                }
                            }
                            catch (Exception exInner)
                            {
                                Debug.WriteLine($"[TruongBPMuahang/ThemPhieumuahangSQL] Lỗi gửi email cho Giám đốc: {exInner.Message}");
                                Debug.WriteLine($"[TruongBPMuahang/ThemPhieumuahangSQL] Stack trace: {exInner.StackTrace}");
                            }
                        });
                        
                        // Gửi email cho người yêu cầu
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                using (var scope = _serviceScopeFactory.CreateScope())
                                {
                                    var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                                    await emailService.SendNotificationToRequesterOnBaoGiaAsync(MaMuahang);
                                    Debug.WriteLine($"[TruongBPMuahang/ThemPhieumuahangSQL] Đã gửi email cho người yêu cầu");
                                }
                            }
                            catch (Exception exInner)
                            {
                                Debug.WriteLine($"[TruongBPMuahang/ThemPhieumuahangSQL] Lỗi gửi email cho người yêu cầu: {exInner.Message}");
                                Debug.WriteLine($"[TruongBPMuahang/ThemPhieumuahangSQL] Stack trace: {exInner.StackTrace}");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[TruongBPMuahang/ThemPhieumuahangSQL] Lỗi gửi email báo giá: {ex.Message}");
                        Debug.WriteLine($"[TruongBPMuahang/ThemPhieumuahangSQL] Stack trace: {ex.StackTrace}");
                    }
                }
                else
                {
                    Debug.WriteLine($"[TruongBPMuahang/ThemPhieumuahangSQL] Không gửi email - updatedCount: {updatedCount}");
                }

                string message = updatedCount > 0 
                    ? $"Đã cập nhật báo giá cho {updatedCount} vật tư thành công!" 
                    : "Không có dữ liệu nào được cập nhật.";

                return Json(new { success = true, message = message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult CapNhatNgayThanhToan(string MaMuahang, string MaSanpham, string? NgayThanhToan)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(MaMuahang) || string.IsNullOrWhiteSpace(MaSanpham))
                {
                    return Json(new { success = false, message = "Thiếu mã mua hàng hoặc mã vật tư." });
                }

                var vt = _context.vtphieumuahang
                    .FirstOrDefault(v => v.MaMuahang == MaMuahang && v.MaSanpham == MaSanpham);

                if (vt == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy vật tư trong phiếu mua hàng." });
                }

                // Lưu vào trường riêng cho BP Mua hàng
                if (string.IsNullOrWhiteSpace(NgayThanhToan))
                {
                    vt.NgayThanhToanBPMuahang = null;
                }
                else
                {
                    if (DateTime.TryParse(NgayThanhToan, out var dt))
                    {
                        vt.NgayThanhToanBPMuahang = dt;
                    }
                    else
                    {
                        return Json(new { success = false, message = "Định dạng ngày thanh toán không hợp lệ." });
                    }
                }

                _context.SaveChanges();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult CapNhatNgayCoHang(string MaMuahang, string MaSanpham, string? NgayCoHang)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(MaMuahang) || string.IsNullOrWhiteSpace(MaSanpham))
                {
                    return Json(new { success = false, message = "Thiếu mã mua hàng hoặc mã vật tư." });
                }

                var vt = _context.vtphieumuahang
                    .FirstOrDefault(v => v.MaMuahang == MaMuahang && v.MaSanpham == MaSanpham);

                if (vt == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy vật tư trong phiếu mua hàng." });
                }

                // Lưu ngày có hàng vào vtphieumuahang
                DateTime? ngayCoHangValue = null;
                if (string.IsNullOrWhiteSpace(NgayCoHang))
                {
                    vt.NgayCoHang = null;
                }
                else
                {
                    if (DateTime.TryParse(NgayCoHang, out var dt))
                    {
                        vt.NgayCoHang = dt;
                        ngayCoHangValue = dt;
                    }
                    else
                    {
                        return Json(new { success = false, message = "Định dạng ngày có hàng không hợp lệ." });
                    }
                }

                // Đồng bộ Ngày có hàng sang vtyeucau để hiển thị ở bảng chi tiết yêu cầu
                if (!string.IsNullOrWhiteSpace(vt.MaYeucau))
                {
                    var vtyeucauList = _context.vtyeucau
                        .Where(v => v.VTMaYeucau == vt.MaYeucau && v.MaSanpham == vt.MaSanpham)
                        .ToList();
                    foreach (var vty in vtyeucauList)
                    {
                        vty.NgayCoHang = ngayCoHangValue;
                    }
                }

                _context.SaveChanges();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult CapNhatGhiChuPhieumuahang(string MaMuahang, string MaSanpham, string? GhiChu)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(MaMuahang) || string.IsNullOrWhiteSpace(MaSanpham))
                {
                    return Json(new { success = false, message = "Thiếu mã mua hàng hoặc mã vật tư." });
                }

                var vt = _context.vtphieumuahang
                    .FirstOrDefault(v => v.MaMuahang == MaMuahang && v.MaSanpham == MaSanpham);

                if (vt == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy vật tư trong phiếu mua hàng." });
                }

                // Lưu vào trường riêng cho BP Mua hàng
                vt.GhiChuBPMuahang = string.IsNullOrWhiteSpace(GhiChu) ? null : GhiChu.Trim();

                _context.SaveChanges();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult CapNhatNhaCCPhieumuahang(string MaMuahang, string MaSanpham, string? NhaCC)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(MaMuahang) || string.IsNullOrWhiteSpace(MaSanpham))
                {
                    return Json(new { success = false, message = "Thiếu mã mua hàng hoặc mã vật tư." });
                }

                // Chỉ cho phép BP mua hàng cập nhật
                var boPhan = HttpContext.Session.GetString("Bophan");
                if (!string.Equals(boPhan, "BP mua hàng", StringComparison.OrdinalIgnoreCase))
                {
                    return Json(new { success = false, message = "Bạn không có quyền cập nhật nhà cung cấp." });
                }

                var vt = _context.vtphieumuahang
                    .FirstOrDefault(v => v.MaMuahang == MaMuahang && v.MaSanpham == MaSanpham);

                if (vt == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy vật tư trong phiếu mua hàng." });
                }

                vt.NhaCC = string.IsNullOrWhiteSpace(NhaCC) ? null : NhaCC.Trim();

                // Đồng bộ NCC sang các bảng liên quan để các areas khác hiển thị ngay
                if (!string.IsNullOrWhiteSpace(vt.MaYeucau))
                {
                    var maYc = vt.MaYeucau;
                    var maSp = vt.MaSanpham;
                    var ncc = vt.NhaCC;

                    var vtyeucauList = _context.vtyeucau
                        .Where(v => v.VTMaYeucau == maYc && v.MaSanpham == maSp)
                        .ToList();
                    foreach (var vty in vtyeucauList)
                    {
                        vty.NhaCC = ncc;
                    }

                    var vtxuatList = _context.vtphieuxuatkho
                        .Where(v => v.MaYeucau == maYc && v.MaSanpham == maSp)
                        .ToList();
                    foreach (var vtx in vtxuatList)
                    {
                        vtx.NhaCC = ncc;
                    }

                    var vtnhapList = _context.vtphieunhapkho
                        .Where(v => v.MaYeucau == maYc && v.MaSanpham == maSp)
                        .ToList();
                    foreach (var vtn in vtnhapList)
                    {
                        vtn.NhaCC = ncc;
                    }
                }

                _context.SaveChanges();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult XuLyPhieumuahang(string MaMuahang, string action, phieumuahang phieumuahang, vtphieumuahang vtphieumuahang, phieunhapkho phieunhapkho, vtphieunhapkho vtphieunhapkho)
        {
            var chucVu2 = HttpContext.Session.GetString("Chucvu");
            var boPhan2 = HttpContext.Session.GetString("Bophan");
            var maNv2 = HttpContext.Session.GetString("MaNguoidung");

            if (action == "approve")
            {
                Console.WriteLine($"MaMuahang nhận được: {MaMuahang}");
                var Phieumuahang = _context.phieumuahang.FirstOrDefault(p => p.MaMuahang == MaMuahang);
                var VTPhieumuahanglist = _context.vtphieumuahang.Where(vt => vt.MaMuahang == MaMuahang).ToList();
                if (chucVu2 == "Giám đốc")
                {
                    Phieumuahang.TrangThai = "Chờ thanh toán";
                }
                else if (boPhan2 == "BP kế toán")
                {
                    Phieumuahang.TrangThai = "Đã thanh toán";
                }
                else if (boPhan2 == "BP mua hàng")
                {
                    // Lưu trạng thái hiện tại trước khi thay đổi
                    var trangThaiHienTai = Phieumuahang.TrangThai;
                    
                    // Nếu trạng thái hiện tại là "Chờ thanh toán" → duyệt thanh toán
                    if (trangThaiHienTai == "Chờ thanh toán")
                    {
                        Phieumuahang.TrangThai = "Đã thanh toán";
                    }
                    else
                    {
                        // Các trường hợp khác → nhận hàng
                        Phieumuahang.TrangThai = "Đã nhận hàng";
                        // Lưu thời gian mua hàng khi bộ phận mua hàng nhận hàng
                        Phieumuahang.NgayMuahang = DateTime.Now;
                        Taophieunhapkhobyphieumuahang(MaMuahang, phieunhapkho, vtphieunhapkho, phieumuahang, vtphieumuahang);
                    }
                }
                foreach (var VTPhieumuahang in VTPhieumuahanglist)
                {
                    // Chỉ cập nhật trạng thái các mục đã báo giá (có đơn giá)
                    // Các mục chưa báo giá giữ nguyên trạng thái "Đang chờ báo giá"
                    if (VTPhieumuahang.TrangThai == "Đã báo giá" && VTPhieumuahang.DonGia != null && VTPhieumuahang.DonGia > 0)
                    {
                        if (chucVu2 == "Giám đốc")
                        {
                            VTPhieumuahang.TrangThai = "Chờ thanh toán";
                        }
                        else if (boPhan2 == "BP kế toán")
                        {
                            VTPhieumuahang.TrangThai = "Đã thanh toán";
                        }
                        else if (boPhan2 == "BP mua hàng")
                        {
                            // Lấy trạng thái phiếu sau khi đã cập nhật
                            if (Phieumuahang.TrangThai == "Đã thanh toán")
                            {
                                VTPhieumuahang.TrangThai = "Đã thanh toán";
                            }
                            else
                            {
                                VTPhieumuahang.TrangThai = "Đã nhận hàng";
                            }
                        }
                        _context.vtphieumuahang.Update(VTPhieumuahang);
                    }
                    // Cập nhật trạng thái cho các mục có trạng thái "Chờ thanh toán" khi BP mua hàng duyệt thanh toán
                    else if (VTPhieumuahang.TrangThai == "Chờ thanh toán" && boPhan2 == "BP mua hàng" && Phieumuahang.TrangThai == "Đã thanh toán")
                    {
                        VTPhieumuahang.TrangThai = "Đã thanh toán";
                        _context.vtphieumuahang.Update(VTPhieumuahang);
                    }
                }
                _context.phieumuahang.Update(Phieumuahang);
            }
            else if (action == "reject")
            {
                Xulituchoiyeucau(MaMuahang, null, null, phieumuahang, vtphieumuahang, saveChanges: false);
            }
            _context.SaveChanges();
            return RedirectToAction("Phieumuahang", "Yeucau", new { area = "TruongBPMuahang" });
        }

        [HttpPost]
        public IActionResult Taophieunhapkhobyphieumuahang(string MaMuahang, phieunhapkho phieunhapkho, vtphieunhapkho vtphieunhapkho, phieumuahang phieumuahang, vtphieumuahang vtphieumuahang)
        {
            var Phieumuahang = _context.phieumuahang.FirstOrDefault(p => p.MaMuahang == MaMuahang);
            var VTPhieumuahanglist = _context.vtphieumuahang.Where(vt => vt.MaMuahang == MaMuahang).ToList();

            // Tạo mã phiếu nhập kho duy nhất bằng service
            string MaNhapkho = _phieuCodeService.GenerateMaNhapKho(Phieumuahang?.MaDuan, Phieumuahang?.MaYeucau);

            var newphieunhapkho = new phieunhapkho
            {
                MaNhapkho = MaNhapkho,
                MaYeucau = Phieumuahang.MaYeucau,
                MaDuan = Phieumuahang.MaDuan,
                MaNguoidung = Phieumuahang.MaNguoidung,
                NgayNhapkho = DateTime.Now,
                TrangThai = "Chờ nhập kho"
            };
            _context.phieunhapkho.Add(newphieunhapkho);
            _context.SaveChanges();

            foreach (var VTPhieumuahang in VTPhieumuahanglist)
            {
                // Đảm bảo Makho tồn tại trong khotongs trước khi tạo vtphieunhapkho
                var targetMakho = EnsureKhoTongForNhapKho(VTPhieumuahang);

                var newvtphieunhapkho = new vtphieunhapkho
                {
                    MaNhapkho = MaNhapkho,
                    MaYeucau = VTPhieumuahang.MaYeucau,
                    TenSanpham = VTPhieumuahang.TenSanpham,
                    MaSanpham = VTPhieumuahang.MaSanpham,
                    Makho = targetMakho,
                    HangSX = VTPhieumuahang.HangSX,
                    NhaCC = VTPhieumuahang.NhaCC,
                    SL = VTPhieumuahang.SL,
                    DonVi = VTPhieumuahang.DonVi,
                    TrangThai = "Chờ nhập kho",
                };
                _context.vtphieunhapkho.Add(newvtphieunhapkho);
            }
            _context.SaveChanges();

            // Gửi email thông báo cho nhân viên kho khi có phiếu nhập kho mới cần xử lý (dùng scope riêng để tránh DbContext concurrent)
            SendWarehouseNotificationOnNhapKhoAsync(MaNhapkho);

            return RedirectToAction("Phieumuahang", "Yeucau", new { area = "TruongBPMuahang" });
        }

        [HttpGet]
        public IActionResult GetDataByMaYeucau(string mayeucau)
        {
            var yeucau = _context.yeucau
                .FirstOrDefault(y => y.MaYeucau == mayeucau);

            if (yeucau == null) return NotFound();

            // Lấy dữ liệu từ vtyeucau (vật tư yêu cầu gốc) cho ThemPhieunhapkho
            var vtYeucau = _context.vtyeucau
                .Where(v => v.VTMaYeucau == mayeucau)
                .Select(v => new
                {
                    tenSanpham = v.TenSanpham,
                    maSanpham = v.MaSanpham,
                    makho = v.YCMakho,
                    hangSX = v.HangSX,
                    nhaCC = v.NhaCC,
                    sl = v.SL,
                    donVi = v.DonVi
                })
                .ToList();

            return Json(new
            {
                maNguoidung = yeucau.YCMaNguoidung,
                maDuan = yeucau.YCMaDuan,
                vtPhieuMuaHang = vtYeucau  // Trả về dữ liệu từ vtyeucau
            });
        }

        [HttpPost]
        public IActionResult ThemPhieunhapkhoSQL(phieunhapkho phieunhapkho, vtphieunhapkho vtphieunhapkho,
            string[] TenSanpham, string[] MaSanpham, string[] HangSX, string[] NhaCC,
            int[] SL, string[] DonVi, string[] Makho, string LoaiNhapkho, decimal[] DonGia, string[] DiengiaiNhapKho)
        {
            var maNv = HttpContext.Session.GetString("MaNguoidung");
            if (string.IsNullOrEmpty(maNv))
            {
                TempData["Error"] = "Session đã hết hạn. Vui lòng đăng nhập lại!";
                return RedirectToAction("Login", "Home", new { area = "" });
            }

            string currentArea = "TruongBPMuahang";

            try
            {
                if (TenSanpham == null || TenSanpham.Length == 0)
                {
                    TempData["Error"] = "Vui lòng nhập ít nhất một vật tư!";
                    return RedirectToAction("ThemPhieunhapkho", "Yeucau", new { area = currentArea });
                }

                if (string.IsNullOrEmpty(LoaiNhapkho))
                {
                    TempData["Error"] = "Vui lòng chọn loại nhập kho!";
                    return RedirectToAction("ThemPhieunhapkho", "Yeucau", new { area = currentArea });
                }

                if (string.IsNullOrEmpty(phieunhapkho.MaNguoidung))
                {
                    phieunhapkho.MaNguoidung = maNv;
                }

                int count = TenSanpham.Length;

                var validationErrors = new List<string>();
                for (int i = 0; i < count; i++)
                {
                    var maSp = (MaSanpham != null && MaSanpham.Length > i) ? (MaSanpham[i] ?? "") : "";
                    var maKho = (Makho != null && Makho.Length > i) ? (Makho[i] ?? "") : "";
                    int soLuongTra = (SL != null && SL.Length > i) ? (SL[i]) : 0;
                    if (soLuongTra <= 0) continue;

                    int soLuongDaMuon = 0;

                    if (LoaiNhapkho == "duan" && !string.IsNullOrEmpty(phieunhapkho.MaDuan))
                    {
                        soLuongDaMuon = (from vt in _context.vtphieuxuatkho
                                         join px in _context.phieuxuatkho on vt.MaXuatkho equals px.MaXuatkho
                                         where px.MaDuan == phieunhapkho.MaDuan
                                               && (vt.TrangThai == "Đã xác nhận nhận hàng"
                                                   || vt.TrangThai == "Đã lấy hàng"
                                                   || vt.TrangThai == "Đã xuất kho")
                                               && (vt.SL ?? 0) > 0
                                               && (maSp == "" || vt.MaSanpham == maSp)
                                               && (maKho == "" || vt.Makho == maKho)
                                         select vt.SL ?? 0).Sum();
                    }
                    else if (LoaiNhapkho == "canhan")
                    {
                        soLuongDaMuon = _context.khonguoidungs
                            .Where(k => k.NDMaNguoidung == phieunhapkho.MaNguoidung
                                        && (k.TrangThai == "Đang mượn" || k.TrangThai == "Đang sử dụng")
                                        && (maSp == "" || k.MaSanpham == maSp))
                            .Select(k => k.SL ?? 0)
                            .Sum();
                    }

                    if (soLuongTra > soLuongDaMuon)
                    {
                        var tenSp = (TenSanpham != null && TenSanpham.Length > i) ? (TenSanpham[i] ?? "") : "";
                        var donVi = (DonVi != null && DonVi.Length > i) ? (DonVi[i] ?? "") : "";
                        validationErrors.Add($"- {tenSp} ({maSp}): Trả {soLuongTra} {donVi}, nhưng chỉ mượn {soLuongDaMuon}.");
                    }
                }

                if (validationErrors.Count > 0)
                {
                    TempData["Error"] = "Số lượng trả vượt quá số lượng đã mượn cho các vật tư sau:\n" + string.Join("\n", validationErrors);
                    return RedirectToAction("ThemPhieunhapkho", "Yeucau", new { area = currentArea });
                }

                // Tạo mã phiếu nhập kho duy nhất bằng service
                phieunhapkho.MaNhapkho = _phieuCodeService.GenerateMaNhapKho(phieunhapkho.MaDuan, phieunhapkho.MaYeucau);
                phieunhapkho.NgayNhapkho = DateTime.Now;

                if (!string.IsNullOrEmpty(phieunhapkho.MaDuan))
                {
                    phieunhapkho.TrangThai = "Chờ quản lý dự án duyệt";
                }
                else
                {
                    phieunhapkho.TrangThai = "Chờ Giám đốc duyệt";
                }

                if (string.IsNullOrEmpty(phieunhapkho.MaYeucau))
                {
                    // Format: MãDựÁnNK YYMMDD-01 hoặc MãNhânViênNK YYMMDD-01 (gọi PhieuCodeService)
                    string maDuanForYc = (LoaiNhapkho == "duan" && !string.IsNullOrEmpty(phieunhapkho.MaDuan)) ? phieunhapkho.MaDuan : null;
                    string maYeucauDacBiet = _phieuCodeService.GenerateMaYeucauNhapKho(maDuanForYc, maNv);

                    string ycMaDuan = null;
                    if (!string.IsNullOrEmpty(phieunhapkho.MaDuan))
                    {
                        var duanExists = _context.duans.FirstOrDefault(d => d.MaDuan == phieunhapkho.MaDuan);
                        if (duanExists == null)
                            duanExists = _context.duans.AsEnumerable().FirstOrDefault(d => d.MaDuan != null && d.MaDuan.Equals(phieunhapkho.MaDuan, StringComparison.OrdinalIgnoreCase));
                        if (duanExists != null) ycMaDuan = duanExists.MaDuan;
                        else { var allDuans = _context.duans.Select(d => d.MaDuan).ToList(); Console.WriteLine($"Warning: Mã dự án '{phieunhapkho.MaDuan}' không tồn tại. Available: {string.Join(", ", allDuans)}"); }
                    }

                    var nguoiDung = _context.nguoidungs.FirstOrDefault(n => n.MaNguoidung == maNv);
                    string tenNguoiDung = nguoiDung?.TenNguoidung ?? "";
                    string boPhanNguoiDung = nguoiDung?.Bophan ?? "";

                    var newYeucauDacBiet = new yeucau
                    {
                        MaYeucau = maYeucauDacBiet,
                        TenYeucau = "Yêu cầu nhập kho",
                        YCMaNguoidung = maNv,
                        NguoiYeucau = tenNguoiDung,
                        Bophan = boPhanNguoiDung,
                        YCMaDuan = ycMaDuan,
                        NgayYeucau = DateTime.Now,
                        TrangThai = (LoaiNhapkho == "duan" && !string.IsNullOrEmpty(phieunhapkho.MaDuan)) ? "Chờ quản lý dự án duyệt" : (LoaiNhapkho == "canhan" ? "Chờ Giám đốc duyệt" : "Đã duyệt")
                    };
                    _context.yeucau.Add(newYeucauDacBiet);
                    _context.SaveChanges();

                    phieunhapkho.MaYeucau = maYeucauDacBiet;
                }

                _context.phieunhapkho.Add(phieunhapkho);
                _context.SaveChanges();

                for (int i = 0; i < count; i++)
                {
                    if (string.IsNullOrEmpty(TenSanpham[i])) continue;

                    decimal donGia = DonGia != null && i < DonGia.Length ? DonGia[i] : 0;
                    int soLuong = (SL != null && i < SL.Length) ? SL[i] : 0;
                    decimal thanhTien = donGia * soLuong;
                    string diengiaiNhapKho = (DiengiaiNhapKho != null && i < DiengiaiNhapKho.Length)
                        ? (DiengiaiNhapKho[i] ?? "Không sử dụng")
                        : "Không sử dụng";

                    var newvtphieunhapkho = new vtphieunhapkho
                    {
                        TenSanpham = TenSanpham[i],
                        MaSanpham = MaSanpham?[i] ?? "",
                        Makho = Makho?[i] ?? "",
                        HangSX = HangSX?[i] ?? "",
                        NhaCC = NhaCC?[i] ?? "",
                        SL = soLuong,
                        DonVi = DonVi?[i] ?? "",
                        DiengiaiNhapKho = diengiaiNhapKho,
                        DonGia = donGia,
                        ThanhTien = thanhTien,
                        TrangThai = phieunhapkho.TrangThai,
                        MaNhapkho = phieunhapkho.MaNhapkho,
                        MaYeucau = phieunhapkho.MaYeucau
                    };

                    _context.vtphieunhapkho.Add(newvtphieunhapkho);
                }

                _context.SaveChanges();

                // Gửi email thông báo theo luồng duyệt phiếu nhập kho
                try
                {
                    var maNhapkhoForEmail = phieunhapkho.MaNhapkho;
                    _ = System.Threading.Tasks.Task.Run(async () =>
                    {
                        try
                        {
                            await _emailService.SendNotificationOnNhapKhoCreatedAsync(maNhapkhoForEmail);
                        }
                        catch (Exception exInner)
                        {
                            System.Diagnostics.Debug.WriteLine($"[TruongBPMuahang/ThemPhieunhapkhoSQL] Lỗi gửi email tạo phiếu nhập kho: {exInner.Message}");
                        }
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TruongBPMuahang/ThemPhieunhapkhoSQL] Lỗi khởi chạy task gửi email: {ex.Message}");
                }

                TempData["Success"] = "Tạo phiếu nhập kho thành công!";
                return RedirectToAction("Phieunhapkho", "Yeucau", new { area = currentArea });
            }
            catch (Exception ex)
            {
                Console.WriteLine("==========================================");
                Console.WriteLine($"ERROR in ThemPhieunhapkhoSQL: {ex.Message}");
                Console.WriteLine($"Exception Type: {ex.GetType().Name}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                    Console.WriteLine($"Inner Stack trace: {ex.InnerException.StackTrace}");
                }
                Console.WriteLine("==========================================");

                var maNvCheck = HttpContext.Session.GetString("MaNguoidung") ?? maNv;
                Console.WriteLine($"Session MaNguoidung after error: {maNvCheck ?? "NULL"}");
                Console.WriteLine($"Original maNv (from before try): {maNv ?? "NULL"}");

                TempData["Error"] = $"Có lỗi xảy ra khi xử lý: {ex.Message}. Vui lòng kiểm tra lại dữ liệu hoặc liên hệ admin.";

                if (!string.IsNullOrEmpty(maNv))
                {
                    return RedirectToAction("ThemPhieunhapkho", "Yeucau", new { area = currentArea });
                }
                else
                {
                    TempData["Error"] = "Session đã hết hạn. Vui lòng đăng nhập lại!";
                    return RedirectToAction("Login", "Home", new { area = "" });
                }
            }
        }

        [HttpPost]
        public IActionResult Xuliphieunhapkho(
                                string MaNhapkho, string action,
                                phieuxuatkho phieunhapkho,
                                vtphieuxuatkho vtphieunhapkho, phieuxuatkho phieuxuatkho, vtphieuxuatkho vtphieuxuatkho)
        {
            var chucVu2 = HttpContext.Session.GetString("Chucvu");
            var boPhan2 = HttpContext.Session.GetString("Bophan");
            var maNv2 = HttpContext.Session.GetString("MaNguoidung");

            if (action == "approve")
            {
                var Phieunhapkho = _context.phieunhapkho.FirstOrDefault(p => p.MaNhapkho == MaNhapkho);
                var VTPhieunhapkholist = _context.vtphieunhapkho.Where(vt => vt.MaNhapkho == MaNhapkho).ToList();
                // Chỉ cần 1 lần ấn để xác nhận nhập kho và cập nhật tồn kho
                if (boPhan2 == "BP kho" && Phieunhapkho.TrangThai == "Chờ nhập kho")
                {
                    Phieunhapkho.TrangThai = "Đã nhập kho";
                    
                    // Cập nhật tồn kho khi nhập hàng
                    foreach (var VTPhieunhapkho in VTPhieunhapkholist)
                    {
                        // Tìm vật tư trong tồn kho
                        var khotong = _context.khotongs.FirstOrDefault(k => 
                            k.TenSanpham == VTPhieunhapkho.TenSanpham && 
                            k.MaSanpham == VTPhieunhapkho.MaSanpham && 
                            k.HangSX == VTPhieunhapkho.HangSX &&
                            k.Makho == VTPhieunhapkho.Makho);
                            
                        if (khotong != null)
                        {
                            // Cộng số lượng vào tồn kho
                            khotong.SL += VTPhieunhapkho.SL;
                            _context.khotongs.Update(khotong);
                        }
                        else
                        {
                            // Tạo mới vật tư trong tồn kho nếu chưa có
                            var newKhotong = new khotongs
                            {
                                TenSanpham = VTPhieunhapkho.TenSanpham,
                                MaSanpham = VTPhieunhapkho.MaSanpham,
                                HangSX = VTPhieunhapkho.HangSX,
                                NhaCC = VTPhieunhapkho.NhaCC,
                                SL = VTPhieunhapkho.SL,
                                DonVi = VTPhieunhapkho.DonVi,
                                Makho = VTPhieunhapkho.Makho,
                                NgayNhapkho = DateTime.Now,
                                TrangThai = "Tồn kho"
                            };
                            _context.khotongs.Add(newKhotong);
                        }
                    }
                }
                else if (boPhan2 == "BP kho" && Phieunhapkho.TrangThai == "Đã nhập kho")
                {
                    // Hoàn thành phiếu nhập kho
                    Phieunhapkho.TrangThai = "Hoàn thành";
                }

                foreach (var VTPhieunhapkho in VTPhieunhapkholist)
                {
                    var VTPhieunhapkhott = _context.vtphieunhapkho.FirstOrDefault(vt => vt.MaNhapkho == VTPhieunhapkho.MaNhapkho);
                    if (boPhan2 == "BP kho" && Phieunhapkho.TrangThai == "Đã nhập kho")
                    {
                        VTPhieunhapkhott.TrangThai = "Đã nhập kho";
                    }
                    else if (boPhan2 == "BP kho" && Phieunhapkho.TrangThai == "Hoàn thành")
                    {
                        VTPhieunhapkhott.TrangThai = "Hoàn thành";
                    }
                    _context.vtphieunhapkho.Update(VTPhieunhapkhott);
                }
                _context.phieunhapkho.Update(Phieunhapkho);
                
                // Gửi email thông báo cho người yêu cầu khi nhập kho xong
                if (Phieunhapkho.TrangThai == "Đã nhập kho")
                {
                    try
                    {
                        _ = _emailService.SendNotificationToRequesterOnNhapKhoAsync(MaNhapkho);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[TruongBPMuahang/Xuliphieunhapkho] Lỗi gửi email nhập kho cho người yêu cầu: {ex.Message}");
                    }
                }
            }
            else if (action == "reject")
            {
                var Phieunhapkho = _context.phieunhapkho.FirstOrDefault(p => p.MaNhapkho == MaNhapkho);
                Phieunhapkho.TrangThai = "Đã từ chối";
                _context.phieunhapkho.Update(Phieunhapkho);

            }
            _context.SaveChanges();
            return RedirectToAction("Phieunhapkho", "Yeucau", new { area = "TruongBPMuahang" });
        }

        [HttpPost]
        public IActionResult Taophieuxuatkhobyphieunhapkho(string MaNhapkho, phieuxuatkho phieuxuatkho, vtphieuxuatkho vtphieuxuatkho, phieunhapkho phieunhapkho, vtphieunhapkho vtphieunhapkho)
        {
            var Phieunhapkho = _context.phieunhapkho.FirstOrDefault(p => p.MaNhapkho == MaNhapkho);
            var VTPhieunhapkholist = _context.vtphieunhapkho.Where(vt => vt.MaNhapkho == MaNhapkho).ToList();

            // Tạo mã phiếu xuất kho duy nhất bằng service
            string MaXuatkho = _phieuCodeService.GenerateMaXuatKho(Phieunhapkho?.MaDuan, Phieunhapkho?.MaYeucau);

            var newphieuxuatkho = new phieuxuatkho
            {
                MaXuatkho = MaXuatkho,
                MaYeucau = Phieunhapkho.MaYeucau,
                MaDuan = Phieunhapkho.MaDuan,
                MaNguoidung = Phieunhapkho.MaNguoidung,
                NgayXuatkho = null,
                NgayChuanBi = DateTime.Now,
                TrangThai = "Đang chuẩn bị hàng"
            };
            _context.phieuxuatkho.Add(newphieuxuatkho);
            _context.SaveChanges();

            foreach (var VTPhieunhapkho in VTPhieunhapkholist)
            {
                var newvtphieuxuatkho = new vtphieuxuatkho
                {
                    MaXuatkho = MaXuatkho,
                    MaYeucau = VTPhieunhapkho.MaYeucau,
                    TenSanpham = VTPhieunhapkho.TenSanpham,
                    MaSanpham = VTPhieunhapkho.MaSanpham,
                    Makho = VTPhieunhapkho.Makho,
                    HangSX = VTPhieunhapkho.HangSX,
                    NhaCC = VTPhieunhapkho.NhaCC,
                    SL = VTPhieunhapkho.SL,
                    DonVi = VTPhieunhapkho.DonVi,
                    TrangThai = "Đang chuẩn bị hàng",
                };
                _context.vtphieuxuatkho.Add(newvtphieuxuatkho);
            }
            _context.SaveChanges();

            return RedirectToAction("Phieunhapkho", "Yeucau", new { area = "TruongBPMuahang" });
        }

        [HttpPost]
        public IActionResult Xulituchoiyeucau(
                        string Ma,
                        yeucau yeucau,
                        vtyeucau vtyeucau,
                        phieumuahang phieumuahang,
                        vtphieumuahang vtphieumuahang,
                        bool saveChanges = true)
        {
            var chucVu = HttpContext.Session.GetString("Chucvu");
            var boPhan = HttpContext.Session.GetString("Bophan");
            var maNv = HttpContext.Session.GetString("MaNguoidung");

            // Không dựa vào tiền tố "PMH" nữa vì mã phiếu đã được chuẩn hoá theo service
            // (vd: <maDuAn/MaNV>MH yyMMdd[-01])
            bool isPhieuMuaHang = _context.phieumuahang.Any(p => p.MaMuahang == Ma);
            if (!isPhieuMuaHang)
            {
                var Phieu = _context.yeucau.FirstOrDefault(p => p.MaYeucau == Ma);
                if (Phieu != null)
                {
                    Phieu.TrangThai = $"{chucVu} - Đã từ chối";
                    _context.yeucau.Update(Phieu);

                    var Listvtyeucau = _context.vtyeucau.Where(p => p.VTMaYeucau == Ma).ToList();
                    foreach (var VTyeucau in Listvtyeucau)
                    {
                        VTyeucau.TrangThai = $"{chucVu} - Đã từ chối";
                        _context.vtyeucau.Update(VTyeucau);
                    }

                    if (saveChanges)
                    {
                        _context.SaveChanges();
                    }
                }
            }
            else
            {
                var Phieumuahang = _context.phieumuahang.FirstOrDefault(p => p.MaMuahang == Ma);
                if (Phieumuahang != null)
                {
                    Phieumuahang.TrangThai = $"{chucVu} - Đã từ chối";
                    _context.phieumuahang.Update(Phieumuahang);

                    var Listvtmuahang = _context.vtphieumuahang.Where(p => p.MaMuahang == Ma).ToList();
                    foreach (var VTmuahang in Listvtmuahang)
                    {
                        VTmuahang.TrangThai = $"{chucVu} - Đã từ chối";
                        _context.vtphieumuahang.Update(VTmuahang);
                    }
                    if (saveChanges)
                    {
                        _context.SaveChanges();
                    }
                }
            }

            var refererUrl = HttpContext.Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(refererUrl))
            {
                return Redirect(refererUrl);
            }

            return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPMuahang" });
        }

        public IActionResult XacnhanNhanHang()
        {
            var currentUserId = HttpContext.Session.GetString("MaNguoidung");

            // Lấy các yêu cầu mà kỹ thuật viên này đã tạo
            var yeuCauList = _context.yeucau
                .Where(y => y.YCMaNguoidung == currentUserId)
                .Select(y => y.MaYeucau)
                .ToList();

            // Lấy phiếu xuất kho liên quan tới các yêu cầu đó
            // Hiển thị cả phiếu đang chờ xác nhận và đã xác nhận
            var PhieuxuatkhoList = _context.phieuxuatkho
                .Where(p => yeuCauList.Contains(p.MaYeucau)
                         && (p.TrangThai == "Chờ người yêu cầu xác nhận" 
                             || p.TrangThai == "Đã xác nhận nhận hàng"))
                .OrderByDescending(p => p.NgayXuatkho)
                .ToList();

            var VTphieuxuatkhoList = _context.vtphieuxuatkho.ToList();

            var model = new Phieuxuatkhoviewmodel
            {
                Phieuxuatkho = PhieuxuatkhoList,
                VTphieuxuatkho = VTphieuxuatkhoList,
            };

            return View(model);
        }

        // Helper method: Tính số lượng hàng đã cam kết (committed) từ các phiếu xuất đã duyệt nhưng chưa giao
        // Các trạng thái được tính: "Đang chuẩn bị hàng", "Chờ người yêu cầu xác nhận"
        // LƯU Ý: "Đã xác nhận nhận hàng" KHÔNG tính vì đã trừ kho rồi
        private int TinhSoLuongDaCamKet(string makho, string masanpham, string maXuatkhoHienTai = null)
        {
            // Lấy tất cả các phiếu xuất có trạng thái đã duyệt nhưng chưa giao (chưa trừ kho)
            var cacTrangThaiDaCamKet = new[] { "Đang chuẩn bị hàng", "Chờ người yêu cầu xác nhận" };
            
            var phieuXuatDaCamKet = _context.phieuxuatkho
                .Where(px => cacTrangThaiDaCamKet.Contains(px.TrangThai))
                .Select(px => px.MaXuatkho)
                .ToList();

            // Nếu có phiếu xuất hiện tại, loại trừ nó khỏi danh sách (vì đang kiểm tra cho chính nó)
            if (!string.IsNullOrEmpty(maXuatkhoHienTai))
            {
                phieuXuatDaCamKet = phieuXuatDaCamKet
                    .Where(mx => mx != maXuatkhoHienTai)
                    .ToList();
            }

            // Tính tổng số lượng vật tư đã cam kết từ các phiếu xuất này
            // Ưu tiên khớp chính xác Makho; nếu không có thì cho phép khớp linh hoạt theo tiền tố/hậu tố
            var tongSoLuongDaCamKet = _context.vtphieuxuatkho
                .Where(vt => phieuXuatDaCamKet.Contains(vt.MaXuatkho)
                    && vt.MaSanpham == masanpham
                    && (
                        vt.Makho == makho
                        || ((vt.Makho ?? "") != "" && (makho ?? "") != "" &&
                            (((vt.Makho ?? "").StartsWith(makho ?? "")) || ((makho ?? "").StartsWith(vt.Makho ?? ""))))
                    ))
                .Sum(vt => vt.SL ?? 0);

            return tongSoLuongDaCamKet;
        }

        [HttpPost]
        public IActionResult XacnhanNhanHang(string MaXuatkho)
        {
            var phieu = _context.phieuxuatkho.FirstOrDefault(p => p.MaXuatkho == MaXuatkho);

            if (phieu != null && phieu.TrangThai == "Chờ người yêu cầu xác nhận")
            {
                phieu.TrangThai = "Đã xác nhận nhận hàng";
                phieu.NgayXacNhanNhan = DateTime.Now;
                _context.phieuxuatkho.Update(phieu);

                // ✅ Cập nhật trạng thái vật tư trong phiếu xuất kho
                var VTphieuxuatkhoList = _context.vtphieuxuatkho
                    .Where(vt => vt.MaXuatkho == MaXuatkho)
                    .ToList();

                foreach (var vt in VTphieuxuatkhoList)
                {
                    // Bỏ qua các dòng đã được xác nhận hoặc đã xuất kho trước đó
                    if (vt.TrangThai == "Đã xác nhận nhận hàng" || vt.TrangThai == "Đã xuất kho")
                    {
                        continue;
                    }

                    // Cập nhật trạng thái vật tư thành "Đã xác nhận nhận hàng"
                    vt.TrangThai = "Đã xác nhận nhận hàng";
                    vt.NgayNhapkho = DateTime.Now;
                    _context.vtphieuxuatkho.Update(vt);
                    
                    // Trừ kho tổng khi xác nhận nhận hàng - KIỂM TRA CHẶT CHẼ SỐ LƯỢNG
                    // Chỉ xử lý nếu số lượng yêu cầu > 0
                    if ((vt.SL ?? 0) > 0)
                    {
                        var khotong = _context.khotongs.FirstOrDefault(k => k.Makho == vt.Makho && k.MaSanpham == vt.MaSanpham);
                        if (khotong != null)
                        {
                            // Tính số lượng hàng đã cam kết từ các phiếu xuất khác (loại trừ phiếu hiện tại vì nó đang được xác nhận)
                            int soLuongDaCamKetKhac = TinhSoLuongDaCamKet(vt.Makho ?? "", vt.MaSanpham ?? "", MaXuatkho);
                            
                            // Số lượng khả dụng = Tồn kho - Số lượng đã cam kết từ các phiếu khác
                            int soLuongKhaDung = (khotong.SL ?? 0) - soLuongDaCamKetKhac;
                            
                            // TUYỆT ĐỐI KHÔNG cho phép xuất nếu hết hàng hoặc không đủ số lượng
                            if (soLuongKhaDung <= 0 || soLuongKhaDung < vt.SL)
                            {
                                TempData["ErrorMessage"] = $"Không thể xuất kho: Vật tư {vt.TenSanpham} không đủ số lượng trong kho (Tồn kho: {khotong.SL}, Đã cam kết: {soLuongDaCamKetKhac}, Khả dụng: {soLuongKhaDung}, Yêu cầu: {vt.SL})";
                                return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "TruongBPMuahang" });
                            }
                            
                            khotong.SL -= vt.SL;
                            _context.khotongs.Update(khotong);
                        }
                        else
                        {
                            TempData["ErrorMessage"] = $"Không thể xuất kho: Vật tư {vt.TenSanpham} không tồn tại trong kho";
                            return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "TruongBPMuahang" });
                        }
                    }
                    
                    // chỉ xử lý nếu phiếu này không có dự án
                    if (string.IsNullOrEmpty(phieu.MaDuan))
                    {
                        var existingItem = _context.khonguoidungs
                            .FirstOrDefault(k => k.NDMaNguoidung == phieu.MaNguoidung && k.MaSanpham == vt.MaSanpham);

                        if (existingItem != null)
                        {
                            existingItem.SL += vt.SL;
                            _context.khonguoidungs.Update(existingItem);
                        }
                        else
                        {
                            var newItem = new khonguoidungs
                            {
                                NDMaNguoidung = phieu.MaNguoidung,
                                TenSanpham = vt.TenSanpham,
                                MaSanpham = vt.MaSanpham,
                                NDMakho = vt.Makho,
                                HangSX = vt.HangSX,
                                NhaCC = vt.NhaCC,
                                DonVi = vt.DonVi,
                                SL = vt.SL,
                                NgayBaohanh = vt.NgayBaohanh,
                                ThoiGianBH = vt.ThoiGianBH,
                                TrangThai = "Đang mượn",
                                NgayNhapkho = DateTime.Now
                            };
                            _context.khonguoidungs.Add(newItem);
                        }
                    }
                }

                _context.SaveChanges();

                // Gửi email thông báo cho người yêu cầu khi đã xác nhận nhận hàng
                try
                {
                    if (!string.IsNullOrEmpty(phieu.MaYeucau))
                    {
                        _ = _emailService.SendNotificationToRequesterOnIssueAsync(
                            phieu.MaYeucau,
                            MaXuatkho
                        );
                        
                        // Gửi email thông báo cho bộ phận kho khi xuất kho
                        _ = _emailService.SendNotificationToWarehouseOnXuatKhoAsync(
                            MaXuatkho,
                            phieu.MaYeucau
                        );
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TruongBPMuahang/XacnhanNhanHang] Lỗi gửi email nhận hàng: {ex.Message}");
                }

                TempData["SuccessMessage"] = "Xác nhận nhận hàng thành công!";
                return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "TruongBPMuahang" });
            }

            TempData["ErrorMessage"] = "Phiếu không hợp lệ hoặc đã được xác nhận!";
            return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "TruongBPMuahang" });
        }

        [HttpPost]
        public IActionResult DongsBoTrangThaiVatTu(string MaXuatkho)
        {
            try
            {
                var phieu = _context.phieuxuatkho.FirstOrDefault(p => p.MaXuatkho == MaXuatkho);
                
                if (phieu != null && phieu.TrangThai == "Đã xác nhận nhận hàng")
                {
                    var VTphieuxuatkhoList = _context.vtphieuxuatkho
                        .Where(vt => vt.MaXuatkho == MaXuatkho)
                        .ToList();

                    foreach (var vt in VTphieuxuatkhoList)
                    {
                        // Chỉ cập nhật nếu trạng thái chưa đúng
                        if (vt.TrangThai != "Đã xác nhận nhận hàng" && vt.TrangThai != "Đã xuất kho")
                        {
                            vt.TrangThai = "Đã xác nhận nhận hàng";
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
        public IActionResult AutoDongBoTrangThai()
        {
            try
            {
                var currentUserId = HttpContext.Session.GetString("MaNguoidung");
                
                // Lấy các yêu cầu mà kỹ thuật viên này đã tạo
                var yeuCauList = _context.yeucau
                    .Where(y => y.YCMaNguoidung == currentUserId)
                    .Select(y => y.MaYeucau)
                    .ToList();

                // Lấy các phiếu đã xác nhận nhận hàng
                var phieuxuatkhoList = _context.phieuxuatkho
                    .Where(p => yeuCauList.Contains(p.MaYeucau)
                             && p.TrangThai == "Đã xác nhận nhận hàng")
                    .ToList();

                int updatedCount = 0;
                foreach (var phieu in phieuxuatkhoList)
                {
                    var VTphieuxuatkhoList = _context.vtphieuxuatkho
                        .Where(vt => vt.MaXuatkho == phieu.MaXuatkho
                                 && vt.TrangThai != "Đã xác nhận nhận hàng"
                                 && vt.TrangThai != "Đã xuất kho")
                        .ToList();

                    foreach (var vt in VTphieuxuatkhoList)
                    {
                        vt.TrangThai = "Đã xác nhận nhận hàng";
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

        // In phiếu mua hàng
        [HttpGet]
        public IActionResult InPhieumuahang(string MaMuahang)
        {
            if (string.IsNullOrEmpty(MaMuahang))
            {
                return NotFound();
            }

            var phieumuahang = _context.phieumuahang
                .FirstOrDefault(p => p.MaMuahang == MaMuahang);

            if (phieumuahang == null)
            {
                return NotFound();
            }

            var vtphieumuahang = _context.vtphieumuahang
                .Where(vt => vt.MaMuahang == MaMuahang)
                .ToList();

            var yeucau = _context.yeucau
                .FirstOrDefault(y => y.MaYeucau == phieumuahang.MaYeucau);

            ViewBag.Phieumuahang = phieumuahang;
            ViewBag.VTPhieumuahang = vtphieumuahang;
            ViewBag.Yeucau = yeucau;

            return View();
        }

        // Xuất Excel phiếu mua hàng (định dạng tiếng Việt)
        [HttpGet]
        public IActionResult ExportPhieumuahangExcel(string MaMuahang)
        {
            if (string.IsNullOrEmpty(MaMuahang))
            {
                return NotFound();
            }

            var phieumuahang = _context.phieumuahang
                .FirstOrDefault(p => p.MaMuahang == MaMuahang);

            if (phieumuahang == null)
            {
                return NotFound();
            }

            var vtphieumuahang = _context.vtphieumuahang
                .Where(vt => vt.MaMuahang == MaMuahang)
                .ToList();

            var yeucau = _context.yeucau
                .FirstOrDefault(y => y.MaYeucau == phieumuahang.MaYeucau);

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Phiếu mua hàng");

                // Tiêu đề phiếu
                worksheet.Cells[1, 1, 1, 12].Merge = true;
                worksheet.Cells[1, 1].Value = $"PHIẾU MUA HÀNG - {phieumuahang.MaMuahang}";
                worksheet.Cells[1, 1].Style.Font.Bold = true;
                worksheet.Cells[1, 1].Style.Font.Size = 14;
                worksheet.Cells[1, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                worksheet.Cells[1, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                worksheet.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(68, 114, 196));
                worksheet.Cells[1, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                worksheet.Row(1).Height = 25;

                // Thông tin phiếu
                int infoRow = 2;
                worksheet.Cells[infoRow, 1].Value = "Mã mua hàng:"; worksheet.Cells[infoRow, 2].Value = phieumuahang.MaMuahang ?? "";
                infoRow++;
                worksheet.Cells[infoRow, 1].Value = "Mã yêu cầu:"; worksheet.Cells[infoRow, 2].Value = phieumuahang.MaYeucau ?? "";
                infoRow++;
                worksheet.Cells[infoRow, 1].Value = "Người yêu cầu:"; worksheet.Cells[infoRow, 2].Value = yeucau?.NguoiYeucau ?? (phieumuahang.MaNguoidung ?? "");
                infoRow++;
                worksheet.Cells[infoRow, 1].Value = "Ngày mua hàng:"; worksheet.Cells[infoRow, 2].Value = phieumuahang.NgayMuahang?.ToString("dd/MM/yyyy") ?? "";
                infoRow++;
                worksheet.Cells[infoRow, 1].Value = "Trạng thái:"; worksheet.Cells[infoRow, 2].Value = phieumuahang.TrangThai ?? "";
                infoRow += 2;

                // Header bảng chi tiết
                int headerRow = infoRow;
                worksheet.Cells[headerRow, 1].Value = "TT";
                worksheet.Cells[headerRow, 2].Value = "Tên thiết bị/hàng hóa";
                worksheet.Cells[headerRow, 3].Value = "Mã VT";
                worksheet.Cells[headerRow, 4].Value = "Hãng SX";
                worksheet.Cells[headerRow, 5].Value = "Nhà cung cấp";
                worksheet.Cells[headerRow, 6].Value = "Số lượng";
                worksheet.Cells[headerRow, 7].Value = "Đơn vị";
                worksheet.Cells[headerRow, 8].Value = "Đơn giá";
                worksheet.Cells[headerRow, 9].Value = "Thành tiền";
                worksheet.Cells[headerRow, 10].Value = "Ngày thanh toán";
                worksheet.Cells[headerRow, 11].Value = "Ngày có hàng";
                worksheet.Cells[headerRow, 12].Value = "Ghi chú";
                worksheet.Cells[headerRow, 13].Value = "Trạng thái";

                using (var range = worksheet.Cells[headerRow, 1, headerRow, 13])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(173, 216, 230));
                    range.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    range.Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    range.Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    range.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    range.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                }

                // Định dạng tiền VND cho cột Đơn giá (8) và Thành tiền (9)
                var vndFormat = "#,##0 \"đ\"";

                int row = headerRow + 1;
                int stt = 1;
                foreach (var vt in vtphieumuahang)
                {
                    worksheet.Cells[row, 1].Value = stt;
                    worksheet.Cells[row, 2].Value = vt.TenSanpham ?? "";
                    worksheet.Cells[row, 3].Value = vt.MaSanpham ?? "";
                    worksheet.Cells[row, 4].Value = vt.HangSX ?? "";
                    worksheet.Cells[row, 5].Value = vt.NhaCC ?? "";
                    worksheet.Cells[row, 6].Value = vt.SL ?? 0;
                    worksheet.Cells[row, 7].Value = vt.DonVi ?? "";
                    worksheet.Cells[row, 8].Value = vt.DonGia ?? 0;
                    worksheet.Cells[row, 8].Style.Numberformat.Format = vndFormat;
                    worksheet.Cells[row, 9].Value = vt.ThanhTien ?? 0;
                    worksheet.Cells[row, 9].Style.Numberformat.Format = vndFormat;
                    worksheet.Cells[row, 10].Value = vt.NgayThanhToan?.ToString("dd/MM/yyyy") ?? "";
                    worksheet.Cells[row, 11].Value = vt.NgayCoHang?.ToString("dd/MM/yyyy") ?? "";
                    worksheet.Cells[row, 12].Value = vt.GhiChu ?? "";
                    worksheet.Cells[row, 13].Value = vt.TrangThai ?? "";

                    using (var range = worksheet.Cells[row, 1, row, 13])
                    {
                        range.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                        range.Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                        range.Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                        range.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    }

                    row++;
                    stt++;
                }

                worksheet.Cells.AutoFitColumns();
                var excelBytes = package.GetAsByteArray();
                var fileName = $"Phieu_mua_hang_{phieumuahang.MaMuahang}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

                return File(excelBytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
        }

        /// <summary>
        /// Trang so sánh nhiều phiếu mua hàng: gộp theo Tên thiết bị + Mã VT, hiển thị SL theo từng yêu cầu, bôi màu ô SL chênh.
        /// </summary>
        [HttpGet]
        public IActionResult SoSanhPhieumuahang(string ids)
        {
            if (string.IsNullOrWhiteSpace(ids))
            {
                return NotFound();
            }
            var idList = ids.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .ToList();
            if (idList.Count == 0)
            {
                return NotFound();
            }

            var phieuList = _context.phieumuahang
                .Where(p => idList.Contains(p.MaMuahang))
                .OrderBy(p => p.MaYeucau)
                .Select(p => new PhieuSoSanhItem { MaMuahang = p.MaMuahang, MaYeucau = p.MaYeucau ?? p.MaMuahang })
                .ToList();
            if (phieuList.Count == 0)
            {
                return NotFound();
            }
            var maMuahangOrder = phieuList.Select(x => x.MaMuahang).ToList();

            var allVt = _context.vtphieumuahang
                .Where(v => idList.Contains(v.MaMuahang))
                .ToList();

            // Gộp theo (TenSanpham, MaSanpham) - lấy từ dòng đầu, SL theo từng phiếu
            var grouped = allVt
                .GroupBy(v => new { Ten = v.TenSanpham ?? "", Ma = v.MaSanpham ?? "" })
                .Select(g =>
                {
                    var first = g.First();
                    var slByPhieu = new List<int?>();
                    var slChenh = new List<bool>();
                    var slValues = new List<int?>();
                    foreach (var ma in maMuahangOrder)
                    {
                        var vt = g.FirstOrDefault(x => x.MaMuahang == ma);
                        var sl = vt?.SL;
                        slValues.Add(sl);
                        slByPhieu.Add(sl);
                    }
                    var distinctSl = slValues.Where(s => s.HasValue).Select(s => s.Value).Distinct().ToList();
                    bool hasChenh = distinctSl.Count > 1;
                    // Khi có chênh: bôi màu tất cả ô SL có giá trị để dễ nhận biết
                    for (int i = 0; i < slValues.Count; i++)
                    {
                        slChenh.Add(hasChenh && slValues[i].HasValue);
                    }
                    var hangSXValues = new List<string>();
                    var nhaCCValues = new List<string>();
                    foreach (var ma in maMuahangOrder)
                    {
                        var vt = g.FirstOrDefault(x => x.MaMuahang == ma);
                        hangSXValues.Add(vt?.HangSX ?? "");
                        nhaCCValues.Add(vt?.NhaCC ?? "");
                    }
                    var distinctHangSX = hangSXValues.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
                    var distinctNhaCC = nhaCCValues.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
                    string hangSXDisplay = distinctHangSX.Count <= 1 ? (distinctHangSX.FirstOrDefault() ?? "") : string.Join(" / ", distinctHangSX);
                    string nhaCCDisplay = distinctNhaCC.Count <= 1 ? (distinctNhaCC.FirstOrDefault() ?? "") : string.Join(" / ", distinctNhaCC);
                    return new SoSanhRowViewModel
                    {
                        TenSanpham = first.TenSanpham ?? "",
                        MaSanpham = first.MaSanpham ?? "",
                        HangSX = hangSXDisplay,
                        HangSXChenh = distinctHangSX.Count > 1,
                        NhaCC = nhaCCDisplay,
                        NhaCCChenh = distinctNhaCC.Count > 1,
                        HangSXValues = hangSXValues,
                        NhaCCValues = nhaCCValues,
                        DonVi = first.DonVi ?? "",
                        SlValues = slValues,
                        SlChenh = slChenh
                    };
                })
                .OrderBy(r => r.TenSanpham).ThenBy(r => r.MaSanpham)
                .ToList();

            var model = new SoSanhPhieumuahangViewModel
            {
                PhieuList = phieuList,
                Rows = grouped
            };
            ViewBag.IdsQuery = ids;
            return View(model);
        }

        /// <summary>
        /// Xuất Excel so sánh nhiều phiếu: gộp theo Tên TB + Mã VT, cột SL theo từng yêu cầu, tô màu ô SL chênh.
        /// </summary>
        [HttpGet]
        public IActionResult ExportSoSanhPhieumuahangExcel(string ids)
        {
            if (string.IsNullOrWhiteSpace(ids))
            {
                return NotFound();
            }
            var idList = ids.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .ToList();
            if (idList.Count == 0)
            {
                return NotFound();
            }

            var phieuList = _context.phieumuahang
                .Where(p => idList.Contains(p.MaMuahang))
                .OrderBy(p => p.MaYeucau)
                .Select(p => new { p.MaMuahang, p.MaYeucau })
                .ToList();
            if (phieuList.Count == 0)
            {
                return NotFound();
            }
            var maMuahangOrder = phieuList.Select(x => x.MaMuahang).ToList();

            var allVt = _context.vtphieumuahang
                .Where(v => idList.Contains(v.MaMuahang))
                .ToList();

            var grouped = allVt
                .GroupBy(v => new { Ten = v.TenSanpham ?? "", Ma = v.MaSanpham ?? "" })
                .Select(g =>
                {
                    var first = g.First();
                    var slValues = new List<int?>();
                    var hangSXValues = new List<string>();
                    var nhaCCValues = new List<string>();
                    foreach (var ma in maMuahangOrder)
                    {
                        var vt = g.FirstOrDefault(x => x.MaMuahang == ma);
                        slValues.Add(vt?.SL);
                        hangSXValues.Add(vt?.HangSX ?? "");
                        nhaCCValues.Add(vt?.NhaCC ?? "");
                    }
                    var distinctSl = slValues.Where(s => s.HasValue).Select(s => s.Value).Distinct().ToList();
                    bool hasChenh = distinctSl.Count > 1;
                    var referenceSl = slValues.FirstOrDefault(s => s.HasValue);
                    var slChenh = slValues.Select((s, i) => hasChenh && s.HasValue && s != referenceSl).ToList();
                    var distinctHangSX = hangSXValues.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
                    var distinctNhaCC = nhaCCValues.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList();
                    string hangSXDisplay = distinctHangSX.Count <= 1 ? (distinctHangSX.FirstOrDefault() ?? "") : string.Join(" / ", distinctHangSX);
                    string nhaCCDisplay = distinctNhaCC.Count <= 1 ? (distinctNhaCC.FirstOrDefault() ?? "") : string.Join(" / ", distinctNhaCC);
                    return new { first, slValues, slChenh, hangSXDisplay, nhaCCDisplay };
                })
                .OrderBy(x => x.first.TenSanpham).ThenBy(x => x.first.MaSanpham)
                .ToList();

            using (var package = new ExcelPackage())
            {
                var ws = package.Workbook.Worksheets.Add("So sánh phiếu mua hàng");
                int col = 1;
                ws.Cells[1, col].Value = "TT"; col++;
                ws.Cells[1, col].Value = "Tên thiết bị/hàng hóa"; col++;
                ws.Cells[1, col].Value = "Mã VT"; col++;
                ws.Cells[1, col].Value = "Hãng SX"; col++;
                ws.Cells[1, col].Value = "Nhà CC"; col++;
                ws.Cells[1, col].Value = "ĐV"; col++;
                foreach (var p in phieuList)
                {
                    ws.Cells[1, col].Value = "SL (" + (p.MaYeucau ?? p.MaMuahang) + ")";
                    col++;
                }
                ws.Cells[1, col].Value = "Tổng";
                col++;
                int headerRow = 1;
                using (var range = ws.Cells[headerRow, 1, headerRow, col - 1])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(68, 114, 196));
                    range.Style.Font.Color.SetColor(System.Drawing.Color.White);
                }

                int row = 2;
                int stt = 1;
                foreach (var g in grouped)
                {
                    col = 1;
                    ws.Cells[row, col].Value = stt; col++;
                    ws.Cells[row, col].Value = g.first.TenSanpham ?? ""; col++;
                    ws.Cells[row, col].Value = g.first.MaSanpham ?? ""; col++;
                    ws.Cells[row, col].Value = g.hangSXDisplay; col++;
                    ws.Cells[row, col].Value = g.nhaCCDisplay; col++;
                    ws.Cells[row, col].Value = g.first.DonVi ?? ""; col++;
                    for (int i = 0; i < g.slValues.Count; i++)
                    {
                        ws.Cells[row, col].Value = g.slValues[i] ?? (object)"";
                        if (g.slChenh[i])
                        {
                            ws.Cells[row, col].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                            ws.Cells[row, col].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(255, 255, 0));
                        }
                        col++;
                    }
                    int tongRowVal = g.slValues.Sum(s => s ?? 0);
                    ws.Cells[row, col].Value = tongRowVal;
                    col++;
                    row++;
                    stt++;
                }

                int totalRow = row;
                int totalColCount = 6 + phieuList.Count + 1;
                ws.Cells[totalRow, 1].Value = "Tổng số lượng vật tư";
                ws.Cells[totalRow, 1].Style.Font.Bold = true;
                ws.Cells[totalRow, totalColCount].Value = grouped.Sum(x => x.slValues.Sum(s => s ?? 0));
                ws.Cells[totalRow, totalColCount].Style.Font.Bold = true;
                using (var totalRange = ws.Cells[totalRow, 1, totalRow, totalColCount])
                {
                    totalRange.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    totalRange.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(227, 242, 253));
                }

                ws.Cells.AutoFitColumns();
                var excelBytes = package.GetAsByteArray();
                var fileName = $"So_sanh_phieu_mua_hang_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                return File(excelBytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    fileName);
            }
        }

        private string EnsureKhoTongForNhapKho(vtphieumuahang vtPhieumuahang)
        {
            var requestedMakho = NormalizeMakhoValue(vtPhieumuahang);
            var existingKho = _context.khotongs.FirstOrDefault(k => k.Makho == requestedMakho);

            if (existingKho == null)
            {
                var newKhoTong = new khotongs
                {
                    Makho = requestedMakho,
                    TenSanpham = vtPhieumuahang.TenSanpham,
                    MaSanpham = vtPhieumuahang.MaSanpham,
                    HangSX = vtPhieumuahang.HangSX,
                    NhaCC = vtPhieumuahang.NhaCC,
                    DonVi = vtPhieumuahang.DonVi,
                    SL = 0,
                    NgayNhapkho = DateTime.Now,
                    TrangThai = "Chờ nhập kho",
                    LoaiCapPhat = "Kho tổng"
                };
                _context.khotongs.Add(newKhoTong);
                _context.SaveChanges(); // Lưu ngay để đảm bảo Makho tồn tại khi tạo vtphieunhapkho
            }

            // Đảm bảo requestedMakho tồn tại trong khotongs trước khi sử dụng
            var verifiedKho = _context.khotongs.FirstOrDefault(k => k.Makho == requestedMakho);
            if (verifiedKho == null)
            {
                // Nếu không tồn tại, tạo lại
                var newKhoTong = new khotongs
                {
                    Makho = requestedMakho,
                    TenSanpham = vtPhieumuahang.TenSanpham,
                    MaSanpham = vtPhieumuahang.MaSanpham,
                    HangSX = vtPhieumuahang.HangSX,
                    NhaCC = vtPhieumuahang.NhaCC,
                    DonVi = vtPhieumuahang.DonVi,
                    SL = 0,
                    NgayNhapkho = DateTime.Now,
                    TrangThai = "Chờ nhập kho",
                    LoaiCapPhat = "Kho tổng"
                };
                _context.khotongs.Add(newKhoTong);
                _context.SaveChanges();
            }

            // Đảm bảo vtyeucau có YCMakho tương ứng với requestedMakho
            if (!string.IsNullOrEmpty(vtPhieumuahang.MaYeucau) && !string.IsNullOrEmpty(requestedMakho))
            {
                // Tìm vtyeucau tương ứng dựa trên MaYeucau và thông tin sản phẩm
                var vtyeucauList = _context.vtyeucau
                    .Where(vt => vt.VTMaYeucau == vtPhieumuahang.MaYeucau
                        && vt.TenSanpham == vtPhieumuahang.TenSanpham
                        && vt.MaSanpham == vtPhieumuahang.MaSanpham)
                    .ToList();

                foreach (var vtyeucau in vtyeucauList)
                {
                    // Cập nhật YCMakho để khớp với requestedMakho
                    if (vtyeucau.YCMakho != requestedMakho)
                    {
                        vtyeucau.YCMakho = requestedMakho;
                        _context.vtyeucau.Update(vtyeucau);
                    }
                }

                // Nếu không tìm thấy vtyeucau tương ứng, tạo mới
                if (!vtyeucauList.Any())
                {
                    var newVtyeucau = new vtyeucau
                    {
                        VTMaYeucau = vtPhieumuahang.MaYeucau,
                        TenSanpham = vtPhieumuahang.TenSanpham,
                        MaSanpham = vtPhieumuahang.MaSanpham,
                        YCMakho = requestedMakho,
                        HangSX = vtPhieumuahang.HangSX,
                        NhaCC = vtPhieumuahang.NhaCC,
                        DonVi = vtPhieumuahang.DonVi,
                        SL = vtPhieumuahang.SL,
                        TrangThai = "Đang mua hàng"
                    };
                    _context.vtyeucau.Add(newVtyeucau);
                }

                _context.SaveChanges(); // Lưu để đảm bảo YCMakho tồn tại trước khi cập nhật vtphieumuahang
            }

            if (!string.Equals(vtPhieumuahang.Makho, requestedMakho, StringComparison.Ordinal))
            {
                vtPhieumuahang.Makho = requestedMakho;
                _context.vtphieumuahang.Update(vtPhieumuahang);
                _context.SaveChanges();
            }

            return requestedMakho;
        }

        private string NormalizeMakhoValue(vtphieumuahang vtPhieumuahang)
        {
            var makho = vtPhieumuahang.Makho;
            if (!string.IsNullOrWhiteSpace(makho) && !makho.Equals("VT mới", StringComparison.OrdinalIgnoreCase))
            {
                return makho.Trim();
            }

            return GenerateUniqueMakho(vtPhieumuahang);
        }

        private string GenerateUniqueMakho(vtphieumuahang vtPhieumuahang)
        {
            string Sanitize(string? value, string fallback)
            {
                var raw = string.IsNullOrWhiteSpace(value) ? fallback : value;
                var cleaned = new string(raw.Where(char.IsLetterOrDigit).ToArray());
                return string.IsNullOrWhiteSpace(cleaned) ? fallback : cleaned.ToUpper();
            }

            var maSp = Sanitize(vtPhieumuahang.MaSanpham, "VT");
            var hangSx = Sanitize(vtPhieumuahang.HangSX, "HSX");
            var ngayNhap = (vtPhieumuahang.NgayNhapkho ?? DateTime.Now).ToString("yyyyMMdd");
            var baseCode = $"{maSp}-{hangSx}-{ngayNhap}";

            var candidate = baseCode;
            var suffix = 1;
            while (_context.khotongs.Any(k => k.Makho == candidate))
            {
                candidate = $"{baseCode}-{suffix:D2}";
                suffix++;
            }

            return candidate;
        }

        // In phiếu trả hàng
        [HttpGet]
        public IActionResult InPhietrahang(string MaNhapkho)
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
