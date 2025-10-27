using System.ComponentModel.DataAnnotations;

namespace Webkho_20241021.Models
{
    public class phieunhapkho
    {
        public string? MaNhapkho { get; set; }
        public string MaYeucau { get; set; }
        public string? MaDuan { get; set; }
        public string? MaNguoidung { get; set; }
        public DateTime? NgayNhapkho { get; set; }
        public string? TrangThai { get; set; }
    }
}
