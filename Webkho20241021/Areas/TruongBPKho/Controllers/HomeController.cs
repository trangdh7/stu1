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

        public ActionResult Tongkho()
        {
            var Tongkho = _context.khotongs.ToList();
            return View(Tongkho);
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
        public IActionResult ThemvattuSQL(string[] TenSanpham, string[] MaSanpham, string[] HangSX, string[] NhaCC, int[] SL, string[] DonVi, DateTime?[] NgayBaohanh, DateTime?[] ThoiGianBH)
        {
            int count = TenSanpham.Length;

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
                    _context.khotongs.Update(existingItem);
                }
                else
                {
                    // === Sinh mã kho mới theo format: MãSP-HãngSX-Ngày ===
                    string safeHangSX = HangSX[i]?.Replace(" ", "").Replace("/", "-") ?? "NA";
                    string Makho = $"{MaSanpham[i]}-{safeHangSX}-{DateTime.Now:yyyyMMdd}";

                    int suffix = 1;
                    while (_context.khotongs.Any(k => k.Makho == Makho))
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
                        NgayBaohanh = NgayBaohanh[i],
                        ThoiGianBH = ThoiGianBH[i],
                        Makho = Makho,
                        NgayNhapkho = DateTime.Now,
                        TrangThai = "Tồn kho"
                    };

                    _context.khotongs.Add(khotongs);
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
        public IActionResult ImportSQL(string[] TenSanpham, string[] MaSanpham, string[] HangSX, string[] NhaCC, int[] SL, string[] DonVi, DateTime?[] NgayBaohanh, DateTime?[] ThoiGianBH)
        {
            int count = TenSanpham.Length;
            int added = 0;
            int updated = 0;

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
                    _context.khotongs.Update(existingItem);
                    updated++;
                }
                else
                {
                    // === Sinh mã kho mới theo format: MãSP-HãngSX-Ngày ===
                    string safeHangSX = HangSX[i]?.Replace(" ", "").Replace("/", "-") ?? "NA";
                    string Makho = $"{MaSanpham[i]}-{safeHangSX}-{DateTime.Now:yyyyMMdd}";

                    int suffix = 1;
                    while (_context.khotongs.Any(k => k.Makho == Makho))
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
                        NgayBaohanh = NgayBaohanh[i],
                        ThoiGianBH = ThoiGianBH[i],
                        Makho = Makho,
                        NgayNhapkho = DateTime.Now,
                        TrangThai = "Tồn kho"
                    };

                    _context.khotongs.Add(newKhotong);
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

        public IActionResult VatTuMoi()
        {
            var items = _context.vtphieuxuatkho
                .Where(v => v.LoaiCapPhat == "ChoNhanVienMoi")
                .OrderByDescending(v => v.NgayNhapkho)
                .ToList();
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

    }
}
