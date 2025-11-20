using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Webkho_20241021.Models
{
    public class SanPhamNhaCC
    {
        [Key]
        public int ID { get; set; }
        
        [Required]
        public string MaSanpham { get; set; }
        
        [Required]
        public string NhaCC { get; set; }
        
        // Thông tin bổ sung có thể có
        [Column(TypeName = "decimal(20,6)")]
        public decimal? DonGiaMacDinh { get; set; }
        
        public DateTime? NgayTao { get; set; }
        
        public string? GhiChu { get; set; }
    }
}

