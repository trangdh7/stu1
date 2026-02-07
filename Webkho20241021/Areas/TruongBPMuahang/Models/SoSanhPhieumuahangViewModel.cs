using System.Collections.Generic;

namespace Webkho_20241021.Areas.TruongBPMuahang.Data
{
    /// <summary>
    /// ViewModel cho trang so sánh nhiều phiếu mua hàng (gộp theo Tên thiết bị + Mã VT).
    /// </summary>
    public class SoSanhPhieumuahangViewModel
    {
        /// <summary>Danh sách phiếu được chọn: MaMuahang, MaYeucau (hiển thị tiêu đề cột SL).</summary>
        public List<PhieuSoSanhItem> PhieuList { get; set; } = new List<PhieuSoSanhItem>();

        /// <summary>Dòng dữ liệu gộp: mỗi dòng là một (Tên TB + Mã VT), có SL theo từng phiếu và đánh dấu chênh.</summary>
        public List<SoSanhRowViewModel> Rows { get; set; } = new List<SoSanhRowViewModel>();
    }

    public class PhieuSoSanhItem
    {
        public string MaMuahang { get; set; }
        public string MaYeucau { get; set; }
    }

    public class SoSanhRowViewModel
    {
        public string TenSanpham { get; set; }
        public string MaSanpham { get; set; }
        /// <summary>Hãng SX: một giá trị nếu giống nhau, hoặc "val1 / val2" nếu khác nhau giữa các yêu cầu.</summary>
        public string HangSX { get; set; }
        public bool HangSXChenh { get; set; }
        /// <summary>Nhà CC: một giá trị nếu giống nhau, hoặc "val1 / val2" nếu khác nhau giữa các yêu cầu.</summary>
        public string NhaCC { get; set; }
        public bool NhaCCChenh { get; set; }
        /// <summary>Danh sách giá trị Hãng SX theo từng phiếu (để lọc).</summary>
        public List<string> HangSXValues { get; set; } = new List<string>();
        /// <summary>Danh sách giá trị Nhà CC theo từng phiếu (để lọc).</summary>
        public List<string> NhaCCValues { get; set; } = new List<string>();
        public string DonVi { get; set; }
        /// <summary>Số lượng theo từng phiếu (theo thứ tự PhieuList).</summary>
        public List<int?> SlValues { get; set; } = new List<int?>();
        /// <summary>True nếu ô SL đó cần bôi màu (chênh so với các yêu cầu khác).</summary>
        public List<bool> SlChenh { get; set; } = new List<bool>();
    }
}
