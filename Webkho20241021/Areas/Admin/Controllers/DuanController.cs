using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Webkho_20241021.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Webkho_20241021.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DuanController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DuanController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Danh sách dự án
        public IActionResult DanhSachDuan(int page = 1, int pageSize = 20, string q = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var query = _context.duans.AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var keyword = q.Trim();
                query = query.Where(d =>
                    (d.TenDuan ?? "").Contains(keyword) ||
                    (d.MaDuan ?? "").Contains(keyword) ||
                    (d.NguoiQLDA ?? "").Contains(keyword) ||
                    (d.MaNguoiQLDA ?? "").Contains(keyword) ||
                    (d.KhachHang ?? "").Contains(keyword) ||
                    (d.TrangThai ?? "").Contains(keyword)
                );
            }

            var total = query.Count();
            var duans = query
                .OrderByDescending(d => d.NgayBatdau ?? DateTime.MinValue)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
            ViewBag.Q = q;

            return View(duans);
        }

        // Tạo dự án - GET
        public IActionResult TaoDuan()
        {
            var Tennguoidunglist = _context.nguoidungs
                .Select(n => new { n.TenNguoidung, n.MaNguoidung })
                .ToList();

            ViewBag.Tennguoidunglist = Tennguoidunglist;
            return View();
        }

        // Tạo dự án - POST
        [HttpPost]
        public async Task<IActionResult> TaoDuan(string TenDuan, string MaDuan, string NguoiQLDA, string MaNguoiQLDA, string KhachHang, DateTime? NgayBatdau, DateTime? NgayKetthuc, string TrangThai)
        {
            // Kiểm tra mã dự án đã tồn tại chưa
            if (await _context.duans.AnyAsync(d => d.MaDuan == MaDuan))
            {
                ModelState.AddModelError("", "Mã dự án đã tồn tại");
                var Tennguoidunglist = _context.nguoidungs
                    .Select(n => new { n.TenNguoidung, n.MaNguoidung })
                    .ToList();
                ViewBag.Tennguoidunglist = Tennguoidunglist;
                return View();
            }

            var duan = new duans
            {
                TenDuan = TenDuan,
                MaDuan = MaDuan,
                NguoiQLDA = NguoiQLDA,
                MaNguoiQLDA = MaNguoiQLDA,
                KhachHang = KhachHang,
                NgayBatdau = NgayBatdau,
                NgayKetthuc = NgayKetthuc,
                TrangThai = string.IsNullOrWhiteSpace(TrangThai) ? "Chờ" : TrangThai
            };

            _context.duans.Add(duan);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Tạo dự án thành công!";
            return RedirectToAction("DanhSachDuan", "Duan", new { area = "Admin" });
        }

        // Sửa dự án - GET
        [HttpGet]
        public async Task<IActionResult> SuaDuan(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var duan = await _context.duans.FirstOrDefaultAsync(d => d.MaDuan == id);
            if (duan == null)
            {
                return NotFound();
            }

            var Tennguoidunglist = _context.nguoidungs
                .Select(n => new { n.TenNguoidung, n.MaNguoidung })
                .ToList();

            ViewBag.Tennguoidunglist = Tennguoidunglist;
            return View(duan);
        }

        // Sửa dự án - POST
        [HttpPost]
        public async Task<IActionResult> SuaDuan(string id, string TenDuan, string NguoiQLDA, string MaNguoiQLDA, string KhachHang, DateTime? NgayBatdau, DateTime? NgayKetthuc, string TrangThai)
        {
            var duan = await _context.duans.FirstOrDefaultAsync(d => d.MaDuan == id);
            if (duan == null)
            {
                return NotFound();
            }

            duan.TenDuan = TenDuan;
            duan.NguoiQLDA = NguoiQLDA;
            duan.MaNguoiQLDA = MaNguoiQLDA;
            duan.KhachHang = KhachHang;
            duan.NgayBatdau = NgayBatdau;
            duan.NgayKetthuc = NgayKetthuc;
            duan.TrangThai = TrangThai;

            _context.duans.Update(duan);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Cập nhật thông tin dự án thành công!";
            return RedirectToAction("DanhSachDuan", "Duan", new { area = "Admin" });
        }

        // Xóa dự án
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> XoaDuan([FromBody] JsonElement data)
        {
            try
            {
                string id = null;
                if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("id", out JsonElement idElement))
                {
                    id = idElement.GetString();
                }

                if (string.IsNullOrEmpty(id))
                {
                    return Json(new { success = false, message = "Thiếu mã dự án" });
                }

                var duan = await _context.duans.FirstOrDefaultAsync(d => d.MaDuan == id);
                if (duan == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy dự án" });
                }

                // Kiểm tra xem dự án có đang được sử dụng không (có thể kiểm tra trong bảng khoduans, phieuxuatkho, etc.)
                var hasKhoDuan = await _context.khoduans.AnyAsync(kd => kd.DAMaDuan == id);
                if (hasKhoDuan)
                {
                    return Json(new { success = false, message = "Không thể xóa dự án vì đang có kho dự án liên quan" });
                }

                _context.duans.Remove(duan);
                await _context.SaveChangesAsync();

                return Json(new { success = true, message = "Xóa dự án thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }
    }
}

