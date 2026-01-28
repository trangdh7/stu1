using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text;
using Webkho_20241021.Models;
using Webkho_20241021.Helpers;

namespace Webkho_20241021.Services
{
    public static class YeucauUpdateHelper
    {
        private static string NormalizeText(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return string.Empty;

            var normalized = input.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder();

            foreach (var ch in normalized)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    builder.Append(char.ToLowerInvariant(ch));
                }
            }

            return builder.ToString();
        }

        private static bool ContainsIgnoreAccent(string source, string value)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(value))
                return false;

            return NormalizeText(source).Contains(NormalizeText(value));
        }

        private static bool EqualsIgnoreAccent(string source, string value)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(value))
                return false;

            return NormalizeText(source) == NormalizeText(value);
        }

        public static string GetBaseRequestCode(string maYeucau)
        {
            if (string.IsNullOrWhiteSpace(maYeucau))
                return "";

            var parts = maYeucau.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            
            // Bỏ phần cuối cùng (tên người) nếu có
            if (parts.Length > 2)
            {
                return string.Join(" ", parts.Take(parts.Length - 1));
            }
            
            return maYeucau;
        }

        public static string GetBaseProductCode(string maSanpham)
        {
            if (string.IsNullOrWhiteSpace(maSanpham))
                return "";

            // Bỏ khoảng trắng, chuẩn hóa hoa thường
            var code = maSanpham.Trim().ToUpperInvariant();

            // Nếu có dấu '-' thì chỉ lấy phần TRƯỚC đoạn supplier / lô (thường là phần đầu tiên chứa mã gốc)
            // Ví dụ "DS03AATTASS02-TDS-20251215" → "DS03AATTASS02"
            var firstDashIndex = code.IndexOf('-');
            if (firstDashIndex > 0)
            {
                code = code.Substring(0, firstDashIndex);
            }

            // Bỏ ký tự không phải chữ/số để gom gần nhau hơn
            code = new string(code.Where(char.IsLetterOrDigit).ToArray());

            return code;
        }

        public static yeucau FindExistingRequest(ApplicationDbContext context, string newMaYeucau)
        {
            if (context == null || string.IsNullOrWhiteSpace(newMaYeucau))
                return null;

            string baseCode = GetBaseRequestCode(newMaYeucau);
            
            if (string.IsNullOrWhiteSpace(baseCode))
                return null;

            // Tìm tất cả yêu cầu và so sánh mã yêu cầu cơ bản
            var allRequests = context.yeucau.ToList();
            
            foreach (var request in allRequests)
            {
                if (string.IsNullOrWhiteSpace(request.MaYeucau))
                    continue;

                string existingBaseCode = GetBaseRequestCode(request.MaYeucau);
                
                if (string.Equals(baseCode, existingBaseCode, StringComparison.OrdinalIgnoreCase))
                {
                    return request;
                }
            }

            return null;
        }

        public static int TinhSoLuongDaCap(ApplicationDbContext context, string maYeucau, string maSanpham)
        {
            if (context == null || string.IsNullOrWhiteSpace(maYeucau) || string.IsNullOrWhiteSpace(maSanpham))
                return 0;

            // Lấy tất cả mã yêu cầu có cùng mã cơ bản
            string baseCode = GetBaseRequestCode(maYeucau);
            var allRelatedMaYeucau = context.yeucau
                .Where(y => !string.IsNullOrWhiteSpace(y.MaYeucau))
                .ToList()
                .Where(y => string.Equals(GetBaseRequestCode(y.MaYeucau), baseCode, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // ⭐ SỬA BUG: Loại trừ các yêu cầu đã bị từ chối
            var maYeucauBiTuChoi = allRelatedMaYeucau
                .Where(y => !string.IsNullOrEmpty(y.TrangThai) && 
                           y.TrangThai.Contains("Đã từ chối", StringComparison.OrdinalIgnoreCase))
                .Select(y => y.MaYeucau)
                .ToList();

            var allRelatedMaYeucauHopLe = allRelatedMaYeucau
                .Where(y => !maYeucauBiTuChoi.Contains(y.MaYeucau))
                .Select(y => y.MaYeucau)
                .ToList();

            if (!allRelatedMaYeucauHopLe.Any())
                return 0;

            // Lấy các phiếu xuất kho của các mã yêu cầu này đã/đang cấp (kể cả đang chuẩn bị hàng)
            // Mục tiêu: trừ đi cả lượng đã cấp trước đó và lượng đã cam kết xuất cho cùng dự án/yêu cầu.
            var trangThaiPhieuDuocTinh = new[]
            {
                "Đang chuẩn bị hàng",
                "Hoàn thành",
                "Đã xuất kho",
                "Đã lấy hàng"
            };

            // Materialize query trước, sau đó lọc case-insensitive trong memory
            // ⭐ SỬA BUG: Chỉ lấy phiếu xuất kho từ các yêu cầu hợp lệ (không bị từ chối)
            var phieuXuatKhoCuaVatTu = context.phieuxuatkho
                .Where(px => allRelatedMaYeucauHopLe.Contains(px.MaYeucau))
                .ToList()
                .Where(px => !string.IsNullOrEmpty(px.TrangThai) 
                             && trangThaiPhieuDuocTinh.Contains(px.TrangThai, StringComparer.OrdinalIgnoreCase))
                .Select(px => px.MaXuatkho)
                .Where(mx => !string.IsNullOrEmpty(mx))
                .ToList();

            if (!phieuXuatKhoCuaVatTu.Any())
                return 0;

            // Chuẩn hóa mã sản phẩm về mã cơ bản để gom các biến thể (theo kho/lô/ngày)
            string baseProductCode = GetBaseProductCode(maSanpham);

            // Lấy tất cả dòng vật tư thuộc các phiếu xuất này rồi lọc theo mã cơ bản
            var trangThaiVTDuocTinh = new[]
            {
                "Đang chuẩn bị hàng",
                "Chờ lấy hàng",
                "Đang giao",
                "Chờ người yêu cầu xác nhận",
                "Hoàn thành",
                "Đã xuất kho",
                "Đã lấy hàng"
            };

            // Materialize query trước, sau đó lọc case-insensitive trong memory
            var vtDaXuatList = context.vtphieuxuatkho
                .Where(vt => phieuXuatKhoCuaVatTu.Contains(vt.MaXuatkho)
                             && !string.IsNullOrWhiteSpace(vt.MaSanpham))
                .ToList()
                .Where(vt => !string.IsNullOrEmpty(vt.TrangThai)
                             && trangThaiVTDuocTinh.Contains(vt.TrangThai, StringComparer.OrdinalIgnoreCase)
                             && !maYeucauBiTuChoi.Contains(vt.MaYeucau ?? "")) // ⭐ Loại trừ phiếu xuất từ yêu cầu đã bị từ chối
                .ToList();

            int soLuongDaCap = vtDaXuatList
                .Where(vt => string.Equals(GetBaseProductCode(vt.MaSanpham), baseProductCode, StringComparison.OrdinalIgnoreCase))
                .Sum(vt => vt.SL ?? 0);

            return soLuongDaCap;
        }

        /// <summary>
        /// Tính tổng số lượng đã cấp cho một vật tư theo DỰ ÁN + mã yêu cầu cơ bản.
        /// Dùng cho logic cấp vật tư theo dự án, gom tất cả phiếu xuất thuộc các yêu cầu cùng base code.
        /// </summary>
        public static int TinhSoLuongDaCapTheoDuAn(
            ApplicationDbContext context,
            string maDuan,
            string baseMaYeucau,
            string maSanpham)
        {
            if (context == null ||
                string.IsNullOrWhiteSpace(maDuan) ||
                string.IsNullOrWhiteSpace(baseMaYeucau) ||
                string.IsNullOrWhiteSpace(maSanpham))
            {
                return 0;
            }

            // Lấy tất cả yêu cầu thuộc dự án này và có cùng mã yêu cầu cơ bản
            var allRelatedMaYeucau = context.yeucau
                .Where(y => y.YCMaDuan == maDuan && !string.IsNullOrWhiteSpace(y.MaYeucau))
                .ToList()
                .Where(y => string.Equals(
                    GetBaseRequestCode(y.MaYeucau),
                    baseMaYeucau,
                    StringComparison.OrdinalIgnoreCase))
                .Select(y => y.MaYeucau)
                .ToList();

            if (!allRelatedMaYeucau.Any())
            {
                return 0;
            }

            var trangThaiPhieuDuocTinh = new[]
            {
                "Đang chuẩn bị hàng",
                "Chờ lấy hàng",
                "Đang giao",
                "Chờ người yêu cầu xác nhận",
                "Hoàn thành",
                "Đã xuất kho",
                "Đã lấy hàng"
            };

            // Lấy các phiếu xuất kho thuộc các mã yêu cầu này và trong trạng thái cần tính
            var phieuXuatKhoCuaVatTu = context.phieuxuatkho
                .Where(px => allRelatedMaYeucau.Contains(px.MaYeucau))
                .ToList()
                .Where(px => !string.IsNullOrEmpty(px.TrangThai)
                             && trangThaiPhieuDuocTinh.Contains(px.TrangThai, StringComparer.OrdinalIgnoreCase))
                .Select(px => px.MaXuatkho)
                .Where(mx => !string.IsNullOrEmpty(mx))
                .ToList();

            if (!phieuXuatKhoCuaVatTu.Any())
            {
                return 0;
            }

            string baseProductCode = GetBaseProductCode(maSanpham);

            var trangThaiVTDuocTinh = new[]
            {
                "Đang chuẩn bị hàng",
                "Chờ lấy hàng",
                "Đang giao",
                "Chờ người yêu cầu xác nhận",
                "Hoàn thành",
                "Đã xuất kho",
                "Đã lấy hàng"
            };

            var vtDaXuatList = context.vtphieuxuatkho
                .Where(vt => phieuXuatKhoCuaVatTu.Contains(vt.MaXuatkho)
                             && !string.IsNullOrWhiteSpace(vt.MaSanpham))
                .ToList()
                .Where(vt => !string.IsNullOrEmpty(vt.TrangThai)
                             && trangThaiVTDuocTinh.Contains(vt.TrangThai, StringComparer.OrdinalIgnoreCase))
                .ToList();

            int soLuongDaCap = vtDaXuatList
                .Where(vt => string.Equals(GetBaseProductCode(vt.MaSanpham), baseProductCode, StringComparison.OrdinalIgnoreCase))
                .Sum(vt => vt.SL ?? 0);

            return soLuongDaCap;
        }

        /// <summary>
        /// Tính tổng nhu cầu cuối cùng theo mã yêu cầu cơ bản
        /// Với mỗi (Dự án + Mã yêu cầu cơ bản + Mã vật tư): chỉ có 1 số lượng chuẩn tại mọi thời điểm
        /// Khi có yêu cầu mới, số lượng mới là tổng nhu cầu cuối cùng (không cộng dồn)
        /// </summary>
        public static int TinhTongNhuCauTheoMaYeuCauCoBan(ApplicationDbContext context, string maYeucau, string maSanpham)
        {
            if (context == null || string.IsNullOrWhiteSpace(maYeucau) || string.IsNullOrWhiteSpace(maSanpham))
                return 0;

            // Lấy mã yêu cầu cơ bản
            string baseCode = GetBaseRequestCode(maYeucau);
            if (string.IsNullOrWhiteSpace(baseCode))
                return 0;

            // Lấy tất cả mã yêu cầu có cùng mã cơ bản
            var allRelatedMaYeucau = context.yeucau
                .Where(y => !string.IsNullOrWhiteSpace(y.MaYeucau))
                .ToList()
                .Where(y => string.Equals(GetBaseRequestCode(y.MaYeucau), baseCode, StringComparison.OrdinalIgnoreCase))
                .Select(y => y.MaYeucau)
                .ToList();

            if (!allRelatedMaYeucau.Any())
                return 0;

            // Lấy tất cả vật tư yêu cầu có cùng mã yêu cầu cơ bản và cùng mã sản phẩm
            var allVTYeucau = context.vtyeucau
                .Where(vt => allRelatedMaYeucau.Contains(vt.VTMaYeucau)
                             && string.Equals(vt.MaSanpham, maSanpham, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!allVTYeucau.Any())
                return 0;

            // Theo nghiệp vụ: chỉ có 1 số lượng chuẩn tại mọi thời điểm
            // Khi có yêu cầu mới, số lượng mới là tổng nhu cầu cuối cùng
            // Lấy số lượng mới nhất (SLMoi nếu có, nếu không thì SL) - ưu tiên SLMoi vì đó là số lượng được cập nhật mới nhất
            // Nếu có nhiều yêu cầu, lấy MAX để đảm bảo lấy số lượng cuối cùng
            int tongNhuCau = allVTYeucau
                .Select(vt => Math.Max(vt.SLMoi ?? 0, vt.SL ?? 0))
                .DefaultIfEmpty(0)
                .Max();

            return tongNhuCau;
        }

        /// <summary>
        /// Tính số lượng còn thiếu cần mua theo mã yêu cầu cơ bản
        /// Công thức: SoLuongConThieu = TongNhuCau - TongDaXuat - TongDaNhap
        /// Với mỗi (Dự án + Mã yêu cầu cơ bản + Mã vật tư): tính tổng xuất và số lượng thiếu
        /// </summary>
        public static int TinhSoLuongConThieuTheoMaYeuCauCoBan(ApplicationDbContext context, string maYeucau, string maSanpham)
        {
            if (context == null || string.IsNullOrWhiteSpace(maYeucau) || string.IsNullOrWhiteSpace(maSanpham))
                return 0;

            // 1. Tính tổng nhu cầu cuối cùng theo mã yêu cầu cơ bản
            int tongNhuCau = TinhTongNhuCauTheoMaYeuCauCoBan(context, maYeucau, maSanpham);
            if (tongNhuCau <= 0)
                return 0;

            // 2. Tính tổng đã xuất kho (tất cả phiếu xuất của các yêu cầu có cùng mã cơ bản)
            int tongDaXuat = TinhSoLuongDaCap(context, maYeucau, maSanpham);

            // 3. Tính tổng đã nhập kho (tất cả phiếu nhập của các yêu cầu có cùng mã cơ bản)
            string baseCode = GetBaseRequestCode(maYeucau);
            var allRelatedMaYeucau = context.yeucau
                .Where(y => !string.IsNullOrWhiteSpace(y.MaYeucau))
                .ToList()
                .Where(y => string.Equals(GetBaseRequestCode(y.MaYeucau), baseCode, StringComparison.OrdinalIgnoreCase))
                .Select(y => y.MaYeucau)
                .ToList();

            int tongDaNhap = 0;
            if (allRelatedMaYeucau.Any())
            {
                var trangThaiNhapDuocTinh = new[]
                {
                    "Đã nhập kho",
                    "Hoàn thành"
                };

                tongDaNhap = context.vtphieunhapkho
                    .Where(vt => allRelatedMaYeucau.Contains(vt.MaYeucau)
                                 && string.Equals(vt.MaSanpham, maSanpham, StringComparison.OrdinalIgnoreCase))
                    .ToList()
                    .Where(vt => !string.IsNullOrEmpty(vt.TrangThai)
                                 && trangThaiNhapDuocTinh.Contains(vt.TrangThai, StringComparer.OrdinalIgnoreCase))
                    .Sum(vt => vt.SL ?? 0);
            }

            // 4. Tính số lượng còn thiếu
            int conThieu = tongNhuCau - tongDaXuat - tongDaNhap;

            return conThieu > 0 ? conThieu : 0;
        }

        public static UpdateResult XuLyCapNhatYeuCau(
            ApplicationDbContext context,
            yeucau existingYeucau,
            string maSanpham,
            int slMoi,
            string maKho,
            out int slThieu)
        {
            slThieu = 0;

            if (context == null || existingYeucau == null || string.IsNullOrWhiteSpace(maSanpham))
            {
                return new UpdateResult { Success = false, Message = "Dữ liệu không hợp lệ" };
            }

            // ⭐ SỬA: Tính số lượng thiếu theo mã yêu cầu cơ bản
            // Với mỗi (Dự án + Mã yêu cầu cơ bản + Mã vật tư): chỉ có 1 số lượng chuẩn tại mọi thời điểm
            // Khi Phương gửi lại yêu cầu 11 cái → Tổng nhu cầu = 11 (không phải 1 + 11)
            // Số lượng thiếu = Tổng nhu cầu cuối cùng - Tổng đã xuất - Tổng đã nhập
            slThieu = TinhSoLuongConThieuTheoMaYeuCauCoBan(context, existingYeucau.MaYeucau, maSanpham);

            // Tìm vật tư yêu cầu hiện có
            var existingVTYeucau = context.vtyeucau
                .FirstOrDefault(vt => vt.VTMaYeucau == existingYeucau.MaYeucau 
                    && string.Equals(vt.MaSanpham, maSanpham, StringComparison.OrdinalIgnoreCase));

            if (existingVTYeucau == null)
            {
                return new UpdateResult { Success = false, Message = "Không tìm thấy vật tư yêu cầu" };
            }

            // Cập nhật số lượng mới
            existingVTYeucau.SLMoi = slMoi;
            existingVTYeucau.SL = slMoi;

            // Xử lý các trường hợp - chỉ cập nhật trạng thái, không tạo phiếu
            if (slThieu == 0)
            {
                // Trường hợp 1: Đã cấp đủ - không làm gì
                // Giữ nguyên trạng thái hiện tại nếu đã là "Đã xuất kho"
                if (existingVTYeucau.TrangThai != "Đã xuất kho" && existingVTYeucau.TrangThai != "Hoàn thành")
                {
                    existingVTYeucau.TrangThai = "Đã xuất kho";
                }
                context.vtyeucau.Update(existingVTYeucau);
                
                return new UpdateResult 
                { 
                    Success = true, 
                    Message = "Đã cấp đủ số lượng",
                    Action = UpdateAction.None
                };
            }
            else if (slThieu < 0)
            {
                // Trường hợp 2: Đã cấp thừa - cần trả lại kho
                // Đánh dấu để hệ thống xử lý tạo phiếu nhập trả sau
                existingVTYeucau.TrangThai = "Cần trả hàng";
                context.vtyeucau.Update(existingVTYeucau);
                
                return new UpdateResult 
                { 
                    Success = true, 
                    Message = $"Cần trả lại {Math.Abs(slThieu)} cái",
                    Action = UpdateAction.NhapTra
                };
            }
            else
            {
                // Trường hợp 3: Thiếu - cần xuất thêm hoặc mua
                // Đánh dấu để hệ thống xử lý sau
                // Nếu yêu cầu đã được duyệt, đánh dấu "Chờ xuất kho" để Xuliphieuyeucau xử lý
                if (existingYeucau.TrangThai == "Đã duyệt")
                {
                    existingVTYeucau.TrangThai = "Chờ xuất kho";
                }
                else
                {
                    // Giữ nguyên trạng thái hiện tại nếu chưa duyệt
                    if (string.IsNullOrEmpty(existingVTYeucau.TrangThai))
                    {
                        existingVTYeucau.TrangThai = "Chờ duyệt";
                    }
                }
                context.vtyeucau.Update(existingVTYeucau);
                
                return new UpdateResult 
                { 
                    Success = true, 
                    Message = $"Cần bổ sung {slThieu} cái",
                    Action = UpdateAction.XuatKho
                };
            }
        }

        public class UpdateResult
        {
            public bool Success { get; set; }
            public string Message { get; set; }
            public UpdateAction Action { get; set; }
            public int? SoLuongXuat { get; set; }
            public int? SoLuongMua { get; set; }
            public string MaPhieuNhapKho { get; set; }
            public string MaPhieuMuaHang { get; set; }
        }

        public enum UpdateAction
        {
            None,           // Không làm gì
            NhapTra,        // Nhập trả
            XuatKho,        // Chỉ xuất kho
            XuatKhoVaMua    // Xuất kho + mua hàng
        }

        /// <summary>
        /// Tính trạng thái yêu cầu dựa trên danh sách vật tư yêu cầu với ưu tiên:
        /// 1. "Chờ giám đốc duyệt" (nếu có vật tư chờ duyệt)
        /// 2. "Chờ quản lý dự án duyệt" (nếu có vật tư chờ quản lý dự án duyệt)
        /// 3. "Đang mua hàng" (nếu có bất kỳ vật tư nào đang mua hàng)
        /// 4. "Chờ xuất kho" (nếu có vật tư chờ xuất, và không có mua hàng)
        /// 5. "Đã xuất kho" (nếu tất cả đã xuất hoặc hoàn thành, không có chờ xuất/mua hàng)
        /// 6. "Đã nhập kho" (nếu có nhập kho nhưng chưa xuất)
        /// 7. "Đã từ chối" (nếu tất cả bị từ chối)
        /// 8. "Hoàn thành" (nếu tất cả hoàn thành, không có từ chối)
        /// </summary>
        public static string TinhTrangThaiYeuCau(List<vtyeucau> vtList)
        {
            if (vtList == null || !vtList.Any())
                return "Không có vật tư";

            // Lọc bỏ vt có SLMoi == 0, NHƯNG giữ lại các vật tư có trạng thái quan trọng
            // (Đã nhập kho, Đã xuất kho, Hoàn thành, Đã từ chối) để đảm bảo trạng thái yêu cầu được tính đúng
            vtList = vtList.Where(vt => 
                vt.SLMoi != 0 || 
                (!string.IsNullOrEmpty(vt.TrangThai) && (
                    ContainsIgnoreAccent(vt.TrangThai, "đã nhập kho") ||
                    ContainsIgnoreAccent(vt.TrangThai, "đã xuất kho") ||
                    EqualsIgnoreAccent(vt.TrangThai, "hoàn thành") ||
                    ContainsIgnoreAccent(vt.TrangThai, "đã từ chối")
                ))
            ).ToList();
            if (!vtList.Any())
                return "Không có vật tư hợp lệ";

            // Kiểm tra các flag với ưu tiên cao -> thấp
            bool hasChoGiamDoc = vtList.Any(v => 
                string.IsNullOrWhiteSpace(v.TrangThai) || 
                (!string.IsNullOrEmpty(v.TrangThai) && ContainsIgnoreAccent(v.TrangThai, "giám đốc duyệt")));

            bool hasChoQLDA = vtList.Any(v => 
                !string.IsNullOrEmpty(v.TrangThai) && 
                ContainsIgnoreAccent(v.TrangThai, "quản lý dự án duyệt"));

            bool hasDangMuaHang = vtList.Any(v => 
                !string.IsNullOrEmpty(v.TrangThai) && 
                ContainsIgnoreAccent(v.TrangThai, "đang mua hàng"));

            bool hasChoXuatKho = vtList.Any(v => 
                !string.IsNullOrEmpty(v.TrangThai) && 
                ContainsIgnoreAccent(v.TrangThai, "chờ xuất kho"));

            bool hasDaXuatKho = vtList.Any(v => 
                !string.IsNullOrEmpty(v.TrangThai) && 
                (ContainsIgnoreAccent(v.TrangThai, "đã xuất kho") || 
                 EqualsIgnoreAccent(v.TrangThai, "hoàn thành")));

            bool hasDaNhapKho = vtList.Any(v => 
                !string.IsNullOrEmpty(v.TrangThai) && 
                ContainsIgnoreAccent(v.TrangThai, "đã nhập kho"));

            bool hasRejected = vtList.All(v => 
                !string.IsNullOrEmpty(v.TrangThai) && 
                ContainsIgnoreAccent(v.TrangThai, "đã từ chối")); // Tất cả từ chối

            bool hasHoanThanh = vtList.All(v => 
                !string.IsNullOrEmpty(v.TrangThai) && 
                EqualsIgnoreAccent(v.TrangThai, "hoàn thành")); // Tất cả hoàn thành

            // Áp dụng ưu tiên theo thứ tự
            if (hasChoGiamDoc)
                return TrangThaiVatTu.ChoGiamDoc;
            
            if (hasChoQLDA)
                return TrangThaiVatTu.ChoQLDA;
            
            if (hasDangMuaHang)
                return TrangThaiVatTu.DangMuaHang; // Ưu tiên cao nhất sau chờ duyệt
            
            if (hasChoXuatKho)
                return "Chờ xuất kho";
            
            // Kiểm tra "Đã xuất kho": tất cả đã xuất hoặc hoàn thành, không có chờ xuất/mua hàng
            if (hasDaXuatKho && 
                vtList.All(v => 
                    (!string.IsNullOrEmpty(v.TrangThai) && 
                     (v.TrangThai.Contains("Đã xuất kho", StringComparison.OrdinalIgnoreCase) || 
                      v.TrangThai == "Hoàn thành" || 
                      v.TrangThai.Contains("Đã từ chối", StringComparison.OrdinalIgnoreCase)))))
            {
                return TrangThaiVatTu.DaXuatKho;
            }
            
            // Kiểm tra "Đã nhập kho": có nhập kho và tất cả đã nhập hoặc từ chối
            if (hasDaNhapKho && 
                vtList.All(v => 
                    (!string.IsNullOrEmpty(v.TrangThai) && 
                     (v.TrangThai.Contains("Đã nhập kho", StringComparison.OrdinalIgnoreCase) || 
                      v.TrangThai.Contains("Đã từ chối", StringComparison.OrdinalIgnoreCase)))))
            {
                return TrangThaiVatTu.DaNhapKho;
            }
            
            if (hasRejected)
                return TrangThaiVatTu.DaTuChoi;
            
            if (hasHoanThanh)
                return TrangThaiVatTu.HoanThanh;

            return "Chưa xác định";
        }

        public static string TinhTrangThaiNhapKhoTuChiTiet(List<vtphieunhapkho> vtNhapKhoList)
        {
            if (vtNhapKhoList == null || !vtNhapKhoList.Any())
                return "Không có vật tư";

            bool hasChoGiamDoc = vtNhapKhoList.Any(v =>
                string.IsNullOrWhiteSpace(v.TrangThai) ||
                ContainsIgnoreAccent(v.TrangThai, "giám đốc"));

            bool hasChoNhapKho = vtNhapKhoList.Any(v =>
                !string.IsNullOrEmpty(v.TrangThai) &&
                ContainsIgnoreAccent(v.TrangThai, "chờ nhập kho"));

            bool allDaNhapKho = vtNhapKhoList.All(v =>
                !string.IsNullOrEmpty(v.TrangThai) &&
                ContainsIgnoreAccent(v.TrangThai, "đã nhập kho"));

            if (hasChoGiamDoc)
                return TrangThaiVatTu.ChoGiamDoc;

            if (hasChoNhapKho)
                return TrangThaiVatTu.ChoNhapKho;

            if (allDaNhapKho)
                return TrangThaiVatTu.DaNhapKho;

            var firstTrangThai = vtNhapKhoList.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v.TrangThai));
            if (firstTrangThai != null)
                return firstTrangThai.TrangThai!;

            return "Chưa xác định";
        }

    
        public static void DongBoTrangThaiYeuCau(ApplicationDbContext context, string maYeucau)
        {
            if (context == null || string.IsNullOrWhiteSpace(maYeucau))
                return;

            var yeuCau = context.yeucau.FirstOrDefault(y => y.MaYeucau == maYeucau);
            if (yeuCau == null)
                return;

            // Lấy danh sách vật tư yêu cầu
            var vtYeuCauList = context.vtyeucau
                .Where(v => v.VTMaYeucau == maYeucau)
                .ToList();

            if (!vtYeuCauList.Any())
            {
                // Với "Yêu cầu nhập kho", vật tư nằm ở vtphieunhapkho (không nằm ở vtyeucau).
                // Trước đây nhận diện bằng prefix "NHAPKHO_", nhưng mã mới có thể là dạng "...NK_..." (vd: 251202NK_260128).
                bool isNhapKhoRequest =
                    (!string.IsNullOrEmpty(maYeucau) && maYeucau.StartsWith("NHAPKHO_", StringComparison.OrdinalIgnoreCase)) ||
                    (yeuCau.TenYeucau == "Yêu cầu nhập kho") ||
                    context.phieunhapkho.Any(p => p.MaYeucau == maYeucau);

                if (isNhapKhoRequest)
                {
                    var vtNhapKhoList = context.vtphieunhapkho
                        .Where(v => v.MaYeucau == maYeucau)
                        .ToList();

                    System.Diagnostics.Debug.WriteLine($"[YeucauUpdateHelper/DongBoTrangThaiYeuCau] maYeucau={maYeucau}, isNhapKhoRequest={isNhapKhoRequest}, vtNhapKhoList.Count={vtNhapKhoList.Count}");

                    if (vtNhapKhoList.Any())
                    {
                        yeuCau.TrangThai = TinhTrangThaiNhapKhoTuChiTiet(vtNhapKhoList);
                        context.yeucau.Update(yeuCau);
                    }
                }
                return;
            }

            // Đồng bộ trạng thái yeucau dựa trên vtyeucau
            yeuCau.TrangThai = TinhTrangThaiYeuCau(vtYeuCauList);
            context.yeucau.Update(yeuCau);
        }

        
        public static void DongBoTrangThaiVatTuYeuCau(ApplicationDbContext context, string maYeucau, string maSanpham, string trangThaiMoi)
        {
            if (context == null || string.IsNullOrWhiteSpace(maYeucau) || string.IsNullOrWhiteSpace(maSanpham))
                return;

            var vtYeuCau = context.vtyeucau
                .FirstOrDefault(v => v.VTMaYeucau == maYeucau && v.MaSanpham == maSanpham);

            if (vtYeuCau == null)
                return;

            // Cập nhật trạng thái vật tư
            vtYeuCau.TrangThai = trangThaiMoi;
            context.vtyeucau.Update(vtYeuCau);

            // Đồng bộ trạng thái yêu cầu
            DongBoTrangThaiYeuCau(context, maYeucau);
        }

       
        public static string XuLyLuongDuyetYeuCau(
            ApplicationDbContext context,
            yeucau yeuCau,
            string chucVu,
            string boPhan,
            bool yeuCauTuGiamDoc = false)
        {
            if (context == null || yeuCau == null)
                return null;

            string trangThaiMoi = null;
            bool isNhapKho = !string.IsNullOrEmpty(yeuCau.MaYeucau) &&
                            (yeuCau.MaYeucau.StartsWith("NHAPKHO_DUAN_") ||
                             yeuCau.MaYeucau.StartsWith("NHAPKHO_CANHAN_"));

            // Kiểm tra trạng thái hiện tại và xử lý theo luồng
            string trangThaiHienTai = yeuCau.TrangThai ?? "";

            // Xử lý khi Trưởng BP duyệt
            if (chucVu == "Trưởng BP" && trangThaiHienTai.Contains("Chờ Trưởng BP"))
            {
                if (yeuCauTuGiamDoc)
                {
                    // Bỏ qua bước QLDA, duyệt trực tiếp như Giám đốc
                    trangThaiMoi = "Đã duyệt";
                }
                else if (isNhapKho)
                {
                    if (yeuCau.MaYeucau.StartsWith("NHAPKHO_DUAN_"))
                    {
                        trangThaiMoi = TrangThaiVatTu.ChoQLDA;
                    }
                    else if (yeuCau.MaYeucau.StartsWith("NHAPKHO_CANHAN_"))
                    {
                        trangThaiMoi = TrangThaiVatTu.ChoGiamDoc;
                    }
                }
                else
                {
                    // Yêu cầu vật tư thông thường
                    var duan = context.duans.FirstOrDefault(d => d.MaDuan == yeuCau.YCMaDuan);
                    if (duan != null && !string.IsNullOrEmpty(yeuCau.YCMaDuan))
                    {
                        // Có dự án: Chờ quản lý dự án duyệt
                        trangThaiMoi = TrangThaiVatTu.ChoQLDA;
                    }
                    else
                    {
                        // Không có dự án: Chờ Giám đốc duyệt
                        trangThaiMoi = TrangThaiVatTu.ChoGiamDoc;
                    }
                }
            }
            // Xử lý khi QLDA duyệt
            else if (chucVu == "Quản lý dự án" && trangThaiHienTai.Contains("quản lý dự án duyệt", StringComparison.OrdinalIgnoreCase))
            {
                trangThaiMoi = TrangThaiVatTu.ChoGiamDoc;
            }
            // Xử lý khi Giám đốc duyệt
            else if (chucVu == "Giám đốc" && trangThaiHienTai.Contains("giám đốc duyệt", StringComparison.OrdinalIgnoreCase))
            {
                trangThaiMoi = "Đã duyệt";
            }

            // Cập nhật trạng thái yêu cầu
            if (!string.IsNullOrEmpty(trangThaiMoi))
            {
                yeuCau.TrangThai = trangThaiMoi;
                context.yeucau.Update(yeuCau);

                // Đồng bộ trạng thái cho tất cả vật tư yêu cầu
                var vtYeuCauList = context.vtyeucau
                    .Where(v => v.VTMaYeucau == yeuCau.MaYeucau)
                    .ToList();

                foreach (var vt in vtYeuCauList)
                {
                    // Chỉ cập nhật nếu vật tư chưa có trạng thái cuối cùng (Đã xuất kho, Hoàn thành, Đã từ chối)
                    if (string.IsNullOrEmpty(vt.TrangThai) ||
                        (!vt.TrangThai.Contains("Đã xuất kho", StringComparison.OrdinalIgnoreCase) &&
                         vt.TrangThai != "Hoàn thành" &&
                         !vt.TrangThai.Contains("Đã từ chối", StringComparison.OrdinalIgnoreCase)))
                    {
                        // Đồng bộ trạng thái vật tư theo trạng thái yêu cầu
                        if (trangThaiMoi == "Đã duyệt")
                        {
                            // Sau khi duyệt, vật tư sẽ được xử lý bởi Xuliphieuyeucau
                            // Giữ nguyên trạng thái hiện tại hoặc set "Chờ xuất kho" nếu chưa có
                            if (string.IsNullOrEmpty(vt.TrangThai))
                            {
                                vt.TrangThai = "Chờ xuất kho";
                            }
                        }
                        else
                        {
                            // Các trạng thái chờ duyệt: đồng bộ với yeucau
                            vt.TrangThai = trangThaiMoi;
                        }
                        context.vtyeucau.Update(vt);
                    }
                }
            }

            return trangThaiMoi;
        }

      
        public static string TaoTrangThaiBanDau(string chucVu, string boPhan, string maDuan)
        {
            if (chucVu == "Nhân viên")
            {
                // Quy tắc: Nhân viên → Chờ Trưởng BP-BP {bộ phận} duyệt
                return $"Chờ Trưởng BP-BP {boPhan} duyệt";
            }
            else if (chucVu == "Trưởng BP")
            {
                // Trưởng BP không thuộc dự án → Chờ Giám đốc duyệt
                // Trưởng BP thuộc dự án → Chờ quản lý dự án duyệt
                if (string.IsNullOrEmpty(maDuan))
                {
                    return TrangThaiVatTu.ChoGiamDoc;
                }
                else
                {
                    return TrangThaiVatTu.ChoQLDA;
                }
            }
            else if (chucVu == "Giám đốc")
            {
                return "Đã duyệt";
            }

            return "Chưa xác định";
        }
    }
}
