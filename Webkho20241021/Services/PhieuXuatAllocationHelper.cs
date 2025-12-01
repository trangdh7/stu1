using System;
using System.Collections.Generic;
using System.Linq;
using Webkho_20241021.Models;

namespace Webkho_20241021.Services
{
    public static class PhieuXuatAllocationHelper
    {
        /// <summary>
        /// Sau khi nhập hàng, tự động bơm thêm số lượng thiếu vào phiếu xuất và cập nhật trạng thái vật tư.
        /// </summary>
        public static void CapNhatPhieuXuatSauNhapHang(
            ApplicationDbContext context,
            phieuxuatkho phieuXuat,
            List<vtphieunhapkho> vtNhapList)
        {
            if (context == null || phieuXuat == null || string.IsNullOrEmpty(phieuXuat.MaYeucau))
            {
                return;
            }

            if (vtNhapList == null || vtNhapList.Count == 0)
            {
                return;
            }

            var vtYeuCauList = context.vtyeucau
                .Where(vt => vt.VTMaYeucau == phieuXuat.MaYeucau)
                .ToList();

            if (!vtYeuCauList.Any())
            {
                return;
            }

            var vtPhieuXuatList = context.vtphieuxuatkho
                .Where(vt => vt.MaXuatkho == phieuXuat.MaXuatkho)
                .ToList();

            bool daCapNhat = false;

            foreach (var vtYC in vtYeuCauList)
            {
                int soLuongYeuCau = vtYC.SL ?? 0;
                if (soLuongYeuCau <= 0)
                {
                    continue;
                }

                int soLuongDaCap = vtPhieuXuatList
                    .Where(vt => string.Equals(vt.MaSanpham, vtYC.MaSanpham, StringComparison.OrdinalIgnoreCase))
                    .Sum(vt => vt.SL ?? 0);

                int soLuongConThieu = soLuongYeuCau - soLuongDaCap;
                if (soLuongConThieu <= 0)
                {
                    if (vtYC.TrangThai == "Đang mua hàng")
                    {
                        vtYC.TrangThai = "Đang chuẩn bị hàng";
                        context.vtyeucau.Update(vtYC);
                        daCapNhat = true;
                    }
                    continue;
                }

                var vtNhapPhuHop = vtNhapList
                    .Where(vn => string.Equals(vn.MaSanpham, vtYC.MaSanpham, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var vtNhap in vtNhapPhuHop)
                {
                    if (soLuongConThieu <= 0)
                    {
                        break;
                    }

                    int soLuongBoSung = Math.Min(soLuongConThieu, vtNhap.SL ?? 0);
                    if (soLuongBoSung <= 0)
                    {
                        continue;
                    }

                    decimal? donGia = vtNhap.DonGia;
                    decimal? thanhTien = null;
                    if (vtNhap.ThanhTien.HasValue && vtNhap.SL.HasValue && vtNhap.SL.Value > 0)
                    {
                        thanhTien = (vtNhap.ThanhTien.Value / vtNhap.SL.Value) * soLuongBoSung;
                        if (!donGia.HasValue && soLuongBoSung > 0)
                        {
                            donGia = thanhTien / soLuongBoSung;
                        }
                    }
                    else if (donGia.HasValue)
                    {
                        thanhTien = donGia * soLuongBoSung;
                    }

                    var dongVtXuat = vtPhieuXuatList.FirstOrDefault(vt =>
                        string.Equals(vt.MaSanpham, vtNhap.MaSanpham, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(vt.Makho, vtNhap.Makho, StringComparison.OrdinalIgnoreCase) &&
                        vt.TrangThai != "Đã xác nhận nhận hàng" &&
                        vt.TrangThai != "Đã xuất kho");

                    if (dongVtXuat != null)
                    {
                        dongVtXuat.SL = (dongVtXuat.SL ?? 0) + soLuongBoSung;
                        dongVtXuat.TrangThai = "Đang chuẩn bị hàng";
                        if (donGia.HasValue)
                        {
                            dongVtXuat.DonGia = donGia;
                        }
                        if (thanhTien.HasValue)
                        {
                            dongVtXuat.ThanhTien = (dongVtXuat.ThanhTien ?? 0) + thanhTien;
                        }
                        context.vtphieuxuatkho.Update(dongVtXuat);
                    }
                    else
                    {
                        var newDong = new vtphieuxuatkho
                        {
                            MaXuatkho = phieuXuat.MaXuatkho,
                            MaYeucau = phieuXuat.MaYeucau,
                            TenSanpham = vtNhap.TenSanpham,
                            MaSanpham = vtNhap.MaSanpham,
                            Makho = vtNhap.Makho,
                            HangSX = vtNhap.HangSX,
                            NhaCC = vtNhap.NhaCC,
                            DonVi = vtNhap.DonVi,
                            SL = soLuongBoSung,
                            DonGia = donGia,
                            ThanhTien = thanhTien,
                            TrangThai = "Đang chuẩn bị hàng"
                        };
                        context.vtphieuxuatkho.Add(newDong);
                        vtPhieuXuatList.Add(newDong);
                    }

                    soLuongConThieu -= soLuongBoSung;
                    daCapNhat = true;
                }

                if (soLuongConThieu <= 0 && vtYC.TrangThai == "Đang mua hàng")
                {
                    vtYC.TrangThai = "Đang chuẩn bị hàng";
                    context.vtyeucau.Update(vtYC);
                }
            }

            if (!daCapNhat)
            {
                return;
            }

            bool duHang = vtYeuCauList.All(vt =>
            {
                int required = vt.SL ?? 0;
                if (required <= 0)
                {
                    return true;
                }

                int daCap = vtPhieuXuatList
                    .Where(vpx => string.Equals(vpx.MaSanpham, vt.MaSanpham, StringComparison.OrdinalIgnoreCase))
                    .Sum(vpx => vpx.SL ?? 0);

                return daCap >= required;
            });

            if (duHang)
            {
                phieuXuat.TrangThai = "Chờ xác nhận";
                phieuXuat.GhiChu = null;
                context.phieuxuatkho.Update(phieuXuat);

                foreach (var vtLine in vtPhieuXuatList)
                {
                    if (string.IsNullOrEmpty(vtLine.TrangThai) ||
                        vtLine.TrangThai.Contains("thiếu", StringComparison.OrdinalIgnoreCase) ||
                        vtLine.TrangThai.Contains("mua", StringComparison.OrdinalIgnoreCase))
                    {
                        vtLine.TrangThai = "Đang chuẩn bị hàng";
                        context.vtphieuxuatkho.Update(vtLine);
                    }
                }
            }
        }
    }
}

