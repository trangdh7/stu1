using System.ComponentModel.DataAnnotations;

namespace Webkho_20241021.Models
{
    /// <summary>
    /// Danh mục nhà cung cấp - quản lý bởi Trưởng BP mua hàng
    /// </summary>
    public class NhaCungCap
    {
        [Key]
        public int ID { get; set; }

        [Required(ErrorMessage = "Tên nhà cung cấp không được để trống")]
        [MaxLength(255)]
        [Display(Name = "Tên nhà cung cấp")]
        public string TenNhaCC { get; set; }

        [MaxLength(500)]
        [Display(Name = "Ghi chú")]
        public string? GhiChu { get; set; }

        public DateTime? NgayTao { get; set; }
        public DateTime? NgayCapNhat { get; set; }
    }
}
