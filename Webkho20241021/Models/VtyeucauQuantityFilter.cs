namespace Webkho_20241021.Models
{
    /// <summary>
    /// Bộ lọc theo các cột số lượng: Cũ, Mới, SL, Tồn kho, Thiếu, Đã xuất.
    /// </summary>
    public class VtyeucauQuantityFilter
    {
        public int? SLCuMin { get; set; }
        public int? SLCuMax { get; set; }
        public int? SLMoiMin { get; set; }
        public int? SLMoiMax { get; set; }
        public int? SLMin { get; set; }
        public int? SLMax { get; set; }
        public int? TonKhoMin { get; set; }
        public int? TonKhoMax { get; set; }
        public int? SlThieuMin { get; set; }
        public int? SlThieuMax { get; set; }
        public int? SlDaXuatMin { get; set; }
        public int? SlDaXuatMax { get; set; }

        public bool IsEmpty =>
            SLCuMin == null && SLCuMax == null &&
            SLMoiMin == null && SLMoiMax == null &&
            SLMin == null && SLMax == null &&
            TonKhoMin == null && TonKhoMax == null &&
            SlThieuMin == null && SlThieuMax == null &&
            SlDaXuatMin == null && SlDaXuatMax == null;
    }
}
