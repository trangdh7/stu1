using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Webkho_20241021.Models
{
    public class vtphieunhapkho
    {
        [Key]
        public int ID { get; set; }
        public string? MaNhapkho { get; set; }
        public string? MaYeucau { get; set; }
        public string? TenSanpham { get; set; }
        public string? MaSanpham { get; set; }
        public string? Makho { get; set; }
        public string? HangSX { get; set; }
        public string? NhaCC { get; set; }
        public int? SL { get; set; }
        public string? DonVi { get; set; }
        [Column(TypeName = "decimal(20,6)")]
        public decimal? DonGia { get; set; }
        [Column(TypeName = "decimal(20,6)")]
        public decimal? ThanhTien { get; set; }
        public DateTime? NgayNhapkho { get; set; }
        public DateTime? NgayBaohanh { get; set; }
        public DateTime? ThoiGianBH { get; set; }
        public string? TrangThai { get; set; }
    }
}
