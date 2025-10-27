using System.ComponentModel.DataAnnotations;

namespace Webkho_20241021.Models
{
    public class yeucau
    {     
        public string? TenYeucau { get; set; }

        [Key]
        public string MaYeucau { get; set; }
        public string? NguoiYeucau { get; set; }
        public string? Bophan { get; set; }
        public string? YCMaNguoidung { get; set; }
        public string? YCMaDuan { get; set; }
        public DateTime? NgayYeucau { get; set; }
        public string? TrangThai { get; set; }

    }
}
