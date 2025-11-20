using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Webkho_20241021.Models;
using Webkho_20241021.Models.ViewModels;

namespace Webkho_20241021.Services
{
    public class InventoryMovementService
    {
        private readonly ApplicationDbContext _context;

        public InventoryMovementService(ApplicationDbContext context)
        {
            _context = context;
        }

        public InventoryMovementPageViewModel BuildPageViewModel(string? keyword)
        {
            keyword = keyword?.Trim();

            var khoRecords = _context.khotongs
                .AsNoTracking()
                .Where(k => !string.IsNullOrWhiteSpace(k.MaSanpham))
                .ToList();

            var nhapRecords = _context.vtphieunhapkho
                .AsNoTracking()
                .Where(v => !string.IsNullOrWhiteSpace(v.MaSanpham))
                .ToList();

            var xuatRecords = _context.vtphieuxuatkho
                .AsNoTracking()
                .Where(v => !string.IsNullOrWhiteSpace(v.MaSanpham))
                .ToList();

            var khoAggregates = khoRecords
                .GroupBy(k => k.MaSanpham!)
                .ToDictionary(
                    g => g.Key,
                    g => new ItemAggregate
                    {
                        MaSanpham = g.Key,
                        TenSanpham = g.Select(x => x.TenSanpham).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? g.Key,
                        HangSX = g.Select(x => x.HangSX).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                        NhaCC = g.Select(x => x.NhaCC).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                        DonVi = g.Select(x => x.DonVi).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                        DuAn = g.Select(x => x.DuAn).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                        TaiKhoanVatTu = g.Select(x => x.LoaiCapPhat).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                        Quantity = g.Sum(x => x.SL ?? 0),
                        LastDate = g.Max(x => x.NgayNhapkho)
                    });

            var nhapAggregates = nhapRecords
                .GroupBy(v => v.MaSanpham!)
                .ToDictionary(
                    g => g.Key,
                    g => new ItemAggregate
                    {
                        MaSanpham = g.Key,
                        TenSanpham = g.Select(x => x.TenSanpham).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? g.Key,
                        HangSX = g.Select(x => x.HangSX).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                        NhaCC = g.Select(x => x.NhaCC).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                        DonVi = g.Select(x => x.DonVi).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                        Quantity = g.Sum(x => x.SL ?? 0),
                        Amount = g.Sum(x => x.ThanhTien ?? ((x.SL ?? 0) * (x.DonGia ?? 0m))),
                        LastDate = g.Max(x => x.NgayNhapkho)
                    });

            var xuatAggregates = xuatRecords
                .GroupBy(v => v.MaSanpham!)
                .ToDictionary(
                    g => g.Key,
                    g => new ItemAggregate
                    {
                        MaSanpham = g.Key,
                        TenSanpham = g.Select(x => x.TenSanpham).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? g.Key,
                        HangSX = g.Select(x => x.HangSX).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                        NhaCC = g.Select(x => x.NhaCC).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                        DonVi = g.Select(x => x.DonVi).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                        Quantity = g.Sum(x => x.SL ?? 0),
                        Amount = g.Sum(x => x.ThanhTien ?? ((x.SL ?? 0) * (x.DonGia ?? 0m))),
                        LastDate = g.Max(x => x.NgayNhapkho)
                    });

            var allCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in khoAggregates.Keys) allCodes.Add(key);
            foreach (var key in nhapAggregates.Keys) allCodes.Add(key);
            foreach (var key in xuatAggregates.Keys) allCodes.Add(key);

