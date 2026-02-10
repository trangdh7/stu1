using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Webkho_20241021.Areas.TruongBPKythuat.Data;
using Webkho_20241021.Models;
using Webkho_20241021.Services;
using Webkho_20241021.Helpers;
using OfficeOpenXml;
using Microsoft.Extensions.DependencyInjection;


namespace Webkho_20241021.Areas.TruongBPKythuat.Controllers
{
    [Area("TruongBPKythuat")]
    [Authorize(Roles = "Trưởng BP-BP kỹ thuật")]
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
                        System.Diagnostics.Debug.WriteLine($"[TruongBPKythuat] Bắt đầu gửi email từ chối cho {maYeucau}");
                        await emailService.SendNotificationToRequesterOnRejectionAsync(maYeucau, ghiChu);
                        System.Diagnostics.Debug.WriteLine($"[TruongBPKythuat] Đã gửi email từ chối cho {maYeucau}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TruongBPKythuat] Lỗi gửi email từ chối cho {maYeucau}: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[TruongBPKythuat] Stack trace: {ex.StackTrace}");
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
                .OrderByDescending(y => y.TrangThai != null && y.TrangThai.Trim().Equals("Chờ Trưởng BP-BP kỹ thuật duyệt", StringComparison.OrdinalIgnoreCase))
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
        /// Xóa yêu cầu khi Trưởng BP kỹ thuật chưa duyệt hoặc đã duyệt nhưng QLDA/Giám đốc chưa duyệt.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult XoaYeucau(string MaYeucau)
        {
            if (string.IsNullOrWhiteSpace(MaYeucau))
            {
                TempData["ErrorMessage"] = "Mã yêu cầu không hợp lệ.";
                return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPKythuat" });
            }
            var chucVu = HttpContext.Session.GetString("Chucvu");
            var boPhan = HttpContext.Session.GetString("Bophan");
            var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
            if (yeucau == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy yêu cầu.";
                return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPKythuat" });
            }
            if (!YeucauDeleteHelper.CoTheXoaYeucauTruongBP(yeucau, chucVu ?? "", boPhan ?? ""))
            {
                TempData["ErrorMessage"] = "Bạn chỉ được xóa yêu cầu khi chưa đến bước QLDA/Giám đốc duyệt.";
                return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPKythuat" });
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
            return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPKythuat" });
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
            var Duanslist = _context.duans.ToList();
            var model = new Phieunhapkhoviewmodel
            {
                Phieunhapkho = Phieunhapkholist,
                VTphieunhapkho = VTphieunhapkholist,
                Duans = Duanslist
            };
            ViewBag.Search = search;
            return View(model);
        }

        public IActionResult Phieumuahang(string search = "")
        {
            var Phieumuahanglist = _context.phieumuahang
            .OrderByDescending(y => y.NgayMuahang)
            .ToList();
            // Gán tên Người yêu cầu cho từng phiếu mua hàng
            var nguoiDungDict = _context.nguoidungs.ToDictionary(n => n.MaNguoidung, n => n.TenNguoidung);
            // Lấy Ngày cần từ bảng vtyeucau (vật tư chi tiết) - lấy ngày sớm nhất
            var vtyeucauDict = _context.vtyeucau
                .Where(v => v.NgayCanHang != null)
                .GroupBy(v => v.VTMaYeucau)
                .ToDictionary(g => g.Key, g => g.Min(v => v.NgayCanHang));
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
                thongbaomuahangcount = _context.phieumuahang.Count(p => p.TrangThai == "Đang chờ báo giá");
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
            int Duyetyeucaucount = _context.yeucau.Count(p => p.TrangThai == ("Chờ Trưởng BP-" + boPhan + " duyệt"));
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

            // Nếu yêu cầu đã có dòng chi tiết trong vtyeucau
            // thì luôn ưu tiên hiển thị danh sách đó (yêu cầu vật tư gốc),
            // kể cả khi đã phát sinh phiếu nhập kho.
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
                    // Thiếu = Yêu cầu - Đã xuất (khi đã xuất đủ thì thiếu = 0)
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
                    var slThieu = Math.Max(0, slMoi - tonKho);
                    var isDaXuatKho = (v.TrangThai ?? "").IndexOf("Đã xuất kho", StringComparison.OrdinalIgnoreCase) >= 0;
                    var slDaXuat = isDaXuatKho ? (v.SL ?? v.SLMoi) : (int?)null;
                    var (ngayCoHangDisplay, ghiChuConLai) = GhiChuExportHelper.ParseGhiChuForExport(v.GhiChu, v.NgayCoHang);
                    exportRows.Add(new { v.TT, v.TenSanpham, v.MaSanpham, v.HangSX, v.NhaCC, v.SLCu, v.SLMoi, SlThieu = slThieu, SlDaXuat = slDaXuat, TonKho = tonKho, v.DonVi, v.NgayCanHang, v.NgayCoHang, NgayCoHangDisplay = ngayCoHangDisplay, v.TrangThai, GhiChu = ghiChuConLai, v.NgayDuyet });
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
                        exportRows.Add(new { TT = (object)stt++, v.TenSanpham, v.MaSanpham, v.HangSX, v.NhaCC, SLCu = (int?)null, SLMoi = v.SL, SlThieu = slThieu, SlDaXuat = (int?)null, TonKho = tonKho, v.DonVi, NgayCanHang = (DateTime?)null, NgayCoHang = (DateTime?)null, NgayCoHangDisplay = "", v.TrangThai, GhiChu = (string?)null, NgayDuyet = (DateTime?)null });
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
                    var ngayCoHangDisp = (r as dynamic)?.NgayCoHangDisplay as string;
                    worksheet.Cells[row, 13].Value = !string.IsNullOrEmpty(ngayCoHangDisp) ? ngayCoHangDisp : (r.NgayCoHang != null ? ((DateTime)r.NgayCoHang).ToString("dd/MM/yyyy") : "");
                    if (!string.IsNullOrEmpty(ngayCoHangDisp) && ngayCoHangDisp.Contains('\n'))
                        worksheet.Cells[row, 13].Style.WrapText = true;
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

            return Json(items);
        }

        [HttpGet]
        public IActionResult GetVTPhieunhapkho(string MaNhapkho)
        {
            var PhieunhapkhoList = _context.vtphieunhapkho
                                 .Where(v => v.MaNhapkho == MaNhapkho).ToList();
            return Json(PhieunhapkhoList);
        }

        [HttpGet]
        public IActionResult GetVTPhieumuahang(string MaMuahang)
        {
            var PhieumuahangList = _context.vtphieumuahang
                                 .Where(v => v.MaMuahang == MaMuahang).ToList();
            return Json(PhieumuahangList);
        }

        [HttpPost]
        public IActionResult XuLyVatTuYeucau(string MaYeucau, string MaSanpham, string action, string? GhiChu = null)
        {
            try
            {
                var vatTu = _context.vtyeucau
                    .FirstOrDefault(v => v.VTMaYeucau == MaYeucau && v.MaSanpham == MaSanpham);

                if (vatTu == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy vật tư." });
                }

                // Kiểm tra xem yêu cầu có mã dự án không
                var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
                bool hasMaDuan = yeucau != null && !string.IsNullOrWhiteSpace(yeucau.YCMaDuan);
                
                // Xác định trạng thái tiếp theo dựa trên việc có mã dự án hay không
                string nextTrangThaiVT = hasMaDuan ? "Chờ quản lý dự án duyệt" : "Chờ giám đốc duyệt";
                string nextTrangThaiYC = hasMaDuan ? "Chờ quản lý dự án duyệt" : "Chờ giám đốc duyệt";

                if (action == "approve")
                {
                    // Khi trưởng phòng duyệt, đặt trạng thái dựa trên việc có mã dự án hay không
                    vatTu.TrangThai = nextTrangThaiVT;
                    vatTu.GhiChu = null; // Xóa ghi chú khi duyệt
                    
                    // Lưu thông tin người duyệt vào bảng yeucau
                    if (yeucau != null)
                    {
                        var maNguoiDuyet = HttpContext.Session.GetString("MaNguoidung");
                        if (!string.IsNullOrWhiteSpace(maNguoiDuyet))
                        {
                            yeucau.NguoiDuyet = maNguoiDuyet;
                            yeucau.NgayDuyet = DateTime.Now;
                            _context.yeucau.Update(yeucau);
                        }
                    }
                }
                else if (action == "reject")
                {
                    vatTu.TrangThai = "Đã từ chối";
                    vatTu.GhiChu = GhiChu; // Lưu ghi chú khi từ chối
                    
                    // Lưu thông tin người từ chối vào bảng yeucau
                    if (yeucau != null)
                    {
                        var maNguoiDuyet = HttpContext.Session.GetString("MaNguoidung");
                        if (!string.IsNullOrWhiteSpace(maNguoiDuyet))
                        {
                            yeucau.NguoiDuyet = maNguoiDuyet;
                            yeucau.NgayDuyet = DateTime.Now;
                            _context.yeucau.Update(yeucau);
                        }
                    }
                }

                _context.vtyeucau.Update(vatTu);
                _context.SaveChanges();

                // Gửi email thông báo từ chối nếu trạng thái yêu cầu là "Đã từ chối"
                if (action == "reject")
                {
                    var yeucauAfterReject = _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
                    if (yeucauAfterReject != null && yeucauAfterReject.TrangThai == "Đã từ chối")
                    {
                        SendRejectionEmailAsync(MaYeucau, GhiChu ?? "");
                    }
                }

                // Cập nhật trạng thái yêu cầu nếu tất cả vật tư đã được duyệt
                if (action == "approve" && yeucau != null)
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
                                                                   v.TrangThai == "Chờ quản lý dự án duyệt");
                    
                    // ===== DEBUG EMAIL (XuLyVatTuYeucau) =====
                    Debug.WriteLine("[XuLyVatTuYeucau] CHECK send mail when approve");
                    Debug.WriteLine($"MaYeucau={MaYeucau}");
                    Debug.WriteLine($"allApprovedByTruongBP={allApprovedByTruongBP}");
                    Debug.WriteLine($"ChucVu={chucVu}, BoPhan={boPhan}");
                    Debug.WriteLine($"HasMaDuan={hasMaDuan}");

                    if (allApprovedByTruongBP && chucVu == "Trưởng BP" && boPhan == "BP kỹ thuật")
                    {
                        yeucau.TrangThai = nextTrangThaiYC;
                        _context.yeucau.Update(yeucau);
                        _context.SaveChanges();

                        // Gửi email thông báo sau khi Trưởng BP kỹ thuật duyệt xong tất cả vật tư
                        try
                        {
                            var maYeucauForEmail = MaYeucau;
                            var nguoiYeuCauForEmail = yeucau.NguoiYeucau ?? "";

                            Debug.WriteLine("[XuLyVatTuYeucau] START Task.Run send approval email");
                            Debug.WriteLine($"MaYeucau={maYeucauForEmail}");
                            Debug.WriteLine($"NguoiYeuCau={nguoiYeuCauForEmail}");
                            Debug.WriteLine($"MaDuan={yeucau.YCMaDuan}");

                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    using (var scope = _serviceScopeFactory.CreateScope())
                                    {
                                        Debug.WriteLine("[XuLyVatTuYeucau] Email scope created");
                                        var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                                        
                                        // Thông báo cho người yêu cầu
                                        if (!string.IsNullOrWhiteSpace(nguoiYeuCauForEmail))
                                        {
                                            var trangThaiThongBao = hasMaDuan
                                                ? "Đã được Trưởng BP-BP kỹ thuật duyệt - chuyển quản lý dự án"
                                                : "Đã được Trưởng BP-BP kỹ thuật duyệt - chờ Giám đốc duyệt";

                                            Debug.WriteLine("[XuLyVatTuYeucau] Send email to requester");
                                            Debug.WriteLine($"To={nguoiYeuCauForEmail}");
                                            Debug.WriteLine($"TrangThaiThongBao={trangThaiThongBao}");

                                            await emailService.SendNotificationToEmployeeAsync(
                                                maYeucauForEmail,
                                                nguoiYeuCauForEmail,
                                                trangThaiThongBao
                                            );
                                        }

                                        // Thông báo cho QLDA (nếu là yêu cầu dự án) hoặc Giám đốc (nếu không có dự án)
                                        if (hasMaDuan && !string.IsNullOrWhiteSpace(yeucau.YCMaDuan))
                                        {
                                            Debug.WriteLine("[XuLyVatTuYeucau] Send email to PROJECT MANAGER");
                                            Debug.WriteLine($"MaDuan={yeucau.YCMaDuan}");
                                            await emailService.SendNotificationToProjectManagerAsync(
                                                maYeucauForEmail,
                                                yeucau.YCMaDuan
                                            );
                                        }
                                        else
                                        {
                                            Debug.WriteLine("[XuLyVatTuYeucau] Send email to DIRECTOR");
                                            await emailService.SendNotificationToDirectorAsync(maYeucauForEmail);
                                        }
                                    }
                                }
                                catch (Exception exInner)
                                {
                                    Debug.WriteLine("[XuLyVatTuYeucau][ERROR] Send approval email FAILED");
                                    Debug.WriteLine($"Message={exInner.Message}");
                                    Debug.WriteLine($"StackTrace={exInner.StackTrace}");
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("[XuLyVatTuYeucau][ERROR] Init Task.Run send approval email FAILED");
                            Debug.WriteLine($"Message={ex.Message}");
                            Debug.WriteLine($"StackTrace={ex.StackTrace}");
                        }
                    }
                }

                return Json(new { success = true, message = action == "approve" ? "Đã duyệt vật tư thành công." : "Đã từ chối vật tư.", ghiChu = vatTu.GhiChu });
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
                string nextTrangThaiYC = hasMaDuan ? "Chờ quản lý dự án duyệt" : "Chờ giám đốc duyệt";

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

                    // Helper function để kiểm tra xem vật tư có đang chờ Trưởng BP kỹ thuật duyệt không
                    Func<string, bool> isAwaitingTruongBPStatus = status =>
                    {
                        if (string.IsNullOrWhiteSpace(status))
                        {
                            return true;
                        }
                        var normalized = status.Trim();
                        return normalized.Equals("Chờ Trưởng BP-BP kỹ thuật duyệt", StringComparison.OrdinalIgnoreCase)
                            || normalized.StartsWith("Chờ Trưởng BP", StringComparison.OrdinalIgnoreCase)
                            || normalized.Contains("chờ trưởng bp", StringComparison.OrdinalIgnoreCase)
                            || normalized.Equals("Chờ duyệt", StringComparison.OrdinalIgnoreCase);
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

                    // Chỉ xử lý các vật tư đang chờ Trưởng BP kỹ thuật duyệt và chưa được duyệt/từ chối
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
                    
                    
                    var allVatTu = _context.vtyeucau.Where(v => v.VTMaYeucau == MaYeucau).ToList();
                    
                    
                    var allApprovedByTruongBP = allVatTu.All(v => 
                    {
                        if (string.IsNullOrWhiteSpace(v.TrangThai))
                            return false;
                        
                        var normalized = v.TrangThai.Trim();
                        
                        // Loại trừ các trạng thái chờ duyệt
                        if (normalized.Equals("Chờ Trưởng BP-BP kỹ thuật duyệt", StringComparison.OrdinalIgnoreCase) ||
                            normalized.Contains("chờ trưởng bp", StringComparison.OrdinalIgnoreCase))
                            return false;
                        
                        // Chấp nhận các trạng thái đã được xử lý
                        return normalized == nextTrangThaiVT ||
                               normalized == "Đã duyệt" ||
                               normalized == "Đang mua hàng" ||
                               normalized == "Đã xuất kho" ||
                               normalized == "Đã nhận hàng" ||
                               normalized == "Chờ giám đốc duyệt" ||
                               normalized == "Chờ quản lý dự án duyệt" ||
                               normalized == "Hoàn thành" ||
                               normalized.Contains("Đã từ chối", StringComparison.OrdinalIgnoreCase);
                    });

                    // ===== DEBUG EMAIL (XuLyVatTuYeucauWithCheckbox) =====
                    Debug.WriteLine("[XuLyVatTuYeucauWithCheckbox] CHECK send mail when approve");
                    Debug.WriteLine($"MaYeucau={MaYeucau}");
                    Debug.WriteLine($"allApprovedByTruongBP={allApprovedByTruongBP}");
                    Debug.WriteLine($"ChucVu={chucVu}, BoPhan={boPhan}");
                    Debug.WriteLine($"HasMaDuan={hasMaDuan}");
                    
                    if (allApprovedByTruongBP && chucVu == "Trưởng BP" && boPhan == "BP kỹ thuật")
                    {
                        yeucau.TrangThai = nextTrangThaiYC;
                        _context.yeucau.Update(yeucau);
                        _context.SaveChanges();

                        // Sau khi Trưởng BP kỹ thuật duyệt xong TẤT CẢ vật tư bằng checkbox:
                        // - Gửi mail cho người yêu cầu để họ biết yêu cầu đã được duyệt
                        // - Gửi mail cho bước tiếp theo trong quy trình (QLDA hoặc Giám đốc)
                        try
                        {
                            // Lưu các giá trị cần thiết trước khi vào Task.Run
                            var maYeucauForEmail = MaYeucau;
                            var nguoiYeuCauForEmail = yeucau.NguoiYeucau ?? "";
                            var maDuanForEmail = yeucau.YCMaDuan;

                            Debug.WriteLine("[XuLyVatTuYeucauWithCheckbox] START Task.Run send approval email");
                            Debug.WriteLine($"MaYeucau={maYeucauForEmail}");
                            Debug.WriteLine($"NguoiYeuCau={nguoiYeuCauForEmail}");
                            Debug.WriteLine($"MaDuan={maDuanForEmail}");

                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    using (var scope = _serviceScopeFactory.CreateScope())
                                    {
                                        Debug.WriteLine("[XuLyVatTuYeucauWithCheckbox] Email scope created");
                                        var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                                        
                                        // Thông báo cho người yêu cầu
                                        if (!string.IsNullOrWhiteSpace(nguoiYeuCauForEmail))
                                        {
                                            var trangThaiThongBao = hasMaDuan
                                                ? "Đã được Trưởng BP-BP kỹ thuật duyệt - chuyển quản lý dự án"
                                                : "Đã được Trưởng BP-BP kỹ thuật duyệt - chờ Giám đốc duyệt";

                                            Debug.WriteLine("[XuLyVatTuYeucauWithCheckbox] Send email to requester");
                                            Debug.WriteLine($"To={nguoiYeuCauForEmail}");
                                            Debug.WriteLine($"TrangThaiThongBao={trangThaiThongBao}");

                                            await emailService.SendNotificationToEmployeeAsync(
                                                maYeucauForEmail,
                                                nguoiYeuCauForEmail,
                                                trangThaiThongBao
                                            );
                                        }

                                        // Thông báo cho QLDA (nếu là yêu cầu dự án) hoặc Giám đốc (nếu không có dự án)
                                        if (hasMaDuan && !string.IsNullOrWhiteSpace(maDuanForEmail))
                                        {
                                            Debug.WriteLine("[XuLyVatTuYeucauWithCheckbox] Send email to PROJECT MANAGER");
                                            Debug.WriteLine($"MaDuan={maDuanForEmail}");
                                            await emailService.SendNotificationToProjectManagerAsync(
                                                maYeucauForEmail,
                                                maDuanForEmail
                                            );
                                        }
                                        else
                                        {
                                            Debug.WriteLine("[XuLyVatTuYeucauWithCheckbox] Send email to DIRECTOR");
                                            await emailService.SendNotificationToDirectorAsync(maYeucauForEmail);
                                        }
                                    }
                                }
                                catch (Exception exInner)
                                {
                                    Debug.WriteLine("[XuLyVatTuYeucauWithCheckbox][ERROR] Send approval email FAILED");
                                    Debug.WriteLine($"Message={exInner.Message}");
                                    Debug.WriteLine($"StackTrace={exInner.StackTrace}");
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine("[XuLyVatTuYeucauWithCheckbox][ERROR] Init Task.Run send approval email FAILED");
                            Debug.WriteLine($"Message={ex.Message}");
                            Debug.WriteLine($"StackTrace={ex.StackTrace}");
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

        public class BulkActionRequest
        {
            public string MaYeucau { get; set; } = "";
            public string action { get; set; } = "";
            public List<string>? selectedVatTu { get; set; }
            public Dictionary<string, string>? ghiChuList { get; set; }
        }

        [HttpPost]
        public IActionResult XuLyTatCaVatTuYeucau([FromBody] BulkActionRequest requestData)
        {
            try
            {
                if (requestData == null)
                {
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ." });
                }
                
                string MaYeucau = requestData.MaYeucau ?? "";
                string action = requestData.action ?? "";
                List<string>? selectedVatTu = requestData.selectedVatTu;
                Dictionary<string, string>? ghiChuList = requestData.ghiChuList;
                
                // Nếu có danh sách vật tư được chọn, chỉ xử lý những cái đó
                // Nếu không, lấy tất cả (để tương thích với code cũ)
                List<vtyeucau> vatTuList;
                if (selectedVatTu != null && selectedVatTu.Any())
                {
                    vatTuList = _context.vtyeucau
                        .Where(v => v.VTMaYeucau == MaYeucau && selectedVatTu.Contains(v.MaSanpham))
                        .ToList();
                }
                else
                {
                    vatTuList = _context.vtyeucau
                        .Where(v => v.VTMaYeucau == MaYeucau)
                        .ToList();
                }

                if (!vatTuList.Any())
                {
                    return Json(new { success = false, message = "Không tìm thấy vật tư nào." });
                }

                var chucVu = HttpContext.Session.GetString("Chucvu");
                var boPhan = HttpContext.Session.GetString("Bophan");
                
                // Kiểm tra xem yêu cầu có mã dự án không
                var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
                bool hasMaDuan = yeucau != null && !string.IsNullOrWhiteSpace(yeucau.YCMaDuan);
                
                // Xác định trạng thái tiếp theo dựa trên việc có mã dự án hay không
                string nextTrangThaiVT = hasMaDuan ? "Chờ quản lý dự án duyệt" : "Chờ giám đốc duyệt";
                string nextTrangThaiYC = hasMaDuan ? "Chờ quản lý dự án duyệt" : "Chờ giám đốc duyệt";
                
                foreach (var vatTu in vatTuList)
                {
                    if (action == "approve")
                    {
                        // Khi trưởng phòng duyệt, đặt trạng thái dựa trên việc có mã dự án hay không
                        vatTu.TrangThai = nextTrangThaiVT;
                        vatTu.GhiChu = null; // Xóa ghi chú khi duyệt
                        
                        // Lưu thông tin người duyệt vào bảng yeucau (ghi đè mã người duyệt mới nhất)
                        if (yeucau != null)
                        {
                            var maNguoiDuyet = HttpContext.Session.GetString("MaNguoidung");
                            if (!string.IsNullOrWhiteSpace(maNguoiDuyet))
                            {
                                yeucau.NguoiDuyet = maNguoiDuyet;
                                yeucau.NgayDuyet = DateTime.Now;
                                _context.yeucau.Update(yeucau);
                            }
                        }
                    }
                    else if (action == "reject")
                    {
                        vatTu.TrangThai = "Đã từ chối";
                        // Lấy ghi chú từ dictionary nếu có
                        if (ghiChuList != null && ghiChuList.ContainsKey(vatTu.MaSanpham))
                        {
                            vatTu.GhiChu = ghiChuList[vatTu.MaSanpham];
                        }
                        
                        // Lưu thông tin người từ chối vào bảng yeucau (ghi đè mã người duyệt mới nhất)
                        if (yeucau != null)
                        {
                            var maNguoiDuyet = HttpContext.Session.GetString("MaNguoidung");
                            if (!string.IsNullOrWhiteSpace(maNguoiDuyet))
                            {
                                yeucau.NguoiDuyet = maNguoiDuyet;
                                yeucau.NgayDuyet = DateTime.Now;
                                _context.yeucau.Update(yeucau);
                            }
                        }
                    }
                    _context.vtyeucau.Update(vatTu);
                }

                // Lưu thay đổi trạng thái vật tư
                _context.SaveChanges();

                // Cập nhật trạng thái yêu cầu chính nếu Trưởng BP duyệt tất cả vật tư
                if (action == "approve" && yeucau != null)
                {
                    // Lấy lại TẤT CẢ vật tư từ DB sau SaveChanges để có dữ liệu mới nhất
                    var allVatTu = _context.vtyeucau.Where(v => v.VTMaYeucau == MaYeucau).ToList();
                    
                    // Kiểm tra xem TẤT CẢ vật tư (không chỉ các vật tư được chọn) đã được trưởng phòng duyệt chưa
                    var allApprovedByTruongBP = allVatTu.All(v =>
                    {
                        if (string.IsNullOrWhiteSpace(v.TrangThai))
                            return false;
                        
                        var normalized = v.TrangThai.Trim();
                        
                        // Loại trừ các trạng thái chờ duyệt
                        if (normalized.Equals("Chờ Trưởng BP-BP kỹ thuật duyệt", StringComparison.OrdinalIgnoreCase) ||
                            normalized.Contains("chờ trưởng bp", StringComparison.OrdinalIgnoreCase))
                            return false;
                        
                        // Chấp nhận các trạng thái đã được xử lý
                        return normalized == nextTrangThaiVT ||
                               normalized == "Đã duyệt" ||
                               normalized == "Đang mua hàng" ||
                               normalized == "Đã xuất kho" ||
                               normalized == "Đã nhận hàng" ||
                               normalized == "Chờ giám đốc duyệt" ||
                               normalized == "Chờ quản lý dự án duyệt" ||
                               normalized == "Hoàn thành" ||
                               normalized.Contains("Đã từ chối", StringComparison.OrdinalIgnoreCase);
                    });
                    
                    if (allApprovedByTruongBP && chucVu == "Trưởng BP" && boPhan == "BP kỹ thuật")
                    {
                        // Tất cả vật tư đã được trưởng phòng duyệt, cập nhật trạng thái yêu cầu
                        yeucau.TrangThai = nextTrangThaiYC;
                        _context.yeucau.Update(yeucau);
                        _context.SaveChanges();

                        // Gửi email thông báo sau khi Trưởng BP kỹ thuật duyệt xong tất cả vật tư
                        try
                        {
                            var maYeucauForEmail = MaYeucau;
                            var nguoiYeuCauForEmail = yeucau.NguoiYeucau ?? "";
                            var maDuanForEmail = yeucau.YCMaDuan;

                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    using (var scope = _serviceScopeFactory.CreateScope())
                                    {
                                        var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                                        
                                        // Thông báo cho người yêu cầu
                                        if (!string.IsNullOrWhiteSpace(nguoiYeuCauForEmail))
                                        {
                                            var trangThaiThongBao = hasMaDuan
                                                ? "Đã được Trưởng BP-BP kỹ thuật duyệt - chuyển quản lý dự án"
                                                : "Đã được Trưởng BP-BP kỹ thuật duyệt - chờ Giám đốc duyệt";

                                            await emailService.SendNotificationToEmployeeAsync(
                                                maYeucauForEmail,
                                                nguoiYeuCauForEmail,
                                                trangThaiThongBao
                                            );
                                        }

                                        // Thông báo cho QLDA (nếu là yêu cầu dự án) hoặc Giám đốc (nếu không có dự án)
                                        if (hasMaDuan && !string.IsNullOrWhiteSpace(maDuanForEmail))
                                        {
                                            await emailService.SendNotificationToProjectManagerAsync(
                                                maYeucauForEmail,
                                                maDuanForEmail
                                            );
                                        }
                                        else
                                        {
                                            await emailService.SendNotificationToDirectorAsync(maYeucauForEmail);
                                        }
                                    }
                                }
                                catch (Exception exInner)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[TruongBPKythuat/XuLyTatCaVatTuYeucau] Lỗi gửi email: {exInner.Message}");
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[TruongBPKythuat/XuLyTatCaVatTuYeucau] Lỗi khởi tạo email: {ex.Message}");
                        }
                    }
                }
                else if (action == "reject")
                {
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

            // Lấy mã người dùng hiện tại để auto-fill vào form
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
            if (string.IsNullOrWhiteSpace(timkiem))
            {
                return Json(new List<object>());
            }

            var searchTerm = timkiem.Trim().ToLower();
            var results = _context.khotongs
                .Where(k => (k.TenSanpham != null && k.TenSanpham.ToLower().Contains(searchTerm)) ||
                            (k.MaSanpham != null && k.MaSanpham.ToLower().Contains(searchTerm)))
                .Take(10)
                .Select(k => new
                {
                    k.TenSanpham,
                    k.MaSanpham,
                    k.Makho,
                    k.HangSX,
                    k.NhaCC,
                    k.SL,
                    k.DonVi
                })
                .ToList();

            return Json(results);
        }

        [HttpPost]
        public IActionResult ThemyeucauSQL(yeucau yeucau, vtyeucau vtyeucau,
                                           duans duans, phieunhapkho phieunhapkho, vtphieunhapkho vtphieunhapkho, List<string> YCMaKho,
                                           List<string> TenSanpham, List<string> MaSanpham,
                                           List<string> HangSX, List<string> NhaCC, List<int?> SL,
                                           List<int?> SLCu, List<int?> SLMoi, List<string> VTNgayCanHang, List<string> GhiChu,
                                           List<string> DonVi, string MaYeucau, string action, phieuxuatkho phieuxuatkho, vtphieuxuatkho vtphieuxuatkho, phieumuahang phieumuahang, vtphieumuahang vtphieumuahang)
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

                // ===== DEBUG EMAIL (ThemyeucauSQL) =====
                Debug.WriteLine("===== ThemyeucauSQL START =====");
                Debug.WriteLine($"ChucVu={chucVu2}, BoPhan={boPhan2}, MaNV={maNv2}");
                Debug.WriteLine($"TrangThaiYC={yeucau.TrangThai}");
                Debug.WriteLine($"MaDuan={yeucau.YCMaDuan}");

                yeucau.YCMaDuan = yeucau.YCMaDuan?.Trim();
                var duan = string.IsNullOrEmpty(yeucau.YCMaDuan)
                    ? null
                    : _context.duans.FirstOrDefault(d => d.MaDuan == yeucau.YCMaDuan);

                // ================== RÚT GỌN LOGIC TRẠNG THÁI ==================
                bool hasDuan = duan != null;
                bool isGiamDoc = chucVu2 == "Giám đốc";
                bool isTruongBP = chucVu2 == "Trưởng BP";
                bool isNhanVien = chucVu2 == "Nhân viên";
                bool isBPKythuat = boPhan2 == "BP kỹ thuật";
                bool isBPKho = boPhan2 == "BP kho";
                bool isBPMuaHang = boPhan2 == "BP mua hàng";
                bool isBPKeToan = boPhan2 == "BP kế toán";
                bool isTruongBPKythuat = isTruongBP && isBPKythuat;
                bool isQLDA = hasDuan && !string.IsNullOrWhiteSpace(duan?.MaNguoiQLDA) && maNv2 == duan!.MaNguoiQLDA;

                if (isGiamDoc)
                {
                    yeucau.TrangThai = "Đã duyệt";
                }
                else if (isNhanVien)
                {
                    if (isBPKythuat) yeucau.TrangThai = "Chờ Trưởng BP-BP kỹ thuật duyệt";
                    else if (isBPKho) yeucau.TrangThai = "Chờ Trưởng BP-BP kho duyệt";
                    else if (isBPMuaHang) yeucau.TrangThai = "Chờ Trưởng BP-BP mua hàng duyệt";
                    else if (isBPKeToan) yeucau.TrangThai = "Chờ Trưởng BP-BP kế toán duyệt";
                }
                else if (!hasDuan)
                {
                    // Không có dự án: Trưởng BP -> chờ giám đốc duyệt
                    if (isTruongBP)
                    {
                        yeucau.TrangThai = "Chờ giám đốc duyệt";
                    }
                }
                else if (isQLDA)
                {
                    // Có dự án & người tạo là QLDA
                    if (isTruongBPKythuat) yeucau.TrangThai = "Chờ quản lý dự án duyệt";
                    else if (isTruongBP) yeucau.TrangThai = "Giám đốc";
                }
                else
                {
                    // Có dự án & người tạo KHÔNG phải QLDA
                    if (isTruongBPKythuat) yeucau.TrangThai = "Chờ quản lý dự án duyệt";
                    else if (isTruongBP && (isBPKho || isBPMuaHang)) yeucau.TrangThai = "Quản lí dự án";
                }

                // ================== TẠO MÃ YÊU CẦU ĐÚNG CHUẨN ==================

                // Đánh dấu có file Excel hay không (phục vụ logic auto-approve & email phía dưới)
                bool hasExcelFile = Request.Form.Files != null && Request.Form.Files.Any(f =>
                    !string.IsNullOrEmpty(f.FileName) &&
                    (f.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) || f.FileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase)));

                // Sinh mã yêu cầu dùng chung (tách ra service để khỏi phải sửa từng controller)
                yeucau.MaYeucau = _yeucauCodeService.GenerateMaYeucauCommon(
                    yeucau.YCMaDuan,
                    MaSanpham,
                    Request.Form.Files,
                    DateTime.Now);

                // ================================================================

                // Đảm bảo danh sách không null để tránh lỗi khi submit từ Excel
                YCMaKho ??= new List<string>();
                TenSanpham ??= new List<string>();
                MaSanpham ??= new List<string>();
                HangSX ??= new List<string>();
                NhaCC ??= new List<string>();
                SL ??= new List<int?>();
                SLCu ??= new List<int?>();
                SLMoi ??= new List<int?>();
                VTNgayCanHang ??= new List<string>();
                GhiChu ??= new List<string>();
                DonVi ??= new List<string>();

                // Luôn tạo yêu cầu mới
                _context.yeucau.Add(yeucau);
                
                // Nếu có file Excel và là Trưởng BP kỹ thuật, set ngày duyệt (ghi đè mã người duyệt)
                if (hasExcelFile && (chucVu2 == "Trưởng BP" && boPhan2 == "BP kỹ thuật"))
                {
                    var maNguoiDuyet = HttpContext.Session.GetString("MaNguoidung");
                    if (!string.IsNullOrWhiteSpace(maNguoiDuyet))
                    {
                        yeucau.NguoiDuyet = maNguoiDuyet;
                        yeucau.NgayDuyet = DateTime.Now;
                    }
                }
                
                _context.SaveChanges();

                // Gửi thông báo email khi tạo yêu cầu mới (nếu là nhân viên gửi lên Trưởng BP)
                if (chucVu2 == "Nhân viên" && boPhan2 == "BP kỹ thuật" && 
                    (yeucau.TrangThai == "Chờ Trưởng BP-BP kỹ thuật duyệt"))
                {
                    try
                    {
                        Debug.WriteLine("[ThemyeucauSQL] Send mail to Department Head");
                        Debug.WriteLine($"MaYeucau={yeucau.MaYeucau}");
                        Debug.WriteLine($"NguoiYeuCau={yeucau.NguoiYeucau}");
                        Debug.WriteLine($"BoPhan={yeucau.Bophan}");

                        var maYeucauForEmail = yeucau.MaYeucau;
                        var nguoiYeuCauForEmail = yeucau.NguoiYeucau ?? "";
                        var boPhanForEmail = yeucau.Bophan ?? "BP kỹ thuật";

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                Debug.WriteLine("[ThemyeucauSQL] Task.Run START send mail to Department Head");
                                using (var scope = _serviceScopeFactory.CreateScope())
                                {
                                    var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                                    await emailService.SendNotificationToDepartmentHeadAsync(
                                        maYeucauForEmail,
                                        nguoiYeuCauForEmail,
                                        boPhanForEmail
                                    );
                                }
                            }
                            catch (Exception exInner)
                            {
                                Debug.WriteLine("[ThemyeucauSQL][ERROR] Send mail to Department Head FAILED");
                                Debug.WriteLine($"Message={exInner.Message}");
                                Debug.WriteLine($"StackTrace={exInner.StackTrace}");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[ThemyeucauSQL][ERROR] Init send mail to Department Head FAILED");
                        Debug.WriteLine($"Message={ex.Message}");
                        Debug.WriteLine($"StackTrace={ex.StackTrace}");
                    }
                }

                // Gửi thông báo email khi Trưởng BP kỹ thuật tự tạo yêu cầu (gửi lên QLDA hoặc Giám đốc)
                if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kỹ thuật" && 
                    (yeucau.TrangThai == "Chờ quản lý dự án duyệt" || yeucau.TrangThai == "Chờ giám đốc duyệt" || yeucau.TrangThai == "Giám đốc"))
                {
                    try
                    {
                        var maYeucauForEmail = yeucau.MaYeucau;
                        var maDuanForEmail = yeucau.YCMaDuan;
                        bool hasMaDuan = !string.IsNullOrWhiteSpace(maDuanForEmail);

                        Debug.WriteLine("[ThemyeucauSQL] Send mail from Truong BP");
                        Debug.WriteLine($"MaYeucau={yeucau.MaYeucau}");
                        Debug.WriteLine($"HasMaDuan={hasMaDuan}");
                        Debug.WriteLine($"MaDuan={maDuanForEmail}");

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                Debug.WriteLine("[ThemyeucauSQL] Task.Run START send mail Truong BP");
                                using (var scope = _serviceScopeFactory.CreateScope())
                                {
                                    var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                                    
                                    // Thông báo cho QLDA (nếu là yêu cầu dự án) hoặc Giám đốc (nếu không có dự án)
                                    if (hasMaDuan && !string.IsNullOrWhiteSpace(maDuanForEmail))
                                    {
                                        await emailService.SendNotificationToProjectManagerAsync(
                                            maYeucauForEmail,
                                            maDuanForEmail
                                        );
                                    }
                                    else
                                    {
                                        await emailService.SendNotificationToDirectorAsync(maYeucauForEmail);
                                    }
                                }
                            }
                            catch (Exception exInner)
                            {
                                Debug.WriteLine("[ThemyeucauSQL][ERROR] Send mail Truong BP FAILED");
                                Debug.WriteLine($"Message={exInner.Message}");
                                Debug.WriteLine($"StackTrace={exInner.StackTrace}");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("[ThemyeucauSQL][ERROR] Init send mail Truong BP FAILED");
                        Debug.WriteLine($"Message={ex.Message}");
                        Debug.WriteLine($"StackTrace={ex.StackTrace}");
                    }
                }

                var itemCount = new[] { YCMaKho.Count, TenSanpham.Count, MaSanpham.Count, HangSX.Count, NhaCC.Count, SL.Count, SLCu.Count, SLMoi.Count, VTNgayCanHang.Count, GhiChu.Count, DonVi.Count }.Max();

                for (int i = 0; i < itemCount; i++)
                {
                    var tenSanPham = i < TenSanpham.Count ? TenSanpham[i] : null;
                    if (string.IsNullOrWhiteSpace(tenSanPham))
                    {
                        continue;
                    }

                    var maKhoItem = i < YCMaKho.Count ? YCMaKho[i] : null;
                    var maSanPhamItem = i < MaSanpham.Count ? MaSanpham[i] : null;
                    var slCu = (SLCu != null && i < SLCu.Count) ? SLCu[i] : null;
                    var slMoi = (SLMoi != null && i < SLMoi.Count) ? SLMoi[i] : null;

                    // Bỏ qua dòng nếu số lượng mới không nhập (null) hoặc <= 0 (không cần lưu và hiển thị)
                    if (!slMoi.HasValue || slMoi.Value <= 0)
                    {
                        continue;
                    }
                    
                    var ghiChuItem = (GhiChu != null && i < GhiChu.Count) ? GhiChu[i] : null;
                    DateTime? ngayCanHang = null;
                    if (VTNgayCanHang != null && i < VTNgayCanHang.Count && !string.IsNullOrWhiteSpace(VTNgayCanHang[i]))
                    {
                        if (DateTime.TryParse(VTNgayCanHang[i], out var parsedDate))
                        {
                            ngayCanHang = parsedDate;
                        }
                    }

                    // Tìm vật tư yêu cầu hiện có theo MaYeucau + MaSanpham
                    var existingVTYeucau = _context.vtyeucau
                        .FirstOrDefault(vt => vt.VTMaYeucau == yeucau.MaYeucau
                            && string.Equals(vt.MaSanpham, maSanPhamItem, StringComparison.OrdinalIgnoreCase));

                    // Ưu tiên kho gửi lên theo từng dòng, nếu thiếu thì dò theo mã sản phẩm
                    var khoMatch = !string.IsNullOrWhiteSpace(maKhoItem)
                        ? _context.khotongs.FirstOrDefault(p => p.Makho == maKhoItem)
                        : null;
                    if (khoMatch == null && !string.IsNullOrWhiteSpace(maSanPhamItem))
                    {
                        khoMatch = _context.khotongs.FirstOrDefault(p => p.MaSanpham == maSanPhamItem);
                    }
                    if (khoMatch != null)
                    {
                        // Tính số lượng mới (ưu tiên SLMoi, sau đó SLCu, cuối cùng là SL)
                        int slMoiValue = slMoi ?? slCu ?? ((SL != null && i < SL.Count) ? (SL[i] ?? 0) : 0);
                        
                        // existingVTYeucau đã được tính ở đầu vòng lặp (theo MaYeucau + maSanPhamItem)
                        if (existingVTYeucau != null)
                        {
                            // Cập nhật vật tư yêu cầu hiện có
                            existingVTYeucau.TenSanpham = tenSanPham;
                            existingVTYeucau.TT = GetTTAt(i);
                            existingVTYeucau.HangSX = (i < HangSX.Count) ? HangSX[i] : null;
                            existingVTYeucau.NhaCC = (i < NhaCC.Count) ? NhaCC[i] : null;
                            existingVTYeucau.SLCu = slCu;
                            existingVTYeucau.SLMoi = slMoi;
                            existingVTYeucau.SL = slMoi;
                            existingVTYeucau.DonVi = (i < DonVi.Count) ? DonVi[i] : null;
                            existingVTYeucau.YCMakho = khoMatch.Makho;
                            existingVTYeucau.NgayCanHang = ngayCanHang;
                            existingVTYeucau.NgayNhapkho = khoMatch.NgayNhapkho;
                            existingVTYeucau.NgayBaohanh = khoMatch.NgayBaohanh;
                            existingVTYeucau.ThoiGianBH = khoMatch.ThoiGianBH;
                            existingVTYeucau.GhiChu = ghiChuItem;
                            
                            // Xử lý cập nhật theo logic mới
                            int slThieu;
                            var updateResult = YeucauUpdateHelper.XuLyCapNhatYeuCau(
                                _context, 
                                yeucau, 
                                maSanPhamItem, 
                                slMoiValue, 
                                khoMatch.Makho, 
                                out slThieu);
                            
                            if (updateResult.Success)
                            {
                                existingVTYeucau.SL = slMoiValue;
                                if (string.IsNullOrEmpty(existingVTYeucau.TrangThai))
                                {
                                    existingVTYeucau.TrangThai = yeucau.TrangThai;
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
                            newVtyeucau.TenSanpham = tenSanPham;
                            newVtyeucau.MaSanpham = maSanPhamItem;
                            newVtyeucau.HangSX = (i < HangSX.Count) ? HangSX[i] : null;
                            newVtyeucau.NhaCC = (i < NhaCC.Count) ? NhaCC[i] : null;
                            newVtyeucau.SL = slMoiValue;
                            newVtyeucau.SLCu = slCu;
                            newVtyeucau.SLMoi = slMoi;
                            newVtyeucau.DonVi = (i < DonVi.Count) ? DonVi[i] : null;
                            newVtyeucau.YCMakho = khoMatch.Makho;
                            newVtyeucau.NgayCanHang = ngayCanHang;
                            newVtyeucau.NgayNhapkho = khoMatch.NgayNhapkho;
                            newVtyeucau.NgayBaohanh = khoMatch.NgayBaohanh;
                            newVtyeucau.ThoiGianBH = khoMatch.ThoiGianBH;
                            newVtyeucau.TrangThai = yeucau.TrangThai;
                            newVtyeucau.GhiChu = ghiChuItem;
                            _context.vtyeucau.Add(newVtyeucau);
                        }
                    }
                    else
                    {
                        // Tạo bản ghi "VT mới" trong khotongs nếu chưa tồn tại
                        var vtMoiKho = _context.khotongs.FirstOrDefault(p => p.Makho == "VT mới");
                        if (vtMoiKho == null)
                        {
                            vtMoiKho = new khotongs
                            {
                                Makho = "VT mới",
                                TenSanpham = "Vật tư mới",
                                MaSanpham = "",
                                HangSX = "",
                                NhaCC = "",
                                SL = 0,
                                DonVi = "",
                                NgayNhapkho = null,
                                NgayBaohanh = null,
                                ThoiGianBH = null,
                                TrangThai = "VT mới"
                            };
                            _context.khotongs.Add(vtMoiKho);
                            _context.SaveChanges(); // Lưu ngay để đảm bảo Makho tồn tại
                        }

                        // Tính số lượng mới (ưu tiên SLMoi, sau đó SLCu, cuối cùng là SL)
                        int slMoiValue = slMoi ?? slCu ?? ((SL != null && i < SL.Count) ? (SL[i] ?? 0) : 0);
                        
                        // existingVTYeucau đã được tính ở đầu vòng lặp (theo MaYeucau + maSanPhamItem)
                        if (existingVTYeucau != null)
                        {
                            // Cập nhật vật tư yêu cầu hiện có
                            existingVTYeucau.TenSanpham = tenSanPham;
                            existingVTYeucau.TT = GetTTAt(i);
                            existingVTYeucau.HangSX = (i < HangSX.Count) ? HangSX[i] : null;
                            existingVTYeucau.NhaCC = (i < NhaCC.Count) ? NhaCC[i] : null;
                            existingVTYeucau.SLCu = slCu;
                            existingVTYeucau.SLMoi = slMoi;
                            existingVTYeucau.SL = slMoiValue;
                            existingVTYeucau.DonVi = (i < DonVi.Count) ? DonVi[i] : null;
                            existingVTYeucau.YCMakho = "VT mới";
                            existingVTYeucau.NgayCanHang = ngayCanHang;
                            existingVTYeucau.NgayNhapkho = null;
                            existingVTYeucau.NgayBaohanh = null;
                            existingVTYeucau.ThoiGianBH = null;
                            existingVTYeucau.GhiChu = ghiChuItem;
                            
                            // Xử lý cập nhật theo logic mới
                            int slThieu;
                            var updateResult = YeucauUpdateHelper.XuLyCapNhatYeuCau(
                                _context, 
                                yeucau, 
                                maSanPhamItem, 
                                slMoiValue, 
                                "VT mới", 
                                out slThieu);
                            
                            if (updateResult.Success)
                            {
                                if (string.IsNullOrEmpty(existingVTYeucau.TrangThai))
                                {
                                    existingVTYeucau.TrangThai = yeucau.TrangThai;
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
                            newVtyeucau.TenSanpham = tenSanPham;
                            newVtyeucau.MaSanpham = maSanPhamItem;
                            newVtyeucau.HangSX = (i < HangSX.Count) ? HangSX[i] : null;
                            newVtyeucau.NhaCC = (i < NhaCC.Count) ? NhaCC[i] : null;
                            newVtyeucau.SL = slMoiValue;
                            newVtyeucau.SLCu = slCu;
                            newVtyeucau.SLMoi = slMoi;
                            newVtyeucau.DonVi = (i < DonVi.Count) ? DonVi[i] : null;
                            newVtyeucau.YCMakho = "VT mới";
                            newVtyeucau.NgayCanHang = ngayCanHang;
                            newVtyeucau.NgayNhapkho = null;
                            newVtyeucau.NgayBaohanh = null;
                            newVtyeucau.ThoiGianBH = null;
                            newVtyeucau.TrangThai = yeucau.TrangThai;
                            newVtyeucau.GhiChu = ghiChuItem;
                            _context.vtyeucau.Add(newVtyeucau);
                        }
                    }
                    _context.SaveChanges();
                    
                    // Đồng bộ trạng thái yêu cầu dựa trên trạng thái của các vật tư
                    YeucauUpdateHelper.DongBoTrangThaiYeuCau(_context, yeucau.MaYeucau);
                    _context.SaveChanges();
                }
                if (yeucau.TrangThai == "Đã duyệt")
                {
                    Xuliphieuyeucau(yeucau.MaYeucau, phieuxuatkho, vtphieuxuatkho, phieumuahang, vtphieumuahang, yeucau, vtyeucau);
                }
            }
            else
            {
                // Yêu cầu nhập kho:
                // - có dự án -> chờ quản lí dự án duyệt
                // - không có -> chờ giám đốc duyệt
                yeucau.NgayYeucau = DateTime.Now;
                yeucau.YCMaDuan = yeucau.YCMaDuan?.Trim();
                if (!string.IsNullOrWhiteSpace(yeucau.YCMaDuan))
                {
                    yeucau.TrangThai = "Chờ quản lí dự án duyệt";
                }
                else
                {
                    yeucau.TrangThai = "Chờ giám đốc duyệt";
                }

                    // Tạo mã phiếu nhập kho duy nhất bằng service
                    phieunhapkho.MaNhapkho = _phieuCodeService.GenerateMaNhapKho(phieunhapkho.MaDuan, phieunhapkho.MaYeucau);
            }

            return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPKythuat" });

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

            if (action == "approve")
            {
                if (duan != null)
                {
                    string maNguoiQLDA = duan.MaNguoiQLDA;
                    if (maNv2 == maNguoiQLDA)
                    {
                        if (chucVu2 != "Giám đốc")
                        {
                            Yeucau.TrangThai = "Giám đốc";
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
                    if (chucVu2 == "Nhân viên" && boPhan2 == "BP kỹ thuật")
                    {
                        Yeucau.TrangThai = "Trưởng BP-BP kỹ thuật";
                    }
                    else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kỹ thuật")
                    {
                        Yeucau.TrangThai = "Giám đốc";
                        // Gửi thông báo đến Giám đốc sau khi Trưởng BP duyệt (không có dự án)
                        _ = _emailService.SendNotificationToDirectorAsync(Yeucau.MaYeucau);
                    }
                    else if (chucVu2 == "Nhân viên" && boPhan2 == "BP kho")
                    {
                        Yeucau.TrangThai = "Trưởng BP-BP kho";
                    }
                    else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kho")
                    {
                        Yeucau.TrangThai = "Giám đốc";
                        // Gửi thông báo đến Giám đốc sau khi Trưởng BP duyệt (không có dự án)
                        _ = _emailService.SendNotificationToDirectorAsync(Yeucau.MaYeucau);
                    }
                    else if (chucVu2 == "Nhân viên" && boPhan2 == "BP mua hàng")
                    {
                        Yeucau.TrangThai = "Trưởng BP-BP mua hàng";
                    }
                    else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP mua hàng")
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
                Xulituchoiyeucau(MaYeucau, yeucau, vtyeucau, null, null);
            }
            _context.yeucau.Update(Yeucau);
            _context.SaveChanges();

            return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPKythuat" });
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
                return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPKythuat" });
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
            if ((isPhieuMuaHangCreated == true) && (isPhieuXuatKhoCreated = true))
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
            else if (isPhieuXuatKhoCreated = true)
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
                    }
                    else
                    {
                        VTPhieuxuatkho.SL = soLuongKhaDung > 0 ? soLuongKhaDung : 0;
                        var SLThieu = VattuYC.SL - khotong.SL;
                        VattuYC.TrangThai = "Đang mua hàng";
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

                    _context.vtyeucau.Update(VattuYC);
                    // KHÔNG cập nhật khotong ở đây - chỉ cập nhật khi người nhận xác nhận đã nhận hàng
                    _context.Add(VTPhieuxuatkho);
                }
                else
                {
                    VattuYC.TrangThai = "Đang mua hàng";
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

                    _context.vtyeucau.Update(VattuYC);
                    _context.Add(VTPhieumuahang);
                }
            }

            _context.SaveChanges();


            return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPKythuat" });
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
            return RedirectToAction("Phieuxuatkho", "Yeucau", new { area = "TruongBPKythuat" });
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

                Phieumuahang.TrangThai = "Đã báo giá";
                _context.phieumuahang.Update(Phieumuahang);

                var VTPhieumuahanglist = _context.vtphieumuahang
                                                  .Where(kt => kt.MaMuahang == MaMuahang)
                                                  .ToList();

                Console.WriteLine($"Số vật tư được tìm thấy: {VTPhieumuahanglist.Count}");
                Console.WriteLine($"Số lượng phần tử trong VTphieumuahang: {model.VTphieumuahang?.Count ?? 0}");

                for (int i = 0; i < VTPhieumuahanglist.Count; i++)
                {
                    var VTmuahang = VTPhieumuahanglist[i];

                    // Kiểm tra nếu trong model.VTphieumuahang có phần tử tại cùng vị trí
                    if (i < model.VTphieumuahang.Count)
                    {
                        var updatedVTmuahang = model.VTphieumuahang[i];

                        Console.WriteLine($"Cập nhật VTmuahang: {updatedVTmuahang.MaMuahang}");

                        // Cập nhật giá trị DonGia và ThanhTien
                        VTmuahang.DonGia = updatedVTmuahang.DonGia;
                        VTmuahang.ThanhTien = updatedVTmuahang.ThanhTien;

                        Console.WriteLine($"Đơn giá là: {updatedVTmuahang.DonGia}");
                        Console.WriteLine($"Thành tiền là: {updatedVTmuahang.ThanhTien}");

                        VTmuahang.TrangThai = "Đã báo giá";
                        _context.vtphieumuahang.Update(VTmuahang);
                    }
                    else
                    {
                        Console.WriteLine($"Không có dữ liệu tương ứng trong model cho VTmuahang tại index: {i}");
                    }
                }

                _context.SaveChanges();



                return Json(new { success = true, message = "Dữ liệu đã được gửi thành công!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi: {ex.Message}");
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
                    Phieumuahang.TrangThai = "Đã nhận hàng";
                    Taophieunhapkhobyphieumuahang(MaMuahang, phieunhapkho, vtphieunhapkho, phieumuahang, vtphieumuahang);
                }
                foreach (var VTPhieumuahang in VTPhieumuahanglist)
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
                        VTPhieumuahang.TrangThai = "Đã nhận hàng";
                    }
                    _context.vtphieumuahang.Update(VTPhieumuahang);
                }
                _context.phieumuahang.Update(Phieumuahang);
            }
            else if (action == "reject")
            {
                Xulituchoiyeucau(MaMuahang, null, null, phieumuahang, vtphieumuahang);
            }
            _context.SaveChanges();
            return RedirectToAction("Phieumuahang", "Yeucau", new { area = "TruongBPKythuat" });
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
                NgayNhapkho = null, // Để trống, chỉ lưu khi bộ phận kho nhập kho
                TrangThai = "Chờ nhập kho"
            };
            _context.phieunhapkho.Add(newphieunhapkho);
            _context.SaveChanges();

            foreach (var VTPhieumuahang in VTPhieumuahanglist)
            {
                var newvtphieunhapkho = new vtphieunhapkho
                {
                    MaNhapkho = MaNhapkho,
                    MaYeucau = VTPhieumuahang.MaYeucau,
                    TenSanpham = VTPhieumuahang.TenSanpham,
                    MaSanpham = VTPhieumuahang.MaSanpham,
                    Makho = VTPhieumuahang.Makho,
                    HangSX = VTPhieumuahang.HangSX,
                    NhaCC = VTPhieumuahang.NhaCC,
                    SL = VTPhieumuahang.SL,
                    DonVi = VTPhieumuahang.DonVi,
                    TrangThai = "Chờ nhập kho",
                };
                _context.vtphieunhapkho.Add(newvtphieunhapkho);
            }
            _context.SaveChanges();

            return RedirectToAction("Phieumuahang", "Yeucau", new { area = "TruongBPKythuat" });
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

            string currentArea = "TruongBPKythuat";

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
                string MaNhapkho = _phieuCodeService.GenerateMaNhapKho(phieunhapkho.MaDuan, phieunhapkho.MaYeucau);

                phieunhapkho.MaNhapkho = MaNhapkho;
                phieunhapkho.NgayNhapkho = DateTime.Now;

                // Thiết lập trạng thái ban đầu theo quy trình duyệt
                // Nếu có dự án: gửi đến Trưởng dự án
                // Nếu không có dự án (cá nhân): gửi đến Giám đốc
                if (!string.IsNullOrEmpty(phieunhapkho.MaDuan))
                {
                    phieunhapkho.TrangThai = "Chờ quản lý dự án duyệt"; // Trưởng dự án duyệt
                }
                else
                {
                    phieunhapkho.TrangThai = "Chờ giám đốc duyệt"; // Giám đốc duyệt
                }

                if (string.IsNullOrEmpty(phieunhapkho.MaYeucau))
                {
                    // MãDựÁnNK YYMMDD-01 hoặc MãNhânViênNK YYMMDD-01 (PhieuCodeService)
                    string maDuanForYc = (LoaiNhapkho == "duan" && !string.IsNullOrEmpty(phieunhapkho.MaDuan)) ? phieunhapkho.MaDuan : null;
                    string maYeucauDacBiet = _phieuCodeService.GenerateMaYeucauNhapKho(maDuanForYc, maNv);

                    string ycMaDuan = null;
                    if (!string.IsNullOrEmpty(phieunhapkho.MaDuan))
                    {
                        var duanExists = _context.duans.FirstOrDefault(d => d.MaDuan == phieunhapkho.MaDuan);
                        if (duanExists == null) duanExists = _context.duans.AsEnumerable().FirstOrDefault(d => d.MaDuan != null && d.MaDuan.Equals(phieunhapkho.MaDuan, StringComparison.OrdinalIgnoreCase));
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
                        TrangThai = (LoaiNhapkho == "duan" && !string.IsNullOrEmpty(phieunhapkho.MaDuan)) ? "Chờ quản lý dự án duyệt" : (LoaiNhapkho == "canhan" ? "Chờ giám đốc duyệt" : "Đã duyệt")
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
                        MaNhapkho = MaNhapkho,
                        MaYeucau = phieunhapkho.MaYeucau
                    };

                    _context.vtphieunhapkho.Add(newvtphieunhapkho);
                }

                _context.SaveChanges();

                // Gửi email thông báo theo luồng duyệt phiếu nhập kho
                try
                {
                    var maNhapkhoForEmail = MaNhapkho;
                    _ = System.Threading.Tasks.Task.Run(async () =>
                    {
                        try
                        {
                            await _emailService.SendNotificationOnNhapKhoCreatedAsync(maNhapkhoForEmail);
                        }
                        catch (Exception exInner)
                        {
                            System.Diagnostics.Debug.WriteLine($"[TruongBPKythuat/ThemPhieunhapkhoSQL] Lỗi gửi email tạo phiếu nhập kho: {exInner.Message}");
                        }
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TruongBPKythuat/ThemPhieunhapkhoSQL] Lỗi khởi chạy task gửi email: {ex.Message}");
                }

                TempData["Success"] = "Tạo phiếu nhập kho thành công!";
                return RedirectToAction("Phieunhapkho", "Yeucau", new { area = currentArea });
            }
            catch (Exception ex)
            {
                
                if (ex.InnerException != null)
                {
                    
                }
                

                var maNvCheck = HttpContext.Session.GetString("MaNguoidung") ?? maNv;
               

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

            var Phieunhapkho = _context.phieunhapkho.FirstOrDefault(p => p.MaNhapkho == MaNhapkho);
            if (Phieunhapkho == null)
            {
                return NotFound();
            }

            var VTPhieunhapkholist = _context.vtphieunhapkho.Where(vt => vt.MaNhapkho == MaNhapkho).ToList();
            
            // Lấy thông tin dự án (nếu có)
            var duan = !string.IsNullOrEmpty(Phieunhapkho.MaDuan) 
                ? _context.duans.FirstOrDefault(d => d.MaDuan == Phieunhapkho.MaDuan) 
                : null;

            if (action == "approve")
            {
                // Workflow duyệt:
                // 1. "Quản lí dự án" (nếu có dự án) -> Trưởng dự án duyệt -> "Giám đốc"
                // 2. "Giám đốc" -> Giám đốc duyệt -> "Chờ nhập kho"
                // 3. "Chờ nhập kho" -> Kho xử lý -> "Đã nhập kho" và cộng vào kho tổng

                if (Phieunhapkho.TrangThai == "Quản lí dự án")
                {
                    // Trưởng dự án duyệt (kiểm tra nếu user này là QLDA)
                    if (duan != null && duan.MaNguoiQLDA == maNv2)
                    {
                        Phieunhapkho.TrangThai = "Giám đốc";
                        foreach (var vt in VTPhieunhapkholist)
                        {
                            vt.TrangThai = "Giám đốc";
                            _context.vtphieunhapkho.Update(vt);
                        }
                    }
                }
                else if (Phieunhapkho.TrangThai == "Chờ nhập kho" && boPhan2 == "BP kho")
                {
                    // Kho xử lý nhập kho
                    Phieunhapkho.TrangThai = "Đã nhập kho";
                    
                    // Cập nhật tồn kho khi nhập hàng
                    foreach (var VTPhieunhapkho in VTPhieunhapkholist)
                    {
                        // Tìm vật tư trong tồn kho với đầy đủ điều kiện
                        var khotong = _context.khotongs
                            .AsNoTracking()
                            .FirstOrDefault(k => 
                                k.TenSanpham == VTPhieunhapkho.TenSanpham && 
                                k.MaSanpham == VTPhieunhapkho.MaSanpham && 
                                k.HangSX == VTPhieunhapkho.HangSX &&
                                k.Makho == VTPhieunhapkho.Makho &&
                                (k.NhaCC == VTPhieunhapkho.NhaCC || 
                                 (string.IsNullOrWhiteSpace(k.NhaCC) && string.IsNullOrWhiteSpace(VTPhieunhapkho.NhaCC))));
                            
                        if (khotong != null)
                        {
                            // Kiểm tra xem entity đã được track trong context chưa
                            var trackedEntity = _context.khotongs.Local
                                .FirstOrDefault(k => k.Makho == khotong.Makho && 
                                    (k.NhaCC == khotong.NhaCC || 
                                     (string.IsNullOrWhiteSpace(k.NhaCC) && string.IsNullOrWhiteSpace(khotong.NhaCC))));
                            
                            if (trackedEntity != null)
                            {
                                // Sử dụng entity đã được track
                                trackedEntity.SL += VTPhieunhapkho.SL ?? 0;
                            }
                            else
                            {
                                // Attach và update entity
                                khotong.SL += VTPhieunhapkho.SL ?? 0;
                                _context.khotongs.Attach(khotong);
                                _context.Entry(khotong).State = EntityState.Modified;
                            }
                        }
                        else
                        {
                            // Kiểm tra lại với điều kiện không cần TenSanpham (vì có thể khác nhau nhưng vẫn là cùng vật tư)
                            var existingInDb = _context.khotongs
                                .AsNoTracking()
                                .FirstOrDefault(k => 
                                    k.MaSanpham == VTPhieunhapkho.MaSanpham &&
                                    k.Makho == VTPhieunhapkho.Makho &&
                                    k.HangSX == VTPhieunhapkho.HangSX &&
                                    (k.NhaCC == VTPhieunhapkho.NhaCC || 
                                     (string.IsNullOrWhiteSpace(k.NhaCC) && string.IsNullOrWhiteSpace(VTPhieunhapkho.NhaCC))));
                            
                            if (existingInDb != null)
                            {
                                // Tìm thấy record phù hợp, cập nhật số lượng
                                existingInDb.SL += VTPhieunhapkho.SL ?? 0;
                                _context.khotongs.Attach(existingInDb);
                                _context.Entry(existingInDb).State = EntityState.Modified;
                            }
                            else
                            {
                                // Kiểm tra xem có record với cùng Makho nhưng khác NhaCC/HangSX không
                                var existingInDbSameMakho = _context.khotongs
                                    .AsNoTracking()
                                    .FirstOrDefault(k => k.Makho == VTPhieunhapkho.Makho);
                                
                                if (existingInDbSameMakho != null)
                                {
                                    // Cùng Makho nhưng khác NhaCC/HangSX → tạo Makho mới với suffix
                                    string baseMakho = VTPhieunhapkho.Makho;
                                    int suffix = 1;
                                    string newMakho;
                                    
                                    do
                                    {
                                        newMakho = $"{baseMakho}-{suffix:D2}";
                                        suffix++;
                                    }
                                    while (_context.khotongs.Any(k => k.Makho == newMakho) ||
                                           _context.khotongs.Local.Any(k => k.Makho == newMakho));
                                    
                                    // Tạo mới với Makho mới
                                    var newKhotong = new khotongs
                                    {
                                        TenSanpham = VTPhieunhapkho.TenSanpham,
                                        MaSanpham = VTPhieunhapkho.MaSanpham,
                                        HangSX = VTPhieunhapkho.HangSX,
                                        NhaCC = VTPhieunhapkho.NhaCC,
                                        SL = VTPhieunhapkho.SL ?? 0,
                                        DonVi = VTPhieunhapkho.DonVi,
                                        Makho = newMakho,
                                        NgayNhapkho = DateTime.Now,
                                        TrangThai = "Tồn kho"
                                    };
                                    _context.khotongs.Add(newKhotong);
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
                                        SL = VTPhieunhapkho.SL ?? 0,
                                        DonVi = VTPhieunhapkho.DonVi,
                                        Makho = VTPhieunhapkho.Makho,
                                        NgayNhapkho = DateTime.Now,
                                        TrangThai = "Tồn kho"
                                    };
                                    _context.khotongs.Add(newKhotong);
                                }
                            }
                        }
                        
                        VTPhieunhapkho.TrangThai = "Đã nhập kho";
                        _context.vtphieunhapkho.Update(VTPhieunhapkho);
                    }
                }

                _context.phieunhapkho.Update(Phieunhapkho);
            }
            else if (action == "reject")
            {
                Phieunhapkho.TrangThai = $"{chucVu2} - Đã từ chối";
                foreach (var vt in VTPhieunhapkholist)
                {
                    vt.TrangThai = $"{chucVu2} - Đã từ chối";
                    _context.vtphieunhapkho.Update(vt);
                }
                _context.phieunhapkho.Update(Phieunhapkho);
            }
            
            _context.SaveChanges();
            return RedirectToAction("Phieunhapkho", "Yeucau", new { area = "TruongBPKythuat" });
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

            return RedirectToAction("Phieunhapkho", "Yeucau", new { area = "TruongBPKythuat" });
        }

        [HttpPost]
        public IActionResult Xulituchoiyeucau(
                        string Ma,
                        yeucau yeucau,
                        vtyeucau vtyeucau,
                        phieumuahang phieumuahang,
                        vtphieumuahang vtphieumuahang)
        {
            var chucVu = HttpContext.Session.GetString("Chucvu");
            var boPhan = HttpContext.Session.GetString("Bophan");
            var maNv = HttpContext.Session.GetString("MaNguoidung");

            // Không dựa vào tiền tố "PMH" nữa vì mã phiếu đã được chuẩn hoá theo service
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

                    _context.SaveChanges();
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
                    _context.SaveChanges();
                }
            }

