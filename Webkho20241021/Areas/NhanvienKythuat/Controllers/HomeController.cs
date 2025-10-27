using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Webkho_20241021.Models;
using System.Linq;
using System.Collections.Generic;
using System;

namespace Webkho_20241021.Areas.NhanvienKythuat.Controllers
{
    [Area("NhanvienKythuat")]
    [Authorize(Roles = "Nhân viên-BP kỹ thuật")] 
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

    }
}
