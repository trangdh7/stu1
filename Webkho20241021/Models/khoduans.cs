using System.ComponentModel.DataAnnotations;

namespace Webkho_20241021.Models
{
    public class khoduans
    {

        public string? DAMaDuan { get; set; }
        public string? TenSanpham { get; set; }
        public string? MaSanpham { get; set; }
        public string? DAMakho { get; set; }
        public string? HangSX { get; set; }
        public string? NhaCC { get; set; }
        public int? SL { get; set; }
        public string? DonVi { get; set; }
        public DateTime? NgayNhapkho { get; set; }
        public DateTime? NgayBaohanh { get; set; }
        public DateTime? ThoiGianBH { get; set; }
        public string? TrangThai { get; set; }
    }
}
