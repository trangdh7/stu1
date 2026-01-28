using Microsoft.EntityFrameworkCore;
using Webkho_20241021.Areas.TruongBPKho.Data;
using Webkho_20241021.Helpers;
using Webkho_20241021.Models;
using Webkho_20241021.Services;

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

            // Populate TenYeucau and Bophan từ nguoidungs nếu chưa có
            var nguoiDungDict = _context.nguoidungs.ToDictionary(n => n.MaNguoidung, n => new { n.TenNguoidung, n.Bophan });
            foreach (var yeucau in yeucauList)
            {
                // Populate Bophan từ nguoidungs nếu chưa có
                if (string.IsNullOrWhiteSpace(yeucau.Bophan) && !string.IsNullOrWhiteSpace(yeucau.YCMaNguoidung))
                {
                    if (nguoiDungDict.TryGetValue(yeucau.YCMaNguoidung, out var nguoiDung))
                    {
                        yeucau.Bophan = nguoiDung.Bophan ?? "";
                    }
                }

                // Populate TenYeucau nếu chưa có
                if (string.IsNullOrWhiteSpace(yeucau.TenYeucau))
                {
                    // Nếu là yêu cầu nhập kho đặc biệt (NHAPKHO_), set mặc định
                    if (!string.IsNullOrEmpty(yeucau.MaYeucau) && (yeucau.MaYeucau.StartsWith("NHAPKHO_") || yeucau.MaYeucau.StartsWith("NK")))
                    {
                        yeucau.TenYeucau = "Yêu cầu nhập kho";
                    }
                    else
                    {
                        // Các yêu cầu khác, set mặc định là "Yêu cầu vật tư"
                        yeucau.TenYeucau = "Yêu cầu vật tư";
                    }
                }
            }

            // Đồng bộ trạng thái vật tư dựa trên phiếu
            DongBoTrangThaiVatTu();

            // Cập nhật trạng thái yêu cầu
            CapNhatTrangThaiYeucau(yeucauList);

            // Lọc dữ liệu hợp lệ:
            // - Yêu cầu vật tư: có dòng chi tiết trong vtyeucau
            // - Yêu cầu nhập kho: chi tiết nằm ở vtphieunhapkho (thông qua phieunhapkho)
            // Trước đây dùng DataFilterService.FilterYeucau(yeucauList, vtyeucauList) => làm "mất" các yêu cầu nhập kho.
            // Chỉ hiển thị các vật tư có SLMoi > 0
            var VTyeucaulist = _context.vtyeucau
                .Where(v => v.SLMoi.HasValue && v.SLMoi.Value > 0)
                .ToList();

            var yeucauCodesWithVtYeuCau = VTyeucaulist
                .Select(vt => vt.VTMaYeucau)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .ToHashSet();

            var yeucauCodesWithVtNhapKho = (from vtnk in _context.vtphieunhapkho
                                           join pnk in _context.phieunhapkho on vtnk.MaNhapkho equals pnk.MaNhapkho
                                           select pnk.MaYeucau)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct()
                .ToHashSet();

            var validYeucauList = yeucauList
                .Where(y => !string.IsNullOrWhiteSpace(y.MaYeucau)
                            && (yeucauCodesWithVtYeuCau.Contains(y.MaYeucau)
                                || yeucauCodesWithVtNhapKho.Contains(y.MaYeucau)))
                .ToList();

            var result = validYeucauList
                .OrderByDescending(y => y.TrangThai == userRole)
                .ThenByDescending(y => y.NgayYeucau)
                .AsQueryable();

            // Áp dụng tìm kiếm
            result = SearchHelper.ApplySearchYeucau(result, search);

            return new Yeucauviewmodel
            {
                Yeucau = result.ToList(),
                VTyeucau = VTyeucaulist,
                Duans = _context.duans.ToList()
            };
        }

        private void DongBoTrangThaiVatTu()
        {
            // Lấy tất cả phiếu xuất kho có trạng thái "Đã xuất kho"
            var phieuxuatkhoList = _context.phieuxuatkho
                .Where(p => p.TrangThai == "Đã xuất kho")
                .ToList();

            foreach (var phieu in phieuxuatkhoList)
            {
                if (string.IsNullOrEmpty(phieu.MaYeucau))
                    continue;

                // Lấy các vật tư trong phiếu xuất kho
                var vtPhieuxuatkhoList = _context.vtphieuxuatkho
                    .Where(vt => vt.MaXuatkho == phieu.MaXuatkho)
                    .ToList();

                foreach (var vtPhieu in vtPhieuxuatkhoList)
                {
                    if (string.IsNullOrEmpty(vtPhieu.MaYeucau) || string.IsNullOrEmpty(vtPhieu.MaSanpham))
                        continue;

                    // Tìm các vật tư yêu cầu tương ứng
                    var vtYeucauList = _context.vtyeucau
                        .Where(v => v.VTMaYeucau == vtPhieu.MaYeucau && v.MaSanpham == vtPhieu.MaSanpham)
                        .ToList();

                    foreach (var vtYeucau in vtYeucauList)
                    {
                        // Nếu phiếu đã xuất kho, cập nhật trạng thái vật tư thành "Đã xuất kho"
                        if (vtYeucau.TrangThai != "Đã xuất kho" && vtYeucau.TrangThai != "Hoàn thành")
                        {
                            vtYeucau.TrangThai = "Đã xuất kho";
                            _context.vtyeucau.Update(vtYeucau);
                        }
                    }
                }
            }

            _context.SaveChanges();
        }

        
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

