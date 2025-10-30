using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Webkho_20241021.Models;
using System.Linq;
using System.Collections.Generic;
using System;

namespace Webkho_20241021.Areas.Giamdoc.Controllers
{
    [Area("Giamdoc")]
    [Authorize(Roles = "Giám đốc")] 
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

        [HttpPost]
        public IActionResult ThemvattuSQL(string[] TenSanpham, string[] MaSanpham, string[] HangSX, string[] NhaCC, int[] SL, string[] DonVi, DateTime?[] NgayBaohanh, DateTime?[] ThoiGianBH)
        {
            int count = TenSanpham.Length;
            var MakhoPrefix = "STU";

            for (int i = 0; i < count; i++)
            {
                if (string.IsNullOrWhiteSpace(TenSanpham[i]) || string.IsNullOrWhiteSpace(MaSanpham[i]) ||
                    SL[i] <= 0 || string.IsNullOrWhiteSpace(DonVi[i]))
                {
                    continue;
                }

                // Kiểm tra nếu tồn tại thiết bị với các thuộc tính giống nhau
                var existingItem = _context.khotongs
                    .FirstOrDefault(k => k.TenSanpham == TenSanpham[i] && k.MaSanpham == MaSanpham[i] && k.HangSX == HangSX[i]);

                if (existingItem != null)
                {
                    // Nếu tồn tại, cộng số lượng
                    existingItem.SL += SL[i];
                    _context.khotongs.Update(existingItem);
                }
                else
                {
                    string Makho;
                    int index = _context.khotongs.Count() + i + 1;

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

                    _context.khotongs.Add(khotongs);
                }
            }

            _context.SaveChanges();

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
                }
                else
                {
                    // Nếu chưa tồn tại, tạo mã kho mới
                    string Makho;
                    int index = _context.khotongs.Count() + i + 1;

                    do
                    {
                        Makho = $"{MakhoPrefix}{index}";
                        index++;
                    }
                    while (_context.khotongs.Any(k => k.Makho == Makho));

                    // Tạo mục mới
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
                }
            }

            // Lưu thay đổi vào database
            _context.SaveChanges();

            return RedirectToAction("Tongkho", "Home", new { area = "Giamdoc" });
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

        public IActionResult KhoDuAn(int page = 1)
        {
            int pageSize = 20;
            var items = _context.khotongs.Where(k => !string.IsNullOrEmpty(k.DuAn)).ToList();
            ViewBag.Page = page;
            return View("KhoDuAn", items);
        }

    }
}
