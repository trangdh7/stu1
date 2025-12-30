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

            foreach (var phieu in phieuList)
            {
                if (!string.IsNullOrEmpty(phieu.MaNguoidung) && 
                    nguoiDungDict.TryGetValue(phieu.MaNguoidung, out var ten))
                {
                    phieu.TenNguoiyeucau = ten;
                }
            }
        }
    }
}

