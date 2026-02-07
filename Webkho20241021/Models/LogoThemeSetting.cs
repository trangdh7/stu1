using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Webkho_20241021.Models
{
    /// <summary>
    /// Chế độ giao diện logo: Normal (bình thường), Tet (Tết - hoa đào/ mai + pháo hoa), NationalDay (Quốc khánh - lá cờ).
    /// </summary>
    [Table("logothemesettings")]
    public class LogoThemeSetting
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Normal | Tet | NationalDay</summary>
        [Required]
        [MaxLength(32)]
        public string Theme { get; set; } = "Normal";

        [MaxLength(128)]
        public string UpdatedBy { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
