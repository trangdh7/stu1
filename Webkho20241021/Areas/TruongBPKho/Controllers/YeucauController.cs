using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Text.Json;
using Webkho_20241021.Areas.TruongBPKho.Data;
using Webkho_20241021.Areas.TruongBPKho.Services;
using Webkho_20241021.Models;
using Webkho_20241021.Services;
using static Webkho_20241021.Services.YeucauUpdateHelper;
using Webkho_20241021.Helpers;
using OfficeOpenXml;
using Microsoft.Extensions.DependencyInjection;

namespace Webkho_20241021.Areas.TruongBPKho.Controllers
{
    [Area("TruongBPKho")]
    [Authorize(Roles = "Trưởng BP-BP kho")]
    public class YeucauController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly YeucauService _yeucauService;
        private readonly ThongbaoService _thongbaoService;
        private readonly PhieuService _phieuService;
        private readonly EmailService _emailService;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IYeucauCodeService _yeucauCodeService;
        private readonly IPhieuCodeService _phieuCodeService;

        public YeucauController(ApplicationDbContext context, EmailService emailService, IServiceScopeFactory serviceScopeFactory, IYeucauCodeService yeucauCodeService, IPhieuCodeService phieuCodeService)
        {
            _context = context;
            _yeucauService = new YeucauService(context);
            _thongbaoService = new ThongbaoService(context);
            _emailService = emailService;
            _serviceScopeFactory = serviceScopeFactory;
            _phieuService = new PhieuService(context);
            _yeucauCodeService = yeucauCodeService;
            _phieuCodeService = phieuCodeService;
        }

        private sealed class SlThucXuatItem
        {
            public int id { get; set; }
            public int slThucXuat { get; set; }
        }

        private static Dictionary<int, int> ParseSlThucXuatJson(string? json)
        {
            var map = new Dictionary<int, int>();
            if (string.IsNullOrWhiteSpace(json))
            {
                return map;
            }

            try
            {
                var items = JsonSerializer.Deserialize<List<SlThucXuatItem>>(json);
                if (items == null) return map;
                foreach (var it in items)
                {
                    if (it == null) continue;
                    if (it.id <= 0) continue;
                    map[it.id] = it.slThucXuat;
                }
            }
            catch
            {
                // ignore parse errors -> fallback default
            }

            return map;
        }

