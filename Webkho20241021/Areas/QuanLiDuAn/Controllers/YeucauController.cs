using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Webkho_20241021.Models;
using Webkho_20241021.Areas.QuanLiDuAn.Data;
using Webkho_20241021.Areas.TruongBPKho.Services;
using Webkho_20241021.Services;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using OfficeOpenXml;


namespace Webkho_20241021.Areas.QuanLiDuAn.Controllers
{
    [Area("QuanLiDuAn")]
    [Authorize(Roles = "Quản lí dự án")]
    public class YeucauController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;
        public YeucauController(ApplicationDbContext context, EmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }
        public IActionResult Yeucau(string search = "")
        {
            var userRole = HttpContext.Session.GetString("Chucvu");

            var Yeucaulist = _context.yeucau.ToList();

            var PhieuMuaHangList = _context.phieumuahang.ToList();

            foreach (var yeucau in Yeucaulist)
            {
                // Kiểm tra nếu là yêu cầu nhập kho (mã bắt đầu bằng "NHAPKHO_")
                bool isNhapKhoRequest = !string.IsNullOrEmpty(yeucau.MaYeucau) && 
                                        (yeucau.MaYeucau.StartsWith("NHAPKHO_DUAN_") || 
                                         yeucau.MaYeucau.StartsWith("NHAPKHO_CANHAN_"));
                
                // Kiểm tra và cập nhật trạng thái dựa trên vật tư trước
                var vatTus = _context.vtyeucau.Where(v => v.VTMaYeucau == yeucau.MaYeucau).ToList();
                
                // Với yêu cầu nhập kho, lấy vật tư từ vtphieunhapkho thay vì vtyeucau
                if (isNhapKhoRequest)
                {
                    var vtPhieuNhap = (from vtnk in _context.vtphieunhapkho
                                      join pnk in _context.phieunhapkho on vtnk.MaNhapkho equals pnk.MaNhapkho
                                      where pnk.MaYeucau == yeucau.MaYeucau
                                      select vtnk.TrangThai).ToList();
                    
                    // Nếu có vật tư trong phiếu nhập kho, kiểm tra trạng thái
                    if (vtPhieuNhap.Any())
                    {
                        // Kiểm tra xem còn vật tư nào đang chờ quản lý dự án duyệt không
                        var hasAwaitingQLDA = vtPhieuNhap.Any(v => IsAwaitingProjectManagerApproval(v));
                        
                        // Nếu không còn vật tư nào chờ QLDA duyệt và có ít nhất một vật tư đã được duyệt
                        if (!hasAwaitingQLDA)
                        {
                            var hasApproved = vtPhieuNhap.Any(v => IsPostProjectManagerStatus(v));
                            var hasRejected = vtPhieuNhap.Any(v => (v ?? string.Empty).Trim() == "Đã từ chối");
                            
                            // Chỉ cập nhật nếu trạng thái hiện tại không phải là các trạng thái bất biến
                            var immutableStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                            {
                                "Đang mua hàng",
                                "Đã xuất kho",
                                "Đã nhận hàng"
                            };
                            
                            if (!immutableStatuses.Contains((yeucau.TrangThai ?? string.Empty).Trim()))
                            {
                                if (hasApproved)
                                {
                                    yeucau.TrangThai = "Chờ giám đốc duyệt";
                                }
                                else if (hasRejected && !hasApproved)
                                {
                                    yeucau.TrangThai = "Đã từ chối";
                                }
                            }
                        }
                    }
                }
                // Nếu có vật tư, kiểm tra trạng thái
                else if (vatTus.Any())
                {
                    // Kiểm tra xem còn vật tư nào đang chờ quản lý dự án duyệt không
                    var hasAwaitingQLDA = vatTus.Any(v => IsAwaitingProjectManagerApproval(v.TrangThai));
                    
                    // Nếu không còn vật tư nào chờ QLDA duyệt và có ít nhất một vật tư đã được duyệt
                    if (!hasAwaitingQLDA)
                    {
                        var hasApproved = vatTus.Any(v => IsPostProjectManagerStatus(v.TrangThai));
                        var hasRejected = vatTus.Any(v => (v.TrangThai ?? string.Empty).Trim() == "Đã từ chối");
                        
                        // Chỉ cập nhật nếu trạng thái hiện tại không phải là các trạng thái bất biến
                        var immutableStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                        {
                            "Đang mua hàng",
                            "Đã xuất kho",
                            "Đã nhận hàng"
                        };
                        
                        if (!immutableStatuses.Contains((yeucau.TrangThai ?? string.Empty).Trim()))
                        {
                            if (hasApproved)
                            {
                                yeucau.TrangThai = "Chờ giám đốc duyệt";
                            }
                            else if (hasRejected && !hasApproved)
                            {
                                yeucau.TrangThai = "Đã từ chối";
                            }
                        }
                    }
                }
                
                // Sau đó mới kiểm tra trạng thái "Đang mua hàng" (chỉ nếu chưa phải "Chờ giám đốc duyệt")
                var phieus = PhieuMuaHangList.Where(p => p.MaYeucau == yeucau.MaYeucau).ToList();
                if (phieus.Any(p => p.TrangThai != "Đã nhận hàng") && 
                    !string.Equals(yeucau.TrangThai, "Chờ giám đốc duyệt", StringComparison.OrdinalIgnoreCase))
                {
                    yeucau.TrangThai = "Đang mua hàng";
                }
            }

            _context.SaveChanges();

            // Sắp xếp: nhóm các yêu cầu có cùng mã cơ bản (bỏ phần tên người) lại với nhau
            var SortedYeucaulist = Yeucaulist
                .OrderByDescending(y => IsAwaitingProjectManagerApproval(y.TrangThai))
                .ThenBy(y => YeucauUpdateHelper.GetBaseRequestCode(y.MaYeucau ?? "")) // Nhóm theo mã cơ bản
                .ThenByDescending(y => y.NgayYeucau) // Trong cùng nhóm, sắp xếp theo ngày giảm dần
                .ToList();

            // Áp dụng tìm kiếm nếu có
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchTerm = search.Trim().ToLower();
                SortedYeucaulist = SortedYeucaulist
                    .Where(y =>
                        (y.MaYeucau != null && y.MaYeucau.ToLower().Contains(searchTerm)) ||
                        (y.TenYeucau != null && y.TenYeucau.ToLower().Contains(searchTerm)) ||
                        (y.NguoiYeucau != null && y.NguoiYeucau.ToLower().Contains(searchTerm)) ||
                        (y.Bophan != null && y.Bophan.ToLower().Contains(searchTerm)) ||
                        (y.YCMaNguoidung != null && y.YCMaNguoidung.ToLower().Contains(searchTerm)) ||
                        (y.YCMaDuan != null && y.YCMaDuan.ToLower().Contains(searchTerm)) ||
                        (y.TrangThai != null && y.TrangThai.ToLower().Contains(searchTerm))
                    )
                    .ToList();
            }

            var VTyeucaulist = _context.vtyeucau.ToList();
            var Duans = _context.duans.ToList();

            var model = new Yeucauviewmodel
            {
                Yeucau = SortedYeucaulist,
                VTyeucau = VTyeucaulist,
                Duans = Duans
            };