            var summaries = new List<InventoryMovementSummaryViewModel>();
            foreach (var code in allCodes)
            {
                var kho = TryGetAggregate(khoAggregates, code);
                var nhap = TryGetAggregate(nhapAggregates, code);
                var xuat = TryGetAggregate(xuatAggregates, code);

                var tonCuoi = kho?.Quantity ?? Math.Max((nhap?.Quantity ?? 0) - (xuat?.Quantity ?? 0), 0);
                var tongNhap = nhap?.Quantity ?? 0;
                var tongXuat = xuat?.Quantity ?? 0;
                var tonDau = tonCuoi - (tongNhap - tongXuat);
                if (tonDau < 0) tonDau = 0;

                var giaTriNhap = nhap?.Amount ?? 0m;
                var giaTriXuat = xuat?.Amount ?? 0m;
                decimal giaTriTonCuoi = 0m;
                if (tonCuoi > 0 && tongNhap > 0)
                {
                    var giaBinhQuan = giaTriNhap / Math.Max(1, tongNhap);
                    giaTriTonCuoi = giaBinhQuan * tonCuoi;
                }

                var giaTriTonDau = Math.Max(0m, giaTriTonCuoi + giaTriXuat - giaTriNhap);

                var summary = new InventoryMovementSummaryViewModel
                {
                    MaSanpham = code,
                    TenSanpham = kho?.TenSanpham ?? nhap?.TenSanpham ?? xuat?.TenSanpham ?? code,
                    HangSX = kho?.HangSX ?? nhap?.HangSX ?? xuat?.HangSX,
                    NhaCC = kho?.NhaCC ?? nhap?.NhaCC ?? xuat?.NhaCC,
                    DonVi = kho?.DonVi ?? nhap?.DonVi ?? xuat?.DonVi,
                    DuAnGanNhat = kho?.DuAn,
                    TonDauSoLuong = tonDau,
                    GiaTriTonDau = giaTriTonDau,
                    TongNhap = tongNhap,
                    GiaTriNhap = giaTriNhap,
                    TongXuat = tongXuat,
                    GiaTriXuat = giaTriXuat,
                    TonKho = tonCuoi,
                    GiaTriTonCuoi = giaTriTonCuoi,
                    TaiKhoanVatTu = kho?.TaiKhoanVatTu ?? "152",
                    LanNhapGanNhat = nhap?.LastDate,
                    LanXuatGanNhat = xuat?.LastDate
                };

                if (IsMatch(summary, keyword))
                {
                    summaries.Add(summary);
                }
            }

            var ordered = summaries
                .OrderByDescending(s => s.TonKho)
                .ThenBy(s => s.TenSanpham)
                .ToList();

            return new InventoryMovementPageViewModel
            {
                Keyword = keyword,
                Items = ordered
            };
        }

