using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Webkho_20241021.Models
{
    [Table("emailsetting")]
    public class EmailSetting
    {
        [Key]
        public int Id { get; set; }
        [Required]
        [MaxLength(255)]
        public string SmtpServer { get; set; } = string.Empty;

        [Required]
        [Range(1, 65535, ErrorMessage = "SMTP Port phải nằm trong khoảng 1 - 65535")]
        public int SmtpPort { get; set; }

        [Required]
        [MaxLength(255)]
        public string FromEmail { get; set; } = string.Empty;

        [MaxLength(512)]
        public string FromPassword { get; set; }

        [MaxLength(255)]
        public string FromName { get; set; }
        [MaxLength(255)]
        public string UpdatedBy { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}