using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using Webkho_20241021.Areas.Giamdoc.Data;
using Webkho_20241021.Models;
using Webkho_20241021.Services;


namespace Webkho_20241021.Areas.Giamdoc.Controllers
{
    [Area("Giamdoc")]
    [Authorize(Roles = "Giám đốc")]
    public class YeucauController : Controller
    {
        private readonly ApplicationDbContext _context;
        public YeucauController(ApplicationDbContext context)
        {
            _context = context;
        }
        public IActionResult Yeucau()
        {
            var userRole = HttpContext.Session.GetString("Chucvu");

            var Yeucaulist = _context.yeucau.ToList();

            var PhieuMuaHangList = _context.phieumuahang.ToList();

            foreach (var yeucau in Yeucaulist)
            {
                var phieus = PhieuMuaHangList.Where(p => p.MaYeucau == yeucau.MaYeucau).ToList();

                if (phieus.Any(p => p.TrangThai != "Đã nhận hàng"))
                {
                    yeucau.TrangThai = "Đang mua hàng";
                }
            }

            _context.SaveChanges();

            // Sắp xếp yêu cầu: đưa những yêu cầu đang chờ Giám đốc duyệt lên đầu để xử lý trước
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

            var VTyeucaulist = _context.vtyeucau.ToList();
            var Duans = _context.duans.ToList();

            var model = new Yeucauviewmodel
            {
                Yeucau = SortedYeucaulist,
                VTyeucau = VTyeucaulist,
                Duans = Duans
            };

            return View(model);
        }

        public IActionResult Phieuxuatkho()
        {
            var Phieuxuatkholist = _context.phieuxuatkho
            .OrderByDescending(y => y.TrangThai == "Chờ lấy hàng")
            .ThenByDescending(y => y.TrangThai == "Đang chuẩn bị hàng")
            .ThenByDescending(y => y.NgayXuatkho)
            .ToList();
            var VTphieuxuatkholist = _context.vtphieuxuatkho.ToList();
            var model = new Phieuxuatkhoviewmodel
            {
                Phieuxuatkho = Phieuxuatkholist,
                VTphieuxuatkho = VTphieuxuatkholist,
            };
            return View(model);
        }

        public IActionResult Phieunhapkho()
        {
            var Phieunhapkholist = _context.phieunhapkho
            .OrderByDescending(y => y.NgayNhapkho)
            .ToList();              
            var VTphieunhapkholist = _context.vtphieunhapkho.ToList();
            var Duanslist = _context.duans.ToList();
            var model = new Phieunhapkhoviewmodel
            {
                Phieunhapkho = Phieunhapkholist,
                VTphieunhapkho = VTphieunhapkholist,
                Duans = Duanslist
            };
            return View(model);
        }

        public IActionResult Phieumuahang()
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

            // Xuất kho - chỉ đếm các trạng thái còn cần xử lý (không đếm "Hoàn thành" và "Đã xác nhận nhận hàng")
            int thongbaoxuatkhocount = 0;
            if (boPhan == "BP kho")
            {
                thongbaoxuatkhocount = _context.phieuxuatkho.Count(p => p.TrangThai != "Hoàn thành" && p.TrangThai != "Đã xác nhận nhận hàng");
            }
            else if (chucVu == "Giám đốc")
            {
                // Giám đốc xem tất cả phiếu xuất kho chưa hoàn thành
                thongbaoxuatkhocount = _context.phieuxuatkho.Count(p => p.TrangThai != "Hoàn thành" && p.TrangThai != "Đã xác nhận nhận hàng");
            }

            // Nhập kho
            int thongbaonhapkhocount = 0;
            if (boPhan == "BP kho")
            {
                thongbaonhapkhocount = _context.phieunhapkho.Count(p => p.TrangThai == "Chờ nhập kho" || p.TrangThai == "Sẵn sàng nhập kho");
            }
            else if (chucVu == "Giám đốc")
            {
                // Giám đốc xem tất cả phiếu nhập kho chờ xử lý
                thongbaonhapkhocount = _context.phieunhapkho.Count(p => p.TrangThai == "Chờ nhập kho" || p.TrangThai == "Sẵn sàng nhập kho" || p.TrangThai == "Giám đốc");
            }

            var Maduanquanli = _context.duans
                .Where(da => da.MaNguoiQLDA == maNv)
                .Select(da => da.MaDuan)
                .ToList();

            int QLDAyeucaucount = _context.yeucau.Count(p => p.TrangThai == "Quản lí dự án" && Maduanquanli.Contains(p.YCMaDuan));
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