            var refererUrl = HttpContext.Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(refererUrl))
            {
                return Redirect(refererUrl);
            }

            return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPKythuat" });
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

            // Tính tổng số lượng vật tư đã cam kết từ các phiếu xuất này cho đúng 1 mã kho
            var tongSoLuongDaCamKet = _context.vtphieuxuatkho
                .Where(vt => phieuXuatDaCamKet.Contains(vt.MaXuatkho) 
                    && vt.Makho == makho 
                    && vt.MaSanpham == masanpham)
                .Sum(vt => vt.SL ?? 0);

            return tongSoLuongDaCamKet;
        }

        // Helper: Tính số lượng đã cam kết theo Mã SP + Hãng SX (bỏ qua mã kho, gom tất cả các lô ATB15-HIVERO-...)
        private int TinhSoLuongDaCamKetTheoMaVaHang(string masanpham, string hangSX, string maXuatkhoHienTai = null)
        {
            var cacTrangThaiDaCamKet = new[] { "Đang chuẩn bị hàng", "Chờ người yêu cầu xác nhận" };

            var phieuXuatDaCamKet = _context.phieuxuatkho
                .Where(px => cacTrangThaiDaCamKet.Contains(px.TrangThai))
                .Select(px => px.MaXuatkho)
                .ToList();

            if (!string.IsNullOrEmpty(maXuatkhoHienTai))
            {
                phieuXuatDaCamKet = phieuXuatDaCamKet
                    .Where(mx => mx != maXuatkhoHienTai)
                    .ToList();
            }

            var tongSoLuongDaCamKet = _context.vtphieuxuatkho
                .Where(vt => phieuXuatDaCamKet.Contains(vt.MaXuatkho)
                             && vt.MaSanpham == masanpham
                             && vt.HangSX == hangSX)
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
                        var maSp = vt.MaSanpham ?? "";
                        var hangSX = vt.HangSX ?? "";

                        // Lấy toàn bộ các lô trong kho có cùng mã sản phẩm + hãng SX (ví dụ: ATB15-HIVERO-20251203, ATB15-HIVERO-20251203-01, ...)
                        var danhSachLoKho = _context.khotongs
                            .Where(k => k.MaSanpham == maSp && k.HangSX == hangSX)
                            .OrderBy(k => k.NgayNhapkho) // ưu tiên xuất trước lô nhập sớm (FIFO)
                            .ToList();

                        if (!danhSachLoKho.Any())
                        {
                            TempData["ErrorMessage"] = $"Không thể xuất kho: Vật tư {vt.TenSanpham} không tồn tại trong kho";
                            return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "TruongBPKythuat" });
                        }

                        int tongTon = danhSachLoKho.Sum(k => k.SL ?? 0);
                        int soLuongDaCamKetKhac = TinhSoLuongDaCamKetTheoMaVaHang(maSp, hangSX, MaXuatkho);
                        int soLuongKhaDung = tongTon - soLuongDaCamKetKhac;

                        // TUYỆT ĐỐI KHÔNG cho phép xuất nếu hết hàng hoặc không đủ số lượng
                        if (soLuongKhaDung <= 0 || soLuongKhaDung < vt.SL)
                        {
                            TempData["ErrorMessage"] = $"Không thể xuất kho: Vật tư {vt.TenSanpham} không đủ số lượng trong kho (Tồn kho: {tongTon}, Đã cam kết: {soLuongDaCamKetKhac}, Khả dụng: {soLuongKhaDung}, Yêu cầu: {vt.SL})";
                            return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "TruongBPKythuat" });
                        }

                        // Đủ hàng: trừ lần lượt trên từng lô (FIFO)
                        int soLuongCanTru = vt.SL ?? 0;
                        foreach (var lo in danhSachLoKho)
                        {
                            if (soLuongCanTru <= 0) break;

                            var soLuongTrongLo = lo.SL ?? 0;
                            if (soLuongTrongLo <= 0) continue;

                            var tru = Math.Min(soLuongTrongLo, soLuongCanTru);
                            lo.SL = soLuongTrongLo - tru;
                            soLuongCanTru -= tru;
                            _context.khotongs.Update(lo);
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

                TempData["SuccessMessage"] = "Xác nhận nhận hàng thành công!";
                return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "TruongBPKythuat" });
            }

            TempData["ErrorMessage"] = "Phiếu không hợp lệ hoặc đã được xác nhận!";
            return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "TruongBPKythuat" });
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
