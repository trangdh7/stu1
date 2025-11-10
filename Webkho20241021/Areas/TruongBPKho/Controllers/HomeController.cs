using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Webkho_20241021.Models;
using System.Linq;
using System.Collections.Generic;
using System;
using OfficeOpenXml;
using Microsoft.AspNetCore.Http;

namespace Webkho_20241021.Areas.TruongBPKho.Controllers
{
    [Area("TruongBPKho")]
    [Authorize(Roles = "Trưởng BP-BP kho")]
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
        //import excel




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
            var usedMakho = new HashSet<string>(_context.khotongs.Select(k => k.Makho));

            for (int i = 0; i < count; i++)
            {
                if (string.IsNullOrWhiteSpace(TenSanpham[i]) || string.IsNullOrWhiteSpace(MaSanpham[i]) ||
                    SL[i] <= 0 || string.IsNullOrWhiteSpace(DonVi[i]))
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
                    // === Sinh mã kho mới theo format: MãSP-HãngSX-Ngày ===
                    string safeHangSX = HangSX[i]?.Replace(" ", "").Replace("/", "-") ?? "NA";
                    string Makho = $"{MaSanpham[i]}-{safeHangSX}-{DateTime.Now:yyyyMMdd}";

                    int suffix = 1;
                    // Đảm bảo duy nhất cả ở DB lẫn các entity đang Add trong DbContext hiện tại
                    while (_context.khotongs.Any(k => k.Makho == Makho) ||
                           _context.khotongs.Local.Any(k => k.Makho == Makho) ||
                           usedMakho.Contains(Makho))
                    {
                        Makho = $"{MaSanpham[i]}-{safeHangSX}-{DateTime.Now:yyyyMMdd}-{suffix}";
                        suffix++;
                    }

                    // Giới hạn độ dài tối đa 50 ký tự
                    if (Makho.Length > 50)
                    {
                        Makho = Makho.Substring(0, 50);
                    }

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
                        Makho = Makho,
                        NgayNhapkho = DateTime.Now,
                        TrangThai = "Tồn kho"
                    };

                    _context.khotongs.Add(khotongs);
                    usedMakho.Add(Makho);
                }
            }

            _context.SaveChanges();
            return RedirectToAction("Tongkho", "Home", new { area = "TruongBPKho" });
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
            var usedMakho = new HashSet<string>(_context.khotongs.Select(k => k.Makho));

            for (int i = 0; i < count; i++)
            {
                if (string.IsNullOrWhiteSpace(TenSanpham[i]) || string.IsNullOrWhiteSpace(MaSanpham[i]) ||
                    SL[i] <= 0 || string.IsNullOrWhiteSpace(DonVi[i]))
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
                    // === Sinh mã kho mới theo format: MãSP-HãngSX-Ngày ===
                    string safeHangSX = HangSX[i]?.Replace(" ", "").Replace("/", "-") ?? "NA";
                    string Makho = $"{MaSanpham[i]}-{safeHangSX}-{DateTime.Now:yyyyMMdd}";

                    int suffix = 1;
                    // Đảm bảo duy nhất cả ở DB lẫn các entity đang Add trong DbContext hiện tại
                    while (_context.khotongs.Any(k => k.Makho == Makho) ||
                           _context.khotongs.Local.Any(k => k.Makho == Makho) ||
                           usedMakho.Contains(Makho))
                    {
                        Makho = $"{MaSanpham[i]}-{safeHangSX}-{DateTime.Now:yyyyMMdd}-{suffix}";
                        suffix++;
                    }

                    // Giới hạn độ dài tối đa 50 ký tự
                    if (Makho.Length > 50)
                    {
                        Makho = Makho.Substring(0, 50);
                    }

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
                        Makho = Makho,
                        NgayNhapkho = DateTime.Now,
                        TrangThai = "Tồn kho"
                    };

                    _context.khotongs.Add(newKhotong);
                    usedMakho.Add(Makho);
                    added++;
                }
            }

            _context.SaveChanges();
            TempData["Success"] = $"Import thành công: thêm {added} dòng, cập nhật {updated} dòng.";
            return RedirectToAction("Tongkho", "Home", new { area = "TruongBPKho" });
        }

        // In tem
        public IActionResult InTem(string makho)
        {
            var item = _context.khotongs.FirstOrDefault(k => k.Makho == makho);
            if (item == null)
            {
                return NotFound();
            }

            ViewBag.Makho = item.Makho;
            ViewBag.TenSanpham = item.TenSanpham;
            ViewBag.MaSanpham = item.MaSanpham;
            ViewBag.HangSX = item.HangSX;
            ViewBag.NgayNhapkho = item.NgayNhapkho?.ToString("dd/MM/yyyy");

            return View("InTem");
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
        [HttpGet]
        public IActionResult KhoDuAn(int page = 1, int pageSize = 20, string q = null, string duAn = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            // Chỉ lấy những vật tư có gán mã/ten dự án
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

        // Export Excel tổng kho
        public IActionResult ExportTongkho(string q = null)
        {
            
            
            var query = _context.khotongs.AsQueryable();
              
            // Áp dụng tìm kiếm nếu có
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

            var items = query
                .OrderByDescending(k => k.NgayNhapkho)
                .ToList();

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
                worksheet.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(68, 114, 196)); // Xanh đậm
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

                // Định dạng header
                using (var range = worksheet.Cells[2, 1, 2, 14])
                {
                    range.Style.Font.Bold = true;
                    range.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    range.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(173, 216, 230)); // LightBlue
                    range.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    range.Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    range.Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    range.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    range.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                }

                // Điền dữ liệu
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

                    // Định dạng border cho từng dòng
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

                // Tự động điều chỉnh độ rộng cột
                worksheet.Cells.AutoFitColumns();

                // Đảm bảo encoding UTF-8 cho tiếng Việt
                var excelBytes = package.GetAsByteArray();
                var fileName = $"Tong_kho_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                
                return File(excelBytes, 
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
                    fileName);
            }
        }

    }
}
