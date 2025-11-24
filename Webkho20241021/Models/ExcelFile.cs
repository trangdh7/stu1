using System.ComponentModel.DataAnnotations;

namespace Webkho_20241021.Models
{
    public class ExcelFile
    {
        [Key]
        public int ID { get; set; }
        public string? MaYeucau { get; set; }
        public string? MaDuan { get; set; }
        public string? TenFile { get; set; }
        public string? DuongDanFile { get; set; }
        public DateTime? NgayUpload { get; set; }
        public string? NguoiUpload { get; set; }
        public long? KichThuocFile { get; set; }
    }
}

