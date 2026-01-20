using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Webkho_20241021.Models;
using System.Linq;
using System.Collections.Generic;
using System;
using OfficeOpenXml;
using Webkho_20241021.Models.ViewModels;
using Webkho_20241021.Services;
using Webkho_20241021.Helpers;

namespace Webkho_20241021.Areas.NhanvienKho.Controllers
{
    [Area("NhanvienKho")]
    [Authorize(Roles = "Nhân viên-BP kho")]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public ActionResult Tongkho(int page = 1, int pageSize = 20, string q = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var query = _context.khotongs
                .Where(k => !string.Equals(k.Makho, "VT mới", StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(k.MaSanpham, "VT-MOI", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(q))
            {
                var keyword = q.Trim();
                query = query.Where(k =>
                    (k.TenSanpham ?? "").Contains(keyword) ||
                    (k.MaSanpham ?? "").Contains(keyword) ||
                    (k.Makho ?? "").Contains(keyword) ||
                    (k.HangSX ?? "").Contains(keyword) ||
                    (k.NhaCC ?? "").Contains(keyword) ||
                    (k.DuAn ?? "").Contains(keyword)
                );
            }

            var total = query.Count();
            var items = query
                .OrderByDescending(k => k.NgayNhapkho)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
            ViewBag.Q = q;
            return View(items);
        }

        public IActionResult Trangchu()
        {
            return View();
        }

        public IActionResult Themthietbi()
        {
            return View();
        }

        public IActionResult Import()
        {
            return View();
        }

        [HttpGet]
        public IActionResult XuatNhapTon(string q = null)
        {
            var service = new InventoryMovementService(_context);
            var viewModel = service.BuildPageViewModel(q);

            ViewBag.LayoutPath = "~/Areas/NhanvienKho/Views/Layout/Layout.cshtml";
            ViewBag.AreaName = "NhanvienKho";

            return View("~/Views/Shared/Warehouse/XuatNhapTon.cshtml", viewModel);
        }

        [HttpPost]
        public IActionResult ThemvattuSQL(string[] TenSanpham, string[] MaSanpham, string[] HangSX, string[] NhaCC, int[] SL, string[] DonVi, DateTime?[] NgayBaohanh, DateTime?[] ThoiGianBH, string[] DuAn)
        {
            // Duyệt theo độ dài nhỏ nhất của các mảng bắt buộc để tránh IndexOutOfRange
            int count = new[]
            {
                TenSanpham?.Length ?? 0,
                MaSanpham?.Length ?? 0,
                HangSX?.Length ?? 0,
                NhaCC?.Length ?? 0,
                SL?.Length ?? 0,
                DonVi?.Length ?? 0
            }.Min();

            // Tập hợp mã kho đã sử dụng (bao gồm dữ liệu hiện có và các bản ghi đang thêm trong batch này)
            var usedMakho = new HashSet<string>(
                _context.khotongs
                    .Select(k => k.Makho)
                    .Where(m => !string.IsNullOrWhiteSpace(m)),
                StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < count; i++)
            {
                if (string.IsNullOrWhiteSpace(TenSanpham[i]) || string.IsNullOrWhiteSpace(MaSanpham[i]) ||
                    SL[i] < 0 || string.IsNullOrWhiteSpace(DonVi[i]))
                {
                    continue;
                }

                var existingItem = _context.khotongs
                    .FirstOrDefault(k => k.TenSanpham == TenSanpham[i] && k.MaSanpham == MaSanpham[i] && k.HangSX == HangSX[i]);

                if (existingItem != null)
                {
                    existingItem.SL += SL[i];
                    // Nếu file có cột Dự án thì luôn đồng bộ vào vật tư hiện có
                    if (DuAn != null && i < DuAn.Length && !string.IsNullOrWhiteSpace(DuAn[i]))
                    {
                        existingItem.DuAn = DuAn[i].Trim();
                    }
                    _context.khotongs.Update(existingItem);
                }
                else
                {
                    var ngayNhap = DateTime.Now;
                    var makho = MakhoHelper.BuildUniqueOfficialCode(
                        _context,
                        MaSanpham[i],
                        HangSX[i],
                        ngayNhap,
                        usedMakho);

                    var khotongs = new khotongs
                    {
                        TenSanpham = TenSanpham[i],
                        MaSanpham = MaSanpham[i],
                        HangSX = HangSX[i],
                        NhaCC = NhaCC[i],
                        SL = SL[i],
                        DonVi = DonVi[i],
                        // Hai cột ngày có thể thiếu – truy cập an toàn theo chỉ số
                        NgayBaohanh = (NgayBaohanh != null && i < NgayBaohanh.Length) ? NgayBaohanh[i] : null,
                        ThoiGianBH = (ThoiGianBH != null && i < ThoiGianBH.Length) ? ThoiGianBH[i] : null,
                        DuAn = (DuAn != null && i < DuAn.Length && !string.IsNullOrWhiteSpace(DuAn[i])) ? DuAn[i] : null,
                        Makho = makho,
                        NgayNhapkho = ngayNhap,
                        TrangThai = "Tồn kho"
                    };

                    _context.khotongs.Add(khotongs);
                    usedMakho.Add(makho);
                }
            }

            int totalAdded = _context.SaveChanges();
            TempData["Success"] = "Thêm vật tư thành công!";
            return RedirectToAction("Tongkho", "Home", new { area = "NhanvienKho" });
        }

        [HttpGet]
        public IActionResult TimKiem(string timkiem)
        {
            var results = _context.khotongs
                .Where(k => k.TenSanpham.Contains(timkiem) || k.MaSanpham.Contains(timkiem))
                .ToList();
            return Json(results);
        }

        [HttpGet]
        public IActionResult ChiTietVatTu(string maSanpham)
        {
            var service = new InventoryMovementService(_context);
            var detail = service.BuildDetail(maSanpham);

            if (detail.Summary == null && (detail.Transactions == null || detail.Transactions.Count == 0))
            {
                return Json(new { success = false, message = "Không tìm thấy dữ liệu cho vật tư này." });
            }

            return Json(new { success = true, data = detail });
        }

        [HttpPost]
        public IActionResult ImportSQL(string[] TenSanpham, string[] MaSanpham, string[] HangSX, string[] NhaCC, int[] SL, string[] DonVi, DateTime?[] NgayBaohanh, DateTime?[] ThoiGianBH, string[] DuAn)
        {
            // Duyệt theo độ dài nhỏ nhất của các mảng bắt buộc để tránh IndexOutOfRange
            int count = new[]
            {
                TenSanpham?.Length ?? 0,
                MaSanpham?.Length ?? 0,
                HangSX?.Length ?? 0,
                NhaCC?.Length ?? 0,
                SL?.Length ?? 0,
                DonVi?.Length ?? 0
            }.Min();
            int added = 0;
            int updated = 0;

            // Tập hợp mã kho đã sử dụng (bao gồm dữ liệu hiện có và các bản ghi đang thêm trong batch này)
            var usedMakho = new HashSet<string>(
                _context.khotongs
                    .Select(k => k.Makho)
                    .Where(m => !string.IsNullOrWhiteSpace(m)),
                StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < count; i++)
            {
                if (string.IsNullOrWhiteSpace(TenSanpham[i]) || string.IsNullOrWhiteSpace(MaSanpham[i]) ||
                    SL[i] < 0 || string.IsNullOrWhiteSpace(DonVi[i]))
                {
                    continue;
                }

                var existingItem = _context.khotongs.FirstOrDefault(k =>
                    k.TenSanpham == TenSanpham[i] &&
                    k.MaSanpham == MaSanpham[i] &&
                    k.HangSX == HangSX[i]);

                if (existingItem != null)
                {
                    existingItem.SL += SL[i];
                    // Nếu file có cột Dự án thì luôn đồng bộ vào vật tư hiện có
                    if (DuAn != null && i < DuAn.Length && !string.IsNullOrWhiteSpace(DuAn[i]))
                    {
                        existingItem.DuAn = DuAn[i].Trim();
                    }
                    _context.khotongs.Update(existingItem);
                    updated++;
                }
                else
                {
                    var ngayNhap = DateTime.Now;
                    var makho = MakhoHelper.BuildUniqueOfficialCode(
                        _context,
                        MaSanpham[i],
                        HangSX[i],
                        ngayNhap,
                        usedMakho);

                    var newKhotong = new khotongs
                    {
                        TenSanpham = TenSanpham[i],
                        MaSanpham = MaSanpham[i],
                        HangSX = HangSX[i],
                        NhaCC = NhaCC[i],
                        SL = SL[i],
                        DonVi = DonVi[i],
                        // Hai cột ngày có thể thiếu – truy cập an toàn theo chỉ số
                        NgayBaohanh = (NgayBaohanh != null && i < NgayBaohanh.Length) ? NgayBaohanh[i] : null,
                        ThoiGianBH = (ThoiGianBH != null && i < ThoiGianBH.Length) ? ThoiGianBH[i] : null,
                        DuAn = (DuAn != null && i < DuAn.Length && !string.IsNullOrWhiteSpace(DuAn[i])) ? DuAn[i] : null,
                        Makho = makho,
                        NgayNhapkho = ngayNhap,
                        TrangThai = "Tồn kho"
                    };

                    _context.khotongs.Add(newKhotong);
                    usedMakho.Add(makho);
                    added++;
                }
            }

            _context.SaveChanges();
            
            // Kiểm tra nếu là AJAX request
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.ContentType?.Contains("multipart/form-data") == true)
            {
                return Json(new { 
                    success = true, 
                    added = added, 
                    updated = updated,
                    message = $"Import thành công: thêm {added} dòng, cập nhật {updated} dòng."
                });
            }
            
            TempData["Success"] = $"Import thành công: thêm {added} dòng, cập nhật {updated} dòng.";
            return RedirectToAction("Tongkho", "Home", new { area = "NhanvienKho" });
        }

        public IActionResult VatTuMoi(int page = 1, int pageSize = 20, string q = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var query = _context.khotongs.Where(k => k.LoaiCapPhat == "ChoNhanVienMoi");
            if (!string.IsNullOrWhiteSpace(q))
            {
                var keyword = q.Trim();
                query = query.Where(k =>
                    (k.TenSanpham ?? "").Contains(keyword) ||
                    (k.MaSanpham ?? "").Contains(keyword) ||
                    (k.Makho ?? "").Contains(keyword) ||
                    (k.HangSX ?? "").Contains(keyword) ||
                    (k.NhaCC ?? "").Contains(keyword) ||
                    (k.DuAn ?? "").Contains(keyword)
                );
            }

            var total = query.Count();
            var items = query
                .OrderByDescending(k => k.NgayNhapkho)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
            ViewBag.Q = q;
            return View("VatTuMoi", items);
        }
        public ActionResult CapPhatNvMoi()
        {
            var capPhatNvMoi = _context.khotongs
                .Where(k => k.LoaiCapPhat == "ChoNhanVienMoi")
                .ToList();

            return View("Tongkho", capPhatNvMoi);
        }

        // Action debug để kiểm tra dữ liệu
        public ActionResult DebugCapPhatNvMoi()
        {
            var allKhotongs = _context.khotongs.ToList();
            var allVtphieuxuatkho = _context.vtphieuxuatkho.ToList();

            var capPhatNvMoiFromKhotongs = allKhotongs.Where(k => k.LoaiCapPhat == "ChoNhanVienMoi").ToList();
            var capPhatNvMoiFromVtphieuxuatkho = allVtphieuxuatkho.Where(k => k.LoaiCapPhat == "ChoNhanVienMoi").ToList();

            ViewBag.TotalKhotongsRecords = allKhotongs.Count;
            ViewBag.TotalVtphieuxuatkhoRecords = allVtphieuxuatkho.Count;
            ViewBag.CapPhatNvMoiFromKhotongs = capPhatNvMoiFromKhotongs.Count;
            ViewBag.CapPhatNvMoiFromVtphieuxuatkho = capPhatNvMoiFromVtphieuxuatkho.Count;
            ViewBag.AllLoaiCapPhatKhotongs = allKhotongs.Select(k => k.LoaiCapPhat).Distinct().ToList();
            ViewBag.AllLoaiCapPhatVtphieuxuatkho = allVtphieuxuatkho.Select(k => k.LoaiCapPhat).Distinct().ToList();

            return View("Tongkho", capPhatNvMoiFromVtphieuxuatkho);
        }

        public IActionResult KhoDuAn(int page = 1, int pageSize = 20, string q = null, string duAn = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            // Lấy mã nhân viên từ session để lọc theo cá nhân
            var maNv = HttpContext.Session.GetString("MaNguoidung");

            // Lấy vật tư từ phiếu xuất kho dự án của cá nhân và join với vtphieunhapkho qua phieunhapkho
            var query = (from vtx in _context.vtphieuxuatkho
                        join px in _context.phieuxuatkho on vtx.MaXuatkho equals px.MaXuatkho
                        where !string.IsNullOrEmpty(px.MaDuan) && !string.IsNullOrEmpty(vtx.MaYeucau) // Chỉ lấy phiếu xuất kho dự án
                            && px.MaNguoidung == maNv // Lọc theo người dùng hiện tại
                        // Join với duans để lấy tên dự án
                        join da in _context.duans on px.MaDuan equals da.MaDuan into daGroup
                        from da in daGroup.DefaultIfEmpty()
                        // Left join với vtphieunhapkho qua MaYeucau và MaSanpham
                        join vtn in _context.vtphieunhapkho 
                            on new { MaYeucau = vtx.MaYeucau, MaSanpham = vtx.MaSanpham } 
                            equals new { MaYeucau = vtn.MaYeucau, MaSanpham = vtn.MaSanpham } into vtnGroup
                        from vtn in vtnGroup.DefaultIfEmpty()
                        // Ưu tiên dữ liệu từ vtphieunhapkho, nếu không có thì dùng vtphieuxuatkho
                        select new { 
                            khotong = new khotongs
                            {
                                MaSanpham = vtn != null ? vtn.MaSanpham : vtx.MaSanpham,
                                TenSanpham = vtn != null ? vtn.TenSanpham : vtx.TenSanpham,
                                Makho = vtn != null ? vtn.Makho : vtx.Makho,
                                HangSX = vtn != null ? vtn.HangSX : vtx.HangSX,
                                NhaCC = vtn != null ? vtn.NhaCC : vtx.NhaCC,
                                DuAn = da != null ? da.TenDuan : px.MaDuan, // Dùng tên dự án nếu có
                                SL = vtn != null ? (vtn.SL ?? 0) : (vtx.SL ?? 0),
                                DonVi = vtn != null ? vtn.DonVi : vtx.DonVi,
                                TrangThai = vtn != null ? vtn.TrangThai : vtx.TrangThai // Trạng thái của kho dự án cá nhân
                            },
                            MaDuan = px.MaDuan
                        }).Distinct();

            // Lấy danh sách dự án từ phieuxuatkho của người dùng hiện tại
            var duAnList = (from px in _context.phieuxuatkho
                           join da in _context.duans on px.MaDuan equals da.MaDuan into daGroup
                           from da in daGroup.DefaultIfEmpty()
                           where !string.IsNullOrEmpty(px.MaDuan) && px.MaNguoidung == maNv
                           select da != null ? da.TenDuan : px.MaDuan)
                           .Distinct()
                           .OrderBy(x => x)
                           .ToList();

            // Lọc theo dự án nếu có
            if (!string.IsNullOrWhiteSpace(duAn))
            {
                // Kiểm tra xem duAn là MaDuan hay TenDuan
                var isMaDuan = _context.duans.Any(d => d.MaDuan == duAn);
                if (isMaDuan)
                {
                    var tenDuan = _context.duans.Where(d => d.MaDuan == duAn).Select(d => d.TenDuan).FirstOrDefault();
                    query = query.Where(x => x.khotong.DuAn == tenDuan || x.khotong.DuAn == duAn || x.MaDuan == duAn);
                }
                else
                {
                    query = query.Where(x => x.khotong.DuAn == duAn);
                }
            }

            // Tìm kiếm
            if (!string.IsNullOrWhiteSpace(q))
            {
                var keyword = q.Trim();
                query = query.Where(x =>
                    (x.khotong.TenSanpham ?? "").Contains(keyword) ||
                    (x.khotong.MaSanpham ?? "").Contains(keyword) ||
                    (x.khotong.Makho ?? "").Contains(keyword) ||
                    (x.khotong.HangSX ?? "").Contains(keyword) ||
                    (x.khotong.NhaCC ?? "").Contains(keyword) ||
                    (x.khotong.DuAn ?? "").Contains(keyword) ||
                    (x.MaDuan ?? "").Contains(keyword)
                );
            }

            var total = query.Count();
            var itemsWithMaDuan = query
                .OrderByDescending(x => x.khotong.NgayNhapkho)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var items = itemsWithMaDuan.Select(x => x.khotong).ToList();
            var maDuanDict = itemsWithMaDuan.ToDictionary(x => x.khotong.Makho ?? "", x => x.MaDuan ?? "");

            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
            ViewBag.Q = q;
            ViewBag.DuAn = duAn;
            ViewBag.DuAnList = duAnList;
            ViewBag.MaDuanDict = maDuanDict;
            return View("KhoDuAn", items);
        }

        // Export Excel tổng kho
        public IActionResult ExportTongkho(string q = null)
        {
            var query = _context.khotongs.AsQueryable();
            
            if (!string.IsNullOrWhiteSpace(q))
            {
                var keyword = q.Trim();
                query = query.Where(k =>
                    (k.TenSanpham ?? "").Contains(keyword) ||
                    (k.MaSanpham ?? "").Contains(keyword) ||
                    (k.Makho ?? "").Contains(keyword) ||
                    (k.HangSX ?? "").Contains(keyword) ||
                    (k.NhaCC ?? "").Contains(keyword) ||
                    (k.DuAn ?? "").Contains(keyword)
                );
            }

            var items = query.OrderByDescending(k => k.NgayNhapkho).ToList();

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Tổng kho");

                // Hàng 1: Tiêu đề gộp tất cả các cột
                worksheet.Cells[1, 1, 1, 14].Merge = true;
                worksheet.Cells[1, 1].Value = $"Tổng kho xuất file ngày {DateTime.Now:dd/MM/yyyy}";
                worksheet.Cells[1, 1].Style.Font.Bold = true;
                worksheet.Cells[1, 1].Style.Font.Size = 14;
                worksheet.Cells[1, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                worksheet.Cells[1, 1].Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                worksheet.Cells[1, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                worksheet.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(68, 114, 196));
                worksheet.Cells[1, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                worksheet.Row(1).Height = 25;

                // Hàng 2: Header row với định dạng đẹp
                worksheet.Cells[2, 1].Value = "STT";
                worksheet.Cells[2, 2].Value = "Mã vật tư";
                worksheet.Cells[2, 3].Value = "Tên vật tư";
                worksheet.Cells[2, 4].Value = "Mã kho";
                worksheet.Cells[2, 5].Value = "Hãng SX";
                worksheet.Cells[2, 6].Value = "Nhà cung cấp";
                worksheet.Cells[2, 7].Value = "Dự án";
                worksheet.Cells[2, 8].Value = "Số lượng";
                worksheet.Cells[2, 9].Value = "Đơn vị";
                worksheet.Cells[2, 10].Value = "Ngày nhập kho";
                worksheet.Cells[2, 11].Value = "Ngày bảo hành";
                worksheet.Cells[2, 12].Value = "Thời gian BH";
                worksheet.Cells[2, 13].Value = "Trạng thái";
                worksheet.Cells[2, 14].Value = "Loại cấp phát";

                using (var range = worksheet.Cells[2, 1, 2, 14])
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

                int row = 3;
                int stt = 1;
                foreach (var item in items)
                {
                    worksheet.Cells[row, 1].Value = stt;
                    worksheet.Cells[row, 2].Value = item.MaSanpham ?? "";
                    worksheet.Cells[row, 3].Value = item.TenSanpham ?? "";
                    worksheet.Cells[row, 4].Value = item.Makho ?? "";
                    worksheet.Cells[row, 5].Value = item.HangSX ?? "";
                    worksheet.Cells[row, 6].Value = item.NhaCC ?? "";
                    worksheet.Cells[row, 7].Value = item.DuAn ?? "";
                    worksheet.Cells[row, 8].Value = item.SL ?? 0;
                    worksheet.Cells[row, 9].Value = item.DonVi ?? "";
                    worksheet.Cells[row, 10].Value = item.NgayNhapkho?.ToString("dd/MM/yyyy") ?? "";
                    worksheet.Cells[row, 11].Value = item.NgayBaohanh?.ToString("dd/MM/yyyy") ?? "";
                    worksheet.Cells[row, 12].Value = item.ThoiGianBH?.ToString("dd/MM/yyyy") ?? "";
                    worksheet.Cells[row, 13].Value = item.TrangThai ?? "";
                    worksheet.Cells[row, 14].Value = item.LoaiCapPhat ?? "";

                    using (var range = worksheet.Cells[row, 1, row, 14])
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
                var fileName = $"Tong_kho_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                
                return File(excelBytes, 
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
                    fileName);
            }
        }

        // In tem
        public IActionResult InTem(string makho, string tenSanpham = null, string maSanpham = null, string hangSX = null, string nhaCC = null, string ngayNhapkho = null)
        {
            // Ưu tiên sử dụng tham số từ query string nếu có, nếu không thì tìm trong khotongs
            if (!string.IsNullOrEmpty(makho))
            {
                var item = _context.khotongs.FirstOrDefault(k => k.Makho == makho);
                
                ViewBag.Makho = makho;
                ViewBag.TenSanpham = !string.IsNullOrEmpty(tenSanpham) ? tenSanpham : (item?.TenSanpham ?? "");
                ViewBag.MaSanpham = !string.IsNullOrEmpty(maSanpham) ? maSanpham : (item?.MaSanpham ?? "");
                ViewBag.HangSX = !string.IsNullOrEmpty(hangSX) ? hangSX : (item?.HangSX ?? "");
                ViewBag.NhaCC = !string.IsNullOrEmpty(nhaCC) ? nhaCC : (item?.NhaCC ?? "");
                ViewBag.NgayNhapkho = !string.IsNullOrEmpty(ngayNhapkho) ? ngayNhapkho : (item?.NgayNhapkho?.ToString("dd/MM/yyyy") ?? DateTime.Now.ToString("dd/MM/yyyy"));
            }
            else
            {
                return NotFound();
            }

            return View("InTem");
        }

    }
}
