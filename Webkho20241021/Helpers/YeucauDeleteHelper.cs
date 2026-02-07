using Microsoft.EntityFrameworkCore;
using Webkho_20241021.Models;

namespace Webkho_20241021.Helpers
{
    /// <summary>
    /// Helper xóa yêu cầu và dữ liệu liên quan; kiểm tra quyền được phép xóa theo vai trò.
    /// </summary>
    public static class YeucauDeleteHelper
    {
        /// <summary>
        /// Xóa yêu cầu và toàn bộ dữ liệu liên quan (vtyeucau, phiếu mua hàng, phiếu xuất/nhập kho) khỏi database.
        /// Thứ tự xóa: vtphieumuahang → phieumuahang → vtphieuxuatkho → phieuxuatkho → vtphieunhapkho → phieunhapkho → vtyeucau → yeucau.
        /// </summary>
        public static bool XoaYeucauVaPhieuLienQuan(ApplicationDbContext context, string maYeucau)
        {
            if (string.IsNullOrWhiteSpace(maYeucau)) return false;

            var yeucau = context.yeucau.FirstOrDefault(y => y.MaYeucau == maYeucau);
            if (yeucau == null) return false;

            // Phiếu mua hàng + vt
            var phieuMuaHangList = context.phieumuahang.Where(p => p.MaYeucau == maYeucau).ToList();
            foreach (var pmh in phieuMuaHangList)
            {
                var vtList = context.vtphieumuahang.Where(v => v.MaMuahang == pmh.MaMuahang).ToList();
                context.vtphieumuahang.RemoveRange(vtList);
            }
            context.phieumuahang.RemoveRange(phieuMuaHangList);

            // Phiếu xuất kho + vt
            var phieuXuatList = context.phieuxuatkho.Where(p => p.MaYeucau == maYeucau).ToList();
            foreach (var px in phieuXuatList)
            {
                var vtList = context.vtphieuxuatkho.Where(v => v.MaXuatkho == px.MaXuatkho).ToList();
                context.vtphieuxuatkho.RemoveRange(vtList);
            }
            context.phieuxuatkho.RemoveRange(phieuXuatList);

            // Phiếu nhập kho + vt
            var phieuNhapList = context.phieunhapkho.Where(p => p.MaYeucau == maYeucau).ToList();
            foreach (var pn in phieuNhapList)
            {
                var vtList = context.vtphieunhapkho.Where(v => v.MaNhapkho == pn.MaNhapkho).ToList();
                context.vtphieunhapkho.RemoveRange(vtList);
            }
            context.phieunhapkho.RemoveRange(phieuNhapList);

            // Vật tư yêu cầu + yêu cầu
            var vtyeucauList = context.vtyeucau.Where(v => v.VTMaYeucau == maYeucau).ToList();
            context.vtyeucau.RemoveRange(vtyeucauList);
            context.yeucau.Remove(yeucau);

            context.SaveChanges();
            return true;
        }

        /// <summary>
        /// Nhân viên (người tạo yêu cầu) chỉ được xóa khi Trưởng BP chưa duyệt: trạng thái bắt đầu bằng "Chờ Trưởng BP" hoặc "Chờ Trưởng Phòng".
        /// </summary>
        public static bool CoTheXoaYeucauNhanVien(yeucau y, string maNguoiDung)
        {
            if (y == null || string.IsNullOrWhiteSpace(maNguoiDung)) return false;
            if (y.YCMaNguoidung != maNguoiDung) return false;

            var tt = (y.TrangThai ?? "").Trim();
            return tt.StartsWith("Chờ Trưởng BP", StringComparison.OrdinalIgnoreCase)
                   || tt.StartsWith("Chờ Trưởng Phòng", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Trưởng BP được xóa khi: (1) chưa duyệt — trạng thái chờ Trưởng BP đúng bộ phận; (2) đã duyệt nhưng QLDA/Giám đốc chưa duyệt — "Chờ quản lý dự án duyệt" hoặc "Chờ giám đốc duyệt".
        /// </summary>
        public static bool CoTheXoaYeucauTruongBP(yeucau y, string chucVu, string boPhan)
        {
            if (y == null || chucVu != "Trưởng BP" || string.IsNullOrWhiteSpace(boPhan)) return false;

            var tt = (y.TrangThai ?? "").Trim();
            // Đang chờ mình duyệt (chờ Trưởng BP đúng bộ phận)
            if (tt.StartsWith("Chờ Trưởng BP", StringComparison.OrdinalIgnoreCase) && tt.IndexOf(boPhan, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (tt.StartsWith("Chờ Trưởng Phòng", StringComparison.OrdinalIgnoreCase) && tt.IndexOf(boPhan, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            // Đã duyệt, chờ QLDA hoặc Giám đốc
            if (tt.Equals("Chờ quản lý dự án duyệt", StringComparison.OrdinalIgnoreCase)) return true;
            if (tt.Equals("Chờ giám đốc duyệt", StringComparison.OrdinalIgnoreCase)) return true;
            if (tt.Equals("Chờ Giám đốc duyệt", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>
        /// Quản lý dự án được xóa khi: chờ QLDA duyệt hoặc đã duyệt nhưng chờ Giám đốc duyệt.
        /// </summary>
        public static bool CoTheXoaYeucauQLDA(yeucau y)
        {
            if (y == null) return false;
            var tt = (y.TrangThai ?? "").Trim();
            return tt.Equals("Chờ quản lý dự án duyệt", StringComparison.OrdinalIgnoreCase)
                   || tt.Equals("Chờ giám đốc duyệt", StringComparison.OrdinalIgnoreCase)
                   || tt.Equals("Chờ Giám đốc duyệt", StringComparison.OrdinalIgnoreCase);
        }
    }
}
