using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Webkho_20241021.Helpers
{
    /// <summary>
    /// Helper parse GhiChu để xuất Excel, tách phần "Lịch giao" thành Ngày có hàng dạng Đợt 1, Đợt 2...
    /// </summary>
    public static class GhiChuExportHelper
    {
        /// <summary>
        /// Parse GhiChu để tách phần "Lịch giao: X cái DD/MM/yyyy" thành text hiển thị Ngày có hàng (Đợt 1, Đợt 2...) và phần ghi chú còn lại.
        /// </summary>
        public static (string ngayCoHangDisplay, string ghiChuConLai) ParseGhiChuForExport(string? ghiChu, DateTime? ngayCoHangFallback)
        {
            var ghiChuConLai = ghiChu ?? "";
            if (string.IsNullOrWhiteSpace(ghiChu))
                return (ngayCoHangFallback?.ToString("dd/MM/yyyy") ?? "", "");
            var regex = new Regex(@"(\d+)\s*cái\s*(\d{1,2}/\d{1,2}/\d{4})");
            var dotList = new List<(int sl, string dateStr)>();
            foreach (Match m in regex.Matches(ghiChu))
            {
                if (m.Success && m.Groups.Count >= 3 && int.TryParse(m.Groups[1].Value, out var sl))
                    dotList.Add((sl, m.Groups[2].Value));
            }
            string ngayCoHangDisplay;
            if (dotList.Count > 0)
            {
                var lines = dotList.Select((d, i) => $"Đợt {i + 1}: {d.dateStr} ({d.sl} cái)");
                ngayCoHangDisplay = string.Join("\n", lines);
            }
            else
                ngayCoHangDisplay = ngayCoHangFallback?.ToString("dd/MM/yyyy") ?? "";
            if (ghiChu.Contains("Lịch giao:", StringComparison.OrdinalIgnoreCase))
            {
                var idx = ghiChu.IndexOf("Lịch giao:", StringComparison.OrdinalIgnoreCase);
                var after = ghiChu.Substring(idx);
                var endIdx = after.IndexOf(" | ", StringComparison.Ordinal);
                var lichGiaoLen = endIdx >= 0 ? idx + endIdx + 3 : ghiChu.Length;
                var before = (idx > 0 ? ghiChu.Substring(0, idx).Trim() : "");
                var afterPart = lichGiaoLen < ghiChu.Length ? ghiChu.Substring(lichGiaoLen).Trim() : "";
                ghiChuConLai = string.Join(" ", new[] { before, afterPart }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
            }
            return (ngayCoHangDisplay, ghiChuConLai);
        }
    }
}
