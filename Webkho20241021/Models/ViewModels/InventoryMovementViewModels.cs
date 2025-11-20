using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Webkho_20241021.Models.ViewModels
{
    public class InventoryMovementSummaryViewModel
    {
        [Required]
        public string MaSanpham { get; set; } = string.Empty;

        public string? TenSanpham { get; set; }
        public string? HangSX { get; set; }
        public string? NhaCC { get; set; }
        public string? DonVi { get; set; }
        public string? DuAnGanNhat { get; set; }
        public int TonDauSoLuong { get; set; }
        public decimal GiaTriTonDau { get; set; }
        public int TongNhap { get; set; }
        public decimal GiaTriNhap { get; set; }
        public int TongXuat { get; set; }
        public decimal GiaTriXuat { get; set; }
        public int TonKho { get; set; }
        public decimal GiaTriTonCuoi { get; set; }
        public string? TaiKhoanVatTu { get; set; }
        public DateTime? LanNhapGanNhat { get; set; }
        public DateTime? LanXuatGanNhat { get; set; }
    }

    public class InventoryMovementDetailViewModel
    {
        [Required]
        public string Loai { get; set; } = string.Empty; // Nhập / Xuất

        public string? MaChungTu { get; set; }
        public DateTime? Ngay { get; set; }
        public string? DoiTuong { get; set; }
        public string? DuAn { get; set; }
        public int SoLuong { get; set; }
        public string? DonVi { get; set; }
        public decimal? DonGia { get; set; }
        public decimal? ThanhTien { get; set; }
        public string? MaKho { get; set; }
        public string? TkDoiUng { get; set; }
        public string? NguoiThucHien { get; set; }
        public string? GhiChu { get; set; }
    }

    public class InventoryMovementPageViewModel
    {
        public string? Keyword { get; set; }

        public List<InventoryMovementSummaryViewModel> Items { get; set; } = new();

        public int TongSoMatHang => Items?.Count ?? 0;
        public int TongNhap => Items?.Sum(i => i.TongNhap) ?? 0;
        public int TongXuat => Items?.Sum(i => i.TongXuat) ?? 0;
        public int TongTon => Items?.Sum(i => i.TonKho) ?? 0;
        public decimal TongGiaTriNhap => Items?.Sum(i => i.GiaTriNhap) ?? 0m;
        public decimal TongGiaTriXuat => Items?.Sum(i => i.GiaTriXuat) ?? 0m;
        public decimal TongGiaTriTon => Items?.Sum(i => i.GiaTriTonCuoi) ?? 0m;
    }

    public class InventoryMovementDetailResponse
    {
        public InventoryMovementSummaryViewModel? Summary { get; set; }
        public List<InventoryMovementDetailViewModel> Transactions { get; set; } = new();
    }
}


