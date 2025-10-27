using System.ComponentModel.DataAnnotations;

namespace Webkho_20241021.Models
{
    public class nguoidungs
    {     
        public string? TenNguoidung { get; set; }

        [Key]
        public string MaNguoidung { get; set; }

        public string? Chucvu { get; set; }
        public string? Bophan { get; set; }
    }
}
