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
    }
}
