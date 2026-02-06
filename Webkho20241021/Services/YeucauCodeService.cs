using Microsoft.AspNetCore.Http;
using Webkho_20241021.Models;

namespace Webkho_20241021.Services
{
    public class YeucauCodeService : IYeucauCodeService
    {
        private readonly ApplicationDbContext _context;

        public YeucauCodeService(ApplicationDbContext context)
        {
            _context = context;
        }

        // ✅ 1 HÀM DUY NHẤT để gọi chung
        public string GenerateMaYeucauCommon(
            string? ycMaDuan,
            List<string>? maSanpham,
            IFormFileCollection? files,
            DateTime now)
        {
            ycMaDuan = ycMaDuan?.Trim();

            var (stPart, stPartFromFile, datePartFromFile, _) = ParseFromExcelFileName(files);

            // Fallback về MaSanpham chỉ khi KHÔNG có file
            bool hasFile = files != null && files.Count > 0;
            if (string.IsNullOrWhiteSpace(stPart) && !hasFile && maSanpham != null && maSanpham.Count > 0)
            {
                stPart = maSanpham.FirstOrDefault(m => !string.IsNullOrWhiteSpace(m));
            }

            if (string.IsNullOrWhiteSpace(stPart))
            {
                stPart = "VT";
            }

            // Chỉ bỏ khoảng trắng nếu KHÔNG lấy từ file (giữ nguyên khoảng trắng trong mã vật tư từ file)
            if (!stPartFromFile)
            {
                stPart = stPart.Replace(" ", "");
            }

            bool hasDuan = !string.IsNullOrWhiteSpace(ycMaDuan);

            string baseCode = BuildBaseCommon(hasDuan, ycMaDuan, stPart, datePartFromFile, now);

            // ✅ Đồng bộ suffix chung: -01, -02...
            return EnsureUnique(baseCode, SuffixStyle.DashTwoDigits);
        }

        public string GenerateMaYeucauNhapKho(string? maDuan, string maNguoiDung, DateTime now)
        {
            string datePart = now.ToString("yyMMdd");
            string duanPart = CleanStringForCode(maDuan);
            string userPart = CleanStringForCode(maNguoiDung);

            string baseCode = !string.IsNullOrWhiteSpace(maDuan)
                ? $"NK_DUAN_{duanPart}_{datePart}"
                : $"NK_CN_{userPart}_{datePart}";

            return EnsureUnique(baseCode, SuffixStyle.DashTwoDigits);
        }

        private static string CleanStringForCode(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return "";

            string cleaned = new string(input.Where(char.IsLetterOrDigit).ToArray());
            if (cleaned.Length > 20) cleaned = cleaned.Substring(0, 20);
            return cleaned;
        }

        private static string BuildBaseCommon(
            bool hasDuan,
            string? ycMaDuan,
            string stPart,
            string? datePartFromFile,
            DateTime now)
        {
            // Bỏ mã dự án và bỏ luôn "YC" / "YCCN"
            // Định dạng mới: ST YYMMDD
            string datePart = datePartFromFile ?? now.ToString("yyMMdd");
            return $"{stPart} {datePart}";
        }

        private enum SuffixStyle
        {
            ConcatNumber,   // ABC -> ABC1 -> ABC2
            DashTwoDigits   // ABC -> ABC-01 -> ABC-02
        }

        private string EnsureUnique(string baseCode, SuffixStyle suffixStyle)
        {
            if (!Exists(baseCode))
            {
                return baseCode;
            }

            int suffixNumber = 1;
            while (true)
            {
                string candidate = suffixStyle switch
                {
                    SuffixStyle.ConcatNumber => $"{baseCode}{suffixNumber}",
                    SuffixStyle.DashTwoDigits => $"{baseCode}-{suffixNumber:D2}",
                    _ => $"{baseCode}{suffixNumber}"
                };

                if (!Exists(candidate))
                {
                    return candidate;
                }

                suffixNumber++;
            }
        }

        private bool Exists(string maYeucau)
        {
            return _context.yeucau.Any(x => x.MaYeucau == maYeucau);
        }

        private static string FormatLast6Digits(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return "000000";
            }

            var digits = new string(raw.Where(char.IsDigit).ToArray());
            if (digits.Length > 6) return digits.Substring(digits.Length - 6);
            return digits.PadLeft(6, '0');
        }

        private static (string? stPart, bool stPartFromFile, string? datePartFromFile, bool hasExcelFile)
            ParseFromExcelFileName(IFormFileCollection? files)
        {
            if (files == null || files.Count == 0)
            {
                return (null, false, null, false);
            }

            var excelFile = files.FirstOrDefault(f =>
                !string.IsNullOrEmpty(f.FileName) &&
                (f.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) ||
                 f.FileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase)));

            if (excelFile == null || string.IsNullOrEmpty(excelFile.FileName))
            {
                return (null, false, null, false);
            }

            try
            {
                string fileNameWithoutExt = Path.GetFileNameWithoutExtension(excelFile.FileName);
                fileNameWithoutExt = fileNameWithoutExt.Replace('_', ' '); // giữ nguyên '-'
                var parts = fileNameWithoutExt.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length == 0)
                {
                    return (null, false, null, true);
                }

                string? datePartFromFile = null;
                int startIndex = -1;
                int endIndex = parts.Length;

                // Tìm cụm 6 số cuối (thường là ngày). Nếu user thêm -01 trong file (vd: 260128-01) thì bỏ hậu tố, lấy 260128
                for (int i = parts.Length - 1; i >= 0; i--)
                {
                    var part = parts[i];
                    if (part.Length == 6 && part.All(char.IsDigit))
                    {
                        datePartFromFile = part;
                        endIndex = i;
                        break;
                    }
                    if (part.Length == 9 && part[6] == '-' && part.Substring(0, 6).All(char.IsDigit) && part.Substring(7, 2).All(char.IsDigit))
                    {
                        datePartFromFile = part.Substring(0, 6);
                        endIndex = i;
                        break;
                    }
                }

                // Tìm vị trí bắt đầu ST: bỏ qua cụm ngày và cụm kiểu "251204YC"
                for (int i = 0; i < parts.Length; i++)
                {
                    var part = parts[i];

                    if (part.Length == 6 && part.All(char.IsDigit))
                        continue;

                    if (part.Length >= 6 && part.Length <= 10 &&
                        part.Substring(0, Math.Min(6, part.Length)).All(char.IsDigit) &&
                        part.EndsWith("YC", StringComparison.OrdinalIgnoreCase))
                        continue;

                    startIndex = i;
                    break;
                }

                string? stPart = null;
                if (startIndex >= 0 && startIndex < endIndex)
                {
                    stPart = string.Join(" ", parts.Skip(startIndex).Take(endIndex - startIndex));
                }
                else if (startIndex >= 0 && endIndex == parts.Length)
                {
                    stPart = string.Join(" ", parts.Skip(startIndex));
                }
                else if (parts.Length == 1)
                {
                    stPart = parts[0];
                }

                if (string.IsNullOrWhiteSpace(stPart))
                {
                    stPart = fileNameWithoutExt;
                }

                bool stPartFromFile = !string.IsNullOrWhiteSpace(stPart);
                return (stPart, stPartFromFile, datePartFromFile, true);
            }
            catch
            {
                return (null, false, null, true);
            }
        }
    }
}
