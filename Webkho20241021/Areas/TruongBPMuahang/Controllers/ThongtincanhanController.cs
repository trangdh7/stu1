using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Webkho_20241021.Models;
using OfficeOpenXml;

namespace Webkho_20241021.Areas.TruongBPMuahang.Controllers
{
    [Area("TruongBPMuahang")]
    [Authorize(Roles = "Trưởng BP-BP mua hàng")]
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
            
            using (var package = new ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Vật tư cá nhân");

                // Hàng 1: Tiêu đề
                worksheet.Cells[1, 1, 1, 12].Merge = true;
                worksheet.Cells[1, 1].Value = $"Danh sách vật tư cá nhân xuất file ngày {DateTime.Now:dd/MM/yyyy}";
                worksheet.Cells[1, 1].Style.Font.Bold = true;
                worksheet.Cells[1, 1].Style.Font.Size = 14;
                worksheet.Cells[1, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                worksheet.Cells[1, 1].Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                worksheet.Cells[1, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                worksheet.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(68, 114, 196));
                worksheet.Cells[1, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                worksheet.Row(1).Height = 25;

                // Hàng 2: Header row
                worksheet.Cells[2, 1].Value = "STT";
                worksheet.Cells[2, 2].Value = "Tên vật tư";
                worksheet.Cells[2, 3].Value = "Mã vật tư";
                worksheet.Cells[2, 4].Value = "Mã kho";
                worksheet.Cells[2, 5].Value = "Hãng SX";
                worksheet.Cells[2, 6].Value = "Nhà cung cấp";
                worksheet.Cells[2, 7].Value = "Số lượng";
                worksheet.Cells[2, 8].Value = "Đơn vị";
                worksheet.Cells[2, 9].Value = "Ngày nhập kho";
                worksheet.Cells[2, 10].Value = "Ngày bảo hành";
                worksheet.Cells[2, 11].Value = "Thời gian BH";
                worksheet.Cells[2, 12].Value = "Trạng thái";

                // Định dạng header
                using (var range = worksheet.Cells[2, 1, 2, 12])
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

                // Điền dữ liệu
                int row = 3;
                int stt = 1;
                foreach (var item in materials)
                {
                    worksheet.Cells[row, 1].Value = stt;
                    worksheet.Cells[row, 2].Value = item.TenSanpham ?? "";
                    worksheet.Cells[row, 3].Value = item.MaSanpham ?? "";
                    worksheet.Cells[row, 4].Value = item.NDMakho ?? "";
                    worksheet.Cells[row, 5].Value = item.HangSX ?? "";
                    worksheet.Cells[row, 6].Value = item.NhaCC ?? "";
                    worksheet.Cells[row, 7].Value = item.SL ?? 0;
                    worksheet.Cells[row, 8].Value = item.DonVi ?? "";
                    worksheet.Cells[row, 9].Value = item.NgayNhapkho?.ToString("dd/MM/yyyy") ?? "";
                    worksheet.Cells[row, 10].Value = item.NgayBaohanh?.ToString("dd/MM/yyyy") ?? "";
                    worksheet.Cells[row, 11].Value = item.ThoiGianBH?.ToString("dd/MM/yyyy") ?? "";
                    worksheet.Cells[row, 12].Value = item.TrangThai ?? "";

                    // Định dạng border cho từng dòng
                    using (var range = worksheet.Cells[row, 1, row, 12])
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

                var excelBytes = package.GetAsByteArray();
                var fileName = $"Danh_sach_vat_tu_ca_nhan_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                
                return File(excelBytes, 
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
                    fileName);
            }
        }
    }
}
