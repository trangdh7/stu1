namespace Webkho_20241021.Models
{
    /// <summary>
    /// Bộ lọc cho bảng tổng kho (khotongs).
    /// Hiện tại hỗ trợ lọc theo Hãng SX và Nhà cung cấp.
    /// Có thể mở rộng thêm các cột khác khi cần.
    /// </summary>
    public class KhotongFilter
    {
        public string? HangSX { get; set; }
        public string? NhaCC { get; set; }

        public bool IsEmpty =>
            string.IsNullOrWhiteSpace(HangSX) &&
            string.IsNullOrWhiteSpace(NhaCC);
    }
}

