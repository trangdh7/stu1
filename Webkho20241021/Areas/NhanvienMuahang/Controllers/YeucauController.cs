using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Webkho_20241021.Areas.NhanvienMuahang.Data;
using Webkho_20241021.Models;
using Webkho_20241021.Services;
using Webkho_20241021.Services;
using OfficeOpenXml;


namespace Webkho_20241021.Areas.NhanvienMuahang.Controllers
{
    [Area("NhanvienMuahang")]
    [Authorize(Roles = "Nhân viên-BP mua hàng,Nhân viên mua hàng")]
    public class YeucauController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;
        private readonly IPhieuCodeService _phieuCodeService;
        public YeucauController(ApplicationDbContext context, EmailService emailService, IPhieuCodeService phieuCodeService)
        {
            _context = context;
            _emailService = emailService;
            _phieuCodeService = phieuCodeService;
        }
        public IActionResult Yeucau(string search = "")
        {
            var userRole = HttpContext.Session.GetString("Chucvu");

            var Yeucaulist = _context.yeucau.ToList();

            var PhieuMuaHangList = _context.phieumuahang.ToList();

            foreach (var yeucau in Yeucaulist)
            {
                var phieus = PhieuMuaHangList.Where(p => p.MaYeucau == yeucau.MaYeucau).ToList();

                // Nếu có bất kỳ phiếu mua hàng nào của yêu cầu chưa ở trạng thái "Đã nhận hàng"
                // thì trạng thái của yêu cầu sẽ là "Đang mua hàng"
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

            // Chỉ hiển thị các vật tư có SLMoi > 0
            var VTyeucaulist = _context.vtyeucau
                .Where(v => v.SLMoi.HasValue && v.SLMoi.Value > 0)
                .ToList();
            var Duans = _context.duans.ToList();

            // Lọc bằng service dùng chung
            var validYeucaulist = DataFilterService.FilterYeucau(Yeucaulist, VTyeucaulist);

            var SortedYeucaulist = validYeucaulist
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
            var VTphieuxuatkholist = _context.vtphieuxuatkho.ToList();

            var allPhieuxuatkholist = _context.phieuxuatkho.ToList();

            // Lọc bằng service dùng chung
            var validPhieuxuatkholist = DataFilterService.FilterPhieuxuatkho(allPhieuxuatkholist, VTphieuxuatkholist);

            var Phieuxuatkholist = validPhieuxuatkholist
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
            var VTphieunhapkholist = _context.vtphieunhapkho.ToList();

            var allPhieunhapkholist = _context.phieunhapkho.ToList();

            // Lọc bằng service dùng chung
            var validPhieunhapkholist = DataFilterService.FilterPhieunhapkho(allPhieunhapkholist, VTphieunhapkholist);

            var Phieunhapkholist = validPhieunhapkholist
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
            var VTphieumuahanglist = _context.vtphieumuahang.ToList();

            // Lọc bằng service dùng chung
            var allPhieumuahanglist = _context.phieumuahang.ToList();
            var validPhieumuahanglist = DataFilterService.FilterPhieumuahang(allPhieumuahanglist, VTphieumuahanglist);

            var Phieumuahanglist = validPhieumuahanglist
            .OrderByDescending(y => y.TrangThai == "Đang chờ báo giá")
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
                var vatTuList = _context.vtyeucau
                    .Where(v => v.VTMaYeucau == MaYeucau)
                    .ToList();
                var maSanphamList = vatTuList.Where(v => !string.IsNullOrWhiteSpace(v.MaSanpham)).Select(v => v.MaSanpham!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var maSanphamSet = new HashSet<string>(maSanphamList, StringComparer.OrdinalIgnoreCase);
                var tonKhoByMaSanpham = _context.khotongs.Where(k => k.MaSanpham != null).Select(k => new { k.MaSanpham, k.SL }).ToList()
                    .Where(k => maSanphamSet.Contains(k.MaSanpham!)).GroupBy(k => k.MaSanpham!, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.SL ?? 0), StringComparer.OrdinalIgnoreCase);
                var result = vatTuList.Select(v =>
                {
                    var slMoi = v.SLMoi ?? v.SL ?? 0;
                    var tonKho = !string.IsNullOrWhiteSpace(v.MaSanpham) && tonKhoByMaSanpham.TryGetValue(v.MaSanpham, out var tk) ? tk : 0;
                    var slThieu = Math.Max(0, slMoi - tonKho);
                    var isDaXuatKho = (v.TrangThai ?? "").IndexOf("Đã xuất kho", StringComparison.OrdinalIgnoreCase) >= 0;
                    var slDaXuat = isDaXuatKho ? (v.SL ?? v.SLMoi) : (int?)null;
                    return new
                    {
                        v.ID, v.TT, v.VTMaYeucau, v.TenSanpham, v.MaSanpham, v.YCMakho, v.HangSX, v.NhaCC, v.SLCu, v.SLMoi, v.SL,
                        v.DonVi, v.NgayCanHang, v.NgayNhapkho, v.NgayBaohanh, v.ThoiGianBH, v.NgayDuyet, v.TrangThai, v.GhiChu,
                        TonKho = tonKho, SlThieu = slThieu, SlDaXuat = slDaXuat
                    };
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
                                     TT = (string?)null,
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
                    return new
                    {
                        v.ID, TT = (string?)null, v.VTMaYeucau, v.TenSanpham, v.MaSanpham, v.YCMakho, v.HangSX, v.NhaCC, v.SLCu, v.SLMoi, v.SL,
                        v.DonVi, v.NgayCanHang, v.NgayNhapkho, v.NgayBaohanh, v.ThoiGianBH, v.NgayDuyet, v.TrangThai, v.GhiChu,
                        TonKho = tonKho, SlThieu = Math.Max(0, (v.SLMoi ?? v.SL ?? 0) - tonKho), SlDaXuat = (int?)null
                    };
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
                    exportRows.Add(new { v.TT, v.TenSanpham, v.MaSanpham, v.HangSX, v.NhaCC, v.SLCu, v.SLMoi, SlThieu = slThieu, SlDaXuat = slDaXuat, TonKho = tonKho, v.DonVi, v.NgayCanHang, v.TrangThai, v.GhiChu, v.NgayDuyet });
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
                        exportRows.Add(new { TT = (object)stt++, v.TenSanpham, v.MaSanpham, v.HangSX, v.NhaCC, SLCu = (int?)null, SLMoi = v.SL, SlThieu = slThieu, SlDaXuat = (int?)null, TonKho = tonKho, v.DonVi, NgayCanHang = (DateTime?)null, v.TrangThai, GhiChu = (string?)null, NgayDuyet = (DateTime?)null });
                    }
                }
            }
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Danh sách vật tư");
                worksheet.Cells[1, 1, 1, 15].Merge = true;
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
                worksheet.Cells[headerRow1, 13].Value = "Trạng thái";
                worksheet.Cells[headerRow1, 14].Value = "Ghi chú";
                worksheet.Cells[headerRow1, 15].Value = "Ngày duyệt";
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
                for (int r = headerRow1; r <= headerRow2; r++)
                {
                    using (var range = worksheet.Cells[r, 1, r, 15]) { range.Style.Font.Bold = true; range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid; range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(173, 216, 230)); range.Style.Border.Bottom.Style = range.Style.Border.Top.Style = range.Style.Border.Left.Style = range.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin; range.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center; range.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center; }
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
                    worksheet.Cells[row, 13].Value = r.TrangThai ?? "";
                    worksheet.Cells[row, 14].Value = r.GhiChu ?? "";
                    worksheet.Cells[row, 15].Value = r.NgayDuyet != null ? ((DateTime)r.NgayDuyet).ToString("dd/MM/yyyy HH:mm:ss") : "";
                    using (var range = worksheet.Cells[row, 1, row, 15]) { range.Style.Border.Bottom.Style = range.Style.Border.Top.Style = range.Style.Border.Left.Style = range.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin; }
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

            // EF Core MySQL không dịch được Contains(in-memory list) sang SQL -> filter phía client
            var maSanphamSet = new HashSet<string>(maSanphamList, StringComparer.OrdinalIgnoreCase);
            var tonKhoByCode = _context.khotongs
                .Where(k => k.MaSanpham != null)
                .Select(k => new { k.MaSanpham, k.SL })
                .ToList()
                .Where(k => maSanphamSet.Contains(k.MaSanpham!))
                .GroupBy(k => k.MaSanpham!, StringComparer.OrdinalIgnoreCase)
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
            var Duanlist = _context.duans
                          .Select(y => new { y.MaDuan, y.TrangThai })
                          .ToList();

            ViewBag.Duanlist = Duanlist;
            
            // L?y m� nh�n vi�n t? session d? di?n v�o form
            var maNv = HttpContext.Session.GetString("MaNguoidung");
            ViewBag.MaNguoidung = maNv;
            
            var allowedProjectCodes = _context.phieuxuatkho
                .Where(px => px.MaNguoidung == maNv && !string.IsNullOrEmpty(px.MaDuan))
                .Select(px => px.MaDuan)
                .Distinct()
                .ToList();

            if (allowedProjectCodes.Count > 0)
            {
                ViewBag.Duanlist = _context.duans
                    .Where(y => allowedProjectCodes.Contains(y.MaDuan))
                    .Select(y => (object)new { y.MaDuan, y.TrangThai })
                    .ToList();
            }
            else
            {
                ViewBag.Duanlist = new List<object>();
            }

            return View();
        }

