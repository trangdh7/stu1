using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Webkho_20241021.Models;
using System.Linq;
using System.Collections.Generic;
using System;
using OfficeOpenXml;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using Webkho_20241021.Services;

namespace Webkho_20241021.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Trangchu()
        {
            return View();
        }

        // Quản lý kho - Tổng kho
        public ActionResult Tongkho(int page = 1, int pageSize = 20, string q = null, string hangSX = null, string nhaCC = null)
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

            // Áp dụng bộ lọc tổng kho (Hãng SX, Nhà CC)
            var filter = new KhotongFilter
            {
                HangSX = hangSX,
                NhaCC = nhaCC
            };
            query = DataFilterService.FilterKhotongs(query, filter);

            var total = query.Count();
            var items = query
                .OrderByDescending(k => k.NgayNhapkho)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
            ViewBag.Q = q;
            ViewBag.HangSX = hangSX;
            ViewBag.NhaCC = nhaCC;

            ViewBag.HangSXList = _context.khotongs
                .Select(k => k.HangSX)
                .Where(h => !string.IsNullOrWhiteSpace(h))
                .Distinct()
                .OrderBy(h => h)
                .ToList();

            ViewBag.NhaCCList = _context.khotongs
                .Select(k => k.NhaCC)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct()
                .OrderBy(n => n)
                .ToList();
            return View(items);
        }

        // Thêm vật tư - GET
        public IActionResult Themthietbi()
        {
            return View();
        }

        // Thêm vật tư - POST
        [HttpPost]
        public IActionResult ThemvattuSQL(string[] TenSanpham, string[] MaSanpham, string[] HangSX, string[] NhaCC, int[] SL, string[] DonVi, DateTime?[] NgayBaohanh, DateTime?[] ThoiGianBH, string[] DuAn)
        {
            int count = new[]
            {
                TenSanpham?.Length ?? 0,
                MaSanpham?.Length ?? 0,
                HangSX?.Length ?? 0,
                NhaCC?.Length ?? 0,
                SL?.Length ?? 0,
                DonVi?.Length ?? 0
            }.Min();

            var usedMakho = new HashSet<string>(_context.khotongs.Select(k => k.Makho));

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
                    if (DuAn != null && i < DuAn.Length && !string.IsNullOrWhiteSpace(DuAn[i]))
                    {
                        existingItem.DuAn = DuAn[i].Trim();
                    }
                    _context.khotongs.Update(existingItem);
                }
                else
                {
                    string safeHangSX = HangSX[i]?.Replace(" ", "").Replace("/", "-") ?? "NA";
                    string Makho = $"{MaSanpham[i]}-{safeHangSX}-{DateTime.Now:yyyyMMdd}";

                    int suffix = 1;
                    while (_context.khotongs.Any(k => k.Makho == Makho) ||
                           _context.khotongs.Local.Any(k => k.Makho == Makho) ||
                           usedMakho.Contains(Makho))
                    {
                        Makho = $"{MaSanpham[i]}-{safeHangSX}-{DateTime.Now:yyyyMMdd}-{suffix}";
                        suffix++;
                    }

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

            int totalAdded = _context.SaveChanges();
            TempData["Success"] = "Thêm vật tư thành công!";
            return RedirectToAction("Tongkho", "Home", new { area = "Admin" });
        }

        // Sửa vật tư - GET
        [HttpGet]
        public IActionResult SuaVatTu(string makho)
        {
            if (string.IsNullOrEmpty(makho))
            {
                return NotFound();
            }

            var item = _context.khotongs.FirstOrDefault(k => k.Makho == makho);
            if (item == null)
            {
                return NotFound();
            }

            return View(item);
        }

        // Sửa vật tư - POST
        [HttpPost]
        public IActionResult SuaVatTu(string makho, string tenSanpham, string maSanpham, string hangSX, string nhaCC, int sl, string donVi, DateTime? ngayBaohanh, DateTime? thoiGianBH, string duAn, string trangThai, string loaiCapPhat)
        {
            var item = _context.khotongs.FirstOrDefault(k => k.Makho == makho);
            if (item == null)
            {
                return NotFound();
            }

            item.TenSanpham = tenSanpham;
            item.MaSanpham = maSanpham;
            item.HangSX = hangSX;
            item.NhaCC = nhaCC;
            item.SL = sl;
            item.DonVi = donVi;
            item.NgayBaohanh = ngayBaohanh;
            item.ThoiGianBH = thoiGianBH;
            item.DuAn = duAn;
            item.TrangThai = trangThai;
            item.LoaiCapPhat = loaiCapPhat;

            _context.khotongs.Update(item);
            _context.SaveChanges();

            TempData["Success"] = "Cập nhật vật tư thành công!";
            return RedirectToAction("Tongkho", "Home", new { area = "Admin" });
        }

        // Xóa vật tư
        [HttpPost]
        [Microsoft.AspNetCore.Mvc.IgnoreAntiforgeryToken]
        public IActionResult XoaVatTu([FromBody] JsonElement data)
        {
            try
            {
                string makho = null;
                if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("makho", out JsonElement makhoElement))
                {
                    makho = makhoElement.GetString();
                }

                if (string.IsNullOrEmpty(makho))
                {
                    return Json(new { success = false, message = "Thiếu mã kho" });
                }

                var item = _context.khotongs.FirstOrDefault(k => k.Makho == makho);
                if (item == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy vật tư" });
                }

                _context.khotongs.Remove(item);
                _context.SaveChanges();

                return Json(new { success = true, message = "Xóa vật tư thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi khi xóa vật tư: {ex.Message}" });
            }
        }

        // Import Excel
        public IActionResult Import()
        {
            return View();
        }

        [HttpPost]
        public IActionResult ImportSQL(string[] TenSanpham, string[] MaSanpham, string[] HangSX, string[] NhaCC, int[] SL, string[] DonVi, DateTime?[] NgayBaohanh, DateTime?[] ThoiGianBH, string[] DuAn)
        {
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

            var usedMakho = new HashSet<string>(_context.khotongs.Select(k => k.Makho));

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
                    if (DuAn != null && i < DuAn.Length && !string.IsNullOrWhiteSpace(DuAn[i]))
                    {
                        existingItem.DuAn = DuAn[i].Trim();
                    }
                    _context.khotongs.Update(existingItem);
                    updated++;
                }
                else
                {
                    string safeHangSX = HangSX[i]?.Replace(" ", "").Replace("/", "-") ?? "NA";
                    string Makho = $"{MaSanpham[i]}-{safeHangSX}-{DateTime.Now:yyyyMMdd}";

                    int suffix = 1;
                    while (_context.khotongs.Any(k => k.Makho == Makho) ||
                           _context.khotongs.Local.Any(k => k.Makho == Makho) ||
                           usedMakho.Contains(Makho))
                    {
                        Makho = $"{MaSanpham[i]}-{safeHangSX}-{DateTime.Now:yyyyMMdd}-{suffix}";
                        suffix++;
                    }

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
            return RedirectToAction("Tongkho", "Home", new { area = "Admin" });
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

        // Export Excel
        public IActionResult ExportTongkho(string q = null, string hangSX = null, string nhaCC = null)
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

            // Áp dụng bộ lọc giống màn hình Tổng kho
            var filter = new KhotongFilter
            {
                HangSX = hangSX,
                NhaCC = nhaCC
            };
            query = DataFilterService.FilterKhotongs(query, filter);

            var items = query
                .OrderByDescending(k => k.NgayNhapkho)
                .ToList();

            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Tổng kho");

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

        // API quản lý nhà cung cấp cho sản phẩm
        [HttpGet]
        public IActionResult GetNhaCCByMaSanpham(string maSanpham)
        {
            if (string.IsNullOrEmpty(maSanpham))
            {
                return Json(new List<object>());
            }

            var suppliers = _context.SanPhamNhaCC
                .Where(s => s.MaSanpham == maSanpham)
                .Select(s => new
                {
                    id = s.ID,
                    nhaCC = s.NhaCC,
                    donGiaMacDinh = s.DonGiaMacDinh,
                    ngayTao = s.NgayTao,
                    ghiChu = s.GhiChu
                })
                .ToList();

            return Json(suppliers);
        }

        [HttpPost]
        public IActionResult ThemNhaCCChoSanpham(string maSanpham, string nhaCC, decimal? donGiaMacDinh = null, string ghiChu = null)
        {
            if (string.IsNullOrEmpty(maSanpham) || string.IsNullOrEmpty(nhaCC))
            {
                return Json(new { success = false, message = "Mã sản phẩm và nhà cung cấp không được để trống!" });
            }

            // Kiểm tra xem đã tồn tại chưa
            var existing = _context.SanPhamNhaCC
                .FirstOrDefault(s => s.MaSanpham == maSanpham && s.NhaCC == nhaCC);

            if (existing != null)
            {
                return Json(new { success = false, message = "Nhà cung cấp này đã tồn tại cho sản phẩm này!" });
            }

            var newSupplier = new SanPhamNhaCC
            {
                MaSanpham = maSanpham,
                NhaCC = nhaCC,
                DonGiaMacDinh = donGiaMacDinh,
                GhiChu = ghiChu,
                NgayTao = DateTime.Now
            };

            _context.SanPhamNhaCC.Add(newSupplier);
            _context.SaveChanges();

            return Json(new { success = true, message = "Thêm nhà cung cấp thành công!" });
        }

        [HttpPost]
        public IActionResult XoaNhaCCKhoiSanpham(int id)
        {
            var supplier = _context.SanPhamNhaCC.FirstOrDefault(s => s.ID == id);
            if (supplier == null)
            {
                return Json(new { success = false, message = "Không tìm thấy nhà cung cấp!" });
            }

            _context.SanPhamNhaCC.Remove(supplier);
            _context.SaveChanges();

            return Json(new { success = true, message = "Xóa nhà cung cấp thành công!" });
        }

        // Đồng bộ nhà cung cấp từ khotongs sang SanPhamNhaCC (một lần)
        [HttpPost]
        public IActionResult DongBoNhaCC()
        {
            try
            {
                var products = _context.khotongs
                    .Where(k => !string.IsNullOrEmpty(k.MaSanpham) && !string.IsNullOrEmpty(k.NhaCC))
                    .Select(k => new { k.MaSanpham, k.NhaCC })
                    .Distinct()
                    .ToList();

                int count = 0;
                foreach (var product in products)
                {
                    var existing = _context.SanPhamNhaCC
                        .FirstOrDefault(s => s.MaSanpham == product.MaSanpham && s.NhaCC == product.NhaCC);

                    if (existing == null)
                    {
                        var newSupplier = new SanPhamNhaCC
                        {
                            MaSanpham = product.MaSanpham,
                            NhaCC = product.NhaCC,
                            NgayTao = DateTime.Now
                        };
                        _context.SanPhamNhaCC.Add(newSupplier);
                        count++;
                    }
                }

                _context.SaveChanges();
                return Json(new { success = true, message = $"Đã đồng bộ {count} nhà cung cấp từ kho tổng!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // Quản lý bản ghi lỗi/null - Hiển thị danh sách
        public IActionResult QuanLyBanGhiLoi()
        {
            // Tìm các yeucau không có vtyeucau
            var yeucauRong = _context.yeucau
                .Where(y => !_context.vtyeucau.Any(vt => vt.VTMaYeucau == y.MaYeucau))
                .ToList();

            // Tìm các phieunhapkho không có vtphieunhapkho
            var phieunhapkhoRong = _context.phieunhapkho
                .Where(p => !_context.vtphieunhapkho.Any(vt => vt.MaNhapkho == p.MaNhapkho))
                .ToList();

            // Tìm các phieuxuatkho không có vtphieuxuatkho
            var phieuxuatkhoRong = _context.phieuxuatkho
                .Where(p => !_context.vtphieuxuatkho.Any(vt => vt.MaXuatkho == p.MaXuatkho))
                .ToList();

            // Tìm các phieumuahang không có vtphieumuahang
            var phieumuahangRong = _context.phieumuahang
                .Where(p => !_context.vtphieumuahang.Any(vt => vt.MaMuahang == p.MaMuahang))
                .ToList();

            ViewBag.YeucauRong = yeucauRong;
            ViewBag.PhieunhapkhoRong = phieunhapkhoRong;
            ViewBag.PhieuxuatkhoRong = phieuxuatkhoRong;
            ViewBag.PhieumuahangRong = phieumuahangRong;

            return View();
        }

        // Xóa yeucau rỗng
        [HttpPost]
        public IActionResult XoaYeucauRong(string maYeucau)
        {
            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    if (string.IsNullOrEmpty(maYeucau))
                    {
                        return Json(new { success = false, message = "Mã yêu cầu không được để trống!" });
                    }

                    var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == maYeucau);
                    if (yeucau == null)
                    {
                        return Json(new { success = false, message = "Không tìm thấy yêu cầu!" });
                    }

                    // Kiểm tra xem có vtyeucau không
                    var hasVtyeucau = _context.vtyeucau.Any(vt => vt.VTMaYeucau == maYeucau);
                    if (hasVtyeucau)
                    {
                        return Json(new { success = false, message = "Yêu cầu này có dữ liệu vật tư, không thể xóa!" });
                    }

                    _context.yeucau.Remove(yeucau);
                    _context.SaveChanges();
                    transaction.Commit();

                    return Json(new { success = true, message = "Xóa yêu cầu thành công!" });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Json(new { success = false, message = $"Lỗi khi xóa: {ex.Message}" });
                }
            }
        }

        // Xóa phieunhapkho rỗng
        [HttpPost]
        public IActionResult XoaPhieunhapkhoRong(string maNhapkho)
        {
            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    if (string.IsNullOrEmpty(maNhapkho))
                    {
                        return Json(new { success = false, message = "Mã phiếu nhập kho không được để trống!" });
                    }

                    var phieu = _context.phieunhapkho.FirstOrDefault(p => p.MaNhapkho == maNhapkho);
                    if (phieu == null)
                    {
                        return Json(new { success = false, message = "Không tìm thấy phiếu nhập kho!" });
                    }

                    // Kiểm tra xem có vtphieunhapkho không
                    var hasVt = _context.vtphieunhapkho.Any(vt => vt.MaNhapkho == maNhapkho);
                    if (hasVt)
                    {
                        return Json(new { success = false, message = "Phiếu nhập kho này có dữ liệu vật tư, không thể xóa!" });
                    }

                    _context.phieunhapkho.Remove(phieu);
                    _context.SaveChanges();
                    transaction.Commit();

                    return Json(new { success = true, message = "Xóa phiếu nhập kho thành công!" });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Json(new { success = false, message = $"Lỗi khi xóa: {ex.Message}" });
                }
            }
        }

        // Xóa phieuxuatkho rỗng
        [HttpPost]
        public IActionResult XoaPhieuxuatkhoRong(string maXuatkho)
        {
            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    if (string.IsNullOrEmpty(maXuatkho))
                    {
                        return Json(new { success = false, message = "Mã phiếu xuất kho không được để trống!" });
                    }

                    var phieu = _context.phieuxuatkho.FirstOrDefault(p => p.MaXuatkho == maXuatkho);
                    if (phieu == null)
                    {
                        return Json(new { success = false, message = "Không tìm thấy phiếu xuất kho!" });
                    }

                    // Kiểm tra xem có vtphieuxuatkho không
                    var hasVt = _context.vtphieuxuatkho.Any(vt => vt.MaXuatkho == maXuatkho);
                    if (hasVt)
                    {
                        return Json(new { success = false, message = "Phiếu xuất kho này có dữ liệu vật tư, không thể xóa!" });
                    }

                    _context.phieuxuatkho.Remove(phieu);
                    _context.SaveChanges();
                    transaction.Commit();

                    return Json(new { success = true, message = "Xóa phiếu xuất kho thành công!" });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Json(new { success = false, message = $"Lỗi khi xóa: {ex.Message}" });
                }
            }
        }

        // Xóa phieumuahang rỗng
        [HttpPost]
        public IActionResult XoaPhieumuahangRong(string maMuahang)
        {
            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    if (string.IsNullOrEmpty(maMuahang))
                    {
                        return Json(new { success = false, message = "Mã phiếu mua hàng không được để trống!" });
                    }

                    var phieu = _context.phieumuahang.FirstOrDefault(p => p.MaMuahang == maMuahang);
                    if (phieu == null)
                    {
                        return Json(new { success = false, message = "Không tìm thấy phiếu mua hàng!" });
                    }

                    // Kiểm tra xem có vtphieumuahang không
                    var hasVt = _context.vtphieumuahang.Any(vt => vt.MaMuahang == maMuahang);
                    if (hasVt)
                    {
                        return Json(new { success = false, message = "Phiếu mua hàng này có dữ liệu vật tư, không thể xóa!" });
                    }

                    _context.phieumuahang.Remove(phieu);
                    _context.SaveChanges();
                    transaction.Commit();

                    return Json(new { success = true, message = "Xóa phiếu mua hàng thành công!" });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Json(new { success = false, message = $"Lỗi khi xóa: {ex.Message}" });
                }
            }
        }

        // Xóa tất cả bản ghi rỗng
        [HttpPost]
        public IActionResult XoaTatCaBanGhiRong()
        {
            using (var transaction = _context.Database.BeginTransaction())
            {
                try
                {
                    int count = 0;

                    // Xóa yeucau rỗng
                    var yeucauRong = _context.yeucau
                        .Where(y => !_context.vtyeucau.Any(vt => vt.VTMaYeucau == y.MaYeucau))
                        .ToList();
                    count += yeucauRong.Count;
                    _context.yeucau.RemoveRange(yeucauRong);

                    // Xóa phieunhapkho rỗng
                    var phieunhapkhoRong = _context.phieunhapkho
                        .Where(p => !_context.vtphieunhapkho.Any(vt => vt.MaNhapkho == p.MaNhapkho))
                        .ToList();
                    count += phieunhapkhoRong.Count;
                    _context.phieunhapkho.RemoveRange(phieunhapkhoRong);

                    // Xóa phieuxuatkho rỗng
                    var phieuxuatkhoRong = _context.phieuxuatkho
                        .Where(p => !_context.vtphieuxuatkho.Any(vt => vt.MaXuatkho == p.MaXuatkho))
                        .ToList();
                    count += phieuxuatkhoRong.Count;
                    _context.phieuxuatkho.RemoveRange(phieuxuatkhoRong);

                    // Xóa phieumuahang rỗng
                    var phieumuahangRong = _context.phieumuahang
                        .Where(p => !_context.vtphieumuahang.Any(vt => vt.MaMuahang == p.MaMuahang))
                        .ToList();
                    count += phieumuahangRong.Count;
                    _context.phieumuahang.RemoveRange(phieumuahangRong);

                    _context.SaveChanges();
                    transaction.Commit();

                    return Json(new { success = true, message = $"Đã xóa {count} bản ghi rỗng thành công!" });
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    return Json(new { success = false, message = $"Lỗi khi xóa: {ex.Message}" });
                }
            }
        }
    }
}

