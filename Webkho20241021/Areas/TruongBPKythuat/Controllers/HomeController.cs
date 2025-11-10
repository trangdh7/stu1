using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Webkho_20241021.Models;
using System.Linq;
using System.Collections.Generic;
using System;
using OfficeOpenXml;

namespace Webkho_20241021.Areas.TruongBPKythuat.Controllers
{
    [Area("TruongBPKythuat")]
    [Authorize(Roles = "Trưởng BP-BP kỹ thuật")]
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

        [HttpPost]
        public IActionResult ThemvattuSQL(string[] TenSanpham, string[] MaSanpham, string[] HangSX, string[] NhaCC, int[] SL, string[] DonVi, DateTime?[] NgayBaohanh, DateTime?[] ThoiGianBH)
        {
            int count = TenSanpham.Length;
            var MakhoPrefix = "STU";

            // Tạo một danh sách để lưu các mục hợp lệ
            List<khotongs> validKhotongs = new List<khotongs>();

            for (int i = 0; i < count; i++)
            {
                if (string.IsNullOrWhiteSpace(TenSanpham[i]) || string.IsNullOrWhiteSpace(MaSanpham[i]) ||
                    SL[i] <= 0 || string.IsNullOrWhiteSpace(DonVi[i]))
                {
                    continue;
                }

                string Makho;
                int index = _context.khotongs.Count() + i + 1; // Khởi tạo index bắt đầu từ số lượng hiện tại + i

                do
                {
                    Makho = $"{MakhoPrefix}{index}";
                    index++;
                }
                while (_context.khotongs.Any(k => k.Makho == Makho));

                var khotongs = new khotongs
                {
                    TenSanpham = TenSanpham[i],
                    MaSanpham = MaSanpham[i],
                    HangSX = HangSX[i],
                    NhaCC = NhaCC[i],
                    SL = SL[i],
                    DonVi = DonVi[i],
                    NgayBaohanh = NgayBaohanh[i],
                    ThoiGianBH = ThoiGianBH[i],
                    Makho = Makho,
                    NgayNhapkho = DateTime.Now,
                    TrangThai = "Tồn kho"
                };

                validKhotongs.Add(khotongs);
            }

            if (validKhotongs.Count > 0)
            {
                _context.khotongs.AddRange(validKhotongs);
                _context.SaveChanges();
            }

            return RedirectToAction("Tongkho", "Home", new { area = "Giamdoc" });
        }

        [HttpGet]
        public IActionResult TimKiem(string timkiem)
        {
            var results = _context.khotongs
                .Where(k => k.TenSanpham.Contains(timkiem) || k.MaSanpham.Contains(timkiem))
                .ToList();
            return Json(results);
        }

        [HttpPost]
        public IActionResult ImportSQL(string[] TenSanpham, string[] MaSanpham, string[] HangSX, string[] NhaCC, int[] SL, string[] DonVi, DateTime?[] NgayBaohanh, DateTime?[] ThoiGianBH)
        {
            int count = TenSanpham.Length;
            var MakhoPrefix = "STU";

            // Tạo một danh sách để lưu các mục hợp lệ
            List<khotongs> validKhotongs = new List<khotongs>();

            for (int i = 0; i < count; i++)
            {
                if (string.IsNullOrWhiteSpace(TenSanpham[i]) || string.IsNullOrWhiteSpace(MaSanpham[i]) ||
                    SL[i] <= 0 || string.IsNullOrWhiteSpace(DonVi[i]))
                {
                    continue;
                }

                string Makho;
                int index = _context.khotongs.Count() + i + 1; // Khởi tạo index bắt đầu từ số lượng hiện tại + i

                do
                {
                    Makho = $"{MakhoPrefix}{index}";
                    index++;
                }
                while (_context.khotongs.Any(k => k.Makho == Makho));

                var khotongs = new khotongs
                {
                    TenSanpham = TenSanpham[i],
                    MaSanpham = MaSanpham[i],
                    HangSX = HangSX[i],
                    NhaCC = NhaCC[i],
                    SL = SL[i],
                    DonVi = DonVi[i],
                    NgayBaohanh = NgayBaohanh[i],
                    ThoiGianBH = ThoiGianBH[i],
                    Makho = Makho,
                    NgayNhapkho = DateTime.Now,
                    TrangThai = "Tồn kho"
                };

                validKhotongs.Add(khotongs);
            }

            if (validKhotongs.Count > 0)
            {
                _context.khotongs.AddRange(validKhotongs);
                _context.SaveChanges();
            }

            return RedirectToAction("Tongkho", "Home", new { area = "Giamdoc" });
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

        public IActionResult KhoDuAn(int page = 1, int pageSize = 20, string q = null, string duAn = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var query = _context.khotongs.Where(k => !string.IsNullOrEmpty(k.DuAn));
            var duAnList = _context.khotongs
                .Where(k => !string.IsNullOrEmpty(k.DuAn))
                .Select(k => k.DuAn)
                .Distinct()
                .OrderBy(x => x)
                .ToList();

            if (!string.IsNullOrWhiteSpace(duAn))
            {
                query = query.Where(k => k.DuAn == duAn);
            }
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
            ViewBag.DuAn = duAn;
            ViewBag.DuAnList = duAnList;
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

    }
}
