using System.ComponentModel.DataAnnotations;

namespace Webkho_20241021.Models
{
    public class duans
    {     
        public string? TenDuan { get; set; }
        [Key]
        public string MaDuan { get; set; }
        public string? NguoiQLDA { get; set; }
        public string? MaNguoiQLDA { get; set; }
        public string? KhachHang { get; set; }
        public DateTime? NgayBatdau { get; set; }
        public DateTime? NgayKetthuc { get; set; }
        public string? TrangThai { get; set; }

    }
}
