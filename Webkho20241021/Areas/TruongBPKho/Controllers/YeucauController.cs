using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Webkho_20241021.Models;
using Webkho_20241021.Areas.TruongBPKho.Data;
using System;


namespace Webkho_20241021.Areas.TruongBPKho.Controllers
{
    [Area("TruongBPKho")]
    [Authorize(Roles = "Trưởng BP-BP kho")]
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

            var SortedYeucaulist = Yeucaulist
                .OrderByDescending(y => y.TrangThai == userRole)
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
            var Phieumuahanglist = _context.phieumuahang
            .OrderByDescending(y => y.NgayMuahang)
            .ToList();
            var chucVu = HttpContext.Session.GetString("Chucvu");
            var boPhan = HttpContext.Session.GetString("Bophan");
            var Phieumuahanglisttt = _context.phieumuahang.ToList();
            var VTphieumuahanglist = _context.vtphieumuahang.ToList();

            int Dangchobaogiacount = _context.phieumuahang.Count(p => p.TrangThai == "Đang chờ báo giá");
            int Dabaogiacount = _context.phieumuahang.Count(p => p.TrangThai == "Đã báo giá");
            int Chothanhtoancount = _context.phieumuahang.Count(p => p.TrangThai == "Chờ thanh toán");
            int Dathanhtoancount = _context.phieumuahang.Count(p => p.TrangThai == "Đã thanh toán");
            int thongbaocount = 0;
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
            };
            return View(model);
        }

        [HttpGet]
        public IActionResult GetDulieuThongbao()
        {
            var chucVu = HttpContext.Session.GetString("Chucvu");
            var boPhan = HttpContext.Session.GetString("Bophan");
            var maNv = HttpContext.Session.GetString("MaNguoidung");

            // Mua hàng
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

            // Nhập kho
            int thongbaonhapkhocount = 0;
            if (boPhan == "BP kho")
            {
                thongbaonhapkhocount = _context.phieunhapkho.Count(p => p.TrangThai == "Chờ nhập kho" || p.TrangThai == "Sẵn sàng nhập kho");
            }

            // Yêu cầu (duyệt/QLDA)
            // Với BP kho: tab "Chưa duyệt" đếm phiếu xuất kho "Chờ xác nhận" cần kho xử lý + yêu cầu chờ duyệt kho
            int thongbaoyeucaucount = 0;
            if (boPhan == "BP kho")
            {
                // Đếm phiếu xuất kho đang chờ kho xác nhận và xử lý
                int phieuxuatkhocount = _context.phieuxuatkho.Count(p => p.TrangThai == "Chờ xác nhận");
                // Đếm yêu cầu chờ duyệt của kho (nếu có)
                int yeucauchoduyetcount = _context.yeucau.Count(p => p.TrangThai == (chucVu + "-" + boPhan));
                thongbaoyeucaucount = phieuxuatkhocount + yeucauchoduyetcount;
            }
            else
            {
                // Các bộ phận khác: đếm yêu cầu chờ duyệt
                var Maduanquanli = _context.duans
                    .Where(d => d.MaNguoiQLDA == maNv)
                    .Select(d => d.MaDuan)
                    .ToList();
                int QLDAyeucaucount = _context.yeucau.Count(p => p.TrangThai == "Quản lí dự án" && Maduanquanli.Contains(p.YCMaDuan));
                int Duyetyeucaucount = _context.yeucau.Count(p => p.TrangThai == (chucVu + "-" + boPhan));
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
            // Reuse the same logic as GetDulieuThongbao for the layout badge
            return GetDulieuThongbao();
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
            return Json(PhieuxuatkhoList);
        }

        [HttpGet]
        public IActionResult GetVTPhieunhapkho(string MaNhapkho)
        {
            var PhieunhapkhoList = _context.vtphieunhapkho
                                 .Where(v => v.MaNhapkho == MaNhapkho).ToList();
            return Json(PhieunhapkhoList);
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
            var Yeucaulist = _context.yeucau
                          .Select(n => new { n.MaYeucau, n.TrangThai })
                          .ToList();

            ViewBag.Yeucaulist = Yeucaulist;

            var Phieumuahanglist = _context.phieumuahang
                                 .Select(n => new { n.MaYeucau, n.TrangThai })
                                 .ToList();
            ViewBag.Phieumuahanglist = Phieumuahanglist;

            var Duanlist = _context.duans
                          .Select(y => new { y.MaDuan, y.TrangThai })
                          .ToList();

            ViewBag.Duanlist = Duanlist;
            return View();
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

            return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPKho" });
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
                return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPKho" });
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
            foreach (var VattuYC in danhSachVatTuYC)
            {
                if (DanhsachVTYCkhotong.Any(kt =>
                kt.SL > 0 && kt.Makho == VattuYC.YCMakho && kt.SL < VattuYC.SL))
                {
                    isPhieuMuaHangCreated = true;
                    isPhieuXuatKhoCreated = true;
                }
                else if (DanhsachVTYCkhotong.Any(kt =>
                kt.SL == 0 && kt.Makho == VattuYC.YCMakho && kt.SL < VattuYC.SL))
                {
                    isPhieuMuaHangCreated = true;
                }
                else
                {
                    isPhieuXuatKhoCreated = true;
                }
            }
            if ((isPhieuMuaHangCreated == true) && (isPhieuXuatKhoCreated = true))
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
            else if (isPhieuMuaHangCreated == true)
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
            else if (isPhieuXuatKhoCreated = true)
            {
                var Phieuxuatkho = new phieuxuatkho
                {
                    MaXuatkho = Maxuatkho,
                    MaYeucau = thongTinYeuCau.MaYeucau,
                    MaDuan = thongTinYeuCau.YCMaDuan,
                    MaNguoidung = thongTinYeuCau.YCMaNguoidung,
                    NgayTao = DateTime.Now,
                    TrangThai = "Chờ xác nhận"
                };
                _context.Add(Phieuxuatkho);
            }


            _context.SaveChanges();

            foreach (var VattuYC in danhSachVatTuYC)
            {
                var khotong = _context.khotongs.FirstOrDefault(yc => yc.Makho == VattuYC.YCMakho);

                if (khotong != null && khotong.SL > 0)
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
                        TrangThai = "Đang chuẩn bị hàng"
                    };

                    if (khotong.SL >= VattuYC.SL)
                    {
                        VTPhieuxuatkho.SL = VattuYC.SL;
                        // KHÔNG trừ kho ở đây - chỉ trừ khi người nhận xác nhận đã nhận hàng
                        VattuYC.TrangThai = "Đã duyệt";
                    }
                    else
                    {
                        VTPhieuxuatkho.SL = khotong.SL;
                        var SLThieu = VattuYC.SL - khotong.SL;
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
                            SL = SLThieu,
                            NgayBaohanh = VattuYC.NgayBaohanh,
                            ThoiGianBH = VattuYC.ThoiGianBH,
                            TrangThai = "Đang chờ báo giá"
                        };

                        _context.Add(VTPhieumuahang);
                        // KHÔNG trừ kho ở đây - chỉ trừ khi người nhận xác nhận đã nhận hàng
                    }

                    _context.vtyeucau.Update(VattuYC);
                    // KHÔNG cập nhật khotong ở đây - chỉ cập nhật khi người nhận xác nhận đã nhận hàng
                    _context.Add(VTPhieuxuatkho);
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


            return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPKho" });
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

            if (Phieuxuatkho.TrangThai == "Chờ xác nhận")
            {
                // Bước 2: Bộ phận kho kiểm tra số lượng tồn kho và chuẩn bị hàng
                bool duHang = true;
                var vatTuThieu = new List<vtphieuxuatkho>();
                
                foreach (var VTxuatkho in VTphieuxuatkho)
                {
                    var khotong = _context.khotongs.FirstOrDefault(k => k.Makho == VTxuatkho.Makho && k.MaSanpham == VTxuatkho.MaSanpham);
                    // Kiểm tra chặt chẽ: không có hàng, số lượng = 0, hoặc không đủ số lượng → không cho xuất
                    if (khotong == null || khotong.SL <= 0 || khotong.SL < VTxuatkho.SL)
                    {
                        duHang = false;
                        vatTuThieu.Add(VTxuatkho);
                    }
                }

                if (duHang)
                {
                    // Đủ hàng → chuẩn bị hàng, chuyển sang "Đang chuẩn bị hàng"
                    Phieuxuatkho.TrangThai = "Đang chuẩn bị hàng";
                    Phieuxuatkho.NgayChuanBi = DateTime.Now;
                    _context.phieuxuatkho.Update(Phieuxuatkho);
                    _context.SaveChanges();
                }
                else
                {
                    // Thiếu hàng → tự động tạo phiếu mua hàng theo dữ liệu đã có
                    Phieuxuatkho.TrangThai = "Thiếu hàng - Đã tạo phiếu mua";
                    Phieuxuatkho.GhiChu = "Không đủ số lượng tồn kho. Đã tự động tạo phiếu mua hàng.";
                    _context.phieuxuatkho.Update(Phieuxuatkho);
                    
                    // Tạo phiếu mua hàng tự động
                    TaoPhieuMuaHangTuDong(Phieuxuatkho, vatTuThieu);
                    
                    _context.SaveChanges();
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
                // Bước 4: Bộ phận kho xác nhận cuối cùng đã giao vật tư và lưu phiếu giao vật tư (không được phép sửa đổi)
                // Cập nhật tồn kho và thêm vào kho cá nhân
                if (Phieuxuatkho.MaDuan != null)
                {
                    // Vật tư cho dự án
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
                }
                else
                {
                    // Vật tư cho cá nhân - thêm vào kho cá nhân
                    foreach (var VTxuatkho in VTphieuxuatkho)
                    {
                        var VTphieuxuatkhott = _context.vtphieuxuatkho.FirstOrDefault(vt => vt.MaXuatkho == VTxuatkho.MaXuatkho);
                        VTphieuxuatkhott.TrangThai = "Đã xuất kho";
                        _context.vtphieuxuatkho.Update(VTphieuxuatkhott);
                        
                        var VTkhonguoidungtt = _context.khonguoidungs.FirstOrDefault(nd => nd.NDMakho == VTxuatkho.Makho && nd.NDMaNguoidung == Phieuxuatkho.MaNguoidung && nd.MaSanpham == VTxuatkho.MaSanpham);
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
                                NgayNhapkho = DateTime.Now, // Ngày nhập vào kho cá nhân
                                NgayBaohanh = VTxuatkho.NgayBaohanh,
                                ThoiGianBH = VTxuatkho.ThoiGianBH,
                                TrangThai = "Đang sử dụng"
                            };
                            _context.Add(VTkhonguoidung);
                        }
                    }
                }
                
                // Trừ kho tổng khi người nhận xác nhận nhận hàng
                foreach (var VTxuatkho in VTphieuxuatkho)
                {
                    var khotong = _context.khotongs.FirstOrDefault(k => k.Makho == VTxuatkho.Makho && k.MaSanpham == VTxuatkho.MaSanpham);
                    if (khotong != null)
                    {
                        khotong.SL -= VTxuatkho.SL;
                        _context.khotongs.Update(khotong);
                    }
                }
                
                // Khóa phiếu và hoàn thành
                Phieuxuatkho.TrangThai = "Hoàn thành";
                Phieuxuatkho.NgayHoanThanh = DateTime.Now;
                _context.phieuxuatkho.Update(Phieuxuatkho);
                _context.SaveChanges();
            }
            
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
                    if (phieu.TrangThai == "Đã xác nhận nhận hàng")
                    {
                        // Nếu phiếu đã xác nhận nhận hàng, vật tư phải là "Đã xác nhận nhận hàng"
                        if (vt.TrangThai != "Đã xác nhận nhận hàng" && vt.TrangThai != "Đã xuất kho")
                        {
                            newTrangThai = "Đã xác nhận nhận hàng";
                        }
                    }
                    else if (phieu.TrangThai == "Hoàn thành")
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
                        // Phiếu đang chuẩn bị, vật tư phải là "Đang chuẩn bị hàng"
                        if (vt.TrangThai != "Đang chuẩn bị hàng" && vt.TrangThai != "Đã xác nhận nhận hàng" && vt.TrangThai != "Đã xuất kho")
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

            return RedirectToAction("Phieumuahang", "Yeucau", new { area = "TruongBPKho" });
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
        public IActionResult ThemPhieunhapkhoSQL(phieunhapkho phieunhapkho, vtphieunhapkho vtphieunhapkho, string[] TenSanpham, string[] MaSanpham, string[] HangSX, string[] NhaCC, int[] SL, string[] DonVi, string[] Makho)
        {
            // Kiểm tra xem đã có phiếu nhập kho nào cho mã yêu cầu này chưa
            var existingPhieunhapkho = _context.phieunhapkho
                .FirstOrDefault(p => p.MaYeucau == phieunhapkho.MaYeucau);
            
            if (existingPhieunhapkho != null)
            {
                TempData["Error"] = $"Đã tồn tại phiếu nhập kho cho mã yêu cầu {phieunhapkho.MaYeucau}. Không thể tạo phiếu nhập kho thủ công!";
                return RedirectToAction("ThemPhieunhapkho", "Yeucau", new { area = "TruongBPKho" });
            }

            // Tính toán số lượng các phần tử tối thiểu giữa các mảng
            int count = TenSanpham.Length;

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
            phieunhapkho.TrangThai = "Chờ nhập kho";

            _context.phieunhapkho.Add(phieunhapkho);
            _context.SaveChanges();

            var newphieunhapkho = _context.phieunhapkho
                                          .FirstOrDefault(y => y.MaNhapkho == MaNhapkho);

            for (int i = 0; i < count; i++)
            {
                var newvtphieunhapkho = new vtphieunhapkho
                {
                    TenSanpham = TenSanpham[i],
                    MaSanpham = MaSanpham[i],
                    Makho = Makho[i],
                    HangSX = HangSX[i],
                    NhaCC = NhaCC[i],
                    SL = SL[i],
                    DonVi = DonVi[i],
                    TrangThai = "Chờ nhập kho",
                    MaNhapkho = MaNhapkho,
                    MaYeucau = newphieunhapkho.MaYeucau
                };

                _context.vtphieunhapkho.Add(newvtphieunhapkho);
            }

            _context.SaveChanges();

            return RedirectToAction("Phieunhapkho", "Yeucau", new { area = "TruongBPKho" });
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

            if (action == "approve")
            {
                var Phieunhapkho = _context.phieunhapkho.FirstOrDefault(p => p.MaNhapkho == MaNhapkho);
                var VTPhieunhapkholist = _context.vtphieunhapkho.Where(vt => vt.MaNhapkho == MaNhapkho).ToList();
                
                // Workflow: "Chờ nhập kho" -> Kho xử lý -> "Đã nhập kho" và cộng vào kho tổng
                // LƯU Ý: Logic này áp dụng cho CẢ phiếu từ mua hàng VÀ phiếu từ dự án/cá nhân (sản lượng thừa)
                // - Phiếu từ mua hàng: Trạng thái ban đầu là "Chờ nhập kho"
                // - Phiếu từ dự án/cá nhân: Trạng thái ban đầu là "Quản lí dự án" hoặc "Giám đốc"
                //   → Sau khi Trưởng dự án/Giám đốc duyệt → Trạng thái chuyển thành "Chờ nhập kho"
                //   → Kho duyệt → Trừ từ kho dự án/cá nhân và cộng vào kho tổng (sản lượng thừa được trả lại)
                if (boPhan2 == "BP kho" && Phieunhapkho.TrangThai == "Chờ nhập kho")
                {
                    Phieunhapkho.TrangThai = "Đã nhập kho";
                    
                    // Kiểm tra xem có phải là phiếu nhập kho dự án/cá nhân không (NHAPKHO_DUAN hoặc NHAPKHO_TUDO)
                    bool isNhapKhoDuanOrCaNhan = !string.IsNullOrEmpty(Phieunhapkho.MaYeucau) && 
                                                 (Phieunhapkho.MaYeucau.Contains("NHAPKHO_DUAN") || 
                                                  Phieunhapkho.MaYeucau.Contains("NHAPKHO_TUDO"));
                    
                    foreach (var VTPhieunhapkho in VTPhieunhapkholist)
                    {
                        // Nếu là nhập kho dự án/cá nhân, trừ số lượng từ vtphieuxuatkho
                        if (isNhapKhoDuanOrCaNhan)
                        {
                            // Trừ từ vtphieuxuatkho nếu có MaDuan (vật tư dự án)
                            if (!string.IsNullOrEmpty(Phieunhapkho.MaDuan) && !string.IsNullOrEmpty(VTPhieunhapkho.MaSanpham))
                            {
                                // Tìm vật tư trong vtphieuxuatkho đã xuất cho dự án này
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
                                    // Trừ số lượng từ vtphieuxuatkho
                                    int slCanTra = VTPhieunhapkho.SL ?? 0;
                                    
                                    foreach (var vtItem in vtXuatKhoItems)
                                    {
                                        if (slCanTra <= 0) break;
                                        
                                        int slHienTai = vtItem.SL ?? 0;
                                        int slTru = Math.Min(slHienTai, slCanTra);
                                        
                                        // Trừ số lượng
                                        vtItem.SL = slHienTai - slTru;
                                        
                                        // Cập nhật trạng thái nếu đã trả hết
                                        if ((vtItem.SL ?? 0) <= 0)
                                        {
                                            vtItem.TrangThai = "Đã trả kho";
                                        }
                                        
                                        _context.vtphieuxuatkho.Update(vtItem);
                                        slCanTra -= slTru;
                                    }
                                    
                                    // Log cảnh báo nếu số lượng không đủ
                                    if (slCanTra > 0)
                                    {
                                        Console.WriteLine($"Cảnh báo: Số lượng trả ({VTPhieunhapkho.SL}) lớn hơn số lượng trong vtphieuxuatkho cho vật tư {VTPhieunhapkho.MaSanpham}, còn thiếu {slCanTra}");
                                    }
                                }
                            }
                            // Trừ từ kho cá nhân nếu không có MaDuan
                            else if (string.IsNullOrEmpty(Phieunhapkho.MaDuan) && !string.IsNullOrEmpty(VTPhieunhapkho.MaSanpham) && !string.IsNullOrEmpty(Phieunhapkho.MaNguoidung))
                            {
                                var khoCaNhanItem = _context.khonguoidungs
                                    .FirstOrDefault(k => k.NDMaNguoidung == Phieunhapkho.MaNguoidung 
                                                      && k.MaSanpham == VTPhieunhapkho.MaSanpham 
                                                      && (k.TrangThai == "Đang mượn" || k.TrangThai == "Đang sử dụng")
                                                      && (k.SL ?? 0) > 0);
                                
                                if (khoCaNhanItem != null)
                                {
                                    // Trừ số lượng từ kho cá nhân
                                    int slCanTra = VTPhieunhapkho.SL ?? 0;
                                    int slHienTai = khoCaNhanItem.SL ?? 0;
                                    
                                    if (slHienTai >= slCanTra)
                                    {
                                        khoCaNhanItem.SL = slHienTai - slCanTra;
                                        if ((khoCaNhanItem.SL ?? 0) <= 0)
                                        {
                                            khoCaNhanItem.TrangThai = "Đã trả";
                                        }
                                        _context.khonguoidungs.Update(khoCaNhanItem);
                                    }
                                    else
                                    {
                                        // Nếu số lượng trong kho cá nhân ít hơn số lượng trả, chỉ trừ phần có
                                        khoCaNhanItem.SL = 0;
                                        khoCaNhanItem.TrangThai = "Đã trả";
                                        _context.khonguoidungs.Update(khoCaNhanItem);
                                        
                                        // Log cảnh báo nếu số lượng không đủ
                                        Console.WriteLine($"Cảnh báo: Số lượng trả ({slCanTra}) lớn hơn số lượng trong kho cá nhân ({slHienTai}) cho vật tư {VTPhieunhapkho.MaSanpham}");
                                    }
                                }
                            }
                        }
                        
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
                        
                        // Cập nhật trạng thái vật tư phiếu nhập kho
                        var VTPhieunhapkhott = _context.vtphieunhapkho.FirstOrDefault(vt => vt.MaNhapkho == VTPhieunhapkho.MaNhapkho && vt.ID == VTPhieunhapkho.ID);
                        if (VTPhieunhapkhott != null)
                        {
                            VTPhieunhapkhott.TrangThai = "Đã nhập kho";
                            _context.vtphieunhapkho.Update(VTPhieunhapkhott);
                        }
                    }
                    
                    // Tự động tạo phiếu xuất kho nếu có yêu cầu ban đầu và chưa có phiếu xuất kho
                    // Logic này áp dụng cho CẢ vật tư dự án VÀ vật tư cá nhân
                    if (!string.IsNullOrEmpty(Phieunhapkho.MaYeucau))
                    {
                        // Kiểm tra xem đã có phiếu xuất kho cho yêu cầu này chưa
                        var existingPhieuxuatkho = _context.phieuxuatkho
                            .FirstOrDefault(px => px.MaYeucau == Phieunhapkho.MaYeucau);
                        
                        if (existingPhieuxuatkho == null)
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
                                    // Kiểm tra chặt chẽ: không có hàng, số lượng = 0, hoặc không đủ số lượng → không cho xuất
                                    if (khotong == null || khotong.SL <= 0 || khotong.SL < VTxuatkho.SL)
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
                                    newPhieuxuatkho.TrangThai = "Thiếu hàng - Đã tạo phiếu mua";
                                    newPhieuxuatkho.GhiChu = "Không đủ số lượng tồn kho. Đã tự động tạo phiếu mua hàng.";
                                    _context.phieuxuatkho.Update(newPhieuxuatkho);
                                    
                                    // Tạo phiếu mua hàng tự động
                                    TaoPhieuMuaHangTuDong(newPhieuxuatkho, vatTuThieu);
                                    
                                    _context.SaveChanges();
                                    Console.WriteLine($"Đã tự động tạo phiếu xuất kho {MaXuatkho} cho yêu cầu {Phieunhapkho.MaYeucau} nhưng thiếu hàng");
                                }
                            }
                        }
                    }
                }
                else if (boPhan2 == "BP kho" && Phieunhapkho.TrangThai == "Đã nhập kho")
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
                        
                        // Cập nhật trạng thái vật tư phiếu nhập kho
                        var VTPhieunhapkhott = _context.vtphieunhapkho.FirstOrDefault(vt => vt.MaNhapkho == VTPhieunhapkho.MaNhapkho && vt.ID == VTPhieunhapkho.ID);
                        if (VTPhieunhapkhott != null)
                        {
                            VTPhieunhapkhott.TrangThai = "Đã xác nhận nhận hàng";
                            _context.vtphieunhapkho.Update(VTPhieunhapkhott);
                        }
                    }
                }
                else if (Phieunhapkho.TrangThai == "Đã xác nhận nhận hàng")
                {
                    // Hoàn thành phiếu nhập kho
                    Phieunhapkho.TrangThai = "Hoàn thành";
                    
                    foreach (var VTPhieunhapkho in VTPhieunhapkholist)
                    {
                        var VTPhieunhapkhott = _context.vtphieunhapkho.FirstOrDefault(vt => vt.MaNhapkho == VTPhieunhapkho.MaNhapkho && vt.ID == VTPhieunhapkho.ID);
                        if (VTPhieunhapkhott != null)
                        {
                            VTPhieunhapkhott.TrangThai = "Hoàn thành";
                            _context.vtphieunhapkho.Update(VTPhieunhapkhott);
                        }
                    }
                }
                
                _context.phieunhapkho.Update(Phieunhapkho);
            }
            else if (action == "reject")
            {
                var Phieunhapkho = _context.phieunhapkho.FirstOrDefault(p => p.MaNhapkho == MaNhapkho);
                Phieunhapkho.TrangThai = "Đã từ chối";
                _context.phieunhapkho.Update(Phieunhapkho);
            }
            
            _context.SaveChanges();
            return RedirectToAction("Phieunhapkho", "Yeucau", new { area = "TruongBPKho" });
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

            return RedirectToAction("Phieunhapkho", "Yeucau", new { area = "TruongBPKho" });
        }
        // Xac nhan xuat kho
        public IActionResult XacnhanXuatkho()
        {
            // Lấy danh sách các phiếu xuất kho mà bộ phận kho cần xử lý
            var PhieuxuatkhoList = _context.phieuxuatkho
                .Where(p => p.TrangThai == "Chờ xác nhận"
                         || p.TrangThai == "Đang chuẩn bị hàng"
                         || p.TrangThai == "Chờ người yêu cầu xác nhận")
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

            return RedirectToAction("Yeucau", "Yeucau", new { area = "TruongBPKho" });
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

            // Xuất kho - chỉ đếm các trạng thái còn cần xử lý (không đếm "Hoàn thành" và "Đã xác nhận nhận hàng")
            int thongbaoxuatkhocount = 0;
            if (boPhan == "BP kho")
            {
                thongbaoxuatkhocount = _context.phieuxuatkho.Count(p => p.TrangThai != "Hoàn thành" && p.TrangThai != "Đã xác nhận nhận hàng");
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
                int yeucauchoduyetcount = _context.yeucau.Count(p => p.TrangThai == (chucVu + "-" + boPhan));
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
                    // Cập nhật trạng thái vật tư thành "Đã xác nhận nhận hàng"
                    vt.TrangThai = "Đã xác nhận nhận hàng";
                    _context.vtphieuxuatkho.Update(vt);
                    
                    // Trừ kho tổng khi xác nhận nhận hàng - KIỂM TRA CHẶT CHẼ SỐ LƯỢNG
                    var khotong = _context.khotongs.FirstOrDefault(k => k.Makho == vt.Makho && k.MaSanpham == vt.MaSanpham);
                    if (khotong != null)
                    {
                        // TUYỆT ĐỐI KHÔNG cho phép xuất nếu hết hàng hoặc không đủ số lượng
                        if (khotong.SL <= 0 || khotong.SL < vt.SL)
                        {
                            TempData["ErrorMessage"] = $"Không thể xuất kho: Vật tư {vt.TenSanpham} không đủ số lượng trong kho (Tồn kho: {khotong.SL}, Yêu cầu: {vt.SL})";
                            return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "TruongBPKho" });
                        }
                        
                        khotong.SL -= vt.SL;
                        _context.khotongs.Update(khotong);
                    }
                    else
                    {
                        TempData["ErrorMessage"] = $"Không thể xuất kho: Vật tư {vt.TenSanpham} không tồn tại trong kho";
                        return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "TruongBPKho" });
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
                return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "TruongBPKho" });
            }

            TempData["ErrorMessage"] = "Phiếu không hợp lệ hoặc đã được xác nhận!";
            return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "TruongBPKho" });
        }

    }
}