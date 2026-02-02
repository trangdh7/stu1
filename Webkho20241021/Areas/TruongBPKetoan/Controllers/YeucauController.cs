using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Webkho_20241021.Areas.TruongBPKetoan.Data;
using Webkho_20241021.Models;
using Webkho_20241021.Services;
using Webkho_20241021.Services;
using OfficeOpenXml;
using Microsoft.Extensions.DependencyInjection;


namespace Webkho_20241021.Areas.TruongBPKetoan.Controllers
{
    [Area("TruongBPKetoan")]
    [Authorize(Roles = "Trưởng BP-BP kế toán")]
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
                        System.Diagnostics.Debug.WriteLine($"[TruongBPKetoan] Bắt đầu gửi email từ chối cho {maYeucau}");
                        await emailService.SendNotificationToRequesterOnRejectionAsync(maYeucau, ghiChu);
                        System.Diagnostics.Debug.WriteLine($"[TruongBPKetoan] Đã gửi email từ chối cho {maYeucau}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TruongBPKetoan] Lỗi gửi email từ chối cho {maYeucau}: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[TruongBPKetoan] Stack trace: {ex.StackTrace}");
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
            .OrderByDescending(y => y.TrangThai == "Chờ thanh toán")
            .ThenByDescending(y => y.NgayMuahang)
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
            object vatTuList;

