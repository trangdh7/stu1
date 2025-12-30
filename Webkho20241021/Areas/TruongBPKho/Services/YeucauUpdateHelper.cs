using System;
using System.Collections.Generic;
using System.Linq;
using Webkho_20241021.Models;

namespace Webkho_20241021.Areas.TruongBPKho.Services
{
    public static class YeucauUpdateHelper
    {
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
                .Select(y => y.MaYeucau)
                .ToList();

            if (!allRelatedMaYeucau.Any())
                return 0;

            // Lấy các phiếu xuất kho của các mã yêu cầu này đã/đang cấp (kể cả đang chuẩn bị hàng)
            // Mục tiêu: trừ đi cả lượng đã cấp trước đó và lượng đã cam kết xuất cho cùng dự án/yêu cầu.
            var trangThaiPhieuDuocTinh = new[]
            {
                "Đang chuẩn bị hàng",
                "Chờ lấy hàng",
                "Đang giao",
                "Chờ người yêu cầu xác nhận",
                "Đã xác nhận nhận hàng",
                "Hoàn thành",
                "Đã xuất kho",
                "Đã lấy hàng"
            };

            // Materialize query trước, sau đó lọc case-insensitive trong memory
            var phieuXuatKhoCuaVatTu = context.phieuxuatkho
                .Where(px => allRelatedMaYeucau.Contains(px.MaYeucau))
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
                "Đã xác nhận nhận hàng",
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
                             && trangThaiVTDuocTinh.Contains(vt.TrangThai, StringComparer.OrdinalIgnoreCase))
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
                "Đã xác nhận nhận hàng",
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
                "Đã xác nhận nhận hàng",
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
                    "Đã xác nhận nhận hàng",
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
    }
}

