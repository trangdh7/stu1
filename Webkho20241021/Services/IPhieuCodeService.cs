namespace Webkho_20241021.Services
{
    public interface IPhieuCodeService
    {
        /// <summary>
        /// Tạo mã phiếu nhập kho.
        /// Format mới: MãDựÁnNK YYMMDD (không còn NHAPKHO_ / không dấu gạch)
        /// </summary>
        string GenerateMaNhapKho(string? maDuan, string? maYeucau);

        /// <summary>
        /// Tạo mã yêu cầu nhập kho (cùng format với phiếu nhập kho).
        /// Format mới: MãDựÁnNK YYMMDD (không còn NHAPKHO_ / không dấu gạch)
        /// </summary>
        string GenerateMaYeucauNhapKho(string? maDuan, string? maNguoiDung);

        string GenerateMaXuatKho(string? maDuan, string? maYeucau);

        string GenerateMaMuaHang(string? maDuan, string? maYeucau);

        /// <summary>
        /// Chuẩn hóa mã phiếu/yêu cầu: bỏ hậu tố -01, -02... do người dùng thêm trong file
        /// để hệ thống vẫn nhận biết cùng một yêu cầu (ví dụ: 260128-01 → 260128).
        /// </summary>
        string? NormalizePhieuCode(string? code);
    }
}