            if (string.IsNullOrWhiteSpace(MaYeucau))
            {
                vatTuList = new List<object>();
            }
            else
            {
                // Nếu yêu cầu đã có dòng chi tiết trong vtyeucau
                // thì luôn ưu tiên hiển thị danh sách đó (yêu cầu vật tư gốc),
                // kể cả khi đã phát sinh phiếu nhập kho.
                bool hasVatTuYeuCau = _context.vtyeucau
                    .Any(v => v.VTMaYeucau == MaYeucau);

                if (hasVatTuYeuCau)
                {
                    var rawList = _context.vtyeucau.Where(v => v.VTMaYeucau == MaYeucau).ToList();
                    var maSanphamList = rawList.Where(v => !string.IsNullOrWhiteSpace(v.MaSanpham)).Select(v => v.MaSanpham!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    var maSanphamSet = new HashSet<string>(maSanphamList, StringComparer.OrdinalIgnoreCase);
                    var tonKhoByMaSanpham = _context.khotongs.Where(k => k.MaSanpham != null).Select(k => new { k.MaSanpham, k.SL }).ToList()
                        .Where(k => maSanphamSet.Contains(k.MaSanpham!)).GroupBy(k => k.MaSanpham!, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => g.Sum(x => x.SL ?? 0), StringComparer.OrdinalIgnoreCase);
                    vatTuList = rawList.Select(v =>
                    {
                        var slMoi = v.SLMoi ?? v.SL ?? 0;
                        var tonKho = !string.IsNullOrWhiteSpace(v.MaSanpham) && tonKhoByMaSanpham.TryGetValue(v.MaSanpham, out var tk) ? tk : 0;
                        var slThieu = tonKho - slMoi;
                        var isDaXuatKho = (v.TrangThai ?? "").IndexOf("Đã xuất kho", StringComparison.OrdinalIgnoreCase) >= 0;
                        var slDaXuat = isDaXuatKho ? (v.SL ?? v.SLMoi) : (int?)null;
                        return new { v.ID, v.VTMaYeucau, v.TenSanpham, v.MaSanpham, v.YCMakho, v.HangSX, v.NhaCC, v.SLCu, v.SLMoi, v.SL, v.DonVi, v.NgayCanHang, v.NgayNhapkho, v.NgayBaohanh, v.ThoiGianBH, v.NgayDuyet, v.TrangThai, v.GhiChu, TonKho = tonKho, SlThieu = slThieu, SlDaXuat = slDaXuat };
                    }).ToList();
                }
                else
                {
                    // Chỉ khi KHÔNG có vtyeucau, mới coi là yêu cầu nhập kho đặc biệt
                    // và lấy dữ liệu từ vtphieunhapkho/phieunhapkho.
                    bool isNhapKhoRequest =
                        (!string.IsNullOrEmpty(MaYeucau) && MaYeucau.StartsWith("NHAPKHO_", StringComparison.OrdinalIgnoreCase)) ||
                        _context.phieunhapkho.Any(p => p.MaYeucau == MaYeucau) ||
                        _context.yeucau.Any(y => y.MaYeucau == MaYeucau && y.TenYeucau == "Yêu cầu nhập kho");

                    if (isNhapKhoRequest)
                    {
                        var rawNk = (from vtnk in _context.vtphieunhapkho
                                     join pnk in _context.phieunhapkho on vtnk.MaNhapkho equals pnk.MaNhapkho
                                     where pnk.MaYeucau == MaYeucau
                                     select new
                                     {
                                         ID = vtnk.ID,
                                         VTMaYeucau = MaYeucau,
                                         TenSanpham = vtnk.TenSanpham,
                                         MaSanpham = vtnk.MaSanpham,
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
                        var maSpList = rawNk.Where(x => !string.IsNullOrWhiteSpace(x.MaSanpham)).Select(x => x.MaSanpham!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                        var maSpSet = new HashSet<string>(maSpList, StringComparer.OrdinalIgnoreCase);
                        var tkByMa = _context.khotongs.Where(k => k.MaSanpham != null).Select(k => new { k.MaSanpham, k.SL }).ToList()
                            .Where(k => maSpSet.Contains(k.MaSanpham!)).GroupBy(k => k.MaSanpham!, StringComparer.OrdinalIgnoreCase)
                            .ToDictionary(g => g.Key, g => g.Sum(x => x.SL ?? 0), StringComparer.OrdinalIgnoreCase);
                        vatTuList = rawNk.Select(x =>
                        {
                            var v = x;
                            var tonKho = !string.IsNullOrWhiteSpace(x.MaSanpham) && tkByMa.TryGetValue(x.MaSanpham, out var t) ? t : 0;
                            return new { x.ID, VTMaYeucau = MaYeucau, x.TenSanpham, x.MaSanpham, YCMakho = (string?)null, x.HangSX, x.NhaCC, SLCu = (int?)null, x.SLMoi, x.SL, x.DonVi, NgayCanHang = (DateTime?)null, x.NgayNhapkho, x.NgayBaohanh, x.ThoiGianBH, NgayDuyet = (DateTime?)null, x.TrangThai, GhiChu = (string?)null, TonKho = tonKho, SlThieu = tonKho - (x.SLMoi ?? 0), SlDaXuat = (int?)null };
                        }).ToList<object>();
                    }
                    else
                    {
                        var rawList = _context.vtyeucau.Where(v => v.VTMaYeucau == MaYeucau).ToList();
                        var maSanphamList = rawList.Where(v => !string.IsNullOrWhiteSpace(v.MaSanpham)).Select(v => v.MaSanpham!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                        var maSanphamSet = new HashSet<string>(maSanphamList, StringComparer.OrdinalIgnoreCase);
                        var tonKhoByMaSanpham = _context.khotongs.Where(k => k.MaSanpham != null).Select(k => new { k.MaSanpham, k.SL }).ToList()
                            .Where(k => maSanphamSet.Contains(k.MaSanpham!)).GroupBy(k => k.MaSanpham!, StringComparer.OrdinalIgnoreCase)
                            .ToDictionary(g => g.Key, g => g.Sum(x => x.SL ?? 0), StringComparer.OrdinalIgnoreCase);
                        vatTuList = rawList.Select(v =>
                        {
                            var slMoi = v.SLMoi ?? v.SL ?? 0;
                            var tonKho = !string.IsNullOrWhiteSpace(v.MaSanpham) && tonKhoByMaSanpham.TryGetValue(v.MaSanpham, out var tk) ? tk : 0;
                            var slThieu = tonKho - slMoi;
                            var isDaXuatKho = (v.TrangThai ?? "").IndexOf("Đã xuất kho", StringComparison.OrdinalIgnoreCase) >= 0;
                            var slDaXuat = isDaXuatKho ? (v.SL ?? v.SLMoi) : (int?)null;
                            return new { v.ID, v.VTMaYeucau, v.TenSanpham, v.MaSanpham, v.YCMakho, v.HangSX, v.NhaCC, v.SLCu, v.SLMoi, v.SL, v.DonVi, v.NgayCanHang, v.NgayNhapkho, v.NgayBaohanh, v.ThoiGianBH, v.NgayDuyet, v.TrangThai, v.GhiChu, TonKho = tonKho, SlThieu = slThieu, SlDaXuat = slDaXuat };
                        }).ToList<object>();
                    }
                }
            }
            
            // Lấy thông tin yêu cầu để lấy tên người yêu cầu
            var yeucau = _context.yeucau
                .FirstOrDefault(y => y.MaYeucau == MaYeucau);
            
            string tenNguoiYeuCau = "";
            if (yeucau != null)
            {
                tenNguoiYeuCau = yeucau.NguoiYeucau ?? "";
            }
            
            return Json(new
            {
                items = vatTuList,
                maYeucau = MaYeucau,
                tenNguoiYeuCau = tenNguoiYeuCau
            });
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
            
            return Json(new
            {
                items = PhieumuahangList,
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
                    // Khi trưởng phòng duyệt, luôn đặt trạng thái "Chờ giám đốc duyệt"
                    vatTu.TrangThai = "Chờ giám đốc duyệt";
                }
                else if (action == "reject")
                {
                    vatTu.TrangThai = "Đã từ chối";
                }

                _context.vtyeucau.Update(vatTu);
                _context.SaveChanges();

                // Gửi email thông báo từ chối nếu trạng thái yêu cầu là "Đã từ chối"
                if (action == "reject")
                {
                    var yeucauAfterReject = _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
                    if (yeucauAfterReject != null && yeucauAfterReject.TrangThai == "Đã từ chối")
                    {
                        SendRejectionEmailAsync(MaYeucau, "");
                    }
                }

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

                    // Helper function để kiểm tra xem vật tư có đang chờ Trưởng BP kế toán duyệt không
                    Func<string, bool> isAwaitingTruongBPStatus = status =>
                    {
                        if (string.IsNullOrWhiteSpace(status))
                        {
                            return true;
                        }
                        var normalized = status.Trim();
                        return normalized.Equals("Chờ Trưởng BP-BP kế toán duyệt", StringComparison.OrdinalIgnoreCase)
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

                    // Chỉ xử lý các vật tư đang chờ Trưởng BP kế toán duyệt và chưa được duyệt/từ chối
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
                    
                    if (allApprovedByTruongBP && chucVu == "Trưởng BP" && boPhan == "BP kế toán")
                    {
                        yeucau.TrangThai = nextTrangThaiYC;
                        _context.yeucau.Update(yeucau);
                        _context.SaveChanges();

                        // Sau khi Trưởng BP kế toán duyệt xong bằng checkbox:
                        // - Gửi mail cho người yêu cầu
                        // - Gửi mail cho bước tiếp theo (QLDA nếu có dự án, hoặc Giám đốc nếu không có dự án)
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(yeucau.NguoiYeucau))
                            {
                                var trangThaiThongBao = hasMaDuan
                                    ? "Đã được Trưởng BP-BP kế toán duyệt - chuyển quản lý dự án"
                                    : "Đã được Trưởng BP-BP kế toán duyệt - chờ Giám đốc duyệt";

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
                            System.Diagnostics.Debug.WriteLine($"[TruongBPKetoan/XuLyVatTuYeucauWithCheckbox] Lỗi gửi email sau duyệt: {ex.Message}");
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
                        // Khi trưởng phòng duyệt, luôn đặt trạng thái "Chờ giám đốc duyệt"
                        vatTu.TrangThai = "Chờ giám đốc duyệt";
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
                        
                        if (allApprovedByTruongBP && chucVu == "Trưởng BP" && boPhan == "BP kế toán")
                        {
                            // Kiểm tra trạng thái hiện tại của yêu cầu
                            if (yeucau.TrangThai == "Chờ Trưởng BP-BP kế toán duyệt")
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
            var results = _context.khotongs
                .Where(k => k.TenSanpham.Contains(timkiem) || k.MaSanpham.Contains(timkiem))
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
                                           List<int?> SLCu, List<int?> SLMoi,
                                           List<string> DonVi, List<string> GhiChu, string MaYeucau, string action, phieuxuatkho phieuxuatkho, vtphieuxuatkho vtphieuxuatkho, phieumuahang phieumuahang, vtphieumuahang vtphieumuahang)
        {
            // Kiểm tra null để tránh lỗi khi upload file Excel lớn
            if (yeucau == null)
            {
                yeucau = new yeucau();
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
                        if (chucVu2 == "Nhân viên" && boPhan2 == "BP kỹ thuật")
                        {
                            yeucau.TrangThai = "Chờ Trưởng BP-BP kỹ thuật duyệt";
                        }
                        else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kỹ thuật")
                        {
                            yeucau.TrangThai = "Quản lí dự án";
                            // Gửi thông báo đến QLDA sau khi Trưởng BP duyệt
                            var duanForEmail = _context.duans.FirstOrDefault(d => d.MaDuan == yeucau.YCMaDuan);
                            if (duanForEmail != null)
                            {
                                _ = _emailService.SendNotificationToProjectManagerAsync(yeucau.MaYeucau, duanForEmail.MaDuan);
                            }
                        }
                        else if (chucVu2 == "Nhân viên" && boPhan2 == "BP kho")
                        {
                            yeucau.TrangThai = "Chờ Trưởng BP-BP kho duyệt";
                        }
                        else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kho")
                        {
                            yeucau.TrangThai = "Chờ quản lý dự án duyệt";
                            // Gửi thông báo đến QLDA sau khi Trưởng BP duyệt
                            var duanForEmail = _context.duans.FirstOrDefault(d => d.MaDuan == yeucau.YCMaDuan);
                            if (duanForEmail != null)
                            {
                                _ = _emailService.SendNotificationToProjectManagerAsync(yeucau.MaYeucau, duanForEmail.MaDuan);
                            }
                        }
                        else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kế toán")
                        {
                            yeucau.TrangThai = "Chờ quản lý dự án duyệt";
                            // Gửi thông báo đến QLDA sau khi Trưởng BP duyệt
                            var duanForEmail = _context.duans.FirstOrDefault(d => d.MaDuan == yeucau.YCMaDuan);
                            if (duanForEmail != null)
                            {
                                _ = _emailService.SendNotificationToProjectManagerAsync(yeucau.MaYeucau, duanForEmail.MaDuan);
                            }
                        }
                        else if (chucVu2 == "Nhân viên" && boPhan2 == "BP mua hàng")
                        {
                            yeucau.TrangThai = "Chờ Trưởng BP-BP mua hàng duyệt";
                        }
                        else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP mua hàng")
                        {
                            yeucau.TrangThai = "Quản lí dự án";
                            // Gửi thông báo đến QLDA sau khi Trưởng BP duyệt
                            var duanForEmail = _context.duans.FirstOrDefault(d => d.MaDuan == yeucau.YCMaDuan);
                            if (duanForEmail != null)
                            {
                                _ = _emailService.SendNotificationToProjectManagerAsync(yeucau.MaYeucau, duanForEmail.MaDuan);
                            }
                        }
                        else if (chucVu2 == "Giám đốc")
                        {
                            yeucau.TrangThai = "Đã duyệt";
                        }
                    }
                }
                else
                {
                    if (chucVu2 == "Nhân viên" && boPhan2 == "BP kỹ thuật")
                    {
                        yeucau.TrangThai = "Trưởng BP-BP kỹ thuật";
                    }
                    else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kỹ thuật")
                    {
                        yeucau.TrangThai = "Giám đốc";
                        // Gửi thông báo đến Giám đốc sau khi Trưởng BP duyệt (không có dự án)
                        _ = _emailService.SendNotificationToDirectorAsync(yeucau.MaYeucau);
                    }
                    else if (chucVu2 == "Nhân viên" && boPhan2 == "BP kho")
                    {
                        yeucau.TrangThai = "Trưởng BP-BP kho";
                    }
                    else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kho")
                    {
                        yeucau.TrangThai = "Chờ Giám đốc duyệt";
                    }
                    else if (chucVu2 == "Nhân viên" && boPhan2 == "BP mua hàng")
                    {
                        yeucau.TrangThai = "Trưởng BP-BP mua hàng";
                    }
                    else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP mua hàng")
                    {
                        yeucau.TrangThai = "Chờ Giám đốc duyệt";
                    }
                    else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kế toán")
                    {
                        yeucau.TrangThai = "Chờ Giám đốc duyệt";
                    }
                    else if (chucVu2 == "Giám đốc")
                    {
                        yeucau.TrangThai = "Đã duyệt";

                    }
                }
                
                // Đảm bảo trạng thái luôn được set đúng cho Trưởng BP kế toán
                if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kế toán")
                {
                    if (duan != null)
                    {
                        // Có dự án: Chờ quản lý dự án duyệt
                        if (yeucau.TrangThai != "Chờ quản lý dự án duyệt" && yeucau.TrangThai != "Giám đốc" && yeucau.TrangThai != "Đã duyệt")
                        {
                            yeucau.TrangThai = "Chờ quản lý dự án duyệt";
                        }
                    }
                    else
                    {
                        // Không có dự án (cá nhân): Chờ Giám đốc duyệt
                        if (yeucau.TrangThai != "Chờ Giám đốc duyệt" && yeucau.TrangThai != "Đã duyệt")
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

                // Lưu thông tin file Excel vào database (không lưu file vào đĩa để tiết kiệm dung lượng)
                try
                {
                    if (Request.Form.Files != null && Request.Form.Files.Count > 0)
                    {
                        var excelFile = Request.Form.Files.FirstOrDefault(f => 
                            f.Name == "excel-upload" || 
                            (!string.IsNullOrEmpty(f.FileName) && (f.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) || f.FileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))));
                        
                        if (excelFile != null && excelFile.Length > 0)
                        {
                            var excelFileRecord = new ExcelFile
                            {
                                MaYeucau = yeucau.MaYeucau,
                                MaDuan = yeucau.YCMaDuan,
                                TenFile = excelFile.FileName,
                                DuongDanFile = null, // Không lưu file vào đĩa
                                NgayUpload = DateTime.Now,
                                NguoiUpload = maNv2,
                                KichThuocFile = excelFile.Length
                            };

                            _context.ExcelFiles.Add(excelFileRecord);
                            _context.SaveChanges();
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log lỗi nhưng không dừng quá trình xử lý
                    Console.WriteLine($"Lỗi khi lưu thông tin file Excel: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                }

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

                        var slCuValue = (SLCu != null && i < SLCu.Count) ? SLCu[i] : null;
                        var slMoiValue = (SLMoi != null && i < SLMoi.Count) ? SLMoi[i] : null;
                        
                        // Bỏ qua dòng nếu số lượng mới bằng 0 (không cần lưu và hiển thị)
                        if (slMoiValue == 0)
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
                            newVtyeucau.TenSanpham = TenSanpham[i];
                            newVtyeucau.MaSanpham = MaSanpham[i];
                            newVtyeucau.HangSX = HangSX[i];
                            newVtyeucau.NhaCC = NhaCC[i];
                            newVtyeucau.SLCu = slCuValue;
                            newVtyeucau.SLMoi = slMoiValue;
                            newVtyeucau.SL = slMoi;
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

                // Gửi thông báo email cho QLDA hoặc Giám đốc khi Trưởng BP Kế toán tạo yêu cầu
                if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kế toán")
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"[TruongBPKetoan/ThemyeucauSQL] ===== BẮT ĐẦU GỬI EMAIL SAU KHI TẠO YÊU CẦU =====");
                        System.Diagnostics.Debug.WriteLine($"[TruongBPKetoan/ThemyeucauSQL] MaYeucau = {yeucau.MaYeucau}");
                        System.Diagnostics.Debug.WriteLine($"[TruongBPKetoan/ThemyeucauSQL] Trạng thái yêu cầu: {yeucau.TrangThai}");
                        System.Diagnostics.Debug.WriteLine($"[TruongBPKetoan/ThemyeucauSQL] YCMaDuan = {yeucau.YCMaDuan ?? "(null)"}");
                        System.Diagnostics.Debug.WriteLine($"[TruongBPKetoan/ThemyeucauSQL] hasMaDuan = {!string.IsNullOrWhiteSpace(yeucau.YCMaDuan)}");

                    // Lưu các giá trị cần thiết trước khi vào Task.Run
                    var maYeucauForEmail = yeucau.MaYeucau;
                    var trangThaiForEmail = yeucau.TrangThai;
                    var maDuanForEmail = yeucau.YCMaDuan;

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            System.Diagnostics.Debug.WriteLine($"[TruongBPKetoan/ThemyeucauSQL/Task] Bắt đầu gửi email trong Task.Run");

                            // Tạo scope mới để có DbContext và EmailService mới
                            using (var scope = _serviceScopeFactory.CreateScope())
                            {
                                var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                                
                                System.Diagnostics.Debug.WriteLine($"[TruongBPKetoan/ThemyeucauSQL/Task] Đã tạo scope và lấy EmailService mới");

                                // Gửi email dựa trên trạng thái
                                if (string.Equals(trangThaiForEmail, "Chờ quản lý dự án duyệt", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(maDuanForEmail))
                                {
                                    // Có dự án: Gửi cho QLDA
                                    System.Diagnostics.Debug.WriteLine($"[TruongBPKetoan/ThemyeucauSQL/Task] Gửi email cho QLDA. MaDuan = {maDuanForEmail}");
                                    await emailService.SendNotificationToProjectManagerAsync(maYeucauForEmail, maDuanForEmail);
                                    System.Diagnostics.Debug.WriteLine($"[TruongBPKetoan/ThemyeucauSQL/Task] ✅ Đã gửi email cho QLDA xong.");
                                }
                                else if (string.Equals(trangThaiForEmail, "Chờ Giám đốc duyệt", StringComparison.OrdinalIgnoreCase) || 
                                         string.Equals(trangThaiForEmail, "Giám đốc", StringComparison.OrdinalIgnoreCase))
                                {
                                    // Không có dự án: Gửi cho Giám đốc
                                    System.Diagnostics.Debug.WriteLine($"[TruongBPKetoan/ThemyeucauSQL/Task] Gửi email cho Giám đốc.");
                                    await emailService.SendNotificationToDirectorAsync(maYeucauForEmail);
                                    System.Diagnostics.Debug.WriteLine($"[TruongBPKetoan/ThemyeucauSQL/Task] ✅ Đã gửi email cho Giám đốc xong.");
                                }
                                else
                                {
                                    System.Diagnostics.Debug.WriteLine($"[TruongBPKetoan/ThemyeucauSQL/Task] ⚠️ Trạng thái '{trangThaiForEmail}' không cần gửi email.");
                                }
                            }
                        }
                        catch (Exception exInner)
                        {
                            System.Diagnostics.Debug.WriteLine($"[TruongBPKetoan/ThemyeucauSQL/Task] ❌ Lỗi trong Task.Run khi gửi email: {exInner.Message}");
                            System.Diagnostics.Debug.WriteLine($"[TruongBPKetoan/ThemyeucauSQL/Task] Stack trace: {exInner.StackTrace}");
                            if (exInner.InnerException != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"[TruongBPKetoan/ThemyeucauSQL/Task] Inner exception: {exInner.InnerException.Message}");
                            }
                        }
                    });
                        System.Diagnostics.Debug.WriteLine($"[TruongBPKetoan/ThemyeucauSQL] ✅ Đã khởi tạo Task.Run để gửi email");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TruongBPKetoan/ThemyeucauSQL] ❌ Lỗi khi khởi tạo Task.Run: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"[TruongBPKetoan/ThemyeucauSQL] Stack trace: {ex.StackTrace}");
                    }
                }
            }
            else
            {
                // Tạo mã phiếu nhập kho duy nhất bằng service
                phieunhapkho.MaNhapkho = _phieuCodeService.GenerateMaNhapKho(phieunhapkho.MaDuan, phieunhapkho.MaYeucau);
            }

            return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPKetoan" });

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
                // Xử lý khi trạng thái là "Chờ Trưởng BP-BP kế toán duyệt"
                if (Yeucau.TrangThai == "Chờ Trưởng BP-BP kế toán duyệt" && chucVu2 == "Trưởng BP" && boPhan2 == "BP kế toán")
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
                                // Gửi thông báo đến QLDA sau khi Trưởng BP duyệt
                                if (duan != null)
                                {
                                    _ = _emailService.SendNotificationToProjectManagerAsync(Yeucau.MaYeucau, duan.MaDuan);
                                }
                            }
                            else if (Yeucau.MaYeucau.StartsWith("NHAPKHO_CANHAN_"))
                            {
                                // Cá nhân: Chờ Giám đốc duyệt
                                Yeucau.TrangThai = "Chờ Giám đốc duyệt";
                                // Gửi thông báo đến Giám đốc sau khi Trưởng BP duyệt (không có dự án)
                                _ = _emailService.SendNotificationToDirectorAsync(Yeucau.MaYeucau);
                            }
                        }
                        else
                        {
                            // Nếu là yêu cầu vật tư thông thường
                            if (duan != null)
                            {
                                // Có dự án: Chờ quản lý dự án duyệt
                                Yeucau.TrangThai = "Chờ quản lý dự án duyệt";
                                // Gửi thông báo đến QLDA sau khi Trưởng BP duyệt
                                _ = _emailService.SendNotificationToProjectManagerAsync(Yeucau.MaYeucau, duan.MaDuan);
                                
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
                                // Gửi thông báo đến Giám đốc sau khi Trưởng BP duyệt (không có dự án)
                                _ = _emailService.SendNotificationToDirectorAsync(Yeucau.MaYeucau);
                                
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
                // Nếu trạng thái đã là "Chờ Giám đốc duyệt" hoặc "Đã duyệt", không xử lý (để giám đốc xử lý)
                else if (Yeucau.TrangThai == "Chờ Giám đốc duyệt" || Yeucau.TrangThai == "Đã duyệt" || Yeucau.TrangThai == "Giám đốc")
                {
                    // Trạng thái đã được xử lý bởi giám đốc, không làm gì
                    return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPKetoan" });
                }
                else if (duan != null)
                {
                    string maNguoiQLDA = duan.MaNguoiQLDA;
                    if (maNv2 == maNguoiQLDA)
                    {
                        if (chucVu2 != "Giám đốc")
                        {
                            Yeucau.TrangThai = "Chờ Giám đốc duyệt";
                            // Gửi thông báo đến Giám đốc sau khi QLDA duyệt
                            _ = _emailService.SendNotificationToDirectorAsync(Yeucau.MaYeucau);
                            
                            // Đồng bộ trạng thái tất cả vật tư khi chuyển sang "Chờ Giám đốc duyệt" (bao gồm cả null/empty)
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
                                Yeucau.TrangThai = "Chờ Giám đốc duyệt";
                                
                                // Đồng bộ trạng thái tất cả vật tư khi chuyển sang "Chờ Giám đốc duyệt"
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
                    // Chỉ xử lý nếu trạng thái hiện tại là "Chờ Trưởng BP-BP [tên bộ phận] duyệt"
                    if ((Yeucau.TrangThai == "Trưởng BP-BP kế toán" || Yeucau.TrangThai == "Chờ Trưởng BP-BP kế toán duyệt") && chucVu2 == "Trưởng BP" && boPhan2 == "BP kế toán")
                    {
                        Yeucau.TrangThai = "Chờ Giám đốc duyệt";
                        
                        // Đồng bộ trạng thái tất cả vật tư khi chuyển sang "Giám đốc"
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
                    }
                    else                     if (chucVu2 == "Nhân viên" && boPhan2 == "BP kỹ thuật")
                    {
                        Yeucau.TrangThai = "Chờ Trưởng BP-BP kỹ thuật duyệt";
                    }
                    else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kỹ thuật" && (Yeucau.TrangThai == "Trưởng BP-BP kỹ thuật" || Yeucau.TrangThai == "Chờ Trưởng BP-BP kỹ thuật duyệt"))
                    {
                        Yeucau.TrangThai = "Chờ Giám đốc duyệt";
                    }
                    else if (chucVu2 == "Nhân viên" && boPhan2 == "BP kho")
                    {
                        Yeucau.TrangThai = "Chờ Trưởng BP-BP kho duyệt";
                    }
                    else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kho" && (Yeucau.TrangThai == "Trưởng BP-BP kho" || Yeucau.TrangThai == "Chờ Trưởng BP-BP kho duyệt"))
                    {
                        Yeucau.TrangThai = "Chờ Giám đốc duyệt";
                    }
                    else if (chucVu2 == "Nhân viên" && boPhan2 == "BP mua hàng")
                    {
                        Yeucau.TrangThai = "Chờ Trưởng BP-BP mua hàng duyệt";
                    }
                    else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP mua hàng" && (Yeucau.TrangThai == "Trưởng BP-BP mua hàng" || Yeucau.TrangThai == "Chờ Trưởng BP-BP mua hàng duyệt"))
                    {
                        Yeucau.TrangThai = "Chờ Giám đốc duyệt";
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

            return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPKetoan" });
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
                return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPKetoan" });
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


            return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPKetoan" });
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
            return RedirectToAction("Phieuxuatkho", "Yeucau", new { area = "TruongBPKetoan" });
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
                _context.SaveChanges();

                // Gửi email thông báo khi kế toán thanh toán
                if (boPhan2 == "BP kế toán" && Phieumuahang.TrangThai == "Đã thanh toán")
                {
                    try
                    {
                        // Gửi email cho BP mua hàng
                        _ = _emailService.SendNotificationToPurchasingOnPaymentAsync(MaMuahang);
                        
                        // Gửi email cho người yêu cầu
                        _ = _emailService.SendNotificationToRequesterOnPaymentAsync(MaMuahang);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TruongBPKetoan/XuLyPhieumuahang] Lỗi gửi email thanh toán: {ex.Message}");
                    }
                }
            }
            else if (action == "reject")
            {
                Xulituchoiyeucau(MaMuahang, null, null, phieumuahang, vtphieumuahang);
                _context.SaveChanges();
            }
            else
            {
                _context.SaveChanges();
            }
            return RedirectToAction("Phieumuahang", "Yeucau", new { area = "TruongBPKetoan" });
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

            // Gửi email thông báo cho nhân viên kho khi có phiếu nhập kho mới cần xử lý
            try
            {
                _ = _emailService.SendNotificationToWarehouseOnNhapKhoAsync(MaNhapkho);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TruongBPKetoan/Taophieunhapkhobyphieumuahang] Lỗi gửi email nhập kho: {ex.Message}");
            }

            return RedirectToAction("Phieumuahang", "Yeucau", new { area = "TruongBPKetoan" });
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

            string currentArea = "TruongBPKetoan";

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
                    phieunhapkho.TrangThai = "Chờ Giám đốc duyệt"; // Giám đốc duyệt
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
                            System.Diagnostics.Debug.WriteLine($"[TruongBPKetoan/ThemPhieunhapkhoSQL] Lỗi gửi email tạo phiếu nhập kho: {exInner.Message}");
                        }
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TruongBPKetoan/ThemPhieunhapkhoSQL] Lỗi khởi chạy task gửi email: {ex.Message}");
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
            }
            else if (action == "reject")
            {
                var Phieunhapkho = _context.phieunhapkho.FirstOrDefault(p => p.MaNhapkho == MaNhapkho);
                Phieunhapkho.TrangThai = "Đã từ chối";
                _context.phieunhapkho.Update(Phieunhapkho);

            }
            _context.SaveChanges();
            return RedirectToAction("Phieunhapkho", "Yeucau", new { area = "TruongBPKetoan" });
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

            return RedirectToAction("Phieunhapkho", "Yeucau", new { area = "TruongBPKetoan" });
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

            if (!Ma.Contains("PMH"))
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

            return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPKetoan" });
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
                                return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "TruongBPKetoan" });
                            }
                            
                            khotong.SL -= vt.SL;
                            _context.khotongs.Update(khotong);
                        }
                        else
                        {
                            TempData["ErrorMessage"] = $"Không thể xuất kho: Vật tư {vt.TenSanpham} không tồn tại trong kho";
                            return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "TruongBPKetoan" });
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
                return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "TruongBPKetoan" });
            }

            TempData["ErrorMessage"] = "Phiếu không hợp lệ hoặc đã được xác nhận!";
            return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "TruongBPKetoan" });
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

        // In phiếu nhập kho
        [HttpGet]
        public IActionResult InPhieunhapkho(string MaNhapkho)
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

            ViewBag.Phieunhapkho = phieunhapkho;
            ViewBag.VTPhieunhapkho = vtphieunhapkho;
            ViewBag.Yeucau = yeucau;

            return View();
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

        // In phiếu xuất kho
        [HttpGet]
        public IActionResult InPhieuxuatkho(string MaXuatkho)
        {
            if (string.IsNullOrEmpty(MaXuatkho))
            {
                return NotFound();
            }

            var phieuxuatkho = _context.phieuxuatkho
                .FirstOrDefault(p => p.MaXuatkho == MaXuatkho);

            if (phieuxuatkho == null)
            {
                return NotFound();
            }

            var vtphieuxuatkho = _context.vtphieuxuatkho
                .Where(vt => vt.MaXuatkho == MaXuatkho)
                .ToList();

            var yeucau = _context.yeucau
                .FirstOrDefault(y => y.MaYeucau == phieuxuatkho.MaYeucau);

            // SL yêu cầu ban đầu theo từng mã vật tư (base code) để in đúng cột "YÊU CẦU"
            var slYeuCauByBaseCode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(phieuxuatkho.MaYeucau))
            {
                var vtYcList = _context.vtyeucau
                    .Where(v => v.VTMaYeucau == phieuxuatkho.MaYeucau)
                    .ToList();

                slYeuCauByBaseCode = vtYcList
                    .GroupBy(v => YeucauUpdateHelper.GetBaseProductCode(v.MaSanpham ?? ""))
                    .ToDictionary(g => g.Key ?? "", g => g.Sum(x => x.SL ?? 0), StringComparer.OrdinalIgnoreCase);
            }

            ViewBag.Phieuxuatkho = phieuxuatkho;
            ViewBag.VTPhieuxuatkho = vtphieuxuatkho;
            ViewBag.Yeucau = yeucau;
            ViewBag.SLYeuCauByBaseCode = slYeuCauByBaseCode;

            return View();
        }


    }
}
