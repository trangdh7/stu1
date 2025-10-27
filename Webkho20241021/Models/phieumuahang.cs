using System.ComponentModel.DataAnnotations;

namespace Webkho_20241021.Models
{
    public class phieumuahang
    {
        public string? MaMuahang { get; set; }
        public string MaYeucau { get; set; }
        public string? MaDuan { get; set; }
        public string? MaNguoidung { get; set; }
        public DateTime? NgayMuahang { get; set; }
        public DateTime? NgayTao { get; set; }
        public string? TrangThai { get; set; }
        public string? GhiChu { get; set; }
    }
}
