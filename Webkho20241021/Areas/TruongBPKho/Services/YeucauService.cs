using Microsoft.EntityFrameworkCore;
using Webkho_20241021.Areas.TruongBPKho.Data;
using Webkho_20241021.Helpers;
using Webkho_20241021.Models;

namespace Webkho_20241021.Areas.TruongBPKho.Services
{
    /// <summary>
    /// Service để xử lý logic nghiệp vụ liên quan đến yêu cầu
    /// </summary>
    public class YeucauService
    {
        private readonly ApplicationDbContext _context;

        public YeucauService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Lấy danh sách yêu cầu với tìm kiếm và sắp xếp
        /// </summary>
        public Yeucauviewmodel GetDanhSachYeucau(string userRole, string search = "")
        {
            var yeucauList = _context.yeucau
                .ToList();

            // Cập nhật trạng thái yêu cầu
            CapNhatTrangThaiYeucau(yeucauList);

            var result = yeucauList
                .OrderByDescending(y => y.TrangThai == userRole)
                .ThenByDescending(y => y.NgayYeucau)
                .AsQueryable();

            // Áp dụng tìm kiếm
            result = SearchHelper.ApplySearchYeucau(result, search);

            return new Yeucauviewmodel
            {
                Yeucau = result.ToList(),
                VTyeucau = _context.vtyeucau.ToList(),
                Duans = _context.duans.ToList()
            };
        }

        /// <summary>
        /// Cập nhật trạng thái yêu cầu dựa trên số lượng vật tư còn thiếu
        /// </summary>
        private void CapNhatTrangThaiYeucau(List<yeucau> yeucauList)
        {
            foreach (var yeucau in yeucauList)
            {
                bool conVatTuThieu = false;

                var vatTuList = _context.vtyeucau
                    .Where(v => v.VTMaYeucau == yeucau.MaYeucau)
                    .ToList();

                foreach (var vt in vatTuList)
                {
                    if (TinhSoLuongConThieu(yeucau.MaYeucau, vt.MaSanpham) > 0)
                    {
                        conVatTuThieu = true;
                        break;
                    }
                }

                // Chỉ cập nhật nếu trạng thái là "Đã duyệt" hoặc "Đang mua hàng"
                if (yeucau.TrangThai == "Đã duyệt" || yeucau.TrangThai == "Đang mua hàng")
                {
                    yeucau.TrangThai = conVatTuThieu ? "Đang mua hàng" : "Hoàn thành";
                }
            }

            _context.SaveChanges();
        }

        /// <summary>
        /// Tính số lượng còn thiếu cần mua cho một vật tư trong yêu cầu
        /// </summary>
        private int TinhSoLuongConThieu(string maYeucau, string maSanpham)
        {
            if (string.IsNullOrWhiteSpace(maYeucau) || string.IsNullOrWhiteSpace(maSanpham))
                return 0;

            // 1. Tổng số lượng yêu cầu
            var tongYeuCau = _context.vtyeucau
                .Where(v => v.VTMaYeucau == maYeucau && v.MaSanpham == maSanpham)
                .Sum(v => (int?)v.SL) ?? 0;

            if (tongYeuCau <= 0)
                return 0;

            // 2. Tổng đã xuất kho
            var tongDaXuat = _context.vtphieuxuatkho
                .Where(v => v.MaYeucau == maYeucau && v.MaSanpham == maSanpham)
                .Sum(v => (int?)v.SL) ?? 0;

            // 3. Tổng đã nhập kho (chỉ đếm phiếu đã nhập kho thành công)
            var tongDaNhap = _context.vtphieunhapkho
                .Where(v => v.MaYeucau == maYeucau 
                    && v.MaSanpham == maSanpham
                    && (v.TrangThai == "Đã nhập kho" || v.TrangThai == "Đã xác nhận nhận hàng" || v.TrangThai == "Hoàn thành"))
                .Sum(v => (int?)v.SL) ?? 0;

            // 4. Công thức: Còn thiếu = Tổng yêu cầu - Đã xuất - Đã nhập
            var conThieu = tongYeuCau - tongDaXuat - tongDaNhap;

            return conThieu > 0 ? conThieu : 0;
        }

        /// <summary>
        /// Xử lý trạng thái vật tư yêu cầu
        /// </summary>
        public void XuLyTrangThaiVatTu(vtyeucau vt, bool isApproved, string nextTrangThai)
        {
            vt.NgayDuyet = DateTime.Now;
            vt.TrangThai = isApproved ? nextTrangThai : TrangThaiVatTu.DaTuChoi;
        }
    }
}