            int Hoanthanhxuatkhocount = _context.phieuxuatkho.Count(p => p.TrangThai != "Hoàn thành");
            int thongbaoxuatkhocount = 0;
            if (boPhan == "BP kho")
            {
                thongbaoxuatkhocount = Hoanthanhxuatkhocount;
            }
            else if (chucVu == "Giám đốc")
            {
                // Giám đốc xem tất cả phiếu xuất kho chưa hoàn thành
                thongbaoxuatkhocount = _context.phieuxuatkho.Count(p => p.TrangThai != "Hoàn thành" && p.TrangThai != "Đã xác nhận nhận hàng");
            }
            

            int Hoanthanhnhapkhocount = _context.phieuxuatkho.Count(p => p.TrangThai != "Hoàn thành");
            int thongbaonhapkhocount = 0;
            if (boPhan == "BP kho")
            {
                thongbaonhapkhocount = Hoanthanhnhapkhocount;
            }
            else if (chucVu == "Giám đốc")
            {
                // Giám đốc xem tất cả phiếu nhập kho chờ xử lý
                thongbaonhapkhocount = _context.phieunhapkho.Count(p => p.TrangThai == "Chờ nhập kho" || p.TrangThai == "Sẵn sàng nhập kho" || p.TrangThai == "Giám đốc");
            }

            var Maduanquanli = _context.duans
                .Where(da => da.MaNguoiQLDA == maNv)
                .Select(da => da.MaDuan)
                .ToList();

            int QLDAyeucaucount = _context.yeucau.Count(p => p.TrangThai == "Quản lí dự án" && Maduanquanli.Contains(p.YCMaDuan));
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
                thongbaoxuatkhocount = _context.phieuxuatkho.Count(p => p.TrangThai != "Hoàn thành" && p.TrangThai != "Đã xác nhận nhận hàng");
            }

            int Hoanthanhnhapkhocount = _context.phieuxuatkho.Count(p => p.TrangThai != "Hoàn thành");
            int thongbaonhapkhocount = 0;
            if (boPhan == "BP kho")
            {
                thongbaonhapkhocount = Hoanthanhnhapkhocount;
            }
            else if (chucVu == "Giám đốc")
            {
                // Giám đốc xem tất cả phiếu nhập kho chờ xử lý
                thongbaonhapkhocount = _context.phieunhapkho.Count(p => p.TrangThai == "Chờ nhập kho" || p.TrangThai == "Sẵn sàng nhập kho" || p.TrangThai == "Giám đốc");
            }

            var Maduanquanli = _context.duans
                .Where(da => da.MaNguoiQLDA == maNv)
                .Select(da => da.MaDuan)
                .ToList();

            int QLDAyeucaucount = _context.yeucau.Count(p => p.TrangThai == "Quản lí dự án" && Maduanquanli.Contains(p.YCMaDuan));
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
            var vatTuList = _context.vtyeucau
                                 .Where(v => v.VTMaYeucau == MaYeucau).ToList();
            return Json(vatTuList);
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
                    vatTu.GhiChu = null; // Xóa ghi chú khi duyệt
                    
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
                            vatTu.TrangThai = "Đã xuất kho";
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

                        // Kiểm tra các vật tư đã được duyệt/xử lý (bao gồm cả "Đang mua hàng")
                        var approvedVatTu = vatTuListAfter.Where(v =>
                            v.TrangThai == "Đã xuất kho" ||
                            v.TrangThai == "Đã duyệt" ||
                            v.TrangThai == "Đang mua hàng").ToList();

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
                            var approvedMaSanphamList = approvedVatTu.Select(v => v.MaSanpham).ToList();
                            XuliphieuyeucauPartial(MaYeucau, approvedMaSanphamList);

                            // Sau khi tạo phiếu, kiểm tra lại trạng thái vật tư để quyết định trạng thái yêu cầu
                            _context.SaveChanges();
                            
                            // Reload lại danh sách vật tư từ database để lấy trạng thái mới nhất sau khi XuliphieuyeucauPartial chạy xong
                            _context.Entry(yeucau).Reload();
                            var vatTuListFinal = _context.vtyeucau
                                .Where(v => v.VTMaYeucau == MaYeucau).ToList();
                            
