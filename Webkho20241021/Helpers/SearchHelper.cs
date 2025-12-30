using System;
using System.Linq;

namespace Webkho_20241021.Helpers
{
    /// <summary>
    /// Helper class để xử lý tìm kiếm với logic chung, tránh lặp code
    /// </summary>
    public static class SearchHelper
    {
        /// <summary>
        /// Kiểm tra xem chuỗi có chứa keyword không (case-insensitive)
        /// </summary>
        public static bool ContainsIgnoreCase(string? source, string keyword)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(keyword))
                return false;

            return source.Contains(keyword, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Áp dụng tìm kiếm cho danh sách Yeucau
        /// </summary>
        public static IQueryable<Models.yeucau> ApplySearchYeucau(IQueryable<Models.yeucau> query, string search)
        {
            if (string.IsNullOrWhiteSpace(search))
                return query;

            var searchTerm = search.Trim();
            return query.Where(y =>
                ContainsIgnoreCase(y.MaYeucau, searchTerm) ||
                ContainsIgnoreCase(y.TenYeucau, searchTerm) ||
                ContainsIgnoreCase(y.NguoiYeucau, searchTerm) ||
                ContainsIgnoreCase(y.Bophan, searchTerm) ||
                ContainsIgnoreCase(y.YCMaNguoidung, searchTerm) ||
                ContainsIgnoreCase(y.YCMaDuan, searchTerm) ||
                ContainsIgnoreCase(y.TrangThai, searchTerm)
            );
        }

        /// <summary>
        /// Áp dụng tìm kiếm cho danh sách Phieuxuatkho
        /// </summary>
        public static IQueryable<Models.phieuxuatkho> ApplySearchPhieuxuatkho(IQueryable<Models.phieuxuatkho> query, string search)
        {
            if (string.IsNullOrWhiteSpace(search))
                return query;

            var searchTerm = search.Trim();
            return query.Where(p =>
                ContainsIgnoreCase(p.MaXuatkho, searchTerm) ||
                ContainsIgnoreCase(p.MaYeucau, searchTerm) ||
                ContainsIgnoreCase(p.MaDuan, searchTerm) ||
                ContainsIgnoreCase(p.MaNguoidung, searchTerm) ||
                ContainsIgnoreCase(p.TrangThai, searchTerm) ||
                ContainsIgnoreCase(p.GhiChu, searchTerm)
            );
        }

        /// <summary>
        /// Áp dụng tìm kiếm cho danh sách Phieunhapkho
        /// </summary>
        public static IQueryable<Models.phieunhapkho> ApplySearchPhieunhapkho(IQueryable<Models.phieunhapkho> query, string search)
        {
            if (string.IsNullOrWhiteSpace(search))
                return query;

            var searchTerm = search.Trim();
            return query.Where(p =>
                ContainsIgnoreCase(p.MaNhapkho, searchTerm) ||
                ContainsIgnoreCase(p.MaYeucau, searchTerm) ||
                ContainsIgnoreCase(p.MaDuan, searchTerm) ||
                ContainsIgnoreCase(p.MaNguoidung, searchTerm) ||
                ContainsIgnoreCase(p.TrangThai, searchTerm)
            );
        }

        /// <summary>
        /// Áp dụng tìm kiếm cho danh sách Phieumuahang
        /// </summary>
        public static IQueryable<Models.phieumuahang> ApplySearchPhieumuahang(IQueryable<Models.phieumuahang> query, string search)
        {
            if (string.IsNullOrWhiteSpace(search))
                return query;

            var searchTerm = search.Trim();
            return query.Where(p =>
                ContainsIgnoreCase(p.MaMuahang, searchTerm) ||
                ContainsIgnoreCase(p.MaYeucau, searchTerm) ||
                ContainsIgnoreCase(p.MaDuan, searchTerm) ||
                ContainsIgnoreCase(p.MaNguoidung, searchTerm) ||
                ContainsIgnoreCase(p.TenNguoiyeucau, searchTerm) ||
                ContainsIgnoreCase(p.TrangThai, searchTerm) ||
                ContainsIgnoreCase(p.GhiChu, searchTerm)
            );
        }
    }
}

