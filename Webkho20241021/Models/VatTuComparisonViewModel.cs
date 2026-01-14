namespace Webkho_20241021.Models
{
    public class VatTuComparisonViewModel
    {
        public string MaSanpham { get; set; } = "";
        public string TenSanpham { get; set; } = "";
        public string HangSX { get; set; } = "";
        public string DonVi { get; set; } = "";
        public List<FileVatTuDetail> ChiTiet { get; set; } = new List<FileVatTuDetail>();
        public int TongSL { get; set; }
        public int CapPhat { get; set; }
        public int TonDong { get; set; }
        public int Du { get; set; }
    }

    public class FileVatTuDetail
    {
        public int FileID { get; set; }
        public string TenFile { get; set; } = "";
        public DateTime NgayUpload { get; set; }
        public string MaYeucau { get; set; } = "";
        public int SL { get; set; }
        public string TrangThai { get; set; } = "";
    }
}

