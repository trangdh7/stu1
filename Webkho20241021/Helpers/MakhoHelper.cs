using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Webkho_20241021.Models;

namespace Webkho_20241021.Helpers
{
    public static class MakhoHelper
    {
        private const string PlaceholderCode = "VT mới";
        private const int MaxLength = 50;
        private static readonly Regex InvalidCharsRegex = new(@"[^A-Z0-9]", RegexOptions.Compiled);

        private static string NormalizeComponent(string? value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return fallback;
            }

            var normalized = value.Trim().ToUpperInvariant();
            normalized = InvalidCharsRegex.Replace(normalized, string.Empty);
            return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
        }

        public static string BuildOfficialCode(string? maSanpham, string? hangSX, DateTime ngayNhap)
        {
            var ma = NormalizeComponent(maSanpham, "VT");
            var hang = NormalizeComponent(hangSX, "HSX");
            var code = $"{ma}-{hang}-{ngayNhap:yyyyMMdd}";
            return code.Length > MaxLength ? code[..MaxLength] : code;
        }

        public static string BuildUniqueOfficialCode(
            ApplicationDbContext context,
            string? maSanpham,
            string? hangSX,
            DateTime ngayNhap,
            IEnumerable<string>? reserved = null)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var baseCode = BuildOfficialCode(maSanpham, hangSX, ngayNhap);
            var suffix = 1;
            var candidate = baseCode;

            var reservedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (reserved != null)
            {
                foreach (var code in reserved)
                {
                    if (!string.IsNullOrWhiteSpace(code))
                    {
                        reservedSet.Add(code);
                    }
                }
            }

            bool Exists(string code) =>
                context.khotongs.Any(k => k.Makho == code) ||
                context.khotongs.Local.Any(k => k.Makho == code) ||
                reservedSet.Contains(code);

            while (Exists(candidate))
            {
                var suffixText = $"-{suffix:D2}";
                var maxBaseLength = MaxLength - suffixText.Length;
                var trimmedBase = baseCode.Length > maxBaseLength ? baseCode[..maxBaseLength] : baseCode;
                candidate = $"{trimmedBase}{suffixText}";
                suffix++;
            }

            reservedSet.Add(candidate);
            return candidate;
        }

        public static khotongs EnsurePlaceholderKho(ApplicationDbContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            var existing = context.khotongs.FirstOrDefault(k => k.Makho == PlaceholderCode);
            if (existing != null)
            {
                return existing;
            }

            var placeholder = new khotongs
            {
                Makho = PlaceholderCode,
                TenSanpham = "Vật tư mới",
                MaSanpham = "VT-MOI",
                HangSX = "Chưa xác định",
                NhaCC = "Chưa xác định",
                DuAn = "N/A",
                SL = 0,
                DonVi = "Cái",
                TrangThai = PlaceholderCode
            };

            context.khotongs.Add(placeholder);
            context.SaveChanges();
            return placeholder;
        }
    }
}

