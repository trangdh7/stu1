using Microsoft.EntityFrameworkCore;
using Webkho_20241021.Areas.TruongBPKho.Data;
using Webkho_20241021.Helpers;
using Webkho_20241021.Models;
using Webkho_20241021.Services;

namespace Webkho_20241021.Services
{
    /// <summary>
    /// Service để xử lý logic nghiệp vụ liên quan đến phiếu (xuất, nhập, mua hàng)
    /// </summary>
    public class PhieuService
    {
        private readonly ApplicationDbContext _context;

        public PhieuService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Lấy danh sách phiếu xuất kho với tìm kiếm
        /// </summary>
        public Phieuxuatkhoviewmodel GetDanhSachPhieuxuatkho(string search = "")
        {
            var VTphieuxuatkholist = _context.vtphieuxuatkho.ToList();
            var allPhieuxuatkholist = _context.phieuxuatkho
                .Where(p => string.IsNullOrEmpty(p.MaYeucau) || !p.MaYeucau.StartsWith("NHAPKHO_"))
                .ToList();

            // Lọc bằng service dùng chung
            var validPhieuxuatkholist = DataFilterService.FilterPhieuxuatkho(allPhieuxuatkholist, VTphieuxuatkholist);

            var phieuxuatkholist = validPhieuxuatkholist
                .OrderByDescending(y => y.TrangThai == "Chờ lấy hàng")
                .ThenByDescending(y => y.TrangThai == "Đang chuẩn bị hàng")
                .ThenByDescending(y => y.NgayXuatkho)
                .AsQueryable();

            // Áp dụng tìm kiếm
            phieuxuatkholist = SearchHelper.ApplySearchPhieuxuatkho(phieuxuatkholist, search);

            return new Phieuxuatkhoviewmodel
            {
                Phieuxuatkho = phieuxuatkholist.ToList(),
                VTphieuxuatkho = VTphieuxuatkholist
            };
        }

        /// <summary>
        /// Lấy danh sách phiếu nhập kho với tìm kiếm
        /// </summary>
        public Phieunhapkhoviewmodel GetDanhSachPhieunhapkho(string search = "")
        {
            var VTphieunhapkholist = _context.vtphieunhapkho.ToList();
            var allPhieunhapkholist = _context.phieunhapkho.ToList();

            // Lọc bằng service dùng chung
            var validPhieunhapkholist = DataFilterService.FilterPhieunhapkho(allPhieunhapkholist, VTphieunhapkholist)
                .AsQueryable();

            var phieunhapkholist = validPhieunhapkholist
                .OrderByDescending(y => y.TrangThai == "Chờ nhập kho")
                .ThenByDescending(y => y.NgayNhapkho)
                .AsQueryable();

            // Áp dụng tìm kiếm
            phieunhapkholist = SearchHelper.ApplySearchPhieunhapkho(phieunhapkholist, search);

            return new Phieunhapkhoviewmodel
            {
                Phieunhapkho = phieunhapkholist.ToList(),
                VTphieunhapkho = VTphieunhapkholist,
                Duans = _context.duans.ToList()
            };
        }

        /// <summary>
        /// Lấy danh sách phiếu mua hàng với tìm kiếm
        /// </summary>
        public Phieumuahangviewmodel GetDanhSachPhieumuahang(string search = "")
        {
            var VTphieumuahanglist = _context.vtphieumuahang.ToList();
            var allPhieumuahanglist = _context.phieumuahang.ToList();

            // Lọc bằng service dùng chung
            var validPhieumuahanglist = DataFilterService.FilterPhieumuahang(allPhieumuahanglist, VTphieumuahanglist)
                .AsQueryable();

            var phieumuahanglist = validPhieumuahanglist
                .OrderByDescending(y => y.TrangThai == "Đang chờ báo giá")
                .ThenByDescending(y => y.NgayMuahang)
                .AsQueryable();

            // Áp dụng tìm kiếm trước
            phieumuahanglist = SearchHelper.ApplySearchPhieumuahang(phieumuahanglist, search);

            // Gán tên người yêu cầu sau khi đã filter
            var phieuList = phieumuahanglist.ToList();
            NguoiYeuCauHelper.GanTenNguoiYeuCauChoPhieuMuaHang(_context, phieuList);

            return new Phieumuahangviewmodel
            {
                Phieumuahang = phieuList,
                VTphieumuahang = VTphieumuahanglist
            };
        }

        /// <summary>
        /// Lấy tên người yêu cầu cho phiếu
        /// </summary>
        public string GetTenNguoiYeuCau(string? maYeucau, string? maNguoidung)
        {
            return NguoiYeuCauHelper.GetTenNguoiYeuCau(_context, maYeucau, maNguoidung);
        }
    }
}

