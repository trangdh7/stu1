using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Webkho_20241021.Models;

namespace Webkho_20241021.Areas.TruongBPKetoan.Controllers
{
    [Area("TruongBPKetoan")]
    [Authorize(Roles = "Trưởng BP-BP kế toán")]
    public class ThongtincanhanController : Controller
    {
        
        private readonly ApplicationDbContext _context;
        public ThongtincanhanController(ApplicationDbContext context)
        {
            _context = context;
        }
        public IActionResult Thongtincanhan(string searchString = "", int page = 1, int pageSize = 10)
        {
            var currentUserId = HttpContext.Session.GetString("MaNguoidung");
            
            // Lọc dữ liệu theo người dùng hiện tại
            var query = _context.khonguoidungs
                .Where(k => k.NDMaNguoidung == currentUserId);
            
            // Tìm kiếm theo tên vật tư hoặc mã vật tư
            if (!string.IsNullOrEmpty(searchString))
            {
                query = query.Where(k => 
                    k.TenSanpham.Contains(searchString) || 
                    k.MaSanpham.Contains(searchString));
            }
            
            // Sắp xếp theo ngày nhập kho mới nhất
            query = query.OrderByDescending(k => k.NgayNhapkho);
            
            // Phân trang
            var totalItems = query.Count();
            var totalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            
            var KhoNguoidung = query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            
            ViewBag.SearchString = searchString;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = totalItems;
            
            return View(KhoNguoidung);
        }
        
        [HttpPost]
        public IActionResult UpdateMaterialStatus(string maSanpham, string trangThai)
        {
            try
            {
                var currentUserId = HttpContext.Session.GetString("MaNguoidung");
                var material = _context.khonguoidungs
                    .FirstOrDefault(k => k.NDMaNguoidung == currentUserId && k.MaSanpham == maSanpham);
                
                if (material != null)
                {
                    material.TrangThai = trangThai;
                    _context.SaveChanges();
                    return Json(new { success = true, message = "Cập nhật trạng thái thành công!" });
                }
                
                return Json(new { success = false, message = "Không tìm thấy vật tư!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }
        
        public IActionResult ExportPersonalMaterials()
        {
            var currentUserId = HttpContext.Session.GetString("MaNguoidung");
            var materials = _context.khonguoidungs
                .Where(k => k.NDMaNguoidung == currentUserId)
                .OrderByDescending(k => k.NgayNhapkho)
                .ToList();
            
            // Tạo file Excel hoặc CSV
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("STT,Tên vật tư,Mã vật tư,Mã kho,Hãng SX,Nhà cung cấp,Số lượng,Đơn vị,Ngày nhập kho,Ngày bảo hành,Thời gian BH,Trạng thái");
            
            int stt = 1;
            foreach (var item in materials)
            {
                csv.AppendLine($"{stt},{item.TenSanpham},{item.MaSanpham},{item.NDMakho},{item.HangSX},{item.NhaCC},{item.SL},{item.DonVi},{item.NgayNhapkho?.ToString("dd/MM/yyyy")},{item.NgayBaohanh?.ToString("dd/MM/yyyy")},{item.ThoiGianBH?.ToString("dd/MM/yyyy")},{item.TrangThai}");
                stt++;
            }
            
            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"Danh_sach_vat_tu_ca_nhan_{DateTime.Now:yyyyMMdd}.csv");
        }
    }
}
