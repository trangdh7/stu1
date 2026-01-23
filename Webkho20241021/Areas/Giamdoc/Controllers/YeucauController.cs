using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Webkho_20241021.Areas.Giamdoc.Data;
using Webkho_20241021.Models;
using Webkho_20241021.Services;
using OfficeOpenXml;
using Microsoft.Extensions.DependencyInjection;


namespace Webkho_20241021.Areas.Giamdoc.Controllers
{
    [Area("Giamdoc")]
    [Authorize(Roles = "Giám đốc")]
    public class YeucauController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailService _emailService;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        public YeucauController(ApplicationDbContext context, EmailService emailService, IServiceScopeFactory serviceScopeFactory)
        {
            _context = context;
            _emailService = emailService;
            _serviceScopeFactory = serviceScopeFactory;
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
                        System.Diagnostics.Debug.WriteLine($"[Giamdoc] Bắt đầu gửi email từ chối cho {maYeucau}");
                        await emailService.SendNotificationToRequesterOnRejectionAsync(maYeucau, ghiChu);
                        System.Diagnostics.Debug.WriteLine($"[Giamdoc] Đã gửi email từ chối cho {maYeucau}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Giamdoc] Lỗi gửi email từ chối cho {maYeucau}: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[Giamdoc] Stack trace: {ex.StackTrace}");
                }
            });
        }
        public IActionResult Yeucau(string search = "")
        {
            var userRole = HttpContext.Session.GetString("Chucvu");

            var Yeucaulist = _context.yeucau.ToList();
            // Lấy toàn bộ vật tư yêu cầu; lọc trạng thái sẽ do helper xử lý (bao gồm cả trường hợp SLMoi = 0 nhưng đã nhập/xuất)
            var VTyeucaulist = _context.vtyeucau.ToList();
            var PhieuMuaHangList = _context.phieumuahang.ToList();

            foreach (var yeucau in Yeucaulist)
            {
                // Lấy danh sách vật tư của yêu cầu này
                var vtList = VTyeucaulist.Where(vt => vt.VTMaYeucau == yeucau.MaYeucau).ToList();

                // Logic kiểm tra phiếu mua hàng
                var phieus = PhieuMuaHangList.Where(p => p.MaYeucau == yeucau.MaYeucau).ToList();

                // Logic kiểm tra phiếu mua hàng (ưu tiên cao nhất)
                if (phieus.Any(p => p.TrangThai != "Đã nhận hàng"))
                {
                    yeucau.TrangThai = "Đang mua hàng";
                    _context.yeucau.Update(yeucau);
                    continue; // Bỏ qua các kiểm tra khác nếu đang mua hàng
                }

                // Đồng bộ trạng thái từ helper dựa trên vtyeucau
                if (vtList.Any())
                {
                    yeucau.TrangThai = YeucauUpdateHelper.TinhTrangThaiYeuCau(vtList);
                    _context.yeucau.Update(yeucau);
                }
                else if (!string.IsNullOrEmpty(yeucau.MaYeucau) && yeucau.MaYeucau.StartsWith("NHAPKHO_", StringComparison.OrdinalIgnoreCase))
                {
                    var vtNhapKhoList = _context.vtphieunhapkho
                        .Where(vt => vt.MaYeucau == yeucau.MaYeucau)
                        .ToList();

                    if (vtNhapKhoList.Any())
                    {
                        yeucau.TrangThai = YeucauUpdateHelper.TinhTrangThaiNhapKhoTuChiTiet(vtNhapKhoList);
                        _context.yeucau.Update(yeucau);
                    }
                }
            }

            _context.SaveChanges();

            // Populate TenYeucau and Bophan từ nguoidungs nếu chưa có
            var nguoiDungDict = _context.nguoidungs.ToDictionary(n => n.MaNguoidung, n => new { n.TenNguoidung, n.Bophan });
            foreach (var yeucau in Yeucaulist)
            {
                // Populate Bophan từ nguoidungs nếu chưa có
                if (string.IsNullOrWhiteSpace(yeucau.Bophan) && !string.IsNullOrWhiteSpace(yeucau.YCMaNguoidung))
                {
                    if (nguoiDungDict.TryGetValue(yeucau.YCMaNguoidung, out var nguoiDung))
                    {
                        yeucau.Bophan = nguoiDung.Bophan ?? "";
                    }
                }

                // Populate TenYeucau nếu chưa có
                if (string.IsNullOrWhiteSpace(yeucau.TenYeucau))
                {
                    // Nếu là yêu cầu nhập kho đặc biệt (NHAPKHO_), set mặc định
                    if (!string.IsNullOrEmpty(yeucau.MaYeucau) && yeucau.MaYeucau.StartsWith("NHAPKHO_"))
                    {
                        yeucau.TenYeucau = "Yêu cầu nhập kho";
                    }
                    else
                    {
                        // Các yêu cầu khác, set mặc định là "Yêu cầu vật tư"
                        yeucau.TenYeucau = "Yêu cầu vật tư";
                    }
                }
            }

            // Sắp xếp yêu cầu: đưa những yêu cầu đang Chờ giám đốc duyệt lên đầu để xử lý trước
            var SortedYeucaulist = Yeucaulist
                .OrderByDescending(y => 
                {
                    var trangThai = (y.TrangThai ?? "").Trim();
                    // Kiểm tra xem trạng thái có chứa "giám đốc duyệt" hoặc "Giám đốc" (không phân biệt hoa thường)
                    bool isChoGiamDocDuyet = trangThai.IndexOf("giám đốc duyệt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                             trangThai == "Giám đốc";
                    // Trả về 1 nếu cần duyệt, 0 nếu không - OrderByDescending sẽ đưa 1 lên đầu
                    return isChoGiamDocDuyet ? 1 : 0;
                })
                .ThenByDescending(y => y.NgayYeucau)
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
            // Sắp xếp: đưa các phiếu có trạng thái "Chờ giám đốc duyệt" lên đầu
            var Phieunhapkholist = _context.phieunhapkho
                .OrderByDescending(y => y.TrangThai == "Chờ giám đốc duyệt")
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
            // Sắp xếp phiếu mua hàng: đưa những phiếu có trạng thái "Đã báo giá" lên đầu để giám đốc duyệt
            var Phieumuahanglist = _context.phieumuahang
            .OrderByDescending(y => y.TrangThai == "Đã báo giá")
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

            var chucVu = HttpContext.Session.GetString("Chucvu");
            var boPhan = HttpContext.Session.GetString("Bophan");
            var Phieumuahanglisttt = _context.phieumuahang.ToList();
            var VTphieumuahanglist = _context.vtphieumuahang.ToList();

            int Dangchobaogiacount = _context.phieumuahang.Count(p => p.TrangThai == "Đang chờ báo giá");
            int Dabaogiacount = _context.phieumuahang.Count(p => p.TrangThai == "Đã báo giá");
            int Chothanhtoancount = _context.phieumuahang.Count(p => p.TrangThai == "Chờ thanh toán");
            int Dathanhtoancount = _context.phieumuahang.Count(p => p.TrangThai == "Đã thanh toán");
            int thongbaocount = 0 ;
            if (boPhan == "BP mua hàng")
            {
                thongbaocount = Dangchobaogiacount;
            }
            else if (boPhan == "BP kỹ thuật" && chucVu == "Giám đốc")
            {
                thongbaocount = Dabaogiacount;
            }
            else if (boPhan == "BP kế toán")
            {
                thongbaocount = Chothanhtoancount;
            }
            var model = new Phieumuahangviewmodel
            {
                Phieumuahang = Phieumuahanglist,
                VTphieumuahang = VTphieumuahanglist,
                ThongbaomuahangCount = thongbaocount,
            };
            ViewBag.Search = search;
            return View(model);
        }

        [HttpGet]
        public IActionResult GetDulieuThongbao()
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
            else if (chucVu == "Giám đốc")
            {
                // Giám đốc xem các phiếu mua hàng cần duyệt (đã báo giá)
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
            else if (chucVu == "Giám đốc")
            {
                // Giám đốc xem tất cả phiếu xuất kho chưa hoàn thành
                // Chỉ đếm phiếu xuất kho, không đếm thêm vật tư "Chờ xuất kho" để tránh đếm trùng
                thongbaoxuatkhocount = _context.phieuxuatkho
                    .Count(p => p.TrangThai != "Hoàn thành");
            }

            // Nhập kho
            int thongbaonhapkhocount = 0;
            if (boPhan == "BP kho")
            {
                thongbaonhapkhocount = _context.phieunhapkho.Count(p => p.TrangThai == "Chờ nhập kho" || p.TrangThai == "Sẵn sàng nhập kho");
            }
            else if (chucVu == "Giám đốc")
            {
                // Giám đốc xem các phiếu nhập kho Chờ giám đốc duyệt
                thongbaonhapkhocount = _context.phieunhapkho.Count(p => p.TrangThai == "Chờ giám đốc duyệt");
            }

            var MaduanquanliMain = _context.duans
                .Where(da => da.MaNguoiQLDA == maNv)
                .Select(da => da.MaDuan)
                .ToList();

            int QLDAyeucaucount = _context.yeucau.Count(p => p.TrangThai == "Quản lí dự án" && MaduanquanliMain.Contains(p.YCMaDuan));
            int Duyetyeucaucount = _context.yeucau.Count(p => p.TrangThai == (chucVu + "-" + boPhan));

            // Yêu cầu ở bước Giám đốc: gồm cả trạng thái cũ "Giám đốc" và trạng thái mới "Chờ giám đốc duyệt"
            int Giamdocyeucaucount = _context.yeucau.Count(p =>
                p.TrangThai == "Giám đốc" ||
                p.TrangThai == "Chờ giám đốc duyệt");
            
           
            int Dangmuahangcount = 0;
            if (chucVu == "Giám đốc")
            {
                // Lấy danh sách yêu cầu có TrangThai == "Đang mua hàng"
                var yeucauDangmuahang = _context.yeucau.Where(p => p.TrangThai == "Đang mua hàng").ToList();
               
                foreach (var yc in yeucauDangmuahang)
                {
                    var phieus = _context.phieumuahang.Where(p => p.MaYeucau == yc.MaYeucau && p.TrangThai != "Đã nhận hàng").ToList();
                    if (phieus.Any())
                    {
                        Dangmuahangcount++;
                    }
                }
            }
            
            int thongbaoyeucaucount = 0;

            if (chucVu == "Giám đốc")
            {
                // Đếm cả yêu cầu "Giám đốc" và "Đang mua hàng" cần giám đốc xử lý
                thongbaoyeucaucount = Giamdocyeucaucount + Dangmuahangcount;
            }
            else if (Duyetyeucaucount != 0 || QLDAyeucaucount != 0)
            {
                thongbaoyeucaucount = Duyetyeucaucount + QLDAyeucaucount;
            }

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
            else if (chucVu == "Giám đốc")
            {
                // Giám đốc xem các phiếu mua hàng cần duyệt (đã báo giá)
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
                thongbaoxuatkhocount = _context.phieuxuatkho
                    .Count(p => p.TrangThai != "Hoàn thành");
            }
            else if (chucVu == "Giám đốc")
            {
                // Giám đốc xem tất cả phiếu xuất kho chưa hoàn thành
                // Chỉ đếm phiếu xuất kho, không đếm thêm vật tư "Chờ xuất kho" để tránh đếm trùng
                thongbaoxuatkhocount = _context.phieuxuatkho
                    .Count(p => p.TrangThai != "Hoàn thành");
            }
            

            int Hoanthanhnhapkhocount = _context.phieuxuatkho.Count(p => p.TrangThai != "Hoàn thành");
            int thongbaonhapkhocount = 0;
            if (boPhan == "BP kho")
            {
                thongbaonhapkhocount = Hoanthanhnhapkhocount;
            }
            else if (chucVu == "Giám đốc")
            {
                // Giám đốc xem các phiếu nhập kho Chờ giám đốc duyệt
                thongbaonhapkhocount = _context.phieunhapkho.Count(p => p.TrangThai == "Chờ giám đốc duyệt");
            }

            var MaduanquanliLayout = _context.duans
                .Where(da => da.MaNguoiQLDA == maNv)
                .Select(da => da.MaDuan)
                .ToList();

            int QLDAyeucaucount = _context.yeucau.Count(p => p.TrangThai == "Quản lí dự án" && MaduanquanliLayout.Contains(p.YCMaDuan));
            int Duyetyeucaucount = _context.yeucau.Count(p => p.TrangThai == (chucVu + "-" + boPhan));
            // Trạng thái bước Giám đốc: gồm cả "Giám đốc" và "Chờ giám đốc duyệt"
            int Giamdocyeucaucount = _context.yeucau.Count(p =>
                p.TrangThai == "Giám đốc" ||
                p.TrangThai == "Chờ giám đốc duyệt");
            
            // Đếm các yêu cầu "Đang mua hàng" mà giám đốc vẫn cần xử lý
            int Dangmuahangcount = 0;
            if (chucVu == "Giám đốc")
            {
                var yeucauDangmuahang = _context.yeucau.Where(p => p.TrangThai == "Đang mua hàng").ToList();
                foreach (var yc in yeucauDangmuahang)
                {
                    var phieus = _context.phieumuahang.Where(p => p.MaYeucau == yc.MaYeucau && p.TrangThai != "Đã nhận hàng").ToList();
                    if (phieus.Any())
                    {
                        Dangmuahangcount++;
                    }
                }
            }
            
            int thongbaoyeucaucount = 0;

            if (chucVu == "Giám đốc")
            {
               
                thongbaoyeucaucount = Giamdocyeucaucount + Dangmuahangcount + QLDAyeucaucount + Duyetyeucaucount;
            }
            else if (Duyetyeucaucount != 0 || QLDAyeucaucount != 0)
            {
                thongbaoyeucaucount = Duyetyeucaucount + QLDAyeucaucount;
            }

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
            else if (chucVu == "Giám đốc")
            {
                // Giám đốc xem các phiếu mua hàng cần duyệt (đã báo giá)
                thongbaomuahangcount = Dabaogiacount;
            }
            else if (boPhan == "BP kế toán")
            {
                thongbaomuahangcount = Chothanhtoancount;
            }

            int Hoanthanhxuatkhocount = _context.phieuxuatkho.Count(p => p.TrangThai != "Hoàn thành");
            int thongbaoxuatkhocount = 0;
            if (boPhan == "BP kho")
            {
                thongbaoxuatkhocount = Hoanthanhxuatkhocount;
            }
            else if (chucVu == "Giám đốc")
            {
                // Giám đốc xem tất cả phiếu xuất kho chưa hoàn thành
                // Chỉ đếm phiếu xuất kho, không đếm thêm vật tư "Chờ xuất kho" để tránh đếm trùng
                thongbaoxuatkhocount = _context.phieuxuatkho.Count(p => p.TrangThai != "Hoàn thành");
            }

            int Hoanthanhnhapkhocount = _context.phieuxuatkho.Count(p => p.TrangThai != "Hoàn thành");
            int thongbaonhapkhocount = 0;
            if (boPhan == "BP kho")
            {
                thongbaonhapkhocount = Hoanthanhnhapkhocount;
            }
            else if (chucVu == "Giám đốc")
            {
                // Giám đốc xem các phiếu nhập kho Chờ giám đốc duyệt
                thongbaonhapkhocount = _context.phieunhapkho.Count(p => p.TrangThai == "Chờ giám đốc duyệt");
            }

            var MaduanquanliTrangchu = _context.duans
                .Where(da => da.MaNguoiQLDA == maNv)
                .Select(da => da.MaDuan)
                .ToList();

            int QLDAyeucaucount = _context.yeucau.Count(p => p.TrangThai == "Quản lí dự án" && MaduanquanliTrangchu.Contains(p.YCMaDuan));
            int Duyetyeucaucount = _context.yeucau.Count(p => p.TrangThai == (chucVu + "-" + boPhan));
            // Yêu cầu ở bước Giám đốc: gồm cả trạng thái cũ "Giám đốc" và trạng thái mới "Chờ giám đốc duyệt"
            int Giamdocyeucaucount = _context.yeucau.Count(p =>
                p.TrangThai == "Giám đốc" ||
                p.TrangThai == "Chờ giám đốc duyệt");
            
            // Đếm các yêu cầu "Đang mua hàng" mà giám đốc vẫn cần xử lý
            int Dangmuahangcount = 0;
            if (chucVu == "Giám đốc")
            {
                var yeucauDangmuahang = _context.yeucau.Where(p => p.TrangThai == "Đang mua hàng").ToList();
                foreach (var yc in yeucauDangmuahang)
                {
                    var phieus = _context.phieumuahang.Where(p => p.MaYeucau == yc.MaYeucau && p.TrangThai != "Đã nhận hàng").ToList();
                    if (phieus.Any())
                    {
                        Dangmuahangcount++;
                    }
                }
            }
            
            int thongbaoyeucaucount = 0;

            if (chucVu == "Giám đốc")
            {
                // Đếm cả yêu cầu "Giám đốc" và "Đang mua hàng" cần giám đốc xử lý
                thongbaoyeucaucount = Giamdocyeucaucount + Dangmuahangcount;
            }
            else if (Duyetyeucaucount != 0 || QLDAyeucaucount != 0)
            {
                thongbaoyeucaucount = Duyetyeucaucount + QLDAyeucaucount;
            }

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
                                     ID = vtnk.ID,
                                     VTMaYeucau = MaYeucau,
                                     TenSanpham = vtnk.TenSanpham,
                                     MaSanpham = vtnk.MaSanpham,
                                     YCMakho = vtnk.Makho,
                                     HangSX = vtnk.HangSX,
                                     NhaCC = vtnk.NhaCC,
                                     // Đưa số lượng vào cả SL, SLMoi để JS có thể hiển thị đúng
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
            else
            {
                // Lấy dữ liệu từ vtyeucau như bình thường (không loại bỏ SLMoi = 0 để giữ các dòng đã nhập/xuất)
                var vatTuList = _context.vtyeucau
                                     .Where(v => v.VTMaYeucau == MaYeucau).ToList();
                
                // Ẩn NgayDuyet nếu vật tư chưa được giám đốc duyệt
                // Chỉ hiển thị NgayDuyet khi trạng thái là "Đã duyệt", "Đang mua hàng", "Chờ xuất kho", "Đã xuất kho", "Đã nhận hàng"
                var processedVatTuList = vatTuList.Select(v => new
                {
                    v.ID,
                    v.VTMaYeucau,
                    v.TenSanpham,
                    v.MaSanpham,
                    v.YCMakho,
                    v.HangSX,
                    v.NhaCC,
                    v.SL,
                    v.SLCu,
                    v.SLMoi,
                    v.DonVi,
                    v.NgayCanHang,
                    v.NgayNhapkho,
                    v.NgayBaohanh,
                    v.ThoiGianBH,
                    // Chỉ hiển thị NgayDuyet nếu đã được giám đốc duyệt
                    NgayDuyet = (v.TrangThai == "Đã duyệt" || 
                                v.TrangThai == "Đang mua hàng" || 
                                v.TrangThai == "Chờ xuất kho" ||
                                v.TrangThai == "Đã xuất kho" || 
                                v.TrangThai == "Đã nhận hàng") ? v.NgayDuyet : null,
                    v.TrangThai,
                    v.GhiChu
                }).ToList();
                
                return Json(processedVatTuList);
            }
        }

        [HttpGet]
        public IActionResult GetVTPhieuxuatkho(string MaXuatkho)
        {
            var PhieuxuatkhoList = _context.vtphieuxuatkho
                                 .Where(v => v.MaXuatkho == MaXuatkho).ToList();
            
            // Lấy thông tin phiếu xuất kho để lấy tên người yêu cầu và mã yêu cầu
            var phieuxuatkho = _context.phieuxuatkho
                .FirstOrDefault(p => p.MaXuatkho == MaXuatkho);
            
            string tenNguoiYeuCau = "";
            string maYeucau = "";
            if (phieuxuatkho != null && !string.IsNullOrEmpty(phieuxuatkho.MaNguoidung))
            {
                // Lưu lại mã yêu cầu để hiển thị giống màn Yeucau
                maYeucau = phieuxuatkho.MaYeucau ?? "";

                var nguoidung = _context.nguoidungs
                    .FirstOrDefault(n => n.MaNguoidung == phieuxuatkho.MaNguoidung);
                if (nguoidung != null)
                {
                    tenNguoiYeuCau = nguoidung.TenNguoidung ?? "";
                }
            }
            
            return Json(new
            {
                items = PhieuxuatkhoList,
                maXuatkho = MaXuatkho,
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
            if (phieunhapkho != null && !string.IsNullOrEmpty(phieunhapkho.MaNguoidung))
            {
                var nguoidung = _context.nguoidungs
                    .FirstOrDefault(n => n.MaNguoidung == phieunhapkho.MaNguoidung);
                if (nguoidung != null)
                {
                    tenNguoiYeuCau = nguoidung.TenNguoidung ?? "";
                }
            }
            
            return Json(new
            {
                items = PhieunhapkhoList,
                maNhapkho = MaNhapkho,
                tenNguoiYeuCau = tenNguoiYeuCau
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
            if (phieumuahang != null && !string.IsNullOrEmpty(phieumuahang.MaNguoidung))
            {
                var nguoidung = _context.nguoidungs
                    .FirstOrDefault(n => n.MaNguoidung == phieumuahang.MaNguoidung);
                if (nguoidung != null)
                {
                    tenNguoiYeuCau = nguoidung.TenNguoidung ?? "";
                }
            }
            
            return Json(new
            {
                items = PhieumuahangList,
                maMuahang = MaMuahang,
                tenNguoiYeuCau = tenNguoiYeuCau
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
                    // Lưu thời gian duyệt (FIFO - ai duyệt trước được ưu tiên)
                    vatTu.NgayDuyet = DateTime.Now;
                    vatTu.GhiChu = GhiChu; // Lưu ghi chú khi duyệt
                    
                    // Lấy yêu cầu và lưu thông tin người duyệt
                    var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
                    if (yeucau != null)
                    {
                        var maNguoiDuyet = HttpContext.Session.GetString("MaNguoidung");
                        if (!string.IsNullOrWhiteSpace(maNguoiDuyet))
                        {
                            yeucau.NguoiDuyet = maNguoiDuyet;
                            yeucau.NgayDuyet = DateTime.Now;
                            _context.yeucau.Update(yeucau);
                        }
                    }
                    
                    // ⚠️ KIỂM TRA: Nếu cùng dự án và cùng mã yêu cầu (base code), và số lượng mới bằng số lượng đã cấp → trạng thái "Hoàn thành"
                    if (yeucau != null && !string.IsNullOrWhiteSpace(yeucau.YCMaDuan) && !string.IsNullOrWhiteSpace(vatTu.MaSanpham))
                    {
                        // Tính base mã yêu cầu để kiểm tra
                        string maYeuCauChuan = NormalizeMaYeucauBase(MaYeucau);
                        
                        // Tính số lượng đã cấp thực tế từ các yêu cầu trước đó cùng base code
                        // Lấy tất cả mã yêu cầu cùng base code (trừ yêu cầu hiện tại)
                        var allRelatedMaYeucau = _context.yeucau
                            .Where(y => !string.IsNullOrWhiteSpace(y.MaYeucau) && 
                                       y.YCMaDuan == yeucau.YCMaDuan &&
                                       y.MaYeucau != MaYeucau) // Loại trừ yêu cầu hiện tại
                            .ToList()
                            .Where(y => string.Equals(
                                NormalizeMaYeucauBase(y.MaYeucau),
                                maYeuCauChuan,
                                StringComparison.OrdinalIgnoreCase))
                            .Select(y => y.MaYeucau)
                            .ToList();
                        
                        // Tính số lượng đã cấp từ các yêu cầu trước đó cùng base code
                        // Lấy số lượng từ vật tư yêu cầu đã được duyệt (có NgayDuyet - đã được giám đốc duyệt)
                        // Tính MAX số lượng của các vật tư cùng mã sản phẩm trong các yêu cầu trước đó
                        // ⭐ SỬA BUG: Loại trừ các yêu cầu và vật tư đã bị từ chối
                        int soLuongDaCapTuVTYeucau = 0;
                        if (allRelatedMaYeucau.Any())
                        {
                            // Lấy danh sách các mã yêu cầu đã bị từ chối
                            var maYeucauBiTuChoi = _context.yeucau
                                .Where(y => allRelatedMaYeucau.Contains(y.MaYeucau) &&
                                           !string.IsNullOrEmpty(y.TrangThai) &&
                                           y.TrangThai.Contains("Đã từ chối", StringComparison.OrdinalIgnoreCase))
                                .Select(y => y.MaYeucau)
                                .ToList();

                            // ⭐ CHỈ TÍNH TỪ CÁC VẬT TƯ ĐÃ CÓ TRẠNG THÁI "Đã xuất kho" (hoặc tương đương)
                            var trangThaiDaXuatKho = new[] { "Đã xuất kho", "Hoàn thành", "Đã lấy hàng" };
                            var vatTuYeuCauTruocDo = _context.vtyeucau
                                .Where(vt => allRelatedMaYeucau.Contains(vt.VTMaYeucau) &&
                                           vt.MaSanpham == vatTu.MaSanpham &&
                                           vt.NgayDuyet.HasValue && // Đã được duyệt
                                           !maYeucauBiTuChoi.Contains(vt.VTMaYeucau) && // ⭐ Loại trừ yêu cầu đã bị từ chối
                                           !string.IsNullOrEmpty(vt.TrangThai) &&
                                           !vt.TrangThai.Contains("Đã từ chối", StringComparison.OrdinalIgnoreCase) && // ⭐ Loại trừ vật tư đã bị từ chối
                                           trangThaiDaXuatKho.Contains(vt.TrangThai)) // ⭐ CHỈ tính từ vật tư đã xuất kho
                                .ToList();
                            
                            if (vatTuYeuCauTruocDo.Any())
                            {
                                // Lấy MAX số lượng từ các vật tư yêu cầu trước đó (theo logic MAX)
                                soLuongDaCapTuVTYeucau = vatTuYeuCauTruocDo
                                    .Select(vt => Math.Max(vt.SLMoi ?? 0, vt.SL ?? 0))
                                    .DefaultIfEmpty(0)
                                    .Max();
                            }
                        }
                        
                        // Tính số lượng đã cấp từ phiếu xuất kho (nếu có)
                        var trangThaiDaCap = new[]
                        {
                            "Hoàn thành",
                            "Đã xuất kho",
                            "Đã lấy hàng",
                            "Chờ người yêu cầu xác nhận",
                            "Đang chuẩn bị hàng"
                        };
                        var danhSachVTDaXuatHopLe = _context.vtphieuxuatkho
                            .Where(vt => trangThaiDaCap.Contains(vt.TrangThai))
                            .ToList();
                        var danhSachVTDaNhapTra = _context.vtphieunhapkho
                            .Where(vt => vt.TrangThai == "Đã nhập kho")
                            .ToList();
                        int soLuongDaCapTuPhieuXuat = TinhSoLuongDaCapThucTe(maYeuCauChuan, vatTu.MaSanpham ?? "", danhSachVTDaXuatHopLe, danhSachVTDaNhapTra);
                        
                        // Số lượng đã cấp = MAX(số lượng từ vtyeucau, số lượng từ phiếu xuất kho)
                        // Vì có thể yêu cầu trước đó đã được duyệt nhưng chưa có phiếu xuất kho
                        int soLuongDaCap = Math.Max(soLuongDaCapTuVTYeucau, soLuongDaCapTuPhieuXuat);
                        
                        // Lấy số lượng yêu cầu hiện tại
                        int soLuongYeuCauHienTai = vatTu.SLMoi ?? vatTu.SL ?? 0;
                        
                        Console.WriteLine($"[DEBUG XuLyVatTuYeucau] Vật tư {vatTu.MaSanpham}: SLMoi={vatTu.SLMoi}, SL={vatTu.SL}, soLuongYeuCauHienTai={soLuongYeuCauHienTai}, soLuongDaCap={soLuongDaCap} (từ VTYeucau: {soLuongDaCapTuVTYeucau}, từ PhieuXuat: {soLuongDaCapTuPhieuXuat}), maYeuCauChuan={maYeuCauChuan}, allRelatedMaYeucau.Count={allRelatedMaYeucau.Count}");
                        
                        // Nếu số lượng mới bằng số lượng đã cấp → trạng thái "Hoàn thành"
                        if (soLuongYeuCauHienTai == soLuongDaCap && soLuongDaCap > 0)
                        {
                            Console.WriteLine($" [XuLyVatTuYeucau] Số lượng yêu cầu mới ({soLuongYeuCauHienTai}) bằng số lượng đã cấp ({soLuongDaCap}). Đặt trạng thái 'Hoàn thành' cho vật tư {vatTu.MaSanpham}");
                            
                            vatTu.TrangThai = "Hoàn thành";
                            _context.vtyeucau.Update(vatTu);
                            _context.SaveChanges();
                            
                            // Không cần kiểm tra tồn kho nữa, return luôn
                            return Json(new { success = true, message = "Đã duyệt vật tư thành công. Trạng thái: Hoàn thành (số lượng mới bằng số lượng đã cấp)." });
                        }
                        else
                        {
                            Console.WriteLine($"[DEBUG XuLyVatTuYeucau] Vật tư {vatTu.MaSanpham}: Không thỏa điều kiện Hoàn thành. soLuongYeuCauHienTai={soLuongYeuCauHienTai}, soLuongDaCap={soLuongDaCap}, soLuongDaCap > 0={soLuongDaCap > 0}");
                        }
                    }
                    
                    // Kiểm tra tồn kho khi duyệt
                    var khotong = _context.khotongs.FirstOrDefault(kt => 
                        kt.Makho == vatTu.YCMakho && 
                        kt.MaSanpham == vatTu.MaSanpham)
                        ?? _context.khotongs.FirstOrDefault(kt => 
                            kt.MaSanpham == vatTu.MaSanpham);

                    if (khotong != null && khotong.SL > 0)
                    {
                        // Tính số lượng hàng đã cam kết từ các phiếu xuất khác (FIFO: chỉ tính vật tư duyệt trước)
                        int soLuongDaCamKet = TinhSoLuongDaCamKet(khotong.Makho ?? "", khotong.MaSanpham ?? "", vatTu.NgayDuyet, null);
                        // Số lượng khả dụng = Tồn kho - Số lượng đã cam kết
                        int soLuongKhaDung = (khotong.SL ?? 0) - soLuongDaCamKet;
                        int soLuongYeuCau = vatTu.SL ?? 0;

                        // Nếu số lượng khả dụng >= số lượng yêu cầu thì đủ hàng, ngược lại thiếu hàng
                        if (soLuongKhaDung >= soLuongYeuCau)
                        {
                            vatTu.TrangThai = "Chờ xuất kho";
                        }
                        else
                        {
                            vatTu.TrangThai = "Đang mua hàng";
                        }
                    }
                    else
                    {
                        // Không có trong kho, cần mua hàng
                        vatTu.TrangThai = "Đang mua hàng";
                    }
                }
                else if (action == "reject")
                {
                    // Giám đốc hoặc người có quyền từ chối
                    // Lưu lại thời gian xử lý để hiển thị cột "Ngày duyệt" cho trạng thái "Giám đốc - Đã từ chối"
                    vatTu.NgayDuyet = DateTime.Now;
                    vatTu.TrangThai = "Giám đốc - Đã từ chối";
                    vatTu.GhiChu = GhiChu; // Lưu ghi chú khi từ chối
                    
                    // Lưu thông tin người từ chối vào bảng yeucau
                    var yeucauReject = _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
                    if (yeucauReject != null)
                    {
                        var maNguoiDuyet = HttpContext.Session.GetString("MaNguoidung");
                        if (!string.IsNullOrWhiteSpace(maNguoiDuyet))
                        {
                            yeucauReject.NguoiDuyet = maNguoiDuyet;
                            yeucauReject.NgayDuyet = DateTime.Now;
                            _context.yeucau.Update(yeucauReject);
                        }
                    }
                }

                _context.vtyeucau.Update(vatTu);
                _context.SaveChanges();

                // Sau khi duyệt/từ chối một vật tư, kiểm tra trạng thái của tất cả vật tư trong yêu cầu
                var chucVu = HttpContext.Session.GetString("Chucvu");
                if (chucVu == "Giám đốc")
                {
                    var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
                    if (yeucau != null)
                        {
                        // Lấy lại danh sách vật tư sau khi cập nhật
                        var vatTuListAfter = _context.vtyeucau
                            .Where(v => v.VTMaYeucau == MaYeucau).ToList();

                        // Kiểm tra các vật tư đã được duyệt/xử lý (bao gồm cả "Đang mua hàng" và "Hoàn thành")
                        var approvedVatTu = vatTuListAfter.Where(v =>
                            v.TrangThai == "Chờ xuất kho" ||
                            v.TrangThai == "Đã xuất kho" ||
                            v.TrangThai == "Đã duyệt" ||
                            v.TrangThai == "Đang mua hàng" ||
                            v.TrangThai == "Hoàn thành").ToList();

                        // Kiểm tra các vật tư đang chờ duyệt (chưa được xử lý)
                        var pendingVatTu = vatTuListAfter.Where(v =>
                            string.IsNullOrWhiteSpace(v.TrangThai) ||
                            v.TrangThai == "Chờ giám đốc duyệt" ||
                            v.TrangThai == "Giám đốc").ToList();

                        // Kiểm tra các vật tư bị từ chối
                        var rejectedVatTu = vatTuListAfter.Where(v =>
                            !string.IsNullOrEmpty(v.TrangThai) &&
                            v.TrangThai.Contains("Đã từ chối")).ToList();

                        // Tạo phiếu xuất kho/mua hàng NGAY KHI có ít nhất 1 vật tư đã được duyệt/xử lý
                        // XuliphieuyeucauPartial tự kiểm tra và chỉ thêm các vật tư chưa được đưa vào phiếu,
                        // nên có thể gọi nhiều lần mà không bị trùng lặp.
                        if (approvedVatTu.Any())
                        {
                            // Tạo phiếu mua hàng/xuất kho cho các vật tư đã duyệt (nếu chưa được xử lý)
                            // Method XuliphieuyeucauPartial sẽ tự kiểm tra và chỉ xử lý các vật tư chưa được xử lý
                            // ⚠️ Loại trừ vật tư có trạng thái "Hoàn thành" khỏi danh sách xử lý
                            var approvedMaSanphamList = approvedVatTu
                                .Where(v => v.TrangThai != "Hoàn thành")
                                .Select(v => v.MaSanpham)
                                .ToList();
                            
                            // Chỉ gọi XuliphieuyeucauPartial nếu có vật tư cần xử lý (không phải "Hoàn thành")
                            if (approvedMaSanphamList.Any())
                            {
                                XuliphieuyeucauPartial(MaYeucau, approvedMaSanphamList);
                            }

                            // Sau khi tạo phiếu, kiểm tra lại trạng thái vật tư để quyết định trạng thái yêu cầu
                            _context.SaveChanges();
                            
                            // Reload lại danh sách vật tư từ database để lấy trạng thái mới nhất sau khi XuliphieuyeucauPartial chạy xong
                            _context.Entry(yeucau).Reload();
                            var vatTuListFinal = _context.vtyeucau
                                .Where(v => v.VTMaYeucau == MaYeucau).ToList();
                            
                            // Kiểm tra các trạng thái vật tư
                            var hasDangMuaHang = vatTuListFinal.Any(v =>
                                v.TrangThai == "Đang mua hàng");
                            var hasChoXuatKho = vatTuListFinal.Any(v =>
                                v.TrangThai == "Chờ xuất kho");
                            var hasDaXuatKho = vatTuListFinal.Any(v =>
                                v.TrangThai == "Đã xuất kho");
                            var hasRejectedFinal = vatTuListFinal.Any(v =>
                                !string.IsNullOrEmpty(v.TrangThai) &&
                                v.TrangThai.Contains("Đã từ chối"));
                            
                            // Cập nhật trạng thái yêu cầu dựa trên trạng thái vật tư
                            if (hasRejectedFinal && vatTuListFinal.All(v =>
                                !string.IsNullOrEmpty(v.TrangThai) &&
                                v.TrangThai.Contains("Đã từ chối")))
                            {
                                // Tất cả vật tư đều bị từ chối
                                yeucau.TrangThai = "Giám đốc - Đã từ chối";
                            }
                            else if (hasDangMuaHang)
                            {
                                // Có vật tư đang mua hàng → trạng thái yêu cầu là "Đang mua hàng"
                                yeucau.TrangThai = "Đang mua hàng";
                            }
                            else if (hasChoXuatKho || hasDaXuatKho)
                            {
                                // Có vật tư chờ xuất kho hoặc đã xuất kho
                                // Tất cả các vật tư còn lại đã ở các trạng thái “chờ/đã xuất” hoặc đã hoàn thành / bị từ chối
                                // → coi như đã xử lý xong (không còn vật tư cần mua hàng)
                                var allChoXuatKhoOrDaXuatKho = vatTuListFinal.All(v =>
                                    v.TrangThai == "Chờ xuất kho" ||
                                    v.TrangThai == "Đã xuất kho" ||
                                    v.TrangThai == "Hoàn thành" ||
                                    (!string.IsNullOrEmpty(v.TrangThai) && v.TrangThai.Contains("Đã từ chối")));
                                
                                if (allChoXuatKhoOrDaXuatKho)
                                {
                                    // Nếu có vật tư chờ xuất kho thì trạng thái là "Chờ xuất kho", nếu tất cả đã xuất kho thì "Đã xuất kho"
                                    if (hasChoXuatKho && !hasDaXuatKho)
                                    {
                                        yeucau.TrangThai = "Chờ xuất kho";
                                    }
                                    else if (hasDaXuatKho && !hasChoXuatKho)
                                    {
                                        yeucau.TrangThai = "Đã xuất kho";
                                    }
                                    else
                                    {
                                        // Có cả hai, ưu tiên "Chờ xuất kho" vì còn vật tư chưa xuất
                                        yeucau.TrangThai = "Chờ xuất kho";
                                    }
                                }
                                else
                                {
                                    // Có vật tư chờ xuất kho/đã xuất kho nhưng còn vật tư khác chưa hoàn thành → "Đang mua hàng"
                                    yeucau.TrangThai = "Đang mua hàng";
                                }
                            }
                            else if (hasRejectedFinal)
                            {
                                // Có vật tư bị từ chối (nhưng không phải tất cả) và không còn vật tư đang mua hàng
                                // Kiểm tra xem các vật tư còn lại đã xuất kho chưa
                                // Các vật tư còn lại đều đã hoàn tất (chờ/đã xuất hoặc hoàn thành / bị từ chối)
                                var allCompleted = vatTuListFinal.All(v =>
                                    v.TrangThai == "Chờ xuất kho" ||
                                    v.TrangThai == "Đã xuất kho" ||
                                    v.TrangThai == "Hoàn thành" ||
                                    (!string.IsNullOrEmpty(v.TrangThai) && v.TrangThai.Contains("Đã từ chối")));
                                
                                if (allCompleted)
                                {
                                    // Kiểm tra xem có vật tư chờ xuất kho không
                                    var hasChoXuatKhoInRejected = vatTuListFinal.Any(v => v.TrangThai == "Chờ xuất kho");
                                    if (hasChoXuatKhoInRejected)
                                    {
                                        yeucau.TrangThai = "Chờ xuất kho";
                                    }
                                    else
                                    {
                                        yeucau.TrangThai = "Đã xuất kho";
                                    }
                                }
                                else
                                {
                                    // Còn vật tư chưa hoàn thành → "Đang mua hàng" để hệ thống có thể tiếp tục xử lý
                                    yeucau.TrangThai = "Đang mua hàng";
                                }
                            }
                            else
                            {
                                // Trường hợp khác → "Đang mua hàng" để hệ thống có thể tiếp tục xử lý
                                yeucau.TrangThai = "Đang mua hàng";
                            }
                            _context.yeucau.Update(yeucau);
                            _context.SaveChanges();
                        }
                        // Nếu tất cả vật tư đều bị từ chối và không còn vật tư nào đang chờ duyệt
                        else if (rejectedVatTu.Any() && !pendingVatTu.Any() && !approvedVatTu.Any())
                        {
                            yeucau.TrangThai = "Giám đốc - Đã từ chối";
                            _context.yeucau.Update(yeucau);
                            _context.SaveChanges();

                            // Gửi email thông báo từ chối cho người yêu cầu
                            if (action == "reject")
                            {
                                var ghiChuTuChoi = vatTu.GhiChu ?? "";
                                SendRejectionEmailAsync(MaYeucau, ghiChuTuChoi);
                            }
                        }
                    }
                }

                // Gửi email thông báo từ chối nếu giám đốc từ chối một vật tư đơn lẻ
                if (action == "reject")
                {
                    var yeucauAfterReject = _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
                    if (yeucauAfterReject != null && yeucauAfterReject.TrangThai == "Giám đốc - Đã từ chối")
                    {
                        var ghiChuTuChoi = vatTu.GhiChu ?? "";
                        SendRejectionEmailAsync(MaYeucau, ghiChuTuChoi);
                    }
                }

                return Json(new { 
                    success = true, 
                    message = action == "approve" ? "Đã duyệt vật tư thành công." : "Đã từ chối vật tư.",
                    trangThai = vatTu.TrangThai,
                    ghiChu = vatTu.GhiChu
                });
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

                foreach (var vatTu in vatTuList)
                {
                    if (string.IsNullOrWhiteSpace(vatTu.TrangThai))
                    {
                        vatTu.TrangThai = "Chờ giám đốc duyệt";
                        _context.vtyeucau.Update(vatTu);
                    }
                }

                if (!vatTuList.Any())
                {
                    return Json(new { success = false, message = "Không tìm thấy vật tư nào." });
                }

                // Helper function để kiểm tra xem vật tư có đang Chờ giám đốc duyệt không
                Func<string, bool> isAwaitingDirectorStatus = status =>
                {
                    if (string.IsNullOrWhiteSpace(status))
                    {
                        return true;
                    }

                    var normalized = status.Trim();
                    return normalized.Equals("Chờ giám đốc duyệt", StringComparison.OrdinalIgnoreCase)
                        || normalized.Equals("Giám đốc", StringComparison.OrdinalIgnoreCase)
                        || normalized.StartsWith("Chờ giám đốc", StringComparison.OrdinalIgnoreCase)
                        || normalized.Contains("chờ giám đốc", StringComparison.OrdinalIgnoreCase);
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
                           normalized == "Đã nhận hàng";
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

                int processedCount = 0;
                int skippedCount = 0;

                foreach (var vatTu in vatTuList)
                {
                    if (action == "approve")
                    {
                        // Chỉ duyệt các vật tư đang chờ Giám đốc và chưa được duyệt/từ chối
                        if (isAwaitingDirectorStatus(vatTu.TrangThai) && 
                            !isAlreadyApproved(vatTu.TrangThai) && 
                            !isAlreadyRejected(vatTu.TrangThai))
                        {
                            // Lưu thời gian duyệt (FIFO - ai duyệt trước được ưu tiên)
                            vatTu.NgayDuyet = DateTime.Now;
                            
                            // Lưu thông tin người duyệt vào bảng yeucau
                            var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
                            if (yeucau != null)
                            {
                                var maNguoiDuyet = HttpContext.Session.GetString("MaNguoidung");
                                if (!string.IsNullOrWhiteSpace(maNguoiDuyet))
                                {
                                    yeucau.NguoiDuyet = maNguoiDuyet;
                                    yeucau.NgayDuyet = DateTime.Now;
                                    _context.yeucau.Update(yeucau);
                                }
                            }
                            
                            // ⚠️ KIỂM TRA: Nếu cùng dự án và cùng mã yêu cầu (base code), và số lượng mới bằng số lượng đã cấp → trạng thái "Hoàn thành"
                            if (yeucau != null && !string.IsNullOrWhiteSpace(yeucau.YCMaDuan) && !string.IsNullOrWhiteSpace(vatTu.MaSanpham))
                            {
                                // Tính base mã yêu cầu để kiểm tra
                                string maYeuCauChuan = NormalizeMaYeucauBase(MaYeucau);
                                
                                // Tính số lượng đã cấp thực tế từ các yêu cầu trước đó cùng base code
                                // Lấy tất cả mã yêu cầu cùng base code (trừ yêu cầu hiện tại)
                                var allRelatedMaYeucau = _context.yeucau
                                    .Where(y => !string.IsNullOrWhiteSpace(y.MaYeucau) && 
                                               y.YCMaDuan == yeucau.YCMaDuan &&
                                               y.MaYeucau != MaYeucau) // Loại trừ yêu cầu hiện tại
                                    .ToList()
                                    .Where(y => string.Equals(
                                        NormalizeMaYeucauBase(y.MaYeucau),
                                        maYeuCauChuan,
                                        StringComparison.OrdinalIgnoreCase))
                                    .Select(y => y.MaYeucau)
                                    .ToList();
                                
                                // Tính số lượng đã cấp từ các yêu cầu trước đó cùng base code
                                // Lấy số lượng từ vật tư yêu cầu đã được duyệt (có NgayDuyet - đã được giám đốc duyệt)
                                // Tính MAX số lượng của các vật tư cùng mã sản phẩm trong các yêu cầu trước đó
                                // ⭐ SỬA BUG: Loại trừ các yêu cầu và vật tư đã bị từ chối
                                int soLuongDaCapTuVTYeucau = 0;
                                if (allRelatedMaYeucau.Any())
                                {
                                    // Lấy danh sách các mã yêu cầu đã bị từ chối
                                    var maYeucauBiTuChoi = _context.yeucau
                                        .Where(y => allRelatedMaYeucau.Contains(y.MaYeucau) &&
                                                   !string.IsNullOrEmpty(y.TrangThai) &&
                                                   y.TrangThai.Contains("Đã từ chối", StringComparison.OrdinalIgnoreCase))
                                        .Select(y => y.MaYeucau)
                                        .ToList();

                                    // ⭐ CHỈ TÍNH TỪ CÁC VẬT TƯ ĐÃ CÓ TRẠNG THÁI "Đã xuất kho" (hoặc tương đương)
                                    var trangThaiDaXuatKho = new[] { "Đã xuất kho", "Hoàn thành", "Đã lấy hàng" };
                                    var vatTuYeuCauTruocDo = _context.vtyeucau
                                        .Where(vt => allRelatedMaYeucau.Contains(vt.VTMaYeucau) &&
                                                   vt.MaSanpham == vatTu.MaSanpham &&
                                                   vt.NgayDuyet.HasValue && // Đã được duyệt
                                                   !maYeucauBiTuChoi.Contains(vt.VTMaYeucau) && // ⭐ Loại trừ yêu cầu đã bị từ chối
                                                   !string.IsNullOrEmpty(vt.TrangThai) &&
                                                   !vt.TrangThai.Contains("Đã từ chối", StringComparison.OrdinalIgnoreCase) && // ⭐ Loại trừ vật tư đã bị từ chối
                                                   trangThaiDaXuatKho.Contains(vt.TrangThai)) // ⭐ CHỈ tính từ vật tư đã xuất kho
                                        .ToList();
                                    
                                    if (vatTuYeuCauTruocDo.Any())
                                    {
                                        // Lấy MAX số lượng từ các vật tư yêu cầu trước đó (theo logic MAX)
                                        soLuongDaCapTuVTYeucau = vatTuYeuCauTruocDo
                                            .Select(vt => Math.Max(vt.SLMoi ?? 0, vt.SL ?? 0))
                                            .DefaultIfEmpty(0)
                                            .Max();
                                    }
                                }
                                
                                // Tính số lượng đã cấp từ phiếu xuất kho (nếu có)
                                var trangThaiDaCap = new[]
                                {
                                    "Đã xác nhận nhận hàng",
                                    "Hoàn thành",
                                    "Đã xuất kho",
                                    "Đã lấy hàng",
                                    "Chờ người yêu cầu xác nhận",
                                    "Đang chuẩn bị hàng"
                                };
                                var danhSachVTDaXuatHopLe = _context.vtphieuxuatkho
                                    .Where(vt => trangThaiDaCap.Contains(vt.TrangThai))
                                    .ToList();
                                var danhSachVTDaNhapTra = _context.vtphieunhapkho
                                    .Where(vt => vt.TrangThai == "Đã nhập kho")
                                    .ToList();
                                int soLuongDaCapTuPhieuXuat = TinhSoLuongDaCapThucTe(maYeuCauChuan, vatTu.MaSanpham ?? "", danhSachVTDaXuatHopLe, danhSachVTDaNhapTra);
                                
                                // Số lượng đã cấp = MAX(số lượng từ vtyeucau, số lượng từ phiếu xuất kho)
                                // Vì có thể yêu cầu trước đó đã được duyệt nhưng chưa có phiếu xuất kho
                                int soLuongDaCap = Math.Max(soLuongDaCapTuVTYeucau, soLuongDaCapTuPhieuXuat);
                                
                                // Lấy số lượng yêu cầu hiện tại
                                int soLuongYeuCauHienTai = vatTu.SLMoi ?? vatTu.SL ?? 0;
                                
                                Console.WriteLine($"[DEBUG XuLyTatCaVatTuYeucau] Vật tư {vatTu.MaSanpham}: SLMoi={vatTu.SLMoi}, SL={vatTu.SL}, soLuongYeuCauHienTai={soLuongYeuCauHienTai}, soLuongDaCap={soLuongDaCap} (từ VTYeucau: {soLuongDaCapTuVTYeucau}, từ PhieuXuat: {soLuongDaCapTuPhieuXuat}), maYeuCauChuan={maYeuCauChuan}, allRelatedMaYeucau.Count={allRelatedMaYeucau.Count}");
                                
                                // Nếu số lượng mới bằng số lượng đã cấp → trạng thái "Hoàn thành"
                                if (soLuongYeuCauHienTai == soLuongDaCap && soLuongDaCap > 0)
                                {
                                    Console.WriteLine($"ℹ️ [XuLyTatCaVatTuYeucau] Số lượng yêu cầu mới ({soLuongYeuCauHienTai}) bằng số lượng đã cấp ({soLuongDaCap}). Đặt trạng thái 'Hoàn thành' cho vật tư {vatTu.MaSanpham}");
                                    
                                    vatTu.TrangThai = "Hoàn thành";
                                    _context.vtyeucau.Update(vatTu);
                                    processedCount++;
                                    continue; // Bỏ qua phần kiểm tra tồn kho
                                }
                                else
                                {
                                    Console.WriteLine($"[DEBUG XuLyTatCaVatTuYeucau] Vật tư {vatTu.MaSanpham}: Không thỏa điều kiện Hoàn thành. soLuongYeuCauHienTai={soLuongYeuCauHienTai}, soLuongDaCap={soLuongDaCap}, soLuongDaCap > 0={soLuongDaCap > 0}");
                                }
                            }
                            
                            // Kiểm tra tồn kho khi duyệt
                            var khotong = _context.khotongs.FirstOrDefault(kt => 
                                kt.Makho == vatTu.YCMakho && 
                                kt.MaSanpham == vatTu.MaSanpham)
                                ?? _context.khotongs.FirstOrDefault(kt => 
                                    kt.MaSanpham == vatTu.MaSanpham);

                            // ⚠️ Bảo vệ trạng thái "Hoàn thành" - không ghi đè nếu đã có trạng thái này
                            if (vatTu.TrangThai == "Hoàn thành")
                            {
                                Console.WriteLine($"ℹ️ [XuLyTatCaVatTuYeucau] Vật tư {vatTu.MaSanpham} đã có trạng thái 'Hoàn thành', bỏ qua kiểm tra tồn kho");
                                processedCount++;
                                continue;
                            }
                            
                            if (khotong != null && khotong.SL > 0)
                            {
                                // Tính số lượng hàng đã cam kết từ các phiếu xuất khác (FIFO: chỉ tính vật tư duyệt trước)
                                int soLuongDaCamKet = TinhSoLuongDaCamKet(khotong.Makho ?? "", khotong.MaSanpham ?? "", vatTu.NgayDuyet, null);
                                // Số lượng khả dụng = Tồn kho - Số lượng đã cam kết
                                int soLuongKhaDung = (khotong.SL ?? 0) - soLuongDaCamKet;
                                int soLuongYeuCau = vatTu.SL ?? 0;

                                // Nếu số lượng khả dụng >= số lượng yêu cầu thì đủ hàng, ngược lại thiếu hàng
                                if (soLuongKhaDung >= soLuongYeuCau)
                                {
                                    vatTu.TrangThai = "Chờ xuất kho";
                                }
                                else
                                {
                                    vatTu.TrangThai = "Đang mua hàng";
                                }
                            }
                            else
                            {
                                // Không có trong kho, cần mua hàng
                                vatTu.TrangThai = "Đang mua hàng";
                            }
                            
                            _context.vtyeucau.Update(vatTu);
                            processedCount++;
                        }
                        else
                        {
                            skippedCount++;
                        }
                    }
                    else if (action == "reject")
                    {
                        // Chỉ từ chối các vật tư đang chờ Giám đốc và chưa được duyệt/từ chối
                        if (isAwaitingDirectorStatus(vatTu.TrangThai) && 
                            !isAlreadyApproved(vatTu.TrangThai) && 
                            !isAlreadyRejected(vatTu.TrangThai))
                        {
                            // Lưu lại thời gian xử lý để bảng yêu cầu & chi tiết hiển thị đúng "Ngày duyệt"
                            vatTu.NgayDuyet = DateTime.Now;
                            vatTu.TrangThai = "Giám đốc - Đã từ chối";
                            _context.vtyeucau.Update(vatTu);
                            processedCount++;
                        }
                        else
                        {
                            skippedCount++;
                        }
                    }
                }

                // Lưu thay đổi trạng thái vật tư trước khi xử lý tiếp
                _context.SaveChanges();

                // Cập nhật trạng thái yêu cầu chính nếu giám đốc duyệt hoặc từ chối tất cả vật tư
                if (action == "approve")
                {
                    var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
                    if (yeucau != null)
                    {
                        var chucVu = HttpContext.Session.GetString("Chucvu");
                        if (chucVu == "Giám đốc")
                        {
                            // Kiểm tra xem tất cả vật tư đã được duyệt chưa
                            // Lấy lại danh sách vật tư sau khi cập nhật để kiểm tra chính xác
                            var vatTuListAfter = _context.vtyeucau
                                .Where(v => v.VTMaYeucau == MaYeucau).ToList();
                            
                            // Kiểm tra xem tất cả vật tư đã được duyệt chưa (trạng thái "Đã duyệt", "Đang mua hàng", "Hoàn thành", "Đã xuất kho", "Đã nhận hàng")
                            var allApproved = vatTuListAfter.All(v => 
                                v.TrangThai == "Đã duyệt" || 
                                v.TrangThai == "Đang mua hàng" ||
                                v.TrangThai == "Hoàn thành" ||
                                v.TrangThai == "Đã xuất kho" ||
                                v.TrangThai == "Đã nhận hàng" ||
                                v.TrangThai == "Chờ xuất kho");
                            
                            // Kiểm tra xem có vật tư nào bị từ chối không
                            var hasRejected = vatTuListAfter.Any(v =>
                                !string.IsNullOrEmpty(v.TrangThai) &&
                                v.TrangThai.Contains("Đã từ chối"));
                            
                            // Kiểm tra xem có vật tư nào đang mua hàng không
                            var hasDangMuaHang = vatTuListAfter.Any(v =>
                                v.TrangThai == "Đang mua hàng");
                            
                            if (allApproved && !hasRejected)
                            {
                               
                                Xuliphieuyeucau(MaYeucau, null, null, null, null, null, null);
                                
                               
                                _context.SaveChanges();
                                
                              
                                _context.SaveChanges();
                                
                                
                                _context.Entry(yeucau).Reload();
                                var vatTuListFinal = _context.vtyeucau
                                    .Where(v => v.VTMaYeucau == MaYeucau).ToList();
                                
                                
                                var hasDangMuaHangFinal = vatTuListFinal.Any(v =>
                                    v.TrangThai == "Đang mua hàng");
                                var hasDaXuatKhoFinal = vatTuListFinal.Any(v =>
                                    v.TrangThai == "Đã xuất kho");
                                var hasRejectedFinal = vatTuListFinal.Any(v =>
                                    !string.IsNullOrEmpty(v.TrangThai) &&
                                    v.TrangThai.Contains("Đã từ chối"));
                                
                               
                                if (hasRejectedFinal && vatTuListFinal.All(v =>
                                    !string.IsNullOrEmpty(v.TrangThai) &&
                                    v.TrangThai.Contains("Đã từ chối")))
                                {
                                    // Tất cả vật tư đều bị từ chối
                                    yeucau.TrangThai = "Giám đốc - Đã từ chối";
                                }
                                else if (hasDangMuaHangFinal)
                                {
                                    // Có vật tư đang mua hàng → trạng thái yêu cầu là "Đang mua hàng"
                                    yeucau.TrangThai = "Đang mua hàng";
                                }
                                else if (hasDaXuatKhoFinal)
                                {
                                    
                                    var allDaXuatKhoFinal = vatTuListFinal.All(v =>
                                        v.TrangThai == "Đã xuất kho" ||
                                        (!string.IsNullOrEmpty(v.TrangThai) && v.TrangThai.Contains("Đã từ chối")));
                                    
                                    if (allDaXuatKhoFinal)
                                    {
                                        
                                        yeucau.TrangThai = "Đã xuất kho";
                                    }
                                    else
                                    {
                                        
                                        yeucau.TrangThai = "Đang mua hàng";
                                    }
                                }
                                else if (hasRejectedFinal)
                                {
                                    // Có vật tư bị từ chối (nhưng không phải tất cả) và không còn vật tư đang mua hàng
                                    // Kiểm tra xem các vật tư còn lại đã xuất kho chưa
                                    var allCompletedFinal = vatTuListFinal.All(v =>
                                        v.TrangThai == "Đã xuất kho" ||
                                        (!string.IsNullOrEmpty(v.TrangThai) && v.TrangThai.Contains("Đã từ chối")));
                                    
                                    if (allCompletedFinal)
                                    {
                                        // Tất cả vật tư đã xuất kho hoặc từ chối → "Đã xuất kho"
                                        yeucau.TrangThai = "Đã xuất kho";
                                    }
                                    else
                                    {
                                        // Còn vật tư chưa hoàn thành → "Đang mua hàng" để hệ thống có thể tiếp tục xử lý
                                        yeucau.TrangThai = "Đang mua hàng";
                                    }
                                }
                                else
                                {
                                    // Trường hợp khác → "Đang mua hàng" để hệ thống có thể tiếp tục xử lý
                                    yeucau.TrangThai = "Đang mua hàng";
                                }
                                _context.yeucau.Update(yeucau);
                                _context.SaveChanges();
                            }
                            else if (hasRejected)
                            {
                                // Kiểm tra xem tất cả vật tư đều bị từ chối hay chỉ một phần
                                var allRejected = vatTuListAfter.All(v =>
                                    !string.IsNullOrEmpty(v.TrangThai) &&
                                    v.TrangThai.Contains("Đã từ chối"));
                                
                                if (allRejected)
                                {
                                    // Tất cả vật tư đều bị từ chối
                                    yeucau.TrangThai = "Giám đốc - Đã từ chối";
                                    
                                    // Gửi email thông báo từ chối cho người yêu cầu
                                    SendRejectionEmailAsync(MaYeucau, "");
                                }
                                else
                                {
                                    // Có một số vật tư được duyệt (Đã xuất kho/Đang mua hàng/Đã duyệt) và một số bị từ chối
                                    // → vẫn phải sinh phiếu PXK/PMH cho các vật tư hợp lệ
                                    var approvedForPhieu = vatTuListAfter.Where(v =>
                                        v.TrangThai == "Đã xuất kho" ||
                                        v.TrangThai == "Đang mua hàng" ||
                                        v.TrangThai == "Đã duyệt").ToList();

                                    if (approvedForPhieu.Any())
                                    {
                                        var approvedMaSanphamList = approvedForPhieu
                                            .Select(v => v.MaSanpham)
                                            .Where(ms => !string.IsNullOrEmpty(ms))
                                            .ToList();

                                        if (approvedMaSanphamList.Any())
                                        {
                                            XuliphieuyeucauPartial(MaYeucau, approvedMaSanphamList);
                                            _context.SaveChanges();
                                        }
                                    }

                                    // Kiểm tra xem có vật tư đang mua hàng không
                                    var hasDangMuaHangInRejected = vatTuListAfter.Any(v =>
                                        v.TrangThai == "Đang mua hàng");
                                    
                                    if (hasDangMuaHangInRejected)
                                    {
                                        yeucau.TrangThai = "Đang mua hàng";
                                    }
                                    else
                                    {
                                        // Kiểm tra xem tất cả vật tư đã hoàn thành (xuất kho hoặc từ chối) chưa
                                        var allCompletedInRejected = vatTuListAfter.All(v =>
                                            v.TrangThai == "Đã xuất kho" ||
                                            (!string.IsNullOrEmpty(v.TrangThai) && v.TrangThai.Contains("Đã từ chối")));
                                        
                                        if (allCompletedInRejected)
                                        {
                                            // Tất cả vật tư đã xuất kho hoặc từ chối → "Đã xuất kho"
                                            yeucau.TrangThai = "Đã xuất kho";
                                        }
                                        else
                                        {
                                            // Còn vật tư chưa hoàn thành → "Đang mua hàng" để hệ thống có thể tiếp tục xử lý
                                            yeucau.TrangThai = "Đang mua hàng";
                                        }
                                    }
                                }
                                _context.yeucau.Update(yeucau);
                                _context.SaveChanges();
                            }
                        }
                    }
                }
                else if (action == "reject")
                {
                    // Nếu Giám đốc chọn TỪ CHỐI TẤT CẢ thì cập nhật trạng thái yêu cầu thành "Giám đốc - Đã từ chối"
                    var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
                    if (yeucau != null)
                    {
                        var chucVu = HttpContext.Session.GetString("Chucvu");
                        if (chucVu == "Giám đốc")
                        {
                            yeucau.TrangThai = "Giám đốc - Đã từ chối";
                            _context.yeucau.Update(yeucau);
                            _context.SaveChanges();

                            // Gửi email thông báo từ chối cho người yêu cầu
                            SendRejectionEmailAsync(MaYeucau, "");
                        }
                    }
                }

                // Đồng bộ trạng thái yêu cầu với phiếu mua hàng (giống như trong action Yeucau)
                var yeucauAfter = _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
                if (yeucauAfter != null && action == "approve")
                {
                    var PhieuMuaHangList = _context.phieumuahang.Where(p => p.MaYeucau == MaYeucau).ToList();
                    if (PhieuMuaHangList.Any(p => p.TrangThai != "Đã nhận hàng"))
                    {
                        yeucauAfter.TrangThai = "Đang mua hàng";
                        _context.yeucau.Update(yeucauAfter);
                        _context.SaveChanges();
                    }
                }

                string message;
                if (action == "approve")
                {
                    if (processedCount > 0)
                    {
                        message = $"Đã duyệt {processedCount} vật tư thành công.";
                        if (skippedCount > 0)
                        {
                            message += $" ({skippedCount} vật tư đã được duyệt/từ chối trước đó hoặc không ở trạng thái chờ duyệt)";
                        }
                    }
                    else
                    {
                        message = "Không có vật tư nào được duyệt. Tất cả vật tư đã được duyệt/từ chối trước đó hoặc không ở trạng thái chờ duyệt.";
                    }
                }
                else
                {
                    if (processedCount > 0)
                    {
                        message = $"Đã từ chối {processedCount} vật tư.";
                        if (skippedCount > 0)
                        {
                            message += $" ({skippedCount} vật tư đã được duyệt/từ chối trước đó hoặc không ở trạng thái chờ duyệt)";
                        }
                    }
                    else
                    {
                        message = "Không có vật tư nào được từ chối. Tất cả vật tư đã được duyệt/từ chối trước đó hoặc không ở trạng thái chờ duyệt.";
                    }
                }

                return Json(new { success = true, message = message });
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

                // Đồng bộ trạng thái vật tư với trạng thái yêu cầu nếu yêu cầu đang "Chờ giám đốc duyệt" hoặc "Chờ quản lý dự án duyệt"
                // Xử lý cả 2 trường hợp: "Chờ giám đốc duyệt" và "Chờ Giám đốc duyệt" (chữ G hoa/thường)
                var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
                if (yeucau != null && yeucau.TrangThai != null)
                {
                    var trangThaiYeuCau = yeucau.TrangThai.Trim();
                    bool canDirectorApprove = trangThaiYeuCau.Equals("Chờ giám đốc duyệt", StringComparison.OrdinalIgnoreCase) || 
                                             trangThaiYeuCau.Equals("Chờ Giám đốc duyệt", StringComparison.OrdinalIgnoreCase) ||
                                             trangThaiYeuCau.Equals("Giám đốc", StringComparison.OrdinalIgnoreCase) ||
                                             trangThaiYeuCau.StartsWith("Chờ giám đốc", StringComparison.OrdinalIgnoreCase) ||
                                             trangThaiYeuCau.StartsWith("Chờ Giám đốc", StringComparison.OrdinalIgnoreCase) ||
                                             trangThaiYeuCau.Contains("chờ giám đốc", StringComparison.OrdinalIgnoreCase) ||
                                             trangThaiYeuCau.Contains("Chờ Giám đốc", StringComparison.OrdinalIgnoreCase) ||
                                             trangThaiYeuCau.Equals("Chờ quản lý dự án duyệt", StringComparison.OrdinalIgnoreCase) ||
                                             trangThaiYeuCau.StartsWith("Chờ quản lý dự án", StringComparison.OrdinalIgnoreCase) ||
                                             trangThaiYeuCau.Contains("quản lý dự án", StringComparison.OrdinalIgnoreCase);
                    
                    if (canDirectorApprove)
                    {
                        var allVatTu = _context.vtyeucau.Where(v => v.VTMaYeucau == MaYeucau).ToList();
                        foreach (var vatTu in allVatTu)
                        {
                            // Chỉ cập nhật các vật tư chưa được duyệt hoàn toàn
                            var vtTrangThai = vatTu.TrangThai?.Trim() ?? "";
                            bool isAlreadyProcessed = vtTrangThai == "Đã duyệt" || 
                                                      vtTrangThai == "Đang mua hàng" || 
                                                      vtTrangThai == "Đã từ chối" || 
                                                      vtTrangThai == "Đã xuất kho" || 
                                                      vtTrangThai == "Đã nhận hàng" || 
                                                      vtTrangThai == "Chờ xuất kho" ||
                                                      vtTrangThai.Contains("Đã từ chối", StringComparison.OrdinalIgnoreCase);
                            
                            if (!isAlreadyProcessed)
                            {
                                // Nếu yêu cầu đang "Chờ quản lý dự án duyệt" và vật tư chưa có trạng thái, đặt thành "Chờ giám đốc duyệt"
                                // Nếu yêu cầu đang "Chờ giám đốc duyệt" và vật tư chưa có trạng thái, đặt thành "Chờ giám đốc duyệt"
                                if (string.IsNullOrWhiteSpace(vtTrangThai) || 
                                    vtTrangThai.Equals("Chờ quản lý dự án duyệt", StringComparison.OrdinalIgnoreCase) ||
                                    vtTrangThai.StartsWith("Chờ quản lý dự án", StringComparison.OrdinalIgnoreCase))
                                {
                                    vatTu.TrangThai = "Chờ giám đốc duyệt";
                                    _context.vtyeucau.Update(vatTu);
                                }
                            }
                        }
                        _context.SaveChanges();
                    }
                }

                // Kiểm tra xem có phải là yêu cầu nhập kho không
                bool isNhapKhoRequest = !string.IsNullOrEmpty(MaYeucau) && 
                    (MaYeucau.StartsWith("NHAPKHO_DUAN_", StringComparison.OrdinalIgnoreCase) ||
                     MaYeucau.StartsWith("NHAPKHO_CANHAN_", StringComparison.OrdinalIgnoreCase));

                int processedCount = 0;
                int skippedCount = 0;
                bool anyApproved = false; // dùng để quyết định gửi thông báo phản hồi khi có duyệt

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
                        // Reload vật tư từ database để đảm bảo có trạng thái mới nhất sau khi đồng bộ
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

                    // Helper function để kiểm tra xem vật tư có đang chờ duyệt (giám đốc hoặc quản lý dự án) không
                    // Xử lý cả 2 trường hợp: "Chờ giám đốc duyệt" và "Chờ Giám đốc duyệt" (chữ G hoa/thường)
                    Func<string, bool> isAwaitingDirectorStatus = status =>
                    {
                        if (string.IsNullOrWhiteSpace(status))
                        {
                            return true; // Trạng thái null/empty được coi là chờ duyệt
                        }
                        var normalized = status.Trim();
                        // So sánh không phân biệt hoa thường để xử lý cả "Chờ giám đốc duyệt" và "Chờ Giám đốc duyệt"
                        return normalized.Equals("Chờ giám đốc duyệt", StringComparison.OrdinalIgnoreCase)
                            || normalized.Equals("Chờ Giám đốc duyệt", StringComparison.OrdinalIgnoreCase)
                            || normalized.Equals("Giám đốc", StringComparison.OrdinalIgnoreCase)
                            || normalized.StartsWith("Chờ giám đốc", StringComparison.OrdinalIgnoreCase)
                            || normalized.StartsWith("Chờ Giám đốc", StringComparison.OrdinalIgnoreCase)
                            || normalized.Contains("chờ giám đốc", StringComparison.OrdinalIgnoreCase)
                            || normalized.Contains("Chờ Giám đốc", StringComparison.OrdinalIgnoreCase)
                            || normalized.Equals("Chờ quản lý dự án duyệt", StringComparison.OrdinalIgnoreCase)
                            || normalized.StartsWith("Chờ quản lý dự án", StringComparison.OrdinalIgnoreCase)
                            || normalized.Contains("chờ quản lý dự án", StringComparison.OrdinalIgnoreCase);
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

                    // Chỉ xử lý các vật tư đang Chờ giám đốc duyệt/quản lý dự án duyệt và chưa được duyệt/từ chối
                    var currentStatus = isNhapKhoRequest ? (vtPhieuNhap?.TrangThai ?? "") : (vatTu?.TrangThai ?? "");
                    bool isAwaiting = isAwaitingDirectorStatus(currentStatus);
                    bool isAlreadyApprovedStatus = isAlreadyApproved(currentStatus);
                    bool isRejected = isAlreadyRejected(currentStatus);
                    bool canProcess = isAwaiting && !isAlreadyApprovedStatus && !isRejected;
                    
                    // Log để debug
                    if (!canProcess)
                    {
                        Console.WriteLine($"[DEBUG] Vật tư {maSanpham} bị skip - Status: '{currentStatus}', isAwaiting: {isAwaiting}, isAlreadyApproved: {isAlreadyApprovedStatus}, isRejected: {isRejected}");
                        skippedCount++;
                        continue;
                    }
                    
                    Console.WriteLine($"[DEBUG] Xử lý vật tư {maSanpham} - Status: '{currentStatus}', isApproved: {isApproved}, isNhapKhoRequest: {isNhapKhoRequest}");

                    // Kiểm tra số lượng yêu cầu - nếu bằng 0 thì đặt trạng thái "Hoàn thành" và bỏ qua
                    int soLuongYeuCau = isNhapKhoRequest ? (vtPhieuNhap?.SL ?? 0) : (vatTu?.SL ?? 0);
                    if (soLuongYeuCau == 0)
                    {
                        // Nếu số lượng = 0, không cần mua hàng, đặt trạng thái "Hoàn thành"
                        anyApproved = true;
                        if (isNhapKhoRequest)
                        {
                            vtPhieuNhap.TrangThai = "Hoàn thành";
                            _context.vtphieunhapkho.Update(vtPhieuNhap);
                        }
                        else
                        {
                            vatTu.NgayDuyet = DateTime.Now;
                            vatTu.TrangThai = "Hoàn thành";
                            vatTu.GhiChu = ghiChu;
                            _context.vtyeucau.Update(vatTu);
                            
                            // Lưu thông tin người duyệt vào bảng yeucau
                            var yeucauHoanThanh = yeucau ?? _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
                            if (yeucauHoanThanh != null)
                            {
                                var maNguoiDuyet = HttpContext.Session.GetString("MaNguoidung");
                                if (!string.IsNullOrWhiteSpace(maNguoiDuyet))
                                {
                                    yeucauHoanThanh.NguoiDuyet = maNguoiDuyet;
                                    yeucauHoanThanh.NgayDuyet = DateTime.Now;
                                    _context.yeucau.Update(yeucauHoanThanh);
                                }
                            }
                        }
                        processedCount++;
                        continue;
                    }

                    if (isApproved)
                    {
                        anyApproved = true;
                        // Duyệt vật tư
                        if (isNhapKhoRequest)
                        {
                            // Với yêu cầu nhập kho, cập nhật trạng thái trong vtphieunhapkho
                            vtPhieuNhap.TrangThai = "Chờ nhập kho";
                            // Note: vtphieunhapkho không có thuộc tính GhiChu
                            _context.vtphieunhapkho.Update(vtPhieuNhap);
                        }
                        else
                        {
                            // Với yêu cầu xuất kho, cập nhật trạng thái trong vtyeucau
                            vatTu.NgayDuyet = DateTime.Now;
                            vatTu.GhiChu = ghiChu; // Lưu ghi chú khi duyệt

                            // Lưu thông tin người duyệt vào bảng yeucau
                            var yeucauCheckbox = yeucau ?? _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
                            if (yeucauCheckbox != null)
                            {
                                var maNguoiDuyet = HttpContext.Session.GetString("MaNguoidung");
                                if (!string.IsNullOrWhiteSpace(maNguoiDuyet))
                                {
                                    yeucauCheckbox.NguoiDuyet = maNguoiDuyet;
                                    yeucauCheckbox.NgayDuyet = DateTime.Now;
                                    _context.yeucau.Update(yeucauCheckbox);
                                }
                            }

                            bool laDuAn = !string.IsNullOrWhiteSpace(yeucauCheckbox?.YCMaDuan);

                            // =========================
                            // 1️⃣ YÊU CẦU CÁ NHÂN
                            // =========================
                            if (!laDuAn)
                            {
                                var khotong = _context.khotongs.FirstOrDefault(kt =>
                                    kt.Makho == vatTu.YCMakho &&
                                    kt.MaSanpham == vatTu.MaSanpham)
                                    ?? _context.khotongs.FirstOrDefault(kt =>
                                        kt.MaSanpham == vatTu.MaSanpham);

                                if (khotong != null && (khotong.SL ?? 0) > 0)
                                {
                                    vatTu.TrangThai = "Chờ xuất kho";
                                }
                                else
                                {
                                    vatTu.TrangThai = "Đang mua hàng";
                                }

                                _context.vtyeucau.Update(vatTu);
                                processedCount++;
                                continue;
                            }

                            // =========================
                            // YÊU CẦU DỰ ÁN
                            // =========================
                            // KIỂM TRA: Nếu cùng dự án và cùng mã yêu cầu (base code),
                            // và số lượng mới bằng số lượng đã cấp trước đó → trạng thái "Hoàn thành"
                            if (yeucauCheckbox != null &&
                                !string.IsNullOrWhiteSpace(yeucauCheckbox.YCMaDuan) &&
                                !string.IsNullOrWhiteSpace(vatTu.MaSanpham))
                            {
                                // Tính base mã yêu cầu để kiểm tra
                                string maYeuCauChuan = NormalizeMaYeucauBase(MaYeucau);

                                // Lấy tất cả mã yêu cầu cùng base code (trừ yêu cầu hiện tại)
                                var allRelatedMaYeucau = _context.yeucau
                                    .Where(y => !string.IsNullOrWhiteSpace(y.MaYeucau) &&
                                                y.YCMaDuan == yeucauCheckbox.YCMaDuan &&
                                                y.MaYeucau != MaYeucau)
                                    .ToList()
                                    .Where(y => string.Equals(
                                        NormalizeMaYeucauBase(y.MaYeucau),
                                        maYeuCauChuan,
                                        StringComparison.OrdinalIgnoreCase))
                                    .Select(y => y.MaYeucau)
                                    .ToList();

                                // Tính số lượng đã cấp từ các yêu cầu trước đó cùng base code
                                // Lấy số lượng từ vật tư yêu cầu đã được duyệt (có NgayDuyet - đã được giám đốc duyệt)
                                // Tính MAX số lượng của các vật tư cùng mã sản phẩm trong các yêu cầu trước đó
                                // ⭐ SỬA BUG: Loại trừ các yêu cầu và vật tư đã bị từ chối
                                int soLuongDaCapTuVTYeucau = 0;
                                if (allRelatedMaYeucau.Any())
                                {
                                    // Lấy danh sách các mã yêu cầu đã bị từ chối
                                    var maYeucauBiTuChoi = _context.yeucau
                                        .Where(y => allRelatedMaYeucau.Contains(y.MaYeucau) &&
                                                   !string.IsNullOrEmpty(y.TrangThai) &&
                                                   y.TrangThai.Contains("Đã từ chối", StringComparison.OrdinalIgnoreCase))
                                        .Select(y => y.MaYeucau)
                                        .ToList();

                                    // ⭐ CHỈ TÍNH TỪ CÁC VẬT TƯ ĐÃ CÓ TRẠNG THÁI "Đã xuất kho" (hoặc tương đương)
                                    var trangThaiDaXuatKho = new[] { "Đã xuất kho", "Hoàn thành", "Đã lấy hàng" };
                                    var vatTuYeuCauTruocDo = _context.vtyeucau
                                        .Where(vt => allRelatedMaYeucau.Contains(vt.VTMaYeucau) &&
                                                     vt.MaSanpham == vatTu.MaSanpham &&
                                                     vt.NgayDuyet.HasValue &&
                                                     !maYeucauBiTuChoi.Contains(vt.VTMaYeucau) && // ⭐ Loại trừ yêu cầu đã bị từ chối
                                                     !string.IsNullOrEmpty(vt.TrangThai) &&
                                                     !vt.TrangThai.Contains("Đã từ chối", StringComparison.OrdinalIgnoreCase) && // ⭐ Loại trừ vật tư đã bị từ chối
                                                     trangThaiDaXuatKho.Contains(vt.TrangThai)) // ⭐ CHỈ tính từ vật tư đã xuất kho
                                        .ToList();

                                    if (vatTuYeuCauTruocDo.Any())
                                    {
                                        soLuongDaCapTuVTYeucau = vatTuYeuCauTruocDo
                                            .Select(vt => Math.Max(vt.SLMoi ?? 0, vt.SL ?? 0))
                                            .DefaultIfEmpty(0)
                                            .Max();
                                    }
                                }

                                // Tính số lượng đã cấp từ phiếu xuất kho (nếu có)
                                var trangThaiDaCap = new[]
                                {
                                    "Đã xác nhận nhận hàng",
                                    "Hoàn thành",
                                    "Đã xuất kho",
                                    "Đã lấy hàng",
                                    "Chờ người yêu cầu xác nhận",
                                    "Đang chuẩn bị hàng"
                                };
                                var danhSachVTDaXuatHopLe = _context.vtphieuxuatkho
                                    .Where(vt => trangThaiDaCap.Contains(vt.TrangThai))
                                    .ToList();
                                var danhSachVTDaNhapTra = _context.vtphieunhapkho
                                    .Where(vt => vt.TrangThai == "Đã nhập kho")
                                    .ToList();
                                int soLuongDaCapTuPhieuXuat = TinhSoLuongDaCapThucTe(
                                    maYeuCauChuan,
                                    vatTu.MaSanpham ?? "",
                                    danhSachVTDaXuatHopLe,
                                    danhSachVTDaNhapTra);

                                // Số lượng đã cấp = MAX(số lượng từ vtyeucau, số lượng từ phiếu xuất kho)
                                int soLuongDaCap = Math.Max(soLuongDaCapTuVTYeucau, soLuongDaCapTuPhieuXuat);

                                // Lấy số lượng yêu cầu hiện tại
                                int soLuongYeuCauHienTai = vatTu.SLMoi ?? vatTu.SL ?? 0;

                                Console.WriteLine($"[DEBUG XuLyVatTuYeucauWithCheckbox] Vật tư {vatTu.MaSanpham}: SLMoi={vatTu.SLMoi}, SL={vatTu.SL}, soLuongYeuCauHienTai={soLuongYeuCauHienTai}, soLuongDaCap={soLuongDaCap} (từ VTYeucau: {soLuongDaCapTuVTYeucau}, từ PhieuXuat: {soLuongDaCapTuPhieuXuat}), maYeuCauChuan={maYeuCauChuan}, allRelatedMaYeucau.Count={allRelatedMaYeucau.Count}");

                                // Nếu số lượng mới bằng số lượng đã cấp → trạng thái "Hoàn thành"
                                if (soLuongYeuCauHienTai == soLuongDaCap && soLuongDaCap > 0)
                                {
                                    Console.WriteLine($"ℹ️ [XuLyVatTuYeucauWithCheckbox] Số lượng yêu cầu mới ({soLuongYeuCauHienTai}) bằng số lượng đã cấp ({soLuongDaCap}). Đặt trạng thái 'Hoàn thành' cho vật tư {vatTu.MaSanpham}");

                                    vatTu.TrangThai = "Hoàn thành";
                                    _context.vtyeucau.Update(vatTu);
                                    // Không cần kiểm tra tồn kho nữa
                                    processedCount++;
                                    continue;
                                }
                            }

                            // Nếu không rơi vào trường hợp số lượng mới = số lượng đã cấp
                            // → kiểm tra tồn kho như bình thường
                            var khotongDuAn = _context.khotongs.FirstOrDefault(kt =>
                                kt.Makho == vatTu.YCMakho &&
                                kt.MaSanpham == vatTu.MaSanpham)
                                ?? _context.khotongs.FirstOrDefault(kt =>
                                    kt.MaSanpham == vatTu.MaSanpham);

                            if (khotongDuAn != null && khotongDuAn.SL > 0)
                            {
                                // Tính số lượng hàng đã cam kết từ các phiếu xuất khác (FIFO)
                                int soLuongDaCamKet = TinhSoLuongDaCamKet(khotongDuAn.Makho ?? "", khotongDuAn.MaSanpham ?? "", DateTime.Now, null);
                                int soLuongKhaDung = (khotongDuAn.SL ?? 0) - soLuongDaCamKet;
                                int soLuongYeuCauCheck = vatTu.SL ?? 0;

                                if (soLuongKhaDung >= soLuongYeuCauCheck)
                                {
                                    vatTu.TrangThai = "Chờ xuất kho";
                                }
                                else
                                {
                                    vatTu.TrangThai = "Đang mua hàng";
                                }
                            }
                            else
                            {
                                vatTu.TrangThai = "Đang mua hàng";
                            }
                            _context.vtyeucau.Update(vatTu);
                        }
                    }
                    else
                    {
                        // Từ chối vật tư
                        if (isNhapKhoRequest)
                        {
                            // Với yêu cầu nhập kho, cập nhật trạng thái trong vtphieunhapkho
                            vtPhieuNhap.TrangThai = "Giám đốc - Đã từ chối";
                            // Note: vtphieunhapkho không có thuộc tính GhiChu, ghi chú có thể lưu trong trạng thái hoặc bỏ qua
                            _context.vtphieunhapkho.Update(vtPhieuNhap);
                        }
                        else
                        {
                            // Với yêu cầu xuất kho, cập nhật trạng thái trong vtyeucau
                            vatTu.NgayDuyet = DateTime.Now;
                            vatTu.TrangThai = "Giám đốc - Đã từ chối";
                            vatTu.GhiChu = ghiChu; // Lưu ghi chú khi từ chối
                            _context.vtyeucau.Update(vatTu);
                            
                            // Lưu thông tin người từ chối vào bảng yeucau
                            var yeucauRejectCheckbox = yeucau ?? _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
                            if (yeucauRejectCheckbox != null)
                            {
                                var maNguoiDuyet = HttpContext.Session.GetString("MaNguoidung");
                                if (!string.IsNullOrWhiteSpace(maNguoiDuyet))
                                {
                                    yeucauRejectCheckbox.NguoiDuyet = maNguoiDuyet;
                                    yeucauRejectCheckbox.NgayDuyet = DateTime.Now;
                                    _context.yeucau.Update(yeucauRejectCheckbox);
                                }
                            }
                        }
                    }

                    processedCount++;
                }

                _context.SaveChanges();

                // Xử lý tạo phiếu và cập nhật trạng thái yêu cầu
                // Tái sử dụng biến yeucau đã khai báo ở phần đồng bộ trạng thái
                if (yeucau != null)
                {
                    if (isNhapKhoRequest)
                    {
                        // Xử lý yêu cầu nhập kho
                        var vtPhieuNhapListAfter = (from vtnk in _context.vtphieunhapkho
                                                   join pnk in _context.phieunhapkho on vtnk.MaNhapkho equals pnk.MaNhapkho
                                                   where pnk.MaYeucau == MaYeucau
                                                   select vtnk).ToList();

                        var approvedVTNhap = vtPhieuNhapListAfter.Where(v => v.TrangThai == "Chờ nhập kho").ToList();
                        var rejectedVTNhap = vtPhieuNhapListAfter.Where(v => 
                            !string.IsNullOrEmpty(v.TrangThai) && v.TrangThai.Contains("Đã từ chối")).ToList();

                        // Cập nhật trạng thái yêu cầu
                        if (rejectedVTNhap.Any() && rejectedVTNhap.Count == vtPhieuNhapListAfter.Count)
                        {
                            yeucau.TrangThai = "Giám đốc - Đã từ chối";
                            _context.yeucau.Update(yeucau);
                            _context.SaveChanges();
                            
                            // Gửi email thông báo từ chối cho người yêu cầu
                            System.Diagnostics.Debug.WriteLine($"[Giamdoc/XuLyVatTuYeucauWithCheckbox] Tất cả vật tư nhập kho bị từ chối, gửi email. MaYeucau={MaYeucau}");
                            SendRejectionEmailAsync(MaYeucau, "");
                        }
                        else if (approvedVTNhap.Any())
                        {
                            yeucau.TrangThai = "Chờ nhập kho";
                            _context.yeucau.Update(yeucau);
                            _context.SaveChanges();
                        }
                        else
                        {
                            _context.yeucau.Update(yeucau);
                            _context.SaveChanges();
                        }

                        // Tạo/cập nhật phiếu nhập kho nếu có vật tư được duyệt
                        if (approvedVTNhap.Any())
                        {
                            var existingPhieuNhap = _context.phieunhapkho
                                .FirstOrDefault(p => p.MaYeucau == MaYeucau && p.TrangThai != "Đã nhập kho");
                            
                            string maNhapkhoToUse = "";
                            if (existingPhieuNhap != null)
                            {
                                maNhapkhoToUse = existingPhieuNhap.MaNhapkho;
                                existingPhieuNhap.TrangThai = "Chờ nhập kho";
                                _context.phieunhapkho.Update(existingPhieuNhap);
                            }
                            else
                            {
                                int stt = 1;
                                while (true)
                                {
                                    maNhapkhoToUse = $"PNK{stt}";
                                    if (_context.phieunhapkho.FirstOrDefault(p => p.MaNhapkho == maNhapkhoToUse) == null)
                                        break;
                                    stt++;
                                }
                                
                                var newPhieuNhap = new phieunhapkho
                                {
                                    MaNhapkho = maNhapkhoToUse,
                                    MaYeucau = MaYeucau,
                                    MaDuan = yeucau.YCMaDuan,
                                    MaNguoidung = yeucau.YCMaNguoidung,
                                    NgayNhapkho = null,
                                    TrangThai = "Chờ nhập kho"
                                };
                                _context.phieunhapkho.Add(newPhieuNhap);
                            }
                            
                            _context.SaveChanges();
                            
                            // Cập nhật MaNhapkho cho các vật tư đã duyệt (vật tư đã có trong vtphieunhapkho, chỉ cần cập nhật MaNhapkho)
                            foreach (var vt in approvedVTNhap)
                            {
                                vt.MaNhapkho = maNhapkhoToUse;
                                _context.vtphieunhapkho.Update(vt);
                            }
                            _context.SaveChanges();
                        }
                    }
                    else
                    {
                        // Xử lý yêu cầu xuất kho (logic cũ)
                        var vatTuListAfter = _context.vtyeucau
                            .Where(v => v.VTMaYeucau == MaYeucau).ToList();

                        var approvedVatTu = vatTuListAfter.Where(v =>
                            v.TrangThai == "Chờ xuất kho" ||
                            v.TrangThai == "Đã xuất kho" ||
                            v.TrangThai == "Đã duyệt" ||
                            v.TrangThai == "Đang mua hàng").ToList();

                        if (approvedVatTu.Any())
                        {
                            var approvedMaSanphamList = approvedVatTu.Select(v => v.MaSanpham).ToList();

                            // Lưu danh sách phiếu trước khi xử lý để so sánh sau (giống luồng XuLyYeucau)
                            var phieuXuatKhoTruoc = _context.phieuxuatkho
                                .Where(p => p.MaYeucau == MaYeucau)
                                .Select(p => p.MaXuatkho)
                                .ToList();
                            var phieuMuaHangTruoc = _context.phieumuahang
                                .Where(p => p.MaYeucau == MaYeucau)
                                .Select(p => p.MaMuahang)
                                .ToList();

                            XuliphieuyeucauPartial(MaYeucau, approvedMaSanphamList);
                            _context.SaveChanges();

                            // Sau khi tạo phiếu, kiểm tra phiếu mới để gửi email cho Kho / Mua hàng
                            var phieuXuatKhoSau = _context.phieuxuatkho
                                .Where(p => p.MaYeucau == MaYeucau)
                                .Select(p => p.MaXuatkho)
                                .ToList();
                            var phieuMuaHangSau = _context.phieumuahang
                                .Where(p => p.MaYeucau == MaYeucau)
                                .Select(p => p.MaMuahang)
                                .ToList();

                            var phieuXuatKhoMoi = phieuXuatKhoSau.Except(phieuXuatKhoTruoc).ToList();
                            if (phieuXuatKhoMoi.Any())
                            {
                                var maYeuCauForEmail = MaYeucau;
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        using (var scope = _serviceScopeFactory.CreateScope())
                                        {
                                            var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                                            await emailService.SendNotificationToWarehouseAsync(maYeuCauForEmail, true);
                                        }
                                    }
                                    catch (Exception exInner)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[Giamdoc/XuLyVatTuYeucauWithCheckbox] Lỗi gửi email Kho: {exInner.Message}");
                                    }
                                });
                            }

                            var phieuMuaHangMoi = phieuMuaHangSau.Except(phieuMuaHangTruoc).ToList();
                            if (phieuMuaHangMoi.Any())
                            {
                                var maYeuCauForEmail = MaYeucau;
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        using (var scope = _serviceScopeFactory.CreateScope())
                                        {
                                            var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                                            await emailService.SendNotificationToPurchasingAsync(maYeuCauForEmail);
                                        }
                                    }
                                    catch (Exception exInner)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[Giamdoc/XuLyVatTuYeucauWithCheckbox] Lỗi gửi email Mua hàng: {exInner.Message}");
                                    }
                                });
                            }

                            _context.Entry(yeucau).Reload();
                            var vatTuListFinal = _context.vtyeucau
                                .Where(v => v.VTMaYeucau == MaYeucau).ToList();

                            var hasDangMuaHang = vatTuListFinal.Any(v => v.TrangThai == "Đang mua hàng");
                            var hasChoXuatKho = vatTuListFinal.Any(v => v.TrangThai == "Chờ xuất kho");
                            var hasDaXuatKho = vatTuListFinal.Any(v => v.TrangThai == "Đã xuất kho");
                            var hasDaNhapKho = vatTuListFinal.Any(v => v.TrangThai == "Đã nhập kho");
                            var hasRejected = vatTuListFinal.Any(v =>
                                !string.IsNullOrEmpty(v.TrangThai) &&
                                v.TrangThai.Contains("Đã từ chối"));

                            if (hasRejected && vatTuListFinal.All(v =>
                                !string.IsNullOrEmpty(v.TrangThai) &&
                                v.TrangThai.Contains("Đã từ chối")))
                            {
                                yeucau.TrangThai = "Giám đốc - Đã từ chối";
                                _context.yeucau.Update(yeucau);
                                _context.SaveChanges();
                                
                                // Gửi email thông báo từ chối cho người yêu cầu
                                System.Diagnostics.Debug.WriteLine($"[Giamdoc/XuLyVatTuYeucauWithCheckbox] Tất cả vật tư bị từ chối, gửi email. MaYeucau={MaYeucau}");
                                SendRejectionEmailAsync(MaYeucau, "");
                            }
                            else if (hasDaNhapKho)
                            {
                                // Nếu có vật tư đã nhập kho, kiểm tra xem tất cả vật tư đã nhập kho chưa
                                var allDaNhapKho = vatTuListFinal.All(v =>
                                    v.TrangThai == "Đã nhập kho" ||
                                    (!string.IsNullOrEmpty(v.TrangThai) && v.TrangThai.Contains("Đã từ chối")));
                                
                                if (allDaNhapKho)
                                {
                                    yeucau.TrangThai = "Đã nhập kho";
                                }
                                else
                                {
                                    // Có một số vật tư đã nhập kho nhưng chưa tất cả, kiểm tra các trạng thái khác
                                    if (hasDangMuaHang)
                                    {
                                        yeucau.TrangThai = "Đang mua hàng";
                                    }
                                    else if (hasChoXuatKho)
                                    {
                                        yeucau.TrangThai = "Chờ xuất kho";
                                    }
                                    else if (hasDaXuatKho)
                                    {
                                        yeucau.TrangThai = "Đã xuất kho";
                                    }
                                    else
                                    {
                                        yeucau.TrangThai = "Đã nhập kho";
                                    }
                                }
                            }
                            else if (hasDangMuaHang)
                            {
                                yeucau.TrangThai = "Đang mua hàng";
                            }
                            else if (hasChoXuatKho || hasDaXuatKho)
                            {
                                var allChoXuatKhoOrDaXuatKho = vatTuListFinal.All(v =>
                                    v.TrangThai == "Chờ xuất kho" ||
                                    v.TrangThai == "Đã xuất kho" ||
                                    v.TrangThai == "Hoàn thành" ||
                                    (!string.IsNullOrEmpty(v.TrangThai) && v.TrangThai.Contains("Đã từ chối")));

                                if (allChoXuatKhoOrDaXuatKho)
                                {
                                    // Nếu có vật tư chờ xuất kho thì trạng thái là "Chờ xuất kho", nếu tất cả đã xuất kho thì "Đã xuất kho"
                                    if (hasChoXuatKho && !hasDaXuatKho)
                                    {
                                        yeucau.TrangThai = "Chờ xuất kho";
                                    }
                                    else if (hasDaXuatKho && !hasChoXuatKho)
                                    {
                                        yeucau.TrangThai = "Đã xuất kho";
                                    }
                                    else
                                    {
                                        // Có cả hai, ưu tiên "Chờ xuất kho" vì còn vật tư chưa xuất
                                        yeucau.TrangThai = "Chờ xuất kho";
                                    }
                                }
                                else
                                {
                                    yeucau.TrangThai = "Đang mua hàng";
                                }
                            }
                            else if (hasRejected)
                            {
                                var allCompleted = vatTuListFinal.All(v =>
                                    v.TrangThai == "Chờ xuất kho" ||
                                    v.TrangThai == "Đã xuất kho" ||
                                    v.TrangThai == "Hoàn thành" ||
                                    (!string.IsNullOrEmpty(v.TrangThai) && v.TrangThai.Contains("Đã từ chối")));

                                if (allCompleted)
                                {
                                    // Kiểm tra xem có vật tư chờ xuất kho không
                                    var hasChoXuatKhoInRejected = vatTuListFinal.Any(v => v.TrangThai == "Chờ xuất kho");
                                    if (hasChoXuatKhoInRejected)
                                    {
                                        yeucau.TrangThai = "Chờ xuất kho";
                                    }
                                    else
                                    {
                                        yeucau.TrangThai = "Đã xuất kho";
                                    }
                                }
                                else
                                {
                                    yeucau.TrangThai = "Đang mua hàng";
                                }
                            }
                            else
                            {
                                yeucau.TrangThai = "Đang mua hàng";
                            }
                            _context.yeucau.Update(yeucau);
                            _context.SaveChanges();
                        }
                    else
                    {
                        // Nếu tất cả vật tư đều bị từ chối
                        var rejectedVatTu = vatTuListAfter.Where(v =>
                            !string.IsNullOrEmpty(v.TrangThai) &&
                            v.TrangThai.Contains("Đã từ chối")).ToList();
                        var pendingVatTu = vatTuListAfter.Where(v =>
                            string.IsNullOrWhiteSpace(v.TrangThai) ||
                            v.TrangThai == "Chờ giám đốc duyệt" ||
                            v.TrangThai == "Giám đốc").ToList();

                        if (rejectedVatTu.Any() && !pendingVatTu.Any() && !approvedVatTu.Any())
                        {
                            yeucau.TrangThai = "Giám đốc - Đã từ chối";
                            _context.yeucau.Update(yeucau);
                            _context.SaveChanges();
                            
                            // Gửi email thông báo từ chối cho người yêu cầu
                            System.Diagnostics.Debug.WriteLine($"[Giamdoc/XuLyVatTuYeucauWithCheckbox] Tất cả vật tư bị từ chối (else branch), gửi email. MaYeucau={MaYeucau}, rejectedVatTu.Count={rejectedVatTu.Count}, pendingVatTu.Count={pendingVatTu.Count}, approvedVatTu.Count={approvedVatTu.Count}");
                            SendRejectionEmailAsync(MaYeucau, "");
                        }
                    }
                    }
                }

                // Kiểm tra cuối cùng: nếu trạng thái yêu cầu là "Giám đốc - Đã từ chối" nhưng chưa gửi email
                // Điều này đảm bảo email được gửi trong mọi trường hợp khi trạng thái là "Giám đốc - Đã từ chối"
                var yeucauFinal = _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
                if (yeucauFinal != null && yeucauFinal.TrangThai == "Giám đốc - Đã từ chối")
                {
                    // Kiểm tra xem có vật tư nào bị từ chối không
                    var vatTuFinalList = _context.vtyeucau
                        .Where(v => v.VTMaYeucau == MaYeucau && v.TrangThai != null && v.TrangThai.Contains("Đã từ chối"))
                        .ToList();
                    
                    if (vatTuFinalList.Any())
                    {
                        System.Diagnostics.Debug.WriteLine($"[Giamdoc/XuLyVatTuYeucauWithCheckbox] Kiểm tra cuối: Trạng thái yêu cầu là 'Giám đốc - Đã từ chối', gửi email. MaYeucau={MaYeucau}");
                        SendRejectionEmailAsync(MaYeucau, "");
                    }
                }

                // Gửi thông báo phản hồi cho người yêu cầu khi Giám đốc có duyệt ít nhất một vật tư (checkbox)
                // (Trước đó logic này đang thiếu nên người yêu cầu không nhận được thông báo như các luồng Trưởng BP/QLDA)
                if (processedCount > 0 && anyApproved)
                {
                    var yeucauForNotif = _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
                    if (yeucauForNotif != null &&
                        !string.IsNullOrWhiteSpace(yeucauForNotif.NguoiYeucau) &&
                        !string.Equals(yeucauForNotif.TrangThai, "Giám đốc - Đã từ chối", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            var maYeucauForEmail = MaYeucau;
                            var nguoiYeuCauForEmail = yeucauForNotif.NguoiYeucau ?? "";
                            var trangThaiThongBao = !string.IsNullOrWhiteSpace(yeucauForNotif.TrangThai)
                                ? $"Đã được Giám đốc duyệt - {yeucauForNotif.TrangThai}"
                                : "Đã được Giám đốc duyệt";

                            _ = Task.Run(async () =>
                            {
                                try
                                {
                                    using (var scope = _serviceScopeFactory.CreateScope())
                                    {
                                        var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                                        await emailService.SendNotificationToEmployeeAsync(
                                            maYeucauForEmail,
                                            nguoiYeuCauForEmail,
                                            trangThaiThongBao
                                        );
                                    }
                                }
                                catch (Exception exInner)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[Giamdoc/XuLyVatTuYeucauWithCheckbox] Lỗi gửi email phản hồi duyệt: {exInner.Message}");
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[Giamdoc/XuLyVatTuYeucauWithCheckbox] Lỗi khởi tạo gửi email phản hồi duyệt: {ex.Message}");
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
                                           List<int?> SLCu, List<int?> SLMoi, List<string> VTNgayCanHang, List<string> GhiChu,
                                           List<string> DonVi, string MaYeucau, string action, phieuxuatkho phieuxuatkho, vtphieuxuatkho vtphieuxuatkho, phieumuahang phieumuahang, vtphieumuahang vtphieumuahang)
        {
            // Kiểm tra null để tránh lỗi khi upload file Excel lớn
            if (yeucau == null)
            {
                yeucau = new yeucau();
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

                if (duan != null)
                {
                    string maNguoiQLDA = duan.MaNguoiQLDA;
                    if (maNv2 == maNguoiQLDA)
                    {
                        if (chucVu2 == "Trưởng BP")
                        {
                            yeucau.TrangThai = "Giám đốc";
                        }
                        else if (chucVu2 == "Giám đốc")
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
                        else if (chucVu2 == "Giám đốc")
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
                    else if (chucVu2 == "Giám đốc")
                    {
                        yeucau.TrangThai = "Đã duyệt";

                    }
                }

                // ================== TẠO MÃ YÊU CẦU ĐÚNG CHUẨN ==================
                
                // Kiểm tra xem có file Excel được upload không
                bool hasExcelFile = false;
                
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
                        hasExcelFile = true; // Đánh dấu có file Excel được upload
                        try
                        {
                            
                            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(excelFile.FileName);
                            
                            
                            fileNameWithoutExt = fileNameWithoutExt.Replace('_', ' ');
                            
                           
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
                    
                    // Đảm bảo tính duy nhất
                    int suffixNumber = 1;
                    while (true)
                    {
                        var exists = _context.yeucau
                                             .FirstOrDefault(x => x.MaYeucau == yeucau.MaYeucau);
                        if (exists == null)
                        {
                            break;
                        }
                        // Nếu trùng, thêm số suffix
                        yeucau.MaYeucau = $"{maDuanFormatted} {stPart} {tenVietTat}{suffixNumber}";
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
                    
                    // Đảm bảo tính duy nhất
                    int suffixNumber = 1;
                    while (true)
                    {
                        var exists = _context.yeucau
                                             .FirstOrDefault(x => x.MaYeucau == yeucau.MaYeucau);
                        if (exists == null)
                        {
                            break;
                        }
                        // Nếu trùng, thêm số suffix
                        yeucau.MaYeucau = $"{datePart} {stPart} {tenVietTat}{suffixNumber}";
                        suffixNumber++;
                    }
                }
                // ================================================================

                // Nếu Giám đốc upload Excel và trạng thái là "Đã duyệt", tự động set NgayDuyet và NguoiDuyet
                if (hasExcelFile && chucVu2 == "Giám đốc" && yeucau.TrangThai == "Đã duyệt")
                {
                    yeucau.NgayDuyet = DateTime.Now;
                    yeucau.NguoiDuyet = "Giám đốc";
                }

                // Đảm bảo danh sách không null để tránh lỗi khi submit từ Excel
                YCMaKho ??= new List<string>();
                TenSanpham ??= new List<string>();
                MaSanpham ??= new List<string>();
                HangSX ??= new List<string>();
                NhaCC ??= new List<string>();
                SL ??= new List<int?>();
                SLCu ??= new List<int?>();
                SLMoi ??= new List<int?>();
                VTNgayCanHang ??= new List<string>();
                GhiChu ??= new List<string>();
                DonVi ??= new List<string>();

                _context.yeucau.Add(yeucau);
                _context.SaveChanges();

                var itemCount = new[] { YCMaKho.Count, TenSanpham.Count, MaSanpham.Count, HangSX.Count, NhaCC.Count, SL.Count, SLCu.Count, SLMoi.Count, VTNgayCanHang.Count, GhiChu.Count, DonVi.Count }.Max();

                for (int i = 0; i < itemCount; i++)
                {
                    var tenSanPham = i < TenSanpham.Count ? TenSanpham[i] : null;
                    if (string.IsNullOrWhiteSpace(tenSanPham))
                    {
                        continue;
                    }

                    var maKhoItem = i < YCMaKho.Count ? YCMaKho[i] : null;
                    var maSanPhamItem = i < MaSanpham.Count ? MaSanpham[i] : null;
                    var slCuValue = (SLCu != null && i < SLCu.Count) ? SLCu[i] : null;
                    var slMoiValue = (SLMoi != null && i < SLMoi.Count) ? SLMoi[i] : null;
                    
                    // Bỏ qua dòng nếu số lượng mới bằng 0 (không cần lưu và hiển thị)
                    if (slMoiValue == 0)
                    {
                        continue;
                    }
                    
                    var ghiChuValue = (GhiChu != null && i < GhiChu.Count) ? GhiChu[i] : null;
                    DateTime? ngayCanHang = null;
                    if (VTNgayCanHang != null && i < VTNgayCanHang.Count && !string.IsNullOrWhiteSpace(VTNgayCanHang[i]))
                    {
                        if (DateTime.TryParse(VTNgayCanHang[i], out var parsedDate))
                        {
                            ngayCanHang = parsedDate;
                        }
                    }

                    // Ưu tiên kho gửi lên theo từng dòng, nếu thiếu thì dò theo mã sản phẩm
                    var khoMatch = !string.IsNullOrWhiteSpace(maKhoItem)
                        ? _context.khotongs.FirstOrDefault(p => p.Makho == maKhoItem)
                        : null;
                    if (khoMatch == null && !string.IsNullOrWhiteSpace(maSanPhamItem))
                    {
                        khoMatch = _context.khotongs.FirstOrDefault(p => p.MaSanpham == maSanPhamItem);
                    }
                    // Tính số lượng lưu: ưu tiên SLMoi -> SLCu -> SL
                    int slValue = slMoiValue ?? slCuValue ?? ((SL != null && i < SL.Count) ? (SL[i] ?? 0) : 0);

                    if (khoMatch != null)
                    {
                        var newVtyeucau = new vtyeucau();
                        newVtyeucau.VTMaYeucau = yeucau.MaYeucau;
                        newVtyeucau.TenSanpham = tenSanPham;
                        newVtyeucau.MaSanpham = maSanPhamItem;
                        newVtyeucau.HangSX = (i < HangSX.Count) ? HangSX[i] : null;
                        newVtyeucau.NhaCC = (i < NhaCC.Count) ? NhaCC[i] : null;
                        newVtyeucau.SLCu = slCuValue;
                        newVtyeucau.SLMoi = slMoiValue;
                        newVtyeucau.SL = slValue;
                        newVtyeucau.DonVi = (i < DonVi.Count) ? DonVi[i] : null;
                        newVtyeucau.YCMakho = khoMatch.Makho;
                        newVtyeucau.NgayCanHang = ngayCanHang;
                        newVtyeucau.NgayNhapkho = khoMatch.NgayNhapkho;
                        newVtyeucau.NgayBaohanh = khoMatch.NgayBaohanh;
                        newVtyeucau.ThoiGianBH = khoMatch.ThoiGianBH;
                        newVtyeucau.GhiChu = ghiChuValue;
                        _context.vtyeucau.Add(newVtyeucau);
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

                        var newVtyeucau = new vtyeucau();
                        newVtyeucau.VTMaYeucau = yeucau.MaYeucau;
                        newVtyeucau.TenSanpham = tenSanPham;
                        newVtyeucau.MaSanpham = maSanPhamItem;
                        newVtyeucau.HangSX = (i < HangSX.Count) ? HangSX[i] : null;
                        newVtyeucau.NhaCC = (i < NhaCC.Count) ? NhaCC[i] : null;
                        newVtyeucau.SLCu = slCuValue;
                        newVtyeucau.SLMoi = slMoiValue;
                        newVtyeucau.SL = slValue;
                        newVtyeucau.DonVi = (i < DonVi.Count) ? DonVi[i] : null;
                        newVtyeucau.YCMakho = "VT mới";
                        newVtyeucau.NgayCanHang = ngayCanHang;
                        newVtyeucau.NgayNhapkho = null;
                        newVtyeucau.NgayBaohanh = null;
                        newVtyeucau.ThoiGianBH = null;
                        newVtyeucau.GhiChu = ghiChuValue;
                        _context.vtyeucau.Add(newVtyeucau);
                    }
                    _context.SaveChanges();
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

            return RedirectToAction("Yeucau", "Yeucau", new { area = "Giamdoc" });

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
                // Kiểm tra trạng thái hiện tại của yêu cầu
                // Nếu trạng thái là "Chờ giám đốc duyệt" hoặc "Giám đốc" và người duyệt là Giám đốc, thì duyệt luôn
                // Không đặt trạng thái thành "Đã duyệt", để Xuliphieuyeucau xử lý (sẽ tự động chuyển sang "Đang mua hàng" hoặc "Đã xuất kho")
                if ((Yeucau.TrangThai == "Chờ giám đốc duyệt" || Yeucau.TrangThai == "Giám đốc") && chucVu2 == "Giám đốc")
                {
                    // Lưu danh sách phiếu trước khi xử lý để so sánh sau
                    var phieuXuatKhoTruoc = _context.phieuxuatkho.Where(p => p.MaYeucau == Yeucau.MaYeucau).Select(p => p.MaXuatkho).ToList();
                    var phieuMuaHangTruoc = _context.phieumuahang.Where(p => p.MaYeucau == Yeucau.MaYeucau).Select(p => p.MaMuahang).ToList();
                    
                    // Không đặt trạng thái thành "Đã duyệt", để logic cũ xử lý
                    Xuliphieuyeucau(Yeucau.MaYeucau, phieuxuatkho, vtphieuxuatkho, phieumuahang, vtphieumuahang, yeucau, vtyeucau);
                    
                    // Kiểm tra phiếu mới được tạo sau khi duyệt
                    var phieuXuatKhoSau = _context.phieuxuatkho.Where(p => p.MaYeucau == Yeucau.MaYeucau).Select(p => p.MaXuatkho).ToList();
                    var phieuMuaHangSau = _context.phieumuahang.Where(p => p.MaYeucau == Yeucau.MaYeucau).Select(p => p.MaMuahang).ToList();
                    
                    // Gửi email nếu có phiếu xuất kho mới
                    var phieuXuatKhoMoi = phieuXuatKhoSau.Except(phieuXuatKhoTruoc).ToList();
                    if (phieuXuatKhoMoi.Any())
                    {
                        var maYeuCauForEmail = Yeucau.MaYeucau;
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                using (var scope = _serviceScopeFactory.CreateScope())
                                {
                                    var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                                    await emailService.SendNotificationToWarehouseAsync(maYeuCauForEmail, true);
                                }
                            }
                            catch (Exception exInner)
                            {
                                System.Diagnostics.Debug.WriteLine($"[Giamdoc/XuLyYeucau] Lỗi gửi email Kho: {exInner.Message}");
                            }
                        });
                    }
                    
                    // Gửi email nếu có phiếu mua hàng mới
                    var phieuMuaHangMoi = phieuMuaHangSau.Except(phieuMuaHangTruoc).ToList();
                    if (phieuMuaHangMoi.Any())
                    {
                        var maYeuCauForEmail = Yeucau.MaYeucau;
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                using (var scope = _serviceScopeFactory.CreateScope())
                                {
                                    var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                                    await emailService.SendNotificationToPurchasingAsync(maYeuCauForEmail);
                                }
                            }
                            catch (Exception exInner)
                            {
                                System.Diagnostics.Debug.WriteLine($"[Giamdoc/XuLyYeucau] Lỗi gửi email Mua hàng: {exInner.Message}");
                            }
                        });
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
                        }
                        else
                        {
                            // Lưu danh sách phiếu trước khi xử lý để so sánh sau
                            var phieuXuatKhoTruoc = _context.phieuxuatkho.Where(p => p.MaYeucau == Yeucau.MaYeucau).Select(p => p.MaXuatkho).ToList();
                            var phieuMuaHangTruoc = _context.phieumuahang.Where(p => p.MaYeucau == Yeucau.MaYeucau).Select(p => p.MaMuahang).ToList();
                            
                            Yeucau.TrangThai = "Đã duyệt";
                            Xuliphieuyeucau(Yeucau.MaYeucau, phieuxuatkho, vtphieuxuatkho, phieumuahang, vtphieumuahang, yeucau, vtyeucau);
                            
                            // Kiểm tra phiếu mới được tạo sau khi duyệt
                            var phieuXuatKhoSau = _context.phieuxuatkho.Where(p => p.MaYeucau == Yeucau.MaYeucau).Select(p => p.MaXuatkho).ToList();
                            var phieuMuaHangSau = _context.phieumuahang.Where(p => p.MaYeucau == Yeucau.MaYeucau).Select(p => p.MaMuahang).ToList();
                            
                            // Gửi email nếu có phiếu xuất kho mới
                            var phieuXuatKhoMoi = phieuXuatKhoSau.Except(phieuXuatKhoTruoc).ToList();
                            if (phieuXuatKhoMoi.Any())
                            {
                                var maYeuCauForEmail = Yeucau.MaYeucau;
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        using (var scope = _serviceScopeFactory.CreateScope())
                                        {
                                            var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                                            await emailService.SendNotificationToWarehouseAsync(maYeuCauForEmail, true);
                                        }
                                    }
                                    catch (Exception exInner)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[Giamdoc/XuLyYeucau] Lỗi gửi email Kho: {exInner.Message}");
                                    }
                                });
                            }
                            
                            // Gửi email nếu có phiếu mua hàng mới
                            var phieuMuaHangMoi = phieuMuaHangSau.Except(phieuMuaHangTruoc).ToList();
                            if (phieuMuaHangMoi.Any())
                            {
                                var maYeuCauForEmail = Yeucau.MaYeucau;
                                _ = Task.Run(async () =>
                                {
                                    try
                                    {
                                        using (var scope = _serviceScopeFactory.CreateScope())
                                        {
                                            var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                                            await emailService.SendNotificationToPurchasingAsync(maYeuCauForEmail);
                                        }
                                    }
                                    catch (Exception exInner)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"[Giamdoc/XuLyYeucau] Lỗi gửi email Mua hàng: {exInner.Message}");
                                    }
                                });
                            }
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
                                // Lưu danh sách phiếu trước khi xử lý để so sánh sau
                                var phieuXuatKhoTruoc = _context.phieuxuatkho.Where(p => p.MaYeucau == Yeucau.MaYeucau).Select(p => p.MaXuatkho).ToList();
                                var phieuMuaHangTruoc = _context.phieumuahang.Where(p => p.MaYeucau == Yeucau.MaYeucau).Select(p => p.MaMuahang).ToList();
                                
                                // Không đặt trạng thái thành "Đã duyệt", để logic cũ xử lý
                                Xuliphieuyeucau(Yeucau.MaYeucau, phieuxuatkho, vtphieuxuatkho, phieumuahang, vtphieumuahang, yeucau, vtyeucau);
                                
                                // Kiểm tra phiếu mới được tạo sau khi duyệt
                                var phieuXuatKhoSau = _context.phieuxuatkho.Where(p => p.MaYeucau == Yeucau.MaYeucau).Select(p => p.MaXuatkho).ToList();
                                var phieuMuaHangSau = _context.phieumuahang.Where(p => p.MaYeucau == Yeucau.MaYeucau).Select(p => p.MaMuahang).ToList();
                                
                                // Gửi email nếu có phiếu xuất kho mới
                                var phieuXuatKhoMoi = phieuXuatKhoSau.Except(phieuXuatKhoTruoc).ToList();
                                if (phieuXuatKhoMoi.Any())
                                {
                                    var maYeuCauForEmail = Yeucau.MaYeucau;
                                    _ = Task.Run(async () =>
                                    {
                                        try
                                        {
                                            using (var scope = _serviceScopeFactory.CreateScope())
                                            {
                                                var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                                                await emailService.SendNotificationToWarehouseAsync(maYeuCauForEmail, true);
                                            }
                                        }
                                        catch (Exception exInner)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[Giamdoc/XuLyYeucau] Lỗi gửi email Kho: {exInner.Message}");
                                        }
                                    });
                                }
                                
                                // Gửi email nếu có phiếu mua hàng mới
                                var phieuMuaHangMoi = phieuMuaHangSau.Except(phieuMuaHangTruoc).ToList();
                                if (phieuMuaHangMoi.Any())
                                {
                                    var maYeuCauForEmail = Yeucau.MaYeucau;
                                    _ = Task.Run(async () =>
                                    {
                                        try
                                        {
                                            using (var scope = _serviceScopeFactory.CreateScope())
                                            {
                                                var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                                                await emailService.SendNotificationToPurchasingAsync(maYeuCauForEmail);
                                            }
                                        }
                                        catch (Exception exInner)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[Giamdoc/XuLyYeucau] Lỗi gửi email Mua hàng: {exInner.Message}");
                                        }
                                    });
                                }
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
                                // Lưu danh sách phiếu trước khi xử lý để so sánh sau
                                var phieuXuatKhoTruoc = _context.phieuxuatkho.Where(p => p.MaYeucau == Yeucau.MaYeucau).Select(p => p.MaXuatkho).ToList();
                                var phieuMuaHangTruoc = _context.phieumuahang.Where(p => p.MaYeucau == Yeucau.MaYeucau).Select(p => p.MaMuahang).ToList();
                                
                                // Không đặt trạng thái thành "Đã duyệt", để logic cũ xử lý
                                Xuliphieuyeucau(Yeucau.MaYeucau, phieuxuatkho, vtphieuxuatkho, phieumuahang, vtphieumuahang, yeucau, vtyeucau);
                                
                                // Kiểm tra phiếu mới được tạo sau khi duyệt
                                var phieuXuatKhoSau = _context.phieuxuatkho.Where(p => p.MaYeucau == Yeucau.MaYeucau).Select(p => p.MaXuatkho).ToList();
                                var phieuMuaHangSau = _context.phieumuahang.Where(p => p.MaYeucau == Yeucau.MaYeucau).Select(p => p.MaMuahang).ToList();
                                
                                // Gửi email nếu có phiếu xuất kho mới
                                var phieuXuatKhoMoi = phieuXuatKhoSau.Except(phieuXuatKhoTruoc).ToList();
                                if (phieuXuatKhoMoi.Any())
                                {
                                    var maYeuCauForEmail = Yeucau.MaYeucau;
                                    _ = Task.Run(async () =>
                                    {
                                        try
                                        {
                                            using (var scope = _serviceScopeFactory.CreateScope())
                                            {
                                                var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                                                await emailService.SendNotificationToWarehouseAsync(maYeuCauForEmail, true);
                                            }
                                        }
                                        catch (Exception exInner)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[Giamdoc/XuLyYeucau] Lỗi gửi email Kho: {exInner.Message}");
                                        }
                                    });
                                }
                                
                                // Gửi email nếu có phiếu mua hàng mới
                                var phieuMuaHangMoi = phieuMuaHangSau.Except(phieuMuaHangTruoc).ToList();
                                if (phieuMuaHangMoi.Any())
                                {
                                    var maYeuCauForEmail = Yeucau.MaYeucau;
                                    _ = Task.Run(async () =>
                                    {
                                        try
                                        {
                                            using (var scope = _serviceScopeFactory.CreateScope())
                                            {
                                                var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                                                await emailService.SendNotificationToPurchasingAsync(maYeuCauForEmail);
                                            }
                                        }
                                        catch (Exception exInner)
                                        {
                                            System.Diagnostics.Debug.WriteLine($"[Giamdoc/XuLyYeucau] Lỗi gửi email Mua hàng: {exInner.Message}");
                                        }
                                    });
                                }
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

            return RedirectToAction("Yeucau", "Yeucau", new { area = "Giamdoc" });
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
                return RedirectToAction("Yeucau", "Yeucau", new { area = "Giamdoc" });
            }

            var maYeuCauChuan = NormalizeMaYeucauBase(Mayeucau);
            var trangThaiDaCap = new[]
            {
                "Hoàn thành",
                "Đã xuất kho",
                "Đã lấy hàng",
                "Chờ người yêu cầu xác nhận",
                "Đang chuẩn bị hàng"
            };
            var danhSachVTDaXuatHopLe = _context.vtphieuxuatkho
                .Where(vt => trangThaiDaCap.Contains(vt.TrangThai))
                .ToList();
            var danhSachVTDaNhapTra = _context.vtphieunhapkho
                .Where(vt => vt.TrangThai == "Đã nhập kho")
                .ToList();
            var danhSachVatTuCanNhapTra = new List<(vtyeucau VatTu, int SoLuongTra)>();

            var makhoList = danhSachVatTuYC.Select(vt => vt.YCMakho).ToList();

            var DanhsachVTYCkhotong = _context.khotongs
                                               .Where(kt => makhoList.Contains(kt.Makho))
                                               .ToList();


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

            // Kiểm tra xem có vật tư nào có trạng thái "Đã xuất kho" nhưng chưa có trong vtphieuxuatkho không
            var vatTuDaXuatKhoChuaCoPhieu = danhSachVatTuYC.Any(vt =>
                vt.TrangThai == "Đã xuất kho" &&
                !_context.vtphieuxuatkho.Any(vtx => vtx.MaYeucau == Mayeucau && vtx.MaSanpham == vt.MaSanpham));
            
            if (vatTuDaXuatKhoChuaCoPhieu)
            {
                isPhieuXuatKhoCreated = true;
            }

            foreach (var VattuYC in danhSachVatTuYC)
            {
                // ⚠️ Bỏ qua vật tư đã có trạng thái "Hoàn thành" (không cần xử lý thêm)
                if (VattuYC.TrangThai == "Hoàn thành")
                {
                    Console.WriteLine($"ℹ️ [Xuliphieuyeucau - Phần đầu] Bỏ qua vật tư {VattuYC.MaSanpham} vì đã có trạng thái 'Hoàn thành'");
                    continue;
                }
                
                var soLuongMoi = VattuYC.SLMoi ?? VattuYC.SL ?? 0;
                var soLuongDaCap = TinhSoLuongDaCapThucTe(maYeuCauChuan, VattuYC.MaSanpham ?? "", danhSachVTDaXuatHopLe, danhSachVTDaNhapTra);
                var soLuongThieuTinhToan = soLuongMoi - soLuongDaCap;

                if (soLuongThieuTinhToan <= 0)
                {
                    continue;
                }

                // Kiểm tra xem vật tư này đã được xử lý chưa (đã có trong bất kỳ phiếu xuất kho hoặc phiếu mua hàng nào của yêu cầu này)
                var existingVTPhieuXuatKho = _context.vtphieuxuatkho
                    .Any(vt => vt.MaYeucau == Mayeucau && vt.MaSanpham == VattuYC.MaSanpham);
                var existingVTPhieuMuaHang = _context.vtphieumuahang
                    .Any(vt => vt.MaYeucau == Mayeucau && vt.MaSanpham == VattuYC.MaSanpham);

                // Nếu vật tư đã được xử lý, bỏ qua
                if (existingVTPhieuXuatKho || existingVTPhieuMuaHang)
                {
                    continue;
                }

                var khotong = DanhsachVTYCkhotong.FirstOrDefault(kt => kt.Makho == VattuYC.YCMakho && kt.MaSanpham == VattuYC.MaSanpham);

                if (khotong != null)
                {
                    // Tính số lượng hàng đã cam kết từ các phiếu xuất khác (chưa có phiếu hiện tại nên không cần loại trừ)
                    int soLuongDaCamKet = TinhSoLuongDaCamKet(khotong.Makho ?? "", khotong.MaSanpham ?? "", null);
                    
                    // Số lượng khả dụng = Tồn kho - Số lượng đã cam kết
                    int soLuongKhaDung = (khotong.SL ?? 0) - soLuongDaCamKet;
                    int soLuongYeuCau = soLuongThieuTinhToan;

                    if (soLuongKhaDung > 0 && soLuongKhaDung < soLuongYeuCau)
                    {
                        // Trường hợp số lượng khả dụng nhỏ hơn số lượng yêu cầu
                        Console.WriteLine($"Đã chạy: Số lượng khả dụng nhỏ hơn số lượng yêu cầu (Makho: {khotong.Makho}, SL tồn: {khotong.SL}, SL đã cam kết: {soLuongDaCamKet}, SL khả dụng: {soLuongKhaDung}, SL yêu cầu: {soLuongYeuCau})");
                        isPhieuXuatKhoCreated = true;
                        isPhieuMuaHangCreated = true;
                    }
                    else if (soLuongKhaDung == 0)
                    {
                        // Trường hợp số lượng khả dụng bằng 0
                        Console.WriteLine($"Đã chạy: Số lượng khả dụng bằng 0 (Makho: {khotong.Makho}, SL tồn: {khotong.SL}, SL đã cam kết: {soLuongDaCamKet}, SL yêu cầu: {soLuongYeuCau})");
                        isPhieuMuaHangCreated = true;
                    }
                    else if (soLuongKhaDung >= soLuongYeuCau)
                    {
                        // Trường hợp số lượng khả dụng đủ đáp ứng
                        Console.WriteLine($"Đã chạy: Số lượng khả dụng đủ đáp ứng (Makho: {khotong.Makho}, SL tồn: {khotong.SL}, SL đã cam kết: {soLuongDaCamKet}, SL khả dụng: {soLuongKhaDung}, SL yêu cầu: {soLuongYeuCau})");
                        isPhieuXuatKhoCreated = true;
                    }
                    else
                    {
                        // Trường hợp số lượng khả dụng < 0 (tồn kho < đã cam kết) - cần mua hàng
                        Console.WriteLine($"Đã chạy: Số lượng khả dụng âm (Makho: {khotong.Makho}, SL tồn: {khotong.SL}, SL đã cam kết: {soLuongDaCamKet}, SL yêu cầu: {VattuYC.SL})");
                        isPhieuMuaHangCreated = true;
                    }
                }
                else
                {
                    // Trường hợp không tìm thấy kho tổng
                    Console.WriteLine($"Đã chạy: Không tìm thấy kho tổng phù hợp cho Makho: {VattuYC.YCMakho}");
                    // Không có kho tổng nhưng vẫn phải mua → đảm bảo tạo phiếu mua hàng
                    isPhieuMuaHangCreated = true;
                }
            }
            if ((isPhieuMuaHangCreated == true) && (isPhieuXuatKhoCreated == true))
            {
                var Phieuxuatkho = new phieuxuatkho
                {
                    MaXuatkho = Maxuatkho,
                    MaYeucau = thongTinYeuCau.MaYeucau,
                    MaDuan = thongTinYeuCau.YCMaDuan,
                    MaNguoidung = thongTinYeuCau.YCMaNguoidung,
                    NgayXuatkho = null,
                    TrangThai = "Đang chuẩn bị hàng"
                };
                _context.Add(Phieuxuatkho);
                Console.WriteLine($"Đã tạo phiếu xuất kho: MaXuatkho = {Maxuatkho}");

                var Phieumuahang = new phieumuahang
                {
                    MaMuahang = Mamuahang,
                    MaYeucau = thongTinYeuCau.MaYeucau,
                    MaDuan = thongTinYeuCau.YCMaDuan,
                    NgayMuahang = null,
                    MaNguoidung = thongTinYeuCau.YCMaNguoidung,
                    TrangThai = "Đang chờ báo giá"
                };
                _context.Add(Phieumuahang);
                Console.WriteLine($"Đã tạo phiếu mua hàng: MaMuahang = {Mamuahang}");
            }
            else if (isPhieuMuaHangCreated == true && isPhieuXuatKhoCreated == false)
            {
                var Phieumuahang = new phieumuahang
                {
                    MaMuahang = Mamuahang,
                    MaYeucau = thongTinYeuCau.MaYeucau,
                    MaDuan = thongTinYeuCau.YCMaDuan,
                    NgayMuahang = null,
                    MaNguoidung = thongTinYeuCau.YCMaNguoidung,
                    TrangThai = "Đang chờ báo giá"
                };
                _context.Add(Phieumuahang);
                Console.WriteLine($"Đã tạo phiếu mua hàng: MaMuahang = {Mamuahang}");
            }
            else if (isPhieuXuatKhoCreated == true && isPhieuMuaHangCreated == false)
            {
                var Phieuxuatkho = new phieuxuatkho
                {
                    MaXuatkho = Maxuatkho,
                    MaYeucau = thongTinYeuCau.MaYeucau,
                    MaDuan = thongTinYeuCau.YCMaDuan,
                    MaNguoidung = thongTinYeuCau.YCMaNguoidung,
                    NgayXuatkho = null,
                    TrangThai = "Đang chuẩn bị hàng"
                };
                _context.Add(Phieuxuatkho);
                Console.WriteLine($"Đã tạo phiếu xuất kho: MaXuatkho = {Maxuatkho}");
            }

            _context.SaveChanges();
            Console.WriteLine("Đã lưu thay đổi vào cơ sở dữ liệu.");

            // Sắp xếp theo NgayDuyet (FIFO - ai duyệt trước được ưu tiên cấp kho trước)
            // Vật tư có NgayDuyet null hoặc chưa duyệt sẽ được xử lý sau
            var danhSachVatTuYCSapXep = danhSachVatTuYC
                .OrderBy(vt => vt.NgayDuyet.HasValue ? 0 : 1) // Vật tư đã duyệt (có NgayDuyet) xử lý trước
                .ThenBy(vt => vt.NgayDuyet ?? DateTime.MaxValue) // Trong các vật tư đã duyệt, sắp xếp theo thời gian duyệt (FIFO)
                .ToList();

            // Biến để lưu số lượng đã cam kết từ các vật tư đã xử lý trong cùng yêu cầu (theo từng kho và sản phẩm)
            var soLuongDaCamKetTrongYeuCau = new Dictionary<string, int>();

            foreach (var VattuYC in danhSachVatTuYCSapXep)
            {
                // ⚠️ Bỏ qua vật tư đã có trạng thái "Hoàn thành" (không cần xử lý thêm)
                if (VattuYC.TrangThai == "Hoàn thành")
                {
                    Console.WriteLine($"ℹ️ [Xuliphieuyeucau] Bỏ qua vật tư {VattuYC.MaSanpham} vì đã có trạng thái 'Hoàn thành'");
                    continue;
                }
                
                var soLuongMoi = VattuYC.SLMoi ?? VattuYC.SL ?? 0;
                var soLuongDaCap = TinhSoLuongDaCapThucTe(maYeuCauChuan, VattuYC.MaSanpham ?? "", danhSachVTDaXuatHopLe, danhSachVTDaNhapTra);
                var soLuongThieuTinhToan = soLuongMoi - soLuongDaCap;

                // ⚠️ XỬ LÝ TRƯỜNG HỢP: Số lượng mới bằng số lượng đã cấp trước đó → không cần làm gì
                if (soLuongThieuTinhToan == 0)
                {
                    Console.WriteLine($"ℹ️ [Xuliphieuyeucau] Số lượng yêu cầu mới ({soLuongMoi}) bằng số lượng đã cấp ({soLuongDaCap}). Không cần mua/xuất kho cho vật tư {VattuYC.MaSanpham}");
                    
                    // Đặt trạng thái "Hoàn thành" vì không cần mua/xuất kho
                    if (VattuYC.TrangThai != "Hoàn thành" && VattuYC.TrangThai != "Đã xuất kho")
                    {
                        VattuYC.TrangThai = "Hoàn thành";
                    }
                    _context.vtyeucau.Update(VattuYC);
                    continue;
                }

                if (soLuongThieuTinhToan < 0)
                {
                    var soLuongTra = Math.Abs(soLuongThieuTinhToan);
                    danhSachVatTuCanNhapTra.Add((VattuYC, soLuongTra));
                    // Đã xuất đủ hoặc nhiều hơn, trạng thái là "Đã xuất kho"
                    if (VattuYC.TrangThai != "Đã xuất kho")
                    {
                        VattuYC.TrangThai = "Đã xuất kho";
                    }
                    _context.vtyeucau.Update(VattuYC);
                    continue;
                }

                int soLuongYeuCauThucTe = soLuongThieuTinhToan;

                // Kiểm tra xem vật tư này đã được xử lý chưa (đã có trong bất kỳ phiếu xuất kho hoặc phiếu mua hàng nào của yêu cầu này)
                var existingVTPhieuXuatKho = _context.vtphieuxuatkho
                    .Any(vt => vt.MaYeucau == Mayeucau && vt.MaSanpham == VattuYC.MaSanpham);
                var existingVTPhieuMuaHang = _context.vtphieumuahang
                    .Any(vt => vt.MaYeucau == Mayeucau && vt.MaSanpham == VattuYC.MaSanpham);

                // Nếu vật tư đã được xử lý, bỏ qua
                if (existingVTPhieuXuatKho || existingVTPhieuMuaHang)
                {
                    continue;
                }

                // Tìm vật tư trong kho tổng: ưu tiên khớp cả Makho và MaSanpham, nếu không có thì tìm theo MaSanpham
                var khotong = _context.khotongs.FirstOrDefault(kt => 
                    kt.Makho == VattuYC.YCMakho && 
                    kt.MaSanpham == VattuYC.MaSanpham)
                    ?? _context.khotongs.FirstOrDefault(kt => 
                        kt.MaSanpham == VattuYC.MaSanpham);

                if (khotong != null && khotong.SL > 0)
                {
                    // FIFO: Tính số lượng hàng đã cam kết từ các phiếu xuất khác (chỉ tính vật tư duyệt trước)
                    int soLuongDaCamKetTuYeuCauKhac = TinhSoLuongDaCamKet(khotong.Makho ?? "", khotong.MaSanpham ?? "", VattuYC.NgayDuyet, Maxuatkho);
                    
                    // Tính số lượng đã cam kết từ các vật tư đã xử lý TRƯỚC ĐÓ trong cùng yêu cầu
                    // (các vật tư có NgayDuyet < NgayDuyet hiện tại)
                    string keyKhoSanPham = $"{khotong.Makho ?? ""}_{khotong.MaSanpham ?? ""}";
                    int soLuongDaCamKetTrongCungYeuCau = soLuongDaCamKetTrongYeuCau.GetValueOrDefault(keyKhoSanPham, 0);
                    
                    // Tổng số lượng đã cam kết = từ yêu cầu khác (FIFO) + từ yêu cầu hiện tại
                    int tongSoLuongDaCamKet = soLuongDaCamKetTuYeuCauKhac + soLuongDaCamKetTrongCungYeuCau;
                    
                    // Số lượng khả dụng = Tồn kho - Tổng số lượng đã cam kết
                    int soLuongKhaDung = (khotong.SL ?? 0) - tongSoLuongDaCamKet;
                    int soLuongXuat = Math.Max(0, Math.Min(soLuongKhaDung, soLuongYeuCauThucTe));
                    int soLuongThieu = soLuongYeuCauThucTe - soLuongXuat;

                    if (soLuongXuat > 0 && isPhieuXuatKhoCreated == true)
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
                            SL = soLuongXuat
                        };
                        _context.Add(VTPhieuxuatkho);
                        
                        // Cập nhật số lượng đã cam kết trong cùng yêu cầu (để vật tư tiếp theo tính đúng)
                        soLuongDaCamKetTrongYeuCau[keyKhoSanPham] = soLuongDaCamKetTrongCungYeuCau + soLuongXuat;
                    }

                    if (soLuongThieu > 0)
                    {
                        // Không thêm vật tư có trạng thái "Hoàn thành" vào phiếu mua hàng
                        if (VattuYC.TrangThai != "Hoàn thành")
                        {
                            // Nếu có số lượng thiếu, chỉ cập nhật nếu chưa có trạng thái "Chờ xuất kho", "Đã xuất kho" hoặc "Đang mua hàng"
                            if (VattuYC.TrangThai != "Chờ xuất kho" && VattuYC.TrangThai != "Đã xuất kho" && VattuYC.TrangThai != "Đang mua hàng")
                            {
                                VattuYC.TrangThai = "Đang mua hàng";
                            }
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
                                SL = soLuongThieu,
                                NgayBaohanh = VattuYC.NgayBaohanh,
                                ThoiGianBH = VattuYC.ThoiGianBH,
                                TrangThai = "Đang chờ báo giá"
                            };

                            _context.Add(VTPhieumuahang);
                        }
                    }
                    else
                    {
                        // Không có số lượng thiếu, nhưng giữ nguyên trạng thái "Chờ xuất kho" - chỉ chuyển sang "Đã xuất kho" khi kho xác nhận
                        // Nếu đã tạo phiếu xuất kho (soLuongXuat > 0), trạng thái phải là "Chờ xuất kho"
                        if (soLuongXuat > 0)
                        {
                            // Nếu chưa có trạng thái "Chờ xuất kho" hoặc "Đã xuất kho", đặt thành "Chờ xuất kho"
                            if (VattuYC.TrangThai != "Chờ xuất kho" && VattuYC.TrangThai != "Đã xuất kho")
                            {
                                VattuYC.TrangThai = "Chờ xuất kho";
                            }
                        }
                        // Nếu đang ở trạng thái "Chờ xuất kho", giữ nguyên - kho sẽ chuyển sang "Đã xuất kho" khi xác nhận
                    }

                    _context.vtyeucau.Update(VattuYC);
                    // KHÔNG cập nhật khotong ở đây - chỉ cập nhật khi người nhận xác nhận đã nhận hàng
                }
                else
                {
                    // Không thêm vật tư có trạng thái "Hoàn thành" vào phiếu mua hàng
                    if (VattuYC.TrangThai != "Hoàn thành")
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
                            SL = soLuongYeuCauThucTe,
                            NgayBaohanh = VattuYC.NgayBaohanh,
                            ThoiGianBH = VattuYC.ThoiGianBH,
                            TrangThai = "Đang chờ báo giá"
                        };

                        _context.vtyeucau.Update(VattuYC);
                        _context.Add(VTPhieumuahang);
                    }
                }
            }

            if (danhSachVatTuCanNhapTra.Any())
            {
                // ⭐ Tìm người đã nhận hàng từ phiếu xuất kho đầu tiên (người thực sự đã lấy hàng)
                // Ví dụ: Quỳnh yêu cầu 10 cái → đã xuất cho Quỳnh → Quỳnh phải trả
                string maNguoiTraHang = thongTinYeuCau.YCMaNguoidung; // Mặc định
                string tenNguoiTraHang = thongTinYeuCau.NguoiYeucau ?? "";
                
                // Tìm phiếu xuất kho đầu tiên của yêu cầu này để lấy người đã nhận hàng
                // Tìm theo MaYeucau chính xác hoặc theo mã yêu cầu cơ bản (maYeuCauChuan)
                var allMaYeucauLienQuan = _context.yeucau
                    .Where(y => !string.IsNullOrWhiteSpace(y.MaYeucau))
                    .ToList()
                    .Where(y => 
                    {
                        // Lấy mã cơ bản bằng cách bỏ phần tên người (phần cuối)
                        var parts = y.MaYeucau.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        string baseCode = parts.Length > 2 ? string.Join(" ", parts.Take(parts.Length - 1)) : y.MaYeucau;
                        return string.Equals(baseCode, maYeuCauChuan, StringComparison.OrdinalIgnoreCase);
                    })
                    .Select(y => y.MaYeucau)
                    .ToList();
                
                if (!allMaYeucauLienQuan.Any())
                {
                    allMaYeucauLienQuan = new List<string> { Mayeucau };
                }

                var phieuXuatKhoDauTien = _context.phieuxuatkho
                    .Where(px => allMaYeucauLienQuan.Contains(px.MaYeucau))
                    .OrderBy(px => px.NgayXuatkho ?? DateTime.MaxValue)
                    .FirstOrDefault();
                
                if (phieuXuatKhoDauTien != null && !string.IsNullOrEmpty(phieuXuatKhoDauTien.MaNguoidung))
                {
                    // Người đã nhận hàng từ phiếu xuất kho = người phải trả hàng
                    maNguoiTraHang = phieuXuatKhoDauTien.MaNguoidung;
                    var nguoiTraHang = _context.nguoidungs
                        .FirstOrDefault(n => n.MaNguoidung == maNguoiTraHang);
                    if (nguoiTraHang != null)
                    {
                        tenNguoiTraHang = nguoiTraHang.TenNguoidung ?? "";
                    }
                }
                else
                {
                    // Fallback: Lấy từ yêu cầu gốc
                    var nguoiYeuCauBanDau = _context.nguoidungs
                        .FirstOrDefault(n => n.MaNguoidung == thongTinYeuCau.YCMaNguoidung);
                    if (nguoiYeuCauBanDau != null)
                    {
                        maNguoiTraHang = thongTinYeuCau.YCMaNguoidung;
                        tenNguoiTraHang = nguoiYeuCauBanDau.TenNguoidung ?? "";
                    }
                }

                // Tạo mã yêu cầu đặc biệt cho phiếu nhập kho hoàn trả
                string maYeucauDacBiet = "";
                if (!string.IsNullOrEmpty(thongTinYeuCau.YCMaDuan))
                {
                    // Có dự án: NHAPKHO_DUAN_{MaDuan}
                    maYeucauDacBiet = $"NHAPKHO_DUAN_{thongTinYeuCau.YCMaDuan}";
                }
                else
                {
                    // Không có dự án: NHAPKHO_CANHAN_{MaNguoidung} (người trả hàng)
                    maYeucauDacBiet = $"NHAPKHO_CANHAN_{maNguoiTraHang}";
                }

                // Kiểm tra xem yeucau đặc biệt đã tồn tại chưa, nếu chưa thì tạo mới
                var existingYeucauDacBiet = _context.yeucau
                    .FirstOrDefault(y => y.MaYeucau == maYeucauDacBiet);

                if (existingYeucauDacBiet == null)
                {
                    var nguoiTraHangInfo = _context.nguoidungs
                        .FirstOrDefault(n => n.MaNguoidung == maNguoiTraHang);

                    // Kiểm tra nếu người tạo là quản lý dự án
                    string trangThaiYeucau = "Chờ giám đốc duyệt";
                    if (!string.IsNullOrEmpty(thongTinYeuCau.YCMaDuan))
                    {
                        var duan = _context.duans.FirstOrDefault(d => d.MaDuan == thongTinYeuCau.YCMaDuan);
                        string boPhanNguoiTra = nguoiTraHangInfo?.Bophan ?? "";
                        string chucVuNguoiTra = nguoiTraHangInfo?.Chucvu ?? "";
                        
                        // Kiểm tra nếu người tạo thuộc BP dự án HOẶC có chức vụ Quản lí dự án/Quản lý dự án HOẶC là MaNguoiQLDA
                        bool laNguoiThuocDuAn = (!string.IsNullOrEmpty(boPhanNguoiTra) && 
                                                 boPhanNguoiTra.Trim().Equals("BP dự án", StringComparison.OrdinalIgnoreCase)) ||
                                                (!string.IsNullOrEmpty(chucVuNguoiTra) && 
                                                 (chucVuNguoiTra.Trim().Equals("Quản lí dự án", StringComparison.OrdinalIgnoreCase) ||
                                                  chucVuNguoiTra.Trim().Equals("Quản lý dự án", StringComparison.OrdinalIgnoreCase))) ||
                                                (duan != null && !string.IsNullOrEmpty(duan.MaNguoiQLDA) && 
                                                 maNguoiTraHang.Trim().Equals(duan.MaNguoiQLDA.Trim(), StringComparison.OrdinalIgnoreCase));
                        
                        if (laNguoiThuocDuAn)
                        {
                            // Người tạo là quản lý dự án: Chờ giám đốc duyệt
                            trangThaiYeucau = "Chờ giám đốc duyệt";
                        }
                        else
                        {
                            // Người tạo không phải quản lý dự án: Chờ quản lý dự án duyệt
                            trangThaiYeucau = "Chờ quản lý dự án duyệt";
                        }
                    }

                    var newYeucauDacBiet = new yeucau
                    {
                        MaYeucau = maYeucauDacBiet,
                        TenYeucau = "Yêu cầu nhập kho",
                        YCMaNguoidung = maNguoiTraHang, 
                        NguoiYeucau = tenNguoiTraHang,
                        Bophan = nguoiTraHangInfo?.Bophan ?? "",
                        YCMaDuan = thongTinYeuCau.YCMaDuan,
                        NgayYeucau = DateTime.Now,
                        TrangThai = trangThaiYeucau
                    };
                    _context.yeucau.Add(newYeucauDacBiet);
                    _context.SaveChanges();
                }

                // Tạo mã phiếu nhập kho
                int stt = 1;
                string maNhapkhoTra;
                while (true)
                {
                    maNhapkhoTra = $"PNK{stt}";
                    var existingEntry = _context.phieunhapkho
                        .FirstOrDefault(y => y.MaNhapkho == maNhapkhoTra);
                    if (existingEntry == null)
                    {
                        break;
                    }
                    stt++;
                }

                // Tạo phiếu nhập kho hoàn trả với MaYeucau dạng NHAPKHO_DUAN hoặc NHAPKHO_CANHAN
                // Kiểm tra nếu người tạo là quản lý dự án
                string trangThaiPhieuNhap = "Chờ giám đốc duyệt";
                if (!string.IsNullOrEmpty(thongTinYeuCau.YCMaDuan))
                {
                    var duan = _context.duans.FirstOrDefault(d => d.MaDuan == thongTinYeuCau.YCMaDuan);
                    var nguoiTraHangInfo = _context.nguoidungs
                        .FirstOrDefault(n => n.MaNguoidung == maNguoiTraHang);
                    string boPhanNguoiTra = nguoiTraHangInfo?.Bophan ?? "";
                    string chucVuNguoiTra = nguoiTraHangInfo?.Chucvu ?? "";
                    
                    // Kiểm tra nếu người tạo thuộc BP dự án HOẶC có chức vụ Quản lí dự án/Quản lý dự án HOẶC là MaNguoiQLDA
                    bool laNguoiThuocDuAn = (!string.IsNullOrEmpty(boPhanNguoiTra) && 
                                             boPhanNguoiTra.Trim().Equals("BP dự án", StringComparison.OrdinalIgnoreCase)) ||
                                            (!string.IsNullOrEmpty(chucVuNguoiTra) && 
                                             (chucVuNguoiTra.Trim().Equals("Quản lí dự án", StringComparison.OrdinalIgnoreCase) ||
                                              chucVuNguoiTra.Trim().Equals("Quản lý dự án", StringComparison.OrdinalIgnoreCase))) ||
                                            (duan != null && !string.IsNullOrEmpty(duan.MaNguoiQLDA) && 
                                             maNguoiTraHang.Trim().Equals(duan.MaNguoiQLDA.Trim(), StringComparison.OrdinalIgnoreCase));
                    
                    if (laNguoiThuocDuAn)
                    {
                        // Người tạo là quản lý dự án: Chờ giám đốc duyệt
                        trangThaiPhieuNhap = "Chờ giám đốc duyệt";
                    }
                    else
                    {
                        // Người tạo không phải quản lý dự án: Chờ quản lý dự án duyệt
                        trangThaiPhieuNhap = "Chờ quản lý dự án duyệt";
                    }
                }

                var phieuNhapTra = new phieunhapkho
                {
                    MaNhapkho = maNhapkhoTra,
                    MaYeucau = maYeucauDacBiet, // Sử dụng mã yêu cầu đặc biệt
                    MaDuan = thongTinYeuCau.YCMaDuan,
                    MaNguoidung = maNguoiTraHang, // Người đã nhận hàng = người phải trả (Quỳnh)
                    NgayNhapkho = null,
                    TrangThai = trangThaiPhieuNhap
                };
                _context.phieunhapkho.Add(phieuNhapTra);
                _context.SaveChanges();

                // Thêm vật tư vào phiếu nhập kho hoàn trả
                foreach (var vtTra in danhSachVatTuCanNhapTra)
                {
                    var vtPhieuNhap = new vtphieunhapkho
                    {
                        MaNhapkho = maNhapkhoTra,
                        MaYeucau = maYeucauDacBiet, // Sử dụng mã yêu cầu đặc biệt
                        TenSanpham = vtTra.VatTu.TenSanpham,
                        MaSanpham = vtTra.VatTu.MaSanpham,
                        Makho = vtTra.VatTu.YCMakho,
                        HangSX = vtTra.VatTu.HangSX,
                        NhaCC = vtTra.VatTu.NhaCC,
                        DonVi = vtTra.VatTu.DonVi,
                        DiengiaiNhapKho = "Giảm số lượng yêu cầu", // Lý do: Giảm số lượng yêu cầu
                        NgayBaohanh = vtTra.VatTu.NgayBaohanh,
                        ThoiGianBH = vtTra.VatTu.ThoiGianBH,
                        SL = vtTra.SoLuongTra,
                        TrangThai = trangThaiPhieuNhap // Sử dụng trạng thái của phiếu (Chờ giám đốc duyệt hoặc Chờ quản lý dự án duyệt)
                    };
                    _context.vtphieunhapkho.Add(vtPhieuNhap);
                }

                // Cập nhật trạng thái yêu cầu tổng
                var hasDangMuaHang = danhSachVatTuYC.Any(vt => vt.TrangThai == "Đang mua hàng");
                var hasChoXuatKho = danhSachVatTuYC.Any(vt => vt.TrangThai == "Chờ xuất kho");
                var allDaXuatOrRejected = danhSachVatTuYC.All(vt =>
                    vt.TrangThai == "Đã xuất kho" ||
                    (!string.IsNullOrEmpty(vt.TrangThai) && vt.TrangThai.Contains("Đã từ chối")));

                if (allDaXuatOrRejected)
                {
                    thongTinYeuCau.TrangThai = "Đã xuất kho";
                }
                else if (hasDangMuaHang)
                {
                    thongTinYeuCau.TrangThai = "Đang mua hàng";
                }
                else if (hasChoXuatKho)
                {
                    thongTinYeuCau.TrangThai = "Chờ xuất kho";
                }

                _context.yeucau.Update(thongTinYeuCau);
            }

            _context.SaveChanges();


            return RedirectToAction("Yeucau", "Yeucau", new { area = "Giamdoc" });
        }

        private string NormalizeMaYeucauBase(string? maYeucau)
        {
            if (string.IsNullOrWhiteSpace(maYeucau))
            {
                return string.Empty;
            }

            var cleaned = (maYeucau ?? string.Empty).Replace("_", " ").Trim();
            var parts = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

            if (parts.Count <= 2)
            {
                return cleaned;
            }

            // Bỏ phần tên người (token cuối cùng)
            parts.RemoveAt(parts.Count - 1);
            return string.Join(" ", parts);
        }

        private int TinhSoLuongDaCapThucTe(string maYeuCauChuan, string maSanPham, List<vtphieuxuatkho> dsDaXuat, List<vtphieunhapkho> dsDaNhapTra)
        {
            if (string.IsNullOrWhiteSpace(maYeuCauChuan) || string.IsNullOrWhiteSpace(maSanPham))
            {
                return 0;
            }

            // ⭐ SỬA BUG: Loại trừ các phiếu xuất kho từ các yêu cầu đã bị từ chối
            // Lấy danh sách các mã yêu cầu đã bị từ chối
            var allRelatedMaYeucau = _context.yeucau
                .Where(y => !string.IsNullOrWhiteSpace(y.MaYeucau))
                .ToList()
                .Where(y => string.Equals(
                    NormalizeMaYeucauBase(y.MaYeucau),
                    maYeuCauChuan,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
            
            var maYeucauBiTuChoi = allRelatedMaYeucau
                .Where(y => !string.IsNullOrEmpty(y.TrangThai) && 
                           y.TrangThai.Contains("Đã từ chối", StringComparison.OrdinalIgnoreCase))
                .Select(y => y.MaYeucau)
                .ToList();

            // Lọc các phiếu xuất kho, loại trừ các phiếu từ yêu cầu đã bị từ chối
            var daXuat = dsDaXuat
                .Where(vt => NormalizeMaYeucauBase(vt.MaYeucau) == maYeuCauChuan && 
                            vt.MaSanpham == maSanPham &&
                            !maYeucauBiTuChoi.Contains(vt.MaYeucau)) // ⭐ Loại trừ yêu cầu đã bị từ chối
                .Sum(vt => vt.SL ?? 0);

            // Fallback: nếu chưa có phiếu xuất kho, lấy số lượng đã xuất/hoàn thành từ các yêu cầu liên quan
            // Tránh trường hợp yêu cầu trước đã cấp kho nhưng chưa có chi tiết vtphieuxuatkho → không tạo phiếu mua hàng thừa
            // Load về memory trước để có thể dùng NormalizeMaYeucauBase
            // ⭐ SỬA BUG: Loại trừ các vật tư yêu cầu đã bị từ chối
            var daXuatTuYeuCau = _context.vtyeucau
                .Where(vt => vt.MaSanpham == maSanPham &&
                    (vt.TrangThai == "Đã xuất kho" ||
                     vt.TrangThai == "Hoàn thành" ||
                     vt.TrangThai == "Đã lấy hàng"))
                .ToList() // Load về memory để có thể dùng NormalizeMaYeucauBase
                .Where(vt => NormalizeMaYeucauBase(vt.VTMaYeucau) == maYeuCauChuan &&
                            !string.IsNullOrEmpty(vt.TrangThai) &&
                            !vt.TrangThai.Contains("Đã từ chối", StringComparison.OrdinalIgnoreCase) && // ⭐ Loại trừ vật tư đã bị từ chối
                            !maYeucauBiTuChoi.Contains(vt.VTMaYeucau)) // ⭐ Loại trừ yêu cầu đã bị từ chối
                .Sum(vt => Math.Max(vt.SLMoi ?? 0, vt.SL ?? 0));

            var tongDaXuat = Math.Max(daXuat, daXuatTuYeuCau);

            var daTra = dsDaNhapTra
                .Where(vt => NormalizeMaYeucauBase(vt.MaYeucau) == maYeuCauChuan
                    && vt.MaSanpham == maSanPham
                    && !string.IsNullOrWhiteSpace(vt.DiengiaiNhapKho)
                    && vt.DiengiaiNhapKho.IndexOf("trả", StringComparison.OrdinalIgnoreCase) >= 0
                    && !maYeucauBiTuChoi.Contains(vt.MaYeucau)) // ⭐ Loại trừ yêu cầu đã bị từ chối
                .Sum(vt => vt.SL ?? 0);

            return Math.Max(0, tongDaXuat - daTra);
        }

        
        private (int maxHienTai, int maxTruocDo) TinhMaxYeuCauTheoBaseCode(string maYeucauHienTai, string maSanpham, string maYeuCauChuan)
        {
            if (string.IsNullOrWhiteSpace(maYeucauHienTai) || string.IsNullOrWhiteSpace(maSanpham) || string.IsNullOrWhiteSpace(maYeuCauChuan))
            {
                return (0, 0);
            }

            // Lấy tất cả mã yêu cầu có cùng base code
            var allRelatedMaYeucau = _context.yeucau
                .Where(y => !string.IsNullOrWhiteSpace(y.MaYeucau))
                .ToList()
                .Where(y => string.Equals(
                    NormalizeMaYeucauBase(y.MaYeucau),
                    maYeuCauChuan,
                    StringComparison.OrdinalIgnoreCase))
                .Select(y => y.MaYeucau)
                .ToList();

            if (!allRelatedMaYeucau.Any())
            {
                return (0, 0);
            }

            // Lấy tất cả vật tư yêu cầu cùng base code và cùng mã sản phẩm
            var allVTYeucau = _context.vtyeucau
                .Where(vt => allRelatedMaYeucau.Contains(vt.VTMaYeucau) &&
                            string.Equals(vt.MaSanpham, maSanpham, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!allVTYeucau.Any())
            {
                return (0, 0);
            }

            // Lấy vật tư yêu cầu hiện tại để lấy NgayDuyet
            var vtYeucauHienTai = allVTYeucau
                .FirstOrDefault(vt => vt.VTMaYeucau == maYeucauHienTai);
            
            if (vtYeucauHienTai == null || !vtYeucauHienTai.NgayDuyet.HasValue)
            {
                // Nếu không tìm thấy hoặc chưa có NgayDuyet, trả về 0
                return (0, 0);
            }
            
            var ngayDuyetHienTai = vtYeucauHienTai.NgayDuyet.Value;

            // Tính MAX số lượng yêu cầu hiện tại (tất cả các vật tư yêu cầu có NgayDuyet <= NgayDuyet hiện tại)
            var vtyeucauHienTai = allVTYeucau
                .Where(vt => vt.NgayDuyet.HasValue && vt.NgayDuyet.Value <= ngayDuyetHienTai)
                .ToList();
            
            int maxHienTai = 0;
            if (vtyeucauHienTai.Any())
            {
                maxHienTai = vtyeucauHienTai
                    .Select(vt => Math.Max(vt.SLMoi ?? 0, vt.SL ?? 0))
                    .DefaultIfEmpty(0)
                    .Max();
            }

            // Tính MAX số lượng yêu cầu trước đó (các vật tư yêu cầu có NgayDuyet < NgayDuyet hiện tại)
            var vtyeucauTruocDo = allVTYeucau
                .Where(vt => vt.NgayDuyet.HasValue && vt.NgayDuyet.Value < ngayDuyetHienTai)
                .ToList();

            int maxTruocDo = 0;
            if (vtyeucauTruocDo.Any())
            {
                maxTruocDo = vtyeucauTruocDo
                    .Select(vt => Math.Max(vt.SLMoi ?? 0, vt.SL ?? 0))
                    .DefaultIfEmpty(0)
                    .Max();
            }

            return (maxHienTai, maxTruocDo);
        }

        
        private string TimNguoiYeuCauBanDauCuaDuan(string maDuan, string maNguoiDungMacDinh)
        {
            if (string.IsNullOrWhiteSpace(maDuan))
            {
                return maNguoiDungMacDinh;
            }

            try
            {
                // Tìm trong lịch sử vật tư yêu cầu (vtyeucau) join với yêu cầu (yeucau) 
                // để tìm người yêu cầu đầu tiên có vật tư cho dự án này
                var yeucauBanDau = (from vt in _context.vtyeucau
                                   join yc in _context.yeucau on vt.VTMaYeucau equals yc.MaYeucau
                                   where yc.YCMaDuan == maDuan
                                         && !string.IsNullOrEmpty(yc.MaYeucau)
                                         && !yc.MaYeucau.StartsWith("NHAPKHO_", StringComparison.OrdinalIgnoreCase)
                                         && !string.IsNullOrEmpty(yc.YCMaNguoidung)
                                   orderby yc.NgayYeucau ascending
                                   select yc)
                                   .FirstOrDefault();

                if (yeucauBanDau != null && !string.IsNullOrEmpty(yeucauBanDau.YCMaNguoidung))
                {
                    return yeucauBanDau.YCMaNguoidung;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi khi tìm người yêu cầu ban đầu của dự án {maDuan}: {ex.Message}");
            }

            return maNguoiDungMacDinh;
        }

        // Method để xử lý phiếu yêu cầu cho các vật tư đã được duyệt (partial approval)
        private void XuliphieuyeucauPartial(string Mayeucau, List<string> approvedMaSanphamList)
        {
            // Lấy các vật tư đã được duyệt (bao gồm cả "Chờ xuất kho", "Đã xuất kho", "Đã duyệt" và "Đang mua hàng" - nhưng chỉ xử lý những vật tư chưa được xử lý)
            var danhSachVatTuYC = _context.vtyeucau
                                          .Where(vt => vt.VTMaYeucau == Mayeucau && 
                                                       approvedMaSanphamList.Contains(vt.MaSanpham) &&
                                                       vt.TrangThai != "Hoàn thành" && // Không xử lý lại vật tư đã Hoàn thành
                                                       (vt.TrangThai == "Chờ xuất kho" || vt.TrangThai == "Đã xuất kho" || vt.TrangThai == "Đã duyệt" || vt.TrangThai == "Đang mua hàng"))
                                          .ToList();

            var thongTinYeuCau = _context.yeucau
                                        .FirstOrDefault(yc => yc.MaYeucau == Mayeucau);

            if (thongTinYeuCau == null || danhSachVatTuYC == null || !danhSachVatTuYC.Any())
            {
                Console.WriteLine("Không tìm thấy yêu cầu hoặc danh sách vật tư đã duyệt.");
                return;
            }

            // Kiểm tra xem đã có phiếu xuất kho hoặc phiếu mua hàng cho yêu cầu này chưa
            var existingPhieuXuatKho = _context.phieuxuatkho
                .FirstOrDefault(p => p.MaYeucau == Mayeucau);
            var existingPhieuMuaHang = _context.phieumuahang
                .FirstOrDefault(p => p.MaYeucau == Mayeucau);

            // ⭐ QUAN TRỌNG: Kiểm tra xem đây có phải là yêu cầu cá nhân không (YCMaDuan = null)
            // Yêu cầu cá nhân: mỗi yêu cầu cần có phiếu xuất kho riêng, không dùng phiếu cũ
            bool laYeuCauCaNhan = string.IsNullOrWhiteSpace(thongTinYeuCau?.YCMaDuan);

            string Maxuatkho = null;
            string Mamuahang = null;

            // ⭐ Với yêu cầu cá nhân: luôn tạo phiếu xuất kho mới (không dùng phiếu cũ)
            // Với yêu cầu dự án: dùng phiếu cũ nếu có, hoặc tạo mới nếu chưa có
            if (laYeuCauCaNhan || existingPhieuXuatKho == null)
            {
                // Tạo mã phiếu xuất kho mới
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
            }
            else
            {
                // Yêu cầu dự án: dùng phiếu cũ nếu có
                Maxuatkho = existingPhieuXuatKho.MaXuatkho;
            }

            // Nếu chưa có phiếu mua hàng, tạo mã mới
            if (existingPhieuMuaHang == null)
            {
                int Numberpmh = 1;
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
            }
            else
            {
                Mamuahang = existingPhieuMuaHang.MaMuahang;
            }

            var makhoList = danhSachVatTuYC.Select(vt => vt.YCMakho).ToList();
            var DanhsachVTYCkhotong = _context.khotongs
                                               .Where(kt => makhoList.Contains(kt.Makho))
                                               .ToList();

            // ⭐ QUAN TRỌNG: Với yêu cầu cá nhân, không áp dụng logic MAX/base code
            // Mỗi yêu cầu cá nhân cần có phiếu xuất kho riêng, không gộp với yêu cầu khác
            bool coNhieuYeuCauCungBaseCode = false;
            
            // Khai báo các biến với giá trị mặc định (sẽ chỉ được sử dụng cho yêu cầu dự án)
            string maYeuCauChuan = NormalizeMaYeucauBase(Mayeucau);
            List<string> allRelatedMaYeucau = new List<string>();
            
            // Chỉ áp dụng logic MAX/base code cho yêu cầu dự án
            if (!laYeuCauCaNhan)
            {
                // Tính base mã yêu cầu để kiểm tra xem có nhiều yêu cầu cùng base code không
                maYeuCauChuan = NormalizeMaYeucauBase(Mayeucau);
                
                // Lấy tất cả mã yêu cầu cùng base code để kiểm tra
                allRelatedMaYeucau = _context.yeucau
                    .Where(y => !string.IsNullOrWhiteSpace(y.MaYeucau) && 
                               !string.IsNullOrWhiteSpace(y.YCMaDuan) && // Chỉ lấy yêu cầu dự án
                               y.YCMaDuan == thongTinYeuCau.YCMaDuan) // Cùng dự án
                    .ToList()
                    .Where(y => string.Equals(
                        NormalizeMaYeucauBase(y.MaYeucau),
                        maYeuCauChuan,
                        StringComparison.OrdinalIgnoreCase))
                    .Select(y => y.MaYeucau)
                    .ToList();

                // ⭐ QUAN TRỌNG: Kiểm tra xem đây có phải là yêu cầu đầu tiên được duyệt không
                // Nếu đã có yêu cầu khác cùng base code đã có phiếu xuất kho/mua hàng thì đây là yêu cầu thứ 2, 3, 4... trở đi
                bool coYeuCauTruocDoDaXuLy = allRelatedMaYeucau
                    .Where(maYC => maYC != Mayeucau) // Loại trừ yêu cầu hiện tại
                    .Any(maYC => _context.phieuxuatkho.Any(px => px.MaYeucau == maYC) || 
                                 _context.phieumuahang.Any(pm => pm.MaYeucau == maYC));

                // Áp dụng logic MAX khi có >= 2 yêu cầu cùng base code VÀ đã có yêu cầu khác được xử lý trước đó
                // Logic này hoạt động cho 2, 3, 4, ... yêu cầu cùng base code (chỉ cho yêu cầu dự án)
                coNhieuYeuCauCungBaseCode = allRelatedMaYeucau.Count >= 2 && coYeuCauTruocDoDaXuLy;
            }

            bool isPhieuXuatKhoCreated = false;
            bool isPhieuMuaHangCreated = false;

            // Kiểm tra xem có cần tạo phiếu xuất kho hoặc phiếu mua hàng không
            foreach (var VattuYC in danhSachVatTuYC)
            {
                // Bỏ qua vật tư có trạng thái "Hoàn thành" - không tạo phiếu mua hàng cho vật tư này
                if (VattuYC.TrangThai == "Hoàn thành")
                {
                    continue;
                }

                // Kiểm tra xem vật tư này đã được xử lý chưa (đã có trong bất kỳ phiếu xuất kho hoặc phiếu mua hàng nào của yêu cầu này)
                var existingVTPhieuXuatKho = _context.vtphieuxuatkho
                    .Any(vt => vt.MaYeucau == Mayeucau && vt.MaSanpham == VattuYC.MaSanpham);
                var existingVTPhieuMuaHang = _context.vtphieumuahang
                    .Any(vt => vt.MaYeucau == Mayeucau && vt.MaSanpham == VattuYC.MaSanpham);

                // Nếu vật tư đã được xử lý, bỏ qua
                if (existingVTPhieuXuatKho || existingVTPhieuMuaHang)
                {
                    continue;
                }

                var khotong = DanhsachVTYCkhotong.FirstOrDefault(kt => kt.Makho == VattuYC.YCMakho && kt.MaSanpham == VattuYC.MaSanpham);

                if (khotong != null && khotong.SL > 0)
                {
                    // LOGIC CŨ: Tính toán bình thường (cho yêu cầu đầu tiên hoặc khi chỉ có 1 yêu cầu)
                    // FIFO: chỉ tính vật tư duyệt trước thời điểm duyệt hiện tại
                    int soLuongDaCamKet = TinhSoLuongDaCamKet(khotong.Makho ?? "", khotong.MaSanpham ?? "", VattuYC.NgayDuyet, Maxuatkho);
                    int soLuongKhaDung = (khotong.SL ?? 0) - soLuongDaCamKet;
                    int soLuongYeuCau = VattuYC.SL ?? 0;

                    if (soLuongKhaDung > 0 && soLuongKhaDung < soLuongYeuCau)
                    {
                        isPhieuXuatKhoCreated = true;
                        isPhieuMuaHangCreated = true;
                    }
                    else if (soLuongKhaDung == 0)
                    {
                        isPhieuMuaHangCreated = true;
                    }
                    else if (soLuongKhaDung >= soLuongYeuCau)
                    {
                        isPhieuXuatKhoCreated = true;
                    }
                    else
                    {
                        isPhieuMuaHangCreated = true;
                    }
                }
                else
                {
                    // Không có trong kho tổng, cần mua hàng
                    isPhieuMuaHangCreated = true;
                }

                // Nếu vật tư đã được đánh dấu "Đã xuất kho" ở bước duyệt thì chắc chắn cần phiếu xuất kho
                if (VattuYC.TrangThai == "Chờ xuất kho" || VattuYC.TrangThai == "Đã xuất kho")
                {
                    isPhieuXuatKhoCreated = true;
                }
            }

            // Tạo phiếu xuất kho nếu cần
            // ⭐ Với yêu cầu cá nhân: luôn tạo phiếu mới (không kiểm tra existingPhieuXuatKho)
            // Với yêu cầu dự án: chỉ tạo khi chưa có phiếu
            if (isPhieuXuatKhoCreated && (laYeuCauCaNhan || existingPhieuXuatKho == null))
            {
                var Phieuxuatkho = new phieuxuatkho
                {
                    MaXuatkho = Maxuatkho,
                    MaYeucau = thongTinYeuCau.MaYeucau,
                    MaDuan = thongTinYeuCau.YCMaDuan,
                    MaNguoidung = thongTinYeuCau.YCMaNguoidung,
                    NgayXuatkho = null,
                    TrangThai = "Đang chuẩn bị hàng"
                };
                _context.Add(Phieuxuatkho);
                Console.WriteLine($"Đã tạo phiếu xuất kho: MaXuatkho = {Maxuatkho} cho yêu cầu {Mayeucau} ({(laYeuCauCaNhan ? "Cá nhân" : "Dự án")})");
            }

            // Tạo phiếu mua hàng nếu cần
            if (isPhieuMuaHangCreated && existingPhieuMuaHang == null)
            {
                var Phieumuahang = new phieumuahang
                {
                    MaMuahang = Mamuahang,
                    MaYeucau = thongTinYeuCau.MaYeucau,
                    MaDuan = thongTinYeuCau.YCMaDuan,
                    NgayMuahang = null,
                    MaNguoidung = thongTinYeuCau.YCMaNguoidung,
                    TrangThai = "Đang chờ báo giá"
                };
                _context.Add(Phieumuahang);
                Console.WriteLine($"Đã tạo phiếu mua hàng: MaMuahang = {Mamuahang}");
            }

            _context.SaveChanges();

            // Sắp xếp theo NgayDuyet (FIFO - ai duyệt trước được ưu tiên cấp kho trước)
            // Vật tư có NgayDuyet null hoặc chưa duyệt sẽ được xử lý sau
            var danhSachVatTuYCSapXep = danhSachVatTuYC
                .OrderBy(vt => vt.NgayDuyet.HasValue ? 0 : 1) // Vật tư đã duyệt (có NgayDuyet) xử lý trước
                .ThenBy(vt => vt.NgayDuyet ?? DateTime.MaxValue) // Trong các vật tư đã duyệt, sắp xếp theo thời gian duyệt (FIFO)
                .ToList();

            // Biến để lưu số lượng đã cam kết từ các vật tư đã xử lý trong cùng yêu cầu (theo từng kho và sản phẩm)
            var soLuongDaCamKetTrongYeuCau = new Dictionary<string, int>();

            // Danh sách vật tư cần nhập trả khi yêu cầu sau nhỏ hơn yêu cầu trước
            var danhSachVatTuCanNhapTra = new List<(vtyeucau VatTu, int SoLuongTra, int MaxTruocDo, int MaxHienTai)>();

            // Xử lý từng vật tư đã duyệt (theo thứ tự FIFO)
            foreach (var VattuYC in danhSachVatTuYCSapXep)
            {
                // Bỏ qua vật tư có trạng thái "Hoàn thành" - không tạo phiếu mua hàng cho vật tư này
                if (VattuYC.TrangThai == "Hoàn thành")
                {
                    continue;
                }

                // Kiểm tra xem vật tư này đã được xử lý chưa (đã có trong bất kỳ phiếu xuất kho hoặc phiếu mua hàng nào của yêu cầu này)
                var existingVTPhieuXuatKho = _context.vtphieuxuatkho
                    .Any(vt => vt.MaYeucau == Mayeucau && vt.MaSanpham == VattuYC.MaSanpham);
                var existingVTPhieuMuaHang = _context.vtphieumuahang
                    .Any(vt => vt.MaYeucau == Mayeucau && vt.MaSanpham == VattuYC.MaSanpham);

                if (existingVTPhieuXuatKho || existingVTPhieuMuaHang)
                {
                    continue;
                }

                var khotong = _context.khotongs.FirstOrDefault(kt => 
                    kt.Makho == VattuYC.YCMakho && 
                    kt.MaSanpham == VattuYC.MaSanpham)
                    ?? _context.khotongs.FirstOrDefault(kt => 
                        kt.MaSanpham == VattuYC.MaSanpham);

                if (khotong != null && khotong.SL > 0)
                {
                    // ⭐ LOGIC MỚI: Tính số lượng mua/cấp theo MAX nếu có nhiều yêu cầu cùng base code
                    int soLuongYeuCauThucTe = 0;
                    int soLuongMua = 0;
                    int soLuongThieu = 0;
                    int soLuongXuat = 0;
                    
                    // Khai báo keyKhoSanPham để sử dụng ở phần sau
                    string keyKhoSanPham = $"{khotong.Makho ?? ""}_{khotong.MaSanpham ?? ""}";
                    int soLuongDaCamKetTrongCungYeuCau = soLuongDaCamKetTrongYeuCau.GetValueOrDefault(keyKhoSanPham, 0);

                    if (coNhieuYeuCauCungBaseCode)
                    {
                        // ⭐ LOGIC MỚI: Tính theo MAX khi đây là yêu cầu thứ 2 trở đi (đã có yêu cầu khác được xử lý)
                        // Tính MAX yêu cầu hiện tại và trước đó
                        var (maxHienTai, maxTruocDo) = TinhMaxYeuCauTheoBaseCode(Mayeucau, VattuYC.MaSanpham ?? "", maYeuCauChuan);
                        
                        // Tính số lượng đã cấp thực tế (từ các yêu cầu trước đó)
                        var trangThaiDaCap = new[]
                        {
                            "Đã xác nhận nhận hàng",
                            "Hoàn thành",
                            "Đã xuất kho",
                            "Đã lấy hàng",
                            "Chờ người yêu cầu xác nhận",
                            "Đang chuẩn bị hàng"
                        };
                        var danhSachVTDaXuatHopLe = _context.vtphieuxuatkho
                            .Where(vt => trangThaiDaCap.Contains(vt.TrangThai))
                            .ToList();
                        var danhSachVTDaNhapTra = _context.vtphieunhapkho
                            .Where(vt => vt.TrangThai == "Đã nhập kho")
                            .ToList();
                        int soLuongDaCap = TinhSoLuongDaCapThucTe(maYeuCauChuan, VattuYC.MaSanpham ?? "", danhSachVTDaXuatHopLe, danhSachVTDaNhapTra);
                        
                        // Lấy số lượng yêu cầu hiện tại
                        int soLuongYeuCauHienTai = Math.Max(VattuYC.SLMoi ?? 0, VattuYC.SL ?? 0);
                        
                        // ⚠️ XỬ LÝ TRƯỜNG HỢP YÊU CẦU SAU NHỎ HƠN YÊU CẦU TRƯỚC (TÍNH THỪA)
                        // Nếu MAX hiện tại < MAX trước đó HOẶC số lượng yêu cầu hiện tại < MAX trước đó → có thừa, cần nhập kho
                        if (maxHienTai < maxTruocDo || (maxTruocDo > 0 && soLuongYeuCauHienTai < maxTruocDo))
                        {
                            // Yêu cầu sau nhỏ hơn yêu cầu trước → có thừa, cần nhập kho
                            // Số lượng thừa = MAX trước đó - MAX hiện tại (hoặc số lượng yêu cầu hiện tại nếu MAX hiện tại = MAX trước đó)
                            int soLuongThua = maxHienTai < maxTruocDo ? (maxTruocDo - maxHienTai) : (maxTruocDo - soLuongYeuCauHienTai);
                            
                            Console.WriteLine($"⚠️ Phát hiện yêu cầu sau nhỏ hơn yêu cầu trước. Số lượng thừa: {soLuongThua} cho vật tư {VattuYC.MaSanpham}");
                            
                            // Số lượng mua = 0 (không cần mua thêm, chỉ cần nhập lại phần thừa)
                            soLuongMua = 0;
                            soLuongThieu = 0;
                            soLuongXuat = 0;
                            soLuongYeuCauThucTe = maxHienTai;
                            
                            // Thêm vào danh sách vật tư cần nhập trả
                            danhSachVatTuCanNhapTra.Add((VattuYC, soLuongThua, maxTruocDo, maxHienTai));
                            
                            // Cập nhật trạng thái vật tư
                            if (VattuYC.TrangThai != "Đã xuất kho")
                            {
                                VattuYC.TrangThai = "Đã xuất kho";
                            }
                            _context.vtyeucau.Update(VattuYC);
                            
                            // Bỏ qua phần logic tạo phiếu xuất kho/mua hàng, chỉ tạo phiếu nhập kho ở cuối
                            continue;
                        }
                        else
                        {
                            // ⭐ CÔNG THỨC ĐÚNG:
                            // Số lượng cần cấp thêm = MAX hiện tại - Số đã cấp
                            soLuongYeuCauThucTe = Math.Max(0, maxHienTai - soLuongDaCap);
                            
                            // ⚠️ XỬ LÝ TRƯỜNG HỢP: Số lượng yêu cầu mới bằng số lượng đã cấp trước đó
                            // Nếu số lượng yêu cầu hiện tại bằng số lượng đã cấp → không cần mua, không cần xuất kho
                            if (soLuongYeuCauHienTai == soLuongDaCap && soLuongDaCap > 0)
                            {
                                Console.WriteLine($"ℹ️ Số lượng yêu cầu mới ({soLuongYeuCauHienTai}) bằng số lượng đã cấp ({soLuongDaCap}). Không cần mua/xuất kho cho vật tư {VattuYC.MaSanpham}");
                                
                                // Không cần mua, không cần xuất kho
                                soLuongMua = 0;
                                soLuongThieu = 0;
                                soLuongXuat = 0;
                                soLuongYeuCauThucTe = 0;
                                
                                // Cập nhật trạng thái vật tư thành "Hoàn thành" nếu chưa có trạng thái đặc biệt
                                if (VattuYC.TrangThai != "Đã xuất kho" && VattuYC.TrangThai != "Chờ xuất kho" && VattuYC.TrangThai != "Đang mua hàng")
                                {
                                    VattuYC.TrangThai = "Hoàn thành";
                                }
                                _context.vtyeucau.Update(VattuYC);
                                
                                // Bỏ qua phần logic tạo phiếu xuất kho/mua hàng
                                continue;
                            }
                            
                            // FIFO: Tính số lượng hàng đã cam kết từ các phiếu xuất khác (chỉ tính vật tư duyệt trước)
                            int soLuongDaCamKetTuYeuCauKhac = TinhSoLuongDaCamKet(khotong.Makho ?? "", khotong.MaSanpham ?? "", VattuYC.NgayDuyet, Maxuatkho);
                            
                            // Cập nhật số lượng đã cam kết từ các vật tư đã xử lý TRƯỚC ĐÓ trong cùng yêu cầu
                            soLuongDaCamKetTrongCungYeuCau = soLuongDaCamKetTrongYeuCau.GetValueOrDefault(keyKhoSanPham, 0);
                            
                            // Tổng số lượng đã cam kết = từ yêu cầu khác (FIFO) + từ yêu cầu hiện tại
                            int tongSoLuongDaCamKet = soLuongDaCamKetTuYeuCauKhac + soLuongDaCamKetTrongCungYeuCau;
                            
                            // Số lượng khả dụng = Tồn kho - Tổng số lượng đã cam kết
                            int soLuongKhaDung = (khotong.SL ?? 0) - tongSoLuongDaCamKet;
                            
                            // Số lượng xuất = MIN(số lượng cần cấp thêm, số lượng khả dụng)
                            if (VattuYC.TrangThai == "Chờ xuất kho" || VattuYC.TrangThai == "Đã xuất kho")
                            {
                                soLuongXuat = soLuongYeuCauThucTe;
                            }
                            else
                            {
                                soLuongXuat = Math.Max(0, Math.Min(soLuongKhaDung, soLuongYeuCauThucTe));
                            }
                            
                            // ⭐ Số lượng mua = Số lượng cần cấp thêm - Số lượng xuất từ kho
                            soLuongMua = Math.Max(0, soLuongYeuCauThucTe - soLuongXuat);
                            soLuongThieu = soLuongMua;
                        }
                    }
                    else
                    {
                        // LOGIC CŨ: Tính toán bình thường khi là yêu cầu đầu tiên hoặc chỉ có 1 yêu cầu
                        // FIFO: Tính số lượng hàng đã cam kết từ các phiếu xuất khác (chỉ tính vật tư duyệt trước)
                        int soLuongDaCamKetTuYeuCauKhac = TinhSoLuongDaCamKet(khotong.Makho ?? "", khotong.MaSanpham ?? "", VattuYC.NgayDuyet, Maxuatkho);
                        
                        // Cập nhật số lượng đã cam kết từ các vật tư đã xử lý TRƯỚC ĐÓ trong cùng yêu cầu
                        soLuongDaCamKetTrongCungYeuCau = soLuongDaCamKetTrongYeuCau.GetValueOrDefault(keyKhoSanPham, 0);
                        
                        // Tổng số lượng đã cam kết = từ yêu cầu khác (FIFO) + từ yêu cầu hiện tại
                        int tongSoLuongDaCamKet = soLuongDaCamKetTuYeuCauKhac + soLuongDaCamKetTrongCungYeuCau;
                        
                        // Số lượng khả dụng = Tồn kho - Tổng số lượng đã cam kết
                        int soLuongKhaDung = (khotong.SL ?? 0) - tongSoLuongDaCamKet;
                        soLuongYeuCauThucTe = VattuYC.SL ?? 0;

                    // Nếu vật tư đã được duyệt với trạng thái "Đã xuất kho" thì ưu tiên xuất đủ theo SL yêu cầu
                    if (VattuYC.TrangThai == "Chờ xuất kho" || VattuYC.TrangThai == "Đã xuất kho")
                    {
                            soLuongXuat = soLuongYeuCauThucTe;
                    }
                    else
                    {
                            soLuongXuat = Math.Max(0, Math.Min(soLuongKhaDung, soLuongYeuCauThucTe));
                    }
                        soLuongThieu = soLuongYeuCauThucTe - soLuongXuat;
                    }

                    if (soLuongXuat > 0 && isPhieuXuatKhoCreated)
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
                            SL = soLuongXuat
                        };
                        _context.Add(VTPhieuxuatkho);
                        
                        // Cập nhật số lượng đã cam kết trong cùng yêu cầu (để vật tư tiếp theo tính đúng)
                        soLuongDaCamKetTrongYeuCau[keyKhoSanPham] = soLuongDaCamKetTrongCungYeuCau + soLuongXuat;
                    }

                    if (soLuongThieu > 0 && isPhieuMuaHangCreated)
                    {
                        // Không thêm vật tư có trạng thái "Hoàn thành" vào phiếu mua hàng
                        if (VattuYC.TrangThai != "Hoàn thành")
                        {
                            // CHỈ cập nhật trạng thái nếu vật tư chưa có trạng thái "Chờ xuất kho", "Đã xuất kho", "Hoàn thành" hoặc "Đang mua hàng"
                            // Nếu đã có trạng thái "Chờ xuất kho" hoặc "Đã xuất kho", giữ nguyên (vì đã xuất rồi, phần thiếu sẽ mua bổ sung)
                            // Nếu đã có trạng thái "Hoàn thành", giữ nguyên (vì số lượng mới = số lượng đã cấp, không cần mua/xuất kho)
                            // Nếu đã có trạng thái "Đang mua hàng", giữ nguyên (vì đã được xử lý trước đó)
                            if (VattuYC.TrangThai != "Chờ xuất kho" && VattuYC.TrangThai != "Đã xuất kho" && VattuYC.TrangThai != "Hoàn thành" && VattuYC.TrangThai != "Đang mua hàng")
                            {
                                VattuYC.TrangThai = "Đang mua hàng";
                            }
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
                                SL = soLuongThieu,
                                NgayBaohanh = VattuYC.NgayBaohanh,
                                ThoiGianBH = VattuYC.ThoiGianBH,
                                TrangThai = "Đang chờ báo giá"
                            };
                            _context.Add(VTPhieumuahang);
                        }
                    }
                    else if (soLuongXuat > 0 && soLuongThieu == 0)
                    {
                        // Vật tư đã được xuất kho đủ, CHỈ cập nhật trạng thái nếu chưa có trạng thái "Chờ xuất kho", "Đã xuất kho" hoặc "Đang mua hàng"
                        // Giữ nguyên trạng thái "Chờ xuất kho" - chỉ chuyển sang "Đã xuất kho" khi kho thực sự xác nhận đã xuất
                        // Giữ nguyên trạng thái hiện tại nếu đã có (vì đã được xử lý trước đó)
                        if (VattuYC.TrangThai != "Chờ xuất kho" && VattuYC.TrangThai != "Đã xuất kho" && VattuYC.TrangThai != "Đang mua hàng")
                        {
                            VattuYC.TrangThai = "Đã xuất kho";
                        }
                        // Nếu đang ở trạng thái "Chờ xuất kho", giữ nguyên - kho sẽ chuyển sang "Đã xuất kho" khi xác nhận
                    }

                    _context.vtyeucau.Update(VattuYC);
                }
                else
                {
                    // Không có trong kho tổng, cần kiểm tra logic MAX và nhập kho
                    if (coNhieuYeuCauCungBaseCode)
                    {
                        // Tính MAX yêu cầu hiện tại và trước đó
                        var (maxHienTai, maxTruocDo) = TinhMaxYeuCauTheoBaseCode(Mayeucau, VattuYC.MaSanpham ?? "", maYeuCauChuan);
                        
                        // Lấy số lượng yêu cầu hiện tại
                        int soLuongYeuCauHienTai = Math.Max(VattuYC.SLMoi ?? 0, VattuYC.SL ?? 0);
                        
                        // Tính số lượng đã cấp thực tế (từ các yêu cầu trước đó)
                        var trangThaiDaCap = new[]
                        {
                            "Đã xác nhận nhận hàng",
                            "Hoàn thành",
                            "Đã xuất kho",
                            "Đã lấy hàng",
                            "Chờ người yêu cầu xác nhận",
                            "Đang chuẩn bị hàng"
                        };
                        var danhSachVTDaXuatHopLe = _context.vtphieuxuatkho
                            .Where(vt => trangThaiDaCap.Contains(vt.TrangThai))
                            .ToList();
                        var danhSachVTDaNhapTra = _context.vtphieunhapkho
                            .Where(vt => vt.TrangThai == "Đã nhập kho")
                            .ToList();
                        int soLuongDaCap = TinhSoLuongDaCapThucTe(maYeuCauChuan, VattuYC.MaSanpham ?? "", danhSachVTDaXuatHopLe, danhSachVTDaNhapTra);
                        
                        // ⚠️ XỬ LÝ TRƯỜNG HỢP: Số lượng yêu cầu mới bằng số lượng đã cấp trước đó
                        // Nếu số lượng yêu cầu hiện tại bằng số lượng đã cấp → không cần mua hàng
                        if (soLuongYeuCauHienTai == soLuongDaCap && soLuongDaCap > 0)
                        {
                            Console.WriteLine($"ℹ️ Số lượng yêu cầu mới ({soLuongYeuCauHienTai}) bằng số lượng đã cấp ({soLuongDaCap}). Không cần mua hàng cho vật tư {VattuYC.MaSanpham} (không có trong kho)");
                            
                            // Cập nhật trạng thái vật tư thành "Hoàn thành" nếu chưa có trạng thái đặc biệt
                            if (VattuYC.TrangThai != "Đã xuất kho" && VattuYC.TrangThai != "Chờ xuất kho" && VattuYC.TrangThai != "Đang mua hàng")
                            {
                                VattuYC.TrangThai = "Hoàn thành";
                            }
                            _context.vtyeucau.Update(VattuYC);
                            
                            // Bỏ qua phần logic tạo phiếu mua hàng
                            continue;
                        }
                        
                        // ⚠️ XỬ LÝ TRƯỜNG HỢP YÊU CẦU SAU NHỎ HƠN YÊU CẦU TRƯỚC (TÍNH THỪA)
                        if (maxHienTai < maxTruocDo || (maxTruocDo > 0 && soLuongYeuCauHienTai < maxTruocDo))
                        {
                            // Yêu cầu sau nhỏ hơn yêu cầu trước → có thừa, cần nhập kho
                            int soLuongThua = maxHienTai < maxTruocDo ? (maxTruocDo - maxHienTai) : (maxTruocDo - soLuongYeuCauHienTai);
                            
                            Console.WriteLine($"⚠️ Phát hiện yêu cầu sau nhỏ hơn yêu cầu trước (không có trong kho). Số lượng thừa: {soLuongThua} cho vật tư {VattuYC.MaSanpham}");
                            
                            // Thêm vào danh sách vật tư cần nhập trả
                            danhSachVatTuCanNhapTra.Add((VattuYC, soLuongThua, maxTruocDo, maxHienTai));
                            
                            // Cập nhật trạng thái vật tư
                            if (VattuYC.TrangThai != "Đã xuất kho")
                            {
                                VattuYC.TrangThai = "Đã xuất kho";
                            }
                            _context.vtyeucau.Update(VattuYC);
                            
                            // Bỏ qua phần logic tạo phiếu mua hàng
                            continue;
                        }
                    }
                    
                    // Không có trong kho tổng và không cần nhập kho, cần mua hàng
                    if (isPhieuMuaHangCreated)
                    {
                        // Không thêm vật tư có trạng thái "Hoàn thành" vào phiếu mua hàng
                        if (VattuYC.TrangThai != "Hoàn thành")
                        {
                            // CHỈ cập nhật trạng thái nếu vật tư chưa có trạng thái "Chờ xuất kho", "Đã xuất kho", "Hoàn thành" hoặc "Đang mua hàng"
                            // Nếu đã có trạng thái "Chờ xuất kho", "Đã xuất kho" hoặc "Đang mua hàng", giữ nguyên (vì đã được xử lý trước đó)
                            // Nếu đã có trạng thái "Hoàn thành", giữ nguyên (vì số lượng mới = số lượng đã cấp, không cần mua/xuất kho)
                            if (VattuYC.TrangThai != "Chờ xuất kho" && VattuYC.TrangThai != "Đã xuất kho" && VattuYC.TrangThai != "Hoàn thành" && VattuYC.TrangThai != "Đang mua hàng")
                            {
                                VattuYC.TrangThai = "Đang mua hàng";
                            }
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
                }
            }

            // Xử lý tạo phiếu nhập kho cho các vật tư cần nhập trả (khi yêu cầu sau nhỏ hơn yêu cầu trước)
            if (danhSachVatTuCanNhapTra.Any())
            {
                // Sử dụng allRelatedMaYeucau đã được khai báo ở trên
                // Tìm người đã nhận hàng từ phiếu xuất kho đầu tiên (người thực sự đã lấy hàng)
                // Trong trường hợp này, người trả hàng chính là người yêu cầu (Quỳnh)
                string maNguoiTraHang = thongTinYeuCau.YCMaNguoidung; // Mặc định là người yêu cầu
                string tenNguoiTraHang = thongTinYeuCau.NguoiYeucau ?? "";
                
                // Tìm phiếu xuất kho đầu tiên của các yêu cầu cùng base code để lấy người đã nhận hàng
                var phieuXuatKhoDauTien = _context.phieuxuatkho
                    .Where(px => allRelatedMaYeucau.Contains(px.MaYeucau))
                    .OrderBy(px => px.NgayXuatkho ?? DateTime.MaxValue)
                    .FirstOrDefault();
                
                if (phieuXuatKhoDauTien != null && !string.IsNullOrEmpty(phieuXuatKhoDauTien.MaNguoidung))
                {
                    maNguoiTraHang = phieuXuatKhoDauTien.MaNguoidung;
                }
                
                // Đảm bảo lấy được tên người từ database nếu chưa có hoặc cần cập nhật
                if (!string.IsNullOrEmpty(maNguoiTraHang))
                {
                    var nguoiTraHang = _context.nguoidungs
                        .FirstOrDefault(n => n.MaNguoidung == maNguoiTraHang);
                    if (nguoiTraHang != null && !string.IsNullOrEmpty(nguoiTraHang.TenNguoidung))
                    {
                        tenNguoiTraHang = nguoiTraHang.TenNguoidung;
                    }
                }

                // Tạo mã yêu cầu đặc biệt cho phiếu nhập kho hoàn trả
                string maYeucauDacBiet = "";
                if (!string.IsNullOrEmpty(thongTinYeuCau.YCMaDuan))
                {
                    maYeucauDacBiet = $"NHAPKHO_DUAN_{thongTinYeuCau.YCMaDuan}";
                }
                else
                {
                    maYeucauDacBiet = $"NHAPKHO_CANHAN_{maNguoiTraHang}";
                }

                // Kiểm tra xem yêu cầu đặc biệt đã tồn tại chưa
                var existingYeucauDacBiet = _context.yeucau
                    .FirstOrDefault(y => y.MaYeucau == maYeucauDacBiet);

                if (existingYeucauDacBiet == null)
                {
                    // Với trường hợp yêu cầu sau nhỏ hơn yêu cầu trước, 
                    // đặt trạng thái "Chờ nhập kho" để bộ phận kho duyệt (không cần giám đốc và QLDA duyệt)
                    string trangThaiYeucau = "Chờ nhập kho";

                    // Lấy thông tin giám đốc từ session (người đang duyệt yêu cầu)
                    string maGiamDoc = HttpContext.Session.GetString("MaNguoidung") ?? "";
                    DateTime ngayPheDuyet = DateTime.Now;

                    var newYeucauDacBiet = new yeucau
                    {
                        MaYeucau = maYeucauDacBiet,
                        YCMaDuan = thongTinYeuCau.YCMaDuan,
                        YCMaNguoidung = maNguoiTraHang,
                        NguoiYeucau = tenNguoiTraHang,
                        NgayYeucau = DateTime.Now,
                        TrangThai = trangThaiYeucau,
                        // Lưu thông tin người duyệt và ngày phê duyệt là giám đốc
                        NguoiDuyet = maGiamDoc,
                        NgayDuyet = ngayPheDuyet
                    };
                    _context.yeucau.Add(newYeucauDacBiet);
                    _context.SaveChanges();
                }
                else
                {
                    // Nếu yêu cầu đặc biệt đã tồn tại, cập nhật thông tin người duyệt và ngày phê duyệt
                    string maGiamDoc = HttpContext.Session.GetString("MaNguoidung") ?? "";
                    if (!string.IsNullOrEmpty(maGiamDoc))
                    {
                        existingYeucauDacBiet.NguoiDuyet = maGiamDoc;
                        existingYeucauDacBiet.NgayDuyet = DateTime.Now;
                        _context.yeucau.Update(existingYeucauDacBiet);
                        _context.SaveChanges();
                    }
                }

                // Tạo mã phiếu nhập kho
                int sttPNK = 1;
                string maNhapkhoTra = "";
                while (true)
                {
                    maNhapkhoTra = $"PNK{sttPNK}";
                    if (_context.phieunhapkho.FirstOrDefault(p => p.MaNhapkho == maNhapkhoTra) == null)
                        break;
                    sttPNK++;
                }

                // Với trường hợp yêu cầu sau nhỏ hơn yêu cầu trước,
                // đặt trạng thái "Chờ nhập kho" để bộ phận kho duyệt (không cần giám đốc và QLDA duyệt)
                string trangThaiPhieuNhap = "Chờ nhập kho";

                var phieuNhapTra = new phieunhapkho
                {
                    MaNhapkho = maNhapkhoTra,
                    MaYeucau = maYeucauDacBiet,
                    MaDuan = thongTinYeuCau.YCMaDuan,
                    MaNguoidung = maNguoiTraHang,
                    NgayNhapkho = DateTime.Now,
                    TrangThai = trangThaiPhieuNhap
                };
                _context.phieunhapkho.Add(phieuNhapTra);
                _context.SaveChanges();

                // Thêm vật tư vào phiếu nhập kho hoàn trả
                foreach (var vtTra in danhSachVatTuCanNhapTra)
                {
                    var vtPhieuNhap = new vtphieunhapkho
                    {
                        MaNhapkho = maNhapkhoTra,
                        MaYeucau = maYeucauDacBiet, // Sử dụng mã yêu cầu đặc biệt
                        TenSanpham = vtTra.VatTu.TenSanpham,
                        MaSanpham = vtTra.VatTu.MaSanpham,
                        Makho = vtTra.VatTu.YCMakho,
                        HangSX = vtTra.VatTu.HangSX,
                        NhaCC = vtTra.VatTu.NhaCC,
                        DonVi = vtTra.VatTu.DonVi,
                        SL = vtTra.SoLuongTra,
                        NgayBaohanh = vtTra.VatTu.NgayBaohanh,
                        ThoiGianBH = vtTra.VatTu.ThoiGianBH,
                        TrangThai = trangThaiPhieuNhap,
                        DiengiaiNhapKho = $"Trả lại do yêu cầu mới giảm số lượng"
                    };
                    _context.vtphieunhapkho.Add(vtPhieuNhap);
                }
                
                _context.SaveChanges();
                Console.WriteLine($"Đã tạo phiếu nhập kho hoàn trả: {maNhapkhoTra} với {danhSachVatTuCanNhapTra.Count} vật tư");
            }

            _context.SaveChanges();

            // Kiểm tra và xóa phiếu mua hàng rỗng (không có vật tư nào)
            if (isPhieuMuaHangCreated && existingPhieuMuaHang == null)
            {
                var soLuongVatTuTrongPMH = _context.vtphieumuahang
                    .Count(vt => vt.MaMuahang == Mamuahang);
                
                if (soLuongVatTuTrongPMH == 0)
                {
                    var PhieuMuaHangRong = _context.phieumuahang
                        .FirstOrDefault(p => p.MaMuahang == Mamuahang);
                    if (PhieuMuaHangRong != null)
                    {
                        _context.phieumuahang.Remove(PhieuMuaHangRong);
                        _context.SaveChanges();
                        Console.WriteLine($"Đã xóa phiếu mua hàng rỗng: {Mamuahang}");
                    }
                }
            }

            Console.WriteLine("Đã xử lý phiếu yêu cầu cho các vật tư đã duyệt.");
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

            

            if (Phieuxuatkho.TrangThai == "Chờ xác nhận" || Phieuxuatkho.TrangThai == "Thiếu hàng - Đã tạo phiếu mua")
            {
                
                bool duHang = true;
                var vatTuThieu = new List<vtphieuxuatkho>();
                
                // Lấy số lượng yêu cầu ban đầu từ vtyeucau để tính số lượng còn lại cần xuất
                var vtYeuCauList = _context.vtyeucau
                    .Where(vt => vt.VTMaYeucau == Phieuxuatkho.MaYeucau)
                    .ToList();
                
                // Nhóm các vật tư theo MaSanpham và Makho để tránh kiểm tra trùng lặp
                var vatTuNhom = VTphieuxuatkho
                    .GroupBy(vt => new { MaSanpham = vt.MaSanpham ?? "", Makho = vt.Makho ?? "" })
                    .ToList();
                
                foreach (var nhom in vatTuNhom)
                {
                    var maSanpham = nhom.Key.MaSanpham;
                    var makho = nhom.Key.Makho;
                    var dongDauTien = nhom.First();
                    
                    // Tính tổng số lượng từ tất cả records với cùng Makho và MaSanpham (để tránh lỗi do duplicate)
                    int tongSoLuongTonKho = _context.khotongs
                        .Where(k => k.Makho == makho && k.MaSanpham == maSanpham)
                        .Sum(k => k.SL ?? 0);
                    
                    // Tính số lượng còn lại cần xuất:
                    // 1. Lấy số lượng yêu cầu ban đầu từ vtyeucau
                    var vtYeuCau = vtYeuCauList.FirstOrDefault(vt => 
                        string.Equals(vt.MaSanpham, maSanpham, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(vt.YCMakho, makho, StringComparison.OrdinalIgnoreCase))
                        ?? vtYeuCauList.FirstOrDefault(vt =>
                            string.Equals(vt.MaSanpham, maSanpham, StringComparison.OrdinalIgnoreCase));
                    int soLuongYeuCauBanDau = vtYeuCau?.SL ?? 0;
                    
                    // FIFO: Tính số lượng hàng đã cam kết (đã duyệt nhưng chưa giao) từ các phiếu xuất khác
                    int soLuongDaCamKet = TinhSoLuongDaCamKet(makho, maSanpham, vtYeuCau?.NgayDuyet, Phieuxuatkho.MaXuatkho);
                    
                    // Số lượng khả dụng = Tổng tồn kho - Số lượng đã cam kết
                    int soLuongKhaDung = tongSoLuongTonKho - soLuongDaCamKet;
                    
                    // 2. Tính tổng số lượng đã có trong phiếu xuất (tất cả các dòng, bất kể trạng thái)
                    // Vì khi nhập hàng bổ sung, có thể tạo thêm dòng mới hoặc cập nhật dòng cũ
                    int tongSoLuongTrongPhieuXuat = nhom.Sum(vt => vt.SL ?? 0);
                    
                    // 3. Tính số lượng đã được xuất (các vật tư có trạng thái "Đã xuất kho")
                    int soLuongDaXuat = nhom
                        .Where(vt => vt.TrangThai == "Đã xuất kho")
                        .Sum(vt => vt.SL ?? 0);
                    
                    // 4. Số lượng còn lại cần xuất = Yêu cầu ban đầu - Đã xuất
                    int soLuongConLaiCanXuat = Math.Max(0, soLuongYeuCauBanDau - soLuongDaXuat);
                    
                    // Kiểm tra: Nếu số lượng còn lại cần xuất > 0, thì cần có đủ trong kho
                    // Nếu số lượng còn lại = 0, nghĩa là đã xuất đủ, không cần kiểm tra
                    if (soLuongConLaiCanXuat > 0)
                    {
                        // Kiểm tra chặt chẽ: không có hàng, số lượng khả dụng <= 0, hoặc không đủ số lượng còn lại cần xuất
                        if (tongSoLuongTonKho <= 0 || soLuongKhaDung <= 0 || soLuongKhaDung < soLuongConLaiCanXuat)
                        {
                            duHang = false;
                            
                            // Tính số lượng thiếu chính xác
                            int soLuongThieu;
                            if (tongSoLuongTonKho <= 0 || soLuongKhaDung <= 0)
                            {
                                // Không có hàng trong kho → cần mua toàn bộ số lượng còn lại
                                soLuongThieu = soLuongConLaiCanXuat;
                            }
                            else
                            {
                                // Có hàng nhưng không đủ → cần mua phần thiếu
                                soLuongThieu = soLuongConLaiCanXuat - soLuongKhaDung;
                            }
                            
                            // Tạo đối tượng vật tư thiếu với số lượng chính xác
                            var vtThieu = new vtphieuxuatkho
                            {
                                MaXuatkho = dongDauTien.MaXuatkho,
                                MaYeucau = dongDauTien.MaYeucau,
                                TenSanpham = dongDauTien.TenSanpham,
                                MaSanpham = dongDauTien.MaSanpham,
                                Makho = dongDauTien.Makho,
                                HangSX = dongDauTien.HangSX,
                                NhaCC = dongDauTien.NhaCC,
                                DonVi = dongDauTien.DonVi,
                                SL = soLuongThieu, // Số lượng thiếu chính xác
                                TrangThai = dongDauTien.TrangThai
                            };
                            vatTuThieu.Add(vtThieu);
                        }
                    }
                }

                if (duHang)
                {
                    // Đủ hàng → chuẩn bị hàng, chuyển sang "Đang chuẩn bị hàng"
                    Phieuxuatkho.TrangThai = "Đang chuẩn bị hàng";
                    Phieuxuatkho.NgayChuanBi = DateTime.Now;
                    Phieuxuatkho.GhiChu = null; // Xóa ghi chú thiếu hàng nếu có
                    _context.phieuxuatkho.Update(Phieuxuatkho);
                    _context.SaveChanges();
                }
                else
                {
                    // Thiếu hàng → tự động tạo phiếu mua hàng theo dữ liệu đã có
                    // Chỉ tạo phiếu mua hàng nếu chưa có (tránh tạo trùng)
                    if (Phieuxuatkho.TrangThai != "Thiếu hàng - Đã tạo phiếu mua")
                    {
                        Phieuxuatkho.TrangThai = "Thiếu hàng - Đã tạo phiếu mua";
                        Phieuxuatkho.GhiChu = "Không đủ số lượng tồn kho. Đã tự động tạo phiếu mua hàng.";
                        _context.phieuxuatkho.Update(Phieuxuatkho);
                        
                        // Tạo phiếu mua hàng tự động
                        TaoPhieuMuaHangTuDong(Phieuxuatkho, vatTuThieu);
                        
                        _context.SaveChanges();
                    }
                }
            }
            else if (Phieuxuatkho.TrangThai == "Đang chuẩn bị hàng")
            {
                // Bước 3: Người yêu cầu nhận thông báo và xác nhận đã nhận vật tư
                Phieuxuatkho.TrangThai = "Chờ người yêu cầu xác nhận";
                Phieuxuatkho.NgaySanSang = DateTime.Now;
                _context.phieuxuatkho.Update(Phieuxuatkho);
                _context.SaveChanges();
            }
            else if (Phieuxuatkho.TrangThai == "Chờ người yêu cầu xác nhận")
            {
                var allVtConfirmed = VTphieuxuatkho.All(vt =>
                    vt.TrangThai == "Đã xuất kho");

                if (!allVtConfirmed)
                {
                    TempData["Error"] = "Không thể hoàn tất phiếu vì vẫn còn vật tư chưa được xuất kho.";
                    return RedirectToAction("Phieuxuatkho", "Yeucau", new { area = "Giamdoc" });
                }

                if (!Phieuxuatkho.NgayXuatkho.HasValue)
                {
                    Phieuxuatkho.NgayXuatkho = DateTime.Now;
                }

                foreach (var VTxuatkho in VTphieuxuatkho)
                {
                    var VTphieuxuatkhott = _context.vtphieuxuatkho.FirstOrDefault(vt => vt.ID == VTxuatkho.ID);
                    if (VTphieuxuatkhott == null)
                    {
                        continue;
                    }

                    VTphieuxuatkhott.TrangThai = "Đã xuất kho";
                    _context.vtphieuxuatkho.Update(VTphieuxuatkhott);

                    if (!string.IsNullOrEmpty(Phieuxuatkho.MaDuan))
                    {
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
                }
                
                // Đồng bộ trạng thái vật tư yêu cầu và yêu cầu tổng khi đã xuất kho
                foreach (var VTxuatkho in VTphieuxuatkho)
                {
                    var vtYeucauList = _context.vtyeucau
                        .Where(v => v.VTMaYeucau == VTxuatkho.MaYeucau && v.MaSanpham == VTxuatkho.MaSanpham)
                        .ToList();

                    foreach (var vtYc in vtYeucauList)
                    {
                        if (vtYc.TrangThai != "Đã xuất kho")
                        {
                            vtYc.TrangThai = "Đã xuất kho";
                            _context.vtyeucau.Update(vtYc);
                        }
                    }
                }

                var maYeucauList = VTphieuxuatkho
                    .Select(v => v.MaYeucau)
                    .Where(ma => !string.IsNullOrEmpty(ma))
                    .Distinct()
                    .ToList();

                foreach (var maYc in maYeucauList)
                {
                    var yeuCau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == maYc);
                    if (yeuCau == null)
                    {
                        continue;
                    }

                    var vtList = _context.vtyeucau
                        .Where(v => v.VTMaYeucau == maYc)
                        .ToList();

                    var hasDangMuaHang = vtList.Any(v => v.TrangThai == "Đang mua hàng");
                    var hasChoXuatKho = vtList.Any(v => v.TrangThai == "Chờ xuất kho");
                    var hasDaXuatKho = vtList.Any(v => v.TrangThai == "Đã xuất kho");
                    var hasDaNhapKho = vtList.Any(v => v.TrangThai == "Đã nhập kho");
                    var allDoneOrRejected = vtList.All(v =>
                        v.TrangThai == "Đã xuất kho" ||
                        (!string.IsNullOrEmpty(v.TrangThai) && v.TrangThai.Contains("Đã từ chối")));

                    if (hasDaNhapKho)
                    {
                        // Nếu có vật tư đã nhập kho, kiểm tra xem tất cả vật tư đã nhập kho chưa
                        var allDaNhapKho = vtList.All(v =>
                            v.TrangThai == "Đã nhập kho" ||
                            (!string.IsNullOrEmpty(v.TrangThai) && v.TrangThai.Contains("Đã từ chối")));
                        
                        if (allDaNhapKho)
                        {
                            yeuCau.TrangThai = "Đã nhập kho";
                        }
                        else
                        {
                            // Có một số vật tư đã nhập kho nhưng chưa tất cả, kiểm tra các trạng thái khác
                            if (hasDangMuaHang)
                            {
                                yeuCau.TrangThai = "Đang mua hàng";
                            }
                            else if (hasChoXuatKho)
                            {
                                yeuCau.TrangThai = "Chờ xuất kho";
                            }
                            else if (hasDaXuatKho)
                            {
                                yeuCau.TrangThai = "Đã xuất kho";
                            }
                            else
                            {
                                yeuCau.TrangThai = "Đã nhập kho";
                            }
                        }
                    }
                    else if (allDoneOrRejected)
                    {
                        yeuCau.TrangThai = "Đã xuất kho";
                    }
                    else if (hasDangMuaHang)
                    {
                        yeuCau.TrangThai = "Đang mua hàng";
                    }
                    else if (hasChoXuatKho)
                    {
                        yeuCau.TrangThai = "Chờ xuất kho";
                    }

                    _context.yeucau.Update(yeuCau);
                }
                
                // Khóa phiếu và hoàn thành
                Phieuxuatkho.TrangThai = "Hoàn thành";
                Phieuxuatkho.NgayHoanThanh = DateTime.Now;
                _context.phieuxuatkho.Update(Phieuxuatkho);
                _context.SaveChanges();
            }
            
            return RedirectToAction("Phieuxuatkho", "Yeucau", new { area = "Giamdoc" });
        }

        // Helper method: Tính số lượng hàng đã cam kết (committed) từ các phiếu xuất đã duyệt nhưng chưa giao
        // Các trạng thái được tính: "Đang chuẩn bị hàng", "Chờ người yêu cầu xác nhận"
        // FIFO: Chỉ tính các vật tư được duyệt TRƯỚC thời điểm duyệt hiện tại (ngayDuyetHienTai)
        private int TinhSoLuongDaCamKet(string makho, string masanpham, DateTime? ngayDuyetHienTai = null, string maXuatkhoHienTai = null)
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

            // FIFO: Tính số lượng vật tư đã được duyệt TRƯỚC thời điểm duyệt hiện tại
            // Chỉ tính các vật tư có NgayDuyet < ngayDuyetHienTai (ai duyệt trước được ưu tiên)
            int vatTuDaDuyetChuaXuat = 0;
            if (ngayDuyetHienTai.HasValue)
            {
                // Chỉ tính các vật tư được duyệt TRƯỚC thời điểm hiện tại
                var vatTuDaDuyetTruoc = _context.vtyeucau
                    .Where(vt => vt.TrangThai == "Đã xuất kho"
                        && vt.NgayDuyet != null
                        && vt.NgayDuyet < ngayDuyetHienTai.Value  // FIFO: chỉ tính vật tư duyệt trước
                        && (vt.YCMakho == makho || string.IsNullOrEmpty(vt.YCMakho))
                        && vt.MaSanpham == masanpham
                        && !_context.vtphieuxuatkho.Any(vtx => vtx.MaYeucau == vt.VTMaYeucau && vtx.MaSanpham == vt.MaSanpham))
                    .ToList();

                // Tính số lượng đã cam kết từ các vật tư này
                foreach (var vt in vatTuDaDuyetTruoc)
                {
                    // Tìm trong phiếu xuất kho nếu đã tạo
                    int slXuat = _context.vtphieuxuatkho
                        .Where(p => p.MaYeucau == vt.VTMaYeucau && p.MaSanpham == masanpham)
                        .Sum(p => p.SL ?? 0);
                    
                    // Nếu chưa tạo phiếu nhưng đã duyệt → vẫn phải tính SL gốc
                    if (slXuat == 0)
                        slXuat = vt.SL ?? 0;
                    
                    vatTuDaDuyetChuaXuat += slXuat;
                }
            }
            else
            {
                // Nếu không có ngayDuyetHienTai, tính tất cả vật tư đã duyệt (tương thích ngược)
                vatTuDaDuyetChuaXuat = _context.vtyeucau
                    .Where(vt => vt.TrangThai == "Đã xuất kho"
                        && (vt.YCMakho == makho || string.IsNullOrEmpty(vt.YCMakho))
                        && vt.MaSanpham == masanpham
                        && !_context.vtphieuxuatkho.Any(vtx => vtx.MaYeucau == vt.VTMaYeucau && vtx.MaSanpham == vt.MaSanpham))
                    .Sum(vt => vt.SL ?? 0);
            }

            return tongSoLuongDaCamKet + vatTuDaDuyetChuaXuat;
        }

        // Method tự động tạo phiếu mua hàng khi thiếu hàng
        private void TaoPhieuMuaHangTuDong(phieuxuatkho phieuxuatkho, List<vtphieuxuatkho> vatTuThieu)
        {
            try
            {
                // Tạo mã phiếu mua hàng duy nhất
                int STT = 1;
                string MaMuahang;
                do
                {
                    MaMuahang = $"PMH{STT}";
                    STT++;
                } while (_context.phieumuahang.Any(p => p.MaMuahang == MaMuahang));

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
                    // Lưu thời gian mua hàng khi bộ phận mua hàng nhận hàng
                    Phieumuahang.NgayMuahang = DateTime.Now;
                    Taophieunhapkhobyphieumuahang(MaMuahang, phieunhapkho, vtphieunhapkho, phieumuahang, vtphieumuahang);
                }
                foreach (var VTPhieumuahang in VTPhieumuahanglist)
                {
                    // Chỉ cập nhật trạng thái các mục đã báo giá (có đơn giá)
                    // Các mục chưa báo giá giữ nguyên trạng thái "Đang chờ báo giá"
                    if (VTPhieumuahang.TrangThai == "Đã báo giá" && VTPhieumuahang.DonGia != null && VTPhieumuahang.DonGia > 0)
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
                }
                _context.phieumuahang.Update(Phieumuahang);
                _context.SaveChanges();

                // Gửi email thông báo khi giám đốc duyệt
                if (chucVu2 == "Giám đốc" && Phieumuahang.TrangThai == "Chờ thanh toán")
                {
                    try
                    {
                        var maMuahangForEmail = MaMuahang;
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                using (var scope = _serviceScopeFactory.CreateScope())
                                {
                                    var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                                    await emailService.SendNotificationToAccountingOnApprovalAsync(maMuahangForEmail);
                                    await emailService.SendNotificationToRequesterOnApprovalAsync(maMuahangForEmail);
                                }
                            }
                            catch (Exception exInner)
                            {
                                Console.WriteLine($"[Giamdoc/XuLyPhieumuahang] Lỗi gửi email khi duyệt: {exInner.Message}");
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Giamdoc/XuLyPhieumuahang] Lỗi gửi email khi duyệt: {ex.Message}");
                    }
                }
            }
            else if (action == "reject")
            {
                Xulituchoiyeucau(MaMuahang,null,null, phieumuahang, vtphieumuahang);
                _context.SaveChanges();
            }
            return RedirectToAction("Phieumuahang", "Yeucau", new { area = "Giamdoc" });
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
                NgayNhapkho = null,
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

            return RedirectToAction("Phieumuahang", "Yeucau", new { area = "Giamdoc" });
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

        [HttpPost]
        public IActionResult ThemPhieunhapkhoSQL(phieunhapkho phieunhapkho, vtphieunhapkho vtphieunhapkho,
            string[] TenSanpham, string[] MaSanpham, string[] HangSX, string[] NhaCC,
            int[] SL, string[] DonVi, string[] Makho, string LoaiNhapkho, decimal[] DonGia, string[] DiengiaiNhapKho)
        {
            var maNv = HttpContext.Session.GetString("MaNguoidung");
            var chucVu = HttpContext.Session.GetString("Chucvu");
            if (string.IsNullOrEmpty(maNv))
            {
                TempData["Error"] = "Session đã hết hạn. Vui lòng đăng nhập lại!";
                return RedirectToAction("Login", "Home", new { area = "" });
            }

            string currentArea = "Giamdoc";

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

                // Xác định người dùng cho phiếu nhập kho
                // Nếu là nhập kho dự án, cần tìm người yêu cầu ban đầu của dự án dựa trên lịch sử vật tư
                if (string.IsNullOrEmpty(phieunhapkho.MaNguoidung))
                {
                    if (LoaiNhapkho == "duan" && !string.IsNullOrEmpty(phieunhapkho.MaDuan))
                    {
                        phieunhapkho.MaNguoidung = TimNguoiYeuCauBanDauCuaDuan(phieunhapkho.MaDuan, maNv);
                    }
                    else
                    {
                        phieunhapkho.MaNguoidung = maNv;
                    }
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

                int STT = 1;
                string MaNhapkho;

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
                phieunhapkho.NgayNhapkho = null;

                // Kiểm tra nếu người tạo là giám đốc
                bool isGiamDoc = chucVu == "Giám đốc";

                // Lấy thông tin người yêu cầu ban đầu để xác định trạng thái phiếu
                string maNguoiYeuCauBanDau = phieunhapkho.MaNguoidung ?? maNv;
                string boPhanNguoiYeuCauBanDau = "";
                string chucVuNguoiYeuCauBanDau = "";

                if (LoaiNhapkho == "duan" && !string.IsNullOrEmpty(phieunhapkho.MaDuan))
                {
                    // Tìm người yêu cầu ban đầu của dự án
                    maNguoiYeuCauBanDau = TimNguoiYeuCauBanDauCuaDuan(phieunhapkho.MaDuan, maNv);
                    var nguoiYeuCauBanDau = _context.nguoidungs.FirstOrDefault(n => n.MaNguoidung == maNguoiYeuCauBanDau);
                    boPhanNguoiYeuCauBanDau = nguoiYeuCauBanDau?.Bophan ?? "";
                    chucVuNguoiYeuCauBanDau = nguoiYeuCauBanDau?.Chucvu ?? "";
                }

                // Xác định trạng thái phiếu nhập kho
                if (!string.IsNullOrEmpty(phieunhapkho.MaDuan))
                {
                    // Nếu có mã dự án
                    if (isGiamDoc)
                    {
                        // Giám đốc tạo: đã được duyệt rồi
                        phieunhapkho.TrangThai = "Chờ nhập kho";
                    }
                    else
                    {
                        // Kiểm tra bộ phận và chức vụ của người yêu cầu ban đầu
                        // Nếu người yêu cầu ban đầu thuộc BP dự án HOẶC có chức vụ Quản lí dự án/Quản lý dự án
                        // thì chuyển thẳng sang Giám đốc duyệt (không cần qua quản lý dự án)
                        bool laNguoiThuocDuAn = (!string.IsNullOrEmpty(boPhanNguoiYeuCauBanDau) && 
                                                 boPhanNguoiYeuCauBanDau.Trim().Equals("BP dự án", StringComparison.OrdinalIgnoreCase)) ||
                                                (!string.IsNullOrEmpty(chucVuNguoiYeuCauBanDau) && 
                                                 (chucVuNguoiYeuCauBanDau.Trim().Equals("Quản lí dự án", StringComparison.OrdinalIgnoreCase) ||
                                                  chucVuNguoiYeuCauBanDau.Trim().Equals("Quản lý dự án", StringComparison.OrdinalIgnoreCase)));
                        
                        if (laNguoiThuocDuAn)
                        {
                            // Người yêu cầu ban đầu thuộc BP dự án: Chờ giám đốc duyệt
                            phieunhapkho.TrangThai = "Chờ giám đốc duyệt";
                        }
                        else
                        {
                            // Người yêu cầu ban đầu không thuộc BP dự án: Chờ quản lý dự án duyệt
                            phieunhapkho.TrangThai = "Chờ quản lý dự án duyệt";
                        }
                    }
                }
                else
                {
                    // Nếu không có mã dự án (cá nhân): Trạng thái ban đầu = "Chờ giám đốc duyệt" (để Giám đốc duyệt)
                    phieunhapkho.TrangThai = isGiamDoc ? "Chờ nhập kho" : "Chờ giám đốc duyệt";
                }

                if (string.IsNullOrEmpty(phieunhapkho.MaYeucau))
                {
                    // Tìm người yêu cầu ban đầu để tạo mã yêu cầu
                    maNguoiYeuCauBanDau = phieunhapkho.MaNguoidung ?? maNv;
                    if (LoaiNhapkho == "duan" && !string.IsNullOrEmpty(phieunhapkho.MaDuan))
                    {
                        maNguoiYeuCauBanDau = TimNguoiYeuCauBanDauCuaDuan(phieunhapkho.MaDuan, maNguoiYeuCauBanDau);
                    }

                    string maYeucauBase = "";
                    if (LoaiNhapkho == "duan" && !string.IsNullOrEmpty(phieunhapkho.MaDuan))
                    {
                        // Tạo mã yêu cầu dạng: NHAPKHO_DUAN_{MaDuan}_{MaNguoiDung}
                        maYeucauBase = $"NHAPKHO_DUAN_{phieunhapkho.MaDuan}_{maNguoiYeuCauBanDau}";
                    }
                    else if (LoaiNhapkho == "canhan")
                    {
                        maYeucauBase = $"NHAPKHO_CANHAN_{maNv}";
                    }
                    else
                    {
                        maYeucauBase = $"NHAPKHO_TUDO_{maNv}_{DateTime.Now:yyyyMMddHHmmss}";
                    }

                    // Tìm mã yêu cầu phù hợp
                    // Nếu là giám đốc tạo, luôn tạo mã mới (không cập nhật vào phiếu cũ)
                    string maYeucauDacBiet = maYeucauBase;
                    int suffixNumber = 0;
                    var existingYeucauDacBiet = _context.yeucau
                        .FirstOrDefault(y => y.MaYeucau == maYeucauDacBiet);
                    
                    if (isGiamDoc)
                    {
                        // Giám đốc tạo: luôn tạo mã mới với số thứ tự tăng dần
                        while (existingYeucauDacBiet != null)
                        {
                            suffixNumber++;
                            maYeucauDacBiet = $"{maYeucauBase}{suffixNumber}";
                            existingYeucauDacBiet = _context.yeucau
                                .FirstOrDefault(y => y.MaYeucau == maYeucauDacBiet);
                        }
                    }
                    else
                    {
                        // Không phải giám đốc: cho phép tạo mới nếu yêu cầu cũ đã "Đã nhập kho"
                        while (existingYeucauDacBiet != null)
                        {
                            // Đã có yêu cầu, kiểm tra xem có phiếu nào với mã yêu cầu này đã "Đã nhập kho" chưa
                            var phieuDaNhapKho = _context.phieunhapkho
                                .Any(p => p.MaYeucau == maYeucauDacBiet && 
                                         p.TrangThai != null && 
                                         p.TrangThai.Trim().Equals("Đã nhập kho", StringComparison.OrdinalIgnoreCase));
                            
                            if (phieuDaNhapKho)
                            {
                                // Có phiếu đã nhập kho, tạo mã mới với số thứ tự tăng dần
                                suffixNumber++;
                                maYeucauDacBiet = $"{maYeucauBase}{suffixNumber}";
                                existingYeucauDacBiet = _context.yeucau
                                    .FirstOrDefault(y => y.MaYeucau == maYeucauDacBiet);
                            }
                            else
                            {
                                // Chưa có phiếu nào nhập kho, sử dụng lại mã yêu cầu này
                                break;
                            }
                        }
                    }

                    // Kiểm tra lại sau khi tìm được mã phù hợp
                    existingYeucauDacBiet = _context.yeucau
                        .FirstOrDefault(y => y.MaYeucau == maYeucauDacBiet);

                    if (existingYeucauDacBiet == null)
                    {
                        string ycMaDuan = null;
                        if (!string.IsNullOrEmpty(phieunhapkho.MaDuan))
                        {
                            var duanExists = _context.duans
                                .FirstOrDefault(d => d.MaDuan == phieunhapkho.MaDuan);

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
                            }
                            else
                            {
                                var allDuans = _context.duans.Select(d => d.MaDuan).ToList();
                                Console.WriteLine($"Warning: Mã dự án '{phieunhapkho.MaDuan}' không tồn tại trong bảng duans.");
                                Console.WriteLine($"Available project codes: {string.Join(", ", allDuans)}");
                            }
                        }

                        // Sử dụng lại người yêu cầu ban đầu đã tìm ở trên
                        string tenNguoiYeuCauBanDau = "";
                        // Reset lại bộ phận nếu cần
                        boPhanNguoiYeuCauBanDau = "";

                        var nguoiDung = _context.nguoidungs.FirstOrDefault(n => n.MaNguoidung == maNguoiYeuCauBanDau);
                        tenNguoiYeuCauBanDau = nguoiDung?.TenNguoidung ?? "";
                        boPhanNguoiYeuCauBanDau = nguoiDung?.Bophan ?? "";
                        string chucVuNguoiTao = nguoiDung?.Chucvu ?? "";

                        // Xác định trạng thái: nếu giám đốc tạo thì trạng thái là "Chờ nhập kho"
                        string trangThaiYeucau;
                        if (isGiamDoc)
                        {
                            trangThaiYeucau = "Chờ nhập kho";
                        }
                        else
                        {
                            if (LoaiNhapkho == "duan" && !string.IsNullOrEmpty(phieunhapkho.MaDuan))
                            {
                                // Kiểm tra bộ phận và chức vụ của người tạo
                                // Nếu người tạo thuộc BP dự án HOẶC có chức vụ Quản lí dự án/Quản lý dự án
                                // thì chuyển thẳng sang Giám đốc duyệt (không cần qua quản lý dự án)
                                bool laNguoiThuocDuAn = (!string.IsNullOrEmpty(boPhanNguoiYeuCauBanDau) && 
                                                         boPhanNguoiYeuCauBanDau.Trim().Equals("BP dự án", StringComparison.OrdinalIgnoreCase)) ||
                                                        (!string.IsNullOrEmpty(chucVuNguoiTao) && 
                                                         (chucVuNguoiTao.Trim().Equals("Quản lí dự án", StringComparison.OrdinalIgnoreCase) ||
                                                          chucVuNguoiTao.Trim().Equals("Quản lý dự án", StringComparison.OrdinalIgnoreCase)));
                                
                                if (laNguoiThuocDuAn)
                                {
                                    // Người tạo thuộc BP dự án hoặc có chức vụ quản lý dự án: Chờ giám đốc duyệt
                                    trangThaiYeucau = "Chờ giám đốc duyệt";
                                }
                                else
                                {
                                    // Người tạo không thuộc BP dự án: Chờ quản lý dự án duyệt
                                    trangThaiYeucau = "Chờ quản lý dự án duyệt";
                                }
                            }
                            else if (LoaiNhapkho == "canhan")
                            {
                                trangThaiYeucau = "Chờ giám đốc duyệt";
                            }
                            else
                            {
                                trangThaiYeucau = "Đã duyệt";
                            }
                        }

                        var newYeucauDacBiet = new yeucau
                        {
                            MaYeucau = maYeucauDacBiet,
                            TenYeucau = "Yêu cầu nhập kho",
                            YCMaNguoidung = maNguoiYeuCauBanDau, // Sử dụng người yêu cầu ban đầu
                            NguoiYeucau = tenNguoiYeuCauBanDau,
                            Bophan = boPhanNguoiYeuCauBanDau,
                            YCMaDuan = ycMaDuan,
                            NgayYeucau = DateTime.Now,
                            TrangThai = trangThaiYeucau
                        };
                        _context.yeucau.Add(newYeucauDacBiet);
                        _context.SaveChanges();
                    }

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
                        TrangThai = phieunhapkho.TrangThai, // Đồng bộ với trạng thái phiếu (đã được xử lý ở trên)
                        MaNhapkho = MaNhapkho,
                        MaYeucau = phieunhapkho.MaYeucau
                    };

                    _context.vtphieunhapkho.Add(newvtphieunhapkho);
                }

                _context.SaveChanges();

                TempData["Success"] = "Tạo phiếu nhập kho thành công!";
                return RedirectToAction("Phieunhapkho", "Yeucau", new { area = currentArea });
            }
            catch (Exception ex)
            {
              
                if (ex.InnerException != null)
                {
                    
                }
                Console.WriteLine("==========================================");

                var maNvCheck = HttpContext.Session.GetString("MaNguoidung") ?? maNv;
                

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

        [HttpPost]
        public IActionResult Xuliphieunhapkho(
                                string MaNhapkho, string submitAction,
                                phieuxuatkho phieunhapkho,
                                vtphieuxuatkho vtphieunhapkho, phieuxuatkho phieuxuatkho, vtphieuxuatkho vtphieuxuatkho)
        {
            Console.WriteLine("===========================================");
            Console.WriteLine("[GIAMDOC DEBUG] Xuliphieunhapkho called");
            Console.WriteLine($"[GIAMDOC DEBUG] MaNhapkho: {MaNhapkho}");
            Console.WriteLine($"[GIAMDOC DEBUG] submitAction: {submitAction}");
            
            var chucVu2 = HttpContext.Session.GetString("Chucvu");
            var boPhan2 = HttpContext.Session.GetString("Bophan");
            var maNv2 = HttpContext.Session.GetString("MaNguoidung");
            
            Console.WriteLine($"[GIAMDOC DEBUG] Session - Chucvu: {chucVu2}, Bophan: {boPhan2}, MaNguoidung: {maNv2}");

            var Phieunhapkho = _context.phieunhapkho.FirstOrDefault(p => p.MaNhapkho == MaNhapkho);
            if (Phieunhapkho == null)
            {
                Console.WriteLine($"[GIAMDOC DEBUG] ERROR: Không tìm thấy phiếu nhập kho với MaNhapkho: {MaNhapkho}");
                return NotFound();
            }

            Console.WriteLine($"[GIAMDOC DEBUG] Phieunhapkho found - TrangThai: {Phieunhapkho.TrangThai}, MaDuan: {Phieunhapkho.MaDuan}");

            // Kiểm tra: Nếu phiếu đã "Đã nhập kho" thì khóa lại, không cho phép xử lý
            if (Phieunhapkho.TrangThai != null && Phieunhapkho.TrangThai.Trim().Equals("Đã nhập kho", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"[GIAMDOC DEBUG] Phiếu đã 'Đã nhập kho', không cho phép xử lý");
                TempData["Error"] = "Phiếu nhập kho đã được nhập kho, không thể xử lý thêm!";
                return RedirectToAction("Phieunhapkho", "Yeucau", new { area = "Giamdoc" });
            }

            var VTPhieunhapkholist = _context.vtphieunhapkho.Where(vt => vt.MaNhapkho == MaNhapkho).ToList();
            Console.WriteLine($"[GIAMDOC DEBUG] Số lượng vật tư: {VTPhieunhapkholist.Count}");

            if (submitAction == "approve")
            {
                // Workflow duyệt:
                // 1. "Chờ quản lý dự án duyệt" (nếu có dự án) -> Quản lý dự án duyệt -> "Chờ giám đốc duyệt" (xử lý ở QuanLiDuAn)
                //    HOẶC Giám đốc có thể duyệt trực tiếp:
                //    - Vật tư thuộc BP dự án: "Chờ quản lý dự án duyệt" hoặc "Chờ giám đốc duyệt" -> "Chờ nhập kho"
                //    - Vật tư thuộc bộ phận khác: "Chờ quản lý dự án duyệt" hoặc "Chờ giám đốc duyệt" -> "Chờ quản lý dự án duyệt" (để QLDA duyệt)
                // 2. "Chờ nhập kho" -> Kho xử lý -> "Đã nhập kho" và cộng vào kho tổng

                var trangThaiHienTai = Phieunhapkho.TrangThai?.Trim() ?? "";
                bool isChoQuanLyDuanDuyet = trangThaiHienTai.Equals("Chờ quản lý dự án duyệt", StringComparison.OrdinalIgnoreCase);
                bool isChoGiamDocDuyet = trangThaiHienTai.Equals("Chờ giám đốc duyệt", StringComparison.OrdinalIgnoreCase);
                
                // Giám đốc có thể duyệt từ "Chờ quản lý dự án duyệt" hoặc "Chờ giám đốc duyệt"
                if ((isChoQuanLyDuanDuyet || isChoGiamDocDuyet) && chucVu2 == "Giám đốc")
                {
                    Console.WriteLine($"[GIAMDOC DEBUG] Trạng thái là '{trangThaiHienTai}' và chucVu2 == 'Giám đốc'");
                    Console.WriteLine("[GIAMDOC DEBUG] Giám đốc duyệt - Tất cả vật tư chuyển sang 'Chờ nhập kho'");
                    
                    // Lưu thời gian duyệt
                    var ngayDuyet = DateTime.Now;
                    
                    // Khi giám đốc duyệt, TẤT CẢ vật tư đều chuyển sang "Chờ nhập kho"
                    // Vì giám đốc là cấp cao nhất, đã duyệt rồi thì không cần quản lý dự án duyệt nữa
                    foreach (var vt in VTPhieunhapkholist)
                    {
                        vt.TrangThai = "Chờ nhập kho";
                        _context.vtphieunhapkho.Update(vt);
                        Console.WriteLine($"[GIAMDOC DEBUG] Vật tư {vt.TenSanpham} -> 'Chờ nhập kho'");
                    }
                    
                    // Cập nhật trạng thái phiếu nhập kho: "Chờ nhập kho"
                    Phieunhapkho.TrangThai = "Chờ nhập kho";
                    Console.WriteLine($"[GIAMDOC DEBUG] Phiếu nhập kho -> 'Chờ nhập kho' ({VTPhieunhapkholist.Count} vật tư)");
                    _context.phieunhapkho.Update(Phieunhapkho);
                    
                    // Gửi thông báo đến kho sau khi Giám đốc duyệt phiếu nhập kho
                    var maNhapkhoForEmail = Phieunhapkho.MaNhapkho;
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            using (var scope = _serviceScopeFactory.CreateScope())
                            {
                                var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();
                                await emailService.SendNotificationToWarehouseOnNhapKhoAsync(maNhapkhoForEmail);
                            }
                        }
                        catch (Exception exInner)
                        {
                            Console.WriteLine($"[Giamdoc/XuLyPhieunhapkho] Lỗi gửi email kho (nhập kho): {exInner.Message}");
                        }
                    });
                    
                    // Cập nhật NgayDuyet cho tất cả vật tư yêu cầu đã được giám đốc duyệt
                    // Lấy danh sách tất cả các MaYeucau từ các vật tư trong phiếu
                    var danhSachMaYeucauDaDuyet = VTPhieunhapkholist
                        .Where(vt => !string.IsNullOrEmpty(vt.MaYeucau))
                        .Select(vt => vt.MaYeucau)
                        .Distinct()
                        .ToList();
                    
                    foreach (var maYc in danhSachMaYeucauDaDuyet)
                    {
                        var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == maYc);
                        if (yeucau != null)
                        {
                            var vtYeucauList = _context.vtyeucau
                                .Where(vt => vt.VTMaYeucau == maYc && !vt.NgayDuyet.HasValue)
                                .ToList();
                            
                            // Cập nhật NgayDuyet cho tất cả vật tư yêu cầu tương ứng
                            foreach (var vtYc in vtYeucauList)
                            {
                                vtYc.NgayDuyet = ngayDuyet;
                                _context.vtyeucau.Update(vtYc);
                            }
                        }
                    }
                }
                else if (Phieunhapkho.TrangThai == "Chờ nhập kho" && boPhan2 == "BP kho")
                {
                    // Kho xử lý nhập kho
                    Phieunhapkho.TrangThai = "Đã nhập kho";
                    // Lưu thời gian nhập kho khi bộ phận kho nhập kho
                    Phieunhapkho.NgayNhapkho = DateTime.Now;
                    bool isNhapKhoDuanOrCaNhan = IsNhapKhoDuanOrCaNhan(Phieunhapkho);
                    
                    // Cập nhật tồn kho khi nhập hàng
                    foreach (var VTPhieunhapkho in VTPhieunhapkholist)
                    {
                        // Nếu là nhập kho dự án/cá nhân, trừ số lượng từ kho dự án/cá nhân
                        if (isNhapKhoDuanOrCaNhan)
                        {
                            if (!string.IsNullOrEmpty(Phieunhapkho.MaDuan) && !string.IsNullOrEmpty(VTPhieunhapkho.MaSanpham))
                            {
                                TruKhoDuanKhiNhapKho(Phieunhapkho, VTPhieunhapkho);
                            }
                        }
                        
                        // Tìm vật tư trong tồn kho
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

                    // Đồng bộ trạng thái vật tư yêu cầu sau khi hàng đã nhập kho (mua hàng về)
                    var maYeucauList = VTPhieunhapkholist
                        .Select(v => v.MaYeucau)
                        .Where(ma => !string.IsNullOrEmpty(ma))
                        .Distinct()
                        .ToList();

                    foreach (var maYc in maYeucauList)
                    {
                        var vtList = _context.vtyeucau
                            .Where(v => v.VTMaYeucau == maYc)
                            .ToList();

                        foreach (var vtYc in vtList)
                        {
                            if (vtYc.TrangThai == "Đang mua hàng" || vtYc.TrangThai == "Đang chờ báo giá")
                            {
                                vtYc.TrangThai = "Chờ xuất kho";
                                _context.vtyeucau.Update(vtYc);
                            }
                        }

                        var yeuCau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == maYc);
                        if (yeuCau != null)
                        {
                            // Kiểm tra xem có phải là yêu cầu nhập kho không (NHAPKHO_CANHAN, NHAPKHO_DUAN)
                            bool isNhapKhoRequest = !string.IsNullOrEmpty(maYc) && 
                                (maYc.StartsWith("NHAPKHO_CANHAN_", StringComparison.OrdinalIgnoreCase) ||
                                 maYc.StartsWith("NHAPKHO_DUAN_", StringComparison.OrdinalIgnoreCase));
                            
                            if (isNhapKhoRequest)
                            {
                                // Đối với yêu cầu nhập kho hoàn trả, khi kho duyệt thì trạng thái là "Đã nhập kho"
                                if (Phieunhapkho.TrangThai == "Đã nhập kho")
                                {
                                    yeuCau.TrangThai = "Đã nhập kho";
                                    
                                    // ⭐ Cập nhật trạng thái vật tư và yêu cầu gốc (yêu cầu bị giảm số lượng)
                                    // Tìm yêu cầu gốc dựa trên MaDuan hoặc MaNguoidung từ phiếu nhập kho hoàn trả
                                    var maDuan = Phieunhapkho.MaDuan;
                                    var maNguoidung = Phieunhapkho.MaNguoidung;
                                    
                                    // Tìm tất cả yêu cầu gốc có cùng MaDuan hoặc MaNguoidung
                                    var yeucauGocList = _context.yeucau
                                        .Where(y => !string.IsNullOrEmpty(y.MaYeucau) && 
                                                   !y.MaYeucau.StartsWith("NHAPKHO_", StringComparison.OrdinalIgnoreCase) &&
                                                   ((!string.IsNullOrEmpty(maDuan) && y.YCMaDuan == maDuan) ||
                                                    (!string.IsNullOrEmpty(maNguoidung) && y.YCMaNguoidung == maNguoidung)))
                                        .ToList();
                                    
                                    // Lấy danh sách vật tư đã nhập trả (bao gồm cả phiếu vừa được duyệt)
                                    var vtDaXuatHopLe = _context.vtphieuxuatkho
                                        .Where(vt => new[] { "Hoàn thành", "Đã xuất kho", "Đã lấy hàng", "Chờ người yêu cầu xác nhận", "Đang chuẩn bị hàng" }
                                            .Contains(vt.TrangThai))
                                        .ToList();
                                    
                                    var vtDaNhapTra = _context.vtphieunhapkho
                                        .Where(vt => vt.TrangThai == "Đã nhập kho" &&
                                                   !string.IsNullOrWhiteSpace(vt.DiengiaiNhapKho) &&
                                                   vt.DiengiaiNhapKho.IndexOf("trả", StringComparison.OrdinalIgnoreCase) >= 0)
                                        .ToList();
                                    
                                    foreach (var ycGoc in yeucauGocList)
                                    {
                                        // Lấy mã yêu cầu cơ bản
                                        var parts = ycGoc.MaYeucau.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                                        string baseMaYeucau = parts.Length > 2 ? string.Join(" ", parts.Take(parts.Length - 1)) : ycGoc.MaYeucau;
                                        
                                        // Lấy danh sách vật tư yêu cầu gốc
                                        var vtListGoc = _context.vtyeucau
                                            .Where(v => v.VTMaYeucau == ycGoc.MaYeucau)
                                            .ToList();
                                        
                                        foreach (var vtYcGoc in vtListGoc)
                                        {
                                            // Tính lại số lượng đã cấp thực tế (sau khi nhập kho hoàn trả)
                                            int soLuongDaCap = TinhSoLuongDaCapThucTe(baseMaYeucau, vtYcGoc.MaSanpham ?? "", vtDaXuatHopLe, vtDaNhapTra);
                                            var soLuongMoi = vtYcGoc.SLMoi ?? vtYcGoc.SL ?? 0;
                                            
                                            // Nếu số lượng mới = số lượng đã cấp, cập nhật trạng thái thành "Đã xuất kho"
                                            if (soLuongMoi == soLuongDaCap)
                                            {
                                                vtYcGoc.TrangThai = "Đã xuất kho";
                                                _context.vtyeucau.Update(vtYcGoc);
                                            }
                                        }
                                        
                                        // Cập nhật trạng thái yêu cầu gốc
                                        var allVtListGoc = _context.vtyeucau
                                            .Where(v => v.VTMaYeucau == ycGoc.MaYeucau)
                                            .ToList();
                                        
                                        var hasDangMuaHang = allVtListGoc.Any(vt => vt.TrangThai == "Đang mua hàng");
                                        var hasChoXuatKho = allVtListGoc.Any(vt => vt.TrangThai == "Chờ xuất kho");
                                        var hasDaXuatKho = allVtListGoc.Any(vt => vt.TrangThai == "Đã xuất kho");
                                        var hasDaNhapKho = allVtListGoc.Any(vt => vt.TrangThai == "Đã nhập kho");
                                        var allDaXuatOrRejected = allVtListGoc.All(vt =>
                                            vt.TrangThai == "Đã xuất kho" ||
                                            (!string.IsNullOrEmpty(vt.TrangThai) && vt.TrangThai.Contains("Đã từ chối")));
                                        
                                        if (hasDaNhapKho)
                                        {
                                            // Nếu có vật tư đã nhập kho, kiểm tra xem tất cả vật tư đã nhập kho chưa
                                            var allDaNhapKho = allVtListGoc.All(vt =>
                                                vt.TrangThai == "Đã nhập kho" ||
                                                (!string.IsNullOrEmpty(vt.TrangThai) && vt.TrangThai.Contains("Đã từ chối")));
                                            
                                            if (allDaNhapKho)
                                            {
                                                ycGoc.TrangThai = "Đã nhập kho";
                                            }
                                            else
                                            {
                                                // Có một số vật tư đã nhập kho nhưng chưa tất cả, kiểm tra các trạng thái khác
                                                if (hasDangMuaHang)
                                                {
                                                    ycGoc.TrangThai = "Đang mua hàng";
                                                }
                                                else if (hasChoXuatKho)
                                                {
                                                    ycGoc.TrangThai = "Chờ xuất kho";
                                                }
                                                else if (hasDaXuatKho)
                                                {
                                                    ycGoc.TrangThai = "Đã xuất kho";
                                                }
                                                else
                                                {
                                                    ycGoc.TrangThai = "Đã nhập kho";
                                                }
                                            }
                                        }
                                        else if (allDaXuatOrRejected)
                                        {
                                            ycGoc.TrangThai = "Đã xuất kho";
                                        }
                                        else if (hasDangMuaHang)
                                        {
                                            ycGoc.TrangThai = "Đang mua hàng";
                                        }
                                        else if (hasChoXuatKho)
                                        {
                                            ycGoc.TrangThai = "Chờ xuất kho";
                                        }
                                        
                                        _context.yeucau.Update(ycGoc);
                                    }
                                }
                            }
                            else
                            {
                                // Đối với yêu cầu xuất kho thông thường
                                var hasDangMuaHang = vtList.Any(v => v.TrangThai == "Đang mua hàng");
                                var hasChoXuatKho = vtList.Any(v => v.TrangThai == "Chờ xuất kho");
                                var hasDaXuatKho = vtList.Any(v => v.TrangThai == "Đã xuất kho");
                                var hasDaNhapKho = vtList.Any(v => v.TrangThai == "Đã nhập kho");
                                var allDaXuatOrRejected = vtList.All(v =>
                                    v.TrangThai == "Đã xuất kho" ||
                                    (!string.IsNullOrEmpty(v.TrangThai) && v.TrangThai.Contains("Đã từ chối")));

                                if (hasDaNhapKho)
                                {
                                    // Nếu có vật tư đã nhập kho, kiểm tra xem tất cả vật tư đã nhập kho chưa
                                    var allDaNhapKho = vtList.All(v =>
                                        v.TrangThai == "Đã nhập kho" ||
                                        (!string.IsNullOrEmpty(v.TrangThai) && v.TrangThai.Contains("Đã từ chối")));
                                    
                                    if (allDaNhapKho)
                                    {
                                        yeuCau.TrangThai = "Đã nhập kho";
                                    }
                                    else
                                    {
                                        // Có một số vật tư đã nhập kho nhưng chưa tất cả, kiểm tra các trạng thái khác
                                        if (hasDangMuaHang)
                                        {
                                            yeuCau.TrangThai = "Đang mua hàng";
                                        }
                                        else if (hasChoXuatKho)
                                        {
                                            yeuCau.TrangThai = "Chờ xuất kho";
                                        }
                                        else if (hasDaXuatKho)
                                        {
                                            yeuCau.TrangThai = "Đã xuất kho";
                                        }
                                        else
                                        {
                                            yeuCau.TrangThai = "Đã nhập kho";
                                        }
                                    }
                                }
                                else if (allDaXuatOrRejected)
                                {
                                    yeuCau.TrangThai = "Đã xuất kho";
                                }
                                else if (hasDangMuaHang)
                                {
                                    yeuCau.TrangThai = "Đang mua hàng";
                                }
                                else if (hasChoXuatKho)
                                {
                                    yeuCau.TrangThai = "Chờ xuất kho";
                                }
                            }

                            _context.yeucau.Update(yeuCau);
                        }
                    }

                    // Sau khi nhập kho, cập nhật lại các phiếu xuất đang chờ theo mã yêu cầu tương ứng
                    var isNhapKhoOnlyFlow = !string.IsNullOrEmpty(Phieunhapkho.MaYeucau)
                                            && Phieunhapkho.MaYeucau.StartsWith("NHAPKHO_", StringComparison.OrdinalIgnoreCase);
                    if (!string.IsNullOrEmpty(Phieunhapkho.MaYeucau) && !isNhapKhoOnlyFlow)
                    {
                        var phieuXuatLienQuanList = _context.phieuxuatkho
                            .Where(px => px.MaYeucau == Phieunhapkho.MaYeucau)
                            .ToList();
                        var phieuXuatLienQuan = phieuXuatLienQuanList.FirstOrDefault();

                        foreach (var pxLienQuan in phieuXuatLienQuanList)
                        {
                            PhieuXuatAllocationHelper.CapNhatPhieuXuatSauNhapHang(_context, pxLienQuan, VTPhieunhapkholist);
                        }
                        
                        
                        var vatTuChuaXuat = _context.vtyeucau
                            .Where(vt => vt.VTMaYeucau == Phieunhapkho.MaYeucau
                                && vt.TrangThai == "Chờ xuất kho"
                                && !_context.vtphieuxuatkho.Any(vtx => vtx.MaYeucau == vt.VTMaYeucau && vtx.MaSanpham == vt.MaSanpham))
                            .ToList();
                        
                        if (vatTuChuaXuat.Any())
                        {
                            // Nếu chưa có phiếu xuất kho, tạo mới
                            if (phieuXuatLienQuan == null)
                            {
                                int Numberpxk = 1;
                                string MaXuatkho;
                                while (true)
                                {
                                    MaXuatkho = $"PXK{Numberpxk}";
                                    var existingEntry = _context.phieuxuatkho
                                        .FirstOrDefault(y => y.MaXuatkho == MaXuatkho);
                                    if (existingEntry == null)
                                    {
                                        break;
                                    }
                                    Numberpxk++;
                                }
                                
                                var yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == Phieunhapkho.MaYeucau);
                                phieuXuatLienQuan = new phieuxuatkho
                                {
                                    MaXuatkho = MaXuatkho,
                                    MaYeucau = Phieunhapkho.MaYeucau,
                                    MaDuan = yeucau?.YCMaDuan,
                                    MaNguoidung = yeucau?.YCMaNguoidung,
                                    NgayXuatkho = null,
                                    TrangThai = "Đang chuẩn bị hàng"
                                };
                                _context.phieuxuatkho.Add(phieuXuatLienQuan);
                                _context.SaveChanges();

                                phieuXuatLienQuanList.Add(phieuXuatLienQuan);
                            }
                            
                            // Thêm các vật tư đang chờ xuất kho vào phiếu xuất kho
                            foreach (var vatTu in vatTuChuaXuat)
                            {
                                var khotong = _context.khotongs.FirstOrDefault(kt => 
                                    kt.Makho == vatTu.YCMakho && 
                                    kt.MaSanpham == vatTu.MaSanpham)
                                    ?? _context.khotongs.FirstOrDefault(kt => 
                                        kt.MaSanpham == vatTu.MaSanpham);
                                
                                if (khotong != null)
                                {
                                    // FIFO: Tính số lượng hàng đã cam kết (chỉ tính vật tư duyệt trước)
                                    int soLuongDaCamKet = TinhSoLuongDaCamKet(khotong.Makho ?? "", khotong.MaSanpham ?? "", vatTu.NgayDuyet, phieuXuatLienQuan.MaXuatkho);
                                    int soLuongKhaDung = (khotong.SL ?? 0) - soLuongDaCamKet;
                                    int soLuongYeuCau = vatTu.SL ?? 0;
                                    int soLuongXuat = Math.Max(0, Math.Min(soLuongKhaDung, soLuongYeuCau));
                                    
                                    if (soLuongXuat > 0)
                                    {
                                        var VTPhieuxuatkho = new vtphieuxuatkho
                                        {
                                            MaXuatkho = phieuXuatLienQuan.MaXuatkho,
                                            MaYeucau = vatTu.VTMaYeucau,
                                            TenSanpham = khotong.TenSanpham,
                                            MaSanpham = khotong.MaSanpham,
                                            Makho = khotong.Makho,
                                            HangSX = khotong.HangSX,
                                            NhaCC = khotong.NhaCC,
                                            DonVi = khotong.DonVi,
                                            NgayBaohanh = khotong.NgayBaohanh,
                                            ThoiGianBH = khotong.ThoiGianBH,
                                            TrangThai = "Đang chuẩn bị hàng",
                                            SL = soLuongXuat
                                        };
                                        _context.vtphieuxuatkho.Add(VTPhieuxuatkho);
                                    }
                                }
                            }
                            _context.SaveChanges();
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"[GIAMDOC DEBUG] Không khớp điều kiện nào - TrangThai: {Phieunhapkho.TrangThai}, chucVu2: {chucVu2}, boPhan2: {boPhan2}");
                }

                _context.phieunhapkho.Update(Phieunhapkho);
            }
            else if (submitAction == "reject")
            {
                Console.WriteLine("[GIAMDOC DEBUG] Action là 'reject'");
                Phieunhapkho.TrangThai = $"{chucVu2} - Đã từ chối";
                foreach (var vt in VTPhieunhapkholist)
                {
                    vt.TrangThai = $"{chucVu2} - Đã từ chối";
                    _context.vtphieunhapkho.Update(vt);
                }
                _context.phieunhapkho.Update(Phieunhapkho);
            }
            
            // Console.WriteLine($"[GIAMDOC DEBUG] Trạng thái sau khi xử lý: {Phieunhapkho.TrangThai}");
            // Console.WriteLine("[GIAMDOC DEBUG] Đang lưu thay đổi...");
            _context.SaveChanges();
            // Console.WriteLine("[GIAMDOC DEBUG] Đã lưu thành công. Redirecting...");
            // Console.WriteLine("===========================================");
            return RedirectToAction("Phieunhapkho", "Yeucau", new { area = "Giamdoc" });
        }

        [HttpPost]
        public IActionResult Taophieuxuatkhobyphieunhapkho(string MaNhapkho, phieuxuatkho phieuxuatkho, vtphieuxuatkho vtphieuxuatkho, phieunhapkho phieunhapkho, vtphieunhapkho vtphieunhapkho)
        {
            var Phieunhapkho = _context.phieunhapkho.FirstOrDefault(p => p.MaNhapkho == MaNhapkho);
            var VTPhieunhapkholist = _context.vtphieunhapkho.Where(vt => vt.MaNhapkho == MaNhapkho).ToList();

            int STT = 1;
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
                NgayTao = DateTime.Now,
                TrangThai = "Chờ xác nhận"
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

            return RedirectToAction("Phieunhapkho", "Yeucau", new { area = "Giamdoc" });
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

            return RedirectToAction("Yeucau", "Yeucau", new { area = "Giamdoc" });
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

        private bool IsNhapKhoDuanOrCaNhan(phieunhapkho phieunhapkho)
        {
            return !string.IsNullOrEmpty(phieunhapkho.MaYeucau) &&
                   (phieunhapkho.MaYeucau.Contains("NHAPKHO_DUAN") ||
                    phieunhapkho.MaYeucau.Contains("NHAPKHO_TUDO") ||
                    phieunhapkho.MaYeucau.Contains("NHAPKHO_CANHAN"));
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
            List<string> allRelatedMaYeucau)
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

        private void TruKhoDuanKhiNhapKho(phieunhapkho phieunhapkho, vtphieunhapkho vtPhieunhapkho)
        {
            if (phieunhapkho == null || vtPhieunhapkho == null || string.IsNullOrEmpty(vtPhieunhapkho.MaSanpham))
            {
                return;
            }

            // Lấy tất cả mã yêu cầu có cùng mã cơ bản với yêu cầu gốc của phiếu nhập kho
            var maYeucauNhap = phieunhapkho.MaYeucau;
            if (string.IsNullOrEmpty(maYeucauNhap))
            {
                return;
            }

            string baseMaYeucau = YeucauUpdateHelper.GetBaseRequestCode(maYeucauNhap);
            var allRelatedMaYeucau = _context.yeucau
                .Where(y => !string.IsNullOrWhiteSpace(y.MaYeucau))
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

            var maXuatkhoList = _context.phieuxuatkho
                .Where(px => px.MaDuan == phieunhapkho.MaDuan
                             && allRelatedMaYeucau.Contains(px.MaYeucau))
                .Select(px => px.MaXuatkho)
                .Where(mx => !string.IsNullOrEmpty(mx))
                .ToList();

            var vtXuatKhoItems = _context.vtphieuxuatkho
                .Where(v => maXuatkhoList.Contains(v.MaXuatkho)
                            && !string.IsNullOrEmpty(v.MaSanpham))
                .ToList()
                .Where(v => GetMaSanphamBase(v.MaSanpham) == baseProductCode)
                .OrderBy(v => v.ID)
                .ToList();

            if (!vtXuatKhoItems.Any())
            {
                return;
            }

            // ===== CASE 1: CÙNG BASECODE DỰ ÁN → TRẢ 1 LẦN =====
            if (IsCaseBaseCodeDuan(phieunhapkho))
            {
                int slCanTra = slCanTraThucTe;

                if (slCanTra <= 0)
                    return;

                // ⭐ CHỈ TRẢ 1 DÒNG DUY NHẤT (FIFO)
                var vtItem = vtXuatKhoItems.FirstOrDefault();

                if (vtItem == null)
                    return;

                int slHienTai = vtItem.SL ?? 0;
                int slTru = Math.Min(slHienTai, slCanTra);

                vtItem.SL = slHienTai - slTru;

                if (vtItem.SL <= 0)
                    vtItem.TrangThai = "Đã trả kho";

                _context.vtphieuxuatkho.Update(vtItem);
                return;
            }

            // ===== CASE 2: KHÔNG PHẢI BASECODE / CÁ NHÂN → TRẢ TỪNG DÒNG =====
            int slConLai = vtPhieunhapkho.SL ?? 0;

            foreach (var vtItem in vtXuatKhoItems)
            {
                if (slConLai <= 0)
                    break;

                int slHienTai = vtItem.SL ?? 0;
                int slTru = Math.Min(slHienTai, slConLai);

                vtItem.SL = slHienTai - slTru;

                if (vtItem.SL <= 0)
                    vtItem.TrangThai = "Đã trả kho";

                _context.vtphieuxuatkho.Update(vtItem);

                slConLai -= slTru;
            }
        }

    }
}