        public InventoryMovementDetailResponse BuildDetail(string maSanpham)
        {
            var response = new InventoryMovementDetailResponse();

            if (string.IsNullOrWhiteSpace(maSanpham))
            {
                return response;
            }

            var code = maSanpham.Trim();

            var khoRecords = _context.khotongs
                .AsNoTracking()
                .Where(k => k.MaSanpham == code)
                .ToList();

            var nhapDetails = (from detail in _context.vtphieunhapkho.AsNoTracking()
                               where detail.MaSanpham == code
                               join phieu in _context.phieunhapkho.AsNoTracking()
                                   on new { detail.MaNhapkho, detail.MaYeucau }
                                   equals new { phieu.MaNhapkho, phieu.MaYeucau } into phieuGroup
                               from phieu in phieuGroup.DefaultIfEmpty()
                               join user in _context.nguoidungs.AsNoTracking()
                                   on phieu.MaNguoidung equals user.MaNguoidung into userGroup
                               from user in userGroup.DefaultIfEmpty()
                               select new InventoryMovementDetailViewModel
                               {
                                   Loai = "Nhập",
                                   MaChungTu = detail.MaNhapkho,
                                   Ngay = detail.NgayNhapkho ?? (phieu != null ? phieu.NgayNhapkho : null),
                                   DoiTuong = detail.NhaCC,
                                   DuAn = phieu != null ? phieu.MaDuan : null,
                                   SoLuong = detail.SL ?? 0,
                                   DonVi = detail.DonVi,
                                   DonGia = detail.DonGia,
                                   ThanhTien = detail.ThanhTien ?? ((detail.DonGia ?? 0m) * (detail.SL ?? 0)),
                                   MaKho = detail.Makho,
                                   TkDoiUng = phieu != null ? (phieu.MaNguoidung ?? phieu.MaDuan) : null,
                                   NguoiThucHien = user != null ? user.TenNguoidung : null,
                                   GhiChu = phieu != null ? phieu.TrangThai : null
                               }).ToList();

            var xuatDetails = (from detail in _context.vtphieuxuatkho.AsNoTracking()
                               where detail.MaSanpham == code
                               join phieu in _context.phieuxuatkho.AsNoTracking()
                                   on new { detail.MaXuatkho, detail.MaYeucau }
                                   equals new { phieu.MaXuatkho, phieu.MaYeucau } into phieuGroup
                               from phieu in phieuGroup.DefaultIfEmpty()
                               join user in _context.nguoidungs.AsNoTracking()
                                   on phieu.MaNguoidung equals user.MaNguoidung into userGroup
                               from user in userGroup.DefaultIfEmpty()
                               select new InventoryMovementDetailViewModel
                               {
                                   Loai = "Xuất",
                                   MaChungTu = detail.MaXuatkho,
                                   Ngay = phieu != null ? (phieu.NgayXuatkho ?? phieu.NgayHoanThanh ?? detail.NgayNhapkho) : detail.NgayNhapkho,
                                   DoiTuong = user != null
                                       ? user.TenNguoidung
                                       : (phieu != null ? phieu.MaNguoidung : null),
                                   DuAn = phieu != null ? phieu.MaDuan : null,
                                   SoLuong = detail.SL ?? 0,
                                   DonVi = detail.DonVi,
                                   DonGia = detail.DonGia,
                                   ThanhTien = detail.ThanhTien ?? ((detail.DonGia ?? 0m) * (detail.SL ?? 0)),
                                   MaKho = detail.Makho,
                                   TkDoiUng = phieu != null ? (phieu.MaNguoidung ?? phieu.MaDuan) : null,
                                   NguoiThucHien = user != null ? user.TenNguoidung : null,
                                   GhiChu = phieu != null ? (phieu.GhiChu ?? phieu.TrangThai) : null
                               }).ToList();

            response.Transactions = nhapDetails
                .Concat(xuatDetails)
                .OrderByDescending(t => t.Ngay)
                .ThenByDescending(t => t.MaChungTu)
                .ToList();

            var tongNhap = nhapDetails.Sum(t => t.SoLuong);
            var tongXuat = xuatDetails.Sum(t => t.SoLuong);
            var tonKho = khoRecords.Sum(k => k.SL ?? 0);

            response.Summary = new InventoryMovementSummaryViewModel
            {
                MaSanpham = code,
                TenSanpham = khoRecords.Select(k => k.TenSanpham).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                    ?? nhapDetails.FirstOrDefault()?.DoiTuong
                    ?? response.Transactions.FirstOrDefault()?.DoiTuong
                    ?? code,
                HangSX = khoRecords.Select(k => k.HangSX).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                NhaCC = khoRecords.Select(k => k.NhaCC).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                DonVi = khoRecords.Select(k => k.DonVi).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                    ?? nhapDetails.Select(d => d.DonVi).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                DuAnGanNhat = khoRecords.Select(k => k.DuAn).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)),
                TongNhap = tongNhap,
                TongXuat = tongXuat,
                TonKho = tonKho,
                LanNhapGanNhat = nhapDetails.Select(d => d.Ngay).Where(d => d.HasValue).DefaultIfEmpty().Max(),
                LanXuatGanNhat = xuatDetails.Select(d => d.Ngay).Where(d => d.HasValue).DefaultIfEmpty().Max()
            };

            return response;
        }

        private static ItemAggregate? TryGetAggregate(Dictionary<string, ItemAggregate> dict, string key)
        {
            return dict.TryGetValue(key, out var value) ? value : null;
        }

        private static bool IsMatch(InventoryMovementSummaryViewModel summary, string? keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                return true;
            }

            return (summary.MaSanpham?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
                || (summary.TenSanpham?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
                || (summary.HangSX?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false)
                || (summary.NhaCC?.Contains(keyword, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        private class ItemAggregate
        {
            public string MaSanpham { get; set; } = string.Empty;
            public string? TenSanpham { get; set; }
            public string? HangSX { get; set; }
            public string? NhaCC { get; set; }
            public string? DonVi { get; set; }
            public string? DuAn { get; set; }
            public string? TaiKhoanVatTu { get; set; }
            public int Quantity { get; set; }
            public decimal Amount { get; set; }
            public DateTime? LastDate { get; set; }
        }
    }
}


