using Microsoft.EntityFrameworkCore;
using Webkho_20241021.Models;

namespace Webkho_20241021.Helpers
{
    /// <summary>
    /// Helper class để lấy tên người yêu cầu, tránh lặp code
    /// </summary>
    public static class NguoiYeuCauHelper
    {
        /// <summary>
        /// Lấy tên người yêu cầu từ yeucau hoặc nguoidungs
        /// Ưu tiên lấy từ yeucau, không có thì lấy từ nguoidungs
        /// </summary>
        public static string GetTenNguoiYeuCau(ApplicationDbContext context, string? maYeucau, string? maNguoidung)
        {
            // Ưu tiên lấy từ yeucau
            if (!string.IsNullOrEmpty(maYeucau))
            {
                var yc = context.yeucau.FirstOrDefault(x => x.MaYeucau == maYeucau);
                if (yc != null && !string.IsNullOrEmpty(yc.NguoiYeucau))
                {
                    return yc.NguoiYeucau;
                }
            }

            // Không có thì lấy từ nguoidungs
            if (!string.IsNullOrEmpty(maNguoidung))
            {
                var nd = context.nguoidungs.FirstOrDefault(x => x.MaNguoidung == maNguoidung);
                if (nd != null && !string.IsNullOrEmpty(nd.TenNguoidung))
                {
                    return nd.TenNguoidung;
                }
            }

            return "";
        }

        /// <summary>
        /// Gán tên người yêu cầu cho danh sách phiếu mua hàng
        /// </summary>
        public static void GanTenNguoiYeuCauChoPhieuMuaHang(ApplicationDbContext context, List<phieumuahang> phieuList)
        {
            var nguoiDungDict = context.nguoidungs
                .ToDictionary(n => n.MaNguoidung, n => n.TenNguoidung);
            // Lấy Ngày cần từ bảng vtyeucau (vật tư chi tiết) - lấy ngày sớm nhất
            var vtyeucauDict = context.vtyeucau
                .Where(v => v.NgayCanHang != null)
                .GroupBy(v => v.VTMaYeucau)
                .ToDictionary(g => g.Key, g => g.Min(v => v.NgayCanHang));

            foreach (var phieu in phieuList)
            {
                if (!string.IsNullOrEmpty(phieu.MaNguoidung) && 
                    nguoiDungDict.TryGetValue(phieu.MaNguoidung, out var ten))
                {
                    phieu.TenNguoiyeucau = ten;
                }
                // Gán Ngày cần từ vtyeucau (vật tư chi tiết) - lấy ngày sớm nhất
                if (!string.IsNullOrEmpty(phieu.MaYeucau) && vtyeucauDict.TryGetValue(phieu.MaYeucau, out var ngayCanHang))
                {
                    phieu.NgayCanHang = ngayCanHang;
                }
            }
        }
    }
}

