using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Webkho_20241021.Models;

namespace Webkho_20241021.Controllers
{
    public class DatabaseController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DatabaseController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult AddColumns()
        {
            try
            {
                // Thêm cột NgayTao vào bảng phieumuahang
                _context.Database.ExecuteSqlRaw("ALTER TABLE phieumuahang ADD COLUMN NgayTao DATETIME NULL");
                
                // Thêm cột GhiChu vào bảng phieumuahang
                _context.Database.ExecuteSqlRaw("ALTER TABLE phieumuahang ADD COLUMN GhiChu TEXT NULL");
                
                // Thêm cột GhiChu vào bảng vtphieumuahang
                _context.Database.ExecuteSqlRaw("ALTER TABLE vtphieumuahang ADD COLUMN GhiChu TEXT NULL");
                
                return Content("Đã thêm cột thành công!");
            }
            catch (Exception ex)
            {
                return Content($"Lỗi: {ex.Message}");
            }
        }
    }
}