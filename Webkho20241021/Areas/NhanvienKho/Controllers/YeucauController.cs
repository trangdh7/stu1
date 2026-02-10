using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.Json;
using Webkho_20241021.Areas.NhanvienKho.Data;
using Webkho_20241021.Models;
using Webkho_20241021.Services;
using Webkho_20241021.Helpers;
using Webkho_20241021.Controllers;
using OfficeOpenXml;

namespace Webkho_20241021.Areas.NhanvienKho.Controllers
{
    [Area("NhanvienKho")]
    [Authorize(Roles = "Nhân viên-BP kho")]
    public class YeucauController : BaseYeucauController
    {
        private readonly EmailService _emailService;
        private readonly IYeucauCodeService _yeucauCodeService;
        private readonly IPhieuCodeService _phieuCodeService;

        public YeucauController(ApplicationDbContext context, EmailService emailService, IYeucauCodeService yeucauCodeService, IPhieuCodeService phieuCodeService) : base(context)
        {
            _emailService = emailService;
            _yeucauCodeService = yeucauCodeService;
            _phieuCodeService = phieuCodeService;
        }

        public IActionResult Yeucau(string search = "")
        {
            // Kiểm tra quyền truy cập - kiểm tra cả Claims và Session
            var userRole = HttpContext.Session.GetString("Chucvu");
            var selectedRole = HttpContext.Session.GetString("SelectedRole");
            var bophan = HttpContext.Session.GetString("Bophan");
            
            // Kiểm tra role trong Claims
            var userRolesInClaims = User?.Claims?
                .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList() ?? new List<string>();
            
            // Kiểm tra xem user có quyền truy cập không - chỉ role "Nhân viên-BP kho"
            bool hasAccess = false;
            if (!string.IsNullOrEmpty(selectedRole))
            {
                hasAccess = selectedRole == "Nhân viên-BP kho" ||
                           userRolesInClaims.Contains("Nhân viên-BP kho");
            }
            else if (!string.IsNullOrEmpty(userRole) && !string.IsNullOrEmpty(bophan))
            {
                var combinedRole = $"{userRole}-{bophan}";
                hasAccess = combinedRole == "Nhân viên-BP kho" ||
                           userRolesInClaims.Contains("Nhân viên-BP kho");
            }
            // Nếu không có selectedRole, kiểm tra trực tiếp trong Claims
            if (!hasAccess && userRolesInClaims.Any())
            {
                hasAccess = userRolesInClaims.Contains("Nhân viên-BP kho");
            }
            
            if (!hasAccess && !User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Dangnhap", "Home", new { area = "" });
            }
            
            if (!hasAccess)
            {
                // Log để debug
                Console.WriteLine($"Yeucau: Access denied. UserRole: '{userRole}', SelectedRole: '{selectedRole}', Bophan: '{bophan}'");
                Console.WriteLine($"Yeucau: Roles in Claims: [{string.Join(", ", userRolesInClaims)}]");
                return Forbid();
            }
            
            var model = _yeucauService.GetDanhSachYeucau(userRole, search);
            var VTyeucaulist = model.VTyeucau ?? new List<vtyeucau>();
            var maSanphamList = VTyeucaulist.Where(v => !string.IsNullOrWhiteSpace(v.MaSanpham)).Select(v => v.MaSanpham!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var maSanphamSet = new HashSet<string>(maSanphamList, StringComparer.OrdinalIgnoreCase);
            var tonKhoByMaSanpham = _context.khotongs.Where(k => k.MaSanpham != null).Select(k => new { k.MaSanpham, k.SL }).ToList()
                .Where(k => maSanphamSet.Contains(k.MaSanpham!)).GroupBy(k => k.MaSanpham!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.SL ?? 0), StringComparer.OrdinalIgnoreCase);
            ViewBag.Search = search;
            ViewBag.TonKhoByMaSanpham = tonKhoByMaSanpham;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult XoaYeucau(string MaYeucau)
        {
            if (string.IsNullOrWhiteSpace(MaYeucau))
            {
                TempData["ErrorMessage"] = "Mã yêu cầu không hợp lệ.";
                return RedirectToAction("Yeucau", "Yeucau", new { area = "NhanvienKho" });
            }
            var maNv = HttpContext.Session.GetString("MaNguoidung");
            var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
            if (yeucau == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy yêu cầu.";
                return RedirectToAction("Yeucau", "Yeucau", new { area = "NhanvienKho" });
            }
            if (!YeucauDeleteHelper.CoTheXoaYeucauNhanVien(yeucau, maNv))
            {
                TempData["ErrorMessage"] = "Bạn chỉ được xóa yêu cầu khi Trưởng BP chưa duyệt.";
                return RedirectToAction("Yeucau", "Yeucau", new { area = "NhanvienKho" });
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
            return RedirectToAction("Yeucau", "Yeucau", new { area = "NhanvienKho" });
        }

        public IActionResult ThemYeucau()
        {
            var Duanlist = _context.duans
                          .Select(n => new { n.MaDuan, n.TrangThai })
                          .ToList();

            ViewBag.Duanlist = Duanlist;
            return View();
        }

        public IActionResult Phieuxuatkho(string search = "")
        {
            var model = _phieuService.GetDanhSachPhieuxuatkho(search);
            ViewBag.Search = search;
            return View(model);
        }

        public IActionResult Phieunhapkho(string search = "")
        {
            var model = _phieuService.GetDanhSachPhieunhapkho(search);
            ViewBag.Search = search;
            return View(model);
        }

        public IActionResult Phieumuahang(string search = "")
        {
            var model = _phieuService.GetDanhSachPhieumuahang(search);
            ViewBag.Search = search;
            return View(model);
        }

        [HttpGet]
        public override IActionResult GetDulieuThongbao()
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
                    // Thiếu = Yêu cầu - Đã xuất (khi đã xuất đủ thì thiếu = 0, không dùng tồn kho vì sau khi xuất tồn kho = 0)
                    var slDaXuatThucTe = !string.IsNullOrWhiteSpace(v.MaSanpham) ? YeucauUpdateHelper.TinhSoLuongDaCap(_context, MaYeucau, v.MaSanpham) : 0;
                    var slThieu = YeucauUpdateHelper.TinhSoLuongConThieuTheoMaYeuCauCoBan(_context, MaYeucau, v.MaSanpham ?? "");
                    var isDaXuatKho = (v.TrangThai ?? "").IndexOf("Đã xuất kho", StringComparison.OrdinalIgnoreCase) >= 0;
                    var slDaXuat = slDaXuatThucTe > 0 ? (int?)slDaXuatThucTe : (isDaXuatKho ? (v.SL ?? v.SLMoi) : (int?)null);
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

            // Không có chi tiết ở cả 2 nơi
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

            // Lấy thông tin phiếu xuất kho để lấy tên người yêu cầu
            var phieuxuatkho = _context.phieuxuatkho
                .FirstOrDefault(p => p.MaXuatkho == MaXuatkho);

            string tenNguoiYeuCau = "";
            if (phieuxuatkho != null)
            {
                if (!string.IsNullOrEmpty(phieuxuatkho.MaYeucau))
                {
                    var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == phieuxuatkho.MaYeucau);
                    if (yeucau != null)
                    {
                        tenNguoiYeuCau = yeucau.NguoiYeucau ?? "";
                    }
                }

                if (string.IsNullOrEmpty(tenNguoiYeuCau) && !string.IsNullOrEmpty(phieuxuatkho.MaNguoidung))
                {
                    var nguoidung = _context.nguoidungs.FirstOrDefault(n => n.MaNguoidung == phieuxuatkho.MaNguoidung);
                    if (nguoidung != null)
                    {
                        tenNguoiYeuCau = nguoidung.TenNguoidung ?? "";
                    }
                }
            }

            // Tính SL gốc của phiếu (căn cứ) và còn lại theo base code
            var maYeucau = phieuxuatkho?.MaYeucau ?? "";
            Dictionary<string, int> slDaXuatByBase = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> slGocByBase = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(maYeucau))
            {
                slGocByBase = _context.vtphieuxuatkho
                    .Where(v => v.MaYeucau == maYeucau)
                    .ToList()
                    .GroupBy(v => YeucauUpdateHelper.GetBaseProductCode(v.MaSanpham ?? "") ?? "")
                    .ToDictionary(
                        g => g.Key ?? "",
                        g => g.Max(x => x.SL ?? 0),
                        StringComparer.OrdinalIgnoreCase
                    );

                slDaXuatByBase = _context.vtphieuxuatkho
                    .Where(v => v.MaYeucau == maYeucau && (v.TrangThai == "Đã xuất kho" || v.TrangThai == "Hoàn thành"))
                    .ToList()
                    .GroupBy(v => YeucauUpdateHelper.GetBaseProductCode(v.MaSanpham ?? "") ?? "")
                    .ToDictionary(
                        g => g.Key ?? "",
                        g => g.Sum(x => x.SL ?? 0),
                        StringComparer.OrdinalIgnoreCase
                    );
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
                var baseCode = YeucauUpdateHelper.GetBaseProductCode(v.MaSanpham ?? "") ?? "";
                int slGoc = slGocByBase.TryGetValue(baseCode, out var goc) ? goc : (v.SL ?? 0);
                int slDaXuat = slDaXuatByBase.TryGetValue(baseCode, out var issued) ? issued : 0;
                int slConLai = Math.Max(0, slGoc - slDaXuat);
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
                    slYeuCau = slGoc,
                    slConLai,
                    TonKho = tonKho
                };
            }).ToList();

            return Json(new
            {
                items = items,
                tenNguoiYeuCau = tenNguoiYeuCau,
                maYeucau = maYeucau
            });
        }

        [HttpPost]
        public IActionResult CapNhatTrangThaiVT(string MaXuatkho, int Id, string TrangThai)
        {
            try
            {
                var allowed = new[] { "Đang chuẩn bị hàng", "Đã chuẩn bị hàng xong", "Thiếu hàng- đang mua hàng" };
                if (string.IsNullOrEmpty(MaXuatkho) || Id <= 0 || string.IsNullOrEmpty(TrangThai) || !allowed.Contains(TrangThai))
                {
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ." });
                }

                var vt = _context.vtphieuxuatkho.FirstOrDefault(v => v.ID == Id && v.MaXuatkho == MaXuatkho);
                if (vt == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy vật tư." });
                }

                vt.TrangThai = TrangThai;
                _context.vtphieuxuatkho.Update(vt);
                _context.SaveChanges();
                return Json(new { success = true, message = "Đã cập nhật trạng thái." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        private sealed class SlThucXuatItem
        {
            public int id { get; set; }
            public int slThucXuat { get; set; }
        }

        private static Dictionary<int, int> ParseSlThucXuatJson(string? json)
        {
            var map = new Dictionary<int, int>();
            if (string.IsNullOrWhiteSpace(json))
            {
                return map;
            }

            try
            {
                var items = JsonSerializer.Deserialize<List<SlThucXuatItem>>(json);
                if (items == null) return map;
                foreach (var it in items)
                {
                    if (it == null) continue;
                    if (it.id <= 0) continue;
                    map[it.id] = it.slThucXuat;
                }
            }
            catch
            {
                // ignore parse errors -> fallback default
            }

            return map;
        }

        [HttpPost]
        public IActionResult XuatKhoVatTuRieng(string MaXuatkho, int VatTuId, int SoLuongThucXuat)
        {
            try
            {
                if (string.IsNullOrEmpty(MaXuatkho) || VatTuId <= 0)
                {
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ." });
                }

                var vtGoc = _context.vtphieuxuatkho.FirstOrDefault(v => v.ID == VatTuId && v.MaXuatkho == MaXuatkho);
                if (vtGoc == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy vật tư." });
                }

                if (vtGoc.TrangThai == "Đã xuất kho")
                {
                    return Json(new { success = false, message = "Vật tư này đã được xuất kho." });
                }

                if (vtGoc.TrangThai != "Đã chuẩn bị hàng xong")
                {
                    return Json(new { success = false, message = "Vật tư phải ở trạng thái 'Đã chuẩn bị hàng xong' mới được xuất kho." });
                }

                var phieuGoc = _context.phieuxuatkho.FirstOrDefault(p => p.MaXuatkho == MaXuatkho);
                if (phieuGoc == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy phiếu xuất kho gốc." });
                }

                // SL gốc của phiếu (giữ nguyên để làm căn cứ)
                int slGocPhieu = vtGoc.SL ?? 0;
                if (slGocPhieu <= 0)
                {
                    return Json(new { success = false, message = "SL gốc của phiếu không hợp lệ." });
                }

                // Đã xuất (trên toàn bộ yêu cầu, theo base code)
                var maYeucau = phieuGoc.MaYeucau ?? vtGoc.MaYeucau ?? "";
                var baseCode = YeucauUpdateHelper.GetBaseProductCode(vtGoc.MaSanpham ?? "") ?? "";
                // EF không translate được GetBaseProductCode -> lọc DB trước rồi tính trong memory
                var issuedRows = _context.vtphieuxuatkho
                    .AsNoTracking()
                    .Where(v =>
                        v.MaYeucau == maYeucau &&
                        (v.TrangThai == "Đã xuất kho" || v.TrangThai == "Hoàn thành"))
                    .ToList();

                int daXuat = issuedRows
                    .Where(v => (YeucauUpdateHelper.GetBaseProductCode(v.MaSanpham ?? "") ?? "") == baseCode)
                    .Sum(v => v.SL ?? 0);

                int conLai = Math.Max(0, slGocPhieu - daXuat);

                if (conLai <= 0)
                {
                    return Json(new { success = false, message = "Vật tư này đã xuất đủ theo SL gốc của phiếu." });
                }
                if (SoLuongThucXuat <= 0 || SoLuongThucXuat > conLai)
                {
                    return Json(new { success = false, message = $"Cần xuất phải > 0 và ≤ Còn lại ({conLai})." });
                }

                // Kiểm tra tồn kho (logic NV kho: trừ theo đúng Makho + MaSanpham)
                var khoTong = _context.khotongs
                    .FirstOrDefault(k => k.Makho == vtGoc.Makho && k.MaSanpham == vtGoc.MaSanpham);
                if (khoTong == null)
                {
                    return Json(new { success = false, message = $"Vật tư {vtGoc.TenSanpham} không tồn tại trong kho tổng." });
                }
                int tonKho = khoTong.SL ?? 0;
                if (tonKho < SoLuongThucXuat)
                {
                    return Json(new { success = false, message = $"Không đủ tồn kho để xuất vật tư {vtGoc.TenSanpham}." });
                }

                // Tạo mã phiếu xuất kho mới bằng service
                string maXuatkhoMoi = _phieuCodeService.GenerateMaXuatKho(phieuGoc.MaDuan, phieuGoc.MaYeucau);

                var phieuMoi = new phieuxuatkho
                {
                    MaXuatkho = maXuatkhoMoi,
                    MaYeucau = phieuGoc.MaYeucau,
                    MaDuan = phieuGoc.MaDuan,
                    MaNguoidung = phieuGoc.MaNguoidung,
                    NgayXuatkho = DateTime.Now,
                    NgayChuanBi = DateTime.Now,
                    TrangThai = "Đã xuất kho",
                    GhiChu = $"Tạo từ phiếu {phieuGoc.MaXuatkho} (xuất từng phần)"
                };
                _context.phieuxuatkho.Add(phieuMoi);

                // Tính đơn giá / thành tiền theo số lượng thực xuất
                decimal? donGia = vtGoc.DonGia;
                if (!donGia.HasValue && vtGoc.ThanhTien.HasValue && (vtGoc.SL ?? 0) > 0)
                {
                    donGia = vtGoc.ThanhTien.Value / (vtGoc.SL ?? 1);
                }
                decimal? thanhTienXuat = null;
                if (donGia.HasValue)
                {
                    thanhTienXuat = donGia.Value * SoLuongThucXuat;
                }
                else if (vtGoc.ThanhTien.HasValue && (vtGoc.SL ?? 0) > 0)
                {
                    // fallback theo tỷ lệ
                    thanhTienXuat = (vtGoc.ThanhTien.Value / (vtGoc.SL ?? 1)) * SoLuongThucXuat;
                }

                var vtMoi = new vtphieuxuatkho
                {
                    MaXuatkho = maXuatkhoMoi,
                    MaYeucau = vtGoc.MaYeucau,
                    TenSanpham = vtGoc.TenSanpham,
                    MaSanpham = vtGoc.MaSanpham,
                    Makho = vtGoc.Makho,
                    HangSX = vtGoc.HangSX,
                    NhaCC = vtGoc.NhaCC,
                    // SL trên phiếu con = số lượng THỰC XUẤT lần này
                    SL = SoLuongThucXuat,
                    DonVi = vtGoc.DonVi,
                    DonGia = donGia,
                    ThanhTien = thanhTienXuat,
                    NgayNhapkho = vtGoc.NgayNhapkho,
                    NgayBaohanh = vtGoc.NgayBaohanh,
                    ThoiGianBH = vtGoc.ThoiGianBH,
                    TrangThai = "Đã xuất kho",
                    LoaiCapPhat = vtGoc.LoaiCapPhat
                };
                _context.vtphieuxuatkho.Add(vtMoi);

                // Trừ kho tổng + cộng kho dự án / kho người dùng
                khoTong.SL = tonKho - SoLuongThucXuat;
                _context.khotongs.Update(khoTong);

                if (!string.IsNullOrEmpty(phieuGoc.MaDuan))
                {
                    _context.khoduans.Add(new khoduans
                    {
                        DAMaDuan = phieuGoc.MaDuan,
                        TenSanpham = vtGoc.TenSanpham,
                        MaSanpham = vtGoc.MaSanpham,
                        DAMakho = vtGoc.Makho,
                        HangSX = vtGoc.HangSX,
                        NhaCC = vtGoc.NhaCC,
                        DonVi = vtGoc.DonVi,
                        SL = SoLuongThucXuat,
                        NgayBaohanh = vtGoc.NgayBaohanh,
                        ThoiGianBH = vtGoc.ThoiGianBH,
                        TrangThai = "Đã xuất kho"
                    });
                }
                else
                {
                    var vatTuNguoiDung = _context.khonguoidungs
                        .FirstOrDefault(nd => nd.NDMakho == vtGoc.Makho && nd.NDMaNguoidung == phieuGoc.MaNguoidung && nd.MaSanpham == vtGoc.MaSanpham);

                    if (vatTuNguoiDung != null)
                    {
                        vatTuNguoiDung.SL = (vatTuNguoiDung.SL ?? 0) + SoLuongThucXuat;
                        _context.khonguoidungs.Update(vatTuNguoiDung);
                    }
                    else
                    {
                        _context.khonguoidungs.Add(new khonguoidungs
                        {
                            NDMaNguoidung = phieuGoc.MaNguoidung,
                            TenSanpham = vtGoc.TenSanpham,
                            MaSanpham = vtGoc.MaSanpham,
                            NDMakho = vtGoc.Makho,
                            HangSX = vtGoc.HangSX,
                            NhaCC = vtGoc.NhaCC,
                            DonVi = vtGoc.DonVi,
                            SL = SoLuongThucXuat,
                            NgayBaohanh = vtGoc.NgayBaohanh,
                            ThoiGianBH = vtGoc.ThoiGianBH,
                            TrangThai = "Đang mượn"
                        });
                    }
                }

                // Không cập nhật vtGoc.SL (SL gốc phải giữ nguyên để làm căn cứ).
                // Cập nhật vtGoc.TrangThai = "Đã xuất kho" khi đã xuất đủ (conLai - SoLuongThucXuat <= 0)
                // để tránh khi "xuất hết" báo thiếu hàng sai cho vật tư đã xuất.
                if (SoLuongThucXuat >= conLai)
                {
                    vtGoc.TrangThai = "Đã xuất kho";
                    _context.vtphieuxuatkho.Update(vtGoc);
                }

                _context.SaveChanges();

                // Gửi email thông báo khi xuất kho từng phần (từng vật tư)
                try
                {
                    var maYcForEmail = phieuGoc.MaYeucau ?? vtGoc.MaYeucau;
                    var maXkForEmail = maXuatkhoMoi;

                    if (!string.IsNullOrEmpty(maYcForEmail))
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await _emailService.SendNotificationToRequesterOnIssueAsync(maYcForEmail, maXkForEmail);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[NVKho/XuatKhoVatTuRieng] Lỗi gửi email người yêu cầu: {ex.Message}");
                            }
                        });

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await _emailService.SendNotificationToWarehouseOnXuatKhoAsync(maXkForEmail, maYcForEmail);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[NVKho/XuatKhoVatTuRieng] Lỗi gửi email kho: {ex.Message}");
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[NVKho/XuatKhoVatTuRieng] Lỗi tạo task gửi email: {ex.Message}");
                }

                // Chốt trạng thái phiếu tổng (phiếu gốc) nếu đã xuất hết theo SL gốc (ConLai == 0 cho mọi baseCode)
                if (phieuGoc != null && !string.IsNullOrWhiteSpace(phieuGoc.MaYeucau))
                {
                    var allRowsSameYCForClose = _context.vtphieuxuatkho
                        .AsNoTracking()
                        .Where(v => v.MaYeucau == phieuGoc.MaYeucau)
                        .ToList();

                    var slGocByBaseForClose = allRowsSameYCForClose
                        .GroupBy(v => YeucauUpdateHelper.GetBaseProductCode(v.MaSanpham ?? "") ?? "")
                        .ToDictionary(g => g.Key, g => g.Max(x => x.SL ?? 0), StringComparer.OrdinalIgnoreCase);

                    var daXuatByBaseForClose = allRowsSameYCForClose
                        .Where(v => v.TrangThai == "Đã xuất kho" || v.TrangThai == "Hoàn thành")
                        .GroupBy(v => YeucauUpdateHelper.GetBaseProductCode(v.MaSanpham ?? "") ?? "")
                        .ToDictionary(g => g.Key, g => g.Sum(x => x.SL ?? 0), StringComparer.OrdinalIgnoreCase);

                    bool daXuatHet = slGocByBaseForClose.All(kvp =>
                    {
                        var baseCode = kvp.Key ?? "";
                        var slGoc = kvp.Value;
                        var daXuat = daXuatByBaseForClose.TryGetValue(baseCode, out var dx) ? dx : 0;
                        return daXuat >= slGoc;
                    });

                    if (daXuatHet && phieuGoc.TrangThai != "Đã xuất kho")
                    {
                        phieuGoc.TrangThai = "Đã xuất kho";
                        phieuGoc.NgayHoanThanh = DateTime.Now;
                        phieuGoc.NgayXuatkho = DateTime.Now;
                        phieuGoc.GhiChu = "Đã xuất kho (đã xuất hết theo SL gốc)";
                        _context.phieuxuatkho.Update(phieuGoc);
                        _context.SaveChanges();
                    }
                }

                return Json(new { success = true, message = "Đã tạo phiếu xuất kho mới thành công.", maXuatkhoMoi = maXuatkhoMoi });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        [HttpGet]
        public IActionResult GetVTPhieunhapkho(string MaNhapkho)
        {
            var PhieunhapkhoList = _context.vtphieunhapkho
                                 .Where(v => v.MaNhapkho == MaNhapkho).ToList();
            return Json(PhieunhapkhoList);
        }

        [HttpGet]
        public IActionResult XemPhieunhapkho(string MaNhapkho)
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
                .Where(v => v.MaNhapkho == MaNhapkho)
                .ToList();

            var duan = !string.IsNullOrEmpty(phieunhapkho.MaDuan)
                ? _context.duans.FirstOrDefault(d => d.MaDuan == phieunhapkho.MaDuan)
                : null;

            // Lấy thông tin người yêu cầu từ yeucau
            var yeucau = !string.IsNullOrEmpty(phieunhapkho.MaYeucau)
                ? _context.yeucau.FirstOrDefault(y => y.MaYeucau == phieunhapkho.MaYeucau)
                : null;

            // Lấy thông tin người dùng từ nguoidungs (nếu có)
            var nguoidung = !string.IsNullOrEmpty(phieunhapkho.MaNguoidung)
                ? _context.nguoidungs.FirstOrDefault(n => n.MaNguoidung == phieunhapkho.MaNguoidung)
                : null;

            // Ưu tiên lấy từ yeucau, nếu không có thì lấy từ nguoidungs
            string tenNguoiGiaoHang = "";
            string maNguoiGiaoHang = "";
            if (yeucau != null)
            {
                tenNguoiGiaoHang = yeucau.NguoiYeucau ?? "";
                maNguoiGiaoHang = yeucau.YCMaNguoidung ?? "";
            }
            else if (nguoidung != null)
            {
                tenNguoiGiaoHang = nguoidung.TenNguoidung ?? "";
                maNguoiGiaoHang = nguoidung.MaNguoidung ?? "";
            }

            // Lấy thông tin nhà cung cấp từ vật tư đầu tiên (nếu có)
            var nhaCC = vtphieunhapkho.FirstOrDefault()?.NhaCC;

            var model = new Phieunhapkhoviewmodel
            {
                Phieunhapkho = new List<phieunhapkho> { phieunhapkho },
                VTphieunhapkho = vtphieunhapkho,
                Duans = duan != null ? new List<duans> { duan } : new List<duans>()
            };

            ViewBag.NhaCC = nhaCC;
            ViewBag.NguoiDung = nguoidung;
            ViewBag.Duan = duan;
            ViewBag.TenNguoiGiaoHang = tenNguoiGiaoHang;
            ViewBag.MaNguoiGiaoHang = maNguoiGiaoHang;
            ViewBag.Yeucau = yeucau;

            return View(model);
        }

        [HttpGet]
        public IActionResult KiemTraGiaTien(string MaNhapkho)
        {
            if (string.IsNullOrEmpty(MaNhapkho))
            {
                return Json(new { error = "Mã nhập kho không hợp lệ" });
            }

            var phieunhapkho = _context.phieunhapkho.FirstOrDefault(p => p.MaNhapkho == MaNhapkho);
            if (phieunhapkho == null)
            {
                return Json(new { error = "Không tìm thấy phiếu nhập kho" });
            }

            // Lấy vật tư trong phiếu nhập kho
            var vtphieunhapkho = _context.vtphieunhapkho
                .Where(v => v.MaNhapkho == MaNhapkho)
                .Select(v => new
                {
                    MaSanpham = v.MaSanpham,
                    TenSanpham = v.TenSanpham,
                    DonGia = v.DonGia,
                    ThanhTien = v.ThanhTien,
                    SL = v.SL
                })
                .ToList();

            // Lấy vật tư từ phiếu mua hàng (nếu có)
            var vtphieumuahang = new List<object>();
            if (!string.IsNullOrEmpty(phieunhapkho.MaYeucau))
            {
                var phieumuahang = _context.phieumuahang
                    .FirstOrDefault(p => p.MaYeucau == phieunhapkho.MaYeucau);
                
                if (phieumuahang != null)
                {
                    vtphieumuahang = _context.vtphieumuahang
                        .Where(v => v.MaMuahang == phieumuahang.MaMuahang)
                        .Select(v => new
                        {
                            MaSanpham = v.MaSanpham,
                            TenSanpham = v.TenSanpham,
                            DonGia = v.DonGia,
                            ThanhTien = v.ThanhTien,
                            SL = v.SL,
                            TrangThai = v.TrangThai
                        })
                        .ToList<object>();
                }
            }

            return Json(new
            {
                MaNhapkho = MaNhapkho,
                MaYeucau = phieunhapkho.MaYeucau,
                VTPhieunhapkho = vtphieunhapkho,
                VTPhieumuahang = vtphieumuahang
            });
        }

        [HttpGet]
        public IActionResult GetVTPhieumuahang(string MaMuahang)
        {
            var PhieumuahangList = _context.vtphieumuahang
                                 .Where(v => v.MaMuahang == MaMuahang).ToList();
            return Json(PhieumuahangList);
        }

        public IActionResult ThemPhieunhapkho()
        {
            var maNv = HttpContext.Session.GetString("MaNguoidung");

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

            // Lấy dữ liệu từ kho cá nhân
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

            // Lấy mã nhân viên từ session
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
                                   .Distinct() // Tránh trùng lặp nếu có
                                   .ToList();

                // Debug info
                Console.WriteLine($"Querying vtphieuxuatkho for MaDuan = '{maduan}'");
                Console.WriteLine($"Found {khoDuanItems.Count} items");
                
                // Debug: Kiểm tra số phiếu xuất kho có MaDuan này
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
            var results = _context.khotongs
                .Where(k => (k.TenSanpham != null && k.TenSanpham.ToLower().Contains(searchTerm)) || 
                           (k.MaSanpham != null && k.MaSanpham.ToLower().Contains(searchTerm)))
                .Take(10) // Giới hạn 10 kết quả để hiệu suất tốt hơn
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
                
                // Đảm bảo trạng thái luôn được set đúng theo quy tắc
                if (string.IsNullOrEmpty(yeucau.TrangThai))
                {
                    if (chucVu2 == "Giám đốc")
                    {
                        yeucau.TrangThai = "Đã duyệt";
                    }
                    else if (chucVu2 == "Nhân viên" && boPhan2 == "BP kho")
                    {
                        yeucau.TrangThai = "Chờ Trưởng BP-BP kho duyệt";
                    }
                    else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kho")
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

            return RedirectToAction("Yeucau", "Yeucau", new { area = "NhanvienKho" });

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
                            }
                            else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kho")
                            {
                                Yeucau.TrangThai = "Quản lí dự án";
                            }
                            else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP mua hàng")
                            {
                                Yeucau.TrangThai = "Quản lí dự án";
                            }
                            else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kế toán")
                            {
                                Yeucau.TrangThai = "Quản lí dự án";
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

            return RedirectToAction("Yeucau", "Yeucau", new { area = "NhanvienKho" });
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
                return RedirectToAction("Yeucau", "Yeucau", new { area = "NhanvienKho" });
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

            // 🔍 DEBUG: Kiểm tra và bỏ qua vật tư bị giám đốc từ chối
            foreach (var VattuYC in danhSachVatTuYC)
            {
                // ⚠️ BUG FIX: Bỏ qua vật tư đã bị từ chối (bởi giám đốc hoặc người khác)
                if (!string.IsNullOrEmpty(VattuYC.TrangThai) && VattuYC.TrangThai.Contains("Đã từ chối"))
                {
                    Console.WriteLine($"[DEBUG] Bỏ qua vật tư {VattuYC.MaSanpham} - {VattuYC.TenSanpham} vì đã bị từ chối (Trạng thái: {VattuYC.TrangThai})");
                    continue; // Không xử lý vật tư đã bị từ chối
                }

                var khotong = DanhsachVTYCkhotong.FirstOrDefault(kt => kt.Makho == VattuYC.YCMakho && kt.MaSanpham == VattuYC.MaSanpham);

                if (khotong != null)
                {
                    // Tính số lượng hàng đã cam kết từ các phiếu xuất khác (chưa có phiếu hiện tại nên không cần loại trừ)
                    int soLuongDaCamKet = TinhSoLuongDaCamKet(khotong.Makho ?? "", khotong.MaSanpham ?? "", null);
                    
                    // Số lượng khả dụng = Tồn kho - Số lượng đã cam kết
                    int soLuongKhaDung = (khotong.SL ?? 0) - soLuongDaCamKet;

                    if (soLuongKhaDung > 0 && soLuongKhaDung < VattuYC.SL)
                    {
                        // Trường hợp số lượng khả dụng nhỏ hơn số lượng yêu cầu
                        Console.WriteLine($"Đã chạy: Số lượng khả dụng nhỏ hơn số lượng yêu cầu (Makho: {khotong.Makho}, SL tồn: {khotong.SL}, SL đã cam kết: {soLuongDaCamKet}, SL khả dụng: {soLuongKhaDung}, SL yêu cầu: {VattuYC.SL})");
                        isPhieuXuatKhoCreated = true;
                        isPhieuMuaHangCreated = true;
                    }
                    else if (soLuongKhaDung == 0)
                    {
                        // Trường hợp số lượng khả dụng bằng 0
                        Console.WriteLine($"Đã chạy: Số lượng khả dụng bằng 0 (Makho: {khotong.Makho}, SL tồn: {khotong.SL}, SL đã cam kết: {soLuongDaCamKet}, SL yêu cầu: {VattuYC.SL})");
                        isPhieuMuaHangCreated = true;
                    }
                    else if (soLuongKhaDung >= VattuYC.SL)
                    {
                        // Trường hợp số lượng khả dụng đủ đáp ứng
                        Console.WriteLine($"Đã chạy: Số lượng khả dụng đủ đáp ứng (Makho: {khotong.Makho}, SL tồn: {khotong.SL}, SL đã cam kết: {soLuongDaCamKet}, SL khả dụng: {soLuongKhaDung}, SL yêu cầu: {VattuYC.SL})");
                        isPhieuXuatKhoCreated = true;
                    }
                    else
                    {
                        // Trường hợp số lượng khả dụng < 0 (tồn kho < đã cam kết) - cần mua hàng
                        Console.WriteLine($"Đã chạy: Số lượng khả dụng âm (Makho: {khotong.Makho}, SL tồn: {khotong.SL}, SL đã cam kết: {soLuongDaCamKet}, SL yêu cầu: {VattuYC.SL})");
                        isPhieuMuaHangCreated = true;
                    }
                }
                else
                {
                    // Trường hợp không tìm thấy kho tổng
                    Console.WriteLine($"Đã chạy: Không tìm thấy kho tổng phù hợp cho Makho: {VattuYC.YCMakho}");
                    // Không có kho tổng nhưng vẫn phải mua → đảm bảo tạo phiếu mua hàng
                    isPhieuMuaHangCreated = true;
                }
            }

            // 🔍 DEBUG: Kiểm tra xem có vật tư nào không bị từ chối cần tạo phiếu mua hàng không
            var soVatTuKhongBiTuChoi = danhSachVatTuYC.Count(vt => 
                string.IsNullOrEmpty(vt.TrangThai) || !vt.TrangThai.Contains("Đã từ chối"));
            Console.WriteLine($"[DEBUG] Tổng số vật tư: {danhSachVatTuYC.Count}, Số vật tư không bị từ chối: {soVatTuKhongBiTuChoi}");
            
            // ⚠️ BUG FIX: Chỉ tạo phiếu mua hàng nếu có ít nhất 1 vật tư không bị từ chối cần mua
            if (isPhieuMuaHangCreated && soVatTuKhongBiTuChoi == 0)
            {
                Console.WriteLine($"[DEBUG] KHÔNG tạo phiếu mua hàng vì tất cả vật tư đều bị từ chối!");
                isPhieuMuaHangCreated = false;
            }

            if ((isPhieuMuaHangCreated == true) && (isPhieuXuatKhoCreated == true))
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
                Console.WriteLine($"Đã tạo phiếu xuất kho: MaXuatkho = {Maxuatkho}");

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
                Console.WriteLine($"Đã tạo phiếu mua hàng: MaMuahang = {Mamuahang}");
            }
            else if (isPhieuMuaHangCreated == true && isPhieuXuatKhoCreated == false)
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
                Console.WriteLine($"Đã tạo phiếu mua hàng: MaMuahang = {Mamuahang}");
            }
            else if (isPhieuXuatKhoCreated == true && isPhieuMuaHangCreated == false)
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
                Console.WriteLine($"Đã tạo phiếu xuất kho: MaXuatkho = {Maxuatkho}");
            }

            _context.SaveChanges();
            Console.WriteLine("Đã lưu thay đổi vào cơ sở dữ liệu.");

            // 🔍 DEBUG: Tạo chi tiết phiếu mua hàng và phiếu xuất kho - BỎ QUA vật tư bị từ chối
            foreach (var VattuYC in danhSachVatTuYC)
            {
                // ⚠️ BUG FIX: Bỏ qua vật tư đã bị từ chối (bởi giám đốc hoặc người khác)
                if (!string.IsNullOrEmpty(VattuYC.TrangThai) && VattuYC.TrangThai.Contains("Đã từ chối"))
                {
                    Console.WriteLine($"[DEBUG] Bỏ qua vật tư {VattuYC.MaSanpham} - {VattuYC.TenSanpham} khi tạo chi tiết phiếu vì đã bị từ chối (Trạng thái: {VattuYC.TrangThai})");
                    continue; // Không tạo chi tiết phiếu mua hàng/xuất kho cho vật tư đã bị từ chối
                }

                // Tìm vật tư trong kho tổng: ưu tiên khớp cả Makho và MaSanpham, nếu không có thì tìm theo MaSanpham
                var khotong = _context.khotongs.FirstOrDefault(kt => 
                    kt.Makho == VattuYC.YCMakho && 
                    kt.MaSanpham == VattuYC.MaSanpham)
                    ?? _context.khotongs.FirstOrDefault(kt => 
                        kt.MaSanpham == VattuYC.MaSanpham);

                if (khotong != null && khotong.SL > 0)
                {
                    // Tính số lượng hàng đã cam kết từ các phiếu xuất khác (loại trừ phiếu hiện tại)
                    int soLuongDaCamKet = TinhSoLuongDaCamKet(khotong.Makho ?? "", khotong.MaSanpham ?? "", Maxuatkho);
                    
                    // Số lượng khả dụng = Tồn kho - Số lượng đã cam kết
                    int soLuongKhaDung = (khotong.SL ?? 0) - soLuongDaCamKet;
                    int soLuongYeuCau = VattuYC.SL ?? 0;
                    int soLuongXuat = Math.Max(0, Math.Min(soLuongKhaDung, soLuongYeuCau));
                    int soLuongThieu = soLuongYeuCau - soLuongXuat;

                    if (soLuongXuat > 0)
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
                            TrangThai = "Đang chuẩn bị hàng",
                            SL = soLuongXuat
                        };
                        _context.Add(VTPhieuxuatkho);
                    }

                    if (soLuongThieu > 0)
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
                            SL = soLuongThieu,
                            NgayBaohanh = VattuYC.NgayBaohanh,
                            ThoiGianBH = VattuYC.ThoiGianBH,
                            TrangThai = "Đang chờ báo giá"
                        };

                        _context.Add(VTPhieumuahang);
                    }
                    else
                    {
                        VattuYC.TrangThai = "Đã duyệt";
                    }

                    _context.vtyeucau.Update(VattuYC);
                    // KHÔNG cập nhật khotong ở đây - chỉ cập nhật khi người nhận xác nhận đã nhận hàng
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


            return RedirectToAction("Yeucau", "Yeucau", new { area = "NhanvienKho" });
        }

        [HttpPost]
        public IActionResult Xuliphieuxuatkho(
                                string MaXuatkho,
                                phieuxuatkho phieuxuatkho,
                                vtphieuxuatkho vtphieuxuatkho,
                                khoduans khoduans,
                                string? slThucXuatJson)
        {
            var VTphieuxuatkho = _context.vtphieuxuatkho
                                          .Where(vt => vt.MaXuatkho == MaXuatkho)
                                          .ToList();

            var vatTuCanXuat = VTphieuxuatkho
                .Where(vt => vt.TrangThai != "Đã xuất kho")
                .ToList();

            var Phieuxuatkho = _context.phieuxuatkho
                                        .FirstOrDefault(yc => yc.MaXuatkho == MaXuatkho);


            if (Phieuxuatkho == null)
            {
                TempData["Error"] = "Không tìm thấy phiếu xuất kho!";
                return RedirectToAction("Phieuxuatkho", "Yeucau", new { area = "NhanvienKho" });
            }

            if (Phieuxuatkho.TrangThai != "Đang chuẩn bị hàng")
            {
                TempData["Error"] = "Chỉ xử lý xuất kho khi phiếu đang ở trạng thái 'Đang chuẩn bị hàng'.";
                return RedirectToAction("Phieuxuatkho", "Yeucau", new { area = "NhanvienKho" });
            }

            if (!vatTuCanXuat.Any())
            {
                TempData["Info"] = "Tất cả vật tư trong phiếu này đã được xuất trước đó.";
                return RedirectToAction("Phieuxuatkho", "Yeucau", new { area = "NhanvienKho" });
            }

            var slMap = ParseSlThucXuatJson(slThucXuatJson);

            // SL gốc theo base code và đã xuất theo base code (tính trong memory)
            var maYcForCalc = Phieuxuatkho.MaYeucau ?? "";
            var allRowsSameYC = string.IsNullOrWhiteSpace(maYcForCalc)
                ? new List<vtphieuxuatkho>()
                : _context.vtphieuxuatkho.AsNoTracking().Where(v => v.MaYeucau == maYcForCalc).ToList();

            var slGocByBase = allRowsSameYC
                .GroupBy(v => YeucauUpdateHelper.GetBaseProductCode(v.MaSanpham ?? "") ?? "")
                .ToDictionary(g => g.Key, g => g.Max(x => x.SL ?? 0), StringComparer.OrdinalIgnoreCase);

            var daXuatByBase = allRowsSameYC
                .Where(v => v.TrangThai == "Đã xuất kho" || v.TrangThai == "Hoàn thành")
                .GroupBy(v => YeucauUpdateHelper.GetBaseProductCode(v.MaSanpham ?? "") ?? "")
                .ToDictionary(g => g.Key, g => g.Sum(x => x.SL ?? 0), StringComparer.OrdinalIgnoreCase);

            // validate trước để tránh trừ kho nửa chừng
            foreach (var vt in vatTuCanXuat)
            {
                var baseCode = YeucauUpdateHelper.GetBaseProductCode(vt.MaSanpham ?? "") ?? "";
                int slGoc = slGocByBase.TryGetValue(baseCode, out var goc) ? goc : (vt.SL ?? 0);
                int daXuat = daXuatByBase.TryGetValue(baseCode, out var dx) ? dx : 0;
                int conLai = Math.Max(0, slGoc - daXuat);
                int slThucXuat = slMap.ContainsKey(vt.ID) ? slMap[vt.ID] : conLai;
                if (conLai <= 0)
                {
                    TempData["Error"] = $"Số lượng còn lại của vật tư {vt.TenSanpham} không hợp lệ.";
                    return RedirectToAction("Phieuxuatkho", "Yeucau", new { area = "NhanvienKho" });
                }
                if (slThucXuat <= 0 || slThucXuat > conLai)
                {
                    TempData["Error"] = $"Cần xuất của vật tư {vt.TenSanpham} phải > 0 và ≤ Còn lại ({conLai}).";
                    return RedirectToAction("Phieuxuatkho", "Yeucau", new { area = "NhanvienKho" });
                }
            }

            // Tạo 1 phiếu xuất kho mới cho phần thực xuất (để in đúng SL thực xuất)
            string maXuatkhoMoi = _phieuCodeService.GenerateMaXuatKho(Phieuxuatkho.MaDuan, Phieuxuatkho.MaYeucau);
            var phieuMoi = new phieuxuatkho
            {
                MaXuatkho = maXuatkhoMoi,
                MaYeucau = Phieuxuatkho.MaYeucau,
                MaDuan = Phieuxuatkho.MaDuan,
                MaNguoidung = Phieuxuatkho.MaNguoidung,
                NgayXuatkho = DateTime.Now,
                NgayChuanBi = DateTime.Now,
                TrangThai = "Đã xuất kho",
                GhiChu = $"Tạo từ phiếu {Phieuxuatkho.MaXuatkho} (xuất từng phần)"
            };
            _context.phieuxuatkho.Add(phieuMoi);

            var maYeucauDaXuat = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var VTxuatkho in vatTuCanXuat)
            {
                var khoTong = _context.khotongs
                    .FirstOrDefault(k => k.Makho == VTxuatkho.Makho && k.MaSanpham == VTxuatkho.MaSanpham);

                if (khoTong == null)
                {
                    TempData["Error"] = $"Vật tư {VTxuatkho.TenSanpham} không tồn tại trong kho tổng.";
                    return RedirectToAction("Phieuxuatkho", "Yeucau", new { area = "NhanvienKho" });
                }

                int tonKho = khoTong.SL ?? 0;
                var baseCode = YeucauUpdateHelper.GetBaseProductCode(VTxuatkho.MaSanpham ?? "") ?? "";
                int slGoc = slGocByBase.TryGetValue(baseCode, out var goc) ? goc : (VTxuatkho.SL ?? 0);
                int daXuat = daXuatByBase.TryGetValue(baseCode, out var dx) ? dx : 0;
                int conLai = Math.Max(0, slGoc - daXuat);
                int soLuongXuat = slMap.ContainsKey(VTxuatkho.ID) ? slMap[VTxuatkho.ID] : conLai;

                if (tonKho < soLuongXuat)
                {
                    TempData["Error"] = $"Không đủ tồn kho để xuất vật tư {VTxuatkho.TenSanpham}.";
                    return RedirectToAction("Phieuxuatkho", "Yeucau", new { area = "NhanvienKho" });
                }

                khoTong.SL = tonKho - soLuongXuat;
                _context.khotongs.Update(khoTong);

                // Tính đơn giá / thành tiền theo số lượng thực xuất
                decimal? donGia = VTxuatkho.DonGia;
                if (!donGia.HasValue && VTxuatkho.ThanhTien.HasValue && slGoc > 0)
                {
                    donGia = VTxuatkho.ThanhTien.Value / slGoc;
                }
                decimal? thanhTienXuat = null;
                if (donGia.HasValue)
                {
                    thanhTienXuat = donGia.Value * soLuongXuat;
                }
                else if (VTxuatkho.ThanhTien.HasValue && slGoc > 0)
                {
                    thanhTienXuat = (VTxuatkho.ThanhTien.Value / slGoc) * soLuongXuat;
                }

                // Dòng VT trên phiếu mới (phần đã xuất) - trạng thái Đã xuất kho
                _context.vtphieuxuatkho.Add(new vtphieuxuatkho
                {
                    MaXuatkho = maXuatkhoMoi,
                    MaYeucau = VTxuatkho.MaYeucau,
                    TenSanpham = VTxuatkho.TenSanpham,
                    MaSanpham = VTxuatkho.MaSanpham,
                    Makho = VTxuatkho.Makho,
                    HangSX = VTxuatkho.HangSX,
                    NhaCC = VTxuatkho.NhaCC,
                    SL = soLuongXuat,
                    DonVi = VTxuatkho.DonVi,
                    DonGia = donGia,
                    ThanhTien = thanhTienXuat,
                    NgayNhapkho = VTxuatkho.NgayNhapkho,
                    NgayBaohanh = VTxuatkho.NgayBaohanh,
                    ThoiGianBH = VTxuatkho.ThoiGianBH,
                    TrangThai = "Đã xuất kho",
                    LoaiCapPhat = VTxuatkho.LoaiCapPhat
                });

                // Không update VTxuatkho.SL (SL gốc phải giữ nguyên).
                // Chỉ cập nhật trạng thái nếu đã xuất đủ theo SL gốc.
                int conLaiSauXuat = Math.Max(0, conLai - soLuongXuat);
                if (conLaiSauXuat <= 0)
                {
                    VTxuatkho.TrangThai = "Đã xuất kho";
                    _context.vtphieuxuatkho.Update(VTxuatkho);
                }

                if (!string.IsNullOrEmpty(Phieuxuatkho.MaDuan))
                {
                    var VTduan = new khoduans
                    {
                        DAMaDuan = Phieuxuatkho.MaDuan,
                        TenSanpham = VTxuatkho.TenSanpham,
                        MaSanpham = VTxuatkho.MaSanpham,
                        DAMakho = VTxuatkho.Makho,
                        HangSX = VTxuatkho.HangSX,
                        NhaCC = VTxuatkho.NhaCC,
                        DonVi = VTxuatkho.DonVi,
                        SL = soLuongXuat,
                        NgayBaohanh = VTxuatkho.NgayBaohanh,
                        ThoiGianBH = VTxuatkho.ThoiGianBH,
                        TrangThai = "Đã xuất kho"
                    };
                    _context.khoduans.Add(VTduan);
                }
                else
                {
                    var vatTuNguoiDung = _context.khonguoidungs
                        .FirstOrDefault(nd => nd.NDMakho == VTxuatkho.Makho && nd.NDMaNguoidung == Phieuxuatkho.MaNguoidung && nd.MaSanpham == VTxuatkho.MaSanpham);

                    if (vatTuNguoiDung != null)
                    {
                        vatTuNguoiDung.SL = (vatTuNguoiDung.SL ?? 0) + soLuongXuat;
                        _context.khonguoidungs.Update(vatTuNguoiDung);
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
                            SL = soLuongXuat,
                            NgayBaohanh = VTxuatkho.NgayBaohanh,
                            ThoiGianBH = VTxuatkho.ThoiGianBH,
                            TrangThai = "Đang mượn"
                        };
                        _context.khonguoidungs.Add(VTkhonguoidung);
                    }
                }

                if (!string.IsNullOrEmpty(VTxuatkho.MaYeucau))
                {
                    maYeucauDaXuat.Add(VTxuatkho.MaYeucau);
                }
            }

            // Đồng bộ trạng thái vật tư trong yêu cầu dựa trên việc còn dòng chưa xuất hay không
            foreach (var maYc in maYeucauDaXuat)
            {
                var vtList = _context.vtyeucau
                    .Where(v => v.VTMaYeucau == maYc)
                    .ToList();

                foreach (var vtYc in vtList)
                {
                    bool conThieu = _context.vtphieuxuatkho.Any(vt =>
                        vt.MaYeucau == maYc &&
                        vt.MaSanpham == vtYc.MaSanpham &&
                        vt.TrangThai != "Đã xuất kho" &&
                        (vt.SL ?? 0) > 0);

                    vtYc.TrangThai = conThieu ? "Đang chuẩn bị hàng" : "Đã xuất kho";
                    _context.vtyeucau.Update(vtYc);
                }

                var yeuCau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == maYc);
                if (yeuCau != null)
                {
                    yeuCau.TrangThai = YeucauUpdateHelper.TinhTrangThaiYeuCau(vtList);
                    _context.yeucau.Update(yeuCau);
                }
            }

            // Chốt trạng thái phiếu tổng: nếu đã xuất hết theo SL gốc (ConLai == 0 cho mọi baseCode) => Đã xuất kho
            if (!string.IsNullOrWhiteSpace(Phieuxuatkho.MaYeucau))
            {
                var allRowsSameYCForClose = _context.vtphieuxuatkho
                    .AsNoTracking()
                    .Where(v => v.MaYeucau == Phieuxuatkho.MaYeucau)
                    .ToList();

                var slGocByBaseForClose = allRowsSameYCForClose
                    .GroupBy(v => YeucauUpdateHelper.GetBaseProductCode(v.MaSanpham ?? "") ?? "")
                    .ToDictionary(g => g.Key, g => g.Max(x => x.SL ?? 0), StringComparer.OrdinalIgnoreCase);

                var daXuatByBaseForClose = allRowsSameYCForClose
                    .Where(v => v.TrangThai == "Đã xuất kho" || v.TrangThai == "Hoàn thành")
                    .GroupBy(v => YeucauUpdateHelper.GetBaseProductCode(v.MaSanpham ?? "") ?? "")
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.SL ?? 0), StringComparer.OrdinalIgnoreCase);

                bool daXuatHet = slGocByBaseForClose.All(kvp =>
                {
                    var baseCode = kvp.Key ?? "";
                    var slGoc = kvp.Value;
                    var daXuat = daXuatByBaseForClose.TryGetValue(baseCode, out var dx) ? dx : 0;
                    return daXuat >= slGoc;
                });

                if (daXuatHet)
                {
                    Phieuxuatkho.TrangThai = "Đã xuất kho";
                    Phieuxuatkho.NgayHoanThanh = DateTime.Now;
                    Phieuxuatkho.NgayXuatkho = DateTime.Now;
                    Phieuxuatkho.GhiChu = "Đã xuất kho (đã xuất hết theo SL gốc)";
                    _context.phieuxuatkho.Update(Phieuxuatkho);
                }
            }

            _context.SaveChanges();

            // Gửi thông báo cho người yêu cầu khi xuất kho thành công
            var maYeucauListForNotif = maYeucauDaXuat.Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();

            foreach (var maYc in maYeucauListForNotif)
            {
                try
                {
                    _ = Task.Run(async () =>
                    {
                        System.Diagnostics.Debug.WriteLine($"[NV Kho] Gửi email cho người yêu cầu khi xuất kho. MaYeucau = {maYc}, MaXuatkho = {maXuatkhoMoi}");
                        await _emailService.SendNotificationToRequesterOnIssueAsync(maYc, maXuatkhoMoi);
                        System.Diagnostics.Debug.WriteLine($"[NV Kho] Đã gọi xong SendNotificationToRequesterOnIssueAsync cho MaYeucau = {maYc}");
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[NV Kho/Xuliphieuxuatkho] Lỗi gửi email xuất kho cho {maYc}: {ex.Message}");
                }
            }

            // Gửi email thông báo cho bộ phận kho khi xuất kho
            if (maYeucauListForNotif.Any())
            {
                try
                {
                    _ = Task.Run(async () =>
                    {
                        System.Diagnostics.Debug.WriteLine($"[NV Kho] Gửi email thông báo kho khi xuất kho. MaXuatkho = {maXuatkhoMoi}");
                        await _emailService.SendNotificationToWarehouseOnXuatKhoAsync(maXuatkhoMoi, maYeucauListForNotif.First());
                        System.Diagnostics.Debug.WriteLine($"[NV Kho] Đã gọi xong SendNotificationToWarehouseOnXuatKhoAsync cho MaXuatkho = {maXuatkhoMoi}");
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[NV Kho/Xuliphieuxuatkho] Lỗi gửi email thông báo kho khi xuất kho: {ex.Message}");
                }
            }

            TempData["Success"] = $"Xuất kho thành công! Đã tạo phiếu xuất kho mới: {maXuatkhoMoi}.";

            return RedirectToAction("Phieuxuatkho", "Yeucau", new { area = "NhanvienKho" });
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
                return RedirectToAction("Phieumuahang", "Yeucau", new { area = "NhanvienKho" });
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
                var targetMakho = EnsureKhoTongForNhapKho(VTPhieumuahang);

                // Log để debug giá tiền
                Console.WriteLine($"Copying from VTPhieumuahang: DonGia = {VTPhieumuahang.DonGia}, ThanhTien = {VTPhieumuahang.ThanhTien}, SL = {VTPhieumuahang.SL}");
                
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
                    DonGia = VTPhieumuahang.DonGia, // Copy giá đơn vị từ phiếu mua hàng
                    ThanhTien = VTPhieumuahang.ThanhTien, // Copy thành tiền từ phiếu mua hàng
                    TrangThai = "Chờ nhập kho",
                };
                
                // Nếu ThanhTien chưa có hoặc = 0, tính lại từ DonGia * SL
                if ((newvtphieunhapkho.ThanhTien == null || newvtphieunhapkho.ThanhTien == 0) && 
                    newvtphieunhapkho.DonGia != null && newvtphieunhapkho.DonGia > 0 && 
                    newvtphieunhapkho.SL != null && newvtphieunhapkho.SL > 0)
                {
                    newvtphieunhapkho.ThanhTien = newvtphieunhapkho.DonGia * newvtphieunhapkho.SL;
                    Console.WriteLine($"Calculated ThanhTien: {newvtphieunhapkho.ThanhTien} = {newvtphieunhapkho.DonGia} * {newvtphieunhapkho.SL}");
                }
                
                Console.WriteLine($"Final values: DonGia = {newvtphieunhapkho.DonGia}, ThanhTien = {newvtphieunhapkho.ThanhTien}");
                
                _context.vtphieunhapkho.Add(newvtphieunhapkho);
            }
            _context.SaveChanges();

                return RedirectToAction("Phieumuahang", "Yeucau", new { area = "NhanvienKho" });
        }

        private string EnsureKhoTongForNhapKho(vtphieumuahang vtPhieumuahang)
        {
            var ngayNhap = vtPhieumuahang.NgayNhapkho ?? DateTime.Now;
            var requestedMakho = NormalizeMakhoValue(vtPhieumuahang, ngayNhap);
            var existingKho = _context.khotongs.FirstOrDefault(k => k.Makho == requestedMakho);

            if (existingKho == null)
            {
                requestedMakho = MakhoHelper.BuildUniqueOfficialCode(
                    _context,
                    vtPhieumuahang.MaSanpham,
                    vtPhieumuahang.HangSX,
                    ngayNhap);

                var newKhoTong = new khotongs
                {
                    Makho = requestedMakho,
                    TenSanpham = vtPhieumuahang.TenSanpham,
                    MaSanpham = vtPhieumuahang.MaSanpham,
                    HangSX = vtPhieumuahang.HangSX,
                    NhaCC = vtPhieumuahang.NhaCC,
                    DonVi = vtPhieumuahang.DonVi,
                    SL = 0,
                    NgayNhapkho = ngayNhap,
                    TrangThai = "Chờ nhập kho",
                    LoaiCapPhat = "Kho tổng"
                };
                _context.khotongs.Add(newKhoTong);
                _context.SaveChanges();
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
                    NgayNhapkho = ngayNhap,
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

        private string NormalizeMakhoValue(vtphieumuahang vtPhieumuahang, DateTime ngayNhap)
        {
            if (!string.IsNullOrWhiteSpace(vtPhieumuahang.Makho) &&
                !vtPhieumuahang.Makho.Equals("VT mới", StringComparison.OrdinalIgnoreCase))
            {
                var normalizedInput = vtPhieumuahang.Makho.Trim().ToUpperInvariant();
                var existing = _context.khotongs.FirstOrDefault(k => k.Makho == normalizedInput);
                if (existing != null)
                {
                    return existing.Makho;
                }
            }

            return MakhoHelper.BuildOfficialCode(vtPhieumuahang.MaSanpham, vtPhieumuahang.HangSX, ngayNhap);
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
            string currentArea = "NhanvienKho";
            
            try
            {

                // Kiểm tra dữ liệu đầu vào
                if (TenSanpham == null || TenSanpham.Length == 0)
                {
                    TempData["Error"] = "Vui lòng nhập ít nhất một vật tư!";
                    return RedirectToAction("ThemPhieunhapkho", "Yeucau", new { area = "NhanvienKho" });
                }

                if (string.IsNullOrEmpty(LoaiNhapkho))
                {
                    TempData["Error"] = "Vui lòng chọn loại nhập kho!";
                    return RedirectToAction("ThemPhieunhapkho", "Yeucau", new { area = "NhanvienKho" });
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
                phieunhapkho.NgayNhapkho = null; // Để trống, chỉ lưu khi bộ phận kho nhập kho
                
                // Thiết lập trạng thái ban đầu theo quy trình duyệt
                // Nếu có dự án: chờ quản lý dự án duyệt
                // Nếu không có dự án (cá nhân): chờ Giám đốc duyệt
                if (!string.IsNullOrEmpty(phieunhapkho.MaDuan))
                {
                    phieunhapkho.TrangThai = "Chờ quản lý dự án duyệt";
                }
                else
                {
                    phieunhapkho.TrangThai = "Chờ Giám đốc duyệt";
                }

                // Tạo mã yêu cầu nhập kho: MãDựÁnNK YYMMDD-01 hoặc MãNhânViênNK YYMMDD-01 (PhieuCodeService)
                if (string.IsNullOrEmpty(phieunhapkho.MaYeucau))
                {
                    string maDuanForYc = (LoaiNhapkho == "duan" && !string.IsNullOrEmpty(phieunhapkho.MaDuan)) ? phieunhapkho.MaDuan : null;
                    string maYeucauDacBiet = _phieuCodeService.GenerateMaYeucauNhapKho(maDuanForYc, maNv);

                    string ycMaDuan = null;
                    if (!string.IsNullOrEmpty(phieunhapkho.MaDuan))
                    {
                        var duanExists = _context.duans.FirstOrDefault(d => d.MaDuan == phieunhapkho.MaDuan);
                        if (duanExists == null)
                            duanExists = _context.duans.AsEnumerable().FirstOrDefault(d => d.MaDuan != null && d.MaDuan.Equals(phieunhapkho.MaDuan, StringComparison.OrdinalIgnoreCase));
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
                
                // Xử lý vật tư - CHỈ tạo bản ghi, KHÔNG trừ từ kho
                for (int i = 0; i < count; i++)
                {
                    if (string.IsNullOrEmpty(TenSanpham[i])) continue;

                    decimal donGia = DonGia != null && i < DonGia.Length ? DonGia[i] : 0;
                    int soLuong = SL?[i] ?? 0;
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
                        DonGia = donGia, // Lưu đơn giá
                        ThanhTien = thanhTien, // Tính và lưu thành tiền
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
                            System.Diagnostics.Debug.WriteLine($"[NhanvienKho/ThemPhieunhapkhoSQL] Lỗi gửi email tạo phiếu nhập kho: {exInner.Message}");
                        }
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[NhanvienKho/ThemPhieunhapkhoSQL] Lỗi khởi chạy task gửi email: {ex.Message}");
                }

                TempData["Success"] = "Tạo phiếu nhập kho thành công!";
                return RedirectToAction("Phieunhapkho", "Yeucau", new { area = "NhanvienKho" });
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
                    // Trưởng dự án duyệt
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
                    // Giám đốc duyệt
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
                    // Kho xử lý nhập kho
                    // LƯU Ý QUAN TRỌNG: Khi kho duyệt, CHỈ cộng vào kho tổng
                    // KHÔNG trừ từ kho dự án/cá nhân ở đây
                    // Chỉ trừ khi người nhận xác nhận nhận hàng (trạng thái "Đã xác nhận nhận hàng")
                    Phieunhapkho.TrangThai = "Đã nhập kho";
                    Phieunhapkho.NgayNhapkho = DateTime.Now;
                    
                    foreach (var VTPhieunhapkho in VTPhieunhapkholist)
                    {
                        // Cộng vào kho tổng (cho cả phiếu từ mua hàng và phiếu từ dự án/cá nhân)
                        // Kiểm tra xem entity đã được track chưa để tránh lỗi tracking
                        // Lưu ý: Kiểm tra cả NhaCC để tách riêng nếu nhà cung cấp khác nhau
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
                            // Kiểm tra xem có entity với cùng MaSanpham, Makho, HangSX, NhaCC đang được track không
                            // (không cần TenSanpham vì có thể khác nhau nhưng vẫn là cùng vật tư)
                            var existingTracked = _context.khotongs.Local
                                .FirstOrDefault(k => 
                                    k.MaSanpham == VTPhieunhapkho.MaSanpham &&
                                    k.Makho == VTPhieunhapkho.Makho &&
                                    k.HangSX == VTPhieunhapkho.HangSX &&
                                    (k.NhaCC == VTPhieunhapkho.NhaCC || 
                                     (string.IsNullOrWhiteSpace(k.NhaCC) && string.IsNullOrWhiteSpace(VTPhieunhapkho.NhaCC))));
                            
                            if (existingTracked != null)
                            {
                                // Cập nhật entity đã được track
                                existingTracked.SL += VTPhieunhapkho.SL ?? 0;
                            }
                            else
                            {
                                // Kiểm tra lại trong database với điều kiện đầy đủ (không cần TenSanpham)
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
                        }
                        
                        VTPhieunhapkho.TrangThai = "Đã nhập kho";
                        _context.vtphieunhapkho.Update(VTPhieunhapkho);
                    }
                    
                    // Tự động tạo phiếu xuất kho nếu có yêu cầu ban đầu và chưa có phiếu xuất kho
                    // Chỉ áp dụng cho các yêu cầu xuất kho; KHÔNG áp dụng cho các yêu cầu nhập kho hoàn trả
                    bool isNhapKhoOnlyFlow = !string.IsNullOrEmpty(Phieunhapkho.MaYeucau) &&
                        _context.yeucau.Any(y => y.MaYeucau == Phieunhapkho.MaYeucau
                            && y.TenYeucau == "Yêu cầu nhập kho");

                    if (!string.IsNullOrEmpty(Phieunhapkho.MaYeucau) && !isNhapKhoOnlyFlow)
                    {
                        // Lấy tất cả phiếu xuất kho liên quan đến yêu cầu này (có thể >1 phiếu)
                        var phieuXuatLienQuan = _context.phieuxuatkho
                            .Where(px => px.MaYeucau == Phieunhapkho.MaYeucau)
                            .ToList();

                        var phieuXuatCanCapNhatList = new List<phieuxuatkho>(phieuXuatLienQuan);
                        
                        if (!phieuXuatLienQuan.Any())
                        {
                            // Lấy thông tin yêu cầu ban đầu
                            var yeucauBanDau = _context.yeucau
                                .FirstOrDefault(y => y.MaYeucau == Phieunhapkho.MaYeucau);
                            
                            if (yeucauBanDau != null)
                            {
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
                                
                                // Lấy danh sách vật tư yêu cầu ban đầu
                                var danhSachVatTuYC = _context.vtyeucau
                                    .Where(vt => vt.VTMaYeucau == Phieunhapkho.MaYeucau)
                                    .ToList();
                                
                                // Tạo vật tư trong phiếu xuất kho dựa trên vật tư trong phiếu nhập kho
                                foreach (var VTPhieunhapkho in VTPhieunhapkholist)
                                {
                                    // Tìm vật tư tương ứng trong yêu cầu ban đầu
                                    var vtYeucau = danhSachVatTuYC.FirstOrDefault(vt => 
                                        vt.MaSanpham == VTPhieunhapkho.MaSanpham &&
                                        vt.YCMakho == VTPhieunhapkho.Makho);
                                    
                                    if (vtYeucau != null)
                                    {
                                        // Lấy thông tin từ kho tổng để đảm bảo đúng thông tin
                                        var khotong = _context.khotongs.FirstOrDefault(k => 
                                            k.MaSanpham == VTPhieunhapkho.MaSanpham &&
                                            k.Makho == VTPhieunhapkho.Makho);
                                        
                                        if (khotong != null)
                                        {
                                            // Tính số lượng xuất kho (lấy số lượng nhỏ nhất giữa yêu cầu và số lượng nhập)
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
                                
                                // Sau khi tạo phiếu xuất kho, kiểm tra tồn kho và tự động chuyển trạng thái như phiếu xuất kho cơ bản
                                var VTPhieuxuatkhoList = _context.vtphieuxuatkho
                                    .Where(vt => vt.MaXuatkho == MaXuatkho)
                                    .ToList();
                                
                                bool duHang = true;
                                var vatTuThieu = new List<vtphieuxuatkho>();
                                
                                foreach (var VTxuatkho in VTPhieuxuatkhoList)
                                {
                                    var khotong = _context.khotongs.FirstOrDefault(k => k.Makho == VTxuatkho.Makho && k.MaSanpham == VTxuatkho.MaSanpham);
                                    
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
                                    // Đủ hàng → tự động chuyển sang "Đang chuẩn bị hàng" (vì hàng vừa nhập vào nên chắc chắn đủ)
                                    newPhieuxuatkho.TrangThai = "Đang chuẩn bị hàng";
                                    newPhieuxuatkho.NgayChuanBi = DateTime.Now;
                                    _context.phieuxuatkho.Update(newPhieuxuatkho);
                                    
                                    // Cập nhật trạng thái vật tư
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
                                    // Thiếu hàng (trường hợp này hiếm vì vừa nhập vào, nhưng để an toàn)
                                    newPhieuxuatkho.TrangThai = "Thiếu hàng";
                                    newPhieuxuatkho.GhiChu = "Không đủ số lượng tồn kho.";
                                    _context.phieuxuatkho.Update(newPhieuxuatkho);
                                    _context.SaveChanges();
                                    Console.WriteLine($"Đã tự động tạo phiếu xuất kho {MaXuatkho} cho yêu cầu {Phieunhapkho.MaYeucau} nhưng thiếu hàng");
                                }
                                
                                phieuXuatCanCapNhatList.Add(newPhieuxuatkho);
                            }
                        }

                        foreach (var pxCapNhat in phieuXuatCanCapNhatList
                                     .GroupBy(px => px.MaXuatkho)
                                     .Select(g => g.First()))
                        {
                            CapNhatPhieuXuatSauNhapHang(pxCapNhat, VTPhieunhapkholist);
                        }
                    }
                }
                else if (Phieunhapkho.TrangThai == "Đã nhập kho" && boPhan2 == "BP kho")
                {
                    // Khi trạng thái là "Đã nhập kho" và người nhận xác nhận nhận hàng
                    // MỚI trừ từ kho dự án/cá nhân (sản lượng thừa được trả lại)
                    Phieunhapkho.TrangThai = "Đã xác nhận nhận hàng";
                    
                    foreach (var VTPhieunhapkho in VTPhieunhapkholist)
                    {
                        // Trừ từ kho dự án/cá nhân khi người nhận xác nhận nhận hàng
                        bool isFromDuanOrCaNhan = false;
                        
                        // Kiểm tra từ dự án: Nếu có MaDuan và có vật tư trong kho dự án
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
                                // Trừ từ kho dự án
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
                        
                        // Kiểm tra từ cá nhân: Nếu không có MaDuan và có vật tư trong kho cá nhân
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
                                // Trừ từ kho cá nhân
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
                    // Hoàn thành phiếu nhập kho
                    Phieunhapkho.TrangThai = "Hoàn thành";
                    
                    foreach (var VTPhieunhapkho in VTPhieunhapkholist)
                    {
                        VTPhieunhapkho.TrangThai = "Hoàn thành";
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
            
            // Đồng bộ trạng thái bảng yeucau dựa theo chi tiết:
            // - Với yêu cầu nhập kho (mã có thể là NHAPKHO_ hoặc dạng ...NK_...), vật tư nằm ở vtphieunhapkho
            // - Nếu không đồng bộ, yeucau.TrangThai có thể bị kẹt ở "Không có vật tư" dù chi tiết đã "Đã nhập kho"
            if (!string.IsNullOrWhiteSpace(Phieunhapkho.MaYeucau))
            {
                System.Diagnostics.Debug.WriteLine($"[NhanvienKho/Xuliphieunhapkho] DongBoTrangThaiYeuCau MaYeucau={Phieunhapkho.MaYeucau}, TrangThaiPhieu={Phieunhapkho.TrangThai}");
                YeucauUpdateHelper.DongBoTrangThaiYeuCau(_context, Phieunhapkho.MaYeucau);
            }

            _context.SaveChanges();
            return RedirectToAction("Phieunhapkho", "Yeucau", new { area = "NhanvienKho" });
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

            return RedirectToAction("Phieunhapkho", "Yeucau", new { area = "NhanvienKho" });
        }

        private void CapNhatPhieuXuatSauNhapHang(phieuxuatkho phieuXuat, List<vtphieunhapkho> vtNhapList)
        {
            PhieuXuatAllocationHelper.CapNhatPhieuXuatSauNhapHang(_context, phieuXuat, vtNhapList);
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

            return RedirectToAction("Yeucau", "Yeucau", new { area = "NhanvienKho" });
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

        // XÁC NHẬN HÀNG
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
                        // Nếu không tìm thấy đúng Makho (do có hậu tố / tiền tố mới), thử khớp linh hoạt hơn theo cùng mã sản phẩm
                        if (khotong == null)
                        {
                            string makho = vt.Makho ?? "";
                            khotong = _context.khotongs
                                .FirstOrDefault(k =>
                                    k.MaSanpham == vt.MaSanpham &&
                                    (
                                        k.Makho == makho ||
                                        ((k.Makho ?? "") != "" && makho != "" &&
                                         (k.Makho.StartsWith(makho) || makho.StartsWith(k.Makho)))
                                    ));
                        }
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
                                return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "NhanvienKho" });
                            }
                            
                            khotong.SL -= vt.SL;
                            _context.khotongs.Update(khotong);
                        }
                        else
                        {
                            TempData["ErrorMessage"] = $"Không thể xuất kho: Vật tư {vt.TenSanpham} không tồn tại trong kho";
                            return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "NhanvienKho" });
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
                return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "NhanvienKho" });
            }

            TempData["ErrorMessage"] = "Phiếu không hợp lệ hoặc đã được xác nhận!";
            return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "NhanvienKho" });
        }

        // Các method DongsBoTrangThaiVatTu và AutoDongBoTrangThai đã được kế thừa từ BaseYeucauController

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

            // Lấy giá tiền từ phiếu mua hàng nếu phiếu nhập kho không có giá
            if (!string.IsNullOrEmpty(phieunhapkho.MaYeucau))
            {
                var phieumuahang = _context.phieumuahang
                    .FirstOrDefault(pm => pm.MaYeucau == phieunhapkho.MaYeucau);

                if (phieumuahang != null)
                {
                    var vtphieumuahang = _context.vtphieumuahang
                        .Where(vt => vt.MaMuahang == phieumuahang.MaMuahang)
                        .ToList();

                    // Cập nhật giá tiền cho các vật tư trong phiếu nhập kho từ phiếu mua hàng
                    foreach (var vtnhapkho in vtphieunhapkho)
                    {
                        // Tìm vật tư tương ứng trong phiếu mua hàng theo mã sản phẩm và tên sản phẩm
                        var vtmuahang = vtphieumuahang.FirstOrDefault(vt => 
                            vt.MaSanpham == vtnhapkho.MaSanpham && 
                            vt.TenSanpham == vtnhapkho.TenSanpham);

                        if (vtmuahang != null)
                        {
                            // Nếu phiếu nhập kho không có giá hoặc giá = 0, lấy từ phiếu mua hàng
                            if (vtnhapkho.DonGia == null || vtnhapkho.DonGia == 0)
                            {
                                vtnhapkho.DonGia = vtmuahang.DonGia;
                            }

                            if (vtnhapkho.ThanhTien == null || vtnhapkho.ThanhTien == 0)
                            {
                                vtnhapkho.ThanhTien = vtmuahang.ThanhTien;
                                
                                // Nếu thành tiền vẫn = 0, tính từ đơn giá * số lượng
                                if ((vtnhapkho.ThanhTien == null || vtnhapkho.ThanhTien == 0) && 
                                    vtnhapkho.DonGia != null && vtnhapkho.DonGia > 0 && 
                                    vtnhapkho.SL != null && vtnhapkho.SL > 0)
                                {
                                    vtnhapkho.ThanhTien = vtnhapkho.DonGia * vtnhapkho.SL;
                                }
                            }
                        }
                    }
                }
            }

            var yeucau = _context.yeucau
                .FirstOrDefault(y => y.MaYeucau == phieunhapkho.MaYeucau);

            ViewBag.Phieunhapkho = phieunhapkho;
            ViewBag.VTPhieunhapkho = vtphieunhapkho;
            ViewBag.Yeucau = yeucau;

            return View();
        }

        // Method InPhietrahang đã được kế thừa từ BaseYeucauController

        // In phiáº¿u xuáº¥t kho
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

            foreach (var vt in vtphieuxuatkho)
            {
                if ((vt.DonGia == null || vt.DonGia == 0) && vt.ThanhTien.HasValue && vt.SL.HasValue && vt.SL > 0)
                {
                    vt.DonGia = vt.ThanhTien / vt.SL;
                }

                if ((vt.ThanhTien == null || vt.ThanhTien == 0) && vt.DonGia.HasValue && vt.SL.HasValue)
                {
                    vt.ThanhTien = vt.DonGia * vt.SL;
                }
            }

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
