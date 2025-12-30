using Microsoft.EntityFrameworkCore;
using Webkho_20241021.Areas.TruongBPKho.Data;
using Webkho_20241021.Helpers;
using Webkho_20241021.Models;

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
            var phieuxuatkholist = _context.phieuxuatkho
                .Where(p => string.IsNullOrEmpty(p.MaYeucau) || !p.MaYeucau.StartsWith("NHAPKHO_"))
                .OrderByDescending(y => y.TrangThai == "Chờ lấy hàng")
                .ThenByDescending(y => y.TrangThai == "Đang chuẩn bị hàng")
                .ThenByDescending(y => y.NgayXuatkho)
                .AsQueryable();

            // Áp dụng tìm kiếm
            phieuxuatkholist = SearchHelper.ApplySearchPhieuxuatkho(phieuxuatkholist, search);

            return new Phieuxuatkhoviewmodel
            {
                Phieuxuatkho = phieuxuatkholist.ToList(),
                VTphieuxuatkho = _context.vtphieuxuatkho.ToList()
            };
        }

        /// <summary>
        /// Lấy danh sách phiếu nhập kho với tìm kiếm
        /// </summary>
        public Phieunhapkhoviewmodel GetDanhSachPhieunhapkho(string search = "")
        {
            var phieunhapkholist = _context.phieunhapkho
                .OrderByDescending(y => y.TrangThai == "Chờ nhập kho")
                .ThenByDescending(y => y.NgayNhapkho)
                .AsQueryable();

            // Áp dụng tìm kiếm
            phieunhapkholist = SearchHelper.ApplySearchPhieunhapkho(phieunhapkholist, search);

            return new Phieunhapkhoviewmodel
            {
                Phieunhapkho = phieunhapkholist.ToList(),
                VTphieunhapkho = _context.vtphieunhapkho.ToList(),
                Duans = _context.duans.ToList()
            };
        }

        /// <summary>
        /// Lấy danh sách phiếu mua hàng với tìm kiếm
        /// </summary>
        public Phieumuahangviewmodel GetDanhSachPhieumuahang(string search = "")
        {
            var phieumuahanglist = _context.phieumuahang
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
                VTphieumuahang = _context.vtphieumuahang.ToList()
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

