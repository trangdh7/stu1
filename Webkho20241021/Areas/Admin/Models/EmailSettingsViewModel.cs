using System.ComponentModel.DataAnnotations;

namespace Webkho_20241021.Areas.Admin.Models
{
    public class EmailSettingsViewModel
    {
        [Required(ErrorMessage = "Vui lòng nhập SMTP server")]
        [Display(Name = "SMTP Server")]
        public string SmtpServer { get; set; } = string.Empty;

        [Range(1, 65535, ErrorMessage = "Port không hợp lệ")]
        [Display(Name = "SMTP Port")]
        public int SmtpPort { get; set; } = 465;

        [Required(ErrorMessage = "Vui lòng nhập email gửi đi")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [Display(Name = "Từ email")]
        public string FromEmail { get; set; } = string.Empty;

        [Display(Name = "Mật khẩu (để trống giữ nguyên)")]
        [DataType(DataType.Password)]
        public string? FromPassword { get; set; }

        [Display(Name = "Tên hiển thị")]
        public string? FromName { get; set; }
    }
}
