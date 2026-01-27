using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

        // Không map vào DB, dùng để hiển thị tên đầy đủ Người yêu cầu
        [NotMapped]
        public string? TenNguoiyeucau { get; set; }
        
        // Không map vào DB, dùng để hiển thị Ngày cần từ vtyeucau (vật tư chi tiết)
        [NotMapped]
        public DateTime? NgayCanHang { get; set; }
    }
}