            ViewBag.Search = search;
            return View(model);
        }



        public IActionResult Phieuxuatkho(string search = "")
        {
            var Phieuxuatkholist = _context.phieuxuatkho
            .OrderByDescending(y => y.TrangThai == "Chờ lấy hàng")
            .ThenByDescending(y => y.TrangThai == "Đang chuẩn bị hàng")
            .ThenByDescending(y => y.NgayXuatkho)
            .ToList();

            // Áp dụng tìm kiếm nếu có
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchTerm = search.Trim().ToLower();
                Phieuxuatkholist = Phieuxuatkholist
                    .Where(p =>
                        (p.MaXuatkho != null && p.MaXuatkho.ToLower().Contains(searchTerm)) ||
                        (p.MaYeucau != null && p.MaYeucau.ToLower().Contains(searchTerm)) ||
                        (p.MaDuan != null && p.MaDuan.ToLower().Contains(searchTerm)) ||
                        (p.MaNguoidung != null && p.MaNguoidung.ToLower().Contains(searchTerm)) ||
                        (p.TrangThai != null && p.TrangThai.ToLower().Contains(searchTerm)) ||
                        (p.GhiChu != null && p.GhiChu.ToLower().Contains(searchTerm))
                    )
                    .ToList();
            }

            var VTphieuxuatkholist = _context.vtphieuxuatkho.ToList();
            var model = new Phieuxuatkhoviewmodel
            {
                Phieuxuatkho = Phieuxuatkholist,
                VTphieuxuatkho = VTphieuxuatkholist,
            };
            ViewBag.Search = search;
            return View(model);
        }

        public IActionResult Phieunhapkho(string search = "")
        {
            var maNv = HttpContext.Session.GetString("MaNguoidung");
            var chucVu = HttpContext.Session.GetString("Chucvu");
            var boPhan = HttpContext.Session.GetString("Bophan");
            var Maduanquanli = _context.duans
                .Where(d => d.MaNguoiQLDA == maNv)
                .Select(d => d.MaDuan)
                .ToList();
            
            // Sắp xếp: đưa các phiếu có trạng thái "Chờ quản lý dự án duyệt" lên đầu
            // Ưu tiên: 1) Phiếu thuộc dự án QLDA quản lý, 2) Phiếu có trạng thái "Chờ quản lý dự án duyệt" (cho TrưởngBP kho)
            var Phieunhapkholist = _context.phieunhapkho
                .OrderByDescending(y => y.TrangThai == "Chờ quản lý dự án duyệt" && !string.IsNullOrEmpty(y.MaDuan) && Maduanquanli.Contains(y.MaDuan))
                .ThenByDescending(y => y.TrangThai == "Chờ quản lý dự án duyệt" && chucVu == "Trưởng BP" && boPhan == "BP kho")
                .ThenByDescending(y => y.TrangThai == "Chờ quản lý dự án duyệt")
                .ThenByDescending(y => y.NgayNhapkho)
                .ToList();

            // Áp dụng tìm kiếm nếu có
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchTerm = search.Trim().ToLower();
                Phieunhapkholist = Phieunhapkholist
                    .Where(p =>
                        (p.MaNhapkho != null && p.MaNhapkho.ToLower().Contains(searchTerm)) ||
                        (p.MaYeucau != null && p.MaYeucau.ToLower().Contains(searchTerm)) ||
                        (p.MaDuan != null && p.MaDuan.ToLower().Contains(searchTerm)) ||
                        (p.MaNguoidung != null && p.MaNguoidung.ToLower().Contains(searchTerm)) ||
                        (p.TrangThai != null && p.TrangThai.ToLower().Contains(searchTerm))
                    )
                    .ToList();
            }

            var VTphieunhapkholist = _context.vtphieunhapkho.ToList();
            var Duanslist = _context.duans.ToList();
            var model = new Phieunhapkhoviewmodel
            {
                Phieunhapkho = Phieunhapkholist,
                VTphieunhapkho = VTphieunhapkholist,
                Duans = Duanslist
            };
            ViewBag.Search = search;
            return View(model);
        }

        public IActionResult Phieumuahang(string search = "")
        {
            var Phieumuahanglist = _context.phieumuahang
            .OrderByDescending(y => y.TrangThai == "Đang chờ báo giá")
            .ThenByDescending(y => y.NgayMuahang)
            .ToList();
            // Gán tên Người yêu cầu cho từng phiếu mua hàng
            var nguoiDungDict = _context.nguoidungs.ToDictionary(n => n.MaNguoidung, n => n.TenNguoidung);
            foreach (var phieu in Phieumuahanglist)
            {
                if (!string.IsNullOrEmpty(phieu.MaNguoidung) && nguoiDungDict.TryGetValue(phieu.MaNguoidung, out var ten))
                {
                    phieu.TenNguoiyeucau = ten;
                }
            }

            // Áp dụng tìm kiếm nếu có
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchTerm = search.Trim().ToLower();
                Phieumuahanglist = Phieumuahanglist
                    .Where(p =>
                        (p.MaMuahang != null && p.MaMuahang.ToLower().Contains(searchTerm)) ||
                        (p.MaYeucau != null && p.MaYeucau.ToLower().Contains(searchTerm)) ||
                        (p.MaDuan != null && p.MaDuan.ToLower().Contains(searchTerm)) ||
                        (p.MaNguoidung != null && p.MaNguoidung.ToLower().Contains(searchTerm)) ||
                        (p.TenNguoiyeucau != null && p.TenNguoiyeucau.ToLower().Contains(searchTerm)) ||
                        (p.TrangThai != null && p.TrangThai.ToLower().Contains(searchTerm)) ||
                        (p.GhiChu != null && p.GhiChu.ToLower().Contains(searchTerm))
                    )
                    .ToList();
            }

            var VTphieumuahanglist = _context.vtphieumuahang.ToList();
            var model = new Phieumuahangviewmodel
            {
                Phieumuahang = Phieumuahanglist,
                VTphieumuahang = VTphieumuahanglist,
            };
            ViewBag.Search = search;
            return View(model);
        }

        [HttpGet]
        public IActionResult GetVTYeucau(string MaYeucau)
        {
            // Kiểm tra nếu là yêu cầu nhập kho (mã bắt đầu bằng "NHAPKHO_")
            if (!string.IsNullOrEmpty(MaYeucau) && MaYeucau.StartsWith("NHAPKHO_"))
            {
                // Lấy dữ liệu từ vtphieunhapkho thông qua phieunhapkho
                var vatTuList = (from vtnk in _context.vtphieunhapkho
                                join pnk in _context.phieunhapkho on vtnk.MaNhapkho equals pnk.MaNhapkho
                                where pnk.MaYeucau == MaYeucau
                                select new
                                {
                                    id = vtnk.ID,
                                    vtMaYeucau = MaYeucau,
                                    tenSanpham = vtnk.TenSanpham,
                                    maSanpham = vtnk.MaSanpham,
                                    hangSX = vtnk.HangSX,
                                    nhaCC = vtnk.NhaCC,
                                    slCu = (int?)null,
                                    slMoi = vtnk.SL,
                                    sl = vtnk.SL,
                                    donVi = vtnk.DonVi,
                                    ngayCanHang = (DateTime?)null,
                                    ngayNhapkho = vtnk.NgayNhapkho,
                                    ngayBaohanh = vtnk.NgayBaohanh,
                                    thoiGianBH = vtnk.ThoiGianBH,
                                    ngayDuyet = (DateTime?)null,
                                    trangThai = vtnk.TrangThai,
                                    ghiChu = (string?)null
                                }).ToList();
                return Json(vatTuList);
            }
            else
            {
                // Lấy dữ liệu từ vtyeucau và chuẩn hóa field name camelCase để JS đọc thống nhất
                var vatTuList = _context.vtyeucau
                                     .Where(v => v.VTMaYeucau == MaYeucau)
                                     .Select(v => new
                                     {
                                         id = v.ID,
                                         vtMaYeucau = v.VTMaYeucau,
                                         tenSanpham = v.TenSanpham,
                                         maSanpham = v.MaSanpham,
                                         hangSX = v.HangSX,
                                         nhaCC = v.NhaCC,
                                         slCu = v.SLCu,
                                         slMoi = v.SLMoi,
                                         sl = v.SL,
                                         donVi = v.DonVi,
                                         ngayCanHang = v.NgayCanHang,
                                         ngayNhapkho = v.NgayNhapkho,
                                         ngayBaohanh = v.NgayBaohanh,
                                         thoiGianBH = v.ThoiGianBH,
                                         ngayDuyet = v.NgayDuyet,
                                         trangThai = v.TrangThai,
                                         ghiChu = v.GhiChu
                                     })
                                     .ToList();
                return Json(vatTuList);
            }
        }

        [HttpGet]
        public IActionResult GetVTPhieunhapkho(string MaNhapkho)
        {
            var PhieunhapkhoList = _context.vtphieunhapkho
                                 .Where(v => v.MaNhapkho == MaNhapkho).ToList();
            
            // Lấy thông tin phiếu nhập kho để lấy tên người yêu cầu và MaYeucau
            var phieunhapkho = _context.phieunhapkho
                .FirstOrDefault(p => p.MaNhapkho == MaNhapkho);
            
            string tenNguoiYeuCau = "";
            string maYeucau = "";
            if (phieunhapkho != null)
            {
                maYeucau = phieunhapkho.MaYeucau ?? "";
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
                tenNguoiYeuCau = tenNguoiYeuCau,
                maYeucau = maYeucau,
                trangThaiPhieu = phieunhapkho?.TrangThai ?? ""
            });
        }

        [HttpPost]
        public IActionResult XuLyVatTuYeucau(string MaYeucau, string MaSanpham, string action, string? GhiChu = null)
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
                    // Khi quản lí dự án duyệt, đặt trạng thái "Chờ giám đốc duyệt"
                    vatTu.TrangThai = "Chờ giám đốc duyệt";
                    vatTu.GhiChu = null; // Xóa ghi chú khi duyệt
                }
                else if (action == "reject")
                {
                    vatTu.TrangThai = "Đã từ chối";
                    vatTu.GhiChu = GhiChu; // Lưu ghi chú khi từ chối
                }

                _context.vtyeucau.Update(vatTu);
                _context.SaveChanges();

                UpdateYeucauStatusAfterVatTuChange(MaYeucau);

                return Json(new { success = true, message = action == "approve" ? "Đã duyệt vật tư thành công." : "Đã từ chối vật tư.", ghiChu = vatTu.GhiChu });
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
                // Lấy danh sách vật tư có trạng thái "Chờ quản lý dự án duyệt" hoặc trống (tương thích ngược)
                var vatTuList = _context.vtyeucau
                    .Where(v => v.VTMaYeucau == MaYeucau && 
                           (string.IsNullOrWhiteSpace(v.TrangThai) || 
                            v.TrangThai.Trim().Equals("Chờ quản lý dự án duyệt", StringComparison.OrdinalIgnoreCase) ||
                            v.TrangThai.Trim().StartsWith("Chờ quản lý dự án", StringComparison.OrdinalIgnoreCase) ||
                            v.TrangThai.Trim().Contains("chờ quản lý dự án", StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                int processedCount = 0;
                
                foreach (var vatTu in vatTuList)
                {
                    if (action == "approve")
                    {
                        // Khi quản lí dự án duyệt, đặt trạng thái "Chờ giám đốc duyệt"
                        vatTu.TrangThai = "Chờ giám đốc duyệt";
                        _context.vtyeucau.Update(vatTu);
                        processedCount++;
                    }
                    else if (action == "reject")
                    {
                        vatTu.TrangThai = "Đã từ chối";
                        // Ghi chú sẽ được lưu từ từng vật tư riêng lẻ, không có trong bulk action
                        _context.vtyeucau.Update(vatTu);
                        processedCount++;
                    }
                }

                // Lưu thay đổi trạng thái vật tư
                if (processedCount > 0)
                {
                    _context.SaveChanges();
                }

                var updated = UpdateYeucauStatusAfterVatTuChange(MaYeucau);

                // Gửi thông báo khi QLDA duyệt
                if (action == "approve" && processedCount > 0)
                {
                    var yeucauForNotif = _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
                    if (yeucauForNotif != null)
                    {
                        _ = Task.Run(async () =>
                        {
                            // Thông báo cho nhân viên
                            await _emailService.SendNotificationToEmployeeAsync(
                                MaYeucau,
                                yeucauForNotif.NguoiYeucau ?? "",
                                "Đã được quản lý dự án duyệt"
                            );

                            // Thông báo cho giám đốc
                            await _emailService.SendNotificationToDirectorAsync(MaYeucau);
                        });
                    }
                }

                if (processedCount == 0)
                {
                    var note = updated
                        ? "Không có vật tư nào đang chờ quản lý dự án duyệt. Đã đồng bộ trạng thái yêu cầu."
                        : "Không có vật tư nào đang chờ quản lý dự án duyệt.";
                    return Json(new { success = true, message = note });
                }

                return Json(new
                {
                    success = true,
                    message = action == "approve"
                        ? $"Đã duyệt {processedCount} vật tư thành công."
                        : $"Đã từ chối {processedCount} vật tư.",
                    updated
                });
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

                // Kiểm tra xem có phải là yêu cầu nhập kho không
                bool isNhapKhoRequest = !string.IsNullOrEmpty(MaYeucau) && 
                    (MaYeucau.StartsWith("NHAPKHO_DUAN_", StringComparison.OrdinalIgnoreCase) ||
                     MaYeucau.StartsWith("NHAPKHO_CANHAN_", StringComparison.OrdinalIgnoreCase));

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

                    // Nếu là yêu cầu nhập kho, tìm vật tư trong vtphieunhapkho
                    // Nếu không, tìm trong vtyeucau
                    vtyeucau? vatTu = null;
                    vtphieunhapkho? vtPhieuNhap = null;
                    
                    if (isNhapKhoRequest)
                    {
                        // Tìm vật tư trong vtphieunhapkho thông qua phieunhapkho
                        vtPhieuNhap = (from vtnk in _context.vtphieunhapkho
                                      join pnk in _context.phieunhapkho on vtnk.MaNhapkho equals pnk.MaNhapkho
                                      where pnk.MaYeucau == MaYeucau && vtnk.MaSanpham == maSanpham
                                      select vtnk).FirstOrDefault();
                        
                        if (vtPhieuNhap == null)
                        {
                            skippedCount++;
                            continue;
                        }
                        _context.Entry(vtPhieuNhap).Reload();
                    }
                    else
                    {
                        // Reload vật tư từ database để đảm bảo có trạng thái mới nhất
                        vatTu = _context.vtyeucau
                            .FirstOrDefault(v => v.VTMaYeucau == MaYeucau && v.MaSanpham == maSanpham);

                        if (vatTu == null)
                        {
                            skippedCount++;
                            continue;
                        }

                        // Reload để đảm bảo có trạng thái mới nhất
                        _context.Entry(vatTu).Reload();
                    }

                    // Helper function để kiểm tra xem vật tư có đang chờ duyệt không
                    // Với yêu cầu nhập kho: chấp nhận "Chờ quản lý dự án duyệt"
                    // Với yêu cầu xuất kho: chấp nhận "Chờ quản lý dự án duyệt"
                    Func<string, bool> isAwaitingApprovalStatus = status =>
                    {
                        if (string.IsNullOrWhiteSpace(status))
                        {
                            return true; // Trạng thái null/empty được coi là chờ duyệt
                        }
                        var normalized = status.Trim();
                        // Chấp nhận cả "Chờ quản lý dự án duyệt" và "Chờ giám đốc duyệt"
                        return normalized.Equals("Chờ quản lý dự án duyệt", StringComparison.OrdinalIgnoreCase)
                            || normalized.Equals("Chờ giám đốc duyệt", StringComparison.OrdinalIgnoreCase)
                            || normalized.Equals("Chờ Giám đốc duyệt", StringComparison.OrdinalIgnoreCase)
                            || normalized.StartsWith("Chờ quản lý dự án", StringComparison.OrdinalIgnoreCase)
                            || normalized.StartsWith("Chờ giám đốc", StringComparison.OrdinalIgnoreCase)
                            || normalized.StartsWith("Chờ Giám đốc", StringComparison.OrdinalIgnoreCase)
                            || normalized.Contains("chờ quản lý dự án", StringComparison.OrdinalIgnoreCase)
                            || normalized.Contains("chờ giám đốc", StringComparison.OrdinalIgnoreCase)
                            || normalized.Contains("Chờ Giám đốc", StringComparison.OrdinalIgnoreCase);
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
                               normalized == "Chờ xuất kho" ||
                               normalized == "Đã xuất kho" ||
                               normalized == "Đã nhận hàng" ||
                               normalized == "Chờ nhập kho" ||
                               normalized == "Đã nhập kho";
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

                    // Chỉ xử lý các vật tư đang chờ duyệt (Chờ quản lý dự án duyệt hoặc Chờ giám đốc duyệt) và chưa được duyệt/từ chối
                    var currentStatus = isNhapKhoRequest ? (vtPhieuNhap?.TrangThai ?? "") : (vatTu?.TrangThai ?? "");
                    bool isAwaiting = isAwaitingApprovalStatus(currentStatus);
                    bool isAlreadyApprovedStatus = isAlreadyApproved(currentStatus);
                    bool isRejected = isAlreadyRejected(currentStatus);
                    bool canProcess = isAwaiting && !isAlreadyApprovedStatus && !isRejected;
                    
                    if (!canProcess)
                    {
                        skippedCount++;
                        continue;
                    }

                    if (isApproved)
                    {
                        // Duyệt vật tư
                        if (isNhapKhoRequest)
                        {
                            // Với yêu cầu nhập kho, khi QuanLiDuAn duyệt thì chuyển thành "Chờ giám đốc duyệt"
                            vtPhieuNhap.TrangThai = "Chờ giám đốc duyệt";
                            _context.vtphieunhapkho.Update(vtPhieuNhap);
                        }
                        else
                        {
                            // Với yêu cầu xuất kho, cập nhật trạng thái trong vtyeucau
                            vatTu.NgayDuyet = DateTime.Now;
                            vatTu.TrangThai = "Chờ giám đốc duyệt";
                            vatTu.GhiChu = null; // Xóa ghi chú khi duyệt
                            _context.vtyeucau.Update(vatTu);
                        }
                    }
                    else
                    {
                        // Từ chối vật tư
                        if (isNhapKhoRequest)
                        {
                            // Với yêu cầu nhập kho, cập nhật trạng thái trong vtphieunhapkho
                            vtPhieuNhap.TrangThai = "Đã từ chối";
                            _context.vtphieunhapkho.Update(vtPhieuNhap);
                        }
                        else
                        {
                            // Với yêu cầu xuất kho, cập nhật trạng thái trong vtyeucau
                            vatTu.NgayDuyet = DateTime.Now;
                            vatTu.TrangThai = "Đã từ chối";
                            vatTu.GhiChu = ghiChu; // Lưu ghi chú khi từ chối
                            _context.vtyeucau.Update(vatTu);
                        }
                    }

                    processedCount++;
                }

                _context.SaveChanges();

                // Cập nhật trạng thái yêu cầu (chỉ với yêu cầu xuất kho, không áp dụng cho yêu cầu nhập kho)
                if (!isNhapKhoRequest)
                {
                    UpdateYeucauStatusAfterVatTuChange(MaYeucau);
                }
                else
                {
                    // Với yêu cầu nhập kho, cập nhật trạng thái phieunhapkho và yeucau
                    var phieunhapkho = _context.phieunhapkho.FirstOrDefault(p => p.MaYeucau == MaYeucau);
                    if (phieunhapkho != null)
                    {
                        var vtList = _context.vtphieunhapkho.Where(vt => vt.MaNhapkho == phieunhapkho.MaNhapkho).ToList();
                        // Kiểm tra xem tất cả vật tư đã được duyệt (chuyển thành "Chờ giám đốc duyệt") chưa
                        bool allApproved = vtList.All(vt => vt.TrangThai == "Chờ giám đốc duyệt" || vt.TrangThai == "Chờ Giám đốc duyệt");
                        bool hasRejected = vtList.Any(vt => vt.TrangThai == "Đã từ chối");
                        bool hasPending = vtList.Any(vt => vt.TrangThai == "Chờ quản lý dự án duyệt" || string.IsNullOrWhiteSpace(vt.TrangThai));
                        
                        if (allApproved && vtList.Any() && !hasPending)
                        {
                            // Tất cả vật tư đã được duyệt -> chuyển trạng thái phiếu và yêu cầu thành "Chờ giám đốc duyệt"
                            phieunhapkho.TrangThai = "Chờ giám đốc duyệt";
                            var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
                            if (yeucau != null)
                            {
                                yeucau.TrangThai = "Chờ giám đốc duyệt";
                                _context.yeucau.Update(yeucau);
                            }
                        }
                        else if (hasRejected)
                        {
                            phieunhapkho.TrangThai = "Đã từ chối";
                            var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
                            if (yeucau != null)
                            {
                                yeucau.TrangThai = "Đã từ chối";
                                _context.yeucau.Update(yeucau);
                            }
                        }
                        else if (hasPending)
                        {
                            // Vẫn còn vật tư chờ duyệt -> giữ trạng thái "Chờ quản lý dự án duyệt"
                            phieunhapkho.TrangThai = "Chờ quản lý dự án duyệt";
                            var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
                            if (yeucau != null)
                            {
                                yeucau.TrangThai = "Chờ quản lý dự án duyệt";
                                _context.yeucau.Update(yeucau);
                            }
                        }
                        _context.phieunhapkho.Update(phieunhapkho);
                        _context.SaveChanges();
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

        private static bool IsAwaitingProjectManagerApproval(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return false;
            }

            var normalized = status.Trim();
            return normalized.Equals("Chờ quản lý dự án duyệt", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("Chờ quản lý dự án", StringComparison.OrdinalIgnoreCase)
                || normalized.Contains("chờ quản lý dự án", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsPostProjectManagerStatus(string? status)
        {
            if (string.IsNullOrWhiteSpace(status))
            {
                return false;
            }

            var normalized = status.Trim();
            return normalized == "Chờ giám đốc duyệt"
                || normalized == "Đã duyệt"
                || normalized == "Đang mua hàng"
                || normalized == "Đã xuất kho"
                || normalized == "Đã nhận hàng";
        }

        [HttpPost]
        public IActionResult CapNhatTrangThaiYeucau(string MaYeucau)
        {
            try
            {
                var updated = UpdateYeucauStatusAfterVatTuChange(MaYeucau);
                return Json(new
                {
                    success = true,
                    updated,
                    message = updated ? "Đã cập nhật trạng thái yêu cầu." : "Không có thay đổi trạng thái."
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        private bool UpdateYeucauStatusAfterVatTuChange(string maYeucau)
        {
            var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == maYeucau);
            if (yeucau == null)
            {
                return false;
            }

            // Không can thiệp nếu yêu cầu đã ở trạng thái sau khi giám đốc xử lý
            var immutableStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Đang mua hàng",
                "Đã xuất kho",
                "Đã nhận hàng"
            };

            if (immutableStatuses.Contains((yeucau.TrangThai ?? string.Empty).Trim()))
            {
                return false;
            }

            var vatTus = _context.vtyeucau.Where(v => v.VTMaYeucau == maYeucau).ToList();
            if (!vatTus.Any())
            {
                return false;
            }

            if (vatTus.Any(v => IsAwaitingProjectManagerApproval(v.TrangThai)))
            {
                return false;
            }

            var hasApproved = vatTus.Any(v => IsPostProjectManagerStatus(v.TrangThai));
            var hasRejected = vatTus.Any(v => (v.TrangThai ?? string.Empty).Trim() == "Đã từ chối");

            string? newStatus = null;
            if (hasApproved)
            {
                newStatus = "Chờ giám đốc duyệt";
            }
            else if (hasRejected)
            {
                newStatus = "Đã từ chối";
            }

            if (!string.IsNullOrEmpty(newStatus) && !string.Equals(yeucau.TrangThai, newStatus, StringComparison.Ordinal))
            {
                yeucau.TrangThai = newStatus;
                _context.yeucau.Update(yeucau);
                _context.SaveChanges();
                return true;
            }

            return false;
        }

        public IActionResult DanhSachFileExcel(string? q, string? maDuan, int page = 1, int pageSize = 10)
        {
            if (page < 1)
            {
                page = 1;
            }
            if (pageSize <= 0)
            {
                pageSize = 10;
            }

            var query = _context.ExcelFiles
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var keyword = q.Trim();
                query = query.Where(f =>
                    (f.TenFile != null && f.TenFile.Contains(keyword)) ||
                    (f.MaYeucau != null && f.MaYeucau.Contains(keyword)));
            }

            if (!string.IsNullOrWhiteSpace(maDuan))
            {
                query = query.Where(f => f.MaDuan == maDuan);
            }

            var totalItems = query.Count();
            var totalPages = totalItems == 0 ? 1 : (int)Math.Ceiling(totalItems / (double)pageSize);
            if (page > totalPages)
            {
                page = totalPages;
            }

            var excelFiles = query
                .OrderByDescending(f => f.NgayUpload ?? DateTime.MinValue)
                .ThenByDescending(f => f.ID)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var duanList = _context.duans.OrderBy(d => d.MaDuan).ToList();

            var yeucauIds = excelFiles
                .Where(f => !string.IsNullOrEmpty(f.MaYeucau))
                .Select(f => f.MaYeucau!)
                .Distinct()
                .ToList();

            var yeucauList = yeucauIds.Any()
                ? _context.yeucau
                    .Where(y => yeucauIds.Contains(y.MaYeucau))
                    .AsNoTracking()
                    .ToList()
                : new List<yeucau>();

            var yeucauLookup = yeucauList.ToDictionary(y => y.MaYeucau, y => y);
            foreach (var file in excelFiles)
            {
                if (string.IsNullOrEmpty(file.MaDuan) &&
                    !string.IsNullOrEmpty(file.MaYeucau) &&
                    yeucauLookup.TryGetValue(file.MaYeucau, out var yc) &&
                    !string.IsNullOrEmpty(yc.YCMaDuan))
                {
                    file.MaDuan = yc.YCMaDuan;
                }
            }

            ViewBag.Duans = duanList;
            ViewBag.DuanList = duanList;
            ViewBag.YeucauList = yeucauList;
            ViewBag.Q = q;
            ViewBag.MaDuan = maDuan;
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;

            return View(excelFiles);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult XoaFileExcel(int id)
        {
            var excelFile = _context.ExcelFiles.FirstOrDefault(f => f.ID == id);
            if (excelFile == null)
            {
                TempData["Error"] = "File Excel không tồn tại hoặc đã được xóa.";
                return RedirectToAction(nameof(DanhSachFileExcel));
            }

            try
            {
                if (!string.IsNullOrEmpty(excelFile.DuongDanFile))
                {
                    var relativePath = excelFile.DuongDanFile
                        .Replace("~", string.Empty)
                        .TrimStart('/', '\\');
                    var physicalPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath);

                    if (System.IO.File.Exists(physicalPath))
                    {
                        System.IO.File.Delete(physicalPath);
                    }
                }

                _context.ExcelFiles.Remove(excelFile);
                _context.SaveChanges();

                TempData["Success"] = $"Đã xóa file \"{excelFile.TenFile ?? excelFile.ID.ToString()}\".";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Không thể xóa file. Chi tiết: {ex.Message}";
            }

            return RedirectToAction(nameof(DanhSachFileExcel));
        }

        public IActionResult XemFileExcel(int id)
        {
            var excelFile = _context.ExcelFiles.FirstOrDefault(f => f.ID == id);
            if (excelFile == null)
            {
                return NotFound();
            }

            yeucau? yeucau = null;
            List<vtyeucau> vtList = new();

            if (!string.IsNullOrEmpty(excelFile.MaYeucau))
            {
                yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == excelFile.MaYeucau);
                vtList = _context.vtyeucau
                    .Where(v => v.VTMaYeucau == excelFile.MaYeucau)
                    .ToList();
            }

            var maDuan = !string.IsNullOrEmpty(excelFile.MaDuan)
                ? excelFile.MaDuan
                : yeucau?.YCMaDuan;

            var duan = !string.IsNullOrEmpty(maDuan)
                ? _context.duans.FirstOrDefault(d => d.MaDuan == maDuan)
                : null;

            ViewBag.Yeucau = yeucau;
            ViewBag.Duan = duan;
            ViewBag.VTYeucau = vtList;

            return View(excelFile);
        }

        public IActionResult SoSanhVatTuTheoNgay(DateTime? ngay1, DateTime? ngay2, string? maDuan, string? searchMaVT, string? searchTenVT, DateTime? searchNgay, string? searchHangSX, int page = 1, int pageSize = 20)
        {
            if (page < 1)
            {
                page = 1;
            }
            if (pageSize <= 0)
            {
                pageSize = 20;
            }

            var endDate = (ngay2 ?? DateTime.Today).Date;
            var startDate = (ngay1 ?? endDate.AddDays(-7)).Date;

            if (startDate > endDate)
            {
                (startDate, endDate) = (endDate, startDate);
            }

            var endDateInclusive = endDate.AddDays(1).AddTicks(-1);

            var excelFilesQuery = _context.ExcelFiles
                .Where(f => f.NgayUpload.HasValue &&
                            f.NgayUpload.Value >= startDate &&
                            f.NgayUpload.Value <= endDateInclusive);

            if (!string.IsNullOrWhiteSpace(maDuan))
            {
                excelFilesQuery = excelFilesQuery.Where(f => f.MaDuan == maDuan);
            }

            var excelFiles = excelFilesQuery
                .OrderBy(f => f.NgayUpload)
                .ToList();

            ViewBag.ExcelFiles = excelFiles;
            ViewBag.Duans = _context.duans.OrderBy(d => d.MaDuan).ToList();
            ViewBag.Ngay1 = startDate.ToString("yyyy-MM-dd");
            ViewBag.Ngay2 = endDate.ToString("yyyy-MM-dd");
            ViewBag.MaDuan = maDuan;

            if (!excelFiles.Any())
            {
                ViewBag.Page = 1;
                ViewBag.TotalPages = 1;
                return View(new List<dynamic>());
            }

            var yeucauIds = excelFiles
                .Where(f => !string.IsNullOrEmpty(f.MaYeucau))
                .Select(f => f.MaYeucau!)
                .Distinct()
                .ToList();

            var vtList = yeucauIds.Any()
                ? _context.vtyeucau
                    .Where(v => v.VTMaYeucau != null && yeucauIds.Contains(v.VTMaYeucau))
                    .ToList()
                : new List<vtyeucau>();

            var joinedData = vtList
                .Join(excelFiles,
                      vt => vt.VTMaYeucau,
                      file => file.MaYeucau,
                      (vt, file) => new { vt, file })
                .Where(x => x.file.NgayUpload.HasValue)
                .ToList();

            var summary = joinedData
                .GroupBy(g => new
                {
                    NgayUpload = g.file.NgayUpload!.Value.Date,
                    MaSanpham = g.vt.MaSanpham ?? "-",
                    TenSanpham = g.vt.TenSanpham ?? "-",
                    HangSX = g.vt.HangSX ?? "-",
                    DonVi = g.vt.DonVi ?? "-"
                })
                .Select(g => new
                {
                    g.Key.NgayUpload,
                    g.Key.MaSanpham,
                    g.Key.TenSanpham,
                    g.Key.HangSX,
                    g.Key.DonVi,
                    TongSL = g.Sum(x => x.vt.SL ?? 0),
                    SoLuongFile = g.Select(x => x.file.ID).Distinct().Count()
                })
                .ToList();

            // Áp dụng tìm kiếm
            var summaryQuery = summary.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchMaVT))
            {
                var keyword = searchMaVT.Trim().ToLower();
                summaryQuery = summaryQuery.Where(s => s.MaSanpham != null && s.MaSanpham.ToLower().Contains(keyword));
            }

            if (!string.IsNullOrWhiteSpace(searchTenVT))
            {
                var keyword = searchTenVT.Trim().ToLower();
                summaryQuery = summaryQuery.Where(s => s.TenSanpham != null && s.TenSanpham.ToLower().Contains(keyword));
            }

            if (searchNgay.HasValue)
            {
                var searchDate = searchNgay.Value.Date;
                summaryQuery = summaryQuery.Where(s => s.NgayUpload.Date == searchDate);
            }

            if (!string.IsNullOrWhiteSpace(searchHangSX))
            {
                var keyword = searchHangSX.Trim().ToLower();
                summaryQuery = summaryQuery.Where(s => s.HangSX != null && s.HangSX.ToLower().Contains(keyword));
            }

            var summaryList = summaryQuery
                .OrderBy(g => g.NgayUpload)
                .ThenBy(g => g.MaSanpham)
                .Cast<dynamic>()
                .ToList();

            var totalItems = summaryList.Count;
            var totalPages = totalItems == 0 ? 1 : (int)Math.Ceiling(totalItems / (double)pageSize);
            if (page > totalPages)
            {
                page = totalPages;
            }

            var pagedSummary = summaryList
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalItems = totalItems;
            ViewBag.SearchMaVT = searchMaVT;
            ViewBag.SearchTenVT = searchTenVT;
            ViewBag.SearchNgay = searchNgay?.ToString("yyyy-MM-dd");
            ViewBag.SearchHangSX = searchHangSX;

            return View(pagedSummary);
        }

        public IActionResult SoSanhNhieuFile(string ids)
        {
            if (string.IsNullOrWhiteSpace(ids))
            {
                return RedirectToAction(nameof(DanhSachFileExcel));
            }

            var idList = ids
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(id => int.TryParse(id, out var parsed) ? parsed : (int?)null)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            if (!idList.Any())
            {
                return RedirectToAction(nameof(DanhSachFileExcel));
            }

            var excelFiles = _context.ExcelFiles
                .AsNoTracking()
                .Where(f => idList.Contains(f.ID))
                .OrderBy(f => f.NgayUpload ?? DateTime.MinValue)
                .ThenBy(f => f.ID)
                .ToList();

            if (!excelFiles.Any())
            {
                return RedirectToAction(nameof(DanhSachFileExcel));
            }

            var filesByYeucau = excelFiles
                .Where(f => !string.IsNullOrEmpty(f.MaYeucau))
                .GroupBy(f => f.MaYeucau!)
                .ToDictionary(g => g.Key, g => g.First());

            var yeucauIds = filesByYeucau.Keys.ToList();
            var vatTuList = yeucauIds.Any()
                ? _context.vtyeucau
                    .Where(v => v.VTMaYeucau != null && yeucauIds.Contains(v.VTMaYeucau))
                    .AsNoTracking()
                    .ToList()
                : new List<vtyeucau>();

            var comparisonDict = new Dictionary<string, VatTuComparisonViewModel>();
            var firstFileId = excelFiles.First().ID;
            var lastFileId = excelFiles.Last().ID;

            foreach (var vt in vatTuList)
            {
                if (vt.VTMaYeucau == null || !filesByYeucau.TryGetValue(vt.VTMaYeucau, out var file))
                {
                    continue;
                }

                var key = $"{vt.MaSanpham}|{vt.TenSanpham}|{vt.HangSX}|{vt.DonVi}";
                if (!comparisonDict.TryGetValue(key, out var comparison))
                {
                    comparison = new VatTuComparisonViewModel
                    {
                        MaSanpham = vt.MaSanpham ?? "",
                        TenSanpham = vt.TenSanpham ?? "",
                        HangSX = vt.HangSX ?? "",
                        DonVi = vt.DonVi ?? ""
                    };
                    comparisonDict[key] = comparison;
                }

                comparison.ChiTiet.Add(new FileVatTuDetail
                {
                    FileID = file.ID,
                    TenFile = file.TenFile ?? "",
                    NgayUpload = file.NgayUpload ?? DateTime.MinValue,
                    MaYeucau = vt.VTMaYeucau,
                    SL = vt.SL ?? 0
                });
                comparison.TongSL += vt.SL ?? 0;
            }

            var vatTuComparison = comparisonDict.Values
                .OrderBy(v => v.MaSanpham)
                .ThenBy(v => v.TenSanpham)
                .ToList();

            // Lấy danh sách các mã yêu cầu từ các file excel
            var maYeucauList = excelFiles
                .Where(f => !string.IsNullOrEmpty(f.MaYeucau))
                .Select(f => f.MaYeucau!)
                .Distinct()
                .ToList();

            // Lấy tất cả các phiếu xuất kho liên quan đến các mã yêu cầu này
            var phieuXuatKhoList = maYeucauList.Any()
                ? _context.phieuxuatkho
                    .Where(px => maYeucauList.Contains(px.MaYeucau))
                    .AsNoTracking()
                    .ToList()
                : new List<phieuxuatkho>();

            // Lấy danh sách các phiếu xuất kho đã được xác nhận nhận hàng hoặc hoàn thành
            var phieuXuatKhoDaXacNhan = phieuXuatKhoList
                .Where(px => px.TrangThai == "Đã xác nhận nhận hàng" || px.TrangThai == "Hoàn thành")
                .Select(px => px.MaXuatkho)
                .Where(mx => !string.IsNullOrEmpty(mx))
                .ToList();

            // Lấy tất cả vật tư đã xuất kho với trạng thái đã xác nhận
            var vtXuatKhoList = phieuXuatKhoDaXacNhan.Any()
                ? _context.vtphieuxuatkho
                    .Where(vt => !string.IsNullOrEmpty(vt.MaXuatkho) && phieuXuatKhoDaXacNhan.Contains(vt.MaXuatkho))
                    .AsNoTracking()
                    .ToList()
                : new List<vtphieuxuatkho>();

            int totalDu = 0;
            int totalTonDong = 0;
            foreach (var item in vatTuComparison)
            {
                // Tính "Đã cấp phát" = tổng số lượng vật tư đã được xuất kho và xác nhận nhận hàng
                // Đối chiếu theo MaSanpham và MaYeucau
                var daCapPhat = 0;
                
                // Lấy các mã yêu cầu liên quan đến vật tư này
                var maYeucauCuaVatTu = item.ChiTiet
                    .Select(c => c.MaYeucau)
                    .Where(my => !string.IsNullOrEmpty(my))
                    .Distinct()
                    .ToList();

                if (maYeucauCuaVatTu.Any() && !string.IsNullOrEmpty(item.MaSanpham))
                {
                    // Lấy các phiếu xuất kho của các mã yêu cầu này đã được xác nhận nhận hàng
                    var phieuXuatKhoCuaVatTu = phieuXuatKhoList
                        .Where(px => maYeucauCuaVatTu.Contains(px.MaYeucau) 
                            && (px.TrangThai == "Đã xác nhận nhận hàng" || px.TrangThai == "Hoàn thành"))
                        .Select(px => px.MaXuatkho)
                        .Where(mx => !string.IsNullOrEmpty(mx))
                        .ToList();

                    if (phieuXuatKhoCuaVatTu.Any())
                    {
                        // Tính tổng số lượng vật tư đã xuất kho với mã sản phẩm này
                        // Chỉ tính các vật tư trong các phiếu đã được xác nhận nhận hàng
                        daCapPhat = vtXuatKhoList
                            .Where(vt => phieuXuatKhoCuaVatTu.Contains(vt.MaXuatkho) 
                                && vt.MaSanpham == item.MaSanpham)
                            .Sum(vt => vt.SL ?? 0);
                    }
                }

                // Tính "Tồn đọng" = Tổng SL - Đã cấp phát
                var tonDong = Math.Max(0, item.TongSL - daCapPhat);

                // Tính "Dư" = Đã cấp phát - Tồn đọng (nếu đã cấp phát nhiều hơn tổng số lượng yêu cầu)
                var du = Math.Max(0, daCapPhat - item.TongSL);

                item.CapPhat = daCapPhat;
                item.TonDong = tonDong;
                item.Du = du;

                totalTonDong += item.TonDong;
                totalDu += item.Du;
            }

            ViewBag.ExcelFiles = excelFiles;
            ViewBag.VatTuComparison = vatTuComparison;
            ViewBag.TotalTonDong = totalTonDong;
            ViewBag.TotalDu = totalDu;
            ViewBag.HasMultipleFiles = excelFiles.Count > 1;

            return View();
        }

        [HttpGet]
        public IActionResult GetDulieuThongbao()
        {
            var chucVu = HttpContext.Session.GetString("Chucvu");
            var boPhan = HttpContext.Session.GetString("Bophan");
            var maNv = HttpContext.Session.GetString("MaNguoidung");

            // Lấy danh sách mã dự án mà người dùng là QLDA
            var Maduanquanli = _context.duans
                .Where(d => d.MaNguoiQLDA == maNv)
                .Select(d => d.MaDuan)
                .ToList();

            int thongbaomuahangcount = 0;
            if (boPhan == "BP mua hàng")
            {
                thongbaomuahangcount = _context.phieumuahang.Count(p => p.TrangThai == "Đang chờ báo giá");
            }
            else if (boPhan == "BP kế toán")
            {
                thongbaomuahangcount = _context.phieumuahang.Count(p => p.TrangThai == "Chờ thanh toán");
            }

            // Xuất kho - chỉ đếm các trạng thái còn cần xử lý (không đếm "Hoàn thành" và "Đã xác nhận nhận hàng")
            int thongbaoxuatkhocount = 0;
            if (boPhan == "BP kho")
            {
                thongbaoxuatkhocount = _context.phieuxuatkho.Count(p => p.TrangThai != "Hoàn thành" && p.TrangThai != "Đã xác nhận nhận hàng");
            }

            int thongbaonhapkhocount = 0;
            if (boPhan == "BP kho")
            {
                thongbaonhapkhocount = _context.phieunhapkho.Count(p => p.TrangThai == "Chờ nhập kho" || p.TrangThai == "Sẵn sàng nhập kho");
            }
            else
            {
                // QLDA đếm các phiếu nhập kho có trạng thái "Chờ quản lý dự án duyệt" thuộc các dự án quản lý
                thongbaonhapkhocount = _context.phieunhapkho.Count(p => 
                    p.TrangThai == "Chờ quản lý dự án duyệt" && 
                    !string.IsNullOrEmpty(p.MaDuan) && 
                    Maduanquanli.Contains(p.MaDuan));
            }
            // Đếm yêu cầu có trạng thái "Chờ quản lý dự án duyệt" thuộc các dự án quản lý
            int QLDAyeucaucount = _context.yeucau.Count(p => 
                p.TrangThai == "Chờ quản lý dự án duyệt" && 
                Maduanquanli.Contains(p.YCMaDuan));
            int thongbaoyeucaucount = QLDAyeucaucount;

            // Thông báo xác nhận nhận hàng - đếm phiếu xuất kho chờ xác nhận
            int thongbaoxacnhannhanhangcount = 0;
            var yeuCauList = _context.yeucau
                .Where(y => y.YCMaNguoidung == maNv)
                .Select(y => y.MaYeucau)
                .ToList();
            thongbaoxacnhannhanhangcount = _context.phieuxuatkho
                .Count(p => yeuCauList.Contains(p.MaYeucau) && p.TrangThai == "Chờ người yêu cầu xác nhận");

            return Json(new
            {
                thongbaoyeucaucount,
                thongbaomuahangcount,
                thongbaoxuatkhocount,
                thongbaonhapkhocount,
                thongbaoxacnhannhanhangcount
            });
        }

        [HttpGet]
        public IActionResult GetDulieuThongbaolayout()
        {
            return GetDulieuThongbao();
        }

        [HttpGet]
        public IActionResult GetDulieuThongbaotrangchu()
        {
            return GetDulieuThongbao();
        }

        [HttpGet]
        public IActionResult GetVTPhieuxuatkho(string MaXuatkho)
        {
            var PhieuxuatkhoList = _context.vtphieuxuatkho
                                 .Where(v => v.MaXuatkho == MaXuatkho).ToList();
            return Json(PhieuxuatkhoList);
        }

        
       

        [HttpGet]
        public IActionResult GetVTPhieumuahang(string MaMuahang)
        {
            var PhieumuahangList = _context.vtphieumuahang
                                 .Where(v => v.MaMuahang == MaMuahang).ToList();
            return Json(PhieumuahangList);
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

            // Lấy dữ liệu từ kho cá nhân
            var khoCaNhanItems = _context.khonguoidungs
                .Where(k => k.NDMaNguoidung == maNv && (k.TrangThai == "Đang mượn" || k.TrangThai == "Đang sử dụng") && k.SL > 0)
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
        public IActionResult GetKhoTongData()
        {
            var data = _context.khotongs
                .Select(k => new
                {
                    tenSanpham = k.TenSanpham,
                    maSanpham = k.MaSanpham,
                    hangSX = k.HangSX,
                    donVi = k.DonVi,
                    makho = k.Makho,
                    nhaCC = k.NhaCC
                })
                .ToList();

            return Json(data);
        }

        [HttpGet]
        public IActionResult GetDataByMaDuan(string maduan)
        {
            if (string.IsNullOrEmpty(maduan))
            {
                return Json(new
                {
                    maNguoidung = HttpContext.Session.GetString("MaNguoidung"),
                    maDuan = "",
                    vtPhieuMuaHang = new List<object>()
                });
            }

            // Lấy mã nhân viên từ session
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
                // Lấy vật tư từ vtphieuxuatkho (đã xuất kho) kết hợp với phieuxuatkho theo MaDuan
                // Các vật tư đã được xuất kho cho dự án này có thể được trả lại
                var khoDuanItems = (from vt in _context.vtphieuxuatkho
                                   join px in _context.phieuxuatkho on vt.MaXuatkho equals px.MaXuatkho
                                   join yc in _context.yeucau on vt.MaYeucau equals yc.MaYeucau
                                   where px.MaDuan == maduan 
                                      && yc.YCMaNguoidung == maNv
                                      && (vt.TrangThai == "Đã xác nhận nhận hàng" 
                                          || vt.TrangThai == "Đã lấy hàng"
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
                                   .Distinct() // Tránh trùng lặp nếu có
                                   .ToList();

                // Debug info
                Console.WriteLine($"Querying vtphieuxuatkho for MaDuan = '{maduan}'");
                Console.WriteLine($"Found {khoDuanItems.Count} items");
                
                // Debug: Kiểm tra số phiếu xuất kho có MaDuan này
                var phieuxuatCount = _context.phieuxuatkho.Count(p => p.MaDuan == maduan);
                Console.WriteLine($"Total phieuxuatkho records with MaDuan = '{maduan}': {phieuxuatCount}");

                return Json(new
                {
                    maNguoidung = maNv,
                    maDuan = maduan,
                    vtPhieuMuaHang = khoDuanItems,
                    debug = new
                    {
                        phieuxuatCount = phieuxuatCount,
                        returnedCount = khoDuanItems.Count
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetDataByMaDuan: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
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
            if (string.IsNullOrEmpty(timkiem))
            {
                return Json(new List<object>());
            }

            var searchTerm = timkiem.Trim().ToLower();
            var results = _context.khotongs
                .Where(k => (k.TenSanpham != null && k.TenSanpham.ToLower().Contains(searchTerm)) || 
                           (k.MaSanpham != null && k.MaSanpham.ToLower().Contains(searchTerm)))
                .Take(10) // Giới hạn 10 kết quả để hiệu suất tốt hơn
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
                                           List<int?> SLCu, List<int?> SLMoi,
                                           List<string> DonVi, List<string> GhiChu, string MaYeucau, string action, phieuxuatkho phieuxuatkho, vtphieuxuatkho vtphieuxuatkho, phieumuahang phieumuahang, vtphieumuahang vtphieumuahang)
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

                if (duan == null && !string.IsNullOrEmpty(yeucau.YCMaDuan))
                {
                    duan = _context.duans
                        .AsEnumerable()
                        .FirstOrDefault(d => d.MaDuan != null &&
                                             d.MaDuan.Equals(yeucau.YCMaDuan, StringComparison.OrdinalIgnoreCase));
                    if (duan != null)
                    {
                        yeucau.YCMaDuan = duan.MaDuan;
                    }
                }
                var laGiamdoc = string.Equals(chucVu2, "Giám đốc", StringComparison.OrdinalIgnoreCase);
                var laBoPhanDuAn = string.Equals(boPhan2, "BP dự án", StringComparison.OrdinalIgnoreCase);
                var laChucVuQLDA = string.Equals(chucVu2, "Quản lí dự án", StringComparison.OrdinalIgnoreCase);
                var laQLDADuAn = duan != null && maNv2 == duan.MaNguoiQLDA;

                if (laBoPhanDuAn || laChucVuQLDA || laQLDADuAn)
                {
                    yeucau.TrangThai = laGiamdoc ? "Đã duyệt" : "Chờ giám đốc duyệt";
                }
                else if (duan != null)
                {
                    string maNguoiQLDA = duan.MaNguoiQLDA;
                    if (maNv2 == maNguoiQLDA)
                    {
                        if (chucVu2 == "Trưởng BP")
                        {
                            yeucau.TrangThai = "Giám đốc";
                        }
                        else if (laGiamdoc)
                        {
                            yeucau.TrangThai = "Đã duyệt";

                        }
                        else if (chucVu2 == "Nhân viên" && boPhan2 == "BP kỹ thuật")
                        {
                            yeucau.TrangThai = "Trưởng BP-BP kỹ thuật";
                        }
                        else if (chucVu2 == "Nhân viên" && boPhan2 == "BP kho")
                        {
                            yeucau.TrangThai = "Trưởng BP-BP kho";
                        }
                        else if (chucVu2 == "Nhân viên" && boPhan2 == "BP mua hàng")
                        {
                            yeucau.TrangThai = "Trưởng BP-BP mua hàng";
                        }
                        else if (chucVu2 == "Nhân viên" && boPhan2 == "BP kế toán")
                        {
                            yeucau.TrangThai = "Trưởng BP-BP kế toán";
                        }
                    }
                    else
                    {
                        if (chucVu2 == "Nhân viên" && boPhan2 == "BP kỹ thuật")
                        {
                            yeucau.TrangThai = "Trưởng BP-BP kỹ thuật";
                        }
                        else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kỹ thuật")
                        {
                            yeucau.TrangThai = "Quản lí dự án";
                        }
                        else if (chucVu2 == "Nhân viên" && boPhan2 == "BP kho")
                        {
                            yeucau.TrangThai = "Trưởng BP-BP kho";
                        }
                        else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kho")
                        {
                            yeucau.TrangThai = "Quản lí dự án";
                        }
                        else if (chucVu2 == "Nhân viên" && boPhan2 == "BP mua hàng")
                        {
                            yeucau.TrangThai = "Trưởng BP-BP mua hàng";
                        }
                        else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP mua hàng")
                        {
                            yeucau.TrangThai = "Quản lí dự án";
                        }
                        else if (laGiamdoc)
                        {
                            yeucau.TrangThai = "Đã duyệt";
                        }
                    }
                }
                else
                {
                    if (chucVu2 == "Nhân viên" && boPhan2 == "BP kỹ thuật")
                    {
                        yeucau.TrangThai = "Trưởng BP-BP kỹ thuật";
                    }
                    else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kỹ thuật")
                    {
                        yeucau.TrangThai = "Giám đốc";
                    }
                    else if (chucVu2 == "Nhân viên" && boPhan2 == "BP kho")
                    {
                        yeucau.TrangThai = "Trưởng BP-BP kho";
                    }
                    else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kho")
                    {
                        yeucau.TrangThai = "Giám đốc";
                    }
                    else if (chucVu2 == "Nhân viên" && boPhan2 == "BP mua hàng")
                    {
                        yeucau.TrangThai = "Trưởng BP-BP mua hàng";
                    }
                    else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP mua hàng")
                    {
                        yeucau.TrangThai = "Giám đốc";
                    }
                    else if (laGiamdoc)
                    {
                        yeucau.TrangThai = "Đã duyệt";

                    }
                }

                // ================== TẠO MÃ YÊU CẦU ĐÚNG CHUẨN ==================
                
                // Lấy mã sản phẩm (ST) từ tên file Excel hoặc từ form
                string? stPart = null;
                
                // Ưu tiên đọc từ tên file Excel nếu có
               if (Request.Form.Files != null && Request.Form.Files.Count > 0)
                {
                    var excelFile = Request.Form.Files.FirstOrDefault(f => 
                        f.Name == "excel-upload" || 
                        (!string.IsNullOrEmpty(f.FileName) && (f.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) || f.FileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))));
                    
                    if (excelFile != null && !string.IsNullOrEmpty(excelFile.FileName))
                    {
                        try
                        {
                            // Lấy tên file không có extension
                            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(excelFile.FileName);
                            
                            // Xử lý tên file có thể có dấu gạch dưới: thay thế bằng khoảng trắng để parse
                            fileNameWithoutExt = fileNameWithoutExt.Replace('_', ' ');
                            
                            // Parse tên file: 
                            // - "DS03-AAT-TASS-02" → lấy "DS03-AAT-TASS-02" (mã vật tư đầu tiên)
                            // - "251005 STUP10.5013 251203" → lấy "STUP10.5013" (bỏ qua số 6 chữ số)
                            // - "251208 DS03-AAT-TASS-02" → lấy "DS03-AAT-TASS-02" (bỏ qua số 6 chữ số)
                            // Tách theo khoảng trắng
                            var parts = fileNameWithoutExt.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            
                            // Tìm phần không phải số 6 chữ số (mã vật tư đầu tiên)
                                foreach (var part in parts)
                                {
                                // Bỏ qua các phần là số 6 chữ số (mã dự án hoặc ngày)
                                    if (part.Length == 6 && part.All(char.IsDigit))
                                    {
                                    continue;
                                    }
                                // Phần không phải số 6 chữ số là mã vật tư đầu tiên
                                        stPart = part;
                                        break;
                                    }
                                
                            // Nếu sau khi parse không tìm thấy (có thể tên file chỉ có số hoặc format khác),
                            // và chỉ có 1 phần duy nhất, lấy phần đó làm mã vật tư
                            if (string.IsNullOrWhiteSpace(stPart) && parts.Length == 1)
                                {
                                    stPart = parts[0];
                            }
                        }
                        catch (Exception ex)
                        {
                            // Nếu parse tên file lỗi, fallback về MaSanpham từ form
                            Console.WriteLine($"Lỗi khi parse tên file Excel để lấy mã sản phẩm: {ex.Message}");
                        }
                    }
                }
                
                // Nếu không đọc được từ tên file, lấy từ MaSanpham form
                if (string.IsNullOrWhiteSpace(stPart))
                {
                    if (MaSanpham != null && MaSanpham.Count > 0)
                    {
                        stPart = MaSanpham.FirstOrDefault(m => !string.IsNullOrWhiteSpace(m));
                    }
                }
                
                if (string.IsNullOrWhiteSpace(stPart))
                {
                    stPart = "VT";
                }
                stPart = stPart.Replace(" ", ""); // Bỏ dấu cách
                
                // Lấy tên viết tắt từ MaNguoidung (không dùng hàm parse)
                string tenVietTat = maNv2 ?? yeucau.YCMaNguoidung ?? "NGUOIDUNG";
                
                if (!string.IsNullOrEmpty(yeucau.YCMaDuan))
                {
                    // ===== Trường hợp có Mã Dự Án: NNNNNN ST HiepNT =====
                    // Lấy 6 chữ số từ mã dự án
                    string maDuanFormatted = "000000";
                    var maDuanDigits = new string(yeucau.YCMaDuan.Where(char.IsDigit).ToArray());
                    if (maDuanDigits.Length > 6)
                    {
                        maDuanFormatted = maDuanDigits.Substring(maDuanDigits.Length - 6);
                    }
                    else
                    {
                        maDuanFormatted = maDuanDigits.PadLeft(6, '0');
                    }
                    
                    // Tạo mã yêu cầu: NNNNNN ST HiepNT
                    yeucau.MaYeucau = $"{maDuanFormatted} {stPart} {tenVietTat}";
                    
                    // Đảm bảo tính duy nhất cho mã yêu cầu (bao gồm cả tên người)
                    // Nếu cùng người gửi cùng mã cơ bản, thêm suffix để phân biệt
                    int suffixNumber = 1;
                    string originalMaYeucau = yeucau.MaYeucau;
                    while (true)
                    {
                        var exists = _context.yeucau
                                             .FirstOrDefault(x => x.MaYeucau == yeucau.MaYeucau);
                        if (exists == null)
                        {
                            break;
                        }
                        // Nếu trùng, thêm số suffix
                        yeucau.MaYeucau = $"{originalMaYeucau}{suffixNumber}";
                        suffixNumber++;
                    }
                }
                else
                {
                    // ===== Trường hợp KHÔNG có Mã Dự Án: YYMMDD ST HiepNT =====
                    // Lấy ngày hiện tại dạng YYMMDD
                    string datePart = DateTime.Now.ToString("yyMMdd");
                    
                    // Tạo mã yêu cầu: YYMMDD ST HiepNT
                    yeucau.MaYeucau = $"{datePart} {stPart} {tenVietTat}";
                    
                    // Đảm bảo tính duy nhất cho mã yêu cầu (bao gồm cả tên người)
                    // Nếu cùng người gửi cùng mã cơ bản, thêm suffix để phân biệt
                    int suffixNumber = 1;
                    string originalMaYeucau = yeucau.MaYeucau;
                    while (true)
                    {
                        var exists = _context.yeucau
                                             .FirstOrDefault(x => x.MaYeucau == yeucau.MaYeucau);
                        if (exists == null)
                        {
                            break;
                        }
                        // Nếu trùng, thêm số suffix
                        yeucau.MaYeucau = $"{originalMaYeucau}{suffixNumber}";
                        suffixNumber++;
                    }
                }
                // ================================================================

                // Luôn tạo yêu cầu mới để theo dõi từng người gửi
                _context.yeucau.Add(yeucau);
                _context.SaveChanges();

                // Lưu file Excel nếu có
                try
                {
                    if (Request.Form.Files != null && Request.Form.Files.Count > 0)
                    {
                        var excelFile = Request.Form.Files.FirstOrDefault(f => 
                            f.Name == "excel-upload" || 
                            (!string.IsNullOrEmpty(f.FileName) && (f.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) || f.FileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))));
                        
                        if (excelFile != null && excelFile.Length > 0)
                        {
                            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "excel");
                            if (!Directory.Exists(uploadsFolder))
                            {
                                Directory.CreateDirectory(uploadsFolder);
                            }

                            // Tên file theo cấu trúc mới: MaYeucau (đã bao gồm NNNNNN ST)
                            var uniqueFileName = $"{yeucau.MaYeucau.Replace(" ", "_")}_{Path.GetFileName(excelFile.FileName)}";
                            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                            using (var stream = new FileStream(filePath, FileMode.Create))
                            {
                                excelFile.CopyTo(stream);
                            }

                            var excelFileRecord = new ExcelFile
                            {
                                MaYeucau = yeucau.MaYeucau,
                                MaDuan = yeucau.YCMaDuan,
                                TenFile = excelFile.FileName,
                                DuongDanFile = $"/uploads/excel/{uniqueFileName}",
                                NgayUpload = DateTime.Now,
                                NguoiUpload = maNv2,
                                KichThuocFile = excelFile.Length
                            };

                            _context.ExcelFiles.Add(excelFileRecord);
                            _context.SaveChanges();
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log lỗi nhưng không dừng quá trình xử lý
                    Console.WriteLine($"Lỗi khi lưu file Excel: {ex.Message}");
                    Console.WriteLine($"Stack trace: {ex.StackTrace}");
                }

                int rowCount = new[]
                {
                    TenSanpham?.Count ?? 0,
                    MaSanpham?.Count ?? 0,
                    HangSX?.Count ?? 0,
                    NhaCC?.Count ?? 0,
                    SL?.Count ?? 0,
                    DonVi?.Count ?? 0,
                    YCMaKho?.Count ?? 0
                }.Max();

                for (int i = 0; i < rowCount; i++)
                {
                    var ten = (TenSanpham != null && i < TenSanpham.Count) ? TenSanpham[i] : null;
                    if (string.IsNullOrEmpty(ten))
                    {
                        continue;
                    }

                    var maKhoValue = (YCMaKho != null && i < YCMaKho.Count) ? YCMaKho[i] : null;
                    var maValue = (MaSanpham != null && i < MaSanpham.Count) ? MaSanpham[i] : null;
                    var hangValue = (HangSX != null && i < HangSX.Count) ? HangSX[i] : null;
                    var nhaCcValue = (NhaCC != null && i < NhaCC.Count) ? NhaCC[i] : null;
                    var donViValue = (DonVi != null && i < DonVi.Count) ? DonVi[i] : null;
                    var slCuValue = (SLCu != null && i < SLCu.Count) ? SLCu[i] : null;
                    var slMoiValue = (SLMoi != null && i < SLMoi.Count) ? SLMoi[i] : null;
                    var ghiChuValue = (GhiChu != null && i < GhiChu.Count) ? GhiChu[i] : null;
                    // Cột SL lấy giá trị từ SLMoi (nếu có), nếu không thì lấy từ SLCu, cuối cùng mới lấy từ SL
                    var slValue = slMoiValue ?? slCuValue ?? ((SL != null && i < SL.Count) ? (SL[i] ?? 0) : 0);

                    var khoMatch = _context.khotongs.FirstOrDefault(p => p.Makho == maKhoValue);
                    // Kiểm tra xem có phải vật tư từ kho cá nhân không
                    var khoCaNhanMatch = _context.khonguoidungs.FirstOrDefault(k => 
                        k.NDMakho == maKhoValue && 
                        k.MaSanpham == maValue && 
                        k.NDMaNguoidung == maNv2);
                    
                    if (khoMatch != null)
                    {
                        // Kiểm tra xem vật tư này đã tồn tại trong yêu cầu chưa
                        var existingVTYeucau = _context.vtyeucau
                            .FirstOrDefault(vt => vt.VTMaYeucau == yeucau.MaYeucau 
                                && string.Equals(vt.MaSanpham, maValue, StringComparison.OrdinalIgnoreCase));
                        
                        if (existingVTYeucau != null)
                        {
                            // Cập nhật vật tư yêu cầu hiện có
                            existingVTYeucau.TenSanpham = ten;
                            existingVTYeucau.HangSX = hangValue;
                            existingVTYeucau.NhaCC = nhaCcValue;
                            existingVTYeucau.SLCu = slCuValue;
                            existingVTYeucau.SLMoi = slMoiValue;
                            existingVTYeucau.SL = slValue;
                            existingVTYeucau.DonVi = donViValue;
                            existingVTYeucau.GhiChu = ghiChuValue;
                            existingVTYeucau.NgayCanHang = GetNgayCanHangAt(i);
                            existingVTYeucau.YCMakho = khoMatch.Makho;
                            existingVTYeucau.NgayNhapkho = khoMatch.NgayNhapkho;
                            existingVTYeucau.NgayBaohanh = khoMatch.NgayBaohanh;
                            existingVTYeucau.ThoiGianBH = khoMatch.ThoiGianBH;
                            
                            // Xử lý cập nhật theo logic mới
                            int slThieu;
                            var updateResult = YeucauUpdateHelper.XuLyCapNhatYeuCau(
                                _context, 
                                yeucau, 
                                maValue, 
                                slValue, 
                                khoMatch.Makho, 
                                out slThieu);
                            
                            if (updateResult.Success)
                            {
                                // Nếu là Quản lý dự án và lấy vật tư từ kho cá nhân, đặt trạng thái "Chờ Giám đốc duyệt"
                                if (laChucVuQLDA && khoCaNhanMatch != null)
                                {
                                    existingVTYeucau.TrangThai = "Chờ Giám đốc duyệt";
                                }
                                else if (string.IsNullOrEmpty(existingVTYeucau.TrangThai))
                                {
                                    existingVTYeucau.TrangThai = yeucau.TrangThai;
                                }
                                
                                _context.vtyeucau.Update(existingVTYeucau);
                            }
                        }
                        else
                        {
                            // Tạo mới vật tư yêu cầu
                            var newVtyeucau = new vtyeucau();
                            newVtyeucau.VTMaYeucau = yeucau.MaYeucau;
                            newVtyeucau.TenSanpham = ten;
                            newVtyeucau.MaSanpham = maValue;
                            newVtyeucau.HangSX = hangValue;
                            newVtyeucau.NhaCC = nhaCcValue;
                            newVtyeucau.SLCu = slCuValue;
                            newVtyeucau.SLMoi = slMoiValue;
                            newVtyeucau.SL = slValue;
                            newVtyeucau.DonVi = donViValue;
                            newVtyeucau.GhiChu = ghiChuValue;
                            newVtyeucau.NgayCanHang = GetNgayCanHangAt(i);
                            newVtyeucau.YCMakho = khoMatch.Makho;
                            newVtyeucau.NgayNhapkho = khoMatch.NgayNhapkho;
                            newVtyeucau.NgayBaohanh = khoMatch.NgayBaohanh;
                            newVtyeucau.ThoiGianBH = khoMatch.ThoiGianBH;
                            
                            // Nếu là Quản lý dự án và lấy vật tư từ kho cá nhân, đặt trạng thái "Chờ Giám đốc duyệt"
                            if (laChucVuQLDA && khoCaNhanMatch != null)
                            {
                                newVtyeucau.TrangThai = "Chờ Giám đốc duyệt";
                            }
                            else
                            {
                                newVtyeucau.TrangThai = yeucau.TrangThai;
                            }
                            
                            _context.vtyeucau.Add(newVtyeucau);
                        }
                    }
                    else
                    {
                        // Tạo bản ghi "VT mới" trong khotongs nếu chưa tồn tại
                        var vtMoiKho = _context.khotongs.FirstOrDefault(p => p.Makho == "VT mới");
                        if (vtMoiKho == null)
                        {
                            vtMoiKho = new khotongs
                            {
                                Makho = "VT mới",
                                TenSanpham = "Vật tư mới",
                                MaSanpham = "",
                                HangSX = "",
                                NhaCC = "",
                                SL = 0,
                                DonVi = "",
                                NgayNhapkho = null,
                                NgayBaohanh = null,
                                ThoiGianBH = null,
                                TrangThai = "VT mới"
                            };
                            _context.khotongs.Add(vtMoiKho);
                            _context.SaveChanges(); // Lưu ngay để đảm bảo Makho tồn tại
                        }

                        // Kiểm tra xem vật tư này đã tồn tại trong yêu cầu chưa
                        var existingVTYeucau = _context.vtyeucau
                            .FirstOrDefault(vt => vt.VTMaYeucau == yeucau.MaYeucau 
                                && string.Equals(vt.MaSanpham, maValue, StringComparison.OrdinalIgnoreCase));
                        
                        if (existingVTYeucau != null)
                        {
                            // Cập nhật vật tư yêu cầu hiện có
                            existingVTYeucau.TenSanpham = ten;
                            existingVTYeucau.HangSX = hangValue;
                            existingVTYeucau.NhaCC = nhaCcValue;
                            existingVTYeucau.SLCu = slCuValue;
                            existingVTYeucau.SLMoi = slMoiValue;
                            existingVTYeucau.SL = slValue;
                            existingVTYeucau.DonVi = donViValue;
                            existingVTYeucau.GhiChu = ghiChuValue;
                            existingVTYeucau.NgayCanHang = GetNgayCanHangAt(i);
                            existingVTYeucau.YCMakho = "VT mới";
                            existingVTYeucau.NgayNhapkho = null;
                            existingVTYeucau.NgayBaohanh = null;
                            existingVTYeucau.ThoiGianBH = null;
                            
                            // Xử lý cập nhật theo logic mới
                            int slThieu;
                            var updateResult = YeucauUpdateHelper.XuLyCapNhatYeuCau(
                                _context, 
                                yeucau, 
                                maValue, 
                                slValue, 
                                "VT mới", 
                                out slThieu);
                            
                            if (updateResult.Success)
                            {
                                if (string.IsNullOrEmpty(existingVTYeucau.TrangThai))
                                {
                                    existingVTYeucau.TrangThai = yeucau.TrangThai;
                                }
                                _context.vtyeucau.Update(existingVTYeucau);
                            }
                        }
                        else
                        {
                            // Tạo mới vật tư yêu cầu
                            var newVtyeucau = new vtyeucau();
                            newVtyeucau.VTMaYeucau = yeucau.MaYeucau;
                            newVtyeucau.TenSanpham = ten;
                            newVtyeucau.MaSanpham = maValue;
                            newVtyeucau.HangSX = hangValue;
                            newVtyeucau.NhaCC = nhaCcValue;
                            newVtyeucau.SLCu = slCuValue;
                            newVtyeucau.SLMoi = slMoiValue;
                            newVtyeucau.SL = slValue;
                            newVtyeucau.DonVi = donViValue;
                            newVtyeucau.GhiChu = ghiChuValue;
                            newVtyeucau.NgayCanHang = GetNgayCanHangAt(i);
                            newVtyeucau.YCMakho = "VT mới";
                            newVtyeucau.NgayNhapkho = null;
                            newVtyeucau.NgayBaohanh = null;
                            newVtyeucau.ThoiGianBH = null;
                            
                            // Nếu là Quản lý dự án và lấy vật tư từ kho cá nhân, đặt trạng thái "Chờ Giám đốc duyệt"
                            if (laChucVuQLDA && khoCaNhanMatch != null)
                            {
                                newVtyeucau.TrangThai = "Chờ Giám đốc duyệt";
                            }
                            else
                            {
                                newVtyeucau.TrangThai = yeucau.TrangThai;
                            }
                            
                            _context.vtyeucau.Add(newVtyeucau);
                        }
                    }
                    _context.SaveChanges();
                }
                
                // Kiểm tra nếu có vật tư từ kho cá nhân và là Quản lý dự án, cập nhật trạng thái yêu cầu
                if (laChucVuQLDA)
                {
                    var hasKhoCaNhan = _context.vtyeucau
                        .Any(v => v.VTMaYeucau == yeucau.MaYeucau && 
                                  v.TrangThai == "Chờ Giám đốc duyệt");
                    if (hasKhoCaNhan)
                    {
                        yeucau.TrangThai = "Chờ Giám đốc duyệt";
                        _context.yeucau.Update(yeucau);
                        _context.SaveChanges();
                    }
                }
                
                if (yeucau.TrangThai == "Đã duyệt")
                {
                    Xuliphieuyeucau(yeucau.MaYeucau, phieuxuatkho, vtphieuxuatkho, phieumuahang, vtphieumuahang, yeucau, vtyeucau);
                }
            }
            else
            {
                int nextNumber = 1;

                while (true)
                {
                    phieunhapkho.MaNhapkho = $"PNK{nextNumber}";

                    var existingEntry = _context.phieunhapkho
                                                .FirstOrDefault(y => y.MaNhapkho == phieunhapkho.MaNhapkho);
                    if (existingEntry == null)
                    {
                        break;
                    }
                    nextNumber++;
                }
            }

            return RedirectToAction("Yeucau", "Yeucau", new { area = "QuanLiDuAn" });

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
                if (duan != null)
                {
                    string maNguoiQLDA = duan.MaNguoiQLDA;
                    if (maNv2 == maNguoiQLDA)
                    {
                        if (chucVu2 != "Giám đốc")
                        {
                            Yeucau.TrangThai = "Giám đốc";
                        }
                        else
                        {
                            Yeucau.TrangThai = "Đã duyệt";
                            Xuliphieuyeucau(Yeucau.MaYeucau, phieuxuatkho, vtphieuxuatkho, phieumuahang, vtphieumuahang, yeucau, vtyeucau);
                        }
                    }
                    else
                    {
                        if (Yeucau.YCMaNguoidung != maNguoiQLDA)
                        {
                            if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kỹ thuật")
                            {
                                Yeucau.TrangThai = "Quản lí dự án";
                            }
                            else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kho")
                            {
                                Yeucau.TrangThai = "Quản lí dự án";
                            }
                            else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP mua hàng")
                            {
                                Yeucau.TrangThai = "Quản lí dự án";
                            }
                            else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kế toán")
                            {
                                Yeucau.TrangThai = "Quản lí dự án";
                            }
                            else if (chucVu2 == "Giám đốc")
                            {
                                Yeucau.TrangThai = "Đã duyệt";
                                Xuliphieuyeucau(Yeucau.MaYeucau, phieuxuatkho, vtphieuxuatkho, phieumuahang, vtphieumuahang, yeucau, vtyeucau);
                            }
                        }
                        else
                        {
                            if (chucVu2 != "Giám đốc")
                            {
                                Yeucau.TrangThai = "Giám đốc";
                            }
                            else
                            {
                                Yeucau.TrangThai = "Đã duyệt";
                                Xuliphieuyeucau(Yeucau.MaYeucau, phieuxuatkho, vtphieuxuatkho, phieumuahang, vtphieumuahang, yeucau, vtyeucau);
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
                    }
                    else if (chucVu2 == "Nhân viên" && boPhan2 == "BP kho")
                    {
                        Yeucau.TrangThai = "Trưởng BP-BP kho";
                    }
                    else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kho")
                    {
                        Yeucau.TrangThai = "Giám đốc";
                    }
                    else if (chucVu2 == "Nhân viên" && boPhan2 == "BP mua hàng")
                    {
                        Yeucau.TrangThai = "Trưởng BP-BP mua hàng";
                    }
                    else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP mua hàng")
                    {
                        Yeucau.TrangThai = "Giám đốc";
                    }
                    else if (chucVu2 == "Giám đốc")
                    {
                        Yeucau.TrangThai = "Đã duyệt";
                        Xuliphieuyeucau(Yeucau.MaYeucau, phieuxuatkho, vtphieuxuatkho, phieumuahang, vtphieumuahang, yeucau, vtyeucau);
                    }
                }
            }
            else if (action == "reject")
            {
                Xulituchoiyeucau(MaYeucau, yeucau, vtyeucau, null, null);
            }
            _context.yeucau.Update(Yeucau);
            _context.SaveChanges();

            return RedirectToAction("Yeucau", "Yeucau", new { area = "QuanLiDuAn" });
        }

        [HttpPost]
        public IActionResult Xuliphieuyeucau(
                                string Mayeucau,
                                phieuxuatkho phieuxuatkho,
                                vtphieuxuatkho vtphieuxuatkho,
                                phieumuahang phieumuahang,
                                vtphieumuahang vtphieumuahang,
                                yeucau yeucau,
                                vtyeucau vtyeucau)
        {
            var danhSachVatTuYC = _context.vtyeucau
                                          .Where(vt => vt.VTMaYeucau == Mayeucau)
                                          .ToList();

            var thongTinYeuCau = _context.yeucau
                                        .FirstOrDefault(yc => yc.MaYeucau == Mayeucau);

            if (thongTinYeuCau == null || danhSachVatTuYC == null || !danhSachVatTuYC.Any())
            {
                Console.WriteLine("Không tìm thấy yêu cầu hoặc danh sách vật tư.");
                return RedirectToAction("Yeucau", "Yeucau", new { area = "QuanLiDuAn" });
            }

            var makhoList = danhSachVatTuYC
                .Where(vt => !string.IsNullOrEmpty(vt.YCMakho))
                .Select(vt => vt.YCMakho)
                .Distinct()
                .ToList();

            var danhSachVTTrongKho = _context.khotongs
                .Where(kt => makhoList.Contains(kt.Makho))
                .ToList();

            // Kiểm tra xem đã có phiếu xuất kho hoặc phiếu mua hàng đã nhập kho (hoàn thành) cho yêu cầu này chưa
            var trangThaiHoanThanh = new[] { "Hoàn thành", "Đã xác nhận nhận hàng", "Đã xuất kho", "Đã nhập kho" };
            var existingPhieuXuatKhoDaNhapKho = _context.phieuxuatkho
                .Where(px => px.MaYeucau == Mayeucau && trangThaiHoanThanh.Contains(px.TrangThai ?? ""))
                .Any();
            
            var existingPhieuMuaHangDaNhapKho = _context.phieumuahang
                .Where(pm => pm.MaYeucau == Mayeucau && trangThaiHoanThanh.Contains(pm.TrangThai ?? ""))
                .Any();

            // Nếu đã có phiếu đã nhập kho, luôn tạo phiếu mới (không gộp vào phiếu cũ)
            bool shouldCreateNewPhieu = existingPhieuXuatKhoDaNhapKho || existingPhieuMuaHangDaNhapKho;

            string Maxuatkho;
            int Numberpxk = 1;

            while (true)
            {
                Maxuatkho = $"PXK{Numberpxk}";

                var existingEntry = _context.phieuxuatkho
                                           .FirstOrDefault(y => y.MaXuatkho == Maxuatkho);

                if (existingEntry == null)
                {
                    break;
                }
                Numberpxk++;
            }

            int Numberpmh = 1;
            string Mamuahang;

            while (true)
            {
                Mamuahang = $"PMH{Numberpmh}";

                var existingEntry = _context.phieumuahang
                                           .FirstOrDefault(y => y.MaMuahang == Mamuahang);

                if (existingEntry == null)
                {
                    break;
                }
                Numberpmh++;
            }

            bool isPhieuXuatKhoCreated = false;
            bool isPhieuMuaHangCreated = false;

            foreach (var VattuYC in danhSachVatTuYC)
            {
                var khoHienTai = danhSachVTTrongKho
                    .FirstOrDefault(kt => kt.Makho == VattuYC.YCMakho && kt.MaSanpham == VattuYC.MaSanpham);

                var soLuongYeuCau = VattuYC.SL ?? 0;
                var soLuongTrongKho = khoHienTai?.SL ?? 0;

                if (soLuongYeuCau > 0 && khoHienTai != null && soLuongTrongKho >= soLuongYeuCau)
                {
                    isPhieuXuatKhoCreated = true;
                }
                else
                {
                    isPhieuMuaHangCreated = true;
                }
            }

            if (isPhieuXuatKhoCreated)
            {
                var Phieuxuatkho = new phieuxuatkho
                {
                    MaXuatkho = Maxuatkho,
                    MaYeucau = thongTinYeuCau.MaYeucau,
                    MaDuan = thongTinYeuCau.YCMaDuan,
                    MaNguoidung = thongTinYeuCau.YCMaNguoidung,
                    NgayXuatkho = DateTime.Now,
                    TrangThai = "Đang chuẩn bị hàng"
                };
                _context.Add(Phieuxuatkho);
            }

            if (isPhieuMuaHangCreated)
            {
                var Phieumuahang = new phieumuahang
                {
                    MaMuahang = Mamuahang,
                    MaYeucau = thongTinYeuCau.MaYeucau,
                    MaDuan = thongTinYeuCau.YCMaDuan,
                    NgayMuahang = DateTime.Now,
                    MaNguoidung = thongTinYeuCau.YCMaNguoidung,
                    TrangThai = "Đang chờ báo giá"
                };
                _context.Add(Phieumuahang);
            }

            _context.SaveChanges();

            foreach (var VattuYC in danhSachVatTuYC)
            {
                var khotong = danhSachVTTrongKho
                    .FirstOrDefault(kt => kt.Makho == VattuYC.YCMakho && kt.MaSanpham == VattuYC.MaSanpham);

                var soLuongYeuCau = VattuYC.SL ?? 0;
                var soLuongTrongKho = khotong?.SL ?? 0;

                if (isPhieuXuatKhoCreated && khotong != null && soLuongTrongKho >= soLuongYeuCau && soLuongYeuCau > 0)
                {
                    var VTPhieuxuatkho = new vtphieuxuatkho
                    {
                        MaXuatkho = Maxuatkho,
                        MaYeucau = VattuYC.VTMaYeucau,
                        TenSanpham = khotong.TenSanpham,
                        MaSanpham = khotong.MaSanpham,
                        Makho = khotong.Makho,
                        HangSX = khotong.HangSX,
                        NhaCC = khotong.NhaCC,
                        DonVi = khotong.DonVi,
                        NgayBaohanh = khotong.NgayBaohanh,
                        ThoiGianBH = khotong.ThoiGianBH,
                        TrangThai = "Đang chuẩn bị hàng",
                        SL = VattuYC.SL
                    };

                    VattuYC.TrangThai = "Đã duyệt";
                    _context.vtyeucau.Update(VattuYC);
                    _context.Add(VTPhieuxuatkho);
                    continue;
                }

                if (isPhieuMuaHangCreated)
                {
                    VattuYC.TrangThai = "Đang mua hàng";
                    var VTPhieumuahang = new vtphieumuahang
                    {
                        MaMuahang = Mamuahang,
                        MaYeucau = VattuYC.VTMaYeucau,
                        TenSanpham = VattuYC.TenSanpham,
                        MaSanpham = VattuYC.MaSanpham,
                        Makho = VattuYC.YCMakho,
                        HangSX = VattuYC.HangSX,
                        NhaCC = VattuYC.NhaCC,
                        DonVi = VattuYC.DonVi,
                        SL = VattuYC.SL,
                        NgayBaohanh = VattuYC.NgayBaohanh,
                        ThoiGianBH = VattuYC.ThoiGianBH,
                        TrangThai = "Đang chờ báo giá"
                    };

                    _context.vtyeucau.Update(VattuYC);
                    _context.Add(VTPhieumuahang);
                }
            }

            _context.SaveChanges();


            return RedirectToAction("Yeucau", "Yeucau", new { area = "QuanLiDuAn" });
        }

        [HttpPost]
        public IActionResult Xuliphieuxuatkho(
                                string MaXuatkho,
                                phieuxuatkho phieuxuatkho,
                                vtphieuxuatkho vtphieuxuatkho,
                                khoduans khoduans)
        {
            var VTphieuxuatkho = _context.vtphieuxuatkho
                                          .Where(vt => vt.MaXuatkho == MaXuatkho)
                                          .ToList();

            var Phieuxuatkho = _context.phieuxuatkho
                                        .FirstOrDefault(yc => yc.MaXuatkho == MaXuatkho);


            if (Phieuxuatkho.TrangThai == "Đang chuẩn bị hàng")
            {
                Phieuxuatkho.TrangThai = "Chờ lấy hàng";
                _context.phieuxuatkho.Update(Phieuxuatkho);
                _context.SaveChanges();
            }
            else if (Phieuxuatkho.TrangThai == "Chờ lấy hàng")
            {
                if (Phieuxuatkho.MaDuan != null)
                {
                    foreach (var VTxuatkho in VTphieuxuatkho)
                    {
                        var VTphieuxuatkhott = _context.vtphieuxuatkho.FirstOrDefault(vt => vt.MaXuatkho == VTxuatkho.MaXuatkho);
                        VTphieuxuatkhott.TrangThai = "Đã xuất kho";
                        _context.vtphieuxuatkho.Update(VTphieuxuatkhott);
                        var VTduan = new khoduans
                        {
                            DAMaDuan = Phieuxuatkho.MaDuan,
                            TenSanpham = VTxuatkho.TenSanpham,
                            MaSanpham = VTxuatkho.MaSanpham,
                            DAMakho = VTxuatkho.Makho,
                            HangSX = VTxuatkho.HangSX,
                            NhaCC = VTxuatkho.NhaCC,
                            DonVi = VTxuatkho.DonVi,
                            SL = VTxuatkho.SL,
                            NgayBaohanh = VTxuatkho.NgayBaohanh,
                            ThoiGianBH = VTxuatkho.ThoiGianBH,
                            TrangThai = "Đã xuất kho"
                        };
                        _context.Add(VTduan);
                    }
                    _context.SaveChanges();
                }
                else
                {
                    foreach (var VTxuatkho in VTphieuxuatkho)
                    {
                        var VTphieuxuatkhott = _context.vtphieuxuatkho.FirstOrDefault(vt => vt.MaXuatkho == VTxuatkho.MaXuatkho);
                        VTphieuxuatkhott.TrangThai = "Đã xuất kho";
                        _context.vtphieuxuatkho.Update(VTphieuxuatkhott);
                        var VTkhonguoidungtt = _context.khonguoidungs.FirstOrDefault(nd => nd.NDMakho == VTxuatkho.Makho && nd.NDMaNguoidung == Phieuxuatkho.MaNguoidung);
                        if (VTkhonguoidungtt != null)
                        {
                            VTkhonguoidungtt.SL = VTkhonguoidungtt.SL + VTxuatkho.SL;
                            _context.khonguoidungs.Update(VTkhonguoidungtt);
                        }
                        else
                        {
                            var VTkhonguoidung = new khonguoidungs
                            {
                                NDMaNguoidung = Phieuxuatkho.MaNguoidung,
                                TenSanpham = VTxuatkho.TenSanpham,
                                MaSanpham = VTxuatkho.MaSanpham,
                                NDMakho = VTxuatkho.Makho,
                                HangSX = VTxuatkho.HangSX,
                                NhaCC = VTxuatkho.NhaCC,
                                DonVi = VTxuatkho.DonVi,
                                SL = VTxuatkho.SL,
                                NgayBaohanh = VTxuatkho.NgayBaohanh,
                                ThoiGianBH = VTxuatkho.ThoiGianBH,
                                TrangThai = "Đang mượn"
                            };
                            _context.Add(VTkhonguoidung);
                        }

                    }
                }
                Phieuxuatkho.TrangThai = "Đã lấy hàng";
                _context.phieuxuatkho.Update(Phieuxuatkho);
                _context.SaveChanges();
            }
            else if (Phieuxuatkho.TrangThai == "Đã lấy hàng")
            {
                Phieuxuatkho.TrangThai = "Hoàn thành";
                _context.phieuxuatkho.Update(Phieuxuatkho);
                _context.SaveChanges();
            }
            return RedirectToAction("Phieuxuatkho", "Yeucau", new { area = "QuanLiDuAn" });
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
            return RedirectToAction("Phieumuahang", "Yeucau", new { area = "QuanLiDuAn" });
        }

        [HttpPost]
        public IActionResult Taophieunhapkhobyphieumuahang(string MaMuahang, phieunhapkho phieunhapkho, vtphieunhapkho vtphieunhapkho, phieumuahang phieumuahang, vtphieumuahang vtphieumuahang)
        {
            var Phieumuahang = _context.phieumuahang.FirstOrDefault(p => p.MaMuahang == MaMuahang);
            var VTPhieumuahanglist = _context.vtphieumuahang.Where(vt => vt.MaMuahang == MaMuahang).ToList();

            int STT = 1;
            string MaNhapkho;

            // Tạo mã phiếu nhập kho duy nhất
            while (true)
            {
                MaNhapkho = $"PNK{STT}";
                var existingEntry = _context.phieunhapkho
                                           .FirstOrDefault(y => y.MaNhapkho == MaNhapkho);

                if (existingEntry == null)
                {
                    break;
                }
                STT++;
            }

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

            foreach (var VTPhieumuahang in VTPhieumuahanglist)
            {
                var newvtphieunhapkho = new vtphieunhapkho
                {
                    MaNhapkho = MaNhapkho,
                    MaYeucau = VTPhieumuahang.MaYeucau,
                    TenSanpham = VTPhieumuahang.TenSanpham,
                    MaSanpham = VTPhieumuahang.MaSanpham,
                    Makho = VTPhieumuahang.Makho,
                    HangSX = VTPhieumuahang.HangSX,
                    NhaCC = VTPhieumuahang.NhaCC,
                    SL = VTPhieumuahang.SL,
                    DonVi = VTPhieumuahang.DonVi,
                    TrangThai = "Chờ nhập kho",
                };
                _context.vtphieunhapkho.Add(newvtphieunhapkho);
            }
            _context.SaveChanges();

            return RedirectToAction("Phieumuahang", "Yeucau", new { area = "QuanLiDuAn" });
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

            return Json(new
            {
                maNguoidung = yeucau.YCMaNguoidung,
                maDuan = yeucau.YCMaDuan,
                vtPhieuMuaHang = vtYeucau  // Trả về dữ liệu từ vtyeucau
            });
        }

        // Đã rollback logic sinh mã kho cho VT mới, giữ nguyên "VT mới" như trước

        [HttpPost]
        [HttpPost]
        public IActionResult ThemPhieunhapkhoSQL(phieunhapkho phieunhapkho, vtphieunhapkho vtphieunhapkho,
            string[] TenSanpham, string[] MaSanpham, string[] HangSX, string[] NhaCC,
            int[] SL, string[] DonVi, string[] Makho, string LoaiNhapkho, decimal[] DonGia, string[] DiengiaiNhapKho)
        {
            // Lưu session ngay từ đầu để đảm bảo không bị mất khi có exception
            var maNv = HttpContext.Session.GetString("MaNguoidung");
            if (string.IsNullOrEmpty(maNv))
            {
                TempData["Error"] = "Session đã hết hạn. Vui lòng đăng nhập lại!";
                return RedirectToAction("Login", "Home", new { area = "" });
            }

            // Lưu area để dùng trong catch block
            string currentArea = "QuanLiDuAn";

            try
            {

                // Kiểm tra dữ liệu đầu vào
                if (TenSanpham == null || TenSanpham.Length == 0)
                {
                    TempData["Error"] = "Vui lòng nhập ít nhất một vật tư!";
                    return RedirectToAction("ThemPhieunhapkho", "Yeucau", new { area = "QuanLiDuAn" });
                }

                if (string.IsNullOrEmpty(LoaiNhapkho))
                {
                    TempData["Error"] = "Vui lòng chọn loại nhập kho!";
                    return RedirectToAction("ThemPhieunhapkho", "Yeucau", new { area = "QuanLiDuAn" });
                }

                // maNv đã được lấy ở trên (ngoài try block để đảm bảo không bị mất)
                if (string.IsNullOrEmpty(phieunhapkho.MaNguoidung))
                {
                    phieunhapkho.MaNguoidung = maNv;
                }

                // Tính toán số lượng các phần tử
                int count = TenSanpham.Length;

                // VALIDATION: Không cho trả vượt quá số lượng đã mượn
                // Gom lỗi để hiển thị một lần
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
                        // Tổng số lượng đã xuất cho dự án (các trạng thái hợp lệ) theo mã sản phẩm và mã kho
                        soLuongDaMuon = (from vt in _context.vtphieuxuatkho
                                         join px in _context.phieuxuatkho on vt.MaXuatkho equals px.MaXuatkho
                                         where px.MaDuan == phieunhapkho.MaDuan
                                               && (vt.TrangThai == "Đã xác nhận nhận hàng"
                                                   || vt.TrangThai == "Đã lấy hàng"
                                                   || vt.TrangThai == "Đã xuất kho")
                                               && (vt.SL ?? 0) > 0
                                               && (maSp == "" || vt.MaSanpham == maSp)
                                               && (maKho == "" || vt.Makho == maKho)
                                         select vt.SL ?? 0).Sum();
                    }
                    else if (LoaiNhapkho == "canhan")
                    {
                        // Tổng số lượng đang mượn ở kho cá nhân của người dùng theo mã SP
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
                    return RedirectToAction("ThemPhieunhapkho", "Yeucau", new { area = "QuanLiDuAn" });
                }

                int STT = 0;
                string MaNhapkho;

                // Tạo mã phiếu nhập kho duy nhất
                while (true)
                {
                    MaNhapkho = $"PNK{STT}";
                    var existingEntry = _context.phieunhapkho
                                               .FirstOrDefault(y => y.MaNhapkho == MaNhapkho);

                    if (existingEntry == null)
                    {
                        break;
                    }
                    STT++;
                }

                phieunhapkho.MaNhapkho = MaNhapkho;
                phieunhapkho.NgayNhapkho = DateTime.Now;

                // Thiết lập trạng thái ban đầu theo quy trình duyệt
                // Với QuanLiDuAn, khi tạo yêu cầu nhập kho trạng thái sẽ là "Chờ giám đốc duyệt"
                // Nếu có dự án: gửi đến Giám đốc (thay vì Trưởng dự án)
                // Nếu không có dự án (cá nhân): gửi đến Giám đốc
                if (!string.IsNullOrEmpty(phieunhapkho.MaDuan))
                {
                    phieunhapkho.TrangThai = "Chờ giám đốc duyệt"; // Giám đốc duyệt (thay đổi từ "Chờ quản lý dự án duyệt")
                }
                else
                {
                    phieunhapkho.TrangThai = "Chờ Giám đốc duyệt"; // Giám đốc duyệt
                }

                // Tạo hoặc lấy mã yêu cầu đặc biệt cho phiếu nhập kho từ dự án/cá nhân
                // Nếu không có MaYeucau, tạo một yeucau đặc biệt để thỏa mãn foreign key constraint
                if (string.IsNullOrEmpty(phieunhapkho.MaYeucau))
                {
                    // Tạo mã yêu cầu đặc biệt dựa trên loại nhập kho
                    string maYeucauDacBiet = "";
                    if (LoaiNhapkho == "duan" && !string.IsNullOrEmpty(phieunhapkho.MaDuan))
                    {
                        maYeucauDacBiet = $"NHAPKHO_DUAN_{phieunhapkho.MaDuan}";
                    }
                    else if (LoaiNhapkho == "canhan")
                    {
                        maYeucauDacBiet = $"NHAPKHO_CANHAN_{maNv}";
                    }
                    else
                    {
                        maYeucauDacBiet = $"NHAPKHO_TUDO_{maNv}_{DateTime.Now:yyyyMMddHHmmss}";
                    }

                    // Kiểm tra xem yeucau đặc biệt đã tồn tại chưa
                    var existingYeucauDacBiet = _context.yeucau
                        .FirstOrDefault(y => y.MaYeucau == maYeucauDacBiet);

                    // Nếu chưa có yêu cầu, tạo mới với trạng thái phù hợp
                    if (existingYeucauDacBiet == null)
                    {
                        // Kiểm tra xem MaDuan có tồn tại trong bảng duans không
                        string ycMaDuan = null;
                        if (!string.IsNullOrEmpty(phieunhapkho.MaDuan))
                        {
                            // Tìm kiếm mã dự án trong database
                            // MySQL có thể case-sensitive, nên thử cả exact match và case-insensitive
                            var duanExists = _context.duans
                                .FirstOrDefault(d => d.MaDuan == phieunhapkho.MaDuan);

                            // Nếu không tìm thấy với exact match, thử case-insensitive
                            if (duanExists == null)
                            {
                                duanExists = _context.duans
                                    .AsEnumerable() 
                                    .FirstOrDefault(d => d.MaDuan != null &&
                                                       d.MaDuan.Equals(phieunhapkho.MaDuan, StringComparison.OrdinalIgnoreCase));
                            }

                            if (duanExists != null)
                            {
                                
                                ycMaDuan = duanExists.MaDuan;
                                Console.WriteLine($"Found project: '{duanExists.MaDuan}' for input '{phieunhapkho.MaDuan}'");
                            }
                            else
                            {
                               
                                var allDuans = _context.duans.Select(d => d.MaDuan).ToList();
                                Console.WriteLine($"Warning: Mã dự án '{phieunhapkho.MaDuan}' không tồn tại trong bảng duans.");
                                Console.WriteLine($"Available project codes: {string.Join(", ", allDuans)}");
                                
                            }
                        }

                        // Lấy thông tin người dùng từ bảng nguoidungs
                        var nguoiDung = _context.nguoidungs.FirstOrDefault(n => n.MaNguoidung == maNv);
                        string tenNguoiDung = nguoiDung?.TenNguoidung ?? "";
                        string boPhanNguoiDung = nguoiDung?.Bophan ?? "";

                        // Tạo yeucau đặc biệt mới
                        // Với QuanLiDuAn, trạng thái sẽ là "Chờ giám đốc duyệt"
                        var newYeucauDacBiet = new yeucau
                        {
                            MaYeucau = maYeucauDacBiet,
                            TenYeucau = "Yêu cầu nhập kho",
                            YCMaNguoidung = maNv,
                            NguoiYeucau = tenNguoiDung,
                            Bophan = boPhanNguoiDung,
                            YCMaDuan = ycMaDuan, // NULL nếu không có hoặc không tồn tại trong duans
                            NgayYeucau = DateTime.Now,
                            // Với QuanLiDuAn, khi tạo yêu cầu nhập kho trạng thái sẽ là "Chờ giám đốc duyệt"
                            TrangThai = (LoaiNhapkho == "duan" && !string.IsNullOrEmpty(phieunhapkho.MaDuan))
                                ? "Chờ giám đốc duyệt"
                                : (LoaiNhapkho == "canhan"
                                    ? "Chờ Giám đốc duyệt"
                                    : "Đã duyệt") // Giữ nguyên với các luồng khác
                        };
                        _context.yeucau.Add(newYeucauDacBiet);
                        _context.SaveChanges();
                    }

                    phieunhapkho.MaYeucau = maYeucauDacBiet;
                }

                _context.phieunhapkho.Add(phieunhapkho);
                _context.SaveChanges();

                // LƯU Ý QUAN TRỌNG: KHÔNG trừ từ kho dự án/cá nhân ngay khi tạo phiếu nhập kho
                // Chỉ trừ khi kho duyệt phiếu nhập kho (trong Xuliphieunhapkho)
                // Vì nếu trừ ngay thì sẽ thiệt hại kho nếu phiếu bị từ chối hoặc chưa được duyệt

                // Xử lý vật tư - CHỈ tạo bản ghi, KHÔNG trừ từ kho
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
                        TrangThai = phieunhapkho.TrangThai, // Sẽ là "Chờ giám đốc duyệt" cho QuanLiDuAn
                        MaNhapkho = MaNhapkho,
                        MaYeucau = phieunhapkho.MaYeucau // Dùng cùng MaYeucau với phieunhapkho
                    };

                    _context.vtphieunhapkho.Add(newvtphieunhapkho);
                }

                // Save changes sau khi xử lý tất cả vật tư
                try
                {
                    _context.SaveChanges();
                }
                catch (Exception exSave)
                {
                    Console.WriteLine($"Error saving changes: {exSave.Message}");
                    Console.WriteLine($"Stack trace: {exSave.StackTrace}");
                    throw; // Re-throw để catch block bên ngoài xử lý
                }

                TempData["Success"] = "Tạo phiếu nhập kho thành công!";
                return RedirectToAction("Phieunhapkho", "Yeucau", new { area = "QuanLiDuAn" });
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

                // Kiểm tra session - dùng biến maNv đã lưu trước đó thay vì lấy lại
                // Vì có thể exception làm mất session, nhưng nếu maNv đã được lưu thì vẫn dùng được
                var maNvCheck = HttpContext.Session.GetString("MaNguoidung") ?? maNv;
                Console.WriteLine($"Session MaNguoidung after error: {maNvCheck ?? "NULL"}");
                Console.WriteLine($"Original maNv (from before try): {maNv ?? "NULL"}");

                // Luôn redirect về trang tạo phiếu với thông báo lỗi
                // Không redirect về login trừ khi thực sự không có session từ đầu
                TempData["Error"] = $"Có lỗi xảy ra khi xử lý: {ex.Message}. Vui lòng kiểm tra lại dữ liệu hoặc liên hệ admin.";

                // Luôn redirect về trang tạo phiếu để người dùng có thể thử lại
                // Chỉ redirect về login nếu thực sự không có maNv từ đầu
                if (!string.IsNullOrEmpty(maNv))
                {
                    return RedirectToAction("ThemPhieunhapkho", "Yeucau", new { area = currentArea });
                }
                else
                {
                    // Trường hợp này chỉ xảy ra nếu session đã hết hạn từ đầu (đã check ở trên)
                    TempData["Error"] = "Session đã hết hạn. Vui lòng đăng nhập lại!";
                    return RedirectToAction("Login", "Home", new { area = "" });
                }
            }
        }


        [HttpPost]
        public IActionResult Xuliphieunhapkho(
                                string MaNhapkho, string submitAction,
                                phieuxuatkho phieunhapkho,
                                vtphieuxuatkho vtphieunhapkho, phieuxuatkho phieuxuatkho, vtphieuxuatkho vtphieuxuatkho)
        {
            Console.WriteLine("===========================================");
            Console.WriteLine("[QLDA DEBUG] Xuliphieunhapkho called");
            Console.WriteLine($"[QLDA DEBUG] MaNhapkho: {MaNhapkho}");
            Console.WriteLine($"[QLDA DEBUG] submitAction: {submitAction}");
            
            var chucVu2 = HttpContext.Session.GetString("Chucvu");
            var boPhan2 = HttpContext.Session.GetString("Bophan");
            var maNv2 = HttpContext.Session.GetString("MaNguoidung");
            
            Console.WriteLine($"[QLDA DEBUG] Session - Chucvu: {chucVu2}, Bophan: {boPhan2}, MaNguoidung: {maNv2}");

            var Phieunhapkho = _context.phieunhapkho.FirstOrDefault(p => p.MaNhapkho == MaNhapkho);
            if (Phieunhapkho == null)
            {
                Console.WriteLine($"[QLDA DEBUG] ERROR: Không tìm thấy phiếu nhập kho với MaNhapkho: {MaNhapkho}");
                return NotFound();
            }

            Console.WriteLine($"[QLDA DEBUG] Phieunhapkho found - TrangThai: {Phieunhapkho.TrangThai}, MaDuan: {Phieunhapkho.MaDuan}");

            var VTPhieunhapkholist = _context.vtphieunhapkho.Where(vt => vt.MaNhapkho == MaNhapkho).ToList();
            Console.WriteLine($"[QLDA DEBUG] Số lượng vật tư: {VTPhieunhapkholist.Count}");
            
            // Lấy thông tin dự án (nếu có)
            var duan = !string.IsNullOrEmpty(Phieunhapkho.MaDuan) 
                ? _context.duans.FirstOrDefault(d => d.MaDuan == Phieunhapkho.MaDuan) 
                : null;
            
            Console.WriteLine($"[QLDA DEBUG] Duan: {(duan != null ? $"MaDuan={duan.MaDuan}, MaNguoiQLDA={duan.MaNguoiQLDA}" : "null")}");
            Console.WriteLine($"[QLDA DEBUG] maNv2 == duan.MaNguoiQLDA: {duan != null && duan.MaNguoiQLDA == maNv2}");

            if (submitAction == "approve")
            {
                // Workflow duyệt:
                // 1. "Chờ quản lý dự án duyệt" -> Quản lý dự án duyệt -> "Chờ Giám đốc duyệt"
                // 2. "Quản lí dự án" (nếu có dự án) -> Trưởng dự án duyệt -> "Giám đốc"
                // 3. "Giám đốc" -> Giám đốc duyệt -> "Chờ nhập kho"
                // 4. "Chờ nhập kho" -> Kho xử lý -> "Đã nhập kho" và cộng vào kho tổng

                if (Phieunhapkho.TrangThai == "Chờ quản lý dự án duyệt" || 
                    Phieunhapkho.TrangThai?.Trim().Equals("Chờ quản lý dự án duyệt", StringComparison.OrdinalIgnoreCase) == true)
                {
                    Console.WriteLine("[QLDA DEBUG] Trạng thái là 'Chờ quản lý dự án duyệt'");
                    // Quản lý dự án duyệt
                    if (duan != null && duan.MaNguoiQLDA == maNv2)
                    {
                        Console.WriteLine("[QLDA DEBUG] Điều kiện QLDA thỏa mãn - Chuyển trạng thái sang 'Chờ Giám đốc duyệt'");
                        Phieunhapkho.TrangThai = "Chờ Giám đốc duyệt";
                        
                        // Lưu thời gian duyệt vào yeucau và vtyeucau
                        var ngayDuyet = DateTime.Now;
                        if (!string.IsNullOrEmpty(Phieunhapkho.MaYeucau))
                        {
                            var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == Phieunhapkho.MaYeucau);
                            if (yeucau != null)
                            {
                                // Cập nhật NgayDuyet cho các vật tư trong yêu cầu
                                var vtYeucauList = _context.vtyeucau
                                    .Where(vt => vt.VTMaYeucau == Phieunhapkho.MaYeucau)
                                    .ToList();
                                foreach (var vtYc in vtYeucauList)
                                {
                                    if (!vtYc.NgayDuyet.HasValue)
                                    {
                                        vtYc.NgayDuyet = ngayDuyet;
                                        _context.vtyeucau.Update(vtYc);
                                    }
                                }
                            }
                        }
                        
                        foreach (var vt in VTPhieunhapkholist)
                        {
                            vt.TrangThai = "Chờ Giám đốc duyệt";
                            _context.vtphieunhapkho.Update(vt);
                        }
                    }
                    // Trưởng BP kho duyệt
                    else if (chucVu2 == "Trưởng BP" && boPhan2 == "BP kho")
                    {
                        Console.WriteLine("[QLDA DEBUG] Điều kiện Trưởng BP kho thỏa mãn - Chuyển trạng thái sang 'Chờ Giám đốc duyệt'");
                        Phieunhapkho.TrangThai = "Chờ Giám đốc duyệt";
                        
                        // Lưu thời gian duyệt vào yeucau và vtyeucau
                        var ngayDuyet = DateTime.Now;
                        if (!string.IsNullOrEmpty(Phieunhapkho.MaYeucau))
                        {
                            var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == Phieunhapkho.MaYeucau);
                            if (yeucau != null)
                            {
                                // Cập nhật NgayDuyet cho các vật tư trong yêu cầu
                                var vtYeucauList = _context.vtyeucau
                                    .Where(vt => vt.VTMaYeucau == Phieunhapkho.MaYeucau)
                                    .ToList();
                                foreach (var vtYc in vtYeucauList)
                                {
                                    if (!vtYc.NgayDuyet.HasValue)
                                    {
                                        vtYc.NgayDuyet = ngayDuyet;
                                        _context.vtyeucau.Update(vtYc);
                                    }
                                }
                            }
                        }
                        
                        foreach (var vt in VTPhieunhapkholist)
                        {
                            vt.TrangThai = "Chờ Giám đốc duyệt";
                            _context.vtphieunhapkho.Update(vt);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"[QLDA DEBUG] Điều kiện QLDA KHÔNG thỏa mãn - duan != null: {duan != null}, MaNguoiQLDA == maNv2: {duan != null && duan.MaNguoiQLDA == maNv2}");
                    }
                }
                else if (Phieunhapkho.TrangThai == "Quản lí dự án")
                {
                    // Trưởng dự án duyệt
                    if (duan != null && duan.MaNguoiQLDA == maNv2)
                    {
                        Phieunhapkho.TrangThai = "Giám đốc";
                        foreach (var vt in VTPhieunhapkholist)
                        {
                            vt.TrangThai = "Giám đốc";
                            _context.vtphieunhapkho.Update(vt);
                        }
                    }
                }
                else if (Phieunhapkho.TrangThai == "Giám đốc" || 
                         Phieunhapkho.TrangThai == "Chờ Giám đốc duyệt" ||
                         Phieunhapkho.TrangThai?.Trim().Equals("Chờ Giám đốc duyệt", StringComparison.OrdinalIgnoreCase) == true)
                {
                    // Giám đốc duyệt
                    if (chucVu2 == "Giám đốc")
                    {
                        Phieunhapkho.TrangThai = "Chờ nhập kho";
                        
                        // Lưu thời gian duyệt vào yeucau và vtyeucau
                        var ngayDuyet = DateTime.Now;
                        if (!string.IsNullOrEmpty(Phieunhapkho.MaYeucau))
                        {
                            var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == Phieunhapkho.MaYeucau);
                            if (yeucau != null)
                            {
                                // Cập nhật NgayDuyet cho các vật tư trong yêu cầu
                                var vtYeucauList = _context.vtyeucau
                                    .Where(vt => vt.VTMaYeucau == Phieunhapkho.MaYeucau)
                                    .ToList();
                                foreach (var vtYc in vtYeucauList)
                                {
                                    if (!vtYc.NgayDuyet.HasValue)
                                    {
                                        vtYc.NgayDuyet = ngayDuyet;
                                        _context.vtyeucau.Update(vtYc);
                                    }
                                }
                            }
                        }
                        
                        foreach (var vt in VTPhieunhapkholist)
                        {
                            vt.TrangThai = "Chờ nhập kho";
                            _context.vtphieunhapkho.Update(vt);
                        }
                    }
                }
                else if (Phieunhapkho.TrangThai == "Chờ nhập kho" && boPhan2 == "BP kho")
                {
                    // Kho xử lý nhập kho
                    // LƯU Ý QUAN TRỌNG: Khi kho duyệt, CHỈ cộng vào kho tổng
                    // KHÔNG trừ từ kho dự án/cá nhân ở đây
                    // Chỉ trừ khi người nhận xác nhận nhận hàng (trạng thái "Đã xác nhận nhận hàng")
                    Phieunhapkho.TrangThai = "Đã nhập kho";
                    
                    foreach (var VTPhieunhapkho in VTPhieunhapkholist)
                    {
                        // Cộng vào kho tổng (cho cả phiếu từ mua hàng và phiếu từ dự án/cá nhân)
                        var khotong = _context.khotongs.FirstOrDefault(k => 
                            k.TenSanpham == VTPhieunhapkho.TenSanpham && 
                            k.MaSanpham == VTPhieunhapkho.MaSanpham && 
                            k.HangSX == VTPhieunhapkho.HangSX &&
                            k.Makho == VTPhieunhapkho.Makho);
                            
                        if (khotong != null)
                        {
                            // Cộng số lượng vào tồn kho
                            khotong.SL += VTPhieunhapkho.SL ?? 0;
                            _context.khotongs.Update(khotong);
                        }
                        else
                        {
                            // Tạo mới vật tư trong tồn kho nếu chưa có
                            var newKhotong = new khotongs
                            {
                                TenSanpham = VTPhieunhapkho.TenSanpham,
                                MaSanpham = VTPhieunhapkho.MaSanpham,
                                HangSX = VTPhieunhapkho.HangSX,
                                NhaCC = VTPhieunhapkho.NhaCC,
                                SL = VTPhieunhapkho.SL ?? 0,
                                DonVi = VTPhieunhapkho.DonVi,
                                Makho = VTPhieunhapkho.Makho,
                                NgayNhapkho = DateTime.Now,
                                TrangThai = "Tồn kho"
                            };
                            _context.khotongs.Add(newKhotong);
                        }
                        
                        VTPhieunhapkho.TrangThai = "Đã nhập kho";
                        _context.vtphieunhapkho.Update(VTPhieunhapkho);
                    }
                    
                    // Cập nhật trạng thái yeucau cho yêu cầu nhập kho
                    if (!string.IsNullOrEmpty(Phieunhapkho.MaYeucau))
                    {
                        var yeuCau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == Phieunhapkho.MaYeucau);
                        if (yeuCau != null)
                        {
                            // Kiểm tra xem có phải là yêu cầu nhập kho không (NHAPKHO_CANHAN, NHAPKHO_DUAN)
                            bool isNhapKhoRequest = Phieunhapkho.MaYeucau.StartsWith("NHAPKHO_CANHAN_", StringComparison.OrdinalIgnoreCase) ||
                                                     Phieunhapkho.MaYeucau.StartsWith("NHAPKHO_DUAN_", StringComparison.OrdinalIgnoreCase);
                            
                            if (isNhapKhoRequest)
                            {
                                // Đối với yêu cầu nhập kho, khi kho duyệt thì trạng thái là "Đã nhập kho"
                                yeuCau.TrangThai = "Đã nhập kho";
                                _context.yeucau.Update(yeuCau);
                            }
                        }
                    }
                    
                    // Tự động tạo phiếu xuất kho nếu có yêu cầu ban đầu và chưa có phiếu xuất kho
                    // Logic này áp dụng cho CẢ vật tư dự án VÀ vật tư cá nhân
                    bool isNhapkhoReturnFlow = !string.IsNullOrEmpty(Phieunhapkho.MaYeucau) &&
                        Phieunhapkho.MaYeucau.StartsWith("NHAPKHO_", StringComparison.OrdinalIgnoreCase);

                    if (!string.IsNullOrEmpty(Phieunhapkho.MaYeucau) && !isNhapkhoReturnFlow)
                    {
                        // Kiểm tra xem đã có phiếu xuất kho cho yêu cầu này chưa
                        var existingPhieuxuatkho = _context.phieuxuatkho
                            .FirstOrDefault(px => px.MaYeucau == Phieunhapkho.MaYeucau);
                        
                        // Kiểm tra nếu phiếu đã nhập kho (hoàn thành) thì tạo phiếu mới
                        var trangThaiHoanThanh = new[] { "Hoàn thành", "Đã xác nhận nhận hàng", "Đã xuất kho", "Đã nhập kho" };
                        bool existingPhieuDaNhapKho = existingPhieuxuatkho != null && 
                            trangThaiHoanThanh.Contains(existingPhieuxuatkho.TrangThai ?? "");
                        
                        // Tạo phiếu mới nếu chưa có phiếu hoặc phiếu cũ đã nhập kho
                        if (existingPhieuxuatkho == null || existingPhieuDaNhapKho)
                        {
                            // Lấy thông tin yêu cầu ban đầu
                            var yeucauBanDau = _context.yeucau
                                .FirstOrDefault(y => y.MaYeucau == Phieunhapkho.MaYeucau);
                            
                            if (yeucauBanDau != null)
                            {
                                // Tạo mã phiếu xuất kho duy nhất
                                int STT = 0;
                                string MaXuatkho;
                                while (true)
                                {
                                    MaXuatkho = $"PXK{STT}";
                                    var existingEntry = _context.phieuxuatkho
                                        .FirstOrDefault(y => y.MaXuatkho == MaXuatkho);
                                    if (existingEntry == null)
                                    {
                                        break;
                                    }
                                    STT++;
                                }
                                
                                // Tạo phiếu xuất kho
                                var newPhieuxuatkho = new phieuxuatkho
                                {
                                    MaXuatkho = MaXuatkho,
                                    MaYeucau = Phieunhapkho.MaYeucau,
                                    MaDuan = Phieunhapkho.MaDuan,
                                    MaNguoidung = Phieunhapkho.MaNguoidung,
                                    NgayXuatkho = DateTime.Now,
                                    TrangThai = "Chờ xác nhận"
                                };
                                _context.phieuxuatkho.Add(newPhieuxuatkho);
                                _context.SaveChanges();
                                
                                // Lấy danh sách vật tư yêu cầu ban đầu
                                var danhSachVatTuYC = _context.vtyeucau
                                    .Where(vt => vt.VTMaYeucau == Phieunhapkho.MaYeucau)
                                    .ToList();
                                
                                // Tạo vật tư trong phiếu xuất kho dựa trên vật tư trong phiếu nhập kho
                                foreach (var VTPhieunhapkho in VTPhieunhapkholist)
                                {
                                    // Tìm vật tư tương ứng trong yêu cầu ban đầu
                                    var vtYeucau = danhSachVatTuYC.FirstOrDefault(vt => 
                                        vt.MaSanpham == VTPhieunhapkho.MaSanpham &&
                                        vt.YCMakho == VTPhieunhapkho.Makho);
                                    
                                    if (vtYeucau != null)
                                    {
                                        // Lấy thông tin từ kho tổng để đảm bảo đúng thông tin
                                        var khotong = _context.khotongs.FirstOrDefault(k => 
                                            k.MaSanpham == VTPhieunhapkho.MaSanpham &&
                                            k.Makho == VTPhieunhapkho.Makho);
                                        
                                        if (khotong != null)
                                        {
                                            // Tính số lượng xuất kho (lấy số lượng nhỏ nhất giữa yêu cầu và số lượng nhập)
                                            int slXuat = Math.Min(vtYeucau.SL ?? 0, VTPhieunhapkho.SL ?? 0);
                                            
                                            var newVTPhieuxuatkho = new vtphieuxuatkho
                                            {
                                                MaXuatkho = MaXuatkho,
                                                MaYeucau = VTPhieunhapkho.MaYeucau,
                                                TenSanpham = khotong.TenSanpham,
                                                MaSanpham = khotong.MaSanpham,
                                                Makho = khotong.Makho,
                                                HangSX = khotong.HangSX,
                                                NhaCC = khotong.NhaCC,
                                                DonVi = khotong.DonVi,
                                                SL = slXuat,
                                                NgayBaohanh = khotong.NgayBaohanh,
                                                ThoiGianBH = khotong.ThoiGianBH,
                                                TrangThai = "Chờ xác nhận"
                                            };
                                            _context.vtphieuxuatkho.Add(newVTPhieuxuatkho);
                                        }
                                    }
                                }
                                
                                _context.SaveChanges();
                                
                                // Sau khi tạo phiếu xuất kho, kiểm tra tồn kho và tự động chuyển trạng thái như phiếu xuất kho cơ bản
                                var VTPhieuxuatkhoList = _context.vtphieuxuatkho
                                    .Where(vt => vt.MaXuatkho == MaXuatkho)
                                    .ToList();
                                
                                bool duHang = true;
                                var vatTuThieu = new List<vtphieuxuatkho>();
                                
                                foreach (var VTxuatkho in VTPhieuxuatkhoList)
                                {
                                    var khotong = _context.khotongs.FirstOrDefault(k => k.Makho == VTxuatkho.Makho && k.MaSanpham == VTxuatkho.MaSanpham);
                                    
                                    // Tính số lượng hàng đã cam kết (đã duyệt nhưng chưa giao)
                                    int soLuongDaCamKet = TinhSoLuongDaCamKet(VTxuatkho.Makho ?? "", VTxuatkho.MaSanpham ?? "", MaXuatkho);
                                    
                                    // Số lượng khả dụng = Tồn kho - Số lượng đã cam kết
                                    int soLuongKhaDung = (khotong?.SL ?? 0) - soLuongDaCamKet;
                                    
                                    // Kiểm tra chặt chẽ: không có hàng, số lượng khả dụng <= 0, hoặc không đủ số lượng → không cho xuất
                                    if (khotong == null || soLuongKhaDung <= 0 || soLuongKhaDung < VTxuatkho.SL)
                                    {
                                        duHang = false;
                                        vatTuThieu.Add(VTxuatkho);
                                    }
                                }
                                
                                if (duHang)
                                {
                                    // Đủ hàng → tự động chuyển sang "Đang chuẩn bị hàng" (vì hàng vừa nhập vào nên chắc chắn đủ)
                                    newPhieuxuatkho.TrangThai = "Đang chuẩn bị hàng";
                                    newPhieuxuatkho.NgayChuanBi = DateTime.Now;
                                    _context.phieuxuatkho.Update(newPhieuxuatkho);
                                    
                                    // Cập nhật trạng thái vật tư
                                    foreach (var VTxuatkho in VTPhieuxuatkhoList)
                                    {
                                        VTxuatkho.TrangThai = "Đang chuẩn bị hàng";
                                        _context.vtphieuxuatkho.Update(VTxuatkho);
                                    }
                                    
                                    _context.SaveChanges();
                                    Console.WriteLine($"Đã tự động tạo phiếu xuất kho {MaXuatkho} cho yêu cầu {Phieunhapkho.MaYeucau} và chuyển sang trạng thái 'Đang chuẩn bị hàng'");
                                }
                                else
                                {
                                    // Thiếu hàng (trường hợp này hiếm vì vừa nhập vào, nhưng để an toàn)
                                    newPhieuxuatkho.TrangThai = "Thiếu hàng";
                                    newPhieuxuatkho.GhiChu = "Không đủ số lượng tồn kho.";
                                    _context.phieuxuatkho.Update(newPhieuxuatkho);
                                    _context.SaveChanges();
                                    Console.WriteLine($"Đã tự động tạo phiếu xuất kho {MaXuatkho} cho yêu cầu {Phieunhapkho.MaYeucau} nhưng thiếu hàng");
                                }
                            }
                        }
                    }
                }
                else if (Phieunhapkho.TrangThai == "Đã nhập kho" && boPhan2 == "BP kho")
                {
                    // Khi trạng thái là "Đã nhập kho" và người nhận xác nhận nhận hàng
                    // MỚI trừ từ kho dự án/cá nhân (sản lượng thừa được trả lại)
                    Phieunhapkho.TrangThai = "Đã xác nhận nhận hàng";
                    
                    foreach (var VTPhieunhapkho in VTPhieunhapkholist)
                    {
                        // Trừ từ kho dự án/cá nhân khi người nhận xác nhận nhận hàng
                        bool isFromDuanOrCaNhan = false;
                        
                        // Kiểm tra từ dự án: Nếu có MaDuan và có vật tư trong kho dự án
                        if (!string.IsNullOrEmpty(Phieunhapkho.MaDuan) && !string.IsNullOrEmpty(VTPhieunhapkho.MaSanpham))
                        {
                            var vtXuatKhoItems = (from vt in _context.vtphieuxuatkho
                                                  join px in _context.phieuxuatkho on vt.MaXuatkho equals px.MaXuatkho
                                                  where px.MaDuan == Phieunhapkho.MaDuan
                                                     && vt.MaSanpham == VTPhieunhapkho.MaSanpham
                                                     && (vt.TrangThai == "Đã xác nhận nhận hàng" 
                                                         || vt.TrangThai == "Đã lấy hàng"
                                                         || vt.TrangThai == "Đã xuất kho")
                                                     && (vt.SL ?? 0) > 0
                                                  orderby vt.ID ascending
                                                  select vt)
                                                  .ToList();
                            
                            if (vtXuatKhoItems.Any())
                            {
                                isFromDuanOrCaNhan = true;
                                // Trừ từ kho dự án
                                int slCanTra = VTPhieunhapkho.SL ?? 0;
                                foreach (var vtItem in vtXuatKhoItems)
                                {
                                    if (slCanTra <= 0) break;
                                    
                                    int slHienTai = vtItem.SL ?? 0;
                                    int slTru = Math.Min(slHienTai, slCanTra);
                                    vtItem.SL = slHienTai - slTru;
                                    
                                    if ((vtItem.SL ?? 0) <= 0)
                                    {
                                        vtItem.TrangThai = "Đã trả kho";
                                    }
                                    
                                    _context.vtphieuxuatkho.Update(vtItem);
                                    slCanTra -= slTru;
                                }
                            }
                        }
                        
                        // Kiểm tra từ cá nhân: Nếu không có MaDuan và có vật tư trong kho cá nhân
                        if (!isFromDuanOrCaNhan && string.IsNullOrEmpty(Phieunhapkho.MaDuan) && !string.IsNullOrEmpty(VTPhieunhapkho.MaSanpham) && !string.IsNullOrEmpty(Phieunhapkho.MaNguoidung))
                        {
                            var khoCaNhanItem = _context.khonguoidungs
                                .FirstOrDefault(k => k.NDMaNguoidung == Phieunhapkho.MaNguoidung 
                                                   && k.MaSanpham == VTPhieunhapkho.MaSanpham 
                                                   && (k.TrangThai == "Đang mượn" || k.TrangThai == "Đang sử dụng")
                                                   && (k.SL ?? 0) >= (VTPhieunhapkho.SL ?? 0));
                            
                            if (khoCaNhanItem != null)
                            {
                                isFromDuanOrCaNhan = true;
                                // Trừ từ kho cá nhân
                                khoCaNhanItem.SL -= VTPhieunhapkho.SL ?? 0;
                                if (khoCaNhanItem.SL <= 0)
                                {
                                    khoCaNhanItem.TrangThai = "Đã trả";
                                }
                                _context.khonguoidungs.Update(khoCaNhanItem);
                            }
                        }
                        
                        VTPhieunhapkho.TrangThai = "Đã xác nhận nhận hàng";
                        _context.vtphieunhapkho.Update(VTPhieunhapkho);
                    }
                }
                else if (Phieunhapkho.TrangThai == "Đã xác nhận nhận hàng")
                {
                    // Hoàn thành phiếu nhập kho
                    Phieunhapkho.TrangThai = "Hoàn thành";
                    
                    foreach (var VTPhieunhapkho in VTPhieunhapkholist)
                    {
                        VTPhieunhapkho.TrangThai = "Hoàn thành";
                        _context.vtphieunhapkho.Update(VTPhieunhapkho);
                    }
                }

                _context.phieunhapkho.Update(Phieunhapkho);
            }
            else if (submitAction == "reject")
            {
                Phieunhapkho.TrangThai = $"{chucVu2} - Đã từ chối";
                foreach (var vt in VTPhieunhapkholist)
                {
                    vt.TrangThai = $"{chucVu2} - Đã từ chối";
                    _context.vtphieunhapkho.Update(vt);
                }
                _context.phieunhapkho.Update(Phieunhapkho);
            }
            
            _context.SaveChanges();
            return RedirectToAction("Phieunhapkho", "Yeucau", new { area = "QuanLiDuAn" });
        }

        [HttpPost]
        public IActionResult Taophieuxuatkhobyphieunhapkho(string MaNhapkho, phieuxuatkho phieuxuatkho, vtphieuxuatkho vtphieuxuatkho, phieunhapkho phieunhapkho, vtphieunhapkho vtphieunhapkho)
        {
            var Phieunhapkho = _context.phieunhapkho.FirstOrDefault(p => p.MaNhapkho == MaNhapkho);
            var VTPhieunhapkholist = _context.vtphieunhapkho.Where(vt => vt.MaNhapkho == MaNhapkho).ToList();

            int STT = 0;
            string MaXuatkho;
            // Tạo mã phiếu nhập kho duy nhất
            while (true)
            {
                MaXuatkho = $"PXK{STT}";
                var existingEntry = _context.phieuxuatkho
                                           .FirstOrDefault(y => y.MaXuatkho == MaXuatkho);

                if (existingEntry == null)
                {
                    break;
                }
                STT++;
            }

            var newphieuxuatkho = new phieuxuatkho
            {
                MaXuatkho = MaXuatkho,
                MaYeucau = Phieunhapkho.MaYeucau,
                MaDuan = Phieunhapkho.MaDuan,
                MaNguoidung = Phieunhapkho.MaNguoidung,
                NgayXuatkho = DateTime.Now,
                TrangThai = "Đang chuẩn bị hàng"
            };
            _context.phieuxuatkho.Add(newphieuxuatkho);
            _context.SaveChanges();

            foreach (var VTPhieunhapkho in VTPhieunhapkholist)
            {
                var newvtphieuxuatkho = new vtphieuxuatkho
                {
                    MaXuatkho = MaXuatkho,
                    MaYeucau = VTPhieunhapkho.MaYeucau,
                    TenSanpham = VTPhieunhapkho.TenSanpham,
                    MaSanpham = VTPhieunhapkho.MaSanpham,
                    Makho = VTPhieunhapkho.Makho,
                    HangSX = VTPhieunhapkho.HangSX,
                    NhaCC = VTPhieunhapkho.NhaCC,
                    SL = VTPhieunhapkho.SL,
                    DonVi = VTPhieunhapkho.DonVi,
                    TrangThai = "Đang chuẩn bị hàng",
                };
                _context.vtphieuxuatkho.Add(newvtphieuxuatkho);
            }
            _context.SaveChanges();

            return RedirectToAction("Phieunhapkho", "Yeucau", new { area = "QuanLiDuAn" });
        }

        [HttpPost]
        public IActionResult Xulituchoiyeucau(
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

            var refererUrl = HttpContext.Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(refererUrl))
            {
                return Redirect(refererUrl);
            }

            return RedirectToAction("Yeucau", "Yeucau", new { area = "QuanLiDuAn" });
        }

        public IActionResult XacnhanNhanHang()
        {
            var currentUserId = HttpContext.Session.GetString("MaNguoidung");

            // Lấy các yêu cầu mà kỹ thuật viên này đã tạo
            var yeuCauList = _context.yeucau
                .Where(y => y.YCMaNguoidung == currentUserId)
                .Select(y => y.MaYeucau)
                .ToList();

            // Lấy phiếu xuất kho liên quan tới các yêu cầu đó
            // Hiển thị cả phiếu đang chờ xác nhận và đã xác nhận
            var PhieuxuatkhoList = _context.phieuxuatkho
                .Where(p => yeuCauList.Contains(p.MaYeucau)
                         && (p.TrangThai == "Chờ người yêu cầu xác nhận" 
                             || p.TrangThai == "Đã xác nhận nhận hàng"))
                .OrderByDescending(p => p.NgayXuatkho)
                .ToList();

            var VTphieuxuatkhoList = _context.vtphieuxuatkho.ToList();

            var model = new Phieuxuatkhoviewmodel
            {
                Phieuxuatkho = PhieuxuatkhoList,
                VTphieuxuatkho = VTphieuxuatkhoList,
            };

            return View(model);
        }

        // Helper method: Tính số lượng hàng đã cam kết (committed) từ các phiếu xuất đã duyệt nhưng chưa giao
        // Các trạng thái được tính: "Đang chuẩn bị hàng", "Chờ người yêu cầu xác nhận"
        // LƯU Ý: "Đã xác nhận nhận hàng" KHÔNG tính vì đã trừ kho rồi
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

        // XÁC NHẬN HÀNG
        [HttpPost]
        public IActionResult XacnhanNhanHang(string MaXuatkho)
        {
            var phieu = _context.phieuxuatkho.FirstOrDefault(p => p.MaXuatkho == MaXuatkho);

            if (phieu != null && phieu.TrangThai == "Chờ người yêu cầu xác nhận")
            {
                phieu.TrangThai = "Đã xác nhận nhận hàng";
                phieu.NgayXacNhanNhan = DateTime.Now;
                _context.phieuxuatkho.Update(phieu);

                // ✅ Cập nhật trạng thái vật tư trong phiếu xuất kho
                var VTphieuxuatkhoList = _context.vtphieuxuatkho
                    .Where(vt => vt.MaXuatkho == MaXuatkho)
                    .ToList();

                foreach (var vt in VTphieuxuatkhoList)
                {
                    // Bỏ qua các dòng đã được xác nhận hoặc đã xuất kho trước đó
                    if (vt.TrangThai == "Đã xác nhận nhận hàng" || vt.TrangThai == "Đã xuất kho")
                    {
                        continue;
                    }

                    // Cập nhật trạng thái vật tư thành "Đã xác nhận nhận hàng"
                    vt.TrangThai = "Đã xác nhận nhận hàng";
                    vt.NgayNhapkho = DateTime.Now;
                    _context.vtphieuxuatkho.Update(vt);
                    
                    // Trừ kho tổng khi xác nhận nhận hàng - KIỂM TRA CHẶT CHẼ SỐ LƯỢNG
                    // Chỉ xử lý nếu số lượng yêu cầu > 0
                    if ((vt.SL ?? 0) > 0)
                    {
                        var khotong = _context.khotongs.FirstOrDefault(k => k.Makho == vt.Makho && k.MaSanpham == vt.MaSanpham);
                        if (khotong != null)
                        {
                            // Tính số lượng hàng đã cam kết từ các phiếu xuất khác (loại trừ phiếu hiện tại vì nó đang được xác nhận)
                            int soLuongDaCamKetKhac = TinhSoLuongDaCamKet(vt.Makho ?? "", vt.MaSanpham ?? "", MaXuatkho);
                            
                            // Số lượng khả dụng = Tồn kho - Số lượng đã cam kết từ các phiếu khác
                            int soLuongKhaDung = (khotong.SL ?? 0) - soLuongDaCamKetKhac;
                            
                            // TUYỆT ĐỐI KHÔNG cho phép xuất nếu hết hàng hoặc không đủ số lượng
                            if (soLuongKhaDung <= 0 || soLuongKhaDung < vt.SL)
                            {
                                TempData["ErrorMessage"] = $"Không thể xuất kho: Vật tư {vt.TenSanpham} không đủ số lượng trong kho (Tồn kho: {khotong.SL}, Đã cam kết: {soLuongDaCamKetKhac}, Khả dụng: {soLuongKhaDung}, Yêu cầu: {vt.SL})";
                                return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "QuanLiDuAn" });
                            }
                            
                            khotong.SL -= vt.SL;
                            _context.khotongs.Update(khotong);
                        }
                        else
                        {
                            TempData["ErrorMessage"] = $"Không thể xuất kho: Vật tư {vt.TenSanpham} không tồn tại trong kho";
                            return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "QuanLiDuAn" });
                        }
                    }
                    
                    // chỉ xử lý nếu phiếu này không có dự án
                    if (string.IsNullOrEmpty(phieu.MaDuan))
                    {
                        var existingItem = _context.khonguoidungs
                            .FirstOrDefault(k => k.NDMaNguoidung == phieu.MaNguoidung && k.MaSanpham == vt.MaSanpham);

                        if (existingItem != null)
                        {
                            existingItem.SL += vt.SL;
                            _context.khonguoidungs.Update(existingItem);
                        }
                        else
                        {
                            var newItem = new khonguoidungs
                            {
                                NDMaNguoidung = phieu.MaNguoidung,
                                TenSanpham = vt.TenSanpham,
                                MaSanpham = vt.MaSanpham,
                                NDMakho = vt.Makho,
                                HangSX = vt.HangSX,
                                NhaCC = vt.NhaCC,
                                DonVi = vt.DonVi,
                                SL = vt.SL,
                                NgayBaohanh = vt.NgayBaohanh,
                                ThoiGianBH = vt.ThoiGianBH,
                                TrangThai = "Đang mượn",
                                NgayNhapkho = DateTime.Now
                            };
                            _context.khonguoidungs.Add(newItem);
                        }
                    }
                }

                _context.SaveChanges();

                TempData["SuccessMessage"] = "Xác nhận nhận hàng thành công!";
                return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "QuanLiDuAn" });
            }

            TempData["ErrorMessage"] = "Phiếu không hợp lệ hoặc đã được xác nhận!";
            return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "QuanLiDuAn" });
        }

        // Đồng bộ lại trạng thái vật tư cho các phiếu đã xác nhận nhận hàng
        [HttpPost]
        public IActionResult DongsBoTrangThaiVatTu(string MaXuatkho)
        {
            try
            {
                var phieu = _context.phieuxuatkho.FirstOrDefault(p => p.MaXuatkho == MaXuatkho);
                
                if (phieu != null && phieu.TrangThai == "Đã xác nhận nhận hàng")
                {
                    var VTphieuxuatkhoList = _context.vtphieuxuatkho
                        .Where(vt => vt.MaXuatkho == MaXuatkho)
                        .ToList();

                    foreach (var vt in VTphieuxuatkhoList)
                    {
                        // Chỉ cập nhật nếu trạng thái chưa đúng
                        if (vt.TrangThai != "Đã xác nhận nhận hàng" && vt.TrangThai != "Đã xuất kho")
                        {
                            vt.TrangThai = "Đã xác nhận nhận hàng";
                            _context.vtphieuxuatkho.Update(vt);
                        }
                    }

                    _context.SaveChanges();
                    return Json(new { success = true, message = "Đã đồng bộ trạng thái vật tư!" });
                }

                return Json(new { success = false, message = "Phiếu không hợp lệ!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        // Action tự động đồng bộ khi load trang (gọi từ JavaScript)
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

                // Lấy các phiếu đã xác nhận nhận hàng
                var phieuxuatkhoList = _context.phieuxuatkho
                    .Where(p => yeuCauList.Contains(p.MaYeucau)
                             && p.TrangThai == "Đã xác nhận nhận hàng")
                    .ToList();

                int updatedCount = 0;
                foreach (var phieu in phieuxuatkhoList)
                {
                    var VTphieuxuatkhoList = _context.vtphieuxuatkho
                        .Where(vt => vt.MaXuatkho == phieu.MaXuatkho
                                 && vt.TrangThai != "Đã xác nhận nhận hàng"
                                 && vt.TrangThai != "Đã xuất kho")
                        .ToList();

                    foreach (var vt in VTphieuxuatkhoList)
                    {
                        vt.TrangThai = "Đã xác nhận nhận hàng";
                        _context.vtphieuxuatkho.Update(vt);
                        updatedCount++;
                    }
                }

                _context.SaveChanges();
                return Json(new { success = true, message = $"Đã đồng bộ {updatedCount} vật tư!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
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


    }
}
