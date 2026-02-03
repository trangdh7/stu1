using System.ComponentModel.DataAnnotations;

namespace Webkho_20241021.Models
{
    public class vtyeucau
    {
        [Key]
        public int ID { get; set; }
        // Số thứ tự theo file Excel (ví dụ: 1, 1.1, 1.2...)
        public string? TT { get; set; }
        public string? VTMaYeucau { get; set; }
        public string? TenSanpham { get; set; }
        public string? MaSanpham { get; set; }
        public string? YCMakho { get; set; }
        public string? HangSX { get; set; }
        public string? NhaCC { get; set; }
        public int? SLCu { get; set; }
        public int? SLMoi { get; set; }
        public int? SL { get; set; }
        public string? DonVi { get; set; }
        public DateTime? NgayCanHang { get; set; }
        public DateTime? NgayNhapkho { get; set; }
        public DateTime? NgayBaohanh { get; set; }
        public DateTime? ThoiGianBH { get; set; }
        public DateTime? NgayDuyet { get; set; }
        public string? TrangThai { get; set; }
        public string? GhiChu { get; set; }
    }
}
