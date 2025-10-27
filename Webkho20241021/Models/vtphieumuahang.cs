using System.ComponentModel.DataAnnotations;

namespace Webkho_20241021.Models
{
    public class vtphieumuahang
    {
        [Key]
        public int ID { get; set; }
        public string? MaMuahang { get; set; }
        public string? MaYeucau { get; set; }
        public string? TenSanpham { get; set; }
        public string? MaSanpham { get; set; }
        public string? Makho { get; set; }
        public string? HangSX { get; set; }
        public string? NhaCC { get; set; }
        public int? SL { get; set; }
        public string? DonVi { get; set; }
        public decimal? DonGia { get; set; }
        public decimal? ThanhTien { get; set; }

        public DateTime? NgayNhapkho { get; set; }
        public DateTime? NgayBaohanh { get; set; }
        public DateTime? ThoiGianBH { get; set; }
        public string? TrangThai { get; set; }
        public string? GhiChu { get; set; }
    }
}