        private void SendRejectionEmailAsync(string maYeucau, string ghiChu = "")
        {
            // Tạo scope mới để tránh lỗi DbContext thread-safe
            _ = Task.Run(async () =>
            {
                try
                {
                    using (var scope = _serviceScopeFactory.CreateScope())
                    {
                        var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                        System.Diagnostics.Debug.WriteLine($"[TruongBPKho] Bắt đầu gửi email từ chối cho {maYeucau}");
                        await emailService.SendNotificationToRequesterOnRejectionAsync(maYeucau, ghiChu);
                        System.Diagnostics.Debug.WriteLine($"[TruongBPKho] Đã gửi email từ chối cho {maYeucau}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TruongBPKho] Lỗi gửi email từ chối cho {maYeucau}: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[TruongBPKho] Stack trace: {ex.StackTrace}");
                }
            });
        }

        public IActionResult Yeucau(string search = "")
        {
            var userRole = HttpContext.Session.GetString("Chucvu");
            var model = _yeucauService.GetDanhSachYeucau(userRole, search);
            ViewBag.Search = search;
            return View(model);
        }

        public IActionResult Phieuxuatkho(string search = "")
        {
            var model = _phieuService.GetDanhSachPhieuxuatkho(search);
            ViewBag.Search = search;
            return View(model);
        }

        public IActionResult Phieunhapkho(string search = "")
        {
            var model = _phieuService.GetDanhSachPhieunhapkho(search);
            ViewBag.Search = search;
            return View(model);
        }

        public IActionResult Phieumuahang(string search = "")
        {
            var model = _phieuService.GetDanhSachPhieumuahang(search);
            ViewBag.Search = search;
            return View(model);
        }

        [HttpGet]
        public IActionResult GetDulieuThongbao()
        {
            var chucVu = HttpContext.Session.GetString("Chucvu");
            var boPhan = HttpContext.Session.GetString("Bophan");
            var maNv = HttpContext.Session.GetString("MaNguoidung");

            var data = _thongbaoService.GetThongBao(chucVu, boPhan, maNv);
            return Json(data);
        }

        [HttpGet]
        public IActionResult GetDulieuThongbaolayout()
        {
            // Reuse the same logic as GetDulieuThongbao for the layout badge
            return GetDulieuThongbao();
        }

        [HttpGet]
        public IActionResult GetVTYeucau(string MaYeucau)
        {
            if (string.IsNullOrWhiteSpace(MaYeucau))
            {
                return Json(new List<object>());
            }

            // Nếu yêu cầu đã có dòng chi tiết trong vtyeucau
            // thì luôn ưu tiên hiển thị danh sách đó (yêu cầu vật tư gốc),
            // kể cả khi đã phát sinh phiếu nhập kho.
            bool hasVatTuYeuCau = _context.vtyeucau
                .Any(v => v.VTMaYeucau == MaYeucau);

            if (hasVatTuYeuCau)
            {
                var vatTuList = _context.vtyeucau
                    .Where(v => v.VTMaYeucau == MaYeucau)
                    .ToList();
                return Json(vatTuList);
            }

            // Chỉ khi KHÔNG có vtyeucau, mới coi là yêu cầu nhập kho đặc biệt
            // và lấy dữ liệu từ vtphieunhapkho/phieunhapkho.
            bool isNhapKhoRequest =
                (!string.IsNullOrEmpty(MaYeucau) && MaYeucau.StartsWith("NHAPKHO_", StringComparison.OrdinalIgnoreCase)) ||
                _context.phieunhapkho.Any(p => p.MaYeucau == MaYeucau) ||
                _context.yeucau.Any(y => y.MaYeucau == MaYeucau && y.TenYeucau == "Yêu cầu nhập kho");

            if (isNhapKhoRequest)
            {
                var vatTuList = (from vtnk in _context.vtphieunhapkho
                                 join pnk in _context.phieunhapkho on vtnk.MaNhapkho equals pnk.MaNhapkho
                                 where pnk.MaYeucau == MaYeucau
                                 select new
                                 {
                                     ID = vtnk.ID,
                                     VTMaYeucau = MaYeucau,
                                     TenSanpham = vtnk.TenSanpham,
                                     MaSanpham = vtnk.MaSanpham,
                                     YCMakho = vtnk.Makho,
                                     HangSX = vtnk.HangSX,
                                     NhaCC = vtnk.NhaCC,
                                     SLCu = (int?)null,
                                     SLMoi = vtnk.SL,
                                     SL = vtnk.SL,
                                     DonVi = vtnk.DonVi,
                                     NgayCanHang = (DateTime?)null,
                                     NgayNhapkho = vtnk.NgayNhapkho,
                                     NgayBaohanh = vtnk.NgayBaohanh,
                                     ThoiGianBH = vtnk.ThoiGianBH,
                                     NgayDuyet = (DateTime?)null,
                                     TrangThai = vtnk.TrangThai,
                                     GhiChu = (string?)null
                                 }).ToList();
                return Json(vatTuList);
            }

            // Không có chi tiết ở cả 2 nơi
            return Json(new List<object>());
        }

        [HttpPost]
        public IActionResult XuLyVatTuYeucau(string MaYeucau, string MaSanpham, string action)
        {
            try
            {
                var vatTu = _context.vtyeucau
                    .FirstOrDefault(v => v.VTMaYeucau == MaYeucau && v.MaSanpham == MaSanpham);

                if (vatTu == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy vật tư." });
                }

                if (action == "approve")
                {
                    vatTu.TrangThai = TrangThaiVatTu.ChoGiamDoc;
                }
                else if (action == "reject")
                {
                    vatTu.TrangThai = TrangThaiVatTu.DaTuChoi;
                }

                _context.vtyeucau.Update(vatTu);
                _context.SaveChanges();

                // Gửi email thông báo từ chối nếu trạng thái yêu cầu là "Đã từ chối"
                if (action == "reject")
                {
                    var yeucauAfterReject = _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
                    if (yeucauAfterReject != null && yeucauAfterReject.TrangThai == "Đã từ chối")
                    {
                        SendRejectionEmailAsync(MaYeucau, "");
                    }
                }

                return Json(new { success = true, message = action == "approve" ? "Đã duyệt vật tư thành công." : "Đã từ chối vật tư." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        [HttpPost]
        public IActionResult XuLyVatTuYeucauWithCheckbox(string MaYeucau, string VatTuData)
        {
            try
            {
                if (string.IsNullOrEmpty(VatTuData))
                {
                    return Json(new { success = false, message = "Không có dữ liệu vật tư." });
                }

                // Parse JSON data
                var vatTuList = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(VatTuData);
                if (vatTuList == null || !vatTuList.Any())
                {
                    return Json(new { success = false, message = "Dữ liệu vật tư không hợp lệ." });
                }

                // Kiểm tra xem yêu cầu có mã dự án không
                var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
                bool hasMaDuan = yeucau != null && !string.IsNullOrWhiteSpace(yeucau.YCMaDuan);

                // Xác định trạng thái tiếp theo dựa trên việc có mã dự án hay không
                string nextTrangThaiVT = hasMaDuan ? "Chờ quản lý dự án duyệt" : "Chờ giám đốc duyệt";
                string nextTrangThaiYC = hasMaDuan ? "Chờ quản lý dự án duyệt" : "Chờ Giám đốc duyệt";

                int processedCount = 0;
                int skippedCount = 0;

                foreach (var item in vatTuList)
                {
                    var maSanpham = item.ContainsKey("MaSanpham") ? item["MaSanpham"]?.ToString() : null;
                    var isApproved = item.ContainsKey("IsApproved") &&
                                    item["IsApproved"] is System.Text.Json.JsonElement jsonElement &&
                                    jsonElement.GetBoolean();
                    var ghiChu = item.ContainsKey("GhiChu") ? item["GhiChu"]?.ToString() : null;

                    if (string.IsNullOrEmpty(maSanpham))
                    {
                        skippedCount++;
                        continue;
                    }

                    var vatTu = _context.vtyeucau
                        .FirstOrDefault(v => v.VTMaYeucau == MaYeucau && v.MaSanpham == maSanpham);

                    if (vatTu == null)
                    {
                        skippedCount++;
                        continue;
                    }

                    // Helper function để kiểm tra xem vật tư có đang chờ Trưởng BP kho duyệt không
                    Func<string, bool> isAwaitingTruongBPStatus = status =>
                    {
                        if (string.IsNullOrWhiteSpace(status))
                        {
                            return true;
                        }
                        var normalized = status.Trim();
                        return normalized.Equals("Chờ Trưởng BP-BP kho duyệt", StringComparison.OrdinalIgnoreCase)
                            || normalized.StartsWith("Chờ Trưởng BP", StringComparison.OrdinalIgnoreCase)
                            || normalized.Contains("chờ trưởng bp", StringComparison.OrdinalIgnoreCase);
                    };

                    // Helper function để kiểm tra xem vật tư đã được duyệt chưa
                    Func<string, bool> isAlreadyApproved = status =>
                    {
                        if (string.IsNullOrWhiteSpace(status))
                        {
                            return false;
                        }
                        var normalized = status.Trim();
                        return normalized == "Đã duyệt" ||
                               normalized == "Đang mua hàng" ||
                               normalized == "Đã xuất kho" ||
                               normalized == "Đã nhận hàng" ||
                               normalized == "Chờ giám đốc duyệt" ||
                               normalized == "Chờ quản lý dự án duyệt";
                    };

                    // Helper function để kiểm tra xem vật tư đã bị từ chối chưa
                    Func<string, bool> isAlreadyRejected = status =>
                    {
                        if (string.IsNullOrWhiteSpace(status))
                        {
                            return false;
                        }
                        return status.Contains("Đã từ chối", StringComparison.OrdinalIgnoreCase);
                    };

                    // Kiểm tra số lượng yêu cầu - nếu bằng 0 thì đặt trạng thái "Hoàn thành" và bỏ qua
                    int soLuongYeuCau = vatTu.SL ?? 0;
                    if (soLuongYeuCau == 0)
                    {
                        // Nếu số lượng = 0, không cần mua hàng, đặt trạng thái "Hoàn thành"
                        vatTu.NgayDuyet = DateTime.Now;
                        vatTu.TrangThai = "Hoàn thành";
                        vatTu.GhiChu = ghiChu;
                        _context.vtyeucau.Update(vatTu);
                        processedCount++;
                        continue;
                    }

                    // Chỉ xử lý các vật tư đang chờ Trưởng BP kho duyệt và chưa được duyệt/từ chối
                    if (!isAwaitingTruongBPStatus(vatTu.TrangThai) ||
                        isAlreadyApproved(vatTu.TrangThai) ||
                        isAlreadyRejected(vatTu.TrangThai))
                    {
                        skippedCount++;
                        continue;
                    }

                    if (isApproved)
                    {
                        // Duyệt vật tư
                        vatTu.NgayDuyet = DateTime.Now;
                        vatTu.TrangThai = nextTrangThaiVT;
                        vatTu.GhiChu = ghiChu; // Lưu ghi chú khi duyệt
                    }
                    else
                    {
                        // Từ chối vật tư
                        vatTu.NgayDuyet = DateTime.Now;
                        vatTu.TrangThai = TrangThaiVatTu.DaTuChoi;
                        vatTu.GhiChu = ghiChu; // Lưu ghi chú khi từ chối
                    }

                    _context.vtyeucau.Update(vatTu);
                    processedCount++;
                }

                // Lưu thông tin người duyệt vào bảng yeucau (ghi đè mã người duyệt mới nhất)
                if (yeucau != null && processedCount > 0)
                {
                    var maNguoiDuyet = HttpContext.Session.GetString("MaNguoidung");
                    if (!string.IsNullOrWhiteSpace(maNguoiDuyet))
                    {
                        yeucau.NguoiDuyet = maNguoiDuyet;
                        yeucau.NgayDuyet = DateTime.Now;
                        _context.yeucau.Update(yeucau);
                    }
                }

                _context.SaveChanges();

                // Cập nhật trạng thái yêu cầu nếu tất cả vật tư đã được duyệt
                if (yeucau != null)
                {
                    var chucVu = HttpContext.Session.GetString("Chucvu");
                    var boPhan = HttpContext.Session.GetString("Bophan");

                    // Kiểm tra xem tất cả vật tư đã được trưởng phòng duyệt chưa
                    var allVatTu = _context.vtyeucau.Where(v => v.VTMaYeucau == MaYeucau).ToList();
                    var allApprovedByTruongBP = allVatTu.All(v => v.TrangThai == nextTrangThaiVT ||
                                                                   v.TrangThai == "Đã duyệt" ||
                                                                   v.TrangThai == "Đang mua hàng" ||
                                                                   v.TrangThai == "Đã xuất kho" ||
                                                                   v.TrangThai == "Đã nhận hàng" ||
                                                                   v.TrangThai == "Chờ giám đốc duyệt" ||
                                                                   v.TrangThai == "Chờ quản lý dự án duyệt" ||
                                                                   (!string.IsNullOrEmpty(v.TrangThai) && v.TrangThai.Contains("Đã từ chối")));

                    if (allApprovedByTruongBP && chucVu == "Trưởng BP" && boPhan == "BP kho")
                    {
                        yeucau.TrangThai = nextTrangThaiYC;
                        _context.yeucau.Update(yeucau);
                        _context.SaveChanges();

                        // Sau khi Trưởng BP kho duyệt xong bằng checkbox:
                        // - Gửi mail cho người yêu cầu
                        // - Gửi mail cho bước tiếp theo (QLDA nếu có dự án, hoặc Giám đốc nếu không có dự án)
                        try
                        {
                            System.Diagnostics.Debug.WriteLine($"[TruongBPKho/XuLyVatTuYeucauWithCheckbox] ===== BẮT ĐẦU GỬI EMAIL SAU KHI DUYỆT VẬT TƯ =====");
                            System.Diagnostics.Debug.WriteLine($"[TruongBPKho/XuLyVatTuYeucauWithCheckbox] MaYeucau = {yeucau.MaYeucau}");
                            System.Diagnostics.Debug.WriteLine($"[TruongBPKho/XuLyVatTuYeucauWithCheckbox] TrangThai = {yeucau.TrangThai}");
                            System.Diagnostics.Debug.WriteLine($"[TruongBPKho/XuLyVatTuYeucauWithCheckbox] YCMaDuan = {yeucau.YCMaDuan ?? "(null)"}");
                            System.Diagnostics.Debug.WriteLine($"[TruongBPKho/XuLyVatTuYeucauWithCheckbox] hasMaDuan = {hasMaDuan}");
                            System.Diagnostics.Debug.WriteLine($"[TruongBPKho/XuLyVatTuYeucauWithCheckbox] NguoiYeucau = {yeucau.NguoiYeucau ?? "(null)"}");

                            if (!string.IsNullOrWhiteSpace(yeucau.NguoiYeucau))
                            {
                                var trangThaiThongBao = hasMaDuan
                                    ? "Đã được Trưởng BP-BP kho duyệt - chuyển quản lý dự án"
                                    : "Đã được Trưởng BP-BP kho duyệt - chờ Giám đốc duyệt";

                                System.Diagnostics.Debug.WriteLine($"[TruongBPKho/XuLyVatTuYeucauWithCheckbox] Gửi email cho người yêu cầu: {yeucau.NguoiYeucau}");
                                _ = _emailService.SendNotificationToEmployeeAsync(
                                    yeucau.MaYeucau,
                                    yeucau.NguoiYeucau,
                                    trangThaiThongBao
                                );
                                System.Diagnostics.Debug.WriteLine($"[TruongBPKho/XuLyVatTuYeucauWithCheckbox] ✅ Đã gọi SendNotificationToEmployeeAsync");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"[TruongBPKho/XuLyVatTuYeucauWithCheckbox] ⚠️ NguoiYeucau rỗng, bỏ qua gửi email cho người yêu cầu");
                            }

                            if (hasMaDuan && !string.IsNullOrWhiteSpace(yeucau.YCMaDuan))
                            {
                                System.Diagnostics.Debug.WriteLine($"[TruongBPKho/XuLyVatTuYeucauWithCheckbox] Gửi email cho QLDA. MaYeucau = {yeucau.MaYeucau}, YCMaDuan = {yeucau.YCMaDuan}");
                                _ = _emailService.SendNotificationToProjectManagerAsync(
                                    yeucau.MaYeucau,
                                    yeucau.YCMaDuan
                                );
                                System.Diagnostics.Debug.WriteLine($"[TruongBPKho/XuLyVatTuYeucauWithCheckbox] ✅ Đã gọi SendNotificationToProjectManagerAsync");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"[TruongBPKho/XuLyVatTuYeucauWithCheckbox] Gửi email cho Giám đốc. MaYeucau = {yeucau.MaYeucau}");
                                _ = _emailService.SendNotificationToDirectorAsync(yeucau.MaYeucau);
                                System.Diagnostics.Debug.WriteLine($"[TruongBPKho/XuLyVatTuYeucauWithCheckbox] ✅ Đã gọi SendNotificationToDirectorAsync");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[TruongBPKho/XuLyVatTuYeucauWithCheckbox] ❌ Lỗi gửi email sau duyệt: {ex.Message}");
                            System.Diagnostics.Debug.WriteLine($"[TruongBPKho/XuLyVatTuYeucauWithCheckbox] Stack trace: {ex.StackTrace}");
                            if (ex.InnerException != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"[TruongBPKho/XuLyVatTuYeucauWithCheckbox] Inner exception: {ex.InnerException.Message}");
                            }
                        }
                    }
                }

                string message = $"Đã xử lý {processedCount} vật tư thành công.";
                if (skippedCount > 0)
                {
                    message += $" ({skippedCount} vật tư đã được xử lý trước đó hoặc không ở trạng thái chờ duyệt)";
                }

                return Json(new { success = true, message = message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        [HttpPost]
        public IActionResult XuLyTatCaVatTuYeucau(string MaYeucau, string action)
        {
            try
            {
                var vatTuList = _context.vtyeucau
                    .Where(v => v.VTMaYeucau == MaYeucau).ToList();

                if (!vatTuList.Any())
                {
                    return Json(new { success = false, message = "Không tìm thấy vật tư nào." });
                }

                foreach (var vatTu in vatTuList)
                {
                    if (action == "approve")
                    {
                        vatTu.TrangThai = TrangThaiVatTu.ChoGiamDoc;
                    }
                    else if (action == "reject")
                    {
                        vatTu.TrangThai = TrangThaiVatTu.DaTuChoi;
                    }
                    _context.vtyeucau.Update(vatTu);
                }

                // Lưu thay đổi trạng thái vật tư
                _context.SaveChanges();

                // Cập nhật trạng thái yêu cầu chính nếu Trưởng BP duyệt tất cả vật tư
                if (action == "approve")
                {
                    var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
                    if (yeucau != null)
                    {
                        var chucVu = HttpContext.Session.GetString("Chucvu");
                        var boPhan = HttpContext.Session.GetString("Bophan");

                        // Kiểm tra xem tất cả vật tư đã được duyệt chưa (không có vật tư nào bị từ chối)
                        var allApproved = vatTuList.All(v => v.TrangThai == "Chờ giám đốc duyệt");

                        if (allApproved && chucVu == "Trưởng BP" && boPhan == "BP kho")
                        {
                            // Kiểm tra trạng thái hiện tại của yêu cầu
                            if (yeucau.TrangThai == "Chờ Trưởng BP-BP kho duyệt")
                            {
                                // Kiểm tra xem có phải là yêu cầu nhập kho không
                                bool isNhapKho = !string.IsNullOrEmpty(yeucau.MaYeucau) &&
                                                (yeucau.MaYeucau.StartsWith("NHAPKHO_DUAN_") ||
                                                 yeucau.MaYeucau.StartsWith("NHAPKHO_CANHAN_"));

                                if (isNhapKho)
                                {
                                    // Nếu là nhập kho
                                    if (yeucau.MaYeucau.StartsWith("NHAPKHO_DUAN_"))
                                    {
                                        // Dự án: Chờ quản lý dự án duyệt
                                        yeucau.TrangThai = "Chờ quản lý dự án duyệt";
                                    }
                                    else if (yeucau.MaYeucau.StartsWith("NHAPKHO_CANHAN_"))
                                    {
                                        // Cá nhân: Chờ Giám đốc duyệt
                                        yeucau.TrangThai = "Chờ Giám đốc duyệt";
                                    }
                                }
                                else
                                {
                                    // Nếu là yêu cầu vật tư thông thường
                                    var duan = _context.duans.FirstOrDefault(d => d.MaDuan == yeucau.YCMaDuan);
                                    if (duan != null)
                                    {
                                        // Có dự án: Chờ quản lý dự án duyệt
                                        yeucau.TrangThai = "Chờ quản lý dự án duyệt";
                                    }
                                    else
                                    {
                                        // Cá nhân: Chờ Giám đốc duyệt
                                        yeucau.TrangThai = "Chờ Giám đốc duyệt";
                                    }
                                }
                            }
                            else
                            {
                                // Giữ logic cũ cho các trường hợp khác
                                yeucau.TrangThai = "Chờ Giám đốc duyệt";
                            }
                            _context.yeucau.Update(yeucau);
                            _context.SaveChanges();
                        }
                        else if (action == "reject")
                        {
                            // Nếu có bất kỳ vật tư nào bị từ chối, yêu cầu chính cũng bị từ chối
                            yeucau.TrangThai = "Đã từ chối";
                            _context.yeucau.Update(yeucau);
                            _context.SaveChanges();
                        }
                    }
                }

                return Json(new { success = true, message = action == "approve" ? $"Đã duyệt {vatTuList.Count} vật tư thành công." : $"Đã từ chối {vatTuList.Count} vật tư." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        [HttpGet]
        public IActionResult GetVTPhieuxuatkho(string MaXuatkho)
        {
            var PhieuxuatkhoList = _context.vtphieuxuatkho
                                 .Where(v => v.MaXuatkho == MaXuatkho).ToList();

            // Lấy thông tin phiếu xuất kho để lấy tên người yêu cầu
            var phieuxuatkho = _context.phieuxuatkho
                .FirstOrDefault(p => p.MaXuatkho == MaXuatkho);

            string tenNguoiYeuCau = "";
            if (phieuxuatkho != null)
            {
                // Lấy từ yeucau nếu có
                if (!string.IsNullOrEmpty(phieuxuatkho.MaYeucau))
                {
                    var yeucau = _context.yeucau
                        .FirstOrDefault(y => y.MaYeucau == phieuxuatkho.MaYeucau);
                    if (yeucau != null)
                    {
                        tenNguoiYeuCau = yeucau.NguoiYeucau ?? "";
                    }
                }

                // Nếu không có từ yeucau, lấy từ nguoidungs
                if (string.IsNullOrEmpty(tenNguoiYeuCau) && !string.IsNullOrEmpty(phieuxuatkho.MaNguoidung))
                {
                    var nguoidung = _context.nguoidungs
                        .FirstOrDefault(n => n.MaNguoidung == phieuxuatkho.MaNguoidung);
                    if (nguoidung != null)
                    {
                        tenNguoiYeuCau = nguoidung.TenNguoidung ?? "";
                    }
                }
            }

            // Tính SL gốc của phiếu (căn cứ) và còn lại theo base code:
            // - SL gốc: max SL của các dòng vtphieuxuatkho (phiếu gốc giữ SL gốc)
            // - Đã xuất: sum SL của các dòng đã xuất kho/hoàn thành
            var maYeucau = phieuxuatkho?.MaYeucau ?? "";
            Dictionary<string, int> slGocByBase = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, int> slDaXuatByBase = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            if (!string.IsNullOrEmpty(maYeucau))
            {
                // SL gốc = max SL của tất cả các dòng phiếu xuất cùng mã yêu cầu theo base code
                slGocByBase = _context.vtphieuxuatkho
                    .Where(v => v.MaYeucau == maYeucau)
                    .ToList()
                    .GroupBy(v => YeucauUpdateHelper.GetBaseProductCode(v.MaSanpham ?? "") ?? "")
                    .ToDictionary(
                        g => g.Key ?? "",
                        g => g.Max(x => x.SL ?? 0),
                        StringComparer.OrdinalIgnoreCase
                    );

                // Đã xuất = sum SL của các dòng đã xuất kho/hoàn thành (tất cả phiếu cùng mã yêu cầu)
                slDaXuatByBase = _context.vtphieuxuatkho
                    .Where(v => v.MaYeucau == maYeucau && (v.TrangThai == "Đã xuất kho" || v.TrangThai == "Hoàn thành"))
                    .ToList()
                    .GroupBy(v => YeucauUpdateHelper.GetBaseProductCode(v.MaSanpham ?? "") ?? "")
                    .ToDictionary(
                        g => g.Key ?? "",
                        g => g.Sum(x => x.SL ?? 0),
                        StringComparer.OrdinalIgnoreCase
                    );
            }

            // Lấy tồn kho hiện tại theo mã sản phẩm từ bảng khotongs
            var maSanphamList = PhieuxuatkhoList
                .Where(v => !string.IsNullOrWhiteSpace(v.MaSanpham))
                .Select(v => v.MaSanpham!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var tonKhoByCode = _context.khotongs
                .Where(k => k.MaSanpham != null && maSanphamList.Contains(k.MaSanpham))
                .GroupBy(k => k.MaSanpham!)
                .ToDictionary(
                    g => g.Key,
                    g => g.Sum(x => x.SL ?? 0),
                    StringComparer.OrdinalIgnoreCase
                );

            var items = PhieuxuatkhoList.Select(v =>
            {
                var baseCode = YeucauUpdateHelper.GetBaseProductCode(v.MaSanpham ?? "") ?? "";
                int slGoc = slGocByBase.TryGetValue(baseCode, out var goc) ? goc : (v.SL ?? 0);
                int slDaXuat = slDaXuatByBase.TryGetValue(baseCode, out var issued) ? issued : 0;
                int slConLai = Math.Max(0, slGoc - slDaXuat);
                int tonKho = 0;
                if (!string.IsNullOrWhiteSpace(v.MaSanpham) &&
                    tonKhoByCode.TryGetValue(v.MaSanpham, out var ton))
                {
                    tonKho = ton;
                }
                return new
                {
                    v.ID,
                    v.MaXuatkho,
                    v.MaYeucau,
                    v.TenSanpham,
                    v.MaSanpham,
                    v.Makho,
                    v.HangSX,
                    v.NhaCC,
                    v.SL,
                    v.DonVi,
                    v.DonGia,
                    v.ThanhTien,
                    v.NgayNhapkho,
                    v.NgayBaohanh,
                    v.ThoiGianBH,
                    v.TrangThai,
                    v.LoaiCapPhat,
                    // slYeuCau: dùng để HIỂN THỊ cột SL (SL gốc của phiếu)
                    slYeuCau = slGoc,
                    slConLai,
                    TonKho = tonKho
                };
            }).ToList();

            return Json(new
            {
                items = items,
                tenNguoiYeuCau = tenNguoiYeuCau,
                maYeucau = maYeucau
            });
        }

        [HttpGet]
        public IActionResult GetVTPhieunhapkho(string MaNhapkho)
        {
            var PhieunhapkhoList = _context.vtphieunhapkho
                                 .Where(v => v.MaNhapkho == MaNhapkho).ToList();

            // Lấy thông tin phiếu nhập kho để lấy tên người yêu cầu
            var phieunhapkho = _context.phieunhapkho
                .FirstOrDefault(p => p.MaNhapkho == MaNhapkho);

            string tenNguoiYeuCau = "";
            if (phieunhapkho != null)
            {
                // Lấy từ yeucau nếu có
                if (!string.IsNullOrEmpty(phieunhapkho.MaYeucau))
                {
                    var yeucau = _context.yeucau
                        .FirstOrDefault(y => y.MaYeucau == phieunhapkho.MaYeucau);
                    if (yeucau != null)
                    {
                        tenNguoiYeuCau = yeucau.NguoiYeucau ?? "";
                    }
                }

                // Nếu không có từ yeucau, lấy từ nguoidungs
                if (string.IsNullOrEmpty(tenNguoiYeuCau) && !string.IsNullOrEmpty(phieunhapkho.MaNguoidung))
                {
                    var nguoidung = _context.nguoidungs
                        .FirstOrDefault(n => n.MaNguoidung == phieunhapkho.MaNguoidung);
                    if (nguoidung != null)
                    {
                        tenNguoiYeuCau = nguoidung.TenNguoidung ?? "";
                    }
                }
            }

            return Json(new
            {
                items = PhieunhapkhoList,
                tenNguoiYeuCau = tenNguoiYeuCau
            });
        }

        [HttpGet]
        public IActionResult XemPhieunhapkho(string MaNhapkho)
        {
            if (string.IsNullOrEmpty(MaNhapkho))
            {
                return NotFound();
            }

            var phieunhapkho = _context.phieunhapkho
                .FirstOrDefault(p => p.MaNhapkho == MaNhapkho);

            if (phieunhapkho == null)
            {
                return NotFound();
            }

            var vtphieunhapkho = _context.vtphieunhapkho
                .Where(v => v.MaNhapkho == MaNhapkho)
                .ToList();

            var duan = !string.IsNullOrEmpty(phieunhapkho.MaDuan)
                ? _context.duans.FirstOrDefault(d => d.MaDuan == phieunhapkho.MaDuan)
                : null;

            // Lấy thông tin người yêu cầu từ yeucau
            var yeucau = !string.IsNullOrEmpty(phieunhapkho.MaYeucau)
                ? _context.yeucau.FirstOrDefault(y => y.MaYeucau == phieunhapkho.MaYeucau)
                : null;

            // Lấy thông tin người dùng từ nguoidungs (nếu có)
            var nguoidung = !string.IsNullOrEmpty(phieunhapkho.MaNguoidung)
                ? _context.nguoidungs.FirstOrDefault(n => n.MaNguoidung == phieunhapkho.MaNguoidung)
                : null;

            // Ưu tiên lấy từ yeucau, nếu không có thì lấy từ nguoidungs
            string tenNguoiGiaoHang = "";
            string maNguoiGiaoHang = "";
            if (yeucau != null)
            {
                tenNguoiGiaoHang = yeucau.NguoiYeucau ?? "";
                maNguoiGiaoHang = yeucau.YCMaNguoidung ?? "";
            }
            else if (nguoidung != null)
            {
                tenNguoiGiaoHang = nguoidung.TenNguoidung ?? "";
                maNguoiGiaoHang = nguoidung.MaNguoidung ?? "";
            }

            // Lấy thông tin nhà cung cấp từ vật tư đầu tiên (nếu có)
            var nhaCC = vtphieunhapkho.FirstOrDefault()?.NhaCC;

            // Kiểm tra xem có phiếu mua hàng nào liên kết với phiếu nhập kho này không
            // (thông qua MaYeucau)
            var phieumuahang = !string.IsNullOrEmpty(phieunhapkho.MaYeucau)
                ? _context.phieumuahang.FirstOrDefault(p => p.MaYeucau == phieunhapkho.MaYeucau)
                : null;

            // Kiểm tra xem đã có phiếu xuất kho nào cho yêu cầu này chưa
            bool hasPhieuxuatkho = false;
            if (!string.IsNullOrEmpty(phieunhapkho.MaYeucau))
            {
                hasPhieuxuatkho = _context.phieuxuatkho
                    .Any(px => px.MaYeucau == phieunhapkho.MaYeucau &&
                               px.TrangThai != "Đã hủy");
            }

            var model = new Phieunhapkhoviewmodel
            {
                Phieunhapkho = new List<phieunhapkho> { phieunhapkho },
                VTphieunhapkho = vtphieunhapkho,
                Duans = duan != null ? new List<duans> { duan } : new List<duans>()
            };

            ViewBag.NhaCC = nhaCC;
            ViewBag.NguoiDung = nguoidung;
            ViewBag.Duan = duan;
            ViewBag.TenNguoiGiaoHang = tenNguoiGiaoHang;
            ViewBag.Phieumuahang = phieumuahang;
            ViewBag.MaNguoiGiaoHang = maNguoiGiaoHang;
            ViewBag.Yeucau = yeucau;
            ViewBag.HasPhieuxuatkho = hasPhieuxuatkho;

            return View(model);
        }

        [HttpGet]
        public IActionResult KiemTraGiaTien(string MaNhapkho)
        {
            if (string.IsNullOrEmpty(MaNhapkho))
            {
                return Json(new { error = "Mã nhập kho không hợp lệ" });
            }

            var phieunhapkho = _context.phieunhapkho.FirstOrDefault(p => p.MaNhapkho == MaNhapkho);
            if (phieunhapkho == null)
            {
                return Json(new { error = "Không tìm thấy phiếu nhập kho" });
            }

            // Lấy vật tư trong phiếu nhập kho
            var vtphieunhapkho = _context.vtphieunhapkho
                .Where(v => v.MaNhapkho == MaNhapkho)
                .Select(v => new
                {
                    MaSanpham = v.MaSanpham,
                    TenSanpham = v.TenSanpham,
                    DonGia = v.DonGia,
                    ThanhTien = v.ThanhTien,
                    SL = v.SL
                })
                .ToList();

            // Lấy vật tư từ phiếu mua hàng (nếu có)
            var vtphieumuahang = new List<object>();
            if (!string.IsNullOrEmpty(phieunhapkho.MaYeucau))
            {
                var phieumuahang = _context.phieumuahang
                    .FirstOrDefault(p => p.MaYeucau == phieunhapkho.MaYeucau);

                if (phieumuahang != null)
                {
                    vtphieumuahang = _context.vtphieumuahang
                        .Where(v => v.MaMuahang == phieumuahang.MaMuahang)
                        .Select(v => new
                        {
                            MaSanpham = v.MaSanpham,
                            TenSanpham = v.TenSanpham,
                            DonGia = v.DonGia,
                            ThanhTien = v.ThanhTien,
                            SL = v.SL,
                            TrangThai = v.TrangThai
                        })
                        .ToList<object>();
                }
            }

            return Json(new
            {
                MaNhapkho = MaNhapkho,
                MaYeucau = phieunhapkho.MaYeucau,
                VTPhieunhapkho = vtphieunhapkho,
                VTPhieumuahang = vtphieumuahang
            });
        }

        [HttpGet]
        public IActionResult GetVTPhieumuahang(string MaMuahang)
        {
            var PhieumuahangList = _context.vtphieumuahang
                                 .Where(v => v.MaMuahang == MaMuahang).ToList();

            // Lấy thông tin phiếu mua hàng để lấy tên người yêu cầu
            var phieumuahang = _context.phieumuahang
                .FirstOrDefault(p => p.MaMuahang == MaMuahang);

            string tenNguoiYeuCau = "";
            if (phieumuahang != null)
            {
                // Lấy từ TenNguoiyeucau nếu có
                if (!string.IsNullOrEmpty(phieumuahang.TenNguoiyeucau))
                {
                    tenNguoiYeuCau = phieumuahang.TenNguoiyeucau;
                }
                else
                {
                    // Sử dụng helper để lấy tên người yêu cầu
                    tenNguoiYeuCau = _phieuService.GetTenNguoiYeuCau(phieumuahang.MaYeucau, phieumuahang.MaNguoidung);
                }
            }


            var itemsWithConThieu = PhieumuahangList.Select(vt => new
            {
                vt.ID,
                vt.MaMuahang,
                vt.MaYeucau,
                vt.TenSanpham,
                vt.MaSanpham,
                vt.Makho,
                vt.HangSX,
                vt.NhaCC,
                // ⭐ SỬA: Sử dụng số lượng từ phiếu mua hàng (vt.SL)
                sl = vt.SL ?? 0,
                vt.DonVi,
                vt.DonGia,
                vt.ThanhTien,
                vt.TrangThai,
                vt.GhiChu,
                vt.NgayNhapkho
            }).ToList();

            return Json(new
            {
                items = itemsWithConThieu,
                tenNguoiYeuCau = tenNguoiYeuCau
            });
        }
        private int TinhSoLuongConThieuTheoDuAn(string maDuan, string maSanpham)
        {
            if (string.IsNullOrWhiteSpace(maDuan) || string.IsNullOrWhiteSpace(maSanpham))
                return 0;

            // 1. Lấy tất cả yêu cầu thuộc dự án này
            var yeuCauCuaDuan = _context.yeucau
                .Where(yc => yc.YCMaDuan == maDuan && !string.IsNullOrWhiteSpace(yc.MaYeucau))
                .ToList();

            if (!yeuCauCuaDuan.Any())
                return 0;

            // 2. Lấy tất cả vtyeucau của các yêu cầu này có cùng MaSanpham
            var vtYeuCauList = _context.vtyeucau
                .Where(vt => yeuCauCuaDuan.Select(yc => yc.MaYeucau).Contains(vt.VTMaYeucau)
                             && vt.MaSanpham == maSanpham)
                .Join(_context.yeucau,
                      vt => vt.VTMaYeucau,
                      yc => yc.MaYeucau,
                      (vt, yc) => new { vt, yc })
                .ToList();

            if (!vtYeuCauList.Any())
                return 0;

            // 3. TỔNG NHU CẦU = SL của yêu cầu MỚI NHẤT (theo ngày yêu cầu)
            int tongNhuCau = vtYeuCauList
                .OrderByDescending(x => x.yc.NgayYeucau ?? DateTime.MinValue)
                .First()
                .vt.SL ?? 0;

            // 4. TỔNG ĐÃ XUẤT = Sum tất cả đã xuất cho dự án này (bất kể yêu cầu nào)
            int tongDaXuat = _context.vtphieuxuatkho
                .Join(_context.phieuxuatkho,
                      vt => vt.MaXuatkho,
                      px => px.MaXuatkho,
                      (vt, px) => new { vt, px })
                .Where(x => x.px.MaDuan == maDuan &&
                            x.vt.MaSanpham == maSanpham &&
                            x.vt.TrangThai != "Đã hủy" &&
                            (x.vt.TrangThai == "Đã xuất kho" ||
                             x.vt.TrangThai == "Hoàn thành"))  // chỉ tính đã giao thực tế
                .Sum(x => x.vt.SL ?? 0);

            int conThieu = tongNhuCau - tongDaXuat;
            return conThieu > 0 ? conThieu : 0;
        }

        public IActionResult ThemYeucau()
        {
            var Duanlist = _context.duans
                          .Select(n => new { n.MaDuan, n.TrangThai })
                          .ToList();

            ViewBag.Duanlist = Duanlist;
            return View();
        }

        public IActionResult ThemPhieunhapkho()
        {
            var maNv = HttpContext.Session.GetString("MaNguoidung");

            var Yeucaulist = _context.yeucau
                          .Select(n => new { n.MaYeucau, n.TrangThai })
                          .ToList();

            ViewBag.Yeucaulist = Yeucaulist;

            var Phieumuahanglist = _context.phieumuahang
                                 .Select(n => new { n.MaYeucau, n.TrangThai })
                                 .ToList();
            ViewBag.Phieumuahanglist = Phieumuahanglist;

            var allowedProjectCodes = _context.phieuxuatkho
                .Where(px => px.MaNguoidung == maNv && !string.IsNullOrEmpty(px.MaDuan))
                .Select(px => px.MaDuan)
                .Distinct()
                .ToList();

            var Duanlist = allowedProjectCodes.Count > 0
                ? _context.duans
                    .Where(y => allowedProjectCodes.Contains(y.MaDuan))
                    .Select(y => (object)new { y.MaDuan, y.TrangThai })
                    .ToList()
                : new List<object>();

            ViewBag.Duanlist = Duanlist;
            ViewBag.MaNguoidung = maNv;
            return View();
        }

        [HttpGet]
        public IActionResult GetCurrentUser()
        {
            var maNv = HttpContext.Session.GetString("MaNguoidung");
            return Json(new { maNguoidung = maNv });
        }

        [HttpGet]
        public IActionResult GetDataKhoCaNhan()
        {
            var maNv = HttpContext.Session.GetString("MaNguoidung");
            if (string.IsNullOrEmpty(maNv))
            {
                return BadRequest("Không tìm thấy mã nhân viên");
            }

            var khoCaNhanItems = _context.khonguoidungs
                .Where(k => k.NDMaNguoidung == maNv
                         && (k.TrangThai == "Đang mượn" || k.TrangThai == "Đang sử dụng")
                         && k.SL > 0)
                .Select(k => new
                {
                    tenSanpham = k.TenSanpham,
                    maSanpham = k.MaSanpham,
                    makho = k.NDMakho,
                    hangSX = k.HangSX,
                    nhaCC = k.NhaCC,
                    sl = k.SL,
                    donVi = k.DonVi
                })
                .ToList();

            return Json(new
            {
                maNguoidung = maNv,
                vtKhoCaNhan = khoCaNhanItems
            });
        }

        [HttpGet]
        public IActionResult GetDataByMaDuan(string maduan)
        {
            if (string.IsNullOrWhiteSpace(maduan))
            {
                return Json(new
                {
                    maNguoidung = HttpContext.Session.GetString("MaNguoidung"),
                    maDuan = "",
                    vtPhieuMuaHang = new List<object>()
                });
            }

            var maNv = HttpContext.Session.GetString("MaNguoidung");

            var allowedProjectCodes = _context.phieuxuatkho
                .Where(px => px.MaNguoidung == maNv && !string.IsNullOrEmpty(px.MaDuan))
                .Select(px => px.MaDuan)
                .Distinct()
                .ToList();

            if (!allowedProjectCodes.Contains(maduan))
            {
                return Json(new
                {
                    maNguoidung = maNv,
                    maDuan = maduan,
                    vtPhieuMuaHang = new List<object>(),
                    error = "Bạn không có quyền trả kho cho dự án này."
                });
            }

            try
            {
                var khoDuanItems = (from vt in _context.vtphieuxuatkho
                                    join px in _context.phieuxuatkho on vt.MaXuatkho equals px.MaXuatkho
                                    join yc in _context.yeucau on vt.MaYeucau equals yc.MaYeucau
                                    where px.MaDuan == maduan
                                       && yc.YCMaNguoidung == maNv
                                       && (vt.TrangThai == "Đã lấy hàng"
                                           || vt.TrangThai == "Đã xuất kho")
                                       && (vt.SL ?? 0) > 0
                                    select new
                                    {
                                        tenSanpham = vt.TenSanpham,
                                        maSanpham = vt.MaSanpham,
                                        makho = vt.Makho,
                                        hangSX = vt.HangSX,
                                        nhaCC = vt.NhaCC,
                                        sl = vt.SL ?? 0,
                                        donVi = vt.DonVi,
                                        maXuatkho = vt.MaXuatkho,
                                        maYeucau = vt.MaYeucau,
                                        trangThai = vt.TrangThai
                                    })
                                   .Distinct()
                                   .ToList();

                var phieuxuatCount = _context.phieuxuatkho.Count(p => p.MaDuan == maduan);

                return Json(new
                {
                    maNguoidung = maNv,
                    maDuan = maduan,
                    vtPhieuMuaHang = khoDuanItems,
                    debug = new
                    {
                        phieuxuatCount,
                        returnedCount = khoDuanItems.Count
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetDataByMaDuan: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return Json(new
                {
                    maNguoidung = maNv,
                    maDuan = maduan,
                    vtPhieuMuaHang = new List<object>(),
                    error = ex.Message
                });
            }
        }

        [HttpGet]
        public IActionResult TimKiem(string timkiem)
        {
            var results = _context.khotongs
                .Where(k => k.TenSanpham.Contains(timkiem) || k.MaSanpham.Contains(timkiem))
                .Select(k => new
                {
                    k.TenSanpham,
                    k.MaSanpham,
                    k.Makho,
                    k.HangSX,
                    k.NhaCC,
                    k.SL,
                    k.DonVi
                })
                .ToList();
            return Json(results);
        }

        [HttpPost]
        public IActionResult ThemyeucauSQL(yeucau yeucau, vtyeucau vtyeucau,
                                           duans duans, phieunhapkho phieunhapkho, vtphieunhapkho vtphieunhapkho, List<string> YCMaKho,
                                           List<string> TenSanpham, List<string> MaSanpham,
                                           List<string> HangSX, List<string> NhaCC, List<int?> SL,
                                           List<string> DonVi, string MaYeucau, string action, phieuxuatkho phieuxuatkho, vtphieuxuatkho vtphieuxuatkho, phieumuahang phieumuahang, vtphieumuahang vtphieumuahang)
        {
            // Kiểm tra null để tránh lỗi khi upload file Excel lớn
            if (yeucau == null)
            {
                yeucau = new yeucau();
            }

            DateTime? GetNgayCanHangAt(int index)
            {
                if (Request.Form.TryGetValue("VTNgayCanHang", out var dateValues))
                {
                    if (index >= 0 && index < dateValues.Count)
                    {
                        if (DateTime.TryParse(dateValues[index], out var parsedDate))
                        {
                            return parsedDate;
                        }
                    }
                }

                return yeucau?.NgayCanHang;
            }

            if (yeucau.TenYeucau != "Yêu cầu nhập kho")
            {
                yeucau.NgayYeucau = DateTime.Now;

                var chucVu2 = HttpContext.Session.GetString("Chucvu");
                var boPhan2 = HttpContext.Session.GetString("Bophan");
                var maNv2 = HttpContext.Session.GetString("MaNguoidung");

                yeucau.YCMaDuan = yeucau.YCMaDuan?.Trim();
                var duan = string.IsNullOrEmpty(yeucau.YCMaDuan)
                    ? null
                    : _context.duans.FirstOrDefault(d => d.MaDuan == yeucau.YCMaDuan);

                // Áp dụng quy tắc duyệt mới
                if (chucVu2 == "Giám đốc")
                {
                    // Quy tắc 4: Giám đốc → Đã duyệt
                    yeucau.TrangThai = "Đã duyệt";
                }
                else if (duan != null)
                {
                    // Có dự án
                    string maNguoiQLDA = duan.MaNguoiQLDA;
                    if (maNv2 == maNguoiQLDA)
                    {
                        // Quy tắc 3: Quản lý dự án (người trùng mã QLDA)
                        if (chucVu2 == "Trưởng BP")
                        {
                            yeucau.TrangThai = "Chờ Giám đốc duyệt";
                        }
                        else if (chucVu2 == "Giám đốc")
                        {
                            yeucau.TrangThai = "Đã duyệt";
                        }
                        else if (chucVu2 == "Nhân viên")
                        {
                            // Quy tắc 1: Nhân viên → Chờ Trưởng BP-BP {bộ phận} duyệt
                            if (boPhan2 == "BP kỹ thuật")
                            {
                                yeucau.TrangThai = "Chờ Trưởng BP-BP kỹ thuật duyệt";
                            }
                            else if (boPhan2 == "BP kho")
                            {
                                yeucau.TrangThai = "Chờ Trưởng BP-BP kho duyệt";
                            }
                            else if (boPhan2 == "BP mua hàng")
                            {
                                yeucau.TrangThai = "Chờ Trưởng BP-BP mua hàng duyệt";
                            }
                            else if (boPhan2 == "BP kế toán")
                            {
                                yeucau.TrangThai = "Chờ Trưởng BP-BP kế toán duyệt";
                            }
                        }
                    }
                    else
                    {
                        // Không phải quản lý dự án nhưng có dự án
                        if (chucVu2 == "Nhân viên")
                        {
                            // Quy tắc 1: Nhân viên → Chờ Trưởng BP-BP {bộ phận} duyệt
                            if (boPhan2 == "BP kỹ thuật")
                            {
                                yeucau.TrangThai = "Chờ Trưởng BP-BP kỹ thuật duyệt";
                            }
                            else if (boPhan2 == "BP kho")
                            {
                                yeucau.TrangThai = "Chờ Trưởng BP-BP kho duyệt";
                            }
                            else if (boPhan2 == "BP mua hàng")
                            {
                                yeucau.TrangThai = "Chờ Trưởng BP-BP mua hàng duyệt";
                            }
                            else if (boPhan2 == "BP kế toán")
                            {
                                yeucau.TrangThai = "Chờ Trưởng BP-BP kế toán duyệt";
                            }
                        }
                        else if (chucVu2 == "Trưởng BP")
                        {
                            // Quy tắc 2: Trưởng BP thuộc dự án → Chờ quản lý dự án duyệt
                            yeucau.TrangThai = "Chờ quản lý dự án duyệt";
                        }
                    }
                }
                else
                {
                    // Không có dự án
                    if (chucVu2 == "Nhân viên")
                    {
                        // Quy tắc 1: Nhân viên → Chờ Trưởng BP-BP {bộ phận} duyệt
                        if (boPhan2 == "BP kỹ thuật")
                        {
                            yeucau.TrangThai = "Chờ Trưởng BP-BP kỹ thuật duyệt";
                        }
                        else if (boPhan2 == "BP kho")
                        {
                            yeucau.TrangThai = "Chờ Trưởng BP-BP kho duyệt";
                        }
                        else if (boPhan2 == "BP mua hàng")
                        {
                            yeucau.TrangThai = "Chờ Trưởng BP-BP mua hàng duyệt";
                        }
                        else if (boPhan2 == "BP kế toán")
                        {
                            yeucau.TrangThai = "Chờ Trưởng BP-BP kế toán duyệt";
                        }
                    }
                    else if (chucVu2 == "Trưởng BP")
                    {
                        // Quy tắc 2: Trưởng BP không thuộc dự án → Chờ Giám đốc duyệt
                        yeucau.TrangThai = "Chờ Giám đốc duyệt";
                    }
                }

                // Đảm bảo trạng thái luôn được set đúng
                if (string.IsNullOrEmpty(yeucau.TrangThai))
                {
                    if (chucVu2 == "Giám đốc")
                    {
                        yeucau.TrangThai = "Đã duyệt";
                    }
                    else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kho")
                    {
                        if (duan != null)
                        {
                            yeucau.TrangThai = "Chờ quản lý dự án duyệt";
                        }
                        else
                        {
                            yeucau.TrangThai = "Chờ Giám đốc duyệt";
                        }
                    }
                }

                // ================== TẠO MÃ YÊU CẦU (DÙNG CHUNG 1 HÀM) ==================
                yeucau.MaYeucau = _yeucauCodeService.GenerateMaYeucauCommon(
                    yeucau.YCMaDuan,
                    MaSanpham,
                    Request.Form.Files,
                    DateTime.Now);
                // ======================================================================

                _context.yeucau.Add(yeucau);
                _context.SaveChanges();

                for (int i = 0; i < YCMaKho.Count; i++)
                {
                    if (string.IsNullOrEmpty(TenSanpham[i]))
                    {
                        continue;
                    }

                    var khoMatch = _context.khotongs.FirstOrDefault(p => p.Makho == YCMaKho[i]);
                    if (khoMatch != null)
                    {
                        var newVtyeucau = new vtyeucau();
                        newVtyeucau.VTMaYeucau = yeucau.MaYeucau;
                        newVtyeucau.TenSanpham = TenSanpham[i];
                        newVtyeucau.MaSanpham = MaSanpham[i];
                        newVtyeucau.HangSX = HangSX[i];
                        newVtyeucau.NhaCC = NhaCC[i];
                        newVtyeucau.SL = (SL != null && i < SL.Count) ? (SL[i] ?? 0) : 0;
                        newVtyeucau.DonVi = DonVi[i];
                        newVtyeucau.NgayCanHang = GetNgayCanHangAt(i);
                        newVtyeucau.YCMakho = khoMatch.Makho;
                        newVtyeucau.NgayNhapkho = khoMatch.NgayNhapkho;
                        newVtyeucau.NgayBaohanh = khoMatch.NgayBaohanh;
                        newVtyeucau.ThoiGianBH = khoMatch.ThoiGianBH;
                        // Set trạng thái cho vật tư: nếu có dự án thì "Chờ quản lý dự án duyệt", nếu không có dự án thì "Chờ Giám đốc duyệt"
                        if (duan != null)
                        {
                            newVtyeucau.TrangThai = "Chờ quản lý dự án duyệt";
                        }
                        else
                        {
                            newVtyeucau.TrangThai = "Chờ Giám đốc duyệt";
                        }
                        _context.vtyeucau.Add(newVtyeucau);
                    }
                    else
                    {
                        var newVtyeucau = new vtyeucau();
                        newVtyeucau.VTMaYeucau = yeucau.MaYeucau;
                        newVtyeucau.TenSanpham = TenSanpham[i];
                        newVtyeucau.MaSanpham = MaSanpham[i];
                        newVtyeucau.HangSX = HangSX[i];
                        newVtyeucau.NhaCC = NhaCC[i];
                        newVtyeucau.SL = (SL != null && i < SL.Count) ? (SL[i] ?? 0) : 0;
                        newVtyeucau.DonVi = DonVi[i];
                        newVtyeucau.NgayCanHang = GetNgayCanHangAt(i);
                        newVtyeucau.YCMakho = "VT mới";
                        newVtyeucau.NgayNhapkho = null;
                        newVtyeucau.NgayBaohanh = null;
                        newVtyeucau.ThoiGianBH = null;
                        // Set trạng thái cho vật tư: nếu có dự án thì "Chờ quản lý dự án duyệt", nếu không có dự án thì "Chờ Giám đốc duyệt"
                        if (duan != null)
                        {
                            newVtyeucau.TrangThai = "Chờ quản lý dự án duyệt";
                        }
                        else
                        {
                            newVtyeucau.TrangThai = "Chờ Giám đốc duyệt";
                        }
                        _context.vtyeucau.Add(newVtyeucau);
                    }
                    _context.SaveChanges();
                }
                if (yeucau.TrangThai == "Đã duyệt")
                {
                    // Sau khi yêu cầu được duyệt hoàn toàn, tạo/cập nhật các phiếu liên quan
                    Xuliphieuyeucau(yeucau.MaYeucau);
                }

                // Gửi thông báo email cho QLDA hoặc Giám đốc khi Trưởng BP Kho tạo yêu cầu
                if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kho")
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"[TruongBPKho/ThemyeucauSQL] ===== BẮT ĐẦU GỬI EMAIL SAU KHI TẠO YÊU CẦU =====");
                        System.Diagnostics.Debug.WriteLine($"[TruongBPKho/ThemyeucauSQL] MaYeucau = {yeucau.MaYeucau}");
                        System.Diagnostics.Debug.WriteLine($"[TruongBPKho/ThemyeucauSQL] TrangThai = {yeucau.TrangThai}");
                        System.Diagnostics.Debug.WriteLine($"[TruongBPKho/ThemyeucauSQL] YCMaDuan = {yeucau.YCMaDuan ?? "(null)"}");
                        System.Diagnostics.Debug.WriteLine($"[TruongBPKho/ThemyeucauSQL] hasMaDuan = {!string.IsNullOrWhiteSpace(yeucau.YCMaDuan)}");

                        var maYeucauForEmail = yeucau.MaYeucau;
                        var hasMaDuanForEmail = !string.IsNullOrWhiteSpace(yeucau.YCMaDuan);
                        var maDuanForEmail = yeucau.YCMaDuan;

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                System.Diagnostics.Debug.WriteLine($"[TruongBPKho/ThemyeucauSQL/Task] Bắt đầu gửi email trong Task.Run");
                                using (var scope = _serviceScopeFactory.CreateScope())
                                {
                                    var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                                    System.Diagnostics.Debug.WriteLine($"[TruongBPKho/ThemyeucauSQL/Task] Đã tạo scope và lấy EmailService");

                                    if (hasMaDuanForEmail && !string.IsNullOrWhiteSpace(maDuanForEmail))
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[TruongBPKho/ThemyeucauSQL/Task] Gửi email cho QLDA. MaYeucau = {maYeucauForEmail}, MaDuan = {maDuanForEmail}");
                                        await emailService.SendNotificationToProjectManagerAsync(maYeucauForEmail, maDuanForEmail);
                                        System.Diagnostics.Debug.WriteLine($"[TruongBPKho/ThemyeucauSQL/Task] ✅ Đã gửi email cho QLDA xong.");
                                    }
                                    else
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[TruongBPKho/ThemyeucauSQL/Task] Gửi email cho Giám đốc. MaYeucau = {maYeucauForEmail}");
                                        await emailService.SendNotificationToDirectorAsync(maYeucauForEmail);
                                        System.Diagnostics.Debug.WriteLine($"[TruongBPKho/ThemyeucauSQL/Task] ✅ Đã gửi email cho Giám đốc xong.");
                                    }
                                }
                            }
                            catch (Exception exInner)
                            {
                                System.Diagnostics.Debug.WriteLine($"[TruongBPKho/ThemyeucauSQL/Task] ❌ Lỗi trong Task.Run khi gửi email: {exInner.Message}");
                                System.Diagnostics.Debug.WriteLine($"[TruongBPKho/ThemyeucauSQL/Task] Stack trace: {exInner.StackTrace}");
                                if (exInner.InnerException != null)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[TruongBPKho/ThemyeucauSQL/Task] Inner exception: {exInner.InnerException.Message}");
                                }
                            }
                        });
                        System.Diagnostics.Debug.WriteLine($"[TruongBPKho/ThemyeucauSQL] ✅ Đã khởi tạo Task.Run để gửi email");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TruongBPKho/ThemyeucauSQL] ❌ Lỗi khi khởi tạo Task.Run: {ex.Message}");
                        System.Diagnostics.Debug.WriteLine($"[TruongBPKho/ThemyeucauSQL] Stack trace: {ex.StackTrace}");
                    }
                }
            }
            else
            {

            }
            {
                // Tạo mã phiếu nhập kho duy nhất bằng service
                phieunhapkho.MaNhapkho = _phieuCodeService.GenerateMaNhapKho(phieunhapkho.MaDuan, phieunhapkho.MaYeucau);
            }

            return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPKho" });

        }
        [HttpPost]
        public IActionResult XuLyYeucau(string MaYeucau, string action, phieuxuatkho phieuxuatkho, vtphieuxuatkho vtphieuxuatkho, phieumuahang phieumuahang, vtphieumuahang vtphieumuahang, yeucau yeucau, vtyeucau vtyeucau)
        {
            var chucVu2 = HttpContext.Session.GetString("Chucvu");
            var boPhan2 = HttpContext.Session.GetString("Bophan");
            var maNv2 = HttpContext.Session.GetString("MaNguoidung");

            // Lấy yêu cầu hiện tại từ cơ sở dữ liệu
            var Yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
            if (Yeucau == null)
            {
                // Xử lý nếu không tìm thấy yêu cầu
                return NotFound();
            }

            var duan = _context.duans.FirstOrDefault(d => d.MaDuan == Yeucau.YCMaDuan);

            if (action == "approve")
            {
                // Xử lý khi trạng thái là "Chờ Trưởng BP-BP kho duyệt"
                if (Yeucau.TrangThai == "Chờ Trưởng BP-BP kho duyệt" && chucVu2 == "Trưởng BP" && boPhan2 == "BP kho")
                {
                    // Kiểm tra xem có phải là yêu cầu nhập kho không
                    bool isNhapKho = !string.IsNullOrEmpty(Yeucau.MaYeucau) &&
                                    (Yeucau.MaYeucau.StartsWith("NHAPKHO_DUAN_") ||
                                     Yeucau.MaYeucau.StartsWith("NHAPKHO_CANHAN_"));

                    if (isNhapKho)
                    {
                        // Nếu là nhập kho
                        if (Yeucau.MaYeucau.StartsWith("NHAPKHO_DUAN_"))
                        {
                            // Dự án: Chờ quản lý dự án duyệt
                            Yeucau.TrangThai = "Chờ quản lý dự án duyệt";
                            // Gửi thông báo đến QLDA sau khi Trưởng BP duyệt
                            if (duan != null)
                            {
                                _ = _emailService.SendNotificationToProjectManagerAsync(Yeucau.MaYeucau, duan.MaDuan);
                            }
                        }
                        else if (Yeucau.MaYeucau.StartsWith("NHAPKHO_CANHAN_"))
                        {
                            // Cá nhân: Chờ Giám đốc duyệt
                            Yeucau.TrangThai = "Chờ Giám đốc duyệt";
                            // Gửi thông báo đến Giám đốc sau khi Trưởng BP duyệt (không có dự án)
                            _ = _emailService.SendNotificationToDirectorAsync(Yeucau.MaYeucau);
                        }
                    }
                    else
                    {
                        // Nếu là yêu cầu vật tư thông thường
                        if (duan != null)
                        {
                            // Có dự án: Chờ quản lý dự án duyệt
                            Yeucau.TrangThai = "Chờ quản lý dự án duyệt";
                            // Gửi thông báo đến QLDA sau khi Trưởng BP duyệt
                            _ = _emailService.SendNotificationToProjectManagerAsync(Yeucau.MaYeucau, duan.MaDuan);

                            // Đồng bộ trạng thái cho tất cả vật tư (bao gồm cả null/empty)
                            var allVatTu = _context.vtyeucau.Where(v => v.VTMaYeucau == MaYeucau).ToList();
                            foreach (var vt in allVatTu)
                            {
                                // Xử lý vật tư có TrangThai null hoặc rỗng
                                if (string.IsNullOrEmpty(vt.TrangThai) ||
                                    (vt.TrangThai != "Đã duyệt" && vt.TrangThai != "Đang mua hàng" &&
                                     vt.TrangThai != "Đã từ chối" && vt.TrangThai != "Đã xuất kho" &&
                                     vt.TrangThai != "Đã nhận hàng"))
                                {
                                    vt.TrangThai = "Chờ quản lý dự án duyệt";
                                    _context.vtyeucau.Update(vt);
                                }
                            }
                        }
                        else
                        {
                            // Cá nhân: Chờ Giám đốc duyệt
                            Yeucau.TrangThai = "Chờ Giám đốc duyệt";
                            // Gửi thông báo đến Giám đốc sau khi Trưởng BP duyệt (không có dự án)
                            _ = _emailService.SendNotificationToDirectorAsync(Yeucau.MaYeucau);

                            // Đồng bộ trạng thái cho tất cả vật tư (bao gồm cả null/empty)
                            var allVatTu = _context.vtyeucau.Where(v => v.VTMaYeucau == MaYeucau).ToList();
                            foreach (var vt in allVatTu)
                            {
                                // Xử lý vật tư có TrangThai null hoặc rỗng
                                if (string.IsNullOrEmpty(vt.TrangThai) ||
                                    (vt.TrangThai != "Đã duyệt" && vt.TrangThai != "Đang mua hàng" &&
                                     vt.TrangThai != "Đã từ chối" && vt.TrangThai != "Đã xuất kho" &&
                                     vt.TrangThai != "Đã nhận hàng"))
                                {
                                    vt.TrangThai = "Chờ Giám đốc duyệt";
                                    _context.vtyeucau.Update(vt);
                                }
                            }
                        }
                    }
                }
                else if (duan != null)
                {
                    string maNguoiQLDA = duan.MaNguoiQLDA;
                    if (maNv2 == maNguoiQLDA)
                    {
                        if (chucVu2 != "Giám đốc")
                        {
                            Yeucau.TrangThai = "Giám đốc";

                            // Đồng bộ trạng thái cho tất cả vật tư khi có dự án (bao gồm cả null/empty)
                            var allVatTu = _context.vtyeucau.Where(v => v.VTMaYeucau == MaYeucau).ToList();
                            foreach (var vt in allVatTu)
                            {
                                // Xử lý vật tư có TrangThai null hoặc rỗng
                                if (string.IsNullOrEmpty(vt.TrangThai) ||
                                    (vt.TrangThai != "Đã duyệt" && vt.TrangThai != "Đang mua hàng" &&
                                     vt.TrangThai != "Đã từ chối" && vt.TrangThai != "Đã xuất kho" &&
                                     vt.TrangThai != "Đã nhận hàng"))
                                {
                                    vt.TrangThai = "Chờ giám đốc duyệt";
                                    _context.vtyeucau.Update(vt);
                                }
                            }
                        }
                        else
                        {
                            Yeucau.TrangThai = "Đã duyệt";
                            Xuliphieuyeucau(Yeucau.MaYeucau);
                        }
                    }
                    else
                    {
                        if (Yeucau.YCMaNguoidung != maNguoiQLDA)
                        {
                            if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kỹ thuật")
                            {
                                Yeucau.TrangThai = "Quản lí dự án";
                                // Gửi thông báo đến QLDA sau khi Trưởng BP duyệt
                                _ = _emailService.SendNotificationToProjectManagerAsync(Yeucau.MaYeucau, duan.MaDuan);
                            }
                            else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kho")
                            {
                                Yeucau.TrangThai = "Quản lí dự án";
                                // Gửi thông báo đến QLDA sau khi Trưởng BP duyệt
                                _ = _emailService.SendNotificationToProjectManagerAsync(Yeucau.MaYeucau, duan.MaDuan);
                            }
                            else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP mua hàng")
                            {
                                Yeucau.TrangThai = "Quản lí dự án";
                                // Gửi thông báo đến QLDA sau khi Trưởng BP duyệt
                                _ = _emailService.SendNotificationToProjectManagerAsync(Yeucau.MaYeucau, duan.MaDuan);
                            }
                            else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kế toán")
                            {
                                Yeucau.TrangThai = "Quản lí dự án";
                                // Gửi thông báo đến QLDA sau khi Trưởng BP duyệt
                                _ = _emailService.SendNotificationToProjectManagerAsync(Yeucau.MaYeucau, duan.MaDuan);
                            }
                            else if (chucVu2 == "Giám đốc")
                            {
                                Yeucau.TrangThai = "Đã duyệt";
                                Xuliphieuyeucau(Yeucau.MaYeucau);
                            }
                        }
                        else
                        {
                            if (chucVu2 != "Giám đốc")
                            {
                                Yeucau.TrangThai = "Giám đốc";

                                // Đồng bộ trạng thái cho tất cả vật tư khi có dự án (bao gồm cả null/empty)
                                var allVatTu = _context.vtyeucau.Where(v => v.VTMaYeucau == MaYeucau).ToList();
                                foreach (var vt in allVatTu)
                                {
                                    // Xử lý vật tư có TrangThai null hoặc rỗng
                                    if (string.IsNullOrEmpty(vt.TrangThai) ||
                                        (vt.TrangThai != "Đã duyệt" && vt.TrangThai != "Đang mua hàng" &&
                                         vt.TrangThai != "Đã từ chối" && vt.TrangThai != "Đã xuất kho" &&
                                         vt.TrangThai != "Đã nhận hàng"))
                                    {
                                        vt.TrangThai = "Chờ giám đốc duyệt";
                                        _context.vtyeucau.Update(vt);
                                    }
                                }
                            }
                            else
                            {
                                Yeucau.TrangThai = "Đã duyệt";
                                Xuliphieuyeucau(Yeucau.MaYeucau);
                            }
                        }
                    }
                }
                else
                {
                    if (chucVu2 == "Nhân viên" && boPhan2 == "BP kỹ thuật")
                    {
                        Yeucau.TrangThai = "Trưởng BP-BP kỹ thuật";
                    }
                    else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kỹ thuật")
                    {
                        Yeucau.TrangThai = "Giám đốc";
                        // Gửi thông báo đến Giám đốc sau khi Trưởng BP duyệt (không có dự án)
                        _ = _emailService.SendNotificationToDirectorAsync(Yeucau.MaYeucau);
                    }
                    else if (chucVu2 == "Nhân viên" && boPhan2 == "BP kho")
                    {
                        Yeucau.TrangThai = "Trưởng BP-BP kho";
                    }
                    else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kho")
                    {
                        Yeucau.TrangThai = "Giám đốc";
                        // Gửi thông báo đến Giám đốc sau khi Trưởng BP duyệt (không có dự án)
                        _ = _emailService.SendNotificationToDirectorAsync(Yeucau.MaYeucau);

                        // Đồng bộ trạng thái cho tất cả vật tư (bao gồm cả null/empty)
                        var allVatTu = _context.vtyeucau.Where(v => v.VTMaYeucau == MaYeucau).ToList();
                        foreach (var vt in allVatTu)
                        {
                            // Xử lý vật tư có TrangThai null hoặc rỗng
                            if (string.IsNullOrEmpty(vt.TrangThai) ||
                                (vt.TrangThai != "Đã duyệt" && vt.TrangThai != "Đang mua hàng" &&
                                 vt.TrangThai != "Đã từ chối" && vt.TrangThai != "Đã xuất kho" &&
                                 vt.TrangThai != "Đã nhận hàng"))
                            {
                                vt.TrangThai = "Chờ giám đốc duyệt";
                                _context.vtyeucau.Update(vt);
                            }
                        }
                    }
                    else if (chucVu2 == "Nhân viên" && boPhan2 == "BP mua hàng")
                    {
                        Yeucau.TrangThai = "Trưởng BP-BP mua hàng";
                    }
                    else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP mua hàng")
                    {
                        Yeucau.TrangThai = "Giám đốc";
                        // Gửi thông báo đến Giám đốc sau khi Trưởng BP duyệt (không có dự án)
                        _ = _emailService.SendNotificationToDirectorAsync(Yeucau.MaYeucau);
                    }
                    else if (chucVu2 == "Giám đốc")
                    {
                        Yeucau.TrangThai = "Đã duyệt";
                        Xuliphieuyeucau(Yeucau.MaYeucau);
                    }
                }
            }
            else if (action == "reject")
            {
                Xulituchoiyeucau(MaYeucau, yeucau, vtyeucau, null, null);
            }
            _context.yeucau.Update(Yeucau);
            _context.SaveChanges();

            return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPKho" });
        }

        [HttpPost]
        [HttpPost]
        public IActionResult Xuliphieuyeucau(string Mayeucau)
        {
            // 1️⃣ Lấy thông tin yêu cầu
            var thongTinYeuCau = _context.yeucau
                .FirstOrDefault(yc => yc.MaYeucau == Mayeucau);

            if (thongTinYeuCau == null)
            {
                return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPKho" });
            }

            // 2️⃣ Lấy danh sách vật tư của yêu cầu hiện tại
            var danhSachVatTuYC = _context.vtyeucau
                .Where(vt => vt.VTMaYeucau == Mayeucau)
                .ToList();

            if (!danhSachVatTuYC.Any())
            {
                return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPKho" });
            }

            // 3️⃣ Xác định base mã yêu cầu (251005 STUP10.5013)
            string baseMaYeucau = YeucauUpdateHelper.GetBaseRequestCode(Mayeucau);

            // 4️⃣ Lấy toàn bộ mã yêu cầu cùng dự án + cùng base mã
            var relatedMaYeucau = _context.yeucau
                .Where(y => y.YCMaDuan == thongTinYeuCau.YCMaDuan)
                .ToList()
                .Where(y => YeucauUpdateHelper.GetBaseRequestCode(y.MaYeucau) == baseMaYeucau)
                .Select(y => y.MaYeucau)
                .ToList();

            // 5️⃣ Sinh mã phiếu
            string Maxuatkho = TaoMaTuDong("PXK", _context.phieuxuatkho.Select(x => x.MaXuatkho));
            string Mamuahang = TaoMaTuDong("PMH", _context.phieumuahang.Select(x => x.MaMuahang));

            bool canXuat = false;
            bool canMua = false;

            // 6️⃣ Nhóm vật tư theo mã sản phẩm + kho
            var vatTuNhom = danhSachVatTuYC
                .GroupBy(vt => new { vt.MaSanpham, vt.YCMakho })
                .ToList();

            foreach (var nhom in vatTuNhom)
            {
                string maSanpham = nhom.Key.MaSanpham ?? "";
                string makho = nhom.Key.YCMakho ?? "";
                var VattuYC = nhom.First();

                // 7️⃣ Tổng nhu cầu của DỰ ÁN (KHÔNG cộng dồn sai)
                int tongSLYeuCau = _context.vtyeucau
                    .Where(vt =>
                        relatedMaYeucau.Contains(vt.VTMaYeucau) &&
                        vt.MaSanpham == maSanpham)
                    .Sum(vt => vt.SL ?? 0);

                // 8️⃣ Tổng đã xuất cho DỰ ÁN
                int SL_da_cap = YeucauUpdateHelper.TinhSoLuongDaCapTheoDuAn(
                    _context,
                    thongTinYeuCau.YCMaDuan,
                    baseMaYeucau,
                    maSanpham
                );

                int SL_can_cap = tongSLYeuCau - SL_da_cap;
                if (SL_can_cap <= 0) continue;

                // 9️⃣ Tồn kho khả dụng
                var khotong = _context.khotongs
                    .FirstOrDefault(k =>
                        k.MaSanpham == maSanpham &&
                        k.Makho == makho);

                int SL_ton_kho = khotong?.SL ?? 0;
                int SL_xuat = Math.Min(SL_can_cap, SL_ton_kho);
                int SL_mua = SL_can_cap - SL_xuat;

                // 🔹 Xuất kho
                if (SL_xuat > 0)
                {
                    canXuat = true;

                    _context.vtphieuxuatkho.Add(new vtphieuxuatkho
                    {
                        MaXuatkho = Maxuatkho,
                        MaYeucau = VattuYC.VTMaYeucau,
                        MaSanpham = maSanpham,
                        TenSanpham = VattuYC.TenSanpham,
                        Makho = makho,
                        SL = SL_xuat,
                        TrangThai = "Đang chuẩn bị hàng"
                    });
                }

                // 🔹 Mua hàng
                if (SL_mua > 0)
                {
                    canMua = true;

                    foreach (var vt in nhom)
                    {
                        vt.TrangThai = "Đang mua hàng";
                        _context.vtyeucau.Update(vt);
                    }

                    _context.vtphieumuahang.Add(new vtphieumuahang
                    {
                        MaMuahang = Mamuahang,
                        MaYeucau = VattuYC.VTMaYeucau,
                        MaSanpham = maSanpham,
                        TenSanpham = VattuYC.TenSanpham,
                        Makho = makho,
                        SL = SL_mua,
                        TrangThai = "Đang chờ báo giá"
                    });
                }
            }

            // 10️⃣ Tạo phiếu cha
            if (canXuat)
            {
                _context.phieuxuatkho.Add(new phieuxuatkho
                {
                    MaXuatkho = Maxuatkho,
                    MaYeucau = thongTinYeuCau.MaYeucau,
                    MaDuan = thongTinYeuCau.YCMaDuan,
                    MaNguoidung = thongTinYeuCau.YCMaNguoidung,
                    NgayXuatkho = DateTime.Now,
                    TrangThai = "Đang chuẩn bị hàng"
                });
            }

            if (canMua)
            {
                _context.phieumuahang.Add(new phieumuahang
                {
                    MaMuahang = Mamuahang,
                    MaYeucau = thongTinYeuCau.MaYeucau,
                    MaDuan = thongTinYeuCau.YCMaDuan,
                    MaNguoidung = thongTinYeuCau.YCMaNguoidung,
                    NgayMuahang = DateTime.Now,
                    TrangThai = "Đang chờ báo giá"
                });
            }

            _context.SaveChanges();

            return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPKho" });
        }


        [HttpPost]
        public IActionResult Xuliphieuxuatkho(
                                string MaXuatkho,
                                phieuxuatkho phieuxuatkho,
                                vtphieuxuatkho vtphieuxuatkho,
                                khoduans khoduans,
                                string? slThucXuatJson)
        {
            var VTphieuxuatkho = _context.vtphieuxuatkho
                                          .Where(vt => vt.MaXuatkho == MaXuatkho)
                                          .ToList();

            var vatTuCanXuat = VTphieuxuatkho
                .Where(vt => vt.TrangThai != "Đã xuất kho")
                .ToList();

            var Phieuxuatkho = _context.phieuxuatkho
                                        .FirstOrDefault(yc => yc.MaXuatkho == MaXuatkho);

            if (Phieuxuatkho == null)
            {
                TempData["Error"] = "Không tìm thấy phiếu xuất kho!";
                return RedirectToAction("Phieuxuatkho", "Yeucau", new { area = "TruongBPKho" });
            }

            // Nếu phiếu đang chờ xác nhận hoặc đang ở trạng thái thiếu hàng, kiểm tra lại tồn kho sau khi có hàng mới
            if (Phieuxuatkho.TrangThai == "Chờ xác nhận" || Phieuxuatkho.TrangThai == "Thiếu hàng - Đã tạo phiếu mua")
            {
                var vtYeuCauList = string.IsNullOrEmpty(Phieuxuatkho.MaYeucau)
                    ? new List<vtyeucau>()
                    : _context.vtyeucau
                        .Where(vt => vt.VTMaYeucau == Phieuxuatkho.MaYeucau)
                        .ToList();

                bool duHang = true;
                var vatTuThieu = new List<vtphieuxuatkho>();

                var vatTuNhom = VTphieuxuatkho
                    .GroupBy(vt => vt.MaSanpham ?? "")
                    .ToList();

                foreach (var nhom in vatTuNhom)
                {
                    var maSanpham = nhom.Key;
                    var dongDauTien = nhom.First();

                    // Tính tổng số lượng yêu cầu của TẤT CẢ vật tư cùng mã yêu cầu cơ bản
                    // Lấy mã yêu cầu cơ bản từ phiếu xuất kho
                    string baseMaYeucau = "";
                    if (!string.IsNullOrEmpty(Phieuxuatkho.MaYeucau))
                    {
                        baseMaYeucau = YeucauUpdateHelper.GetBaseRequestCode(Phieuxuatkho.MaYeucau);
                    }

                    System.Diagnostics.Debug.WriteLine($"[TruongBPKho/Xuliphieuxuatkho] ----");
                    System.Diagnostics.Debug.WriteLine($"[TruongBPKho/Xuliphieuxuatkho] MaXuatkho = {MaXuatkho}, MaYeucau tren phieu = {Phieuxuatkho.MaYeucau}, BaseMaYeucau = {baseMaYeucau}, MaSanpham = {maSanpham}");

                    // Lấy tất cả mã yêu cầu có cùng mã cơ bản
                    // Nếu phiếu thuộc DỰ ÁN → chỉ gom các yêu cầu cùng dự án + cùng base code
                    var allRelatedMaYeucau = new List<string>();
                    if (!string.IsNullOrEmpty(baseMaYeucau))
                    {
                        var ycQuery = _context.yeucau
                            .Where(y => !string.IsNullOrWhiteSpace(y.MaYeucau));

                        if (!string.IsNullOrWhiteSpace(Phieuxuatkho.MaDuan))
                        {
                            ycQuery = ycQuery.Where(y => y.YCMaDuan == Phieuxuatkho.MaDuan);
                            System.Diagnostics.Debug.WriteLine($"[TruongBPKho/Xuliphieuxuatkho]   Loc theo MaDuan = {Phieuxuatkho.MaDuan}");
                        }

                        var ycList = ycQuery.ToList();
                        allRelatedMaYeucau = ycList
                            .Where(y => string.Equals(
                                YeucauUpdateHelper.GetBaseRequestCode(y.MaYeucau),
                                baseMaYeucau,
                                StringComparison.OrdinalIgnoreCase))
                            .Select(y => y.MaYeucau)
                            .ToList();

                        System.Diagnostics.Debug.WriteLine($"[TruongBPKho/Xuliphieuxuatkho]   Tong so yeu cau sau loc base code = {allRelatedMaYeucau.Count}");
                        foreach (var maYc in allRelatedMaYeucau)
                        {
                            System.Diagnostics.Debug.WriteLine($"[TruongBPKho/Xuliphieuxuatkho]     - Related MaYeucau: {maYc}");
                        }
                    }

                    // Tính tổng số lượng yêu cầu của TẤT CẢ vật tư cùng mã yêu cầu cơ bản và cùng mã sản phẩm
                    int soLuongYeuCauBanDau = 0;
                    if (allRelatedMaYeucau.Any())
                    {
                        soLuongYeuCauBanDau = _context.vtyeucau
                            .Where(vt => allRelatedMaYeucau.Contains(vt.VTMaYeucau)
                                && (vt.MaSanpham ?? "") == maSanpham)
                            .Sum(vt => (int?)vt.SL) ?? 0;
                    }
                    else
                    {
                        // Fallback: nếu không tìm thấy mã yêu cầu cơ bản, dùng logic cũ
                        var vtYeuCau = vtYeuCauList.FirstOrDefault(vt =>
                            string.Equals(vt.MaSanpham, maSanpham, StringComparison.OrdinalIgnoreCase));
                        soLuongYeuCauBanDau = vtYeuCau?.SL ?? 0;
                    }

                    // ⭐ SỬA LỖI: Tính tổng số lượng đã xuất của TẤT CẢ phiếu xuất kho có cùng mã yêu cầu cơ bản
                    int soLuongDaXuat = 0;
                    if (allRelatedMaYeucau.Any())
                    {
                        // Lấy tất cả phiếu xuất kho có cùng mã yêu cầu cơ bản
                        var allPhieuXuatKho = _context.phieuxuatkho
                            .Where(px => allRelatedMaYeucau.Contains(px.MaYeucau))
                            .Select(px => px.MaXuatkho)
                            .ToList();

                        // Tính tổng số lượng đã xuất từ TẤT CẢ phiếu xuất kho có cùng mã yêu cầu cơ bản
                        soLuongDaXuat = _context.vtphieuxuatkho
                            .Where(vt => allPhieuXuatKho.Contains(vt.MaXuatkho)
                                && (vt.MaSanpham ?? "") == maSanpham
                                && (vt.TrangThai == "Đã xuất kho"))
                            .Sum(vt => (int?)vt.SL) ?? 0;

                        System.Diagnostics.Debug.WriteLine($"[TruongBPKho/Xuliphieuxuatkho]   Tong so phieu xuat lien quan = {allPhieuXuatKho.Count}, SoLuongDaXuat = {soLuongDaXuat}");
                    }
                    else
                    {
                        // Fallback: nếu không tìm thấy mã yêu cầu cơ bản, dùng logic cũ (chỉ tính từ phiếu hiện tại)
                        soLuongDaXuat = nhom
                            .Where(vt => vt.TrangThai == "Đã xuất kho")
                            .Sum(vt => vt.SL ?? 0);

                        System.Diagnostics.Debug.WriteLine($"[TruongBPKho/Xuliphieuxuatkho]   KHONG tim thay base code lien quan, dung logic cu. SoLuongDaXuat = {soLuongDaXuat}");
                    }

                    int soLuongConLaiCanXuat = Math.Max(0, soLuongYeuCauBanDau - soLuongDaXuat);

                    // Tổng hợp tồn kho từ tất cả các lô có cùng mã sản phẩm
                    int tongSoLuongTonKho = _context.khotongs
                        .Where(k => k.MaSanpham == maSanpham)
                        .Sum(k => k.SL ?? 0);

                    int soLuongDaCamKet = this.TinhSoLuongDaCamKetTheoMaSanpham(maSanpham, MaXuatkho);
                    int soLuongKhaDung = tongSoLuongTonKho - soLuongDaCamKet;

                    System.Diagnostics.Debug.WriteLine($"[TruongBPKho/Xuliphieuxuatkho]   SoLuongYeuCauBanDau = {soLuongYeuCauBanDau}, SoLuongDaXuat = {soLuongDaXuat}, SoLuongConLaiCanXuat = {soLuongConLaiCanXuat}");
                    System.Diagnostics.Debug.WriteLine($"[TruongBPKho/Xuliphieuxuatkho]   TongTonKho = {tongSoLuongTonKho}, SoLuongDaCamKet = {soLuongDaCamKet}, SoLuongKhaDung = {soLuongKhaDung}");

                    if (soLuongConLaiCanXuat > 0 && (tongSoLuongTonKho <= 0 || soLuongKhaDung < soLuongConLaiCanXuat))
                    {
                        duHang = false;
                        int soLuongThieu = Math.Max(0, soLuongConLaiCanXuat - Math.Max(0, soLuongKhaDung));
                        vatTuThieu.Add(new vtphieuxuatkho
                        {
                            MaXuatkho = dongDauTien.MaXuatkho,
                            MaYeucau = dongDauTien.MaYeucau,
                            TenSanpham = dongDauTien.TenSanpham,
                            MaSanpham = dongDauTien.MaSanpham,
                            Makho = dongDauTien.Makho,
                            HangSX = dongDauTien.HangSX,
                            NhaCC = dongDauTien.NhaCC,
                            DonVi = dongDauTien.DonVi,
                            SL = soLuongThieu,
                            TrangThai = dongDauTien.TrangThai
                        });

                        System.Diagnostics.Debug.WriteLine($"[TruongBPKho/Xuliphieuxuatkho]   -> Thieu hang: SoLuongThieu = {soLuongThieu}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[TruongBPKho/Xuliphieuxuatkho]   Du hang cho ma san pham nay.");
                    }
                }

                if (duHang)
                {
                    Phieuxuatkho.TrangThai = "Đang chuẩn bị hàng";
                    Phieuxuatkho.NgayChuanBi = DateTime.Now;
                    Phieuxuatkho.GhiChu = null;
                    _context.phieuxuatkho.Update(Phieuxuatkho);

                    foreach (var vt in VTphieuxuatkho)
                    {
                        if (vt.TrangThai != "Đã xuất kho")
                        {
                            vt.TrangThai = "Đang chuẩn bị hàng";
                            _context.vtphieuxuatkho.Update(vt);
                        }
                    }

                    _context.SaveChanges();
                }
                else
                {
                    Phieuxuatkho.TrangThai = "Thiếu hàng - Đã tạo phiếu mua";
                    Phieuxuatkho.GhiChu = "Không đủ tồn kho, đã tạo phiếu mua bổ sung.";
                    _context.phieuxuatkho.Update(Phieuxuatkho);

                    TaoPhieuMuaHangTuDong(Phieuxuatkho, vatTuThieu);
                    _context.SaveChanges();

                    TempData["Error"] = "Phiếu chưa đủ hàng, đã tạo yêu cầu mua bổ sung.";
                    return RedirectToAction("Phieuxuatkho", "Yeucau", new { area = "TruongBPKho" });
                }
            }

            if (Phieuxuatkho.TrangThai != "Đang chuẩn bị hàng")
            {
                TempData["Error"] = "Chỉ có thể xuất kho khi phiếu đang ở trạng thái 'Đang chuẩn bị hàng'.";
                return RedirectToAction("Phieuxuatkho", "Yeucau", new { area = "TruongBPKho" });
            }

            if (!vatTuCanXuat.Any())
            {
                TempData["Info"] = "Tất cả vật tư trong phiếu này đã được xuất trước đó.";
                return RedirectToAction("Phieuxuatkho", "Yeucau", new { area = "TruongBPKho" });
            }

            var slMap = ParseSlThucXuatJson(slThucXuatJson);

            // SL gốc theo base code và đã xuất theo base code (tính trong memory)
            var maYcForCalc = Phieuxuatkho.MaYeucau ?? "";
            var allRowsSameYC = string.IsNullOrWhiteSpace(maYcForCalc)
                ? new List<vtphieuxuatkho>()
                : _context.vtphieuxuatkho.AsNoTracking().Where(v => v.MaYeucau == maYcForCalc).ToList();

            var slGocByBase = allRowsSameYC
                .GroupBy(v => YeucauUpdateHelper.GetBaseProductCode(v.MaSanpham ?? "") ?? "")
                .ToDictionary(g => g.Key, g => g.Max(x => x.SL ?? 0), StringComparer.OrdinalIgnoreCase);

            var daXuatByBase = allRowsSameYC
                .Where(v => v.TrangThai == "Đã xuất kho" || v.TrangThai == "Hoàn thành")
                .GroupBy(v => YeucauUpdateHelper.GetBaseProductCode(v.MaSanpham ?? "") ?? "")
                .ToDictionary(g => g.Key, g => g.Sum(x => x.SL ?? 0), StringComparer.OrdinalIgnoreCase);

            // Validate trước để tránh trừ kho nửa chừng
            foreach (var vt in vatTuCanXuat)
            {
                var baseCode = YeucauUpdateHelper.GetBaseProductCode(vt.MaSanpham ?? "") ?? "";
                int slGoc = slGocByBase.TryGetValue(baseCode, out var goc) ? goc : (vt.SL ?? 0);
                int daXuat = daXuatByBase.TryGetValue(baseCode, out var dx) ? dx : 0;
                int conLai = Math.Max(0, slGoc - daXuat);
                int slThucXuat = slMap.ContainsKey(vt.ID) ? slMap[vt.ID] : conLai;
                if (conLai <= 0)
                {
                    TempData["Error"] = $"Số lượng còn lại của vật tư {vt.TenSanpham} không hợp lệ.";
                    return RedirectToAction("Phieuxuatkho", "Yeucau", new { area = "TruongBPKho" });
                }
                if (slThucXuat <= 0 || slThucXuat > conLai)
                {
                    TempData["Error"] = $"Cần xuất của vật tư {vt.TenSanpham} phải > 0 và ≤ Còn lại ({conLai}).";
                    return RedirectToAction("Phieuxuatkho", "Yeucau", new { area = "TruongBPKho" });
                }
            }

            // ✅ Quy tắc chuẩn:
            // - Nếu "xuất hết" (mọi dòng đều xuất đúng bằng Còn lại) => KHÔNG tạo phiếu mới, xuất trực tiếp trên phiếu hiện tại.
            // - Nếu có bất kỳ dòng nào xuất ít hơn Còn lại => coi là "xuất từng phần" => tạo phiếu mới để in đúng SL thực xuất.
            bool xuatHet = true;
            foreach (var vt in vatTuCanXuat)
            {
                var baseCode = YeucauUpdateHelper.GetBaseProductCode(vt.MaSanpham ?? "") ?? "";
                int slGoc = slGocByBase.TryGetValue(baseCode, out var goc) ? goc : (vt.SL ?? 0);
                int daXuat = daXuatByBase.TryGetValue(baseCode, out var dx) ? dx : 0;
                int conLai = Math.Max(0, slGoc - daXuat);
                int slThucXuat = slMap.ContainsKey(vt.ID) ? slMap[vt.ID] : conLai;
                if (slThucXuat != conLai)
                {
                    xuatHet = false;
                    break;
                }
            }

            bool taoPhieuMoi = !xuatHet;
            string maXuatkhoMoi = taoPhieuMoi
                ? _phieuCodeService.GenerateMaXuatKho(Phieuxuatkho.MaDuan, Phieuxuatkho.MaYeucau)
                : (Phieuxuatkho.MaXuatkho ?? MaXuatkho);

            if (taoPhieuMoi)
            {
                // Tạo 1 phiếu xuất kho mới cho phần thực xuất (để in đúng SL thực xuất)
                var phieuMoi = new phieuxuatkho
                {
                    MaXuatkho = maXuatkhoMoi,
                    MaYeucau = Phieuxuatkho.MaYeucau,
                    MaDuan = Phieuxuatkho.MaDuan,
                    MaNguoidung = Phieuxuatkho.MaNguoidung,
                    NgayXuatkho = DateTime.Now,
                    NgayChuanBi = DateTime.Now,
                    TrangThai = "Đã xuất kho",
                    GhiChu = $"Tạo từ phiếu {Phieuxuatkho.MaXuatkho} (xuất từng phần)"
                };
                _context.phieuxuatkho.Add(phieuMoi);
            }

            foreach (var VTxuatkho in vatTuCanXuat)
            {
                var maSp = VTxuatkho.MaSanpham ?? "";
                var baseCode = YeucauUpdateHelper.GetBaseProductCode(VTxuatkho.MaSanpham ?? "") ?? "";
                int slGoc = slGocByBase.TryGetValue(baseCode, out var goc) ? goc : (VTxuatkho.SL ?? 0);
                int daXuat = daXuatByBase.TryGetValue(baseCode, out var dx) ? dx : 0;
                int conLai = Math.Max(0, slGoc - daXuat);

                // Lấy toàn bộ các lô trong kho có cùng mã sản phẩm
                var danhSachLoKho = _context.khotongs
                    .Where(k => k.MaSanpham == maSp)
                    .OrderBy(k => k.NgayNhapkho) // ưu tiên xuất trước lô nhập sớm (FIFO)
                    .ToList();

                if (!danhSachLoKho.Any())
                {
                    TempData["Error"] = $"Vật tư {VTxuatkho.TenSanpham} không tồn tại trong kho tổng.";
                    return RedirectToAction("Phieuxuatkho", "Yeucau", new { area = "TruongBPKho" });
                }

                int tongTon = danhSachLoKho.Sum(k => k.SL ?? 0);
                int soLuongXuat = slMap.ContainsKey(VTxuatkho.ID) ? slMap[VTxuatkho.ID] : conLai;

                if (tongTon < soLuongXuat)
                {
                    TempData["Error"] = $"Không đủ tồn kho để xuất vật tư {VTxuatkho.TenSanpham}.";
                    return RedirectToAction("Phieuxuatkho", "Yeucau", new { area = "TruongBPKho" });
                }

                // Đủ hàng: trừ lần lượt trên từng lô (FIFO)
                int soLuongCanTru = soLuongXuat;
                foreach (var lo in danhSachLoKho)
                {
                    if (soLuongCanTru <= 0) break;

                    var soLuongTrongLo = lo.SL ?? 0;
                    if (soLuongTrongLo <= 0) continue;

                    var tru = Math.Min(soLuongTrongLo, soLuongCanTru);
                    lo.SL = soLuongTrongLo - tru;
                    soLuongCanTru -= tru;
                    _context.khotongs.Update(lo);
                }

                // Tính đơn giá / thành tiền theo số lượng thực xuất
                decimal? donGia = VTxuatkho.DonGia;
                if (!donGia.HasValue && VTxuatkho.ThanhTien.HasValue && slGoc > 0)
                {
                    donGia = VTxuatkho.ThanhTien.Value / slGoc;
                }
                decimal? thanhTienXuat = null;
                if (donGia.HasValue)
                {
                    thanhTienXuat = donGia.Value * soLuongXuat;
                }
                else if (VTxuatkho.ThanhTien.HasValue && slGoc > 0)
                {
                    thanhTienXuat = (VTxuatkho.ThanhTien.Value / slGoc) * soLuongXuat;
                }

                // Dòng VT trên phiếu mới (phần đã xuất) - trạng thái Đã xuất kho
                if (taoPhieuMoi)
                {
                    _context.vtphieuxuatkho.Add(new vtphieuxuatkho
                    {
                        MaXuatkho = maXuatkhoMoi,
                        MaYeucau = VTxuatkho.MaYeucau,
                        TenSanpham = VTxuatkho.TenSanpham,
                        MaSanpham = VTxuatkho.MaSanpham,
                        Makho = VTxuatkho.Makho,
                        HangSX = VTxuatkho.HangSX,
                        NhaCC = VTxuatkho.NhaCC,
                        SL = soLuongXuat,
                        DonVi = VTxuatkho.DonVi,
                        DonGia = donGia,
                        ThanhTien = thanhTienXuat,
                        NgayNhapkho = VTxuatkho.NgayNhapkho,
                        NgayBaohanh = VTxuatkho.NgayBaohanh,
                        ThoiGianBH = VTxuatkho.ThoiGianBH,
                        TrangThai = "Đã xuất kho",
                        LoaiCapPhat = VTxuatkho.LoaiCapPhat
                    });
                }

                // Không update VTxuatkho.SL (SL gốc phải giữ nguyên).
                // Chỉ cập nhật trạng thái nếu đã xuất đủ theo SL gốc.
                int conLaiSauXuat = Math.Max(0, conLai - soLuongXuat);
                if (conLaiSauXuat <= 0)
                {
                    VTxuatkho.TrangThai = "Đã xuất kho";
                    _context.vtphieuxuatkho.Update(VTxuatkho);
                }

                if (!string.IsNullOrEmpty(Phieuxuatkho.MaDuan))
                {
                    // tìm xem sản phẩm này đã xuất cho dự án trước đó chưa
                    var existingDU = _context.khoduans.FirstOrDefault(x =>
                        x.DAMaDuan == Phieuxuatkho.MaDuan &&
                        x.MaSanpham == VTxuatkho.MaSanpham &&
                        x.DAMakho == VTxuatkho.Makho
                    );

                    if (existingDU != null)
                    {
                        // cập nhật SL tăng thêm
                        existingDU.SL = (existingDU.SL ?? 0) + soLuongXuat;
                        existingDU.TrangThai = "Đã xuất kho";
                        _context.khoduans.Update(existingDU);
                    }
                    else
                    {
                        // lần đầu xuất SP này cho dự án → thêm mới
                        var VTduan = new khoduans
                        {
                            DAMaDuan = Phieuxuatkho.MaDuan,
                            TenSanpham = VTxuatkho.TenSanpham,
                            MaSanpham = VTxuatkho.MaSanpham,
                            DAMakho = VTxuatkho.Makho,
                            HangSX = VTxuatkho.HangSX,
                            NhaCC = VTxuatkho.NhaCC,
                            DonVi = VTxuatkho.DonVi,
                            SL = soLuongXuat,
                            NgayBaohanh = VTxuatkho.NgayBaohanh,
                            ThoiGianBH = VTxuatkho.ThoiGianBH,
                            TrangThai = "Đã xuất kho"
                        };

                        _context.khoduans.Add(VTduan);
                    }
                }

                else if (!string.IsNullOrEmpty(Phieuxuatkho.MaNguoidung))
                {
                    // Nếu KHÔNG có mã dự án thì cấp phát thẳng về kho cá nhân (khonguoidungs)
                    var existingItem = _context.khonguoidungs
                        .FirstOrDefault(k =>
                            k.NDMaNguoidung == Phieuxuatkho.MaNguoidung &&
                            k.MaSanpham == VTxuatkho.MaSanpham);

                    if (existingItem != null)
                    {
                        existingItem.SL = (existingItem.SL ?? 0) + soLuongXuat;
                        existingItem.TrangThai = "Đang mượn";
                        existingItem.NgayNhapkho = DateTime.Now;
                        _context.khonguoidungs.Update(existingItem);
                    }
                    else
                    {
                        var newItem = new khonguoidungs
                        {
                            NDMaNguoidung = Phieuxuatkho.MaNguoidung,
                            TenSanpham = VTxuatkho.TenSanpham,
                            MaSanpham = VTxuatkho.MaSanpham,
                            NDMakho = VTxuatkho.Makho,
                            HangSX = VTxuatkho.HangSX,
                            NhaCC = VTxuatkho.NhaCC,
                            DonVi = VTxuatkho.DonVi,
                            SL = soLuongXuat,
                            NgayBaohanh = VTxuatkho.NgayBaohanh,
                            ThoiGianBH = VTxuatkho.ThoiGianBH,
                            TrangThai = "Đang mượn",
                            NgayNhapkho = DateTime.Now
                        };
                        _context.khonguoidungs.Add(newItem);
                    }
                }

                // Đồng bộ trạng thái vật tư trong yêu cầu khi kho đã xuất
                // ⚠️ QUAN TRỌNG:
                // MaSanpham trong kho/phiếu có thể có hậu tố theo lô/NCC/ngày (vd: DS03AATTASS02-TDS-20260128),
                // trong khi vtyeucau thường lưu mã gốc (vd: DS03-AAT-TASS-02). Nếu so sánh == sẽ không khớp và
                // dẫn đến trạng thái yeucau bị tính lại sai ("Không có vật tư"...).
                var baseMaSanphamXuat = YeucauUpdateHelper.GetBaseProductCode(VTxuatkho.MaSanpham ?? "");
                var vtYeucauList = _context.vtyeucau
                    .Where(v => v.VTMaYeucau == VTxuatkho.MaYeucau)
                    .ToList()
                    .Where(v => YeucauUpdateHelper.GetBaseProductCode(v.MaSanpham ?? "") == baseMaSanphamXuat)
                    .ToList();

                // EF Core không translate được GetBaseProductCode trong query SQL,
                // nên lấy danh sách VT phiếu xuất về memory rồi mới so sánh base code.
                var vtPhieuXuatCungYc = _context.vtphieuxuatkho
                    .Where(vt => vt.MaYeucau == VTxuatkho.MaYeucau)
                    .ToList();

                foreach (var vtYc in vtYeucauList)
                {
                    bool conThieu = vtPhieuXuatCungYc.Any(vt =>
                        YeucauUpdateHelper.GetBaseProductCode(vt.MaSanpham ?? "") == baseMaSanphamXuat &&
                        vt.TrangThai != "Đã xuất kho" &&
                        (vt.SL ?? 0) > 0);

                    vtYc.TrangThai = conThieu ? "Đang chuẩn bị hàng" : "Đã xuất kho";
                    _context.vtyeucau.Update(vtYc);
                }
            }

            // ✅ Chốt nhanh trạng thái phiếu gốc khi "xuất hết" theo phiếu hiện tại.
            // Tránh phụ thuộc vào logic tổng hợp theo MaYeucau (có thể lệch do dữ liệu chi tiết).
            if (!taoPhieuMoi && xuatHet)
            {
                Phieuxuatkho.TrangThai = "Đã xuất kho";
                Phieuxuatkho.NgayHoanThanh = DateTime.Now;
                Phieuxuatkho.NgayXuatkho = DateTime.Now;
                Phieuxuatkho.GhiChu = "Đã xuất kho";
                _context.phieuxuatkho.Update(Phieuxuatkho);
            }

            // Chốt trạng thái phiếu tổng: nếu đã xuất hết theo SL gốc (ConLai == 0 cho mọi baseCode) => Đã xuất kho
            if (!string.IsNullOrWhiteSpace(Phieuxuatkho.MaYeucau))
            {
                var allRowsSameYCForClose = _context.vtphieuxuatkho
                    .AsNoTracking()
                    .Where(v => v.MaYeucau == Phieuxuatkho.MaYeucau)
                    .ToList();

                var slGocByBaseForClose = allRowsSameYCForClose
                    .GroupBy(v => YeucauUpdateHelper.GetBaseProductCode(v.MaSanpham ?? "") ?? "")
                    .ToDictionary(g => g.Key, g => g.Max(x => x.SL ?? 0), StringComparer.OrdinalIgnoreCase);

                var daXuatByBaseForClose = allRowsSameYCForClose
                    .Where(v => v.TrangThai == "Đã xuất kho" || v.TrangThai == "Hoàn thành")
                    .GroupBy(v => YeucauUpdateHelper.GetBaseProductCode(v.MaSanpham ?? "") ?? "")
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.SL ?? 0), StringComparer.OrdinalIgnoreCase);

                bool daXuatHet = slGocByBaseForClose.All(kvp =>
                {
                    var baseCode = kvp.Key ?? "";
                    var slGoc = kvp.Value;
                    var daXuat = daXuatByBaseForClose.TryGetValue(baseCode, out var dx) ? dx : 0;
                    return daXuat >= slGoc;
                });

                if (daXuatHet)
                {
                    Phieuxuatkho.TrangThai = "Đã xuất kho";
                    Phieuxuatkho.NgayHoanThanh = DateTime.Now;
                    Phieuxuatkho.NgayXuatkho = DateTime.Now;
                    Phieuxuatkho.GhiChu = "Đã xuất kho (đã xuất hết theo SL gốc)";
                    _context.phieuxuatkho.Update(Phieuxuatkho);
                }
            }

            // Tính lại trạng thái yêu cầu dựa trên các vật tư sau khi xuất
            var maYeucauList = VTphieuxuatkho
                .Select(v => v.MaYeucau)
                .Where(ma => !string.IsNullOrEmpty(ma))
                .Distinct()
                .ToList();

            foreach (var maYc in maYeucauList)
            {
                // Dùng helper để đồng bộ trạng thái yeucau (tự xử lý cả trường hợp yêu cầu nhập kho)
                YeucauUpdateHelper.DongBoTrangThaiYeuCau(_context, maYc);
            }
            _context.SaveChanges();

            // Gửi thông báo cho người yêu cầu khi xuất kho thành công
            // ⚠️ QUAN TRỌNG: Phải reload VTphieuxuatkho sau SaveChanges để lấy dữ liệu mới nhất
            var VTphieuxuatkhoAfterSave = _context.vtphieuxuatkho
                .Where(vt => vt.MaXuatkho == maXuatkhoMoi)
                .ToList();

            // Lấy danh sách tất cả các mã yêu cầu từ vật tư trong phiếu xuất kho
            var maYeucauListForNotif = VTphieuxuatkhoAfterSave
                .Select(v => v.MaYeucau)
                .Where(ma => !string.IsNullOrEmpty(ma))
                .Distinct()
                .ToList();

            Debug.WriteLine($"[TruongBPKho/Xuliphieuxuatkho] Số lượng vật tư trong phiếu: {VTphieuxuatkhoAfterSave.Count}");
            Debug.WriteLine($"[TruongBPKho/Xuliphieuxuatkho] Số mã yêu cầu tìm được: {maYeucauListForNotif.Count}");
            foreach (var maYc in maYeucauListForNotif)
            {
                Debug.WriteLine($"[TruongBPKho/Xuliphieuxuatkho] - Mã yêu cầu: {maYc}");
            }

            // Gửi email cho từng người yêu cầu
            if (maYeucauListForNotif.Any())
            {
                // Lưu các giá trị cần thiết trước khi vào Task.Run
                var maXuatkhoForEmail = maXuatkhoMoi;

                foreach (var maYc in maYeucauListForNotif)
                {
                    try
                    {
                        // Lưu giá trị để tránh closure issue
                        var maYcForEmail = maYc;

                        Debug.WriteLine($"[TruongBPKho/Xuliphieuxuatkho] Bắt đầu gửi email cho MaYeucau = {maYcForEmail}, MaXuatkho = {maXuatkhoForEmail}");
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                Debug.WriteLine($"[TruongBPKho] [Task] Bắt đầu gửi email trong Task.Run với scope mới. MaYeucau = {maYcForEmail}, MaXuatkho = {maXuatkhoForEmail}");

                                // Tạo scope mới để có DbContext và EmailService mới (tránh lỗi disposed context)
                                using (var scope = _serviceScopeFactory.CreateScope())
                                {
                                    var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();

                                    Debug.WriteLine($"[TruongBPKho] [Task] Đã tạo scope và lấy EmailService mới");
                                    Debug.WriteLine($"[TruongBPKho] [Task] Gọi SendNotificationToRequesterOnIssueAsync...");

                                    await emailService.SendNotificationToRequesterOnIssueAsync(maYcForEmail, maXuatkhoForEmail);

                                    Debug.WriteLine($"[TruongBPKho] [Task] ✅ Đã gửi xong email cho MaYeucau = {maYcForEmail}");
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[TruongBPKho] [Task] ❌ Lỗi khi gửi email cho {maYcForEmail}: {ex.Message}");
                                Debug.WriteLine($"[TruongBPKho] [Task] Stack trace: {ex.StackTrace}");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[TruongBPKho/Xuliphieuxuatkho] ❌ Lỗi tạo Task gửi email cho {maYc}: {ex.Message}");
                        Debug.WriteLine($"[TruongBPKho/Xuliphieuxuatkho] Stack trace: {ex.StackTrace}");
                    }
                }
            }
            else
            {
                Debug.WriteLine($"[TruongBPKho/Xuliphieuxuatkho] ⚠️ KHÔNG TÌM THẤY MÃ YÊU CẦU NÀO ĐỂ GỬI EMAIL!");
                Debug.WriteLine($"[TruongBPKho/Xuliphieuxuatkho] MaXuatkho = {Phieuxuatkho.MaXuatkho}");
                Debug.WriteLine($"[TruongBPKho/Xuliphieuxuatkho] Phieuxuatkho.MaYeucau = {Phieuxuatkho.MaYeucau}");
            }

            // Gửi email thông báo cho bộ phận kho khi xuất kho thành công
            if (maYeucauListForNotif.Any())
            {
                try
                {
                    // Lưu các giá trị cần thiết trước khi vào Task.Run
                    var maXuatkhoForEmail = maXuatkhoMoi;
                    var maYeucauForEmail = maYeucauListForNotif.First();

                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            Debug.WriteLine($"[TruongBPKho] [Task] Bắt đầu gửi email thông báo kho khi xuất kho. MaXuatkho = {maXuatkhoForEmail}");

                            // Tạo scope mới để có DbContext và EmailService mới (tránh lỗi disposed context)
                            using (var scope = _serviceScopeFactory.CreateScope())
                            {
                                var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();

                                Debug.WriteLine($"[TruongBPKho] [Task] Đã tạo scope và lấy EmailService mới cho thông báo kho");

                                await emailService.SendNotificationToWarehouseOnXuatKhoAsync(maXuatkhoForEmail, maYeucauForEmail);

                                Debug.WriteLine($"[TruongBPKho] [Task] ✅ Đã gửi xong email thông báo kho cho MaXuatkho = {maXuatkhoForEmail}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[TruongBPKho] [Task] ❌ Lỗi khi gửi email thông báo kho: {ex.Message}");
                            Debug.WriteLine($"[TruongBPKho] [Task] Stack trace: {ex.StackTrace}");
                        }
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TruongBPKho/Xuliphieuxuatkho] ❌ Lỗi tạo Task gửi email thông báo kho: {ex.Message}");
                    Debug.WriteLine($"[TruongBPKho/Xuliphieuxuatkho] Stack trace: {ex.StackTrace}");
                }
            }

            TempData["Success"] = taoPhieuMoi
                ? $"Xuất kho thành công! Đã tạo phiếu xuất kho mới: {maXuatkhoMoi}."
                : "Xuất kho thành công! Đã xuất hết theo phiếu hiện tại (không tạo phiếu mới).";
            return RedirectToAction("Phieuxuatkho", "Yeucau", new { area = "TruongBPKho" });
        }

        // Đồng bộ lại trạng thái vật tư với trạng thái phiếu xuất kho
        [HttpPost]
        public IActionResult DongsBoTrangThaiVatTu(string MaXuatkho)
        {
            try
            {
                var phieu = _context.phieuxuatkho.FirstOrDefault(p => p.MaXuatkho == MaXuatkho);

                if (phieu == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy phiếu xuất kho!" });
                }

                var VTphieuxuatkhoList = _context.vtphieuxuatkho
                    .Where(vt => vt.MaXuatkho == MaXuatkho)
                    .ToList();

                int updatedCount = 0;
                foreach (var vt in VTphieuxuatkhoList)
                {
                    string newTrangThai = null;

                    // Cập nhật trạng thái vật tư dựa trên trạng thái phiếu
                    if (phieu.TrangThai == "Hoàn thành")
                    {
                        // Nếu phiếu đã hoàn thành, vật tư phải là "Đã xuất kho"
                        if (vt.TrangThai != "Đã xuất kho")
                        {
                            newTrangThai = "Đã xuất kho";
                        }
                    }
                    else if (phieu.TrangThai == "Chờ người yêu cầu xác nhận")
                    {
                        // Phiếu đang chờ xác nhận, vật tư vẫn "Đang chuẩn bị hàng"
                        // Không cần cập nhật
                    }
                    else if (phieu.TrangThai == "Đang chuẩn bị hàng")
                    {
                        // Phiếu đang chuẩn bị: giữ "Đã chuẩn bị hàng xong", "Thiếu hàng- đang mua hàng"; chỉ đồng bộ các VT khác về "Đang chuẩn bị hàng"
                        var giuNguyen = new[] { "Đang chuẩn bị hàng", "Đã xuất kho", "Đã chuẩn bị hàng xong", "Thiếu hàng- đang mua hàng" };
                        if (vt.TrangThai == null || !giuNguyen.Contains(vt.TrangThai))
                        {
                            newTrangThai = "Đang chuẩn bị hàng";
                        }
                    }

                    if (newTrangThai != null)
                    {
                        vt.TrangThai = newTrangThai;
                        _context.vtphieuxuatkho.Update(vt);
                        updatedCount++;
                    }
                }

                if (updatedCount > 0)
                {
                    _context.SaveChanges();
                    return Json(new { success = true, message = $"Đã đồng bộ {updatedCount} vật tư!" });
                }
                else
                {
                    return Json(new { success = true, message = "Trạng thái vật tư đã đồng bộ!" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        [HttpPost]
        public IActionResult CapNhatTrangThaiVT(string MaXuatkho, int Id, string TrangThai)
        {
            try
            {
                var allowed = new[] { "Đang chuẩn bị hàng", "Đã chuẩn bị hàng xong", "Thiếu hàng- đang mua hàng" };
                if (string.IsNullOrEmpty(MaXuatkho) || Id <= 0 || string.IsNullOrEmpty(TrangThai) || !allowed.Contains(TrangThai))
                {
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ." });
                }

                var vt = _context.vtphieuxuatkho.FirstOrDefault(v => v.ID == Id && v.MaXuatkho == MaXuatkho);
                if (vt == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy vật tư." });
                }

                vt.TrangThai = TrangThai;
                _context.vtphieuxuatkho.Update(vt);
                _context.SaveChanges();
                return Json(new { success = true, message = "Đã cập nhật trạng thái." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        [HttpPost]
        public IActionResult XuatKhoVatTuRieng(string MaXuatkho, int VatTuId, int SoLuongThucXuat)
        {
            try
            {
                if (string.IsNullOrEmpty(MaXuatkho) || VatTuId <= 0)
                {
                    return Json(new { success = false, message = "Dữ liệu không hợp lệ." });
                }

                var vtGoc = _context.vtphieuxuatkho.FirstOrDefault(v => v.ID == VatTuId && v.MaXuatkho == MaXuatkho);
                if (vtGoc == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy vật tư." });
                }

                if (vtGoc.TrangThai == "Đã xuất kho")
                {
                    return Json(new { success = false, message = "Vật tư này đã được xuất kho." });
                }

                if (vtGoc.TrangThai != "Đã chuẩn bị hàng xong")
                {
                    return Json(new { success = false, message = "Vật tư phải ở trạng thái 'Đã chuẩn bị hàng xong' mới được xuất kho." });
                }

                var phieuGoc = _context.phieuxuatkho.FirstOrDefault(p => p.MaXuatkho == MaXuatkho);
                if (phieuGoc == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy phiếu xuất kho gốc." });
                }

                // SL gốc của phiếu (giữ nguyên để làm căn cứ)
                int slGocPhieu = vtGoc.SL ?? 0;
                if (slGocPhieu <= 0)
                {
                    return Json(new { success = false, message = "SL gốc của phiếu không hợp lệ." });
                }

                // Đã xuất (trên toàn bộ yêu cầu, theo base code) = sum SL các dòng đã xuất kho/hoàn thành
                var maYeucau = phieuGoc.MaYeucau ?? vtGoc.MaYeucau ?? "";
                var baseCode = YeucauUpdateHelper.GetBaseProductCode(vtGoc.MaSanpham ?? "") ?? "";
                // EF không translate được GetBaseProductCode -> lọc DB trước rồi tính trong memory
                var issuedRows = _context.vtphieuxuatkho
                    .AsNoTracking()
                    .Where(v =>
                        v.MaYeucau == maYeucau &&
                        (v.TrangThai == "Đã xuất kho" || v.TrangThai == "Hoàn thành"))
                    .ToList();

                int daXuat = issuedRows
                    .Where(v => (YeucauUpdateHelper.GetBaseProductCode(v.MaSanpham ?? "") ?? "") == baseCode)
                    .Sum(v => v.SL ?? 0);

                // Còn lại (trong phạm vi SL gốc của phiếu) = SL gốc - đã xuất
                int conLai = Math.Max(0, slGocPhieu - daXuat);

                if (conLai <= 0)
                {
                    return Json(new { success = false, message = "Vật tư này đã xuất đủ theo SL gốc của phiếu." });
                }
                if (SoLuongThucXuat <= 0 || SoLuongThucXuat > conLai)
                {
                    return Json(new { success = false, message = $"Cần xuất phải > 0 và ≤ Còn lại ({conLai})." });
                }

                // Trừ kho tổng theo FIFO (giống logic xuất kho tổng)
                var maSp = vtGoc.MaSanpham ?? "";
                var danhSachLoKho = _context.khotongs
                    .Where(k => k.MaSanpham == maSp)
                    .OrderBy(k => k.NgayNhapkho)
                    .ToList();

                if (!danhSachLoKho.Any())
                {
                    return Json(new { success = false, message = $"Vật tư {vtGoc.TenSanpham} không tồn tại trong kho tổng." });
                }

                int tongTon = danhSachLoKho.Sum(k => k.SL ?? 0);
                if (tongTon < SoLuongThucXuat)
                {
                    return Json(new { success = false, message = $"Không đủ tồn kho để xuất vật tư {vtGoc.TenSanpham}." });
                }

                int soLuongCanTru = SoLuongThucXuat;
                foreach (var lo in danhSachLoKho)
                {
                    if (soLuongCanTru <= 0) break;
                    var soLuongTrongLo = lo.SL ?? 0;
                    if (soLuongTrongLo <= 0) continue;

                    var tru = Math.Min(soLuongTrongLo, soLuongCanTru);
                    lo.SL = soLuongTrongLo - tru;
                    soLuongCanTru -= tru;
                    _context.khotongs.Update(lo);
                }

                // Tạo mã phiếu xuất kho mới bằng service
                string maXuatkhoMoi = _phieuCodeService.GenerateMaXuatKho(phieuGoc.MaDuan, phieuGoc.MaYeucau);

                var phieuMoi = new phieuxuatkho
                {
                    MaXuatkho = maXuatkhoMoi,
                    MaYeucau = phieuGoc.MaYeucau,
                    MaDuan = phieuGoc.MaDuan,
                    MaNguoidung = phieuGoc.MaNguoidung,
                    NgayXuatkho = DateTime.Now,
                    NgayChuanBi = DateTime.Now,
                    TrangThai = "Đã xuất kho",
                    GhiChu = $"Tạo từ phiếu {phieuGoc.MaXuatkho} (xuất từng phần)"
                };
                _context.phieuxuatkho.Add(phieuMoi);

                decimal? donGia = vtGoc.DonGia;
                if (!donGia.HasValue && vtGoc.ThanhTien.HasValue && slGocPhieu > 0)
                {
                    donGia = vtGoc.ThanhTien.Value / slGocPhieu;
                }
                decimal? thanhTienXuat = null;
                if (donGia.HasValue)
                {
                    thanhTienXuat = donGia.Value * SoLuongThucXuat;
                }
                else if (vtGoc.ThanhTien.HasValue && slGocPhieu > 0)
                {
                    thanhTienXuat = (vtGoc.ThanhTien.Value / slGocPhieu) * SoLuongThucXuat;
                }

                var vtMoi = new vtphieuxuatkho
                {
                    MaXuatkho = maXuatkhoMoi,
                    MaYeucau = vtGoc.MaYeucau,
                    TenSanpham = vtGoc.TenSanpham,
                    MaSanpham = vtGoc.MaSanpham,
                    Makho = vtGoc.Makho,
                    HangSX = vtGoc.HangSX,
                    NhaCC = vtGoc.NhaCC,
                    // SL trên phiếu con = số lượng THỰC XUẤT lần này
                    SL = SoLuongThucXuat,
                    DonVi = vtGoc.DonVi,
                    DonGia = donGia,
                    ThanhTien = thanhTienXuat,
                    NgayNhapkho = vtGoc.NgayNhapkho,
                    NgayBaohanh = vtGoc.NgayBaohanh,
                    ThoiGianBH = vtGoc.ThoiGianBH,
                    TrangThai = "Đã xuất kho",
                    LoaiCapPhat = vtGoc.LoaiCapPhat
                };
                _context.vtphieuxuatkho.Add(vtMoi);

                // Cộng kho dự án / kho người dùng theo số lượng thực xuất
                if (!string.IsNullOrEmpty(phieuGoc.MaDuan))
                {
                    var existingDU = _context.khoduans.FirstOrDefault(x =>
                        x.DAMaDuan == phieuGoc.MaDuan &&
                        x.MaSanpham == vtGoc.MaSanpham &&
                        x.DAMakho == vtGoc.Makho);

                    if (existingDU != null)
                    {
                        existingDU.SL = (existingDU.SL ?? 0) + SoLuongThucXuat;
                        existingDU.TrangThai = "Đã xuất kho";
                        _context.khoduans.Update(existingDU);
                    }
                    else
                    {
                        _context.khoduans.Add(new khoduans
                        {
                            DAMaDuan = phieuGoc.MaDuan,
                            TenSanpham = vtGoc.TenSanpham,
                            MaSanpham = vtGoc.MaSanpham,
                            DAMakho = vtGoc.Makho,
                            HangSX = vtGoc.HangSX,
                            NhaCC = vtGoc.NhaCC,
                            DonVi = vtGoc.DonVi,
                            SL = SoLuongThucXuat,
                            NgayBaohanh = vtGoc.NgayBaohanh,
                            ThoiGianBH = vtGoc.ThoiGianBH,
                            TrangThai = "Đã xuất kho"
                        });
                    }
                }
                else if (!string.IsNullOrEmpty(phieuGoc.MaNguoidung))
                {
                    var existingItem = _context.khonguoidungs
                        .FirstOrDefault(k =>
                            k.NDMaNguoidung == phieuGoc.MaNguoidung &&
                            k.MaSanpham == vtGoc.MaSanpham);

                    if (existingItem != null)
                    {
                        existingItem.SL = (existingItem.SL ?? 0) + SoLuongThucXuat;
                        existingItem.TrangThai = "Đang mượn";
                        existingItem.NgayNhapkho = DateTime.Now;
                        _context.khonguoidungs.Update(existingItem);
                    }
                    else
                    {
                        _context.khonguoidungs.Add(new khonguoidungs
                        {
                            NDMaNguoidung = phieuGoc.MaNguoidung,
                            TenSanpham = vtGoc.TenSanpham,
                            MaSanpham = vtGoc.MaSanpham,
                            NDMakho = vtGoc.Makho,
                            HangSX = vtGoc.HangSX,
                            NhaCC = vtGoc.NhaCC,
                            DonVi = vtGoc.DonVi,
                            SL = SoLuongThucXuat,
                            NgayBaohanh = vtGoc.NgayBaohanh,
                            ThoiGianBH = vtGoc.ThoiGianBH,
                            TrangThai = "Đang mượn",
                            NgayNhapkho = DateTime.Now
                        });
                    }
                }

                // Không cập nhật vtGoc.SL (SL gốc phải giữ nguyên để làm căn cứ).
                // Đã xuất / Còn lại sẽ được tính theo tổng các dòng đã xuất kho.

                _context.SaveChanges();

                // Gửi email thông báo khi xuất kho từng phần (từng vật tư)
                try
                {
                    var maYcForEmail = phieuGoc.MaYeucau ?? vtGoc.MaYeucau;
                    var maXkForEmail = maXuatkhoMoi;

                    if (!string.IsNullOrEmpty(maYcForEmail))
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                using (var scope = _serviceScopeFactory.CreateScope())
                                {
                                    var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                                    await emailService.SendNotificationToRequesterOnIssueAsync(maYcForEmail, maXkForEmail);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[TruongBPKho/XuatKhoVatTuRieng] ❌ Lỗi gửi email người yêu cầu: {ex.Message}");
                            }
                        });

                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                using (var scope = _serviceScopeFactory.CreateScope())
                                {
                                    var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                                    await emailService.SendNotificationToWarehouseOnXuatKhoAsync(maXkForEmail, maYcForEmail);
                                }
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[TruongBPKho/XuatKhoVatTuRieng] ❌ Lỗi gửi email kho: {ex.Message}");
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TruongBPKho/XuatKhoVatTuRieng] ❌ Lỗi tạo task gửi email: {ex.Message}");
                }

                // Chốt trạng thái phiếu tổng (phiếu gốc) nếu đã xuất hết theo SL gốc (ConLai == 0 cho mọi baseCode)
                if (phieuGoc != null && !string.IsNullOrWhiteSpace(phieuGoc.MaYeucau))
                {
                    var allRowsSameYCForClose = _context.vtphieuxuatkho
                        .AsNoTracking()
                        .Where(v => v.MaYeucau == phieuGoc.MaYeucau)
                        .ToList();

                    var slGocByBaseForClose = allRowsSameYCForClose
                        .GroupBy(v => YeucauUpdateHelper.GetBaseProductCode(v.MaSanpham ?? "") ?? "")
                        .ToDictionary(g => g.Key, g => g.Max(x => x.SL ?? 0), StringComparer.OrdinalIgnoreCase);

                    var daXuatByBaseForClose = allRowsSameYCForClose
                        .Where(v => v.TrangThai == "Đã xuất kho" || v.TrangThai == "Hoàn thành")
                        .GroupBy(v => YeucauUpdateHelper.GetBaseProductCode(v.MaSanpham ?? "") ?? "")
                        .ToDictionary(g => g.Key, g => g.Sum(x => x.SL ?? 0), StringComparer.OrdinalIgnoreCase);

                    bool daXuatHet = slGocByBaseForClose.All(kvp =>
                    {
                        var baseCode = kvp.Key ?? "";
                        var slGoc = kvp.Value;
                        var daXuat = daXuatByBaseForClose.TryGetValue(baseCode, out var dx) ? dx : 0;
                        return daXuat >= slGoc;
                    });

                    if (daXuatHet && phieuGoc.TrangThai != "Đã xuất kho")
                    {
                        phieuGoc.TrangThai = "Đã xuất kho";
                        phieuGoc.NgayHoanThanh = DateTime.Now;
                        phieuGoc.NgayXuatkho = DateTime.Now;
                        phieuGoc.GhiChu = "Đã xuất kho (đã xuất hết theo SL gốc)";
                        _context.phieuxuatkho.Update(phieuGoc);
                        _context.SaveChanges();
                    }
                }

                return Json(new { success = true, message = "Đã tạo phiếu xuất kho mới thành công.", maXuatkhoMoi = maXuatkhoMoi });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        private bool IsCaseBaseCodeDuan(phieunhapkho phieuNhapKho)
        {
            // Dự án + có mã yêu cầu
            if (string.IsNullOrEmpty(phieuNhapKho.MaDuan))
                return false;

            if (string.IsNullOrEmpty(phieuNhapKho.MaYeucau))
                return false;


            return true;
        }
        private int TinhSoLuongThieuTheoBaseCode(
    List<vtyeucau> allVTYeucau,
    string maSanPham,
    List<string> allRelatedMaYeucau
)
        {
            // 1. MAX nhu cầu
            int tongNhuCau = allVTYeucau
                .Select(v => Math.Max(v.SLMoi ?? 0, v.SL ?? 0))
                .DefaultIfEmpty(0)
                .Max();

            // 2. SUM đã xuất
            int tongDaXuat = _context.vtphieuxuatkho
                .Where(v =>
                    allRelatedMaYeucau.Contains(v.MaYeucau) &&
                    v.MaSanpham == maSanPham &&
                    (v.TrangThai == "Đã xuất kho"
                     || v.TrangThai == "Hoàn thành"))
                .Sum(v => (int?)v.SL) ?? 0;

            // 3. Thiếu thực tế
            return Math.Max(0, tongNhuCau - tongDaXuat);
        }

        private int TinhSoLuongDaCamKet(string makho, string masanpham, string maXuatkhoHienTai = null)
        {
            // Lấy tất cả các phiếu xuất có trạng thái đã duyệt nhưng chưa giao (chưa trừ kho)
            var cacTrangThaiDaCamKet = new[] { "Đang chuẩn bị hàng", "Chờ người yêu cầu xác nhận" };

            var phieuXuatDaCamKet = _context.phieuxuatkho
                .Where(px => cacTrangThaiDaCamKet.Contains(px.TrangThai))
                .Select(px => px.MaXuatkho)
                .ToList();

            // Nếu có phiếu xuất hiện tại, loại trừ nó khỏi danh sách (vì đang kiểm tra cho chính nó)
            if (!string.IsNullOrEmpty(maXuatkhoHienTai))
            {
                phieuXuatDaCamKet = phieuXuatDaCamKet
                    .Where(mx => mx != maXuatkhoHienTai)
                    .ToList();
            }

            // Tính tổng số lượng vật tư đã cam kết từ các phiếu xuất này
            // Ưu tiên khớp chính xác Makho; nếu không có thì cho phép khớp linh hoạt theo tiền tố/hậu tố
            var tongSoLuongDaCamKet = _context.vtphieuxuatkho
                .Where(vt => phieuXuatDaCamKet.Contains(vt.MaXuatkho)
                    && vt.MaSanpham == masanpham
                    && (
                        vt.Makho == makho
                        || ((vt.Makho ?? "") != "" && (makho ?? "") != "" &&
                            (((vt.Makho ?? "").StartsWith(makho ?? "")) || ((makho ?? "").StartsWith(vt.Makho ?? ""))))
                    ))
                .Sum(vt => vt.SL ?? 0);

            return tongSoLuongDaCamKet;
        }

        private int TinhSoLuongDaCamKetTheoMaSanpham(string masanpham, string maXuatkhoHienTai = null)
        {
            // Lấy tất cả các phiếu xuất có trạng thái đã duyệt nhưng chưa giao (chưa trừ kho)
            var cacTrangThaiDaCamKet = new[] { "Đang chuẩn bị hàng", "Chờ người yêu cầu xác nhận" };

            var phieuXuatDaCamKet = _context.phieuxuatkho
                .Where(px => cacTrangThaiDaCamKet.Contains(px.TrangThai))
                .Select(px => px.MaXuatkho)
                .ToList();

            // Nếu có phiếu xuất hiện tại, loại trừ nó khỏi danh sách (vì đang kiểm tra cho chính nó)
            if (!string.IsNullOrEmpty(maXuatkhoHienTai))
            {
                phieuXuatDaCamKet = phieuXuatDaCamKet
                    .Where(mx => mx != maXuatkhoHienTai)
                    .ToList();
            }

            // Tính tổng số lượng vật tư đã cam kết từ các phiếu xuất này theo mã sản phẩm
            var tongSoLuongDaCamKet = _context.vtphieuxuatkho
                .Where(vt => phieuXuatDaCamKet.Contains(vt.MaXuatkho)
                             && vt.MaSanpham == masanpham)
                .Sum(vt => vt.SL ?? 0);

            return tongSoLuongDaCamKet;
        }

        /// <summary>
        /// Sinh mã phiếu tự động với prefix và danh sách mã hiện có.
        /// Đảm bảo không trùng mã trong cơ sở dữ liệu.
        /// </summary>
        private string TaoMaTuDong(string prefix, IEnumerable<string> existingCodesQuery)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                prefix = "";
            }

            // Materialize để tránh nhiều lần query DB
            var existingCodes = (existingCodesQuery ?? Enumerable.Empty<string>())
                .Where(c => !string.IsNullOrEmpty(c))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            int nextNumber = 1;
            while (true)
            {
                string candidate = PhieuHelper.ChuanHoaMaPhieu($"{prefix}{nextNumber}");
                if (!existingCodes.Contains(candidate))
                {
                    return candidate;
                }

                nextNumber++;
            }
        }

        // Method tự động tạo phiếu mua hàng khi thiếu hàng
        private void TaoPhieuMuaHangTuDong(phieuxuatkho phieuxuatkho, List<vtphieuxuatkho> vatTuThieu)
        {
            try
            {
                // Tạo mã phiếu mua hàng duy nhất bằng service
                string MaMuahang = _phieuCodeService.GenerateMaMuaHang(phieuxuatkho.MaDuan, phieuxuatkho.MaYeucau);

                // Tạo phiếu mua hàng
                var phieuMuaHang = new phieumuahang
                {
                    MaMuahang = MaMuahang,
                    MaYeucau = phieuxuatkho.MaYeucau,
                    MaDuan = phieuxuatkho.MaDuan,
                    MaNguoidung = phieuxuatkho.MaNguoidung,
                    NgayTao = DateTime.Now,
                    TrangThai = "Chờ duyệt",
                    GhiChu = $"Tự động tạo từ phiếu xuất kho {phieuxuatkho.MaXuatkho} do thiếu hàng"
                };
                _context.phieumuahang.Add(phieuMuaHang);

                // Tạo chi tiết vật tư mua hàng cho những vật tư thiếu
                foreach (var vt in vatTuThieu)
                {
                    var vtPhieuMuaHang = new vtphieumuahang
                    {
                        MaMuahang = MaMuahang,
                        MaYeucau = vt.MaYeucau,
                        TenSanpham = vt.TenSanpham,
                        MaSanpham = vt.MaSanpham,
                        Makho = vt.Makho,
                        HangSX = vt.HangSX,
                        NhaCC = vt.NhaCC,
                        SL = vt.SL,
                        DonVi = vt.DonVi,
                        TrangThai = "Chờ mua",
                        GhiChu = $"Số lượng thiếu: {vt.SL}"
                    };
                    _context.vtphieumuahang.Add(vtPhieuMuaHang);
                }

                _context.SaveChanges();
            }
            catch (Exception ex)
            {
                // Log lỗi nếu cần
                Console.WriteLine($"Lỗi khi tạo phiếu mua hàng tự động: {ex.Message}");
            }
        }

        private void CapNhatPhieuXuatSauNhapHang(phieuxuatkho phieuXuat, List<vtphieunhapkho> vtNhapList)
        {
            PhieuXuatAllocationHelper.CapNhatPhieuXuatSauNhapHang(_context, phieuXuat, vtNhapList);
        }




        private void KiemTraVaCapNhatPhieuXuatKhoThieuHang()
        {
            try
            {
                // Lấy tất cả các phiếu xuất kho có trạng thái "Thiếu hàng - Đã tạo phiếu mua"
                var phieuXuatKhoThieuHang = _context.phieuxuatkho
                    .Where(px => px.TrangThai == "Thiếu hàng - Đã tạo phiếu mua")
                    .ToList();

                foreach (var phieuXuat in phieuXuatKhoThieuHang)
                {
                    // Lấy danh sách vật tư trong phiếu xuất kho
                    var VTphieuxuatkhoList = _context.vtphieuxuatkho
                        .Where(vt => vt.MaXuatkho == phieuXuat.MaXuatkho)
                        .ToList();

                    if (!VTphieuxuatkhoList.Any())
                    {
                        continue;
                    }

                    // Lấy danh sách vật tư yêu cầu ban đầu (nếu có)
                    var vtYeuCauList = string.IsNullOrEmpty(phieuXuat.MaYeucau)
                        ? new List<vtyeucau>()
                        : _context.vtyeucau
                            .Where(vt => vt.VTMaYeucau == phieuXuat.MaYeucau)
                            .ToList();

                    bool duHang = true;
                    var vatTuNhom = VTphieuxuatkhoList
                        .GroupBy(vt => new { MaSanpham = vt.MaSanpham ?? "", Makho = vt.Makho ?? "" })
                        .ToList();

                    foreach (var nhom in vatTuNhom)
                    {
                        var maSanpham = nhom.Key.MaSanpham;
                        var makho = nhom.Key.Makho;
                        var dongDauTien = nhom.First();

                        var vtYeuCau = vtYeuCauList.FirstOrDefault(vt =>
                            string.Equals(vt.MaSanpham, maSanpham, StringComparison.OrdinalIgnoreCase) &&
                            string.Equals(vt.YCMakho, makho, StringComparison.OrdinalIgnoreCase))
                            ?? vtYeuCauList.FirstOrDefault(vt =>
                                string.Equals(vt.MaSanpham, maSanpham, StringComparison.OrdinalIgnoreCase));

                        int soLuongYeuCauBanDau = vtYeuCau?.SL ?? nhom.Sum(vt => vt.SL ?? 0);
                        int soLuongDaXuat = nhom
                            .Where(vt => vt.TrangThai == "Đã xuất kho")
                            .Sum(vt => vt.SL ?? 0);
                        int soLuongConLaiCanXuat = Math.Max(0, soLuongYeuCauBanDau - soLuongDaXuat);

                        // Tính tổng số lượng tồn kho
                        int tongSoLuongTonKho = _context.khotongs
                            .Where(k => k.Makho == makho && k.MaSanpham == maSanpham)
                            .Sum(k => k.SL ?? 0);

                        // Nếu không tìm thấy theo Makho chính xác, thử tìm theo MaSanpham + HangSX
                        if (tongSoLuongTonKho == 0 && !string.IsNullOrEmpty(dongDauTien.HangSX))
                        {
                            tongSoLuongTonKho = _context.khotongs
                                .Where(k => k.MaSanpham == maSanpham && k.HangSX == dongDauTien.HangSX)
                                .Sum(k => k.SL ?? 0);
                        }

                        // Tính số lượng đã cam kết
                        int soLuongDaCamKet = this.TinhSoLuongDaCamKet(makho, maSanpham, phieuXuat.MaXuatkho);
                        int soLuongKhaDung = tongSoLuongTonKho - soLuongDaCamKet;

                        if (soLuongConLaiCanXuat > 0 && (tongSoLuongTonKho <= 0 || soLuongKhaDung < soLuongConLaiCanXuat))
                        {
                            duHang = false;
                            break;
                        }
                    }

                    // Nếu đủ hàng, chuyển trạng thái sang "Đang chuẩn bị hàng"
                    if (duHang)
                    {
                        phieuXuat.TrangThai = "Đang chuẩn bị hàng";
                        phieuXuat.NgayChuanBi = DateTime.Now;
                        phieuXuat.GhiChu = null;
                        _context.phieuxuatkho.Update(phieuXuat);

                        // Cập nhật trạng thái vật tư
                        foreach (var vt in VTphieuxuatkhoList)
                        {
                            if (vt.TrangThai != "Đã xuất kho")
                            {
                                vt.TrangThai = "Đang chuẩn bị hàng";
                                _context.vtphieuxuatkho.Update(vt);
                            }
                        }

                        _context.SaveChanges();
                        Console.WriteLine($"Đã tự động chuyển phiếu xuất kho {phieuXuat.MaXuatkho} từ 'Thiếu hàng - Đã tạo phiếu mua' sang 'Đang chuẩn bị hàng' sau khi nhập kho");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi kiểm tra và cập nhật phiếu xuất kho thiếu hàng: {ex.Message}");
            }
        }

        [HttpPost]
        public IActionResult TaoPhieuMuaHangChoNhanVienMuahang(string MaXuatkho)
        {
            try
            {
                // Lấy phiếu xuất kho
                var phieuxuatkho = _context.phieuxuatkho
                    .FirstOrDefault(p => p.MaXuatkho == MaXuatkho);

                if (phieuxuatkho == null)
                {
                    TempData["Error"] = "Không tìm thấy phiếu xuất kho!";
                    return RedirectToAction("Phieuxuatkho", "Yeucau", new { area = "TruongBPKho" });
                }

                // Kiểm tra trạng thái
                if (phieuxuatkho.TrangThai != "Thiếu hàng - Đã tạo phiếu mua")
                {
                    TempData["Error"] = "Chỉ có thể tạo phiếu mua hàng khi phiếu ở trạng thái 'Thiếu hàng - Đã tạo phiếu mua'.";
                    return RedirectToAction("Phieuxuatkho", "Yeucau", new { area = "TruongBPKho" });
                }

                // Lấy tất cả vật tư trong phiếu xuất kho
                var VTphieuxuatkhoList = _context.vtphieuxuatkho
                    .Where(vt => vt.MaXuatkho == MaXuatkho)
                    .ToList();

                if (!VTphieuxuatkhoList.Any())
                {
                    TempData["Error"] = "Phiếu xuất kho không có vật tư nào!";
                    return RedirectToAction("Phieuxuatkho", "Yeucau", new { area = "TruongBPKho" });
                }

                // Xác định vật tư thiếu
                var vatTuThieu = new List<vtphieuxuatkho>();

                foreach (var VTxuatkho in VTphieuxuatkhoList)
                {
                    var khotong = _context.khotongs.FirstOrDefault(k =>
                        k.Makho == VTxuatkho.Makho &&
                        k.MaSanpham == VTxuatkho.MaSanpham);

                    // Tính số lượng hàng đã cam kết
                    int soLuongDaCamKet = this.TinhSoLuongDaCamKet(VTxuatkho.Makho ?? "", VTxuatkho.MaSanpham ?? "", MaXuatkho);

                    // Số lượng khả dụng = Tồn kho - Số lượng đã cam kết
                    int soLuongKhaDung = (khotong?.SL ?? 0) - soLuongDaCamKet;
                    int soLuongCanXuat = VTxuatkho.SL ?? 0;

                    // Nếu không đủ hàng, thêm vào danh sách thiếu
                    if (khotong == null || soLuongKhaDung <= 0 || soLuongKhaDung < soLuongCanXuat)
                    {
                        // Tính số lượng thiếu chính xác
                        int soLuongThieu;
                        if (khotong == null || (khotong.SL ?? 0) <= 0 || soLuongKhaDung <= 0)
                        {
                            // Không có hàng trong kho → cần mua toàn bộ số lượng
                            soLuongThieu = soLuongCanXuat;
                        }
                        else
                        {
                            // Có hàng nhưng không đủ → cần mua phần thiếu
                            soLuongThieu = soLuongCanXuat - soLuongKhaDung;
                        }

                        // Tạo bản sao vật tư với số lượng thiếu
                        var vtThieu = new vtphieuxuatkho
                        {
                            MaXuatkho = VTxuatkho.MaXuatkho,
                            MaYeucau = VTxuatkho.MaYeucau,
                            TenSanpham = VTxuatkho.TenSanpham,
                            MaSanpham = VTxuatkho.MaSanpham,
                            Makho = VTxuatkho.Makho,
                            HangSX = VTxuatkho.HangSX,
                            NhaCC = VTxuatkho.NhaCC,
                            SL = soLuongThieu,
                            DonVi = VTxuatkho.DonVi,
                            TrangThai = VTxuatkho.TrangThai
                        };
                        vatTuThieu.Add(vtThieu);
                    }
                }

                if (!vatTuThieu.Any())
                {
                    TempData["Error"] = "Không có vật tư nào thiếu để tạo phiếu mua hàng!";
                    return RedirectToAction("Phieuxuatkho", "Yeucau", new { area = "TruongBPKho" });
                }

                // Kiểm tra xem đã có phiếu mua hàng với trạng thái "Đang chờ báo giá" chưa
                var phieuMuaHangDaTao = _context.phieumuahang
                    .FirstOrDefault(p => p.MaYeucau == phieuxuatkho.MaYeucau &&
                                         p.TrangThai == "Đang chờ báo giá" &&
                                         p.GhiChu != null &&
                                         p.GhiChu.Contains($"phiếu xuất kho {MaXuatkho}"));

                if (phieuMuaHangDaTao != null)
                {
                    TempData["Info"] = $"Đã tồn tại phiếu mua hàng {phieuMuaHangDaTao.MaMuahang} cho phiếu xuất kho này!";
                    return RedirectToAction("Phieuxuatkho", "Yeucau", new { area = "TruongBPKho" });
                }

                // Tạo mã phiếu mua hàng duy nhất bằng service
                string MaMuahang = _phieuCodeService.GenerateMaMuaHang(phieuxuatkho.MaDuan, phieuxuatkho.MaYeucau);

                // Tạo phiếu mua hàng với trạng thái "Đang chờ báo giá" cho NhanvienMuahang
                var phieuMuaHang = new phieumuahang
                {
                    MaMuahang = MaMuahang,
                    MaYeucau = phieuxuatkho.MaYeucau,
                    MaDuan = phieuxuatkho.MaDuan,
                    MaNguoidung = phieuxuatkho.MaNguoidung,
                    NgayTao = DateTime.Now,
                    TrangThai = "Đang chờ báo giá",
                    GhiChu = $"Tạo từ phiếu xuất kho {phieuxuatkho.MaXuatkho} do thiếu hàng - Dành cho nhân viên mua hàng"
                };
                _context.phieumuahang.Add(phieuMuaHang);

                // Tạo chi tiết vật tư mua hàng cho những vật tư thiếu
                foreach (var vt in vatTuThieu)
                {
                    var vtPhieuMuaHang = new vtphieumuahang
                    {
                        MaMuahang = MaMuahang,
                        MaYeucau = vt.MaYeucau,
                        TenSanpham = vt.TenSanpham,
                        MaSanpham = vt.MaSanpham,
                        Makho = vt.Makho,
                        HangSX = vt.HangSX,
                        NhaCC = vt.NhaCC,
                        SL = vt.SL,
                        DonVi = vt.DonVi,
                        TrangThai = "Đang chờ báo giá",
                        GhiChu = $"Số lượng thiếu: {vt.SL} - Từ phiếu xuất kho {MaXuatkho}"
                    };
                    _context.vtphieumuahang.Add(vtPhieuMuaHang);
                }

                _context.SaveChanges();

                TempData["Success"] = $"Đã tạo phiếu mua hàng {MaMuahang} cho nhân viên mua hàng thành công!";
                return RedirectToAction("Phieuxuatkho", "Yeucau", new { area = "TruongBPKho" });
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi khi tạo phiếu mua hàng: {ex.Message}";
                return RedirectToAction("Phieuxuatkho", "Yeucau", new { area = "TruongBPKho" });
            }
        }

        [HttpPost]
        public IActionResult ThemPhieumuahangSQL([FromBody] Phieumuahangviewmodel model)
        {
            try
            {
                var MaMuahang = model.MaMuahang;
                Console.WriteLine($"MaMuahang nhận được: {MaMuahang}");

                var Phieumuahang = _context.phieumuahang
                                            .FirstOrDefault(y => y.MaMuahang == MaMuahang);
                if (Phieumuahang == null)
                {
                    Console.WriteLine("Không tìm thấy Phieumuahang.");
                    return Json(new { success = false, message = "Mã mua hàng không tồn tại!" });
                }

                Phieumuahang.TrangThai = "Đã báo giá";
                _context.phieumuahang.Update(Phieumuahang);

                var VTPhieumuahanglist = _context.vtphieumuahang
                                                  .Where(kt => kt.MaMuahang == MaMuahang)
                                                  .ToList();

                Console.WriteLine($"Số vật tư được tìm thấy: {VTPhieumuahanglist.Count}");
                Console.WriteLine($"Số lượng phần tử trong VTphieumuahang: {model.VTphieumuahang?.Count ?? 0}");

                for (int i = 0; i < VTPhieumuahanglist.Count; i++)
                {
                    var VTmuahang = VTPhieumuahanglist[i];

                    // Kiểm tra nếu trong model.VTphieumuahang có phần tử tại cùng vị trí
                    if (i < model.VTphieumuahang.Count)
                    {
                        var updatedVTmuahang = model.VTphieumuahang[i];

                        Console.WriteLine($"Cập nhật VTmuahang: {updatedVTmuahang.MaMuahang}");

                        // Cập nhật giá trị DonGia và ThanhTien
                        VTmuahang.DonGia = updatedVTmuahang.DonGia;
                        VTmuahang.ThanhTien = updatedVTmuahang.ThanhTien;

                        Console.WriteLine($"Đơn giá là: {updatedVTmuahang.DonGia}");
                        Console.WriteLine($"Thành tiền là: {updatedVTmuahang.ThanhTien}");

                        VTmuahang.TrangThai = "Đã báo giá";
                        _context.vtphieumuahang.Update(VTmuahang);
                    }
                    else
                    {
                        Console.WriteLine($"Không có dữ liệu tương ứng trong model cho VTmuahang tại index: {i}");
                    }
                }

                _context.SaveChanges();



                return Json(new { success = true, message = "Dữ liệu đã được gửi thành công!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi: {ex.Message}");
                return Json(new { success = false, message = ex.Message });
            }
        }




        [HttpPost]
        public IActionResult XuLyPhieumuahang(string MaMuahang, string action, phieumuahang phieumuahang, vtphieumuahang vtphieumuahang, phieunhapkho phieunhapkho, vtphieunhapkho vtphieunhapkho)
        {
            var chucVu2 = HttpContext.Session.GetString("Chucvu");
            var boPhan2 = HttpContext.Session.GetString("Bophan");
            var maNv2 = HttpContext.Session.GetString("MaNguoidung");

            if (action == "approve")
            {
                Console.WriteLine($"MaMuahang nhận được: {MaMuahang}");
                var Phieumuahang = _context.phieumuahang.FirstOrDefault(p => p.MaMuahang == MaMuahang);
                var VTPhieumuahanglist = _context.vtphieumuahang.Where(vt => vt.MaMuahang == MaMuahang).ToList();
                if (chucVu2 == "Giám đốc")
                {
                    Phieumuahang.TrangThai = "Chờ thanh toán";
                }
                else if (boPhan2 == "BP kế toán")
                {
                    Phieumuahang.TrangThai = "Đã thanh toán";
                }
                else if (boPhan2 == "BP mua hàng")
                {
                    Phieumuahang.TrangThai = "Đã nhận hàng";
                    Taophieunhapkhobyphieumuahang(MaMuahang, phieunhapkho, vtphieunhapkho, phieumuahang, vtphieumuahang);
                }
                foreach (var VTPhieumuahang in VTPhieumuahanglist)
                {
                    if (chucVu2 == "Giám đốc")
                    {
                        VTPhieumuahang.TrangThai = "Chờ thanh toán";
                    }
                    else if (boPhan2 == "BP kế toán")
                    {
                        VTPhieumuahang.TrangThai = "Đã thanh toán";
                    }
                    else if (boPhan2 == "BP mua hàng")
                    {
                        VTPhieumuahang.TrangThai = "Đã nhận hàng";
                    }
                    _context.vtphieumuahang.Update(VTPhieumuahang);
                }
                _context.phieumuahang.Update(Phieumuahang);
            }
            else if (action == "reject")
            {
                Xulituchoiyeucau(MaMuahang, null, null, phieumuahang, vtphieumuahang);
            }
            _context.SaveChanges();
            return RedirectToAction("Phieumuahang", "Yeucau", new { area = "TruongBPKho" });
        }


        private int TinhSoLuongConThieu(string maYeucau, string maSanpham)
        {
            if (string.IsNullOrWhiteSpace(maYeucau) || string.IsNullOrWhiteSpace(maSanpham))
                return 0;

            // ⭐ SỬA: Sử dụng hàm tính theo mã yêu cầu cơ bản
            int conThieu = YeucauUpdateHelper.TinhSoLuongConThieuTheoMaYeuCauCoBan(_context, maYeucau, maSanpham);

            // Log để debug
            Console.WriteLine($"🔍 TinhSoLuongConThieu: MaYeucau={maYeucau}, MaSanpham={maSanpham}");
            Console.WriteLine($"   => ConThieu={conThieu} (tính theo mã yêu cầu cơ bản)");

            return conThieu;
        }


        private int TinhTongDaXuat(string maYeucau, string maSanpham)
        {
            if (string.IsNullOrWhiteSpace(maYeucau) || string.IsNullOrWhiteSpace(maSanpham))
                return 0;

            // Gom theo mã yêu cầu cơ bản để tránh cộng trùng nhiều yêu cầu cùng base
            string baseCode = YeucauUpdateHelper.GetBaseRequestCode(maYeucau);
            var relatedMaYc = _context.yeucau
                .Where(y => !string.IsNullOrWhiteSpace(y.MaYeucau))
                .ToList()
                .Where(y => string.Equals(YeucauUpdateHelper.GetBaseRequestCode(y.MaYeucau), baseCode, StringComparison.OrdinalIgnoreCase))
                .Select(y => y.MaYeucau)
                .ToList();

            if (!relatedMaYc.Any())
                relatedMaYc = new List<string> { maYeucau };

            var trangThaiTinhXuat = new[]
            {
                "Đã xuất kho",
                "Hoàn thành",
                "Đã lấy hàng"
            };

            var tongDaXuat = _context.vtphieuxuatkho
                .Where(v => relatedMaYc.Contains(v.MaYeucau ?? "")
                            && string.Equals(v.MaSanpham, maSanpham, StringComparison.OrdinalIgnoreCase))
                .ToList()
                .Where(v => !string.IsNullOrEmpty(v.TrangThai)
                            && trangThaiTinhXuat.Contains(v.TrangThai, StringComparer.OrdinalIgnoreCase))
                .Sum(v => (int?)v.SL) ?? 0;

            return tongDaXuat;
        }


        private void PhanBoHangNhapTheoYeuCau(string maYeucauChinh, string maSanpham, int soLuongNhap, vtphieunhapkho vtPhieunhapkho, string maNhapkho = "")
        {
            if (string.IsNullOrWhiteSpace(maYeucauChinh) || string.IsNullOrWhiteSpace(maSanpham) || soLuongNhap <= 0)
            {
                Console.WriteLine($"⚠️ PhanBoHangNhapTheoYeuCau: Bỏ qua do dữ liệu không hợp lệ - MaYeucauChinh={maYeucauChinh}, MaSanpham={maSanpham}, SoLuongNhap={soLuongNhap}");
                return;
            }

            // Lấy mã yêu cầu cơ bản (bỏ phần tên người)
            string maYeuCauCoBan = YeucauUpdateHelper.GetBaseRequestCode(maYeucauChinh);

            if (string.IsNullOrWhiteSpace(maYeuCauCoBan))
            {
                Console.WriteLine($"⚠️ PhanBoHangNhapTheoYeuCau: Không thể lấy mã yêu cầu cơ bản từ {maYeucauChinh}");
                return;
            }

            Console.WriteLine($"🔄 BẮT ĐẦU PhanBoHangNhapTheoYeuCau: MaYeucauChinh={maYeucauChinh}, MaYeuCauCoBan={maYeuCauCoBan}, MaSanpham={maSanpham}, SoLuongNhap={soLuongNhap}");

            // 1. Lấy danh sách tất cả yêu cầu có cùng mã cơ bản
            // Ví dụ: "251005 STUP10.5013 QuynhTT" và "251005 STUP10.5013 Phuongnm" 
            // đều có mã cơ bản là "251005 STUP10.5013" → được nhóm lại
            var tatCaYeuCau = _context.yeucau.ToList();
            var danhSachYeuCauCungCoBan = tatCaYeuCau
                .Where(y => !string.IsNullOrWhiteSpace(y.MaYeucau))
                .ToList()
                .Where(y => string.Equals(
                    YeucauUpdateHelper.GetBaseRequestCode(y.MaYeucau),
                    maYeuCauCoBan,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!danhSachYeuCauCungCoBan.Any())
            {
                Console.WriteLine($"⚠️ Không tìm thấy yêu cầu nào có cùng mã cơ bản {maYeuCauCoBan}");
                return;
            }

            // 2. Lấy danh sách vật tư yêu cầu cùng mã cơ bản và cùng sản phẩm
            var danhSachYeuCau = danhSachYeuCauCungCoBan
                .Join(
                    _context.vtyeucau.Where(vt => vt.MaSanpham == maSanpham),
                    y => y.MaYeucau,
                    vt => vt.VTMaYeucau,
                    (y, vt) => new { YeuCau = y, VatTu = vt }
                )
                .OrderBy(x => x.YeuCau.NgayYeucau ?? DateTime.MaxValue) // FIFO: Sắp xếp theo ngày yêu cầu (cũ nhất trước)
                .ThenBy(x => x.YeuCau.MaYeucau) // Nếu cùng ngày, sắp xếp theo mã yêu cầu
                .ToList();

            if (!danhSachYeuCau.Any())
            {
                Console.WriteLine($"⚠️ Không tìm thấy yêu cầu nào cùng mã cơ bản {maYeuCauCoBan} và sản phẩm {maSanpham}");
                return;
            }

            Console.WriteLine($"📋 Tìm thấy {danhSachYeuCau.Count} yêu cầu cùng mã cơ bản ({maYeuCauCoBan}) và sản phẩm {maSanpham}:");
            foreach (var item in danhSachYeuCau)
            {
                var daXuat = TinhTongDaXuat(item.YeuCau.MaYeucau, maSanpham);
                var conThieu = (item.VatTu.SL ?? 0) - daXuat;
                Console.WriteLine($"   - {item.YeuCau.MaYeucau} (Ngày: {item.YeuCau.NgayYeucau}): SL={item.VatTu.SL}, Đã xuất={daXuat}, Còn thiếu={conThieu}");
            }

            int conLai = soLuongNhap;

            // 2. Phân bổ hàng nhập cho từng yêu cầu theo thứ tự FIFO
            foreach (var item in danhSachYeuCau)
            {
                if (conLai <= 0)
                    break;

                string maYeucau = item.YeuCau.MaYeucau;
                int soLuongYeuCau = item.VatTu.SL ?? 0;

                // Tính số lượng đã nhận (đã xuất) cho yêu cầu này
                int daNhan = TinhTongDaXuat(maYeucau, maSanpham);

                // Tính số lượng còn thiếu
                int conThieu = soLuongYeuCau - daNhan;

                if (conThieu <= 0)
                {
                    Console.WriteLine($"   ⏭️ Bỏ qua {maYeucau}: Đã đủ (SL={soLuongYeuCau}, Đã xuất={daNhan})");
                    continue;
                }

                // Phân bổ: min(số còn thiếu, số còn lại)
                int phanBo = Math.Min(conThieu, conLai);

                Console.WriteLine($"   ✅ Phân bổ {phanBo} cho {maYeucau} (còn thiếu {conThieu}, còn lại {conLai})");

                // 3. Tìm hoặc tạo phiếu xuất kho cho yêu cầu này
                var phieuXuatLienQuan = _context.phieuxuatkho
                    .Where(px => px.MaYeucau == maYeucau)
                    .OrderByDescending(px => px.NgayXuatkho)
                    .ToList();

                // Tìm phiếu xuất kho chưa hoàn thành
                var trangThaiHoanThanh = new[] { "Hoàn thành", "Đã xuất kho" };
                var phieuXuatKhoHienTai = phieuXuatLienQuan
                    .FirstOrDefault(px => !trangThaiHoanThanh.Contains(px.TrangThai ?? "", StringComparer.OrdinalIgnoreCase));

                string maXuatkho = null;

                if (phieuXuatKhoHienTai == null)
                {
                    // Tạo phiếu xuất kho mới bằng service
                    maXuatkho = _phieuCodeService.GenerateMaXuatKho(item.YeuCau.YCMaDuan, maYeucau);

                    phieuXuatKhoHienTai = new phieuxuatkho
                    {
                        MaXuatkho = maXuatkho,
                        MaYeucau = maYeucau,
                        MaDuan = item.YeuCau.YCMaDuan,
                        MaNguoidung = item.YeuCau.YCMaNguoidung,
                        NgayXuatkho = null,
                        NgayChuanBi = DateTime.Now,
                        TrangThai = "Đang chuẩn bị hàng"
                    };
                    _context.phieuxuatkho.Add(phieuXuatKhoHienTai);
                    _context.SaveChanges();
                    Console.WriteLine($"      ✨ Tạo phiếu xuất kho mới {maXuatkho} cho {maYeucau}");
                }
                else
                {
                    maXuatkho = phieuXuatKhoHienTai.MaXuatkho;
                    Console.WriteLine($"      ℹ️ Dùng phiếu xuất kho hiện có {maXuatkho} cho {maYeucau}");
                }

                // 4. Kiểm tra xem vật tư đã có trong phiếu xuất kho chưa
                var vtDaCo = _context.vtphieuxuatkho
                    .FirstOrDefault(vt =>
                        vt.MaXuatkho == maXuatkho &&
                        vt.MaSanpham == maSanpham &&
                        (vt.TrangThai != "Đã xuất kho"));

                // 5. Tính đơn giá và thành tiền từ phiếu nhập kho
                decimal? donGia = vtPhieunhapkho?.DonGia;
                decimal? thanhTien = null;

                if (donGia != null && donGia > 0 && phanBo > 0)
                {
                    if (vtPhieunhapkho?.SL > 0 && vtPhieunhapkho?.ThanhTien != null)
                    {
                        // Tính thành tiền theo tỷ lệ
                        thanhTien = (vtPhieunhapkho.ThanhTien.Value / vtPhieunhapkho.SL.Value) * phanBo;
                    }
                    else
                    {
                        thanhTien = donGia * phanBo;
                    }
                }
                else if (vtPhieunhapkho?.ThanhTien != null && vtPhieunhapkho?.SL > 0 && phanBo > 0)
                {
                    thanhTien = (vtPhieunhapkho.ThanhTien.Value / vtPhieunhapkho.SL.Value) * phanBo;
                    donGia = thanhTien / phanBo;
                }

                // 6. Thêm hoặc cập nhật vật tư trong phiếu xuất kho
                if (vtDaCo != null)
                {
                    // Bổ sung số lượng vào vật tư đã có
                    Console.WriteLine($"[AUTO-PHANBO] PXK={maXuatkho} MaYC={maYeucau} MaSP={maSanpham} - TRUOC khi cong: SL_hien_tai={vtDaCo.SL ?? 0}, SL_phan_bo={phanBo}, TrangThaiVT={vtDaCo.TrangThai}");
                    vtDaCo.SL = (vtDaCo.SL ?? 0) + phanBo;
                    if (donGia.HasValue && vtDaCo.DonGia == null)
                    {
                        vtDaCo.DonGia = donGia;
                    }
                    if (thanhTien.HasValue)
                    {
                        vtDaCo.ThanhTien = (vtDaCo.ThanhTien ?? 0) + thanhTien;
                    }
                    // Cập nhật trạng thái nếu đang ở trạng thái thiếu hàng
                    if (vtDaCo.TrangThai == "Thiếu hàng" || vtDaCo.TrangThai == "Thiếu hàng - Đã tạo phiếu mua")
                    {
                        vtDaCo.TrangThai = "Chờ xác nhận";
                    }
                    _context.vtphieuxuatkho.Update(vtDaCo);
                    Console.WriteLine($"[AUTO-PHANBO] PXK={maXuatkho} MaYC={maYeucau} MaSP={maSanpham} - SAU khi cong: SL_moi={vtDaCo.SL ?? 0}");
                }
                else
                {
                    // Tạo mới vật tư trong phiếu xuất kho
                    // Lấy thông tin từ kho tổng để đảm bảo đúng
                    khotongs khotong = null;
                    if (!string.IsNullOrEmpty(vtPhieunhapkho?.Makho))
                    {
                        khotong = _context.khotongs.FirstOrDefault(k =>
                            k.MaSanpham == maSanpham &&
                            k.Makho == vtPhieunhapkho.Makho);
                    }

                    if (khotong == null && !string.IsNullOrEmpty(vtPhieunhapkho?.MaSanpham))
                    {
                        khotong = _context.khotongs.FirstOrDefault(k => k.MaSanpham == maSanpham);
                    }

                    var newVTPhieuxuatkho = new vtphieuxuatkho
                    {
                        MaXuatkho = maXuatkho,
                        MaYeucau = maYeucau,
                        TenSanpham = khotong?.TenSanpham ?? vtPhieunhapkho?.TenSanpham ?? item.VatTu.TenSanpham,
                        MaSanpham = maSanpham,
                        Makho = khotong?.Makho ?? vtPhieunhapkho?.Makho ?? item.VatTu.YCMakho,
                        HangSX = khotong?.HangSX ?? vtPhieunhapkho?.HangSX ?? item.VatTu.HangSX,
                        NhaCC = khotong?.NhaCC ?? vtPhieunhapkho?.NhaCC ?? item.VatTu.NhaCC,
                        DonVi = khotong?.DonVi ?? vtPhieunhapkho?.DonVi ?? item.VatTu.DonVi,
                        SL = phanBo,
                        DonGia = donGia,
                        ThanhTien = thanhTien,
                        NgayBaohanh = khotong?.NgayBaohanh,
                        ThoiGianBH = khotong?.ThoiGianBH,
                        TrangThai = "Đang chuẩn bị hàng"
                    };
                    _context.vtphieuxuatkho.Add(newVTPhieuxuatkho);
                    Console.WriteLine($"[AUTO-PHANBO] PXK={maXuatkho} MaYC={maYeucau} MaSP={maSanpham} - TAO MOI dong PXK, SL={phanBo}");
                }

                // Cập nhật số lượng còn lại
                conLai -= phanBo;

                // Lưu thay đổi sau mỗi yêu cầu để đảm bảo tính nhất quán
                _context.SaveChanges();
            }

            Console.WriteLine($"✅ KẾT THÚC PhanBoHangNhapTheoYeuCau: Đã phân bổ {soLuongNhap - conLai}/{soLuongNhap}, Còn lại {conLai}");
        }

        /// <summary>
        /// Lấy danh sách vật tư còn thiếu cho một yêu cầu
        /// ⭐ SỬA: Lấy từ tất cả yêu cầu có cùng mã cơ bản và tính số lượng thiếu theo mã cơ bản
        /// KHÔNG clone phiếu cũ, KHÔNG lấy từ phiếu mua, CHỈ lấy CÒN THIẾU THỰC
        /// </summary>
        private List<vtphieunhapkho> LayDanhSachVatTuConThieu(string maYeucau)
        {
            // Lấy mã yêu cầu cơ bản
            string baseCode = YeucauUpdateHelper.GetBaseRequestCode(maYeucau);
            if (string.IsNullOrWhiteSpace(baseCode))
            {
                // Fallback: nếu không lấy được mã cơ bản, dùng logic cũ
                baseCode = maYeucau;
            }

            // Lấy tất cả mã yêu cầu có cùng mã cơ bản
            var allRelatedMaYeucau = _context.yeucau
                .Where(y => !string.IsNullOrWhiteSpace(y.MaYeucau))
                .ToList()
                .Where(y => string.Equals(YeucauUpdateHelper.GetBaseRequestCode(y.MaYeucau), baseCode, StringComparison.OrdinalIgnoreCase))
                .Select(y => y.MaYeucau)
                .ToList();

            if (!allRelatedMaYeucau.Any())
            {
                allRelatedMaYeucau = new List<string> { maYeucau };
            }

            // Lấy tất cả vật tư từ tất cả yêu cầu có cùng mã cơ bản
            var vatTuYeuCau = _context.vtyeucau
                .Where(v => allRelatedMaYeucau.Contains(v.VTMaYeucau))
                .ToList();

            // Gom các vật tư trùng lặp (cùng mã sản phẩm) - lấy thông tin từ vật tư mới nhất
            var vatTuGomNhom = vatTuYeuCau
                .GroupBy(vt => vt.MaSanpham, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderByDescending(vt => vt.SLMoi ?? vt.SL ?? 0).First())
                .ToList();

            var ketQua = new List<vtphieunhapkho>();

            foreach (var vt in vatTuGomNhom)
            {
                // Tính số lượng thiếu theo mã yêu cầu cơ bản
                var conThieu = TinhSoLuongConThieu(maYeucau, vt.MaSanpham);

                if (conThieu > 0)
                {
                    ketQua.Add(new vtphieunhapkho
                    {
                        MaYeucau = maYeucau, // Giữ mã yêu cầu gốc để dễ trace
                        MaSanpham = vt.MaSanpham,
                        TenSanpham = vt.TenSanpham,
                        DonVi = vt.DonVi,
                        HangSX = vt.HangSX,
                        NhaCC = vt.NhaCC,
                        Makho = vt.YCMakho,
                        SL = conThieu,
                        TrangThai = "Chờ nhập kho"
                    });
                }
            }

            return ketQua;
        }

        [HttpPost]
        public IActionResult Taophieunhapkhobyphieumuahang(string MaMuahang, phieunhapkho phieunhapkho, vtphieunhapkho vtphieunhapkho, phieumuahang phieumuahang, vtphieumuahang vtphieumuahang)
        {
            var Phieumuahang = _context.phieumuahang.FirstOrDefault(p => p.MaMuahang == MaMuahang);
            if (Phieumuahang == null || string.IsNullOrEmpty(Phieumuahang.MaYeucau))
            {
                TempData["Error"] = "Không tìm thấy phiếu mua hàng hoặc mã yêu cầu.";
                return RedirectToAction("Phieumuahang", "Yeucau", new { area = "TruongBPKho" });
            }

            // ⭐ SỬA: Lấy danh sách vật tư CÒN THIẾU THỰC TẾ, KHÔNG dựa vào vtphieumuahang
            var vatTuConThieu = LayDanhSachVatTuConThieu(Phieumuahang.MaYeucau);

            Console.WriteLine($"📦 Taophieunhapkhobyphieumuahang: MaMuahang={MaMuahang}, MaYeucau={Phieumuahang.MaYeucau}");
            Console.WriteLine($"   Số vật tư còn thiếu: {vatTuConThieu.Count}");
            foreach (var vt in vatTuConThieu)
            {
                Console.WriteLine($"   - {vt.TenSanpham} (MaSP: {vt.MaSanpham}): SL={vt.SL}");
            }

            if (!vatTuConThieu.Any())
            {
                TempData["Info"] = "Không còn vật tư nào thiếu. Yêu cầu đã được đáp ứng đầy đủ.";
                return RedirectToAction("Phieumuahang", "Yeucau", new { area = "TruongBPKho" });
            }

            // Tạo mã phiếu nhập kho mới bằng service
            string MaNhapkho = _phieuCodeService.GenerateMaNhapKho(Phieumuahang?.MaDuan, Phieumuahang?.MaYeucau);

            // Tạo phiếu nhập kho mới
            var newphieunhapkho = new phieunhapkho
            {
                MaNhapkho = MaNhapkho,
                MaYeucau = Phieumuahang.MaYeucau,
                MaDuan = Phieumuahang.MaDuan,
                MaNguoidung = Phieumuahang.MaNguoidung,
                NgayNhapkho = DateTime.Now,
                TrangThai = "Chờ nhập kho"
            };
            _context.phieunhapkho.Add(newphieunhapkho);
            _context.SaveChanges();

            // ⭐ SỬA: Thêm vật tư từ danh sách CÒN THIẾU THỰC TẾ
            foreach (var vt in vatTuConThieu)
            {
                // Lấy thông tin bổ sung từ vtphieumuahang nếu có (đơn giá, thành tiền)
                var vtPhieuMuaHang = _context.vtphieumuahang
                    .FirstOrDefault(v => v.MaMuahang == MaMuahang
                        && v.MaSanpham == vt.MaSanpham);

                var targetMakho = vt.Makho;
                if (!string.IsNullOrEmpty(vt.MaSanpham) && vtPhieuMuaHang != null)
                {
                    targetMakho = EnsureKhoTongForNhapKho(vtPhieuMuaHang);
                }

                var newvtphieunhapkho = new vtphieunhapkho
                {
                    MaNhapkho = MaNhapkho,
                    MaYeucau = vt.MaYeucau,
                    TenSanpham = vt.TenSanpham,
                    MaSanpham = vt.MaSanpham,
                    Makho = targetMakho,
                    HangSX = vt.HangSX,
                    NhaCC = vt.NhaCC,
                    SL = vt.SL,  // ⭐ Số lượng CÒN THIẾU THỰC TẾ, đã tính từ TinhSoLuongConThieu()
                    DonVi = vt.DonVi,
                    DonGia = vtPhieuMuaHang?.DonGia,
                    ThanhTien = vtPhieuMuaHang?.DonGia != null && vt.SL != null
                        ? vtPhieuMuaHang.DonGia * vt.SL
                        : null,
                    TrangThai = "Chờ nhập kho",
                };
                _context.vtphieunhapkho.Add(newvtphieunhapkho);
            }
            _context.SaveChanges();

            // Gửi email thông báo cho nhân viên kho khi có phiếu nhập kho mới cần xử lý
            try
            {
                _ = _emailService.SendNotificationToWarehouseOnNhapKhoAsync(MaNhapkho);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TruongBPKho/Taophieunhapkhobyphieumuahang] Lỗi gửi email nhập kho: {ex.Message}");
            }

            TempData["Success"] = $"Đã tạo phiếu nhập kho {MaNhapkho} với {vatTuConThieu.Count} vật tư còn thiếu.";
            return RedirectToAction("Phieumuahang", "Yeucau", new { area = "TruongBPKho" });
        }

        private string EnsureKhoTongForNhapKho(vtphieumuahang vtPhieumuahang)
        {
            var ngayNhap = vtPhieumuahang.NgayNhapkho ?? DateTime.Now;
            var requestedMakho = NormalizeMakhoValue(vtPhieumuahang, ngayNhap);
            var existingKho = _context.khotongs.FirstOrDefault(k => k.Makho == requestedMakho);

            if (existingKho == null)
            {
                requestedMakho = MakhoHelper.BuildUniqueOfficialCode(
                    _context,
                    vtPhieumuahang.MaSanpham,
                    vtPhieumuahang.HangSX,
                    ngayNhap);

                var newKhoTong = new khotongs
                {
                    Makho = requestedMakho,
                    TenSanpham = vtPhieumuahang.TenSanpham,
                    MaSanpham = vtPhieumuahang.MaSanpham,
                    HangSX = vtPhieumuahang.HangSX,
                    NhaCC = vtPhieumuahang.NhaCC,
                    DonVi = vtPhieumuahang.DonVi,
                    SL = 0,
                    NgayNhapkho = ngayNhap,
                    TrangThai = "Chờ nhập kho",
                    LoaiCapPhat = "Kho tổng"
                };
                _context.khotongs.Add(newKhoTong);
                _context.SaveChanges();
            }

            // Đảm bảo requestedMakho tồn tại trong khotongs trước khi sử dụng
            var verifiedKho = _context.khotongs.FirstOrDefault(k => k.Makho == requestedMakho);
            if (verifiedKho == null)
            {
                // Nếu không tồn tại, tạo lại
                var newKhoTong = new khotongs
                {
                    Makho = requestedMakho,
                    TenSanpham = vtPhieumuahang.TenSanpham,
                    MaSanpham = vtPhieumuahang.MaSanpham,
                    HangSX = vtPhieumuahang.HangSX,
                    NhaCC = vtPhieumuahang.NhaCC,
                    DonVi = vtPhieumuahang.DonVi,
                    SL = 0,
                    NgayNhapkho = ngayNhap,
                    TrangThai = "Chờ nhập kho",
                    LoaiCapPhat = "Kho tổng"
                };
                _context.khotongs.Add(newKhoTong);
                _context.SaveChanges();
            }

            // Đảm bảo vtyeucau có YCMakho tương ứng với requestedMakho
            if (!string.IsNullOrEmpty(vtPhieumuahang.MaYeucau) && !string.IsNullOrEmpty(requestedMakho))
            {
                // Tìm vtyeucau tương ứng dựa trên MaYeucau và thông tin sản phẩm
                var vtyeucauList = _context.vtyeucau
                    .Where(vt => vt.VTMaYeucau == vtPhieumuahang.MaYeucau
                        && vt.TenSanpham == vtPhieumuahang.TenSanpham
                        && vt.MaSanpham == vtPhieumuahang.MaSanpham)
                    .ToList();

                foreach (var vtyeucau in vtyeucauList)
                {
                    // Cập nhật YCMakho để khớp với requestedMakho
                    if (vtyeucau.YCMakho != requestedMakho)
                    {
                        vtyeucau.YCMakho = requestedMakho;
                        _context.vtyeucau.Update(vtyeucau);
                    }
                }

                // Nếu không tìm thấy vtyeucau tương ứng, tạo mới
                if (!vtyeucauList.Any())
                {
                    var newVtyeucau = new vtyeucau
                    {
                        VTMaYeucau = vtPhieumuahang.MaYeucau,
                        TenSanpham = vtPhieumuahang.TenSanpham,
                        MaSanpham = vtPhieumuahang.MaSanpham,
                        YCMakho = requestedMakho,
                        HangSX = vtPhieumuahang.HangSX,
                        NhaCC = vtPhieumuahang.NhaCC,
                        DonVi = vtPhieumuahang.DonVi,
                        SL = vtPhieumuahang.SL,
                        TrangThai = "Đang mua hàng"
                    };
                    _context.vtyeucau.Add(newVtyeucau);
                }

                _context.SaveChanges(); // Lưu để đảm bảo YCMakho tồn tại trước khi cập nhật vtphieumuahang
            }

            if (!string.Equals(vtPhieumuahang.Makho, requestedMakho, StringComparison.Ordinal))
            {
                vtPhieumuahang.Makho = requestedMakho;
                _context.vtphieumuahang.Update(vtPhieumuahang);
                _context.SaveChanges();
            }

            return requestedMakho;
        }

        private string NormalizeMakhoValue(vtphieumuahang vtPhieumuahang, DateTime ngayNhap)
        {
            if (!string.IsNullOrWhiteSpace(vtPhieumuahang.Makho) &&
                !vtPhieumuahang.Makho.Equals("VT mới", StringComparison.OrdinalIgnoreCase))
            {
                var normalizedInput = vtPhieumuahang.Makho.Trim().ToUpperInvariant();
                var existing = _context.khotongs.FirstOrDefault(k => k.Makho == normalizedInput);
                if (existing != null)
                {
                    return existing.Makho;
                }
            }

            return MakhoHelper.BuildOfficialCode(vtPhieumuahang.MaSanpham, vtPhieumuahang.HangSX, ngayNhap);
        }

        [HttpGet]
        public IActionResult GetDataByMaYeucau(string mayeucau)
        {
            var yeucau = _context.yeucau
                .FirstOrDefault(y => y.MaYeucau == mayeucau);

            if (yeucau == null) return NotFound();

            // Lấy dữ liệu từ vtyeucau (vật tư yêu cầu gốc) cho ThemPhieunhapkho
            var vtYeucau = _context.vtyeucau
                .Where(v => v.VTMaYeucau == mayeucau)
                .Select(v => new
                {
                    tenSanpham = v.TenSanpham,
                    maSanpham = v.MaSanpham,
                    makho = v.YCMakho,
                    hangSX = v.HangSX,
                    nhaCC = v.NhaCC,
                    sl = v.SL,
                    donVi = v.DonVi
                })
                .ToList();

            // Fallback: nếu không có vật tư trong yêu cầu gốc, lấy từ vtphieumuahang
            if (vtYeucau == null || vtYeucau.Count == 0)
            {
                vtYeucau = _context.vtphieumuahang
                    .Where(v => v.MaYeucau == mayeucau)
                    .Select(v => new
                    {
                        tenSanpham = v.TenSanpham,
                        maSanpham = v.MaSanpham,
                        makho = v.Makho,
                        hangSX = v.HangSX,
                        nhaCC = v.NhaCC,
                        sl = v.SL,
                        donVi = v.DonVi
                    })
                    .ToList();
            }

            return Json(new
            {
                maNguoidung = yeucau.YCMaNguoidung,
                maDuan = yeucau.YCMaDuan,
                vtPhieuMuaHang = vtYeucau  // Trả về dữ liệu từ vtyeucau hoặc vtphieumuahang
            });
        }

        [HttpPost]
        public IActionResult ThemPhieunhapkhoSQL(phieunhapkho phieunhapkho, vtphieunhapkho vtphieunhapkho,
            string[] TenSanpham, string[] MaSanpham, string[] HangSX, string[] NhaCC,
            int[] SL, string[] DonVi, string[] Makho, string LoaiNhapkho, decimal[] DonGia, string[] DiengiaiNhapKho)
        {
            var maNv = HttpContext.Session.GetString("MaNguoidung");
            if (string.IsNullOrEmpty(maNv))
            {
                TempData["Error"] = "Session đã hết hạn. Vui lòng đăng nhập lại!";
                return RedirectToAction("Login", "Home", new { area = "" });
            }

            string currentArea = "TruongBPKho";

            try
            {
                if (TenSanpham == null || TenSanpham.Length == 0)
                {
                    TempData["Error"] = "Vui lòng nhập ít nhất một vật tư!";
                    return RedirectToAction("ThemPhieunhapkho", "Yeucau", new { area = currentArea });
                }

                if (string.IsNullOrEmpty(LoaiNhapkho))
                {
                    TempData["Error"] = "Vui lòng chọn loại nhập kho!";
                    return RedirectToAction("ThemPhieunhapkho", "Yeucau", new { area = currentArea });
                }

                if (string.IsNullOrEmpty(phieunhapkho.MaNguoidung))
                {
                    phieunhapkho.MaNguoidung = maNv;
                }

                int count = TenSanpham.Length;

                var validationErrors = new List<string>();
                for (int i = 0; i < count; i++)
                {
                    var maSp = (MaSanpham != null && MaSanpham.Length > i) ? (MaSanpham[i] ?? "") : "";
                    var maKho = (Makho != null && Makho.Length > i) ? (Makho[i] ?? "") : "";
                    int soLuongTra = (SL != null && SL.Length > i) ? (SL[i]) : 0;
                    if (soLuongTra <= 0) continue;

                    int soLuongDaMuon = 0;

                    if (LoaiNhapkho == "duan" && !string.IsNullOrEmpty(phieunhapkho.MaDuan))
                    {
                        soLuongDaMuon = (from vt in _context.vtphieuxuatkho
                                         join px in _context.phieuxuatkho on vt.MaXuatkho equals px.MaXuatkho
                                         where px.MaDuan == phieunhapkho.MaDuan
                                               && (vt.TrangThai == "Đã lấy hàng"
                                                   || vt.TrangThai == "Đã xuất kho")
                                               && (vt.SL ?? 0) > 0
                                               && (maSp == "" || vt.MaSanpham == maSp)
                                               && (maKho == "" || vt.Makho == maKho)
                                         select vt.SL ?? 0).Sum();
                    }
                    else if (LoaiNhapkho == "canhan")
                    {
                        soLuongDaMuon = _context.khonguoidungs
                            .Where(k => k.NDMaNguoidung == phieunhapkho.MaNguoidung
                                        && (k.TrangThai == "Đang mượn" || k.TrangThai == "Đang sử dụng")
                                        && (maSp == "" || k.MaSanpham == maSp))
                            .Select(k => k.SL ?? 0)
                            .Sum();
                    }

                    if (soLuongTra > soLuongDaMuon)
                    {
                        var tenSp = (TenSanpham != null && TenSanpham.Length > i) ? (TenSanpham[i] ?? "") : "";
                        var donVi = (DonVi != null && DonVi.Length > i) ? (DonVi[i] ?? "") : "";
                        validationErrors.Add($"- {tenSp} ({maSp}): Trả {soLuongTra} {donVi}, nhưng chỉ mượn {soLuongDaMuon}.");
                    }
                }

                if (validationErrors.Count > 0)
                {
                    TempData["Error"] = "Số lượng trả vượt quá số lượng đã mượn cho các vật tư sau:\n" + string.Join("\n", validationErrors);
                    return RedirectToAction("ThemPhieunhapkho", "Yeucau", new { area = currentArea });
                }

                // Tạo mã phiếu nhập kho duy nhất bằng service
                string MaNhapkho = _phieuCodeService.GenerateMaNhapKho(phieunhapkho.MaDuan, phieunhapkho.MaYeucau);

                phieunhapkho.MaNhapkho = MaNhapkho;
                phieunhapkho.NgayNhapkho = DateTime.Now;

                if (!string.IsNullOrEmpty(phieunhapkho.MaDuan))
                {
                    phieunhapkho.TrangThai = "Chờ quản lý dự án duyệt";
                }
                else
                {
                    phieunhapkho.TrangThai = "Chờ Giám đốc duyệt";
                }

                if (string.IsNullOrEmpty(phieunhapkho.MaYeucau))
                {
                    // MãDựÁnNK YYMMDD-01 hoặc MãNhânViênNK YYMMDD-01 (PhieuCodeService)
                    string maDuanForYc = (LoaiNhapkho == "duan" && !string.IsNullOrEmpty(phieunhapkho.MaDuan)) ? phieunhapkho.MaDuan : null;
                    string maYeucauDacBiet = _phieuCodeService.GenerateMaYeucauNhapKho(maDuanForYc, maNv);

                    string ycMaDuan = null;
                    if (!string.IsNullOrEmpty(phieunhapkho.MaDuan))
                    {
                        var duanExists = _context.duans.FirstOrDefault(d => d.MaDuan == phieunhapkho.MaDuan);
                        if (duanExists == null) duanExists = _context.duans.AsEnumerable().FirstOrDefault(d => d.MaDuan != null && d.MaDuan.Equals(phieunhapkho.MaDuan, StringComparison.OrdinalIgnoreCase));
                        if (duanExists != null) ycMaDuan = duanExists.MaDuan;
                        else { var allDuans = _context.duans.Select(d => d.MaDuan).ToList(); Console.WriteLine($"Warning: Mã dự án '{phieunhapkho.MaDuan}' không tồn tại. Available: {string.Join(", ", allDuans)}"); }
                    }

                    var nguoiDung = _context.nguoidungs.FirstOrDefault(n => n.MaNguoidung == maNv);
                    string tenNguoiDung = nguoiDung?.TenNguoidung ?? "";
                    string boPhanNguoiDung = nguoiDung?.Bophan ?? "";

                    var newYeucauDacBiet = new yeucau
                    {
                        MaYeucau = maYeucauDacBiet,
                        TenYeucau = "Yêu cầu nhập kho",
                        YCMaNguoidung = maNv,
                        NguoiYeucau = tenNguoiDung,
                        Bophan = boPhanNguoiDung,
                        YCMaDuan = ycMaDuan,
                        NgayYeucau = DateTime.Now,
                        TrangThai = (LoaiNhapkho == "duan" && !string.IsNullOrEmpty(phieunhapkho.MaDuan)) ? "Chờ quản lý dự án duyệt" : (LoaiNhapkho == "canhan" ? "Chờ Giám đốc duyệt" : "Đã duyệt")
                    };
                    _context.yeucau.Add(newYeucauDacBiet);
                    _context.SaveChanges();

                    phieunhapkho.MaYeucau = maYeucauDacBiet;
                }

                _context.phieunhapkho.Add(phieunhapkho);
                _context.SaveChanges();

                for (int i = 0; i < count; i++)
                {
                    if (string.IsNullOrEmpty(TenSanpham[i])) continue;

                    decimal donGia = DonGia != null && i < DonGia.Length ? DonGia[i] : 0;
                    int soLuong = (SL != null && i < SL.Length) ? SL[i] : 0;
                    decimal thanhTien = donGia * soLuong;
                    string diengiaiNhapKho = (DiengiaiNhapKho != null && i < DiengiaiNhapKho.Length)
                        ? (DiengiaiNhapKho[i] ?? "Không sử dụng")
                        : "Không sử dụng";

                    var newvtphieunhapkho = new vtphieunhapkho
                    {
                        TenSanpham = TenSanpham[i],
                        MaSanpham = MaSanpham?[i] ?? "",
                        Makho = Makho?[i] ?? "",
                        HangSX = HangSX?[i] ?? "",
                        NhaCC = NhaCC?[i] ?? "",
                        SL = soLuong,
                        DonVi = DonVi?[i] ?? "",
                        DiengiaiNhapKho = diengiaiNhapKho,
                        DonGia = donGia,
                        ThanhTien = thanhTien,
                        TrangThai = phieunhapkho.TrangThai,
                        MaNhapkho = MaNhapkho,
                        MaYeucau = phieunhapkho.MaYeucau
                    };

                    _context.vtphieunhapkho.Add(newvtphieunhapkho);
                }

                _context.SaveChanges();

                // Gửi email thông báo theo luồng duyệt phiếu nhập kho
                try
                {
                    var maNhapkhoForEmail = MaNhapkho;
                    _ = System.Threading.Tasks.Task.Run(async () =>
                    {
                        try
                        {
                            await _emailService.SendNotificationOnNhapKhoCreatedAsync(maNhapkhoForEmail);
                        }
                        catch (Exception exInner)
                        {
                            System.Diagnostics.Debug.WriteLine($"[TruongBPKho/ThemPhieunhapkhoSQL] Lỗi gửi email tạo phiếu nhập kho: {exInner.Message}");
                        }
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TruongBPKho/ThemPhieunhapkhoSQL] Lỗi khởi chạy task gửi email: {ex.Message}");
                }

                TempData["Success"] = "Tạo phiếu nhập kho thành công!";
                return RedirectToAction("Phieunhapkho", "Yeucau", new { area = currentArea });
            }
            catch (Exception ex)
            {
                Console.WriteLine("==========================================");
                Console.WriteLine($"ERROR in ThemPhieunhapkhoSQL: {ex.Message}");
                Console.WriteLine($"Exception Type: {ex.GetType().Name}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                    Console.WriteLine($"Inner Stack trace: {ex.InnerException.StackTrace}");
                }
                Console.WriteLine("==========================================");

                var maNvCheck = HttpContext.Session.GetString("MaNguoidung") ?? maNv;
                Console.WriteLine($"Session MaNguoidung after error: {maNvCheck ?? "NULL"}");
                Console.WriteLine($"Original maNv (from before try): {maNv ?? "NULL"}");

                TempData["Error"] = $"Có lỗi xảy ra khi xử lý: {ex.Message}. Vui lòng kiểm tra lại dữ liệu hoặc liên hệ admin.";

                if (!string.IsNullOrEmpty(maNv))
                {
                    return RedirectToAction("ThemPhieunhapkho", "Yeucau", new { area = currentArea });
                }
                else
                {
                    TempData["Error"] = "Session đã hết hạn. Vui lòng đăng nhập lại!";
                    return RedirectToAction("Login", "Home", new { area = "" });
                }
            }
        }


        private bool IsNhapKhoDuanOrCaNhan(phieunhapkho phieunhapkho)
        {
            if (string.IsNullOrEmpty(phieunhapkho.MaYeucau))
            {
                return false;
            }

            // Nhận diện "yêu cầu nhập kho" (hoàn trả) theo dữ liệu, KHÔNG còn phụ thuộc tiền tố NHAPKHO_.
            // Mã mới: MaNhanVienNK YYMMDD-01 / MaDuAnNK YYMMDD-01
            return _context.yeucau.Any(y => y.MaYeucau == phieunhapkho.MaYeucau
                                            && y.TenYeucau == "Yêu cầu nhập kho");
        }


        private string GetMaSanphamBase(string maSanpham)
        {
            if (string.IsNullOrEmpty(maSanpham) || !maSanpham.Contains("-"))
                return maSanpham;

            if (maSanpham.Contains("-Misumi-") || maSanpham.Contains("-HIVERO-"))
            {
                var parts = maSanpham.Split(new[] { "-Misumi-", "-HIVERO-" }, StringSplitOptions.None);
                return parts.Length > 0 ? parts[0] : maSanpham.Split('-')[0];
            }

            return maSanpham.Split('-')[0];
        }


        private void TruKhoDuanKhiNhapKho(phieunhapkho phieunhapkho, vtphieunhapkho vtPhieunhapkho)
        {
            Debug.WriteLine($"========== DEBUG TruKhoDuanKhiNhapKho START ==========");
            Debug.WriteLine($"MaNhapkho: {phieunhapkho?.MaNhapkho}");
            Debug.WriteLine($"MaYeucau: {phieunhapkho?.MaYeucau}");
            Debug.WriteLine($"MaDuan: {phieunhapkho?.MaDuan}");
            Debug.WriteLine($"MaNguoidung: {phieunhapkho?.MaNguoidung}");
            Debug.WriteLine($"MaSanpham: {vtPhieunhapkho?.MaSanpham}");
            Debug.WriteLine($"SL: {vtPhieunhapkho?.SL}");

            if (phieunhapkho == null || vtPhieunhapkho == null || string.IsNullOrEmpty(vtPhieunhapkho.MaSanpham))
            {
                Debug.WriteLine($"❌ RETURN: Null check failed");
                return;
            }

            // Lấy tất cả mã yêu cầu có cùng mã cơ bản với yêu cầu gốc của phiếu nhập kho
            var maYeucauNhap = phieunhapkho.MaYeucau;
            if (string.IsNullOrEmpty(maYeucauNhap))
            {
                Debug.WriteLine($"❌ RETURN: MaYeucau is empty");
                return;
            }

            string baseMaYeucau = YeucauUpdateHelper.GetBaseRequestCode(maYeucauNhap);

            // Gom theo base mã yêu cầu nhưng chỉ trong CÙNG DỰ ÁN (MaDuan của phiếu nhập kho)
            var yeucauQuery = _context.yeucau
                .Where(y => !string.IsNullOrWhiteSpace(y.MaYeucau));

            if (!string.IsNullOrWhiteSpace(phieunhapkho.MaDuan))
            {
                yeucauQuery = yeucauQuery.Where(y => y.YCMaDuan == phieunhapkho.MaDuan);
            }

            var allRelatedMaYeucau = yeucauQuery
                .ToList()
                .Where(y => string.Equals(
                    YeucauUpdateHelper.GetBaseRequestCode(y.MaYeucau),
                    baseMaYeucau,
                    StringComparison.OrdinalIgnoreCase))
                .Select(y => y.MaYeucau)
                .ToList();

            // Lấy tất cả vật tư yêu cầu tương ứng với mã sản phẩm này
            var allVTYeucau = _context.vtyeucau
                .Where(v => allRelatedMaYeucau.Contains(v.VTMaYeucau)
                            && string.Equals(v.MaSanpham, vtPhieunhapkho.MaSanpham, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Tính số lượng còn thiếu thực tế theo base code (nếu <= 0 thì không cần trừ kho dự án)
            int slCanTraThucTe = TinhSoLuongThieuTheoBaseCode(
                allVTYeucau,
                vtPhieunhapkho.MaSanpham,
                allRelatedMaYeucau);

            // Lấy danh sách các dòng vật tư đã xuất kho cho dự án này, cùng base mã sản phẩm
            var baseProductCode = GetMaSanphamBase(vtPhieunhapkho.MaSanpham);
            Debug.WriteLine($"BaseProductCode: {baseProductCode}");


            string maNguoiDangGiuVatTu = "";

            if (!string.IsNullOrEmpty(maYeucauNhap) &&
                (maYeucauNhap.StartsWith("NHAPKHO_DUAN_", StringComparison.OrdinalIgnoreCase) ||
                 maYeucauNhap.StartsWith("NHAPKHO_CANHAN_", StringComparison.OrdinalIgnoreCase)))
            {
                // Đây là phiếu nhập kho hoàn trả, người đang giữ vật tư là MaNguoidung của phiếu nhập kho
                maNguoiDangGiuVatTu = phieunhapkho.MaNguoidung ?? "";
                Debug.WriteLine($"✅ NHAPKHO_DUAN/CANHAN detected - Using MaNguoidung from phieunhapkho: {maNguoiDangGiuVatTu}");
            }
            else
            {
                // Trường hợp bình thường, lấy từ yêu cầu
                var yeucauNhapKho = _context.yeucau
                    .FirstOrDefault(y => y.MaYeucau == maYeucauNhap);
                maNguoiDangGiuVatTu = yeucauNhapKho?.YCMaNguoidung ?? "";
                Debug.WriteLine($"⚠️ Normal case - Using YCMaNguoidung from yeucau: {maNguoiDangGiuVatTu}");
            }

            Debug.WriteLine($"maNguoiDangGiuVatTu (FINAL): {maNguoiDangGiuVatTu}");
            Debug.WriteLine($"MaDuan: {phieunhapkho.MaDuan}");

            var maXuatkhoList = _context.phieuxuatkho
                .Where(px => px.MaDuan == phieunhapkho.MaDuan
                             && !string.IsNullOrEmpty(maNguoiDangGiuVatTu)
                             && px.MaNguoidung == maNguoiDangGiuVatTu)
                .Select(px => px.MaXuatkho)
                .Where(mx => !string.IsNullOrEmpty(mx))
                .ToList();

            Debug.WriteLine($"📋 maXuatkhoList count: {maXuatkhoList.Count}");
            foreach (var mx in maXuatkhoList)
            {
                Debug.WriteLine($"   - MaXuatkho: {mx}");
            }

            var vtXuatKhoItems = _context.vtphieuxuatkho
                .Where(v => maXuatkhoList.Contains(v.MaXuatkho)
                            && !string.IsNullOrEmpty(v.MaSanpham))
                .ToList()
                .Where(v => GetMaSanphamBase(v.MaSanpham) == baseProductCode)
                .ToList();

            Debug.WriteLine($"📦 vtXuatKhoItems count (before base code filter): {_context.vtphieuxuatkho.Where(v => maXuatkhoList.Contains(v.MaXuatkho) && !string.IsNullOrEmpty(v.MaSanpham)).ToList().Count}");
            Debug.WriteLine($"📦 vtXuatKhoItems count (after base code filter): {vtXuatKhoItems.Count}");
            foreach (var vt in vtXuatKhoItems)
            {
                Debug.WriteLine($"   - MaXuatkho: {vt.MaXuatkho}, MaSanpham: {vt.MaSanpham}, SL: {vt.SL}, TrangThai: {vt.TrangThai}");
            }

            if (!vtXuatKhoItems.Any())
            {
                Debug.WriteLine($"❌ RETURN: No vtXuatKhoItems found");
                Debug.WriteLine($"========== DEBUG TruKhoDuanKhiNhapKho END ==========");
                return;
            }

            Debug.WriteLine($"slCanTraThucTe: {slCanTraThucTe}");
            Debug.WriteLine($"vtPhieunhapkho.SL: {vtPhieunhapkho.SL}");

            // ===== CASE 1: CÙNG BASECODE DỰ ÁN → TRẢ 1 LẦN =====
            bool isCaseBaseCode = IsCaseBaseCodeDuan(phieunhapkho);
            Debug.WriteLine($"IsCaseBaseCodeDuan: {isCaseBaseCode}");

            if (isCaseBaseCode)
            {
                Debug.WriteLine($"✅ CASE 1: BASECODE DỰ ÁN");

                // Nếu là phiếu nhập kho hoàn trả (NHAPKHO_DUAN_xxx), dùng trực tiếp SL từ vtPhieunhapkho
                // Vì slCanTraThucTe có thể = 0 do không tìm thấy yêu cầu tương ứng
                int slCanTra;
                if (!string.IsNullOrEmpty(maYeucauNhap) &&
                    (maYeucauNhap.StartsWith("NHAPKHO_DUAN_", StringComparison.OrdinalIgnoreCase) ||
                     maYeucauNhap.StartsWith("NHAPKHO_CANHAN_", StringComparison.OrdinalIgnoreCase)))
                {
                    // Phiếu nhập kho hoàn trả: dùng SL trực tiếp từ vtPhieunhapkho
                    slCanTra = vtPhieunhapkho.SL ?? 0;
                    Debug.WriteLine($"NHAPKHO_DUAN/CANHAN: Using vtPhieunhapkho.SL = {slCanTra}");
                }
                else
                {
                    // Trường hợp bình thường: dùng slCanTraThucTe
                    slCanTra = slCanTraThucTe;
                    Debug.WriteLine($"Normal case: Using slCanTraThucTe = {slCanTra}");
                }

                Debug.WriteLine($"slCanTra (FINAL): {slCanTra}");

                if (slCanTra <= 0)
                {
                    Debug.WriteLine($"❌ RETURN: slCanTra <= 0");
                    Debug.WriteLine($"========== DEBUG TruKhoDuanKhiNhapKho END ==========");
                    return;
                }

                // ⭐ CHỈ TRẢ 1 DÒNG DUY NHẤT (FIFO)
                var vtItem = vtXuatKhoItems.FirstOrDefault();

                if (vtItem == null)
                {
                    Debug.WriteLine($"❌ RETURN: vtItem is null");
                    Debug.WriteLine($"========== DEBUG TruKhoDuanKhiNhapKho END ==========");
                    return;
                }

                int slHienTai = vtItem.SL ?? 0;
                int slTru = Math.Min(slHienTai, slCanTra);
                Debug.WriteLine($"vtItem.SL (before): {slHienTai}, slTru: {slTru}");

                vtItem.SL = slHienTai - slTru;
                Debug.WriteLine($"vtItem.SL (after): {vtItem.SL}");

                if (vtItem.SL <= 0)
                    vtItem.TrangThai = "Đã trả kho";

                _context.vtphieuxuatkho.Update(vtItem);
                Debug.WriteLine($"✅ Updated vtItem - MaXuatkho: {vtItem.MaXuatkho}, SL: {vtItem.SL}, TrangThai: {vtItem.TrangThai}");
                Debug.WriteLine($"========== DEBUG TruKhoDuanKhiNhapKho END ==========");
                return;
            }

            // ===== CASE 2: KHÔNG PHẢI BASECODE / CÁ NHÂN → TRẢ TỪNG DÒNG =====
            Debug.WriteLine($"✅ CASE 2: KHÔNG PHẢI BASECODE");
            int slConLai = vtPhieunhapkho.SL ?? 0;
            Debug.WriteLine($"slConLai (start): {slConLai}");

            foreach (var vtItem in vtXuatKhoItems)
            {
                if (slConLai <= 0)
                {
                    Debug.WriteLine($"⏹️ BREAK: slConLai <= 0");
                    break;
                }

                int slHienTai = vtItem.SL ?? 0;
                int slTru = Math.Min(slHienTai, slConLai);
                Debug.WriteLine($"vtItem - MaXuatkho: {vtItem.MaXuatkho}, SL (before): {slHienTai}, slTru: {slTru}");

                vtItem.SL = slHienTai - slTru;
                Debug.WriteLine($"vtItem.SL (after): {vtItem.SL}");

                if (vtItem.SL <= 0)
                    vtItem.TrangThai = "Đã trả kho";

                _context.vtphieuxuatkho.Update(vtItem);
                Debug.WriteLine($"✅ Updated vtItem - MaXuatkho: {vtItem.MaXuatkho}, SL: {vtItem.SL}, TrangThai: {vtItem.TrangThai}");

                slConLai -= slTru;
                Debug.WriteLine($"slConLai (remaining): {slConLai}");
            }

            Debug.WriteLine($"========== DEBUG TruKhoDuanKhiNhapKho END ==========");
        }

        // Hàm debug: Kiểm tra kho dự án TRƯỚC khi nhập kho
        private void KiemTraKhoDuanTruocKhiNhapKho(phieunhapkho phieunhapkho, List<vtphieunhapkho> vtPhieunhapkhoList)
        {
            if (string.IsNullOrEmpty(phieunhapkho.MaDuan) || string.IsNullOrEmpty(phieunhapkho.MaNguoidung))
            {
                Debug.WriteLine($"⚠️ KiemTraKhoDuanTruocKhiNhapKho: Không phải nhập kho dự án");
                return;
            }

            Debug.WriteLine($"🔍 KIỂM TRA KHO DỰ ÁN TRƯỚC KHI NHẬP KHO");
            Debug.WriteLine($"MaDuan: {phieunhapkho.MaDuan}, MaNguoidung: {phieunhapkho.MaNguoidung}");
            Debug.WriteLine($"MaYeucau: {phieunhapkho.MaYeucau}");

            foreach (var vt in vtPhieunhapkhoList)
            {
                if (string.IsNullOrEmpty(vt.MaSanpham)) continue;

                var baseProductCode = GetMaSanphamBase(vt.MaSanpham);
                Debug.WriteLine($"\n📦 MaSanpham: {vt.MaSanpham}, BaseCode: {baseProductCode}, SL cần trả: {vt.SL}");

                // Lấy tất cả phiếu xuất kho của người này trong dự án
                var maXuatkhoList = _context.phieuxuatkho
                    .Where(px => px.MaDuan == phieunhapkho.MaDuan
                                 && px.MaNguoidung == phieunhapkho.MaNguoidung)
                    .Select(px => px.MaXuatkho)
                    .Where(mx => !string.IsNullOrEmpty(mx))
                    .ToList();

                var vtXuatKhoItems = _context.vtphieuxuatkho
                    .Where(v => maXuatkhoList.Contains(v.MaXuatkho)
                                && !string.IsNullOrEmpty(v.MaSanpham))
                    .ToList()
                    .Where(v => GetMaSanphamBase(v.MaSanpham) == baseProductCode)
                    .ToList();

                Debug.WriteLine($"   Tổng số phiếu xuất kho: {maXuatkhoList.Count}");
                Debug.WriteLine($"   Tổng số dòng vật tư xuất kho (cùng base code): {vtXuatKhoItems.Count}");

                int tongSL = 0;
                foreach (var vtXuat in vtXuatKhoItems)
                {
                    Debug.WriteLine($"      - MaXuatkho: {vtXuat.MaXuatkho}, MaSanpham: {vtXuat.MaSanpham}, SL: {vtXuat.SL}, TrangThai: {vtXuat.TrangThai}");
                    tongSL += vtXuat.SL ?? 0;
                }
                Debug.WriteLine($"   ⬆️ Tổng SL trong kho dự án TRƯỚC: {tongSL}");
            }
        }

        // Hàm debug: Kiểm tra kho dự án SAU khi nhập kho
        private void KiemTraKhoDuanSauKhiNhapKho(phieunhapkho phieunhapkho, List<vtphieunhapkho> vtPhieunhapkhoList)
        {
            if (string.IsNullOrEmpty(phieunhapkho.MaDuan) || string.IsNullOrEmpty(phieunhapkho.MaNguoidung))
            {
                Debug.WriteLine($"⚠️ KiemTraKhoDuanSauKhiNhapKho: Không phải nhập kho dự án");
                return;
            }

            Debug.WriteLine($"🔍 KIỂM TRA KHO DỰ ÁN SAU KHI NHẬP KHO");
            Debug.WriteLine($"MaDuan: {phieunhapkho.MaDuan}, MaNguoidung: {phieunhapkho.MaNguoidung}");
            Debug.WriteLine($"MaYeucau: {phieunhapkho.MaYeucau}");

            foreach (var vt in vtPhieunhapkhoList)
            {
                if (string.IsNullOrEmpty(vt.MaSanpham)) continue;

                var baseProductCode = GetMaSanphamBase(vt.MaSanpham);
                Debug.WriteLine($"\n📦 MaSanpham: {vt.MaSanpham}, BaseCode: {baseProductCode}, SL đã trả: {vt.SL}");

                // Lấy tất cả phiếu xuất kho của người này trong dự án
                var maXuatkhoList = _context.phieuxuatkho
                    .Where(px => px.MaDuan == phieunhapkho.MaDuan
                                 && px.MaNguoidung == phieunhapkho.MaNguoidung)
                    .Select(px => px.MaXuatkho)
                    .Where(mx => !string.IsNullOrEmpty(mx))
                    .ToList();

                var vtXuatKhoItems = _context.vtphieuxuatkho
                    .Where(v => maXuatkhoList.Contains(v.MaXuatkho)
                                && !string.IsNullOrEmpty(v.MaSanpham))
                    .ToList()
                    .Where(v => GetMaSanphamBase(v.MaSanpham) == baseProductCode)
                    .ToList();

                Debug.WriteLine($"   Tổng số phiếu xuất kho: {maXuatkhoList.Count}");
                Debug.WriteLine($"   Tổng số dòng vật tư xuất kho (cùng base code): {vtXuatKhoItems.Count}");

                int tongSL = 0;
                foreach (var vtXuat in vtXuatKhoItems)
                {
                    Debug.WriteLine($"      - MaXuatkho: {vtXuat.MaXuatkho}, MaSanpham: {vtXuat.MaSanpham}, SL: {vtXuat.SL}, TrangThai: {vtXuat.TrangThai}");
                    tongSL += vtXuat.SL ?? 0;
                }
                Debug.WriteLine($"   ⬇️ Tổng SL trong kho dự án SAU: {tongSL}");
            }
        }

        private void TruKhoCaNhanKhiNhapKho(phieunhapkho phieunhapkho, vtphieunhapkho vtPhieunhapkho)
        {
            if (!string.IsNullOrEmpty(phieunhapkho.MaDuan) ||
                string.IsNullOrEmpty(vtPhieunhapkho.MaSanpham) ||
                string.IsNullOrEmpty(phieunhapkho.MaNguoidung))
                return;

            Debug.WriteLine($"DEBUG: Đang tìm kho cá nhân - MaNguoidung: {phieunhapkho.MaNguoidung}, MaSanpham: {vtPhieunhapkho.MaSanpham}, SL: {vtPhieunhapkho.SL}");

            // Tìm vật tư trong kho cá nhân - thử tìm chính xác trước
            var khoCaNhanItem = _context.khonguoidungs
                .FirstOrDefault(k => k.NDMaNguoidung == phieunhapkho.MaNguoidung
                                  && k.MaSanpham == vtPhieunhapkho.MaSanpham
                                  && (k.TrangThai == "Đang mượn" || k.TrangThai == "Đang sử dụng")
                                  && (k.SL ?? 0) > 0);

            // Nếu không tìm thấy với mã chính xác, thử tìm với mã cơ bản
            if (khoCaNhanItem == null && vtPhieunhapkho.MaSanpham.Contains("-"))
            {
                var maSanphamBase = GetMaSanphamBase(vtPhieunhapkho.MaSanpham);

                khoCaNhanItem = _context.khonguoidungs
                    .FirstOrDefault(k => k.NDMaNguoidung == phieunhapkho.MaNguoidung
                                      && k.MaSanpham.StartsWith(maSanphamBase)
                                      && (k.TrangThai == "Đang mượn" || k.TrangThai == "Đang sử dụng")
                                      && (k.SL ?? 0) > 0);

                if (khoCaNhanItem != null)
                {
                    Debug.WriteLine($"DEBUG: Tìm thấy kho cá nhân với mã cơ bản - Mã tìm: {maSanphamBase}, Mã tìm thấy: {khoCaNhanItem.MaSanpham}");
                }
            }

            if (khoCaNhanItem != null)
            {
                int slCanTra = vtPhieunhapkho.SL ?? 0;
                int slHienTai = khoCaNhanItem.SL ?? 0;

                Debug.WriteLine($"DEBUG: Trừ kho cá nhân - SL hiện tại: {slHienTai}, SL cần trả: {slCanTra}");

                if (slHienTai >= slCanTra)
                {
                    khoCaNhanItem.SL = slHienTai - slCanTra;
                    if ((khoCaNhanItem.SL ?? 0) <= 0)
                    {
                        khoCaNhanItem.TrangThai = "Đã trả";
                    }
                    _context.khonguoidungs.Update(khoCaNhanItem);
                    Debug.WriteLine($"DEBUG: Đã trừ kho cá nhân thành công - SL còn lại: {khoCaNhanItem.SL}");
                }
                else
                {
                    khoCaNhanItem.SL = 0;
                    khoCaNhanItem.TrangThai = "Đã trả";
                    _context.khonguoidungs.Update(khoCaNhanItem);

                    Debug.WriteLine($"Cảnh báo: Số lượng trả ({slCanTra}) lớn hơn số lượng trong kho cá nhân ({slHienTai}) cho vật tư {vtPhieunhapkho.MaSanpham}");
                }
            }
            else
            {
                Debug.WriteLine($"CẢNH BÁO: Không tìm thấy vật tư trong kho cá nhân - MaNguoidung: {phieunhapkho.MaNguoidung}, MaSanpham: {vtPhieunhapkho.MaSanpham}");

                var allItems = _context.khonguoidungs
                    .Where(k => k.NDMaNguoidung == phieunhapkho.MaNguoidung
                             && (k.TrangThai == "Đang mượn" || k.TrangThai == "Đang sử dụng"))
                    .ToList();
                Debug.WriteLine($"DEBUG: Tổng số vật tư trong kho cá nhân của {phieunhapkho.MaNguoidung}: {allItems.Count}");
                foreach (var item in allItems)
                {
                    Debug.WriteLine($"  - MaSanpham: {item.MaSanpham}, SL: {item.SL}, TrangThai: {item.TrangThai}");
                }
            }
        }


        private khotongs TimKhoTong(vtphieunhapkho vtPhieunhapkho)
        {
            khotongs khotong = null;

            // Ưu tiên 1: Tìm theo Makho chính xác (trong Local tracking trước)
            var trackedByMakho = _context.khotongs.Local
                .FirstOrDefault(k => k.Makho == vtPhieunhapkho.Makho);

            if (trackedByMakho != null)
            {
                khotong = trackedByMakho;
            }
            else
            {
                // Tìm trong database theo Makho chính xác
                khotong = _context.khotongs
                    .FirstOrDefault(k => k.Makho == vtPhieunhapkho.Makho);
            }

            // Nếu không tìm thấy theo Makho, tìm theo MaSanpham + HangSX + Makho
            if (khotong == null && !string.IsNullOrEmpty(vtPhieunhapkho.MaSanpham) && !string.IsNullOrEmpty(vtPhieunhapkho.HangSX))
            {
                khotong = _context.khotongs
                    .FirstOrDefault(k =>
                        k.MaSanpham == vtPhieunhapkho.MaSanpham &&
                        k.HangSX == vtPhieunhapkho.HangSX &&
                        k.Makho == vtPhieunhapkho.Makho);
            }

            // Nếu vẫn không tìm thấy, tìm theo MaSanpham + HangSX (bỏ qua Makho)
            if (khotong == null && !string.IsNullOrEmpty(vtPhieunhapkho.MaSanpham) && !string.IsNullOrEmpty(vtPhieunhapkho.HangSX))
            {
                khotong = _context.khotongs
                    .FirstOrDefault(k =>
                        k.MaSanpham == vtPhieunhapkho.MaSanpham &&
                        k.HangSX == vtPhieunhapkho.HangSX);
            }

            return khotong;
        }


        private void CongVaoKhoTong(vtphieunhapkho vtPhieunhapkho)
        {
            Debug.WriteLine($"========== DEBUG CongVaoKhoTong START ==========");
            Debug.WriteLine($"MaSanpham: {vtPhieunhapkho.MaSanpham}, Makho: {vtPhieunhapkho.Makho}, HangSX: {vtPhieunhapkho.HangSX}, SL: {vtPhieunhapkho.SL}");

            var khotong = TimKhoTong(vtPhieunhapkho);

            if (khotong != null)
            {
                Debug.WriteLine($"✅ Tìm thấy kho tổng - Makho: {khotong.Makho}, SL hiện tại: {khotong.SL}");

                // Cộng số lượng vào khotong đã tồn tại
                int slCu = khotong.SL ?? 0;
                int slThem = vtPhieunhapkho.SL ?? 0;
                khotong.SL = slCu + slThem;
                Debug.WriteLine($"SL sau khi cộng: {slCu} + {slThem} = {khotong.SL}");

                // Cập nhật thông tin nếu cần (ưu tiên thông tin mới hơn)
                if (!string.IsNullOrEmpty(vtPhieunhapkho.TenSanpham))
                {
                    khotong.TenSanpham = vtPhieunhapkho.TenSanpham;
                }
                if (!string.IsNullOrEmpty(vtPhieunhapkho.NhaCC))
                {
                    khotong.NhaCC = vtPhieunhapkho.NhaCC;
                }
                if (!string.IsNullOrEmpty(vtPhieunhapkho.DonVi))
                {
                    khotong.DonVi = vtPhieunhapkho.DonVi;
                }

                // Cập nhật Makho nếu chưa có hoặc khác
                if (string.IsNullOrEmpty(khotong.Makho) || khotong.Makho != vtPhieunhapkho.Makho)
                {
                    khotong.Makho = vtPhieunhapkho.Makho;
                }

                _context.khotongs.Update(khotong);
                Debug.WriteLine($"✅ Đã cập nhật kho tổng");
            }
            else
            {
                Debug.WriteLine($"⚠️ Không tìm thấy kho tổng, tạo mới");

                // Tạo mới vật tư trong tồn kho nếu chưa có
                var newKhotong = new khotongs
                {
                    TenSanpham = vtPhieunhapkho.TenSanpham,
                    MaSanpham = vtPhieunhapkho.MaSanpham,
                    HangSX = vtPhieunhapkho.HangSX,
                    NhaCC = vtPhieunhapkho.NhaCC,
                    SL = vtPhieunhapkho.SL ?? 0,
                    DonVi = vtPhieunhapkho.DonVi,
                    Makho = vtPhieunhapkho.Makho,
                    NgayNhapkho = DateTime.Now,
                    TrangThai = "Tồn kho"
                };
                _context.khotongs.Add(newKhotong);
                Debug.WriteLine($"✅ Đã tạo mới kho tổng - Makho: {newKhotong.Makho}, SL: {newKhotong.SL}");
            }

            Debug.WriteLine($"========== DEBUG CongVaoKhoTong END ==========");
        }


        private void DongBoTrangThaiVatTuYeuCau(phieunhapkho phieunhapkho, List<vtphieunhapkho> vtPhieunhapkhoList)
        {
            var maYeucauList = vtPhieunhapkhoList
                .Select(v => v.MaYeucau)
                .Where(ma => !string.IsNullOrEmpty(ma))
                .Distinct()
                .ToList();

            foreach (var maYc in maYeucauList)
            {
                // Lấy thông tin yêu cầu để phân biệt "Yêu cầu vật tư" và "Yêu cầu nhập kho"
                var yeuCau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == maYc);
                string tenYeuCau = yeuCau?.TenYeucau ?? string.Empty;

                var vtYeuCauList = _context.vtyeucau
                    .Where(v => v.VTMaYeucau == maYc)
                    .ToList();

                // Lấy danh sách vật tư đã nhập cho yêu cầu này (chỉ lấy những vật tư có trạng thái "Đã nhập kho")
                var vtDaNhapChoYeuCau = vtPhieunhapkhoList
                    .Where(v => v.MaYeucau == maYc &&
                                !string.IsNullOrEmpty(v.MaSanpham) &&
                                v.TrangThai == "Đã nhập kho")
                    .GroupBy(v => v.MaSanpham)
                    .ToDictionary(g => g.Key, g => g.Sum(v => v.SL ?? 0));

                // Đồng bộ trạng thái từ vtphieunhapkho sang vtyeucau
                foreach (var vtYc in vtYeuCauList)
                {
                    if (!string.IsNullOrEmpty(vtYc.MaSanpham))
                    {
                        bool coVatTuDaNhap = vtDaNhapChoYeuCau.ContainsKey(vtYc.MaSanpham)
                            && vtDaNhapChoYeuCau[vtYc.MaSanpham] > 0;

                        if (coVatTuDaNhap)
                        {
                            // Với "Yêu cầu nhập kho" → chi tiết vtyeucau (nếu có) giữ trạng thái "Đã nhập kho"
                            // Với "Yêu cầu vật tư" → khi hàng đã nhập về kho cho yêu cầu này thì coi như đã cấp đủ: đặt "Đã xuất kho"
                            string trangThaiSauNhap =
                                string.Equals(tenYeuCau, "Yêu cầu nhập kho", StringComparison.OrdinalIgnoreCase)
                                    ? "Đã nhập kho"
                                    : "Đã xuất kho";

                            if (!string.Equals(vtYc.TrangThai, trangThaiSauNhap, StringComparison.OrdinalIgnoreCase))
                            {
                                vtYc.TrangThai = trangThaiSauNhap;
                                _context.vtyeucau.Update(vtYc);
                                Console.WriteLine($"✅ Đồng bộ trạng thái vtyeucau: MaSP={vtYc.MaSanpham}, Trạng thái mới='{trangThaiSauNhap}'");
                            }
                        }
                        // Logic cũ: xử lý trường hợp "Đang mua hàng" hoặc "Đang chờ báo giá"
                        else if ((vtYc.TrangThai == "Đang mua hàng" || vtYc.TrangThai == "Đang chờ báo giá"))
                        {
                            int soLuongConThieu = TinhSoLuongConThieu(maYc, vtYc.MaSanpham);

                            if (coVatTuDaNhap && soLuongConThieu > 0)
                            {
                                vtYc.TrangThai = "Chờ xuất kho";
                                _context.vtyeucau.Update(vtYc);
                                Console.WriteLine($"✅ Cập nhật trạng thái vtyeucau: MaSP={vtYc.MaSanpham}, Trạng thái mới='Chờ xuất kho', Còn thiếu={soLuongConThieu}");
                            }
                            else if (coVatTuDaNhap && soLuongConThieu <= 0)
                            {
                                vtYc.TrangThai = "Chờ xuất kho";
                                _context.vtyeucau.Update(vtYc);
                                Console.WriteLine($"✅ Đã nhập đủ cho vtyeucau: MaSP={vtYc.MaSanpham}, Trạng thái mới='Chờ xuất kho'");
                            }
                        }
                    }
                }

                // Đồng bộ trạng thái yêu cầu: dùng helper để tự nhận diện loại yêu cầu
                YeucauUpdateHelper.DongBoTrangThaiYeuCau(_context, maYc);
                Console.WriteLine($"✅ Đồng bộ trạng thái yeucau: MaYeucau={maYc}");
            }
        }


        private void XuLyNhapKho(phieunhapkho phieunhapkho, List<vtphieunhapkho> vtPhieunhapkhoList)
        {
            phieunhapkho.TrangThai = "Đã nhập kho";
            // Lưu thời gian nhập kho khi bộ phận kho nhập kho
            phieunhapkho.NgayNhapkho = DateTime.Now;
            bool isNhapKhoDuanOrCaNhan = IsNhapKhoDuanOrCaNhan(phieunhapkho);

            foreach (var vtPhieunhapkho in vtPhieunhapkhoList)
            {
                // Nếu là nhập kho dự án/cá nhân, trừ số lượng từ kho dự án/cá nhân
                if (isNhapKhoDuanOrCaNhan)
                {
                    if (!string.IsNullOrEmpty(phieunhapkho.MaDuan) && !string.IsNullOrEmpty(vtPhieunhapkho.MaSanpham))
                    {
                        TruKhoDuanKhiNhapKho(phieunhapkho, vtPhieunhapkho);
                    }
                    else if (string.IsNullOrEmpty(phieunhapkho.MaDuan) && !string.IsNullOrEmpty(vtPhieunhapkho.MaSanpham) && !string.IsNullOrEmpty(phieunhapkho.MaNguoidung))
                    {
                        TruKhoCaNhanKhiNhapKho(phieunhapkho, vtPhieunhapkho);
                    }
                }

                // Cộng vào kho tổng (cho cả phiếu từ mua hàng và phiếu từ dự án/cá nhân)
                CongVaoKhoTong(vtPhieunhapkho);

                // Cập nhật trạng thái vật tư phiếu nhập kho
                var vtPhieunhapkhoDb = _context.vtphieunhapkho
                    .FirstOrDefault(vt => vt.MaNhapkho == vtPhieunhapkho.MaNhapkho && vt.ID == vtPhieunhapkho.ID);
                if (vtPhieunhapkhoDb != null)
                {
                    vtPhieunhapkhoDb.TrangThai = "Đã nhập kho";
                    _context.vtphieunhapkho.Update(vtPhieunhapkhoDb);
                }

                // Đồng bộ luôn vào danh sách đang xử lý để logic phía sau
                // (DongBoTrangThaiVatTuYeuCau) nhìn thấy đúng trạng thái "Đã nhập kho"
                vtPhieunhapkho.TrangThai = "Đã nhập kho";
            }


            DongBoTrangThaiVatTuYeuCau(phieunhapkho, vtPhieunhapkhoList);

            // Tự động tạo phiếu xuất kho sau khi nhập kho
            // Sử dụng logic đơn giản: nếu có MaYeucau thì tạo/cập nhật phiếu xuất kho với tất cả vật tư vừa nhập
            bool isNhapKhoOnlyFlow = !string.IsNullOrEmpty(phieunhapkho.MaYeucau)
                && _context.yeucau.Any(y => y.MaYeucau == phieunhapkho.MaYeucau
                    && y.TenYeucau == "Yêu cầu nhập kho");

            if (!string.IsNullOrEmpty(phieunhapkho.MaYeucau) && !isNhapKhoOnlyFlow)
            {
                TaoPhieuXuatKhoSauNhapKho(phieunhapkho, vtPhieunhapkhoList);
            }

        }


        private void TaoPhieuXuatKhoSauNhapKho(phieunhapkho phieunhapkho, List<vtphieunhapkho> vtPhieunhapkhoList)
        {
            try
            {
                Console.WriteLine($"[AUTO-PXK] Bắt đầu tạo PXK sau khi nhập kho - MaYeucau={phieunhapkho.MaYeucau}, MaNhapkho={phieunhapkho.MaNhapkho}");

                // Kiểm tra xem đã có phiếu xuất kho chưa hoàn thành cho yêu cầu này chưa
                var phieuXuatLienQuan = _context.phieuxuatkho
                    .Where(px => px.MaYeucau == phieunhapkho.MaYeucau)
                    .ToList();

                var trangThaiHoanThanh = new[] { "Hoàn thành", "Đã xuất kho" };
                var phieuXuatKhoHienTai = phieuXuatLienQuan
                    .FirstOrDefault(px => !trangThaiHoanThanh.Contains(px.TrangThai ?? "", StringComparer.OrdinalIgnoreCase));

                string maXuatkho = null;

                if (phieuXuatKhoHienTai == null)
                {
                    // Chưa có phiếu xuất kho chưa hoàn thành, tạo mới
                    maXuatkho = TaoMaXuatKhoMoi(phieunhapkho.MaDuan, phieunhapkho.MaYeucau);
                    phieuXuatKhoHienTai = new phieuxuatkho
                    {
                        MaXuatkho = maXuatkho,
                        MaYeucau = phieunhapkho.MaYeucau,
                        MaDuan = phieunhapkho.MaDuan,
                        MaNguoidung = phieunhapkho.MaNguoidung,
                        NgayXuatkho = DateTime.Now,
                        NgayChuanBi = DateTime.Now,
                        TrangThai = "Đang chuẩn bị hàng",
                        GhiChu = $"Tự động tạo từ phiếu nhập kho {phieunhapkho.MaNhapkho}"
                    };
                    _context.phieuxuatkho.Add(phieuXuatKhoHienTai);
                    _context.SaveChanges();
                    Console.WriteLine($"[AUTO-PXK] ✅ Đã tạo phiếu xuất kho mới: {maXuatkho}");
                }
                else
                {
                    // Đã có phiếu xuất kho chưa hoàn thành, sử dụng phiếu đó
                    maXuatkho = phieuXuatKhoHienTai.MaXuatkho;
                    Console.WriteLine($"[AUTO-PXK] ℹ️ Sử dụng phiếu xuất kho hiện có: {maXuatkho}");
                }

                // Phân bổ hàng nhập cho tất cả yêu cầu có cùng mã cơ bản (cùng dự án)
                // Đảm bảo các PXK liên quan (kể cả cùng mã yêu cầu gốc) đều nhận được vật tư
                if (vtPhieunhapkhoList != null && vtPhieunhapkhoList.Any())
                {
                    foreach (var vtNhap in vtPhieunhapkhoList)
                    {
                        if (vtNhap == null || string.IsNullOrWhiteSpace(vtNhap.MaSanpham))
                            continue;

                        var soLuongNhap = vtNhap.SL ?? 0;
                        if (soLuongNhap <= 0)
                            continue;

                        // Dùng hàm phân bổ theo mã yêu cầu cơ bản để tự tạo/ghép PXK cho các yêu cầu cùng dự án
                        PhanBoHangNhapTheoYeuCau(phieunhapkho.MaYeucau, vtNhap.MaSanpham, soLuongNhap, vtNhap, phieunhapkho.MaNhapkho);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AUTO-PXK] ❌ Lỗi khi tạo phiếu xuất kho: {ex.Message}");
                Console.WriteLine($"[AUTO-PXK] Stack trace: {ex.StackTrace}");
            }
        }


        private string TaoMaXuatKhoMoi(string? maDuan, string? maYeucau)
        {
            return _phieuCodeService.GenerateMaXuatKho(maDuan, maYeucau);
        }




        private void KiemTraVaCapNhatTrangThaiPhieuXuatKho(List<phieuxuatkho> phieuXuatCapNhatList)
        {
            foreach (var phieuXuatKho in phieuXuatCapNhatList)
            {
                var vtPhieuxuatkhoList = _context.vtphieuxuatkho
                    .Where(vt => vt.MaXuatkho == phieuXuatKho.MaXuatkho)
                    .ToList();

                bool duHang = true;
                var vatTuThieu = new List<vtphieuxuatkho>();

                foreach (var vtXuatkho in vtPhieuxuatkhoList)
                {
                    // Tìm khotong theo thứ tự ưu tiên
                    khotongs khotong = TimKhoTongChoXuatKho(vtXuatkho);

                    // Tính số lượng hàng đã cam kết
                    int soLuongDaCamKet = this.TinhSoLuongDaCamKet(vtXuatkho.Makho ?? "", vtXuatkho.MaSanpham ?? "", phieuXuatKho.MaXuatkho);

                    // Tính tổng số lượng tồn kho
                    int tongSoLuongTonKho = TinhTongSoLuongTonKho(khotong, vtXuatkho);

                    // Số lượng khả dụng = Tổng tồn kho - Số lượng đã cam kết
                    int soLuongKhaDung = tongSoLuongTonKho - soLuongDaCamKet;

                    // Kiểm tra chặt chẽ: không có hàng, số lượng khả dụng <= 0, hoặc không đủ số lượng
                    if (tongSoLuongTonKho <= 0 || soLuongKhaDung <= 0 || soLuongKhaDung < vtXuatkho.SL)
                    {
                        duHang = false;
                        vatTuThieu.Add(vtXuatkho);
                        Console.WriteLine($"CẢNH BÁO: Không đủ hàng cho {vtXuatkho.TenSanpham} - Tồn kho: {tongSoLuongTonKho}, Đã cam kết: {soLuongDaCamKet}, Khả dụng: {soLuongKhaDung}, Yêu cầu: {vtXuatkho.SL}");
                    }
                }

                if (duHang)
                {
                    // Đủ hàng → tự động chuyển sang "Đang chuẩn bị hàng"
                    if (phieuXuatKho.TrangThai == "Chờ xác nhận")
                    {
                        phieuXuatKho.TrangThai = "Đang chuẩn bị hàng";
                        phieuXuatKho.NgayChuanBi = DateTime.Now;
                        _context.phieuxuatkho.Update(phieuXuatKho);
                    }

                    // Cập nhật trạng thái vật tư
                    foreach (var vtXuatkho in vtPhieuxuatkhoList)
                    {
                        if (vtXuatkho.TrangThai == "Chờ xác nhận")
                        {
                            vtXuatkho.TrangThai = "Đang chuẩn bị hàng";
                            _context.vtphieuxuatkho.Update(vtXuatkho);
                        }
                    }

                    _context.SaveChanges();
                    Console.WriteLine($"Đã bổ sung vật tư vào phiếu xuất kho {phieuXuatKho.MaXuatkho} và chuyển sang trạng thái 'Đang chuẩn bị hàng'");
                }
                else
                {
                    // Thiếu hàng
                    if (phieuXuatKho.TrangThai == "Chờ xác nhận")
                    {
                        phieuXuatKho.TrangThai = "Thiếu hàng - Đã tạo phiếu mua";
                        phieuXuatKho.GhiChu = "Không đủ số lượng tồn kho. Đã tự động tạo phiếu mua hàng.";
                        _context.phieuxuatkho.Update(phieuXuatKho);

                        // Tạo phiếu mua hàng tự động
                        TaoPhieuMuaHangTuDong(phieuXuatKho, vatTuThieu);
                    }

                    _context.SaveChanges();
                    Console.WriteLine($"Đã bổ sung vật tư vào phiếu xuất kho {phieuXuatKho.MaXuatkho} nhưng thiếu hàng");
                }
            }
        }


        private khotongs TimKhoTongChoXuatKho(vtphieuxuatkho vtXuatkho)
        {
            khotongs khotong = null;

            // Ưu tiên 1: Tìm theo MaSanpham + Makho chính xác
            if (!string.IsNullOrEmpty(vtXuatkho.MaSanpham) && !string.IsNullOrEmpty(vtXuatkho.Makho))
            {
                khotong = _context.khotongs.FirstOrDefault(k =>
                    k.MaSanpham == vtXuatkho.MaSanpham &&
                    k.Makho == vtXuatkho.Makho);
            }

            // Ưu tiên 2: Nếu không tìm thấy, tìm theo MaSanpham + HangSX + Makho
            if (khotong == null && !string.IsNullOrEmpty(vtXuatkho.MaSanpham) && !string.IsNullOrEmpty(vtXuatkho.HangSX) && !string.IsNullOrEmpty(vtXuatkho.Makho))
            {
                khotong = _context.khotongs.FirstOrDefault(k =>
                    k.MaSanpham == vtXuatkho.MaSanpham &&
                    k.HangSX == vtXuatkho.HangSX &&
                    k.Makho == vtXuatkho.Makho);
            }

            // Ưu tiên 3: Nếu vẫn không tìm thấy, tìm theo MaSanpham + HangSX (bỏ qua Makho)
            if (khotong == null && !string.IsNullOrEmpty(vtXuatkho.MaSanpham) && !string.IsNullOrEmpty(vtXuatkho.HangSX))
            {
                khotong = _context.khotongs.FirstOrDefault(k =>
                    k.MaSanpham == vtXuatkho.MaSanpham &&
                    k.HangSX == vtXuatkho.HangSX);
            }

            return khotong;
        }


        private int TinhTongSoLuongTonKho(khotongs khotong, vtphieuxuatkho vtXuatkho)
        {
            if (khotong != null)
            {
                return _context.khotongs
                    .Where(k => k.Makho == khotong.Makho && k.MaSanpham == khotong.MaSanpham)
                    .Sum(k => k.SL ?? 0);
            }
            else if (!string.IsNullOrEmpty(vtXuatkho.MaSanpham) && !string.IsNullOrEmpty(vtXuatkho.HangSX))
            {
                return _context.khotongs
                    .Where(k => k.MaSanpham == vtXuatkho.MaSanpham && k.HangSX == vtXuatkho.HangSX)
                    .Sum(k => k.SL ?? 0);
            }
            return 0;
        }
        [HttpPost]
        public IActionResult Xuliphieunhapkho(
                                string MaNhapkho, string action,
                                phieunhapkho phieunhapkho,
                                vtphieunhapkho vtphieunhapkho, phieuxuatkho phieuxuatkho, vtphieuxuatkho vtphieuxuatkho)
        {
            var chucVu2 = HttpContext.Session.GetString("Chucvu");
            var boPhan2 = HttpContext.Session.GetString("Bophan");
            var maNv2 = HttpContext.Session.GetString("MaNguoidung");

            var Phieunhapkho = _context.phieunhapkho.FirstOrDefault(p => p.MaNhapkho == MaNhapkho);
            var VTPhieunhapkholist = _context.vtphieunhapkho.Where(vt => vt.MaNhapkho == MaNhapkho).ToList();

            if (Phieunhapkho == null)
            {
                TempData["Error"] = "Không tìm thấy phiếu nhập kho!";
                return RedirectToAction("Phieunhapkho", "Yeucau", new { area = "TruongBPKho" });
            }

            if (action == "approve")
            {
                if (boPhan2 == "BP kho" && Phieunhapkho.TrangThai == "Chờ nhập kho")
                {
                    Debug.WriteLine($"========== BEFORE XuLyNhapKho ==========");
                    KiemTraKhoDuanTruocKhiNhapKho(Phieunhapkho, VTPhieunhapkholist);

                    XuLyNhapKho(Phieunhapkho, VTPhieunhapkholist);

                    Debug.WriteLine($"========== AFTER XuLyNhapKho ==========");
                    KiemTraKhoDuanSauKhiNhapKho(Phieunhapkho, VTPhieunhapkholist);
                }


                _context.phieunhapkho.Update(Phieunhapkho);


                _context.SaveChanges();

                // Gửi email thông báo cho nhân viên kho khi có phiếu nhập kho cần xử lý
                try
                {
                    if (Phieunhapkho.TrangThai == "Đã nhập kho")
                    {
                        _ = _emailService.SendNotificationToWarehouseOnNhapKhoAsync(MaNhapkho);

                        // Gửi email thông báo cho người yêu cầu khi nhập kho xong
                        _ = _emailService.SendNotificationToRequesterOnNhapKhoAsync(MaNhapkho);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[TruongBPKho/Xuliphieunhapkho] Lỗi gửi email nhập kho: {ex.Message}");
                }

                KiemTraVaCapNhatPhieuXuatKhoThieuHang();

                TempData["Success"] = $"Đã nhập kho thành công!";
                // Sau khi nhập kho xong quay lại danh sách yêu cầu,
                // để người dùng vẫn thấy đầy đủ các yêu cầu như mong muốn.
                return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPKho" });
            }
            else if (action == "reject")
            {
                if (Phieunhapkho != null)
                {
                    Phieunhapkho.TrangThai = "Đã từ chối";
                    _context.phieunhapkho.Update(Phieunhapkho);
                    _context.SaveChanges();
                }
                return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPKho" });
            }

            return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPKho" });
        }

        private void Xulituchoiyeucau(
                        string Ma,
                        yeucau yeucau,
                        vtyeucau vtyeucau,
                        phieumuahang phieumuahang,
                        vtphieumuahang vtphieumuahang)
        {
            var chucVu = HttpContext.Session.GetString("Chucvu");
            var boPhan = HttpContext.Session.GetString("Bophan");
            var maNv = HttpContext.Session.GetString("MaNguoidung");

            if (!Ma.Contains("PMH"))
            {
                var Phieu = _context.yeucau.FirstOrDefault(p => p.MaYeucau == Ma);
                if (Phieu != null)
                {
                    Phieu.TrangThai = $"{chucVu} - Đã từ chối";
                    _context.yeucau.Update(Phieu);

                    var Listvtyeucau = _context.vtyeucau.Where(p => p.VTMaYeucau == Ma).ToList();
                    foreach (var VTyeucau in Listvtyeucau)
                    {
                        VTyeucau.TrangThai = $"{chucVu} - Đã từ chối";
                        _context.vtyeucau.Update(VTyeucau);
                    }

                    _context.SaveChanges();
                }
            }
            else
            {
                var Phieumuahang = _context.phieumuahang.FirstOrDefault(p => p.MaMuahang == Ma);
                if (Phieumuahang != null)
                {
                    Phieumuahang.TrangThai = $"{chucVu} - Đã từ chối";
                    _context.phieumuahang.Update(Phieumuahang);

                    var Listvtmuahang = _context.vtphieumuahang.Where(p => p.MaMuahang == Ma).ToList();
                    foreach (var VTmuahang in Listvtmuahang)
                    {
                        VTmuahang.TrangThai = $"{chucVu} - Đã từ chối";
                        _context.vtphieumuahang.Update(VTmuahang);
                    }
                    _context.SaveChanges();
                }
            }
        }
        [HttpGet]
        public IActionResult GetDulieuThongbaotrangchu()
        {
            var chucVu = HttpContext.Session.GetString("Chucvu");
            var boPhan = HttpContext.Session.GetString("Bophan");
            var maNv = HttpContext.Session.GetString("MaNguoidung");

            int Dangchobaogiacount = _context.phieumuahang.Count(p => p.TrangThai == "Đang chờ báo giá");
            int Dabaogiacount = _context.phieumuahang.Count(p => p.TrangThai == "Đã báo giá");
            int Chothanhtoancount = _context.phieumuahang.Count(p => p.TrangThai == "Chờ thanh toán");
            int Dathanhtoancount = _context.phieumuahang.Count(p => p.TrangThai == "Đã thanh toán");
            int thongbaomuahangcount = 0;
            if (boPhan == "BP mua hàng")
            {
                thongbaomuahangcount = Dangchobaogiacount;
            }
            else if (boPhan == "BP kỹ thuật" && chucVu == "Giám đốc")
            {
                thongbaomuahangcount = Dabaogiacount;
            }
            else if (boPhan == "BP kế toán")
            {
                thongbaomuahangcount = Chothanhtoancount;
            }

            // Xuất kho - chỉ đếm các trạng thái còn cần xử lý (không đếm "Hoàn thành")
            int thongbaoxuatkhocount = 0;
            if (boPhan == "BP kho")
            {
                thongbaoxuatkhocount = _context.phieuxuatkho.Count(p => p.TrangThai != "Hoàn thành");
            }

            // Nhập kho
            int thongbaonhapkhocount = 0;
            if (boPhan == "BP kho")
            {
                thongbaonhapkhocount = _context.phieunhapkho.Count(p => p.TrangThai == "Chờ nhập kho" || p.TrangThai == "Sẵn sàng nhập kho");
            }

            // Yêu cầu - Với BP kho: đếm phiếu xuất kho "Chờ xác nhận" + yêu cầu chờ duyệt kho, các bộ phận khác đếm yêu cầu chờ duyệt
            int thongbaoyeucaucount = 0;
            if (boPhan == "BP kho")
            {
                // Đếm phiếu xuất kho đang chờ kho xác nhận và xử lý
                int phieuxuatkhocount = _context.phieuxuatkho.Count(p => p.TrangThai == "Chờ xác nhận");
                // Đếm yêu cầu chờ duyệt của kho (nếu có)
                int yeucauchoduyetcount = _context.yeucau.Count(p => p.TrangThai == ("Chờ Trưởng Phòng bộ phận " + boPhan + " duyệt"));
                thongbaoyeucaucount = phieuxuatkhocount + yeucauchoduyetcount;
            }
            else
            {
                var Maduanquanli = _context.duans
                    .Where(da => da.MaNguoiQLDA == maNv)
                    .Select(da => da.MaDuan)
                    .ToList();

                int QLDAyeucaucount = _context.yeucau.Count(p => p.TrangThai == "Quản lí dự án" && Maduanquanli.Contains(p.YCMaDuan));
                int Duyetyeucaucount = _context.yeucau.Count(p => p.TrangThai == (chucVu + "-" + boPhan));
                int Giamdocyeucaucount = _context.yeucau.Count(p => p.TrangThai == "Giám đốc");

                if (chucVu == "Giám đốc")
                {
                    thongbaoyeucaucount = Giamdocyeucaucount;
                }
                else if (Duyetyeucaucount != 0 || QLDAyeucaucount != 0)
                {
                    thongbaoyeucaucount = Duyetyeucaucount + QLDAyeucaucount;
                }
            }

            return Json(new
            {
                thongbaoyeucaucount,
                thongbaomuahangcount,
                thongbaoxuatkhocount,
                thongbaonhapkhocount
            });
        }



        [HttpGet]
        public IActionResult AutoDongBoTrangThai()
        {
            try
            {
                var currentUserId = HttpContext.Session.GetString("MaNguoidung");

                // Lấy các yêu cầu mà kỹ thuật viên này đã tạo
                var yeuCauList = _context.yeucau
                    .Where(y => y.YCMaNguoidung == currentUserId)
                    .Select(y => y.MaYeucau)
                    .ToList();

                return Json(new { success = true, message = "Không có phiếu nào cần đồng bộ!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        // In phiếu nhập kho
        [HttpGet]
        public IActionResult InPhieunhapkho(string MaNhapkho)
        {
            if (string.IsNullOrEmpty(MaNhapkho))
            {
                return NotFound();
            }

            var phieunhapkho = _context.phieunhapkho
                .FirstOrDefault(p => p.MaNhapkho == MaNhapkho);

            if (phieunhapkho == null)
            {
                return NotFound();
            }

            var vtphieunhapkho = _context.vtphieunhapkho
                .Where(vt => vt.MaNhapkho == MaNhapkho)
                .ToList();

            // Lấy giá tiền từ phiếu mua hàng nếu phiếu nhập kho không có giá
            if (!string.IsNullOrEmpty(phieunhapkho.MaYeucau))
            {
                var phieumuahang = _context.phieumuahang
                    .FirstOrDefault(pm => pm.MaYeucau == phieunhapkho.MaYeucau);

                if (phieumuahang != null)
                {
                    var vtphieumuahang = _context.vtphieumuahang
                        .Where(vt => vt.MaMuahang == phieumuahang.MaMuahang)
                        .ToList();

                    // Cập nhật giá tiền cho các vật tư trong phiếu nhập kho từ phiếu mua hàng
                    foreach (var vtnhapkho in vtphieunhapkho)
                    {
                        // Tìm vật tư tương ứng trong phiếu mua hàng theo mã sản phẩm và tên sản phẩm
                        var vtmuahang = vtphieumuahang.FirstOrDefault(vt =>
                            vt.MaSanpham == vtnhapkho.MaSanpham &&
                            vt.TenSanpham == vtnhapkho.TenSanpham);

                        if (vtmuahang != null)
                        {
                            // Nếu phiếu nhập kho không có giá hoặc giá = 0, lấy từ phiếu mua hàng
                            if (vtnhapkho.DonGia == null || vtnhapkho.DonGia == 0)
                            {
                                vtnhapkho.DonGia = vtmuahang.DonGia;
                            }

                            if (vtnhapkho.ThanhTien == null || vtnhapkho.ThanhTien == 0)
                            {
                                vtnhapkho.ThanhTien = vtmuahang.ThanhTien;

                                // Nếu thành tiền vẫn = 0, tính từ đơn giá * số lượng
                                if ((vtnhapkho.ThanhTien == null || vtnhapkho.ThanhTien == 0) &&
                                    vtnhapkho.DonGia != null && vtnhapkho.DonGia > 0 &&
                                    vtnhapkho.SL != null && vtnhapkho.SL > 0)
                                {
                                    vtnhapkho.ThanhTien = vtnhapkho.DonGia * vtnhapkho.SL;
                                }
                            }
                        }
                    }
                }
            }

            var yeucau = _context.yeucau
                .FirstOrDefault(y => y.MaYeucau == phieunhapkho.MaYeucau);

            ViewBag.Phieunhapkho = phieunhapkho;
            ViewBag.VTPhieunhapkho = vtphieunhapkho;
            ViewBag.Yeucau = yeucau;

            return View();
        }

        // In phiếu trả hàng
        [HttpGet]
        public IActionResult InPhietrahang(string MaNhapkho)
        {
            if (string.IsNullOrEmpty(MaNhapkho))
            {
                return NotFound();
            }

            var phieunhapkho = _context.phieunhapkho
                .FirstOrDefault(p => p.MaNhapkho == MaNhapkho);

            if (phieunhapkho == null)
            {
                return NotFound();
            }

            var vtphieunhapkho = _context.vtphieunhapkho
                .Where(vt => vt.MaNhapkho == MaNhapkho)
                .ToList();

            var yeucau = _context.yeucau
                .FirstOrDefault(y => y.MaYeucau == phieunhapkho.MaYeucau);

            var nguoiBanGiao = !string.IsNullOrEmpty(phieunhapkho.MaNguoidung)
                ? _context.nguoidungs.FirstOrDefault(n => n.MaNguoidung == phieunhapkho.MaNguoidung)
                : null;

            string tenNguoiBanGiao = "";
            string bophanNguoiBanGiao = "";
            if (yeucau != null)
            {
                tenNguoiBanGiao = yeucau.NguoiYeucau ?? "";
                var nguoiYeuCau = !string.IsNullOrEmpty(yeucau.YCMaNguoidung)
                    ? _context.nguoidungs.FirstOrDefault(n => n.MaNguoidung == yeucau.YCMaNguoidung)
                    : null;
                bophanNguoiBanGiao = nguoiYeuCau?.Bophan ?? "";
            }
            else if (nguoiBanGiao != null)
            {
                tenNguoiBanGiao = nguoiBanGiao.TenNguoidung ?? "";
                bophanNguoiBanGiao = nguoiBanGiao.Bophan ?? "";
            }

            // Lấy thông tin dự án
            var duan = !string.IsNullOrEmpty(phieunhapkho.MaDuan)
                ? _context.duans.FirstOrDefault(d => d.MaDuan == phieunhapkho.MaDuan)
                : null;
            string tenDuan = duan?.TenDuan ?? "";
            string maDuan = duan?.MaDuan ?? "";

            // Lấy thông tin Trưởng BP Kho làm người nhận
            var nguoiNhan = _context.nguoidungs
                .FirstOrDefault(n => n.Chucvu == "Trưởng BP" && n.Bophan == "BP kho");

            ViewBag.Phieunhapkho = phieunhapkho;
            ViewBag.VTPhieunhapkho = vtphieunhapkho;
            ViewBag.Yeucau = yeucau;
            ViewBag.TenNguoiBanGiao = tenNguoiBanGiao;
            ViewBag.BophanNguoiBanGiao = bophanNguoiBanGiao;
            ViewBag.TenDuan = tenDuan;
            ViewBag.MaDuan = maDuan;
            ViewBag.NguoiNhan = nguoiNhan;

            return View();
        }

        // In phiếu xuất kho
        [HttpGet]
        public IActionResult InPhieuxuatkho(string MaXuatkho)
        {
            if (string.IsNullOrEmpty(MaXuatkho))
            {
                return NotFound();
            }

            var phieuxuatkho = _context.phieuxuatkho
                .FirstOrDefault(p => p.MaXuatkho == MaXuatkho);

            if (phieuxuatkho == null)
            {
                return NotFound();
            }

            var vtphieuxuatkho = _context.vtphieuxuatkho
                .Where(vt => vt.MaXuatkho == MaXuatkho)
                .ToList();

            // Lấy giá tiền từ phiếu nhập kho hoặc phiếu mua hàng nếu phiếu xuất kho không có giá
            if (!string.IsNullOrEmpty(phieuxuatkho.MaYeucau))
            {
                // Bước 1: Thử lấy từ phiếu nhập kho
                var phieunhapkho = _context.phieunhapkho
                    .FirstOrDefault(pn => pn.MaYeucau == phieuxuatkho.MaYeucau);

                if (phieunhapkho != null)
                {
                    var vtphieunhapkho = _context.vtphieunhapkho
                        .Where(vt => vt.MaNhapkho == phieunhapkho.MaNhapkho)
                        .ToList();

                    // Cập nhật giá tiền cho các vật tư trong phiếu xuất kho từ phiếu nhập kho
                    foreach (var vtxuatkho in vtphieuxuatkho)
                    {
                        // Tìm vật tư tương ứng trong phiếu nhập kho theo mã sản phẩm, nếu không thấy mới so sánh tên
                        var vtnhapkho = vtphieunhapkho.FirstOrDefault(vt =>
                            !string.IsNullOrEmpty(vt.MaSanpham) &&
                            vt.MaSanpham == vtxuatkho.MaSanpham);

                        if (vtnhapkho == null)
                        {
                            vtnhapkho = vtphieunhapkho.FirstOrDefault(vt =>
                                !string.IsNullOrEmpty(vt.TenSanpham) &&
                                vt.TenSanpham == vtxuatkho.TenSanpham);
                        }

                        if (vtnhapkho != null)
                        {
                            // Nếu phiếu xuất kho không có giá hoặc giá = 0, lấy từ phiếu nhập kho
                            if (vtxuatkho.DonGia == null || vtxuatkho.DonGia == 0)
                            {
                                if (vtnhapkho.DonGia != null && vtnhapkho.DonGia > 0)
                                {
                                    vtxuatkho.DonGia = vtnhapkho.DonGia;
                                }
                            }

                            if (vtxuatkho.ThanhTien == null || vtxuatkho.ThanhTien == 0)
                            {
                                // Tính thành tiền theo tỷ lệ số lượng xuất / số lượng nhập
                                if (vtnhapkho.SL > 0 && vtnhapkho.ThanhTien != null && vtnhapkho.ThanhTien > 0 && vtxuatkho.SL > 0)
                                {
                                    vtxuatkho.ThanhTien = (vtnhapkho.ThanhTien.Value / vtnhapkho.SL.Value) * vtxuatkho.SL.Value;
                                }
                                else if (vtxuatkho.DonGia != null && vtxuatkho.DonGia > 0 && vtxuatkho.SL > 0)
                                {
                                    // Nếu thành tiền vẫn = 0, tính từ đơn giá * số lượng
                                    vtxuatkho.ThanhTien = vtxuatkho.DonGia * vtxuatkho.SL;
                                }
                            }
                        }
                    }
                }

                // Bước 2: Nếu phiếu nhập kho không có giá, lấy từ phiếu mua hàng
                var phieumuahang = _context.phieumuahang
                    .FirstOrDefault(pm => pm.MaYeucau == phieuxuatkho.MaYeucau);

                if (phieumuahang != null)
                {
                    var vtphieumuahang = _context.vtphieumuahang
                        .Where(vt => vt.MaMuahang == phieumuahang.MaMuahang)
                        .ToList();

                    // Cập nhật giá tiền cho các vật tư trong phiếu xuất kho từ phiếu mua hàng
                    foreach (var vtxuatkho in vtphieuxuatkho)
                    {
                        // Chỉ cập nhật nếu chưa có giá từ phiếu nhập kho
                        if (vtxuatkho.DonGia == null || vtxuatkho.DonGia == 0 ||
                            vtxuatkho.ThanhTien == null || vtxuatkho.ThanhTien == 0)
                        {
                            // Tìm vật tư tương ứng trong phiếu mua hàng theo mã sản phẩm, nếu không thấy mới so sánh tên
                            var vtmuahang = vtphieumuahang.FirstOrDefault(vt =>
                                !string.IsNullOrEmpty(vt.MaSanpham) &&
                                vt.MaSanpham == vtxuatkho.MaSanpham);

                            if (vtmuahang == null)
                            {
                                vtmuahang = vtphieumuahang.FirstOrDefault(vt =>
                                    !string.IsNullOrEmpty(vt.TenSanpham) &&
                                    vt.TenSanpham == vtxuatkho.TenSanpham);
                            }

                            if (vtmuahang != null)
                            {
                                // Nếu phiếu xuất kho không có giá hoặc giá = 0, lấy từ phiếu mua hàng
                                if (vtxuatkho.DonGia == null || vtxuatkho.DonGia == 0)
                                {
                                    if (vtmuahang.DonGia != null && vtmuahang.DonGia > 0)
                                    {
                                        vtxuatkho.DonGia = vtmuahang.DonGia;
                                    }
                                }

                                if (vtxuatkho.ThanhTien == null || vtxuatkho.ThanhTien == 0)
                                {
                                    // Tính thành tiền theo tỷ lệ số lượng xuất / số lượng mua
                                    if (vtmuahang.SL > 0 && vtmuahang.ThanhTien != null && vtmuahang.ThanhTien > 0 && vtxuatkho.SL > 0)
                                    {
                                        vtxuatkho.ThanhTien = (vtmuahang.ThanhTien.Value / vtmuahang.SL.Value) * vtxuatkho.SL.Value;
                                    }
                                    else if (vtxuatkho.DonGia != null && vtxuatkho.DonGia > 0 && vtxuatkho.SL > 0)
                                    {
                                        // Nếu thành tiền vẫn = 0, tính từ đơn giá * số lượng
                                        vtxuatkho.ThanhTien = vtxuatkho.DonGia * vtxuatkho.SL;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            var yeucau = _context.yeucau
                .FirstOrDefault(y => y.MaYeucau == phieuxuatkho.MaYeucau);

            // SL yêu cầu ban đầu theo từng mã vật tư (base code) để in đúng cột "YÊU CẦU"
            var slYeuCauByBaseCode = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(phieuxuatkho.MaYeucau))
            {
                var vtYcList = _context.vtyeucau
                    .Where(v => v.VTMaYeucau == phieuxuatkho.MaYeucau)
                    .ToList();

                slYeuCauByBaseCode = vtYcList
                    .GroupBy(v => YeucauUpdateHelper.GetBaseProductCode(v.MaSanpham ?? ""))
                    .ToDictionary(g => g.Key ?? "", g => g.Sum(x => x.SL ?? 0), StringComparer.OrdinalIgnoreCase);
            }

            ViewBag.Phieuxuatkho = phieuxuatkho;
            ViewBag.VTPhieuxuatkho = vtphieuxuatkho;
            ViewBag.Yeucau = yeucau;
            ViewBag.SLYeuCauByBaseCode = slYeuCauByBaseCode;

            return View();
        }

    }
}