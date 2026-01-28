using Microsoft.EntityFrameworkCore;
using Webkho_20241021.Models;

namespace Webkho_20241021.Services
{
    public class PhieuCodeService : IPhieuCodeService
    {
        private readonly ApplicationDbContext _context;

        public PhieuCodeService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Tạo mã phiếu nhập kho (PNK).
        /// Format:   hoặc MãNhânViênNK YYMMDD-01
        /// </summary>
        public string GenerateMaNhapKho(string? maDuan, string? maYeucau)
        {
            return GeneratePhieuCode(maDuan, maYeucau, "NK", null, false);
        }

        /// <summary>
        /// Tạo mã yêu cầu nhập kho (cùng format với phiếu nhập kho).
        /// Format: MãDựÁnNK YYMMDD-01 hoặc MãNhânViênNK YYMMDD-01
        /// </summary>
        public string GenerateMaYeucauNhapKho(string? maDuan, string? maNguoiDung)
        {
            return GeneratePhieuCode(maDuan, null, "NK", maNguoiDung, forYeucau: true);
        }

        public string GenerateMaXuatKho(string? maDuan, string? maYeucau)
        {
            return GeneratePhieuCode(maDuan, maYeucau, "XK", null, false);
        }

        public string GenerateMaMuaHang(string? maDuan, string? maYeucau)
        {
            return GeneratePhieuCode(maDuan, maYeucau, "MH", null, false);
        }

        /// <summary>
        /// Tạo mã phiếu/yêu cầu. Format: MãDựÁnNK YYMMDD-01 hoặc MãNhânViênNK YYMMDD-01
        /// </summary>
        /// <param name="maNguoiDungOverride">Khi tạo mã yêu cầu nhập kho (chưa có maYeucau), truyền mã NV.</param>
        /// <param name="forYeucau">true = check trùng trong yeucau.MaYeucau; false = trong bảng phiếu tương ứng.</param>
        private string GeneratePhieuCode(string? maDuan, string? maYeucau, string loaiPhieu, string? maNguoiDungOverride, bool forYeucau)
        {
            DateTime now = DateTime.Now;
            string datePart = now.ToString("yyMMdd");
            string prefix;

            bool isDuAn = !string.IsNullOrWhiteSpace(maDuan);

            if (isDuAn)
            {
                string maDuanPart = FormatLast6Digits(maDuan);
                prefix = $"{maDuanPart}{loaiPhieu}";
            }
            else
            {
                string maNguoiYeuCau = maNguoiDungOverride ?? GetMaNguoiYeuCau(maYeucau);
                maNguoiYeuCau = CleanStringForCode(maNguoiYeuCau);
                prefix = $"{maNguoiYeuCau}{loaiPhieu}";
            }

            string baseCode = $"{prefix} {datePart}";

            if (forYeucau)
                return EnsureUniqueCodeForYeucau(baseCode);
            return EnsureUniqueCode(baseCode, loaiPhieu);
        }

       
        private string GetMaNguoiYeuCau(string? maYeucau)
        {
            if (string.IsNullOrWhiteSpace(maYeucau))
            {
                return "CN"; // Cá nhân mặc định
            }

            var yeucau = _context.yeucau
                .FirstOrDefault(y => y.MaYeucau == maYeucau);

            // Lấy mã nhân viên từ YCMaNguoidung (ví dụ: PhuongMN)
            if (yeucau != null && !string.IsNullOrWhiteSpace(yeucau.YCMaNguoidung))
            {
                return yeucau.YCMaNguoidung;
            }

            return "CN"; // Mặc định nếu không tìm thấy
        }

      
        private string CleanStringForCode(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return "CN";
            }

            // Mã nhân viên đã có sẵn, chỉ cần loại bỏ khoảng trắng và ký tự đặc biệt, chỉ giữ chữ và số
            string cleaned = new string(input
                .Where(c => char.IsLetterOrDigit(c))
                .ToArray());

            // Giới hạn độ dài tối đa 20 ký tự
            if (cleaned.Length > 20)
            {
                cleaned = cleaned.Substring(0, 20);
            }

            return string.IsNullOrWhiteSpace(cleaned) ? "CN" : cleaned;
        }


        
        private string FormatLast6Digits(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return "000000";
            }

            var digits = new string(input.Where(char.IsDigit).ToArray());
            if (digits.Length > 6)
            {
                return digits.Substring(digits.Length - 6);
            }
            return digits.PadLeft(6, '0');
        }

       
        private string EnsureUniqueCode(string baseCode, string loaiPhieu)
        {
            // Kiểm tra mã base có tồn tại không
            if (!CodeExists(baseCode, loaiPhieu))
            {
                return baseCode;
            }

            // Tìm số suffix tiếp theo
            int suffixNumber = 1;
            while (true)
            {
                string candidate = $"{baseCode}-{suffixNumber:D2}";
                if (!CodeExists(candidate, loaiPhieu))
                {
                    return candidate;
                }
                suffixNumber++;
            }
        }

        
        private bool CodeExists(string code, string loaiPhieu)
        {
            return loaiPhieu switch
            {
                "NK" => _context.phieunhapkho.Any(p => p.MaNhapkho == code),
                "XK" => _context.phieuxuatkho.Any(p => p.MaXuatkho == code),
                "MH" => _context.phieumuahang.Any(p => p.MaMuahang == code),
                _ => false
            };
        }

        private string EnsureUniqueCodeForYeucau(string baseCode)
        {
            if (!_context.yeucau.Any(y => y.MaYeucau == baseCode))
                return baseCode;
            int suffixNumber = 1;
            while (true)
            {
                string candidate = $"{baseCode}-{suffixNumber:D2}";
                if (!_context.yeucau.Any(y => y.MaYeucau == candidate))
                    return candidate;
                suffixNumber++;
            }
        }
    }
}