        [HttpGet]
        public IActionResult GetCurrentUser()
        {
            var maNv = HttpContext.Session.GetString("MaNguoidung");
            return Json(new { maNguoidung = maNv });
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

                // Lưu vào trường riêng cho BP Mua hàng (Nhân viên mua hàng cũng thuộc BP Mua hàng)
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

                // Lưu ngày có hàng
                if (string.IsNullOrWhiteSpace(NgayCoHang))
                {
                    vt.NgayCoHang = null;
                }
                else
                {
                    if (DateTime.TryParse(NgayCoHang, out var dt))
                    {
                        vt.NgayCoHang = dt;
                    }
                    else
                    {
                        return Json(new { success = false, message = "Định dạng ngày có hàng không hợp lệ." });
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

        [HttpGet]
        public IActionResult GetDataKhoCaNhan()
        {
            var maNv = HttpContext.Session.GetString("MaNguoidung");
            if (string.IsNullOrEmpty(maNv))
            {
                return BadRequest("Không tìm thấy mã nhân viên");
            }

            // L?y d? li?u t? kho c� nh�n
            var khoCaNhanItems = _context.khonguoidungs
                .Where(k => k.NDMaNguoidung == maNv && (k.TrangThai == "Đang mượn" || k.TrangThai == "Đang sử dụng") && k.SL > 0)
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
            if (string.IsNullOrEmpty(maduan))
            {
                return Json(new
                {
                    maNguoidung = HttpContext.Session.GetString("MaNguoidung"),
                    maDuan = "",
                    vtPhieuMuaHang = new List<object>()
                });
            }

            // L?y m� nh�n vi�n t? session
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
                // Lấy vật tư từ vtphieuxuatkho (đã xuất kho) kết hợp với phieuxuatkho theo MaDuan
                // Các vật tư đã được xuất kho cho dự án này có thể được trả lại
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
                                   .Distinct() // Tr�nh tr�ng l?p n?u c�
                                   .ToList();

                // Debug info
                Console.WriteLine($"Querying vtphieuxuatkho for MaDuan = '{maduan}'");
                Console.WriteLine($"Found {khoDuanItems.Count} items");
                
                // Debug: Ki?m tra s? phi?u xu?t kho c� MaDuan n�y
                var phieuxuatCount = _context.phieuxuatkho.Count(p => p.MaDuan == maduan);
                Console.WriteLine($"Total phieuxuatkho records with MaDuan = '{maduan}': {phieuxuatCount}");

                return Json(new
                {
                    maNguoidung = maNv,
                    maDuan = maduan,
                    vtPhieuMuaHang = khoDuanItems,
                    debug = new
                    {
                        phieuxuatCount = phieuxuatCount,
                        returnedCount = khoDuanItems.Count
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetDataByMaDuan: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
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
                .Take(10) // Gi?i h?n 10 k?t qu? d? hi?u su?t t?t hon
                .ToList();

            var results = new List<object>();
            
            foreach (var product in products)
            {
                // L?y t?t c? nh cung c?p cho s?n ph?m ny t? b?ng SanPhamNhaCC
                var suppliers = _context.SanPhamNhaCC
                    .Where(s => s.MaSanpham == product.MaSanpham)
                    .Select(s => s.NhaCC)
                    .Distinct()
                    .ToList();

                // N?u c nh cung c?p trong b?ng SanPhamNhaCC, s? d?ng danh sch d
                if (suppliers.Any())
                {
                    // T?o m?t k?t qu? cho m?i nh cung c?p
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
                    // N?u khng c trong b?ng SanPhamNhaCC, s? d?ng nh cung c?p t? khotongs (t?ng thch ng?c)
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

                var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
                if (yeucau == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy yêu cầu." });
                }

                var chucVu = HttpContext.Session.GetString("Chucvu");
                var boPhan = HttpContext.Session.GetString("Bophan");

                // Nhân viên không thể duyệt, chỉ gửi yêu cầu với trạng thái "Chờ Trưởng BP-BP mua hàng duyệt"
                if (chucVu != "Nhân viên" || boPhan != "BP mua hàng")
                {
                    return Json(new { success = false, message = "Bạn không có quyền thực hiện thao tác này." });
                }

                int processedCount = 0;
                int skippedCount = 0;

                foreach (var item in vatTuList)
                {
                    var maSanpham = item.ContainsKey("MaSanpham") ? item["MaSanpham"]?.ToString() : null;
                    var isSelected = item.ContainsKey("IsApproved") && 
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

                    // Helper function để kiểm tra xem vật tư đã được gửi lên trưởng BP chưa
                    Func<string, bool> isAlreadySent = status =>
                    {
                        if (string.IsNullOrWhiteSpace(status))
                        {
                            return false;
                        }
                        var normalized = status.Trim();
                        return normalized.Equals("Chờ Trưởng BP-BP mua hàng duyệt", StringComparison.OrdinalIgnoreCase)
                            || normalized.StartsWith("Chờ Trưởng BP", StringComparison.OrdinalIgnoreCase)
                            || normalized == "Đã duyệt"
                            || normalized == "Đang mua hàng"
                            || normalized == "Đã xuất kho"
                            || normalized == "Đã nhận hàng"
                            || normalized == "Chờ giám đốc duyệt"
                            || normalized == "Chờ quản lý dự án duyệt";
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

                    // Chỉ xử lý các vật tư chưa được gửi lên trưởng BP và chưa bị từ chối
                    if (isAlreadySent(vatTu.TrangThai) || isAlreadyRejected(vatTu.TrangThai))
                    {
                        skippedCount++;
                        continue;
                    }

                    if (isSelected)
                    {
                        // Nhân viên chọn vật tư để gửi yêu cầu lên trưởng BP
                        vatTu.TrangThai = "Chờ Trưởng BP-BP mua hàng duyệt";
                        if (!string.IsNullOrWhiteSpace(ghiChu))
                        {
                            vatTu.GhiChu = ghiChu;
                        }
                    }
                    // Nếu không chọn, giữ nguyên trạng thái

                    _context.vtyeucau.Update(vatTu);
                    processedCount++;
                }

                // Cập nhật trạng thái yêu cầu nếu có ít nhất một vật tư được chọn
                if (yeucau != null && processedCount > 0)
                {
                    // Kiểm tra xem có vật tư nào đang chờ trưởng BP duyệt không
                    var hasPendingItems = _context.vtyeucau
                        .Any(v => v.VTMaYeucau == MaYeucau && 
                                 v.TrangThai == "Chờ Trưởng BP-BP mua hàng duyệt");

                    if (hasPendingItems && yeucau.TrangThai != "Chờ Trưởng BP-BP mua hàng duyệt")
                    {
                        yeucau.TrangThai = "Chờ Trưởng BP-BP mua hàng duyệt";
                        _context.yeucau.Update(yeucau);
                    }

                    // Gửi thông báo cho trưởng BP
                    try
                    {
                        _ = _emailService.SendNotificationToDepartmentHeadAsync(
                            yeucau.MaYeucau,
                            yeucau.NguoiYeucau ?? "",
                            "BP mua hàng"
                        );
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[NhanvienMuahang/XuLyVatTuYeucauWithCheckbox] Lỗi gửi email: {ex.Message}");
                    }
                }

                _context.SaveChanges();

                string message = $"Đã gửi {processedCount} vật tư lên trưởng BP thành công.";
                if (skippedCount > 0)
                {
                    message += $" ({skippedCount} vật tư đã được gửi trước đó hoặc không hợp lệ)";
                }

                return Json(new { success = true, message = message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        [HttpPost]
        public IActionResult ThemyeucauSQL(yeucau yeucau, vtyeucau vtyeucau,
                                           duans duans, phieunhapkho phieunhapkho, vtphieunhapkho vtphieunhapkho, List<string> YCMaKho,
                                           List<string> TenSanpham, List<string> MaSanpham,
                                           List<string> HangSX, List<string> NhaCC, List<int?> SL,
                                           List<string> DonVi, string MaYeucau, string action, phieuxuatkho phieuxuatkho, vtphieuxuatkho vtphieuxuatkho, phieumuahang phieumuahang, vtphieumuahang vtphieumuahang)
        {
            DateTime? GetNgayCanHangAt(int index)
            {
                if (Request.Form.TryGetValue("VTNgayCanHang", out var dateValues))
                {
                    if (index >= 0 && index < dateValues.Count)
                    {
                        if (DateTime.TryParse(dateValues[index], out var parsedDate))
                        {
                            return parsedDate;
                        }
                    }
                }

                return yeucau.NgayCanHang;
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
                        if (chucVu2 == "Nhân viên" && boPhan2 == "BP kỹ thuật")
                        {
                            yeucau.TrangThai = "Chờ Trưởng Phòng bộ phận BP kỹ thuật duyệt";
                        }
                        else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kỹ thuật")
                        {
                            yeucau.TrangThai = "Quản lý dự án";
                        }
                        else if (chucVu2 == "Nhân viên" && boPhan2 == "BP kho")
                        {
                            yeucau.TrangThai = "Chờ Trưởng Phòng bộ phận BP kho duyệt";
                        }
                        else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kho")
                        {
                            yeucau.TrangThai = "Quản lý dự án";
                        }
                        else if (chucVu2 == "Nhân viên" && boPhan2 == "BP mua hàng")
                        {
                            yeucau.TrangThai = "Chờ Trưởng BP-BP mua hàng duyệt";
                        }
                        else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP mua hàng")
                        {
                            yeucau.TrangThai = "Quản lý dự án";
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
                        yeucau.TrangThai = "Chờ Trưởng Phòng bộ phận BP kỹ thuật duyệt";
                    }
                    else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kỹ thuật")
                    {
                        yeucau.TrangThai = "Giám đốc";
                    }
                    else if (chucVu2 == "Nhân viên" && boPhan2 == "BP kho")
                    {
                        yeucau.TrangThai = "Chờ Trưởng Phòng bộ phận BP kho duyệt";
                    }
                    else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kho")
                    {
                        yeucau.TrangThai = "Giám đốc";
                    }
                    else if (chucVu2 == "Nhân viên" && boPhan2 == "BP mua hàng")
                    {
                        yeucau.TrangThai = "Chờ Trưởng BP-BP mua hàng duyệt";
                    }
                    else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP mua hàng")
                    {
                        yeucau.TrangThai = "Giám đốc";
                    }
                    else if (chucVu2 == "Giám đốc")
                    {
                        yeucau.TrangThai = "Đã duyệt";
                    }
                    else if (chucVu2 == "Nhân viên" && boPhan2 == "BP mua hàng")
                    {
                        // Fallback: Đảm bảo nhân viên BP mua hàng luôn có trạng thái này
                        yeucau.TrangThai = "Chờ Trưởng BP-BP mua hàng duyệt";
                    }
                }
                
                // Đảm bảo trạng thái luôn được set cho nhân viên BP mua hàng (kiểm tra sau tất cả các điều kiện)
                if (chucVu2 == "Nhân viên" && boPhan2 == "BP mua hàng" && (string.IsNullOrEmpty(yeucau.TrangThai) || yeucau.TrangThai == "Giám đốc"))
                {
                    yeucau.TrangThai = "Chờ Trưởng BP-BP mua hàng duyệt";
                }

                // ================== TẠO MÃ YÊU CẦU ĐÚNG CHUẨN ==================
                
                // Lấy mã sản phẩm (ST) từ tên file Excel hoặc từ form
                string? stPart = null;
                
                // Ưu tiên đọc từ tên file Excel nếu có
                if (Request.Form.Files != null && Request.Form.Files.Count > 0)
                {
                    var excelFile = Request.Form.Files.FirstOrDefault(f => 
                        f.Name == "excel-upload" || 
                        (!string.IsNullOrEmpty(f.FileName) && (f.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) || f.FileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))));
                    
                    if (excelFile != null && !string.IsNullOrEmpty(excelFile.FileName))
                    {
                        try
                        {
                            // Lấy tên file không có extension
                            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(excelFile.FileName);
                            
                            // Xử lý tên file có thể có dấu gạch dưới: thay thế bằng khoảng trắng để parse
                            fileNameWithoutExt = fileNameWithoutExt.Replace('_', ' ');
                            
                            // Parse tên file: 
                            // - "DS03-AAT-TASS-02" → lấy "DS03-AAT-TASS-02" (mã vật tư đầu tiên)
                            // - "251005 STUP10.5013 251203" → lấy "STUP10.5013" (bỏ qua số 6 chữ số)
                            // - "251208 DS03-AAT-TASS-02" → lấy "DS03-AAT-TASS-02" (bỏ qua số 6 chữ số)
                            // Tách theo khoảng trắng
                            var parts = fileNameWithoutExt.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            
                            // Tìm phần không phải số 6 chữ số (mã vật tư đầu tiên)
                                foreach (var part in parts)
                                {
                                // Bỏ qua các phần là số 6 chữ số (mã dự án hoặc ngày)
                                    if (part.Length == 6 && part.All(char.IsDigit))
                                    {
                                    continue;
                                    }
                                // Phần không phải số 6 chữ số là mã vật tư đầu tiên
                                        stPart = part;
                                        break;
                                    }
                                
                            // Nếu sau khi parse không tìm thấy (có thể tên file chỉ có số hoặc format khác),
                            // và chỉ có 1 phần duy nhất, lấy phần đó làm mã vật tư
                            if (string.IsNullOrWhiteSpace(stPart) && parts.Length == 1)
                                {
                                    stPart = parts[0];
                            }
                        }
                        catch (Exception ex)
                        {
                            // Nếu parse tên file lỗi, fallback về MaSanpham từ form
                            Console.WriteLine($"Lỗi khi parse tên file Excel để lấy mã sản phẩm: {ex.Message}");
                        }
                    }
                }
                
                // Nếu không đọc được từ tên file, lấy từ MaSanpham form
                if (string.IsNullOrWhiteSpace(stPart))
                {
                    if (MaSanpham != null && MaSanpham.Count > 0)
                    {
                        stPart = MaSanpham.FirstOrDefault(m => !string.IsNullOrWhiteSpace(m));
                    }
                }
                
                if (string.IsNullOrWhiteSpace(stPart))
                {
                    stPart = "VT";
                }
                stPart = stPart.Replace(" ", ""); // Bỏ dấu cách
                
                // Lấy tên viết tắt từ MaNguoidung (không dùng hàm parse)
                string tenVietTat = maNv2 ?? yeucau.YCMaNguoidung ?? "NGUOIDUNG";
                
                if (!string.IsNullOrEmpty(yeucau.YCMaDuan))
                {
                    // ===== Trường hợp có Mã Dự Án: NNNNNN ST HiepNT =====
                    // Lấy 6 chữ số từ mã dự án
                    string maDuanFormatted = "000000";
                    var maDuanDigits = new string(yeucau.YCMaDuan.Where(char.IsDigit).ToArray());
                    if (maDuanDigits.Length > 6)
                    {
                        maDuanFormatted = maDuanDigits.Substring(maDuanDigits.Length - 6);
                    }
                    else
                    {
                        maDuanFormatted = maDuanDigits.PadLeft(6, '0');
                    }
                    
                    // Tạo mã yêu cầu: NNNNNN ST HiepNT
                    yeucau.MaYeucau = $"{maDuanFormatted} {stPart} {tenVietTat}";
                    
                    // Luôn tạo yêu cầu mới - nếu trùng mã thì thêm suffix để tạo mã mới
                    int suffixNumber = 1;
                    string originalMaYeucau = yeucau.MaYeucau;
                    while (true)
                    {
                        var exists = _context.yeucau
                                             .FirstOrDefault(x => x.MaYeucau == yeucau.MaYeucau);
                        if (exists == null)
                        {
                            break;
                        }
                        // Nếu trùng, thêm số suffix để tạo mã mới
                        yeucau.MaYeucau = $"{originalMaYeucau}{suffixNumber}";
                        suffixNumber++;
                    }
                }
                else
                {
                    // ===== Trường hợp KHÔNG có Mã Dự Án: YYMMDD ST HiepNT =====
                    // Lấy ngày hiện tại dạng YYMMDD
                    string datePart = DateTime.Now.ToString("yyMMdd");
                    
                    // Tạo mã yêu cầu: YYMMDD ST HiepNT
                    yeucau.MaYeucau = $"{datePart} {stPart} {tenVietTat}";
                    
                    // Luôn tạo yêu cầu mới - nếu trùng mã thì thêm suffix để tạo mã mới
                    int suffixNumber = 1;
                    string originalMaYeucau = yeucau.MaYeucau;
                    while (true)
                    {
                        var exists = _context.yeucau
                                             .FirstOrDefault(x => x.MaYeucau == yeucau.MaYeucau);
                        if (exists == null)
                        {
                            break;
                        }
                        // Nếu trùng, thêm số suffix để tạo mã mới
                        yeucau.MaYeucau = $"{originalMaYeucau}{suffixNumber}";
                        suffixNumber++;
                    }
                }
                // ================================================================

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

                // Gửi thông báo cho trưởng phòng khi nhân viên tạo yêu cầu
                _ = Task.Run(async () =>
                {
                    System.Diagnostics.Debug.WriteLine($"[NV MuaHang] Gửi email Trưởng BP cho yêu cầu {yeucau.MaYeucau}");
                    await _emailService.SendNotificationToDepartmentHeadAsync(
                        yeucau.MaYeucau, 
                        yeucau.NguoiYeucau ?? "", 
                        yeucau.Bophan ?? ""
                    );
                    System.Diagnostics.Debug.WriteLine($"[NV MuaHang] Đã gọi xong SendNotificationToDepartmentHeadAsync cho {yeucau.MaYeucau}");
                });

                for (int i = 0; i < YCMaKho.Count; i++)
                {
                    if (string.IsNullOrEmpty(TenSanpham[i]))
                    {
                        continue;
                    }

                    // Tìm vật tư yêu cầu hiện có theo MaYeucau + MaSanpham
                    var existingVTYeucau = _context.vtyeucau
                        .FirstOrDefault(vt => vt.VTMaYeucau == yeucau.MaYeucau
                            && string.Equals(vt.MaSanpham, MaSanpham[i], StringComparison.OrdinalIgnoreCase));

                    var khoMatch = _context.khotongs.FirstOrDefault(p => p.Makho == YCMaKho[i]);
                    if (khoMatch != null)
                    {
                        // Tính số lượng mới
                        int slMoi = (SL != null && i < SL.Count) ? (SL[i] ?? 0) : 0;
                        
                        if (existingVTYeucau != null)
                        {
                            // Cập nhật vật tư yêu cầu hiện có
                            existingVTYeucau.TenSanpham = TenSanpham[i];
                            existingVTYeucau.TT = GetTTAt(i);
                            existingVTYeucau.HangSX = HangSX[i];
                            existingVTYeucau.NhaCC = NhaCC[i];
                            existingVTYeucau.SL = slMoi;
                            existingVTYeucau.DonVi = DonVi[i];
                            existingVTYeucau.NgayCanHang = GetNgayCanHangAt(i);
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
                            newVtyeucau.SL = slMoi;
                            newVtyeucau.DonVi = DonVi[i];
                            newVtyeucau.NgayCanHang = GetNgayCanHangAt(i);
                            newVtyeucau.YCMakho = khoMatch.Makho;
                            newVtyeucau.NgayNhapkho = khoMatch.NgayNhapkho;
                            newVtyeucau.NgayBaohanh = khoMatch.NgayBaohanh;
                            newVtyeucau.ThoiGianBH = khoMatch.ThoiGianBH;
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

                        // Tính số lượng mới
                        int slMoi = (SL != null && i < SL.Count) ? (SL[i] ?? 0) : 0;

                        // Kiểm tra xem vật tư này đã tồn tại trong yêu cầu chưa
                        if (existingVTYeucau != null)
                        {
                            // Cập nhật vật tư yêu cầu hiện có
                            existingVTYeucau.TenSanpham = TenSanpham[i];
                            existingVTYeucau.TT = GetTTAt(i);
                            existingVTYeucau.HangSX = HangSX[i];
                            existingVTYeucau.NhaCC = NhaCC[i];
                            existingVTYeucau.SL = slMoi;
                            existingVTYeucau.DonVi = DonVi[i];
                            existingVTYeucau.NgayCanHang = GetNgayCanHangAt(i);
                            existingVTYeucau.YCMakho = "VT mới";
                            existingVTYeucau.NgayNhapkho = null;
                            existingVTYeucau.NgayBaohanh = null;
                            existingVTYeucau.ThoiGianBH = null;
                            
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
                            newVtyeucau.SL = slMoi;
                            newVtyeucau.DonVi = DonVi[i];
                            newVtyeucau.NgayCanHang = GetNgayCanHangAt(i);
                            newVtyeucau.YCMakho = "VT mới";
                            newVtyeucau.NgayNhapkho = null;
                            newVtyeucau.NgayBaohanh = null;
                            newVtyeucau.ThoiGianBH = null;
                            _context.vtyeucau.Add(newVtyeucau);
                        }
                    }
                    _context.SaveChanges();
                }
                if (yeucau.TrangThai == "Đã duyệt")
                {
                    Xuliphieuyeucau(yeucau.MaYeucau, phieuxuatkho, vtphieuxuatkho, phieumuahang, vtphieumuahang, yeucau, vtyeucau);
                }
            }
            else
            {
                // Tạo mã phiếu nhập kho duy nhất bằng service
                phieunhapkho.MaNhapkho = _phieuCodeService.GenerateMaNhapKho(phieunhapkho.MaDuan, phieunhapkho.MaYeucau);
            }

            return RedirectToAction("Yeucau", "Yeucau", new { area = "NhanvienMuahang" });

        }
        [HttpPost]
        public IActionResult XuLyYeucau(string MaYeucau, string action, phieuxuatkho phieuxuatkho, vtphieuxuatkho vtphieuxuatkho, phieumuahang phieumuahang, vtphieumuahang vtphieumuahang, yeucau yeucau, vtyeucau vtyeucau)
        {
            var chucVu2 = HttpContext.Session.GetString("Chucvu");
            var boPhan2 = HttpContext.Session.GetString("Bophan");
            var maNv2 = HttpContext.Session.GetString("MaNguoidung");

            
            var Yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
            if (Yeucau == null)
            {
                
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
                                Yeucau.TrangThai = "Quản lý dự án";
                            }
                            else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kho")
                            {
                                Yeucau.TrangThai = "Quản lý dự án";
                            }
                            else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP mua hàng")
                            {
                                Yeucau.TrangThai = "Quản lý dự án";
                            }
                            else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kế toán")
                            {
                                Yeucau.TrangThai = "Quản lý dự án";
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
                }
                else if (chucVu2 == "Nhân viên" && boPhan2 == "BP kho")
                {
                    Yeucau.TrangThai = "Trưởng BP-BP kho";
                }
                else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kho")
                {
                    Yeucau.TrangThai = "Giám đốc";
                }
                else if (chucVu2 == "Nhân viên" && boPhan2 == "BP mua hàng")
                {
                    Yeucau.TrangThai = "Trưởng BP-BP mua hàng";
                }
                else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP mua hàng")
                {
                    Yeucau.TrangThai = "Giám đốc";
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

            return RedirectToAction("Yeucau", "Yeucau", new { area = "NhanvienMuahang" });
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
                return RedirectToAction("Yeucau", "Yeucau", new { area = "NhanvienMuahang" });
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
                if (DanhsachVTYCkhotong.Any(kt =>
                kt.SL > 0 && kt.Makho == VattuYC.YCMakho && kt.SL < VattuYC.SL))
                {
                    isPhieuMuaHangCreated = true;
                    isPhieuXuatKhoCreated = true;
                }
                else if (DanhsachVTYCkhotong.Any(kt =>
                kt.SL == 0 && kt.Makho == VattuYC.YCMakho && kt.SL < VattuYC.SL))
                {
                    isPhieuMuaHangCreated = true;
                }
                else
                {
                    isPhieuXuatKhoCreated = true;
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
                var khotong = _context.khotongs.FirstOrDefault(yc => yc.Makho == VattuYC.YCMakho);

                if (khotong != null && khotong.SL > 0)
                {
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

                    if (khotong.SL >= VattuYC.SL)
                    {
                        VTPhieuxuatkho.SL = VattuYC.SL;
                        // KH�NG tr? kho ? d�y - ch? tr? khi ngu?i nh?n x�c nh?n d� nh?n h�ng
                        VattuYC.TrangThai = "Đã duyệt";
                    _context.vtyeucau.Update(VattuYC);
                    }
                    else
                    {
                        VTPhieuxuatkho.SL = khotong.SL;
                        var SLThieu = VattuYC.SL - khotong.SL;
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
                        // KH�NG tr? kho ? d�y - ch? tr? khi ngu?i nh?n x�c nh?n d� nh?n h�ng
                    }
                    // KHNG c?p nh?t khotong ? dy - ch? c?p nh?t khi ngu?i nh?n xc nh?n d nh?n hng
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


            return RedirectToAction("Yeucau", "Yeucau", new { area = "NhanvienMuahang" });
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

                // Gửi thông báo cho người yêu cầu khi xuất kho
                var maYeucauListForNotif = VTphieuxuatkho
                    .Select(v => v.MaYeucau)
                    .Where(ma => !string.IsNullOrEmpty(ma))
                    .Distinct()
                    .ToList();

                foreach (var maYc in maYeucauListForNotif)
                {
                    _ = Task.Run(async () =>
                    {
                        System.Diagnostics.Debug.WriteLine($"[NV MuaHang] Gửi email cho người yêu cầu khi xuất kho. MaYeucau = {maYc}, MaXuatkho = {MaXuatkho}");
                        await _emailService.SendNotificationToRequesterOnIssueAsync(maYc, MaXuatkho);
                        System.Diagnostics.Debug.WriteLine($"[NV MuaHang] Đã gọi xong SendNotificationToRequesterOnIssueAsync cho MaYeucau = {maYc}");
                    });
                }

                // Gửi email thông báo cho bộ phận kho khi xuất kho
                if (maYeucauListForNotif.Any())
                {
                    _ = Task.Run(async () =>
                    {
                        System.Diagnostics.Debug.WriteLine($"[NV MuaHang] Gửi email thông báo kho khi xuất kho. MaXuatkho = {MaXuatkho}");
                        await _emailService.SendNotificationToWarehouseOnXuatKhoAsync(MaXuatkho, maYeucauListForNotif.First());
                        System.Diagnostics.Debug.WriteLine($"[NV MuaHang] Đã gọi xong SendNotificationToWarehouseOnXuatKhoAsync cho MaXuatkho = {MaXuatkho}");
                    });
                }
            }
            else if (Phieuxuatkho.TrangThai == "Đã lấy hàng")
            {
                Phieuxuatkho.TrangThai = "Hoàn thành";
                _context.phieuxuatkho.Update(Phieuxuatkho);
                _context.SaveChanges();
            }
                return RedirectToAction("Phieuxuatkho", "Yeucau", new { area = "NhanvienMuahang" });
        }

        [HttpPost]
        public IActionResult ThemPhieumuahangSQL([FromBody] Phieumuahangviewmodel model)
        {
            try
            {
                var MaMuahang = model.MaMuahang;
                Console.WriteLine($"MaMuahang nh?n du?c: {MaMuahang}");

                var Phieumuahang = _context.phieumuahang
                                            .FirstOrDefault(y => y.MaMuahang == MaMuahang);
                if (Phieumuahang == null)
                {
                    Console.WriteLine("Không tìm thấy Phieumuahang.");
                    return Json(new { success = false, message = "Mã mua hàng không tồn tại!" });
                }

                Phieumuahang.TrangThai = "Đang chờ báo giá";
                _context.phieumuahang.Update(Phieumuahang);

                var VTPhieumuahanglist = _context.vtphieumuahang
                                                  .Where(kt => kt.MaMuahang == MaMuahang)
                                                  .ToList();

                Console.WriteLine($"Số vật tư được tìm thấy: {VTPhieumuahanglist.Count}");
                Console.WriteLine($"Số lượng phần tử trong VTphieumuahang: {model.VTphieumuahang?.Count ?? 0}");

                // Validation: Kiểm tra các vật tư có SL = 1 phải có đơn giá
                var missingData = new List<string>();
                for (int i = 0; i < VTPhieumuahanglist.Count; i++)
                {
                    var VTmuahang = VTPhieumuahanglist[i];
                    
                    // Nếu số lượng = 0, không cần nhập, bỏ qua
                    if (VTmuahang.SL == 0)
                    {
                        continue;
                    }
                    
                    // Nếu số lượng = 1, bắt buộc phải có đơn giá
                    if (VTmuahang.SL == 1)
                    {
                        bool hasPrice = false;
                        if (model.VTphieumuahang != null && i < model.VTphieumuahang.Count)
                        {
                            var updatedVTmuahang = model.VTphieumuahang[i];
                            if (updatedVTmuahang.DonGia != null && updatedVTmuahang.DonGia > 0)
                            {
                                hasPrice = true;
                            }
                        }
                        
                        if (!hasPrice)
                        {
                            missingData.Add(VTmuahang.TenSanpham ?? VTmuahang.MaSanpham ?? $"Vật tư {i + 1}");
                        }
                    }
                }
                
                if (missingData.Count > 0)
                {
                    return Json(new { success = false, message = "Bạn chưa nhập xong hết số liệu. Vui lòng nhập đơn giá cho các vật tư có số lượng = 1:\n" + string.Join("\n", missingData) });
                }

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
                Console.WriteLine($"Mã mua hàng nhận được: {MaMuahang}");
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
                            // Cập nhật tất cả các mục có trạng thái "Chờ thanh toán" thành "Đã thanh toán"
                            if (VTPhieumuahang.TrangThai == "Chờ thanh toán")
                            {
                                VTPhieumuahang.TrangThai = "Đã thanh toán";
                            }
                        }
                        else
                        {
                            VTPhieumuahang.TrangThai = "Đã nhận hàng";
                        }
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
                return RedirectToAction("Phieumuahang", "Yeucau", new { area = "NhanvienMuahang" });
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

                return RedirectToAction("Phieumuahang", "Yeucau", new { area = "NhanvienMuahang" });
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
            // Lưu session ngay từ đầu để đảm bảo không bị mất khi có exception
            var maNv = HttpContext.Session.GetString("MaNguoidung");
            if (string.IsNullOrEmpty(maNv))
            {
                TempData["Error"] = "Session đã hết hạn. Vui lòng đăng nhập lại!";
                return RedirectToAction("Login", "Home", new { area = "" });
            }
            
            // Lưu area để dùng trong catch block
            string currentArea = "NhanvienMuahang";
            
            try
            {

                // Kiểm tra dữ liệu đầu vào
                if (TenSanpham == null || TenSanpham.Length == 0)
                {
                    TempData["Error"] = "Vui lòng nhập ít nhất một vật tư!";
                    return RedirectToAction("ThemPhieunhapkho", "Yeucau", new { area = "NhanvienMuahang" });
                }

                if (string.IsNullOrEmpty(LoaiNhapkho))
                {
                    TempData["Error"] = "Vui lòng chọn loại nhập kho!";
                    return RedirectToAction("ThemPhieunhapkho", "Yeucau", new { area = "NhanvienMuahang" });
                }

                // maNv đã được lấy ở trên (ngoài try block để đảm bảo không bị mất)
                if (string.IsNullOrEmpty(phieunhapkho.MaNguoidung))
                {
                    phieunhapkho.MaNguoidung = maNv;
                }

                // Tính toán số lượng các phần tử
                int count = TenSanpham.Length;

                // Tạo mã phiếu nhập kho duy nhất bằng service
                phieunhapkho.MaNhapkho = _phieuCodeService.GenerateMaNhapKho(phieunhapkho.MaDuan, phieunhapkho.MaYeucau);
                phieunhapkho.NgayNhapkho = DateTime.Now;
                
                // Thiết lập trạng thái ban đầu theo quy trình duyệt
                // Nếu có dự án: gửi đến Trưởng dự án
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

                // Mã yêu cầu nhập kho: MãDựÁnNK YYMMDD-01 hoặc MãNhânViênNK YYMMDD-01 (PhieuCodeService)
                if (string.IsNullOrEmpty(phieunhapkho.MaYeucau))
                {
                    string maDuanForYc = (LoaiNhapkho == "duan" && !string.IsNullOrEmpty(phieunhapkho.MaDuan)) ? phieunhapkho.MaDuan : null;
                    string maYeucauDacBiet = _phieuCodeService.GenerateMaYeucauNhapKho(maDuanForYc, maNv);

                    string ycMaDuan = null;
                    if (!string.IsNullOrEmpty(phieunhapkho.MaDuan))
                    {
                        var duanExists = _context.duans.FirstOrDefault(d => d.MaDuan == phieunhapkho.MaDuan);
                        if (duanExists == null) duanExists = _context.duans.AsEnumerable().FirstOrDefault(d => d.MaDuan != null && d.MaDuan.Equals(phieunhapkho.MaDuan, StringComparison.OrdinalIgnoreCase));
                        if (duanExists != null) { ycMaDuan = duanExists.MaDuan; Console.WriteLine($"Found project: '{duanExists.MaDuan}' for input '{phieunhapkho.MaDuan}'"); }
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

                // LƯU Ý QUAN TRỌNG: KHÔNG trừ từ kho dự án/cá nhân ngay khi tạo phiếu nhập kho
                // Chỉ trừ khi kho duyệt phiếu nhập kho (trong Xuliphieunhapkho)
                // Vì nếu trừ ngay thì sẽ thiệt hại kho nếu phiếu bị từ chối hoặc chưa được duyệt
                
                // Xử lý vật tư - chỉ tạo bản ghi, không trừ từ kho
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
                        MaYeucau = phieunhapkho.MaYeucau // Dùng cùng MaYeucau với phieunhapkho
                    };

                    _context.vtphieunhapkho.Add(newvtphieunhapkho);
                }

                // Save changes sau khi xử lý tất cả vật tư
                try
                {
                    _context.SaveChanges();
                }
                catch (Exception exSave)
                {
                    Console.WriteLine($"Error saving changes: {exSave.Message}");
                    Console.WriteLine($"Stack trace: {exSave.StackTrace}");
                    throw; // Re-throw để catch block bên ngoài xử lý
                }

                // Gửi email thông báo theo luồng duyệt phiếu nhập kho (giống yêu cầu)
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
                            System.Diagnostics.Debug.WriteLine($"[NhanvienMuahang/ThemPhieunhapkhoSQL] Lỗi gửi email tạo phiếu nhập kho: {exInner.Message}");
                        }
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[NhanvienMuahang/ThemPhieunhapkhoSQL] Lỗi khởi chạy task gửi email: {ex.Message}");
                }

                TempData["Success"] = "Tạo phiếu nhập kho thành công!";
                return RedirectToAction("Phieunhapkho", "Yeucau", new { area = "NhanvienMuahang" });
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
                
                // Kiểm tra session - dùng biến maNv đã lưu trước đó thay vì lấy lại
                // Vì có thể exception làm mất session, nhưng nếu maNv đã được lưu thì vẫn dùng được
                var maNvCheck = HttpContext.Session.GetString("MaNguoidung") ?? maNv;
                Console.WriteLine($"Session MaNguoidung after error: {maNvCheck ?? "NULL"}");
                Console.WriteLine($"Original maNv (from before try): {maNv ?? "NULL"}");
                
                // Luôn redirect về trang tạo phiếu với thông báo lỗi
                // Không redirect về login trừ khi thực sự không có session từ đầu
                TempData["Error"] = $"Có lỗi xảy ra khi xử lý: {ex.Message}. Vui lòng kiểm tra lại dữ liệu hoặc liên hệ admin.";
                
                // Luôn redirect về trang tạo phiếu để người dùng có thể thử lại
                // Chỉ redirect về login nếu thực sự không có maNv từ đầu
                if (!string.IsNullOrEmpty(maNv))
                {
                    return RedirectToAction("ThemPhieunhapkho", "Yeucau", new { area = currentArea });
                }
                else
                {
                    // Trường hợp này chỉ xảy ra nếu session đã hết hạn từ đầu (đã check ở trên)
                    TempData["Error"] = "Session d� h?t h?n. Vui l�ng dang nh?p l?i!";
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
            
            // L?y th�ng tin d? �n (n?u c�)
            var duan = !string.IsNullOrEmpty(Phieunhapkho.MaDuan) 
                ? _context.duans.FirstOrDefault(d => d.MaDuan == Phieunhapkho.MaDuan) 
                : null;

            if (action == "approve")
            {
                // Workflow duy?t:
                // 1. "Qu?n l� d? �n" (n?u c� d? �n) -> Tru?ng d? �n duy?t -> "Giám đốc"
                // 2. "Giám đốc" -> Giám đốc duy?t -> "Ch? nh?p kho"
                // 3. "Ch? nh?p kho" -> Kho x? l� -> "�� nh?p kho" v� c?ng v�o kho t?ng

                if (Phieunhapkho.TrangThai == "Quản lý dự án")
                {
                    // Tru?ng d? �n duy?t
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
                else if (Phieunhapkho.TrangThai == "Giám đốc")
                {
                    // Giám đốc duy?t
                    if (chucVu2 == "Giám đốc")
                    {
                        Phieunhapkho.TrangThai = "Chờ nhập kho";
                        foreach (var vt in VTPhieunhapkholist)
                        {
                            vt.TrangThai = "Chờ nhập kho";
                            _context.vtphieunhapkho.Update(vt);
                        }
                    }
                }
                else if (Phieunhapkho.TrangThai == "Chờ nhập kho" && boPhan2 == "BP kho")
                {
                    // Kho x? l� nh?p kho
                    // LUU � QUAN TR?NG: Khi kho duy?t, CH? c?ng v�o kho t?ng
                    // KH�NG tr? t? kho d? �n/c� nh�n ? d�y
                    // Ch? tr? khi ngu?i nh?n x�c nh?n nh?n h�ng (tr?ng th�i "Đã xác nhận nhận hàng")
                    Phieunhapkho.TrangThai = "Đã nhập kho";
                    
                    foreach (var VTPhieunhapkho in VTPhieunhapkholist)
                    {
                        // C?ng v�o kho t?ng (cho c? phi?u t? mua h�ng v� phi?u t? d? �n/c� nh�n)
                        var khotong = _context.khotongs.FirstOrDefault(k => 
                            k.TenSanpham == VTPhieunhapkho.TenSanpham && 
                            k.MaSanpham == VTPhieunhapkho.MaSanpham && 
                            k.HangSX == VTPhieunhapkho.HangSX &&
                            k.Makho == VTPhieunhapkho.Makho);
                            
                        if (khotong != null)
                        {
                            // C?ng s? lu?ng v�o t?n kho
                            khotong.SL += VTPhieunhapkho.SL ?? 0;
                            _context.khotongs.Update(khotong);
                        }
                        else
                        {
                            // T?o m?i v?t tu trong t?n kho n?u chua c�
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
                        
                        VTPhieunhapkho.TrangThai = "Đã nhập kho";
                        _context.vtphieunhapkho.Update(VTPhieunhapkho);
                    }
                    
                    // T? d?ng t?o phi?u xu?t kho n?u c� y�u c?u ban d?u v� chua c� phi?u xu?t kho
                    // Logic n�y �p d?ng cho C? v?t tu d? �n V� v?t tu c� nh�n
                    if (!string.IsNullOrEmpty(Phieunhapkho.MaYeucau))
                    {
                        // Ki?m tra xem d� c� phi?u xu?t kho cho y�u c?u n�y chua
                        var existingPhieuxuatkho = _context.phieuxuatkho
                            .FirstOrDefault(px => px.MaYeucau == Phieunhapkho.MaYeucau);
                        
                        if (existingPhieuxuatkho == null)
                        {
                            // L?y th�ng tin y�u c?u ban d?u
                            var yeucauBanDau = _context.yeucau
                                .FirstOrDefault(y => y.MaYeucau == Phieunhapkho.MaYeucau);
                            
                            if (yeucauBanDau != null)
                            {
                                // T?o m� phi?u xu?t kho duy nh?t
                                // Tạo mã phiếu xuất kho duy nhất bằng service
                                string MaXuatkho = _phieuCodeService.GenerateMaXuatKho(Phieunhapkho.MaDuan, Phieunhapkho.MaYeucau);
                                
                                // Tạo phiếu xuất kho
                                var newPhieuxuatkho = new phieuxuatkho
                                {
                                    MaXuatkho = MaXuatkho,
                                    MaYeucau = Phieunhapkho.MaYeucau,
                                    MaDuan = Phieunhapkho.MaDuan,
                                    MaNguoidung = Phieunhapkho.MaNguoidung,
                                    NgayXuatkho = null,
                                    NgayChuanBi = DateTime.Now,
                                    TrangThai = "Chờ xác nhận"
                                };
                                _context.phieuxuatkho.Add(newPhieuxuatkho);
                                _context.SaveChanges();
                                
                                // L?y danh s�ch v?t tu y�u c?u ban d?u
                                var danhSachVatTuYC = _context.vtyeucau
                                    .Where(vt => vt.VTMaYeucau == Phieunhapkho.MaYeucau)
                                    .ToList();
                                
                                // T?o v?t tu trong phi?u xu?t kho d?a tr�n v?t tu trong phi?u nh?p kho
                                foreach (var VTPhieunhapkho in VTPhieunhapkholist)
                                {
                                    // T�m v?t tu tuong ?ng trong y�u c?u ban d?u
                                    var vtYeucau = danhSachVatTuYC.FirstOrDefault(vt => 
                                        vt.MaSanpham == VTPhieunhapkho.MaSanpham &&
                                        vt.YCMakho == VTPhieunhapkho.Makho);
                                    
                                    if (vtYeucau != null)
                                    {
                                        // L?y th�ng tin t? kho t?ng d? d?m b?o d�ng th�ng tin
                                        var khotong = _context.khotongs.FirstOrDefault(k => 
                                            k.MaSanpham == VTPhieunhapkho.MaSanpham &&
                                            k.Makho == VTPhieunhapkho.Makho);
                                        
                                        if (khotong != null)
                                        {
                                            // T�nh s? lu?ng xu?t kho (l?y s? lu?ng nh? nh?t gi?a y�u c?u v� s? lu?ng nh?p)
                                            int slXuat = Math.Min(vtYeucau.SL ?? 0, VTPhieunhapkho.SL ?? 0);
                                            
                                            var newVTPhieuxuatkho = new vtphieuxuatkho
                                            {
                                                MaXuatkho = MaXuatkho,
                                                MaYeucau = VTPhieunhapkho.MaYeucau,
                                                TenSanpham = khotong.TenSanpham,
                                                MaSanpham = khotong.MaSanpham,
                                                Makho = khotong.Makho,
                                                HangSX = khotong.HangSX,
                                                NhaCC = khotong.NhaCC,
                                                DonVi = khotong.DonVi,
                                                SL = slXuat,
                                                NgayBaohanh = khotong.NgayBaohanh,
                                                ThoiGianBH = khotong.ThoiGianBH,
                                                TrangThai = "Chờ xác nhận"
                                            };
                                            _context.vtphieuxuatkho.Add(newVTPhieuxuatkho);
                                        }
                                    }
                                }
                                
                                _context.SaveChanges();
                                
                                // Sau khi t?o phi?u xu?t kho, ki?m tra t?n kho v� t? d?ng chuy?n tr?ng th�i nhu phi?u xu?t kho co b?n
                                var VTPhieuxuatkhoList = _context.vtphieuxuatkho
                                    .Where(vt => vt.MaXuatkho == MaXuatkho)
                                    .ToList();
                                
                                bool duHang = true;
                                var vatTuThieu = new List<vtphieuxuatkho>();
                                
                                foreach (var VTxuatkho in VTPhieuxuatkhoList)
                                {
                                    var khotong = _context.khotongs.FirstOrDefault(k => k.Makho == VTxuatkho.Makho && k.MaSanpham == VTxuatkho.MaSanpham);
                                    // Ki?m tra ch?t ch?: kh�ng c� h�ng, s? lu?ng = 0, ho?c kh�ng d? s? lu?ng ? kh�ng cho xu?t
                                    // Tính số lượng hàng đã cam kết (đã duyệt nhưng chưa giao)
                                    int soLuongDaCamKet = TinhSoLuongDaCamKet(VTxuatkho.Makho ?? "", VTxuatkho.MaSanpham ?? "", MaXuatkho);
                                    
                                    // Số lượng khả dụng = Tồn kho - Số lượng đã cam kết
                                    int soLuongKhaDung = (khotong?.SL ?? 0) - soLuongDaCamKet;
                                    
                                    // Kiểm tra chặt chẽ: không có hàng, số lượng khả dụng <= 0, hoặc không đủ số lượng → không cho xuất
                                    if (khotong == null || soLuongKhaDung <= 0 || soLuongKhaDung < VTxuatkho.SL)
                                    {
                                        duHang = false;
                                        vatTuThieu.Add(VTxuatkho);
                                    }
                                }
                                
                                if (duHang)
                                {
                                    
                                    newPhieuxuatkho.TrangThai = "Đang chuẩn bị hàng";
                                    newPhieuxuatkho.NgayChuanBi = DateTime.Now;
                                    _context.phieuxuatkho.Update(newPhieuxuatkho);
                                    
                                    // C?p nh?t tr?ng th�i v?t tu
                                    foreach (var VTxuatkho in VTPhieuxuatkhoList)
                                    {
                                        VTxuatkho.TrangThai = "Đang chuẩn bị hàng";
                                        _context.vtphieuxuatkho.Update(VTxuatkho);
                                    }
                                    
                                    _context.SaveChanges();
                                    Console.WriteLine($"Đã tự động tạo phiếu xuất kho {MaXuatkho} cho yêu cầu {Phieunhapkho.MaYeucau} và chuyển sang trạng thái 'Đang chuẩn bị hàng'");
                                }
                                else
                                {
                                    
                                    newPhieuxuatkho.TrangThai = "Thiếu hàng";
                                    newPhieuxuatkho.GhiChu = "Không đủ số lượng tồn kho.";
                                    _context.phieuxuatkho.Update(newPhieuxuatkho);
                                    _context.SaveChanges();
                                    Console.WriteLine($"Đã tự động tạo phiếu xuất kho {MaXuatkho} cho yêu cầu {Phieunhapkho.MaYeucau} nhưng thiếu hàng");
                                }
                            }
                        }
                        // else
                        // {
                        //     // Đã có phiếu xuất (có thể đã xuất một phần) → bơm thêm hàng vừa nhập vào phiếu hiện có
                        //     PhieuXuatAllocationHelper.CapNhatPhieuXuatSauNhapHang(_context, existingPhieuxuatkho, VTPhieunhapkholist);
                        // }
                    }
                }
                else if (Phieunhapkho.TrangThai == "Đã nhập kho" && boPhan2 == "BP kho")
                {
                    // Khi tr?ng th�i l� "�� nh?p kho" v� ngu?i nh?n x�c nh?n nh?n h�ng
                    // M?I tr? t? kho d? �n/c� nh�n (s?n lu?ng th?a du?c tr? l?i)
                    Phieunhapkho.TrangThai = "Đã xác nhận nhận hàng";
                    
                    foreach (var VTPhieunhapkho in VTPhieunhapkholist)
                    {
                        // Tr? t? kho d? �n/c� nh�n khi ngu?i nh?n x�c nh?n nh?n h�ng
                        bool isFromDuanOrCaNhan = false;
                        
                        // Ki?m tra t? d? �n: N?u c� MaDuan v� c� v?t tu trong kho d? �n
                        if (!string.IsNullOrEmpty(Phieunhapkho.MaDuan) && !string.IsNullOrEmpty(VTPhieunhapkho.MaSanpham))
                        {
                            var vtXuatKhoItems = (from vt in _context.vtphieuxuatkho
                                                  join px in _context.phieuxuatkho on vt.MaXuatkho equals px.MaXuatkho
                                                  where px.MaDuan == Phieunhapkho.MaDuan
                                                     && vt.MaSanpham == VTPhieunhapkho.MaSanpham
                                                     && (vt.TrangThai == "Đã xác nhận nhận hàng" 
                                                         || vt.TrangThai == "Đã lấy hàng"
                                                         || vt.TrangThai == "Đã xuất kho")
                                                     && (vt.SL ?? 0) > 0
                                                  orderby vt.ID ascending
                                                  select vt)
                                                  .ToList();
                            
                            if (vtXuatKhoItems.Any())
                            {
                                isFromDuanOrCaNhan = true;
                                // Tr? t? kho d? �n
                                int slCanTra = VTPhieunhapkho.SL ?? 0;
                                foreach (var vtItem in vtXuatKhoItems)
                                {
                                    if (slCanTra <= 0) break;
                                    
                                    int slHienTai = vtItem.SL ?? 0;
                                    int slTru = Math.Min(slHienTai, slCanTra);
                                    vtItem.SL = slHienTai - slTru;
                                    
                                    if ((vtItem.SL ?? 0) <= 0)
                                    {
                                        vtItem.TrangThai = "Đã trả kho";
                                    }
                                    
                                    _context.vtphieuxuatkho.Update(vtItem);
                                    slCanTra -= slTru;
                                }
                            }
                        }
                        
                        // Ki?m tra t? c� nh�n: N?u kh�ng c� MaDuan v� c� v?t tu trong kho c� nh�n
                        if (!isFromDuanOrCaNhan && string.IsNullOrEmpty(Phieunhapkho.MaDuan) && !string.IsNullOrEmpty(VTPhieunhapkho.MaSanpham) && !string.IsNullOrEmpty(Phieunhapkho.MaNguoidung))
                        {
                            var khoCaNhanItem = _context.khonguoidungs
                                .FirstOrDefault(k => k.NDMaNguoidung == Phieunhapkho.MaNguoidung 
                                                   && k.MaSanpham == VTPhieunhapkho.MaSanpham 
                                                   && (k.TrangThai == "Đang mượn" || k.TrangThai == "Đang sử dụng")
                                                   && (k.SL ?? 0) >= (VTPhieunhapkho.SL ?? 0));
                            
                            if (khoCaNhanItem != null)
                            {
                                isFromDuanOrCaNhan = true;
                                // Tr? t? kho c� nh�n
                                khoCaNhanItem.SL -= VTPhieunhapkho.SL ?? 0;
                                if (khoCaNhanItem.SL <= 0)
                                {
                                    khoCaNhanItem.TrangThai = "Đã trả";
                                }
                                _context.khonguoidungs.Update(khoCaNhanItem);
                            }
                        }
                        
                        VTPhieunhapkho.TrangThai = "Đã xác nhận nhận hàng";
                        _context.vtphieunhapkho.Update(VTPhieunhapkho);
                    }
                }
                else if (Phieunhapkho.TrangThai == "Đã xác nhận nhận hàng")
                {
                    // Ho�n th�nh phi?u nh?p kho
                    Phieunhapkho.TrangThai = "Hoàn thành";
                    
                    foreach (var VTPhieunhapkho in VTPhieunhapkholist)
                    {
                        VTPhieunhapkho.TrangThai = "Hoàn thành";
                        _context.vtphieunhapkho.Update(VTPhieunhapkho);
                    }
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
                        System.Diagnostics.Debug.WriteLine($"[NhanvienMuahang/Xuliphieunhapkho] Lỗi gửi email nhập kho cho người yêu cầu: {ex.Message}");
                    }
                }
            }
            else if (action == "reject")
            {
                Phieunhapkho.TrangThai = $"{chucVu2} - đã từ chối";
                foreach (var vt in VTPhieunhapkholist)
                {
                    vt.TrangThai = $"{chucVu2} - đã từ chối";
                    _context.vtphieunhapkho.Update(vt);
                }
                _context.phieunhapkho.Update(Phieunhapkho);
            }
            
            _context.SaveChanges();
            return RedirectToAction("Phieunhapkho", "Yeucau", new { area = "NhanvienMuahang" });
        }

        [HttpPost]
        public IActionResult Taophieuxuatkhobyphieunhapkho(string MaNhapkho, phieuxuatkho phieuxuatkho, vtphieuxuatkho vtphieuxuatkho, phieunhapkho phieunhapkho, vtphieunhapkho vtphieunhapkho)
        {
            var Phieunhapkho = _context.phieunhapkho.FirstOrDefault(p => p.MaNhapkho == MaNhapkho);
            var VTPhieunhapkholist = _context.vtphieunhapkho.Where(vt => vt.MaNhapkho == MaNhapkho).ToList();

            // T?o m� phi?u nh?p kho duy nh?t
            // Tạo mã phiếu xuất kho duy nhất bằng service
            string MaXuatkho = _phieuCodeService.GenerateMaXuatKho(Phieunhapkho?.MaDuan, Phieunhapkho?.MaYeucau);

            var newphieuxuatkho = new phieuxuatkho
            {
                MaXuatkho = MaXuatkho,
                MaYeucau = Phieunhapkho.MaYeucau,
                MaDuan = Phieunhapkho.MaDuan,
                MaNguoidung = Phieunhapkho.MaNguoidung,
                NgayXuatkho = DateTime.Now,
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

            return RedirectToAction("Phieunhapkho", "Yeucau", new { area = "NhanvienMuahang" });
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
                    Phieu.TrangThai = $"{chucVu} - đã từ chối";
                    _context.yeucau.Update(Phieu);

                    var Listvtyeucau = _context.vtyeucau.Where(p => p.VTMaYeucau == Ma).ToList();
                    foreach (var VTyeucau in Listvtyeucau)
                    {
                        VTyeucau.TrangThai = $"{chucVu} - đã từ chối";
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
                    Phieumuahang.TrangThai = $"{chucVu} - đã từ chối";
                    _context.phieumuahang.Update(Phieumuahang);

                    var Listvtmuahang = _context.vtphieumuahang.Where(p => p.MaMuahang == Ma).ToList();
                    foreach (var VTmuahang in Listvtmuahang)
                    {
                        VTmuahang.TrangThai = $"{chucVu} - đã từ chối";
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

            return RedirectToAction("Yeucau", "Yeucau", new { area = "NhanvienMuahang" });
        }

        public IActionResult XacnhanNhanHang()
        {
            var currentUserId = HttpContext.Session.GetString("MaNguoidung");

            // L?y c�c y�u c?u m� k? thu?t vi�n n�y d� t?o
            var yeuCauList = _context.yeucau
                .Where(y => y.YCMaNguoidung == currentUserId)
                .Select(y => y.MaYeucau)
                .ToList();

            // L?y phi?u xu?t kho li�n quan t?i c�c y�u c?u d�
            // Hi?n th? c? phi?u dang ch? x�c nh?n v� d� x�c nh?n
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
        // X�C NH?N H�NG
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

                // ? C?p nh?t tr?ng th�i v?t tu trong phi?u xu?t kho
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

                    int slYeuCau = vt.SL ?? 0;
                    if (slYeuCau <= 0)
                    {
                        continue;
                    }

                    // Cập nhật trạng thái vật tư thành "Đã xác nhận nhận hàng"
                    vt.TrangThai = "Đã xác nhận nhận hàng";
                    vt.NgayNhapkho = DateTime.Now;
                    _context.vtphieuxuatkho.Update(vt);
                    
                    // Tr? kho t?ng khi x�c nh?n nh?n h�ng - KI?M TRA CH?T CH? S? LU?NG
                    var khotong = _context.khotongs.FirstOrDefault(k => k.Makho == vt.Makho && k.MaSanpham == vt.MaSanpham);
                    if (khotong != null)
                    {
                        // TUYỆT ĐỐI KHÔNG cho phép xuất nếu hết hàng hoặc không đủ số lượng
                        // Tính số lượng hàng đã cam kết từ các phiếu xuất khác (loại trừ phiếu hiện tại vì nó đang được xác nhận)
                        int soLuongDaCamKetKhac = TinhSoLuongDaCamKet(vt.Makho ?? "", vt.MaSanpham ?? "", MaXuatkho);
                        
                        // Số lượng khả dụng = Tồn kho - Số lượng đã cam kết từ các phiếu khác
                        int soLuongKhaDung = (khotong.SL ?? 0) - soLuongDaCamKetKhac;
                        
                        // TUYỆT ĐỐI KHÔNG cho phép xuất nếu hết hàng hoặc không đủ số lượng
                        if (soLuongKhaDung <= 0 || soLuongKhaDung < slYeuCau)
                        {
                            TempData["ErrorMessage"] = $"Không thể xuất kho: Vật tư {vt.TenSanpham} không đủ số lượng trong kho (Tồn kho: {khotong.SL}, Yêu cầu: {slYeuCau})";
                            return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "NhanvienMuahang" });
                        }
                        
                        khotong.SL = (khotong.SL ?? 0) - slYeuCau;
                        _context.khotongs.Update(khotong);
                    }
                    else
                    {
                        TempData["ErrorMessage"] = $"Không thể xuất kho: Vật tư {vt.TenSanpham} không tồn tại trong kho";
                        return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "NhanvienMuahang" });
                    }
                    
                    // ch? x? l� n?u phi?u n�y kh�ng c� d? �n
                    if (string.IsNullOrEmpty(phieu.MaDuan))
                    {
                        var existingItem = _context.khonguoidungs
                            .FirstOrDefault(k => k.NDMaNguoidung == phieu.MaNguoidung && k.MaSanpham == vt.MaSanpham);

                        if (existingItem != null)
                        {
                            existingItem.SL = (existingItem.SL ?? 0) + slYeuCau;
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
                                SL = slYeuCau,
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
                return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "NhanvienMuahang" });
            }

            TempData["ErrorMessage"] = "Phiếu không hợp lệ hoặc đã được xác nhận!";
            return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "NhanvienMuahang" });
        }

        // Đồng bộ lại trạng thái vật tư cho các phiếu đã xác nhận nhận hàng
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

        // Action tự động đồng bộ khi load trang (gọi từ JavaScript)
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