                            // Kiểm tra các trạng thái vật tư
                            var hasDangMuaHang = vatTuListFinal.Any(v =>
                                v.TrangThai == "Đang mua hàng");
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
                            else if (hasDaXuatKho)
                            {
                                // Có vật tư đã xuất kho và không còn vật tư đang mua hàng
                                // Kiểm tra xem tất cả vật tư đã xuất kho hoặc từ chối chưa
                                var allDaXuatKho = vatTuListFinal.All(v =>
                                    v.TrangThai == "Đã xuất kho" ||
                                    (!string.IsNullOrEmpty(v.TrangThai) && v.TrangThai.Contains("Đã từ chối")));
                                
                                if (allDaXuatKho)
                                {
                                    // Tất cả vật tư đã xuất kho hoặc bị từ chối → "Đã xuất kho"
                                    yeucau.TrangThai = "Đã xuất kho";
                                }
                                else
                                {
                                    // Có vật tư đã xuất kho nhưng còn vật tư khác chưa hoàn thành → "Đang mua hàng"
                                    yeucau.TrangThai = "Đang mua hàng";
                                }
                            }
                            else if (hasRejectedFinal)
                            {
                                // Có vật tư bị từ chối (nhưng không phải tất cả) và không còn vật tư đang mua hàng
                                // Kiểm tra xem các vật tư còn lại đã xuất kho chưa
                                var allCompleted = vatTuListFinal.All(v =>
                                    v.TrangThai == "Đã xuất kho" ||
                                    (!string.IsNullOrEmpty(v.TrangThai) && v.TrangThai.Contains("Đã từ chối")));
                                
                                if (allCompleted)
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
                        // Nếu tất cả vật tư đều bị từ chối và không còn vật tư nào đang chờ duyệt
                        else if (rejectedVatTu.Any() && !pendingVatTu.Any() && !approvedVatTu.Any())
                        {
                            yeucau.TrangThai = "Giám đốc - Đã từ chối";
                            _context.yeucau.Update(yeucau);
                            _context.SaveChanges();
                        }
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

                // Helper function để kiểm tra xem vật tư có đang chờ giám đốc duyệt không
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
                                    vatTu.TrangThai = "Đã xuất kho";
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
                            
                            // Kiểm tra xem tất cả vật tư đã được duyệt chưa (trạng thái "Đã duyệt" hoặc "Đang mua hàng")
                            var allApproved = vatTuListAfter.All(v => 
                                v.TrangThai == "Đã duyệt" || 
                                v.TrangThai == "Đang mua hàng" ||
                                v.TrangThai == "Đã xuất kho" ||
                                v.TrangThai == "Đã nhận hàng");
                            
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
                                           List<string> HangSX, List<string> NhaCC, List<int> SL,
                                           List<string> DonVi, string MaYeucau, string action, phieuxuatkho phieuxuatkho, vtphieuxuatkho vtphieuxuatkho, phieumuahang phieumuahang, vtphieumuahang vtphieumuahang)
        {
            if (yeucau.TenYeucau != "Yêu cầu nhập kho")
            {
                var prefix = yeucau.YCMaNguoidung;
                int nextNumber = 1;

                while (true)
                {
                    yeucau.MaYeucau = $"{prefix}{nextNumber}";

                    var existingEntry = _context.yeucau
                                                .FirstOrDefault(y => y.MaYeucau == yeucau.MaYeucau);
                    if (existingEntry == null)
                    {
                        break;
                    }
                    nextNumber++;
                }
                yeucau.NgayYeucau = DateTime.Now;

                var chucVu2 = HttpContext.Session.GetString("Chucvu");
                var boPhan2 = HttpContext.Session.GetString("Bophan");
                var maNv2 = HttpContext.Session.GetString("MaNguoidung");

                var duan = _context.duans.FirstOrDefault(d => d.MaDuan == yeucau.YCMaDuan);

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
                        newVtyeucau.SL = SL[i];
                        newVtyeucau.DonVi = DonVi[i];
                        newVtyeucau.YCMakho = khoMatch.Makho;
                        newVtyeucau.NgayNhapkho = khoMatch.NgayNhapkho;
                        newVtyeucau.NgayBaohanh = khoMatch.NgayBaohanh;
                        newVtyeucau.ThoiGianBH = khoMatch.ThoiGianBH;
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
                        newVtyeucau.SL = SL[i];
                        newVtyeucau.DonVi = DonVi[i];
                        newVtyeucau.YCMakho = "VT mới";
                        newVtyeucau.NgayNhapkho = null;
                        newVtyeucau.NgayBaohanh = null;
                        newVtyeucau.ThoiGianBH = null;
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
                // Nếu trạng thái là "Chờ Giám đốc duyệt" hoặc "Giám đốc" và người duyệt là Giám đốc, thì duyệt luôn
                // Không đặt trạng thái thành "Đã duyệt", để Xuliphieuyeucau xử lý (sẽ tự động chuyển sang "Đang mua hàng" hoặc "Đã xuất kho")
                if ((Yeucau.TrangThai == "Chờ Giám đốc duyệt" || Yeucau.TrangThai == "Giám đốc") && chucVu2 == "Giám đốc")
                {
                    // Không đặt trạng thái thành "Đã duyệt", để logic cũ xử lý
                    Xuliphieuyeucau(Yeucau.MaYeucau, phieuxuatkho, vtphieuxuatkho, phieumuahang, vtphieumuahang, yeucau, vtyeucau);
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
                                // Không đặt trạng thái thành "Đã duyệt", để logic cũ xử lý
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
                                // Không đặt trạng thái thành "Đã duyệt", để logic cũ xử lý
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

                    if (soLuongKhaDung > 0 && soLuongKhaDung < VattuYC.SL)
                    {
                        // Trường hợp số lượng khả dụng nhỏ hơn số lượng yêu cầu
                        Console.WriteLine($"Đã chạy: Số lượng khả dụng nhỏ hơn số lượng yêu cầu (Makho: {khotong.Makho}, SL tồn: {khotong.SL}, SL đã cam kết: {soLuongDaCamKet}, SL khả dụng: {soLuongKhaDung}, SL yêu cầu: {VattuYC.SL})");
                        isPhieuXuatKhoCreated = true;
                        isPhieuMuaHangCreated = true;
                    }
                    else if (soLuongKhaDung == 0)
                    {
                        // Trường hợp số lượng khả dụng bằng 0
                        Console.WriteLine($"Đã chạy: Số lượng khả dụng bằng 0 (Makho: {khotong.Makho}, SL tồn: {khotong.SL}, SL đã cam kết: {soLuongDaCamKet}, SL yêu cầu: {VattuYC.SL})");
                        isPhieuMuaHangCreated = true;
                    }
                    else if (soLuongKhaDung >= VattuYC.SL)
                    {
                        // Trường hợp số lượng khả dụng đủ đáp ứng
                        Console.WriteLine($"Đã chạy: Số lượng khả dụng đủ đáp ứng (Makho: {khotong.Makho}, SL tồn: {khotong.SL}, SL đã cam kết: {soLuongDaCamKet}, SL khả dụng: {soLuongKhaDung}, SL yêu cầu: {VattuYC.SL})");
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
                    NgayXuatkho = DateTime.Now,
                    TrangThai = "Đang chuẩn bị hàng"
                };
                _context.Add(Phieuxuatkho);
                Console.WriteLine($"Đã tạo phiếu xuất kho: MaXuatkho = {Maxuatkho}");

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
                Console.WriteLine($"Đã tạo phiếu mua hàng: MaMuahang = {Mamuahang}");
            }
            else if (isPhieuMuaHangCreated == true && isPhieuXuatKhoCreated == false)
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
                    NgayXuatkho = DateTime.Now,
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
                    int soLuongYeuCau = VattuYC.SL ?? 0;
                    int soLuongXuat = Math.Max(0, Math.Min(soLuongKhaDung, soLuongYeuCau));
                    int soLuongThieu = soLuongYeuCau - soLuongXuat;

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
                            SL = soLuongThieu,
                            NgayBaohanh = VattuYC.NgayBaohanh,
                            ThoiGianBH = VattuYC.ThoiGianBH,
                            TrangThai = "Đang chờ báo giá"
                        };

                        _context.Add(VTPhieumuahang);
                    }
                    else
                    {
                        VattuYC.TrangThai = "Đã xuất kho";
                    }

