using System.Collections.Generic;
using System.Linq;
using Webkho_20241021.Models;

namespace Webkho_20241021.Services
{
    
    /// <summary>
    /// Dịch vụ lọc dữ liệu trống/null dùng chung cho tất cả areas.
    /// </summary>
    public static class DataFilterService
    {
        public static List<yeucau> FilterYeucau(IEnumerable<yeucau> yeucauList, IEnumerable<vtyeucau> vtyeucauList)
        {
            var vtLookup = vtyeucauList
                .Select(vt => vt.VTMaYeucau)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .ToHashSet();

            return yeucauList
                .Where(y => !string.IsNullOrWhiteSpace(y.MaYeucau) && vtLookup.Contains(y.MaYeucau))
                .ToList();
        }

        public static List<phieuxuatkho> FilterPhieuxuatkho(IEnumerable<phieuxuatkho> phieuList, IEnumerable<vtphieuxuatkho> vtList)
        {
            var vtLookup = vtList
                .Select(vt => vt.MaXuatkho)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .ToHashSet();

            return phieuList
                .Where(p => !string.IsNullOrWhiteSpace(p.MaXuatkho) && vtLookup.Contains(p.MaXuatkho))
                .ToList();
        }

        public static List<phieunhapkho> FilterPhieunhapkho(IEnumerable<phieunhapkho> phieuList, IEnumerable<vtphieunhapkho> vtList)
        {
            var vtLookup = vtList
                .Select(vt => vt.MaNhapkho)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .ToHashSet();

            return phieuList
                .Where(p => !string.IsNullOrWhiteSpace(p.MaNhapkho) && vtLookup.Contains(p.MaNhapkho))
                .ToList();
        }

        public static List<phieumuahang> FilterPhieumuahang(IEnumerable<phieumuahang> phieuList, IEnumerable<vtphieumuahang> vtList)
        {
            var vtLookup = vtList
                .Select(vt => vt.MaMuahang)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .ToHashSet();

            return phieuList
                .Where(p => !string.IsNullOrWhiteSpace(p.MaMuahang) && vtLookup.Contains(p.MaMuahang))
                .ToList();
        }

        /// <summary>
        /// Lọc danh sách tổng kho theo bộ lọc KhotongFilter.
        /// Dùng chung cho các màn hình Tổng kho (Admin, Trưởng BP kho, Nhân viên, ...).
        /// </summary>
        public static IQueryable<khotongs> FilterKhotongs(IQueryable<khotongs> query, KhotongFilter filter)
        {
            if (filter == null || filter.IsEmpty)
                return query;

            if (!string.IsNullOrWhiteSpace(filter.HangSX))
            {
                query = query.Where(k => k.HangSX == filter.HangSX);
            }

            if (!string.IsNullOrWhiteSpace(filter.NhaCC))
            {
                query = query.Where(k => k.NhaCC == filter.NhaCC);
            }

            return query;
        }

        /// <summary>
        /// Lọc danh sách vtyeucau theo các cột số lượng: Cũ (SLCu), Mới (SLMoi), SL.
        /// </summary>
        public static List<vtyeucau> FilterVtyeucauByQuantity(IEnumerable<vtyeucau> list, VtyeucauQuantityFilter filter)
        {
            if (filter == null || filter.IsEmpty)
                return list?.ToList() ?? new List<vtyeucau>();

            return list
                .Where(v =>
                    (filter.SLCuMin == null || (v.SLCu ?? 0) >= filter.SLCuMin) &&
                    (filter.SLCuMax == null || (v.SLCu ?? 0) <= filter.SLCuMax) &&
                    (filter.SLMoiMin == null || (v.SLMoi ?? 0) >= filter.SLMoiMin) &&
                    (filter.SLMoiMax == null || (v.SLMoi ?? 0) <= filter.SLMoiMax) &&
                    (filter.SLMin == null || (v.SL ?? 0) >= filter.SLMin) &&
                    (filter.SLMax == null || (v.SL ?? 0) <= filter.SLMax))
                .ToList();
        }

        /// <summary>
        /// Kiểm tra một dòng đã xử lý (có TonKho, SlThieu, SlDaXuat) có thỏa tiêu chí lọc số lượng hay không.
        /// Dùng sau khi đã build danh sách processed (có tính Thiếu, Đã xuất, Tồn kho) để lọc thêm theo các cột đó.
        /// </summary>
        public static bool PassesQuantityFilter(
            VtyeucauQuantityFilter filter,
            int? slCu, int? slMoi, int? sl,
            int tonKho, int slThieu, int? slDaXuat)
        {
            if (filter == null || filter.IsEmpty)
                return true;

            if (filter.SLCuMin != null && (slCu ?? 0) < filter.SLCuMin) return false;
            if (filter.SLCuMax != null && (slCu ?? 0) > filter.SLCuMax) return false;
            if (filter.SLMoiMin != null && (slMoi ?? 0) < filter.SLMoiMin) return false;
            if (filter.SLMoiMax != null && (slMoi ?? 0) > filter.SLMoiMax) return false;
            if (filter.SLMin != null && (sl ?? 0) < filter.SLMin) return false;
            if (filter.SLMax != null && (sl ?? 0) > filter.SLMax) return false;
            if (filter.TonKhoMin != null && tonKho < filter.TonKhoMin) return false;
            if (filter.TonKhoMax != null && tonKho > filter.TonKhoMax) return false;
            if (filter.SlThieuMin != null && slThieu < filter.SlThieuMin) return false;
            if (filter.SlThieuMax != null && slThieu > filter.SlThieuMax) return false;
            var daXuat = slDaXuat ?? 0;
            if (filter.SlDaXuatMin != null && daXuat < filter.SlDaXuatMin) return false;
            if (filter.SlDaXuatMax != null && daXuat > filter.SlDaXuatMax) return false;
            return true;
        }
    }
}
