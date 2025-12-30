namespace Webkho_20241021.Helpers
{
    /// <summary>
    /// Helper class để quản lý các trạng thái vật tư, tránh hard-code string
    /// </summary>
    public static class TrangThaiVatTu
    {
        // Trạng thái yêu cầu
        public const string ChoGiamDoc = "Chờ giám đốc duyệt";
        public const string ChoQLDA = "Chờ quản lý dự án duyệt";
        public const string DaTuChoi = "Đã từ chối";
        public const string DaDuyet = "Đã duyệt";
        public const string DangMuaHang = "Đang mua hàng";
        public const string DaXuatKho = "Đã xuất kho";
        public const string HoanThanh = "Hoàn thành";

        // Trạng thái phiếu xuất kho
        public const string ChoXacNhan = "Chờ xác nhận";
        public const string DangChuanBiHang = "Đang chuẩn bị hàng";
        public const string ChoNguoiYeuCauXacNhan = "Chờ người yêu cầu xác nhận";
        public const string DaXacNhanNhanHang = "Đã xác nhận nhận hàng";
        public const string ThieuHangDaTaoPhieuMua = "Thiếu hàng - Đã tạo phiếu mua";

        // Trạng thái phiếu nhập kho
        public const string ChoNhapKho = "Chờ nhập kho";
        public const string SanSangNhapKho = "Sẵn sàng nhập kho";
        public const string DaNhapKho = "Đã nhập kho";

        // Trạng thái phiếu mua hàng
        public const string DangChoBaoGia = "Đang chờ báo giá";
        public const string DaBaoGia = "Đã báo giá";
        public const string ChoThanhToan = "Chờ thanh toán";
        public const string DaThanhToan = "Đã thanh toán";

        // Trạng thái yêu cầu
        public const string ChoGiamDocDuyet = "Chờ Giám đốc duyệt";
        public const string ChoQLDADuyet = "Chờ quản lý dự án duyệt";
    }
}

