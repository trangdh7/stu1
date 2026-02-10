using Webkho_20241021.Models;

namespace Webkho_20241021.Areas.TruongBPMuahang.Data
{
    public class LichCoHangLineDto
    {
        public int SL { get; set; }
        public DateTime? NgayCoHang { get; set; }
        /// <summary>Đơn giá riêng cho đợt này (nay giá này mai giá kia). Null = dùng giá dòng gốc.</summary>
        public decimal? DonGia { get; set; }
    }

    public class VTPhieuMuaHangLichCoHangDto
    {
        public string? MaSanpham { get; set; }
        public List<LichCoHangLineDto>? Lines { get; set; }
    }

    public class Phieumuahangviewmodel
    {
        public string MaMuahang { get; set; }
        public List<phieumuahang> Phieumuahang { get; set; }
        public List<vtphieumuahang> VTphieumuahang { get; set; }
        public List<VTPhieuMuaHangLichCoHangDto>? VTphieumuahangSplits { get; set; }

    }
}
