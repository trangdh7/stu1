using System.Linq;
using System.Text.RegularExpressions;

namespace Webkho_20241021.Helpers
{
    public static class PhieuHelper
    {
        private static readonly Regex DateSuffixRegex = new(@"(\d{8})$", RegexOptions.Compiled);
        private static readonly Regex SplitRegex = new(@"[^A-Z0-9]+", RegexOptions.Compiled);

        public static string ChuanHoaMaPhieu(string? ma)
        {
            if (string.IsNullOrWhiteSpace(ma))
            {
                return ma ?? string.Empty;
            }

            var normalized = ma.Trim().Replace(" ", string.Empty).ToUpperInvariant();
            var match = DateSuffixRegex.Match(normalized);
            if (!match.Success)
            {
                return normalized;
            }

            var ngay = match.Groups[1].Value;
            var prefix = normalized.Substring(0, normalized.Length - ngay.Length);
            var parts = SplitRegex.Split(prefix)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToArray();

            var prefixNormalized = parts.Length > 0 ? string.Join("-", parts) : prefix;
            return $"{prefixNormalized}-{ngay}";
        }
    }
}