                    _context.vtyeucau.Update(VattuYC);
                    // KHÔNG cập nhật khotong ở đây - chỉ cập nhật khi người nhận xác nhận đã nhận hàng
                }
                else
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


            return RedirectToAction("Yeucau", "Yeucau", new { area = "Giamdoc" });
        }

        // Method để xử lý phiếu yêu cầu cho các vật tư đã được duyệt (partial approval)
        private void XuliphieuyeucauPartial(string Mayeucau, List<string> approvedMaSanphamList)
        {
            // Lấy các vật tư đã được duyệt (bao gồm cả "Đã xuất kho", "Đã duyệt" và "Đang mua hàng" - nhưng chỉ xử lý những vật tư chưa được xử lý)
            var danhSachVatTuYC = _context.vtyeucau
                                          .Where(vt => vt.VTMaYeucau == Mayeucau && 
                                                       approvedMaSanphamList.Contains(vt.MaSanpham) &&
                                                       (vt.TrangThai == "Đã xuất kho" || vt.TrangThai == "Đã duyệt" || vt.TrangThai == "Đang mua hàng"))
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

            string Maxuatkho = null;
            string Mamuahang = null;

            // Nếu chưa có phiếu xuất kho, tạo mã mới
            if (existingPhieuXuatKho == null)
            {
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

            bool isPhieuXuatKhoCreated = false;
            bool isPhieuMuaHangCreated = false;

            // Kiểm tra xem có cần tạo phiếu xuất kho hoặc phiếu mua hàng không
            foreach (var VattuYC in danhSachVatTuYC)
            {
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
                    // FIFO: chỉ tính vật tư duyệt trước thời điểm duyệt hiện tại
                    int soLuongDaCamKet = TinhSoLuongDaCamKet(khotong.Makho ?? "", khotong.MaSanpham ?? "", VattuYC.NgayDuyet, Maxuatkho);
                    int soLuongKhaDung = (khotong.SL ?? 0) - soLuongDaCamKet;

                    if (soLuongKhaDung > 0 && soLuongKhaDung < VattuYC.SL)
                    {
                        isPhieuXuatKhoCreated = true;
                        isPhieuMuaHangCreated = true;
                    }
                    else if (soLuongKhaDung == 0)
                    {
                        isPhieuMuaHangCreated = true;
                    }
                    else if (soLuongKhaDung >= VattuYC.SL)
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
                    isPhieuMuaHangCreated = true;
                }

                // Nếu vật tư đã được đánh dấu "Đã xuất kho" ở bước duyệt thì chắc chắn cần phiếu xuất kho
                if (VattuYC.TrangThai == "Đã xuất kho")
                {
                    isPhieuXuatKhoCreated = true;
                }
            }

            // Tạo phiếu xuất kho nếu cần
            if (isPhieuXuatKhoCreated && existingPhieuXuatKho == null)
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
                Console.WriteLine($"Đã tạo phiếu xuất kho: MaXuatkho = {Maxuatkho}");
            }

            // Tạo phiếu mua hàng nếu cần
            if (isPhieuMuaHangCreated && existingPhieuMuaHang == null)
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

            // Xử lý từng vật tư đã duyệt (theo thứ tự FIFO)
            foreach (var VattuYC in danhSachVatTuYCSapXep)
            {
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
                    int soLuongYeuCau = VattuYC.SL ?? 0;

                    // Nếu vật tư đã được duyệt với trạng thái "Đã xuất kho" thì ưu tiên xuất đủ theo SL yêu cầu
                    int soLuongXuat;
                    if (VattuYC.TrangThai == "Đã xuất kho")
                    {
                        soLuongXuat = soLuongYeuCau;
                    }
                    else
                    {
                        soLuongXuat = Math.Max(0, Math.Min(soLuongKhaDung, soLuongYeuCau));
                    }
                    int soLuongThieu = soLuongYeuCau - soLuongXuat;

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
                        // CHỈ cập nhật trạng thái nếu vật tư chưa có trạng thái "Đã xuất kho" hoặc "Đang mua hàng"
                        // Nếu đã có trạng thái "Đã xuất kho", giữ nguyên (vì đã xuất rồi, phần thiếu sẽ mua bổ sung)
                        // Nếu đã có trạng thái "Đang mua hàng", giữ nguyên (vì đã được xử lý trước đó)
                        if (VattuYC.TrangThai != "Đã xuất kho" && VattuYC.TrangThai != "Đang mua hàng")
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
                    else if (soLuongXuat > 0 && soLuongThieu == 0)
                    {
                        // Vật tư đã được xuất kho đủ, CHỈ cập nhật trạng thái nếu chưa có trạng thái "Đã xuất kho" hoặc "Đang mua hàng"
                        // Giữ nguyên trạng thái hiện tại nếu đã có (vì đã được xử lý trước đó)
                        if (VattuYC.TrangThai != "Đã xuất kho" && VattuYC.TrangThai != "Đang mua hàng")
                        {
                            VattuYC.TrangThai = "Đã xuất kho";
                        }
                    }

                    _context.vtyeucau.Update(VattuYC);
                }
                else
                {
                    // Không có trong kho tổng, cần mua hàng
                    if (isPhieuMuaHangCreated)
                    {
                        // CHỈ cập nhật trạng thái nếu vật tư chưa có trạng thái "Đã xuất kho" hoặc "Đang mua hàng"
                        // Nếu đã có trạng thái "Đã xuất kho" hoặc "Đang mua hàng", giữ nguyên (vì đã được xử lý trước đó)
                        if (VattuYC.TrangThai != "Đã xuất kho" && VattuYC.TrangThai != "Đang mua hàng")
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

            _context.SaveChanges();
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

            // Workflow mới theo yêu cầu:
            // 1. Gửi phiếu cho Bộ phận kho chờ xác nhận
            // 2. Bộ phận kho kiểm tra số lượng tồn kho
            // 3. Nếu đủ hàng → chuẩn bị hàng, xác nhận sẵn sàng xuất kho
            // 4. Nếu thiếu hàng → báo Mua hàng hoặc Người yêu cầu để xử lý
            // 5. Người yêu cầu nhận thông báo đã chuẩn bị xong vật tư
            // 6. Người yêu cầu đến kho kiểm tra, xác nhận đã nhận vật tư
            // 7. Bộ phận kho xác nhận giao xong và lưu phiếu giao vật tư
            // 8. Phần mềm cập nhật tồn kho, khóa phiếu (không được chỉnh sửa)
            // 9. Gửi bản sao phiếu cho Kế toán, Quản lý dự án, và Người yêu cầu

            if (Phieuxuatkho.TrangThai == "Chờ xác nhận" || Phieuxuatkho.TrangThai == "Thiếu hàng - Đã tạo phiếu mua")
            {
                // Bước 2: Bộ phận kho kiểm tra số lượng tồn kho và chuẩn bị hàng
                // Xử lý cả trường hợp "Thiếu hàng - Đã tạo phiếu mua" để kiểm tra lại sau khi đã nhập hàng
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
                    
                    // 3. Tính số lượng đã được xuất (các vật tư có trạng thái "Đã xác nhận nhận hàng" hoặc "Đã xuất kho")
                    int soLuongDaXuat = nhom
                        .Where(vt => vt.TrangThai == "Đã xác nhận nhận hàng" || vt.TrangThai == "Đã xuất kho")
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
                // Người yêu cầu xác nhận đã nhận vật tư (Bước 3)
                Phieuxuatkho.TrangThai = "Đã xác nhận nhận hàng";
                Phieuxuatkho.NgayXacNhanNhan = DateTime.Now;
                _context.phieuxuatkho.Update(Phieuxuatkho);
                _context.SaveChanges();
            }
            else if (Phieuxuatkho.TrangThai == "Đã xác nhận nhận hàng")
            {
                var allVtConfirmed = VTphieuxuatkho.All(vt =>
                    vt.TrangThai == "Đã xác nhận nhận hàng" ||
                    vt.TrangThai == "Đã xuất kho");

                if (!allVtConfirmed)
                {
                    TempData["Error"] = "Không thể hoàn tất phiếu vì vẫn còn vật tư chưa được người yêu cầu xác nhận.";
                    return RedirectToAction("Phieuxuatkho", "Yeucau", new { area = "Giamdoc" });
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
        // LƯU Ý: "Đã xác nhận nhận hàng" KHÔNG tính vì đã trừ kho rồi
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
                int STT = 0;
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
            }
            else if (action == "reject")
            {
                Xulituchoiyeucau(MaMuahang,null,null, phieumuahang, vtphieumuahang);
            }
            _context.SaveChanges();
            return RedirectToAction("Phieumuahang", "Yeucau", new { area = "Giamdoc" });
        }

        [HttpPost]
        public IActionResult Taophieunhapkhobyphieumuahang(string MaMuahang, phieunhapkho phieunhapkho, vtphieunhapkho vtphieunhapkho, phieumuahang phieumuahang, vtphieumuahang vtphieumuahang)
        {
            var Phieumuahang = _context.phieumuahang.FirstOrDefault(p => p.MaMuahang == MaMuahang);
            var VTPhieumuahanglist = _context.vtphieumuahang.Where(vt => vt.MaMuahang == MaMuahang).ToList();

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
            int[] SL, string[] DonVi, string[] Makho, string LoaiNhapkho)
        {
            var maNv = HttpContext.Session.GetString("MaNguoidung");
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

                int STT = 0;
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
                phieunhapkho.NgayNhapkho = DateTime.Now;

                if (!string.IsNullOrEmpty(phieunhapkho.MaDuan))
                {
                    phieunhapkho.TrangThai = "Quản lí dự án";
                }
                else
                {
                    phieunhapkho.TrangThai = "Giám đốc";
                }

                if (string.IsNullOrEmpty(phieunhapkho.MaYeucau))
                {
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

                    var existingYeucauDacBiet = _context.yeucau
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
                            TrangThai = "Đã duyệt"
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

                    var newvtphieunhapkho = new vtphieunhapkho
                    {
                        TenSanpham = TenSanpham[i],
                        MaSanpham = MaSanpham?[i] ?? "",
                        Makho = Makho?[i] ?? "",
                        HangSX = HangSX?[i] ?? "",
                        NhaCC = NhaCC?[i] ?? "",
                        SL = SL?[i] ?? 0,
                        DonVi = DonVi?[i] ?? "",
                        TrangThai = phieunhapkho.TrangThai,
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

        [HttpPost]
        public IActionResult Xuliphieunhapkho(
                                string MaNhapkho, string action,
                                phieuxuatkho phieunhapkho,
                                vtphieuxuatkho vtphieunhapkho, phieuxuatkho phieuxuatkho, vtphieuxuatkho vtphieuxuatkho)
        {
            var chucVu2 = HttpContext.Session.GetString("Chucvu");
            var boPhan2 = HttpContext.Session.GetString("Bophan");
            var maNv2 = HttpContext.Session.GetString("MaNguoidung");

            var Phieunhapkho = _context.phieunhapkho.FirstOrDefault(p => p.MaNhapkho == MaNhapkho);
            if (Phieunhapkho == null)
            {
                return NotFound();
            }

            var VTPhieunhapkholist = _context.vtphieunhapkho.Where(vt => vt.MaNhapkho == MaNhapkho).ToList();

            if (action == "approve")
            {
                // Workflow duyệt:
                // 1. "Quản lí dự án" (nếu có dự án) -> Trưởng dự án duyệt -> "Giám đốc"
                // 2. "Giám đốc" -> Giám đốc duyệt -> "Chờ nhập kho"
                // 3. "Chờ nhập kho" -> Kho xử lý -> "Đã nhập kho" và cộng vào kho tổng

                // Kiểm tra nếu giám đốc là quản lý dự án
                var duan = Phieunhapkho.MaDuan != null ? _context.duans.FirstOrDefault(d => d.MaDuan == Phieunhapkho.MaDuan) : null;
                var maQLDA = duan?.MaNguoiQLDA;
                var isQLDA = !string.IsNullOrEmpty(maQLDA) && maQLDA == maNv2;

                if (Phieunhapkho.TrangThai == "Quản lí dự án" && isQLDA && chucVu2 == "Giám đốc")
                {
                    // Giám đốc là QLDA duyệt từ "Quản lí dự án" -> "Giám đốc"
                    Phieunhapkho.TrangThai = "Giám đốc";
                    foreach (var vt in VTPhieunhapkholist)
                    {
                        vt.TrangThai = "Giám đốc";
                        _context.vtphieunhapkho.Update(vt);
                    }
                }
                else if (Phieunhapkho.TrangThai == "Giám đốc" && chucVu2 == "Giám đốc")
                {
                    // Giám đốc duyệt
                    Phieunhapkho.TrangThai = "Chờ nhập kho";
                    foreach (var vt in VTPhieunhapkholist)
                    {
                        vt.TrangThai = "Chờ nhập kho";
                        _context.vtphieunhapkho.Update(vt);
                    }
                }
                else if (Phieunhapkho.TrangThai == "Chờ nhập kho" && boPhan2 == "BP kho")
                {
                    // Kho xử lý nhập kho
                    Phieunhapkho.TrangThai = "Đã nhập kho";
                    
                    // Cập nhật tồn kho khi nhập hàng
                    foreach (var VTPhieunhapkho in VTPhieunhapkholist)
                    {
                        // Tìm vật tư trong tồn kho
                        var khotong = _context.khotongs.FirstOrDefault(k => 
                            k.TenSanpham == VTPhieunhapkho.TenSanpham && 
                            k.MaSanpham == VTPhieunhapkho.MaSanpham && 
                            k.HangSX == VTPhieunhapkho.HangSX &&
                            k.Makho == VTPhieunhapkho.Makho);
                            
                        if (khotong != null)
                        {
                            // Cộng số lượng vào tồn kho
                            khotong.SL += VTPhieunhapkho.SL;
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
                                SL = VTPhieunhapkho.SL,
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
                        
                        // BỔ SUNG: Kiểm tra và tự động tạo phiếu xuất kho cho các vật tư đã được duyệt (trạng thái "Đã xuất kho")
                        // nhưng chưa có trong phiếu xuất kho
                        var vatTuDaDuyetChuaXuat = _context.vtyeucau
                            .Where(vt => vt.VTMaYeucau == Phieunhapkho.MaYeucau
                                && vt.TrangThai == "Đã xuất kho"
                                && !_context.vtphieuxuatkho.Any(vtx => vtx.MaYeucau == vt.VTMaYeucau && vtx.MaSanpham == vt.MaSanpham))
                            .ToList();
                        
                        if (vatTuDaDuyetChuaXuat.Any())
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
                                    NgayXuatkho = DateTime.Now,
                                    TrangThai = "Đang chuẩn bị hàng"
                                };
                                _context.phieuxuatkho.Add(phieuXuatLienQuan);
                                _context.SaveChanges();

                                phieuXuatLienQuanList.Add(phieuXuatLienQuan);
                            }
                            
                            // Thêm các vật tư đã duyệt vào phiếu xuất kho
                            foreach (var vatTu in vatTuDaDuyetChuaXuat)
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

                _context.phieunhapkho.Update(Phieunhapkho);
            }
            else if (action == "reject")
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
            return RedirectToAction("Phieunhapkho", "Yeucau", new { area = "Giamdoc" });
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
                        // Tìm record phù hợp nhất: ưu tiên khớp cả Makho, MaSanpham, HangSX, NhaCC
                        var khotong = _context.khotongs
                            .FirstOrDefault(k => 
                                k.Makho == vt.Makho && 
                                k.MaSanpham == vt.MaSanpham &&
                                k.HangSX == vt.HangSX &&
                                (k.NhaCC == vt.NhaCC || 
                                 (string.IsNullOrWhiteSpace(k.NhaCC) && string.IsNullOrWhiteSpace(vt.NhaCC))))
                            // Nếu không tìm thấy, tìm theo Makho và MaSanpham
                            ?? _context.khotongs.FirstOrDefault(k => k.Makho == vt.Makho && k.MaSanpham == vt.MaSanpham);
                        
                        if (khotong != null)
                        {
                            // Lấy NgayDuyet từ vật tư yêu cầu tương ứng để áp dụng FIFO
                            var vtYeuCau = _context.vtyeucau.FirstOrDefault(v => v.VTMaYeucau == vt.MaYeucau && v.MaSanpham == vt.MaSanpham);
                            
                            // FIFO: Tính số lượng hàng đã cam kết từ các phiếu xuất khác (chỉ tính vật tư duyệt trước)
                            int soLuongDaCamKetKhac = TinhSoLuongDaCamKet(vt.Makho ?? "", vt.MaSanpham ?? "", vtYeuCau?.NgayDuyet, MaXuatkho);
                            
                            // Tính tổng số lượng từ tất cả records với cùng Makho và MaSanpham (để tránh lỗi do duplicate)
                            int tongSoLuongTonKho = _context.khotongs
                                .Where(k => k.Makho == vt.Makho && k.MaSanpham == vt.MaSanpham)
                                .Sum(k => k.SL ?? 0);
                            
                            // Số lượng khả dụng = Tổng tồn kho - Số lượng đã cam kết từ các phiếu khác
                            int soLuongKhaDung = tongSoLuongTonKho - soLuongDaCamKetKhac;
                            
                            // TUYỆT ĐỐI KHÔNG cho phép xuất nếu hết hàng hoặc không đủ số lượng
                            if (soLuongKhaDung <= 0 || soLuongKhaDung < vt.SL)
                            {
                                TempData["ErrorMessage"] = $"Không thể xuất kho: Vật tư {vt.TenSanpham} không đủ số lượng trong kho (Tồn kho: {tongSoLuongTonKho}, Đã cam kết: {soLuongDaCamKetKhac}, Khả dụng: {soLuongKhaDung}, Yêu cầu: {vt.SL})";
                                return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "Giamdoc" });
                            }
                            
                            // Trừ từ record phù hợp nhất
                            if (khotong.SL >= vt.SL)
                            {
                                khotong.SL -= vt.SL;
                                _context.khotongs.Update(khotong);
                            }
                            else
                            {
                                // Nếu record hiện tại không đủ, trừ từ nhiều records
                                int soLuongConLai = vt.SL ?? 0;
                                var khotongList = _context.khotongs
                                    .Where(k => k.Makho == vt.Makho && k.MaSanpham == vt.MaSanpham && (k.SL ?? 0) > 0)
                                    .OrderByDescending(k => k.SL)
                                    .ToList();
                                
                                foreach (var k in khotongList)
                                {
                                    if (soLuongConLai <= 0) break;
                                    int soLuongTru = Math.Min(soLuongConLai, k.SL ?? 0);
                                    k.SL -= soLuongTru;
                                    soLuongConLai -= soLuongTru;
                                    _context.khotongs.Update(k);
                                }
                            }
                        }
                        else
                        {
                            TempData["ErrorMessage"] = $"Không thể xuất kho: Vật tư {vt.TenSanpham} không tồn tại trong kho";
                            return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "Giamdoc" });
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
                return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "Giamdoc" });
            }

            TempData["ErrorMessage"] = "Phiếu không hợp lệ hoặc đã được xác nhận!";
            return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "Giamdoc" });
        }

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

    }
}


