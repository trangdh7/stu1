using System;
using System.Collections.Generic;

namespace Webkho_20241021.Models.ViewModels
{
    public class VatTuDuAnItemViewModel
    {
        public string? TenSanpham { get; set; }
        public string? MaSanpham { get; set; }
        public string? DAMakho { get; set; }
        public string? HangSX { get; set; }
        public string? NhaCC { get; set; }
        public int? SL { get; set; }
        public string? DonVi { get; set; }
        public DateTime? NgayNhapkho { get; set; }
        public DateTime? NgayBaohanh { get; set; }
        public DateTime? ThoiGianBH { get; set; }
        public string? TrangThai { get; set; }
        public string? MaXuatkho { get; set; }
        public string? MaYeucau { get; set; }
        public string? MaNguoidung { get; set; }
        public string? MaNguoiNhan { get; set; }
        public string? TenNguoiNhan { get; set; }
        public DateTime? NgayXuatkho { get; set; }
        public DateTime? NgayXacNhanNhan { get; set; }
    }

    public class NguoiNhanVatTuSummary
    {
        public string? MaNguoi { get; set; }
        public string TenNguoi { get; set; } = "Chưa xác định";
        public int TongSoLuong { get; set; }
        public int SoLoaiVatTu { get; set; }
    }

    public class ChiTietVatTuDuanViewModel
    {
        public duans? Duan { get; set; }
        public List<VatTuDuAnItemViewModel> VatTuList { get; set; } = new();
        public List<NguoiNhanVatTuSummary> NguoiNhanSummaries { get; set; } = new();
        public string? SelectedMaNguoiNhan { get; set; }
        public string SelectedTenNguoiNhan { get; set; } = string.Empty;
        public string? SearchKeyword { get; set; }
    }
}

