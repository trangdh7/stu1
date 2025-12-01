using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using Webkho_20241021.Areas.NhanvienKetoan.Data;
using Webkho_20241021.Models;


namespace Webkho_20241021.Areas.NhanvienKetoan.Controllers
{
    [Area("NhanvienKetoan")]
    [Authorize(Roles = "Nhân viên-BP kế toán,Nhân viên kế toán")]
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

                if (phieus.Any(p => p.TrangThai != "�� nh?n h�ng"))
                {
                    yeucau.TrangThai = "�ang mua h�ng";
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
            .OrderByDescending(y => y.TrangThai == "Ch? l?y h�ng")
            .ThenByDescending(y => y.TrangThai == "�ang chu?n b? h�ng")
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
            var VTphieumuahanglist = _context.vtphieumuahang.ToList();
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

            int thongbaomuahangcount = 0;
            if (boPhan == "BP mua h�ng")
            {
                thongbaomuahangcount = _context.phieumuahang.Count(p => p.TrangThai == "�ang ch? b�o gi�");
            }
            else if (boPhan == "BP k? to�n")
            {
                thongbaomuahangcount = _context.phieumuahang.Count(p => p.TrangThai == "Ch? thanh to�n");
            }

            // Xu?t kho - ch? d?m c�c tr?ng th�i c�n c?n x? l� (kh�ng d?m "Ho�n th�nh" v� "�� x�c nh?n nh?n h�ng")
            int thongbaoxuatkhocount = 0;
            if (boPhan == "BP kho")
            {
                thongbaoxuatkhocount = _context.phieuxuatkho.Count(p => p.TrangThai != "Ho�n th�nh" && p.TrangThai != "�� x�c nh?n nh?n h�ng");
            }

            int thongbaonhapkhocount = 0;
            if (boPhan == "BP kho")
            {
                thongbaonhapkhocount = _context.phieunhapkho.Count(p => p.TrangThai == "Ch? nh?p kho" || p.TrangThai == "S?n s�ng nh?p kho");
            }

            var Maduanquanli = _context.duans
                .Where(d => d.MaNguoiQLDA == maNv)
                .Select(d => d.MaDuan)
                .ToList();
            int QLDAyeucaucount = _context.yeucau.Count(p => p.TrangThai == "Qu?n l� d? �n" && Maduanquanli.Contains(p.YCMaDuan));
            int Duyetyeucaucount = _context.yeucau.Count(p => p.TrangThai == (chucVu + "-" + boPhan));
            int thongbaoyeucaucount = Duyetyeucaucount + QLDAyeucaucount;

            // Th�ng b�o x�c nh?n nh?n h�ng - d?m phi?u xu?t kho ch? x�c nh?n
            int thongbaoxacnhannhanhangcount = 0;
            var yeuCauList = _context.yeucau
                .Where(y => y.YCMaNguoidung == maNv)
                .Select(y => y.MaYeucau)
                .ToList();
            thongbaoxacnhannhanhangcount = _context.phieuxuatkho
                .Count(p => yeuCauList.Contains(p.MaYeucau) && p.TrangThai == "Ch? ngu?i y�u c?u x�c nh?n");

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
                return BadRequest("Kh�ng t�m th?y m� nh�n vi�n");
            }

            // L?y d? li?u t? kho c� nh�n
            var khoCaNhanItems = _context.khonguoidungs
                .Where(k => k.NDMaNguoidung == maNv && (k.TrangThai == "�ang mu?n" || k.TrangThai == "�ang s? d?ng") && k.SL > 0)
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
            if (string.IsNullOrEmpty(maduan))
            {
                return Json(new
                {
                    maNguoidung = HttpContext.Session.GetString("MaNguoidung"),
                    maDuan = "",
                    vtPhieuMuaHang = new List<object>()
                });
            }

            // L?y m� nh�n vi�n t? session
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
                // L?y v?t tu t? vtphieuxuatkho (d� xu?t kho) k?t h?p v?i phieuxuatkho theo MaDuan
                // C�c v?t tu d� du?c xu?t kho cho d? �n n�y c� th? du?c tr? l?i
                var khoDuanItems = (from vt in _context.vtphieuxuatkho
                                   join px in _context.phieuxuatkho on vt.MaXuatkho equals px.MaXuatkho
                                   join yc in _context.yeucau on vt.MaYeucau equals yc.MaYeucau
                                   where px.MaDuan == maduan 
                                      && yc.YCMaNguoidung == maNv
                                      && (vt.TrangThai == "�� x�c nh?n nh?n h�ng" 
                                          || vt.TrangThai == "�� l?y h�ng"
                                          || vt.TrangThai == "�� xu?t kho")
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
                                   .Distinct() // Tr�nh tr�ng l?p n?u c�
                                   .ToList();

                // Debug info
                Console.WriteLine($"Querying vtphieuxuatkho for MaDuan = '{maduan}'");
                Console.WriteLine($"Found {khoDuanItems.Count} items");
                
                // Debug: Ki?m tra s? phi?u xu?t kho c� MaDuan n�y
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
                .Take(10) // Gi?i h?n 10 k?t qu? d? hi?u su?t t?t hon
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

                return yeucau.NgayCanHang;
            }

            if (yeucau.TenYeucau != "Y�u c?u nh?p kho")
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
                        if (chucVu2 == "Tru?ng BP")
                        {
                            yeucau.TrangThai = "Gi�m d?c";
                        }
                        else if (chucVu2 == "Gi�m d?c")
                        {
                            yeucau.TrangThai = "�� duy?t";

                        }
                        else if (chucVu2 == "Nh�n vi�n" && boPhan2 == "BP k? thu?t")
                        {
                            yeucau.TrangThai = "Tru?ng BP-BP k? thu?t";
                        }
                        else if (chucVu2 == "Nh�n vi�n" && boPhan2 == "BP kho")
                        {
                            yeucau.TrangThai = "Tru?ng BP-BP kho";
                        }
                        else if (chucVu2 == "Nh�n vi�n" && boPhan2 == "BP mua h�ng")
                        {
                            yeucau.TrangThai = "Tru?ng BP-BP mua h�ng";
                        }
                        else if (chucVu2 == "Nh�n vi�n" && boPhan2 == "BP k? to�n")
                        {
                            yeucau.TrangThai = "Tru?ng BP-BP k? to�n";
                        }
                    }
                    else
                    {
                        if (chucVu2 == "Nh�n vi�n" && boPhan2 == "BP k? thu?t")
                        {
                            yeucau.TrangThai = "Tru?ng BP-BP k? thu?t";
                        }
                        else if (chucVu2 == "Tru?ng BP" && boPhan2 == "BP k? thu?t")
                        {
                            yeucau.TrangThai = "Qu?n l� d? �n";
                        }
                        else if (chucVu2 == "Nh�n vi�n" && boPhan2 == "BP kho")
                        {
                            yeucau.TrangThai = "Tru?ng BP-BP kho";
                        }
                        else if (chucVu2 == "Tru?ng BP" && boPhan2 == "BP kho")
                        {
                            yeucau.TrangThai = "Qu?n l� d? �n";
                        }
                        else if (chucVu2 == "Nh�n vi�n" && boPhan2 == "BP mua h�ng")
                        {
                            yeucau.TrangThai = "Tru?ng BP-BP mua h�ng";
                        }
                        else if (chucVu2 == "Tru?ng BP" && boPhan2 == "BP mua h�ng")
                        {
                            yeucau.TrangThai = "Qu?n l� d? �n";
                        }
                        else if (chucVu2 == "Gi�m d?c")
                        {
                            yeucau.TrangThai = "�� duy?t";
                        }
                    }
                }
                else
                {
                    if (chucVu2 == "Nh�n vi�n" && boPhan2 == "BP k? thu?t")
                    {
                        yeucau.TrangThai = "Tru?ng BP-BP k? thu?t";
                    }
                    else if (chucVu2 == "Tru?ng BP" && boPhan2 == "BP k? thu?t")
                    {
                        yeucau.TrangThai = "Gi�m d?c";
                    }
                    else if (chucVu2 == "Nh�n vi�n" && boPhan2 == "BP kho")
                    {
                        yeucau.TrangThai = "Tru?ng BP-BP kho";
                    }
                    else if (chucVu2 == "Tru?ng BP" && boPhan2 == "BP kho")
                    {
                        yeucau.TrangThai = "Gi�m d?c";
                    }
                    else if (chucVu2 == "Nh�n vi�n" && boPhan2 == "BP mua h�ng")
                    {
                        yeucau.TrangThai = "Tru?ng BP-BP mua h�ng";
                    }
                    else if (chucVu2 == "Tru?ng BP" && boPhan2 == "BP mua h�ng")
                    {
                        yeucau.TrangThai = "Gi�m d?c";
                    }
                    else if (chucVu2 == "Gi�m d?c")
                    {
                        yeucau.TrangThai = "�� duy?t";

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
                        newVtyeucau.NgayCanHang = GetNgayCanHangAt(i);
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
                        newVtyeucau.NgayCanHang = GetNgayCanHangAt(i);
                        newVtyeucau.YCMakho = "VT mới";
                        newVtyeucau.NgayNhapkho = null;
                        newVtyeucau.NgayBaohanh = null;
                        newVtyeucau.ThoiGianBH = null;
                        _context.vtyeucau.Add(newVtyeucau);
                    }
                    _context.SaveChanges();
                }
                if (yeucau.TrangThai == "�� duy?t")
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

            return RedirectToAction("Yeucau", "Yeucau", new { area = "NhanvienKetoan" });

        }
        [HttpPost]
        public IActionResult XuLyYeucau(string MaYeucau, string action, phieuxuatkho phieuxuatkho, vtphieuxuatkho vtphieuxuatkho, phieumuahang phieumuahang, vtphieumuahang vtphieumuahang, yeucau yeucau, vtyeucau vtyeucau)
        {
            var chucVu2 = HttpContext.Session.GetString("Chucvu");
            var boPhan2 = HttpContext.Session.GetString("Bophan");
            var maNv2 = HttpContext.Session.GetString("MaNguoidung");

            // L?y y�u c?u hi?n t?i t? co s? d? li?u
            var Yeucau = _context.yeucau.FirstOrDefault(y => y.MaYeucau == MaYeucau);
            if (Yeucau == null)
            {
                // X? l� n?u kh�ng t�m th?y y�u c?u
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
                        if (chucVu2 != "Gi�m d?c")
                        {
                            Yeucau.TrangThai = "Gi�m d?c";
                        }
                        else
                        {
                            Yeucau.TrangThai = "�� duy?t";
                            Xuliphieuyeucau(Yeucau.MaYeucau, phieuxuatkho, vtphieuxuatkho, phieumuahang, vtphieumuahang, yeucau, vtyeucau);
                        }
                    }
                    else
                    {
                        if (Yeucau.YCMaNguoidung != maNguoiQLDA)
                        {
                            if (chucVu2 == "Tru?ng BP" && boPhan2 == "BP k? thu?t")
                            {
                                Yeucau.TrangThai = "Qu?n l� d? �n";
                            }
                            else if (chucVu2 == "Tru?ng BP" && boPhan2 == "BP kho")
                            {
                                Yeucau.TrangThai = "Qu?n l� d? �n";
                            }
                            else if (chucVu2 == "Tru?ng BP" && boPhan2 == "BP mua h�ng")
                            {
                                Yeucau.TrangThai = "Qu?n l� d? �n";
                            }
                            else if (chucVu2 == "Tru?ng BP" && boPhan2 == "BP k? to�n")
                            {
                                Yeucau.TrangThai = "Qu?n l� d? �n";
                            }
                            else if (chucVu2 == "Gi�m d?c")
                            {
                                Yeucau.TrangThai = "�� duy?t";
                                Xuliphieuyeucau(Yeucau.MaYeucau, phieuxuatkho, vtphieuxuatkho, phieumuahang, vtphieumuahang, yeucau, vtyeucau);
                            }
                        }
                        else
                        {
                            if (chucVu2 != "Gi�m d?c")
                            {
                                Yeucau.TrangThai = "Gi�m d?c";
                            }
                            else
                            {
                                Yeucau.TrangThai = "�� duy?t";
                                Xuliphieuyeucau(Yeucau.MaYeucau, phieuxuatkho, vtphieuxuatkho, phieumuahang, vtphieumuahang, yeucau, vtyeucau);
                            }
                        }
                    }
                }
                else
                {
                    if (chucVu2 == "Nh�n vi�n" && boPhan2 == "BP k? thu?t")
                    {
                        Yeucau.TrangThai = "Tru?ng BP-BP k? thu?t";
                    }
                    else if (chucVu2 == "Tru?ng BP" && boPhan2 == "BP k? thu?t")
                    {
                        Yeucau.TrangThai = "Gi�m d?c";
                    }
                    else if (chucVu2 == "Nh�n vi�n" && boPhan2 == "BP kho")
                    {
                        Yeucau.TrangThai = "Tru?ng BP-BP kho";
                    }
                    else if (chucVu2 == "Tru?ng BP" && boPhan2 == "BP kho")
                    {
                        Yeucau.TrangThai = "Gi�m d?c";
                    }
                    else if (chucVu2 == "Nh�n vi�n" && boPhan2 == "BP mua h�ng")
                    {
                        Yeucau.TrangThai = "Tru?ng BP-BP mua h�ng";
                    }
                    else if (chucVu2 == "Tru?ng BP" && boPhan2 == "BP mua h�ng")
                    {
                        Yeucau.TrangThai = "Gi�m d?c";
                    }
                    else if (chucVu2 == "Gi�m d?c")
                    {
                        Yeucau.TrangThai = "�� duy?t";
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

            return RedirectToAction("Yeucau", "Yeucau", new { area = "NhanvienKetoan" });
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
                Console.WriteLine("Kh�ng t�m th?y y�u c?u ho?c danh s�ch v?t tu.");
                return RedirectToAction("Yeucau", "Yeucau", new { area = "NhanvienKetoan" });
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
                    TrangThai = "�ang chu?n b? h�ng"
                };
                _context.Add(Phieuxuatkho);

                var Phieumuahang = new phieumuahang
                {
                    MaMuahang = Mamuahang,
                    MaYeucau = thongTinYeuCau.MaYeucau,
                    MaDuan = thongTinYeuCau.YCMaDuan,
                    NgayMuahang = DateTime.Now,
                    MaNguoidung = thongTinYeuCau.YCMaNguoidung,
                    TrangThai = "�ang ch? b�o gi�"
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
                    TrangThai = "�ang ch? b�o gi�"
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
                    NgayXuatkho = DateTime.Now,
                    TrangThai = "�ang chu?n b? h�ng"
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
                        TrangThai = "�ang chu?n b? h�ng"
                    };

                    if (khotong.SL >= VattuYC.SL)
                    {
                        VTPhieuxuatkho.SL = VattuYC.SL;
                        // KH�NG tr? kho ? d�y - ch? tr? khi ngu?i nh?n x�c nh?n d� nh?n h�ng
                        VattuYC.TrangThai = "�� duy?t";
                    }
                    else
                    {
                        VTPhieuxuatkho.SL = khotong.SL;
                        var SLThieu = VattuYC.SL - khotong.SL;
                        VattuYC.TrangThai = "�ang mua h�ng";
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
                            TrangThai = "�ang ch? b�o gi�"
                        };

                        _context.Add(VTPhieumuahang);
                        // KH�NG tr? kho ? d�y - ch? tr? khi ngu?i nh?n x�c nh?n d� nh?n h�ng
                    }

                    _context.vtyeucau.Update(VattuYC);
                    // KH�NG c?p nh?t khotong ? d�y - ch? c?p nh?t khi ngu?i nh?n x�c nh?n d� nh?n h�ng
                    _context.Add(VTPhieuxuatkho);
                }
                else
                {
                    VattuYC.TrangThai = "�ang mua h�ng";
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
                        TrangThai = "�ang ch? b�o gi�"
                    };

                    _context.vtyeucau.Update(VattuYC);
                    _context.Add(VTPhieumuahang);
                }
            }

            _context.SaveChanges();


            return RedirectToAction("Yeucau", "Yeucau", new { area = "NhanvienKetoan" });
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


            if (Phieuxuatkho.TrangThai == "�ang chu?n b? h�ng")
            {
                Phieuxuatkho.TrangThai = "Ch? l?y h�ng";
                _context.phieuxuatkho.Update(Phieuxuatkho);
                _context.SaveChanges();
            }
            else if (Phieuxuatkho.TrangThai == "Ch? l?y h�ng")
            {
                if (Phieuxuatkho.MaDuan != null)
                {
                    foreach (var VTxuatkho in VTphieuxuatkho)
                    {
                        var VTphieuxuatkhott = _context.vtphieuxuatkho.FirstOrDefault(vt => vt.MaXuatkho == VTxuatkho.MaXuatkho);
                        VTphieuxuatkhott.TrangThai = "�� xu?t kho";
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
                            TrangThai = "�� xu?t kho"
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
                        VTphieuxuatkhott.TrangThai = "�� xu?t kho";
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
                                TrangThai = "�ang mu?n"
                            };
                            _context.Add(VTkhonguoidung);
                        }

                    }
                }
                Phieuxuatkho.TrangThai = "�� l?y h�ng";
                _context.phieuxuatkho.Update(Phieuxuatkho);
                _context.SaveChanges();
            }
            else if (Phieuxuatkho.TrangThai == "�� l?y h�ng")
            {
                Phieuxuatkho.TrangThai = "Ho�n th�nh";
                _context.phieuxuatkho.Update(Phieuxuatkho);
                _context.SaveChanges();
            }
                return RedirectToAction("Phieuxuatkho", "Yeucau", new { area = "NhanvienKetoan" });
        }

        [HttpPost]
        public IActionResult ThemPhieumuahangSQL([FromBody] Phieumuahangviewmodel model)
        {
            try
            {
                var MaMuahang = model.MaMuahang;
                Console.WriteLine($"MaMuahang nh?n du?c: {MaMuahang}");

                var Phieumuahang = _context.phieumuahang
                                            .FirstOrDefault(y => y.MaMuahang == MaMuahang);
                if (Phieumuahang == null)
                {
                    Console.WriteLine("Kh�ng t�m th?y Phieumuahang.");
                    return Json(new { success = false, message = "M� mua h�ng kh�ng t?n t?i!" });
                }

                Phieumuahang.TrangThai = "�� b�o gi�";
                _context.phieumuahang.Update(Phieumuahang);

                var VTPhieumuahanglist = _context.vtphieumuahang
                                                  .Where(kt => kt.MaMuahang == MaMuahang)
                                                  .ToList();

                Console.WriteLine($"S? v?t tu du?c t�m th?y: {VTPhieumuahanglist.Count}");
                Console.WriteLine($"S? lu?ng ph?n t? trong VTphieumuahang: {model.VTphieumuahang?.Count ?? 0}");

                for (int i = 0; i < VTPhieumuahanglist.Count; i++)
                {
                    var VTmuahang = VTPhieumuahanglist[i];

                    // Ki?m tra n?u trong model.VTphieumuahang c� ph?n t? t?i c�ng v? tr�
                    if (i < model.VTphieumuahang.Count)
                    {
                        var updatedVTmuahang = model.VTphieumuahang[i];

                        Console.WriteLine($"C?p nh?t VTmuahang: {updatedVTmuahang.MaMuahang}");

                        // C?p nh?t gi� tr? DonGia v� ThanhTien
                        VTmuahang.DonGia = updatedVTmuahang.DonGia;
                        VTmuahang.ThanhTien = updatedVTmuahang.ThanhTien;

                        Console.WriteLine($"�on gi� l�: {updatedVTmuahang.DonGia}");
                        Console.WriteLine($"Th�nh ti?n l�: {updatedVTmuahang.ThanhTien}");

                        VTmuahang.TrangThai = "�� b�o gi�";
                        _context.vtphieumuahang.Update(VTmuahang);
                    }
                    else
                    {
                        Console.WriteLine($"Kh�ng c� d? li?u tuong ?ng trong model cho VTmuahang t?i index: {i}");
                    }
                }

                _context.SaveChanges();



                return Json(new { success = true, message = "D? li?u d� du?c g?i th�nh c�ng!" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"L?i: {ex.Message}");
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
                Console.WriteLine($"MaMuahang nh?n du?c: {MaMuahang}");
                var Phieumuahang = _context.phieumuahang.FirstOrDefault(p => p.MaMuahang == MaMuahang);
                var VTPhieumuahanglist = _context.vtphieumuahang.Where(vt => vt.MaMuahang == MaMuahang).ToList();
                if (chucVu2 == "Gi�m d?c")
                {
                    Phieumuahang.TrangThai = "Ch? thanh to�n";
                }
                else if (boPhan2 == "BP k? to�n")
                {
                    Phieumuahang.TrangThai = "�� thanh to�n";
                }
                else if (boPhan2 == "BP mua h�ng")
                {
                    Phieumuahang.TrangThai = "�� nh?n h�ng";
                    Taophieunhapkhobyphieumuahang(MaMuahang, phieunhapkho, vtphieunhapkho, phieumuahang, vtphieumuahang);
                }
                foreach (var VTPhieumuahang in VTPhieumuahanglist)
                {
                    if (chucVu2 == "Gi�m d?c")
                    {
                        VTPhieumuahang.TrangThai = "Ch? thanh to�n";
                    }
                    else if (boPhan2 == "BP k? to�n")
                    {
                        VTPhieumuahang.TrangThai = "�� thanh to�n";
                    }
                    else if (boPhan2 == "BP mua h�ng")
                    {
                        VTPhieumuahang.TrangThai = "�� nh?n h�ng";
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
                return RedirectToAction("Phieumuahang", "Yeucau", new { area = "NhanvienKetoan" });
        }

        [HttpPost]
        public IActionResult Taophieunhapkhobyphieumuahang(string MaMuahang, phieunhapkho phieunhapkho, vtphieunhapkho vtphieunhapkho, phieumuahang phieumuahang, vtphieumuahang vtphieumuahang)
        {
            var Phieumuahang = _context.phieumuahang.FirstOrDefault(p => p.MaMuahang == MaMuahang);
            var VTPhieumuahanglist = _context.vtphieumuahang.Where(vt => vt.MaMuahang == MaMuahang).ToList();

            int STT = 0;
            string MaNhapkho;

            // T?o m� phi?u nh?p kho duy nh?t
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
                TrangThai = "Ch? nh?p kho"
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
                    TrangThai = "Ch? nh?p kho",
                };
                _context.vtphieunhapkho.Add(newvtphieunhapkho);
            }
            _context.SaveChanges();

                return RedirectToAction("Phieumuahang", "Yeucau", new { area = "NhanvienKetoan" });
        }

        [HttpGet]
        public IActionResult GetDataByMaYeucau(string mayeucau)
        {
            var yeucau = _context.yeucau
                .FirstOrDefault(y => y.MaYeucau == mayeucau);

            if (yeucau == null) return NotFound();

            // L?y d? li?u t? vtyeucau (v?t tu y�u c?u g?c) cho ThemPhieunhapkho
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
                vtPhieuMuaHang = vtYeucau  // Tr? v? d? li?u t? vtyeucau
            });
        }

        [HttpPost]
        public IActionResult ThemPhieunhapkhoSQL(phieunhapkho phieunhapkho, vtphieunhapkho vtphieunhapkho, 
            string[] TenSanpham, string[] MaSanpham, string[] HangSX, string[] NhaCC, 
            int[] SL, string[] DonVi, string[] Makho, string LoaiNhapkho)
        {
            // Luu session ngay t? d?u d? d?m b?o kh�ng b? m?t khi c� exception
            var maNv = HttpContext.Session.GetString("MaNguoidung");
            if (string.IsNullOrEmpty(maNv))
            {
                TempData["Error"] = "Session d� h?t h?n. Vui l�ng dang nh?p l?i!";
                return RedirectToAction("Login", "Home", new { area = "" });
            }
            
            // Luu area d? d�ng trong catch block
            string currentArea = "NhanvienKetoan";
            
            try
            {

                // Ki?m tra d? li?u d?u v�o
                if (TenSanpham == null || TenSanpham.Length == 0)
                {
                    TempData["Error"] = "Vui l�ng nh?p �t nh?t m?t v?t tu!";
                    return RedirectToAction("ThemPhieunhapkho", "Yeucau", new { area = "NhanvienKetoan" });
                }

                if (string.IsNullOrEmpty(LoaiNhapkho))
                {
                    TempData["Error"] = "Vui l�ng ch?n lo?i nh?p kho!";
                    return RedirectToAction("ThemPhieunhapkho", "Yeucau", new { area = "NhanvienKetoan" });
                }

                // maNv d� du?c l?y ? tr�n (ngo�i try block d? d?m b?o kh�ng b? m?t)
                if (string.IsNullOrEmpty(phieunhapkho.MaNguoidung))
                {
                    phieunhapkho.MaNguoidung = maNv;
                }

                // T�nh to�n s? lu?ng c�c ph?n t?
                int count = TenSanpham.Length;

                int STT = 0;
                string MaNhapkho;

                // T?o m� phi?u nh?p kho duy nh?t
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
                
                // Thi?t l?p tr?ng th�i ban d?u theo quy tr�nh duy?t
                // N?u c� d? �n: g?i d?n Tru?ng d? �n
                // N?u kh�ng c� d? �n (c� nh�n): g?i d?n Gi�m d?c
                if (!string.IsNullOrEmpty(phieunhapkho.MaDuan))
                {
                    phieunhapkho.TrangThai = "Qu?n l� d? �n"; // Tru?ng d? �n duy?t
                }
                else
                {
                    phieunhapkho.TrangThai = "Gi�m d?c"; // Gi�m d?c duy?t
                }

                // T?o ho?c l?y m� y�u c?u d?c bi?t cho phi?u nh?p kho t? d? �n/c� nh�n
                // N?u kh�ng c� MaYeucau, t?o m?t yeucau d?c bi?t d? th?a m�n foreign key constraint
                if (string.IsNullOrEmpty(phieunhapkho.MaYeucau))
                {
                    // T?o m� y�u c?u d?c bi?t d?a tr�n lo?i nh?p kho
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

                    // Ki?m tra xem yeucau d?c bi?t d� t?n t?i chua
                    var existingYeucauDacBiet = _context.yeucau
                        .FirstOrDefault(y => y.MaYeucau == maYeucauDacBiet);

                    if (existingYeucauDacBiet == null)
                    {
                        // Ki?m tra xem MaDuan c� t?n t?i trong b?ng duans kh�ng
                        string ycMaDuan = null;
                        if (!string.IsNullOrEmpty(phieunhapkho.MaDuan))
                        {
                            // T�m ki?m m� d? �n trong database
                            // MySQL c� th? case-sensitive, n�n th? c? exact match v� case-insensitive
                            var duanExists = _context.duans
                                .FirstOrDefault(d => d.MaDuan == phieunhapkho.MaDuan);
                            
                            // N?u kh�ng t�m th?y v?i exact match, th? case-insensitive
                            if (duanExists == null)
                            {
                                duanExists = _context.duans
                                    .AsEnumerable() // Switch to in-memory d? d�ng case-insensitive
                                    .FirstOrDefault(d => d.MaDuan != null && 
                                                       d.MaDuan.Equals(phieunhapkho.MaDuan, StringComparison.OrdinalIgnoreCase));
                            }
                            
                            if (duanExists != null)
                            {
                                // D�ng gi� tr? t? database d? d?m b?o d�ng case
                                ycMaDuan = duanExists.MaDuan;
                                Console.WriteLine($"Found project: '{duanExists.MaDuan}' for input '{phieunhapkho.MaDuan}'");
                            }
                            else
                            {
                                // Log warning v� li?t k� c�c m� d? �n c� s?n d? debug
                                var allDuans = _context.duans.Select(d => d.MaDuan).ToList();
                                Console.WriteLine($"Warning: M� d? �n '{phieunhapkho.MaDuan}' kh�ng t?n t?i trong b?ng duans.");
                                Console.WriteLine($"Available project codes: {string.Join(", ", allDuans)}");
                                // �?t YCMaDuan = null thay v� empty string d? tr�nh foreign key violation
                            }
                        }
                        
                        // L?y th�ng tin ngu?i d�ng t? b?ng nguoidungs
                        var nguoiDung = _context.nguoidungs.FirstOrDefault(n => n.MaNguoidung == maNv);
                        string tenNguoiDung = nguoiDung?.TenNguoidung ?? "";
                        string boPhanNguoiDung = nguoiDung?.Bophan ?? "";
                        
                        // T?o yeucau d?c bi?t m?i
                        var newYeucauDacBiet = new yeucau
                        {
                            MaYeucau = maYeucauDacBiet,
                            TenYeucau = "Y�u c?u nh?p kho",
                            YCMaNguoidung = maNv,
                            NguoiYeucau = tenNguoiDung,
                            Bophan = boPhanNguoiDung,
                            YCMaDuan = ycMaDuan, // NULL n?u kh�ng c� ho?c kh�ng t?n t?i trong duans
                            NgayYeucau = DateTime.Now,
                            TrangThai = "�� duy?t" // Tr?ng th�i d� duy?t d? kh�ng hi?n th? trong danh s�ch y�u c?u thu?ng
                        };
                        _context.yeucau.Add(newYeucauDacBiet);
                        _context.SaveChanges();
                    }

                    phieunhapkho.MaYeucau = maYeucauDacBiet;
                }

                _context.phieunhapkho.Add(phieunhapkho);
                _context.SaveChanges();

                // LUU � QUAN TR?NG: KH�NG tr? t? kho d? �n/c� nh�n ngay khi t?o phi?u nh?p kho
                // Ch? tr? khi kho duy?t phi?u nh?p kho (trong Xuliphieunhapkho)
                // V� n?u tr? ngay th� s? thi?t h?i kho n?u phi?u b? t? ch?i ho?c chua du?c duy?t
                
                // X? l� v?t tu - CH? t?o b?n ghi, KH�NG tr? t? kho
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
                        MaYeucau = phieunhapkho.MaYeucau // D�ng c�ng MaYeucau v?i phieunhapkho
                    };

                    _context.vtphieunhapkho.Add(newvtphieunhapkho);
                }

                // Save changes sau khi x? l� t?t c? v?t tu
                try
                {
                    _context.SaveChanges();
                }
                catch (Exception exSave)
                {
                    Console.WriteLine($"Error saving changes: {exSave.Message}");
                    Console.WriteLine($"Stack trace: {exSave.StackTrace}");
                    throw; // Re-throw d? catch block b�n ngo�i x? l�
                }

                TempData["Success"] = "T?o phi?u nh?p kho th�nh c�ng!";
                return RedirectToAction("Phieunhapkho", "Yeucau", new { area = "NhanvienKetoan" });
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
                
                // Ki?m tra session - d�ng bi?n maNv d� luu tru?c d� thay v� l?y l?i
                // V� c� th? exception l�m m?t session, nhung n?u maNv d� du?c luu th� v?n d�ng du?c
                var maNvCheck = HttpContext.Session.GetString("MaNguoidung") ?? maNv;
                Console.WriteLine($"Session MaNguoidung after error: {maNvCheck ?? "NULL"}");
                Console.WriteLine($"Original maNv (from before try): {maNv ?? "NULL"}");
                
                // Lu�n redirect v? trang t?o phi?u v?i th�ng b�o l?i
                // Kh�ng redirect v? login tr? khi th?c s? kh�ng c� session t? d?u
                TempData["Error"] = $"C� l?i x?y ra khi x? l�: {ex.Message}. Vui l�ng ki?m tra l?i d? li?u ho?c li�n h? admin.";
                
                // Lu�n redirect v? trang t?o phi?u d? ngu?i d�ng c� th? th? l?i
                // Ch? redirect v? login n?u th?c s? kh�ng c� maNv t? d?u
                if (!string.IsNullOrEmpty(maNv))
                {
                    return RedirectToAction("ThemPhieunhapkho", "Yeucau", new { area = currentArea });
                }
                else
                {
                    // Tru?ng h?p n�y ch? x?y ra n?u session d� h?t h?n t? d?u (d� check ? tr�n)
                    TempData["Error"] = "Session d� h?t h?n. Vui l�ng dang nh?p l?i!";
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
            
            // L?y th�ng tin d? �n (n?u c�)
            var duan = !string.IsNullOrEmpty(Phieunhapkho.MaDuan) 
                ? _context.duans.FirstOrDefault(d => d.MaDuan == Phieunhapkho.MaDuan) 
                : null;

            if (action == "approve")
            {
                // Workflow duy?t:
                // 1. "Qu?n l� d? �n" (n?u c� d? �n) -> Tru?ng d? �n duy?t -> "Gi�m d?c"
                // 2. "Gi�m d?c" -> Gi�m d?c duy?t -> "Ch? nh?p kho"
                // 3. "Ch? nh?p kho" -> Kho x? l� -> "�� nh?p kho" v� c?ng v�o kho t?ng

                if (Phieunhapkho.TrangThai == "Qu?n l� d? �n")
                {
                    // Tru?ng d? �n duy?t
                    if (duan != null && duan.MaNguoiQLDA == maNv2)
                    {
                        Phieunhapkho.TrangThai = "Gi�m d?c";
                        foreach (var vt in VTPhieunhapkholist)
                        {
                            vt.TrangThai = "Gi�m d?c";
                            _context.vtphieunhapkho.Update(vt);
                        }
                    }
                }
                else if (Phieunhapkho.TrangThai == "Gi�m d?c")
                {
                    // Gi�m d?c duy?t
                    if (chucVu2 == "Gi�m d?c")
                    {
                        Phieunhapkho.TrangThai = "Ch? nh?p kho";
                        foreach (var vt in VTPhieunhapkholist)
                        {
                            vt.TrangThai = "Ch? nh?p kho";
                            _context.vtphieunhapkho.Update(vt);
                        }
                    }
                }
                else if (Phieunhapkho.TrangThai == "Ch? nh?p kho" && boPhan2 == "BP kho")
                {
                    // Kho x? l� nh?p kho
                    // LUU � QUAN TR?NG: Khi kho duy?t, CH? c?ng v�o kho t?ng
                    // KH�NG tr? t? kho d? �n/c� nh�n ? d�y
                    // Ch? tr? khi ngu?i nh?n x�c nh?n nh?n h�ng (tr?ng th�i "�� x�c nh?n nh?n h�ng")
                    Phieunhapkho.TrangThai = "�� nh?p kho";
                    
                    foreach (var VTPhieunhapkho in VTPhieunhapkholist)
                    {
                        // C?ng v�o kho t?ng (cho c? phi?u t? mua h�ng v� phi?u t? d? �n/c� nh�n)
                        var khotong = _context.khotongs.FirstOrDefault(k => 
                            k.TenSanpham == VTPhieunhapkho.TenSanpham && 
                            k.MaSanpham == VTPhieunhapkho.MaSanpham && 
                            k.HangSX == VTPhieunhapkho.HangSX &&
                            k.Makho == VTPhieunhapkho.Makho);
                            
                        if (khotong != null)
                        {
                            // C?ng s? lu?ng v�o t?n kho
                            khotong.SL += VTPhieunhapkho.SL ?? 0;
                            _context.khotongs.Update(khotong);
                        }
                        else
                        {
                            // T?o m?i v?t tu trong t?n kho n?u chua c�
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
                                TrangThai = "T?n kho"
                            };
                            _context.khotongs.Add(newKhotong);
                        }
                        
                        VTPhieunhapkho.TrangThai = "�� nh?p kho";
                        _context.vtphieunhapkho.Update(VTPhieunhapkho);
                    }
                    
                    // T? d?ng t?o phi?u xu?t kho n?u c� y�u c?u ban d?u v� chua c� phi?u xu?t kho
                    // Logic n�y �p d?ng cho C? v?t tu d? �n V� v?t tu c� nh�n
                    if (!string.IsNullOrEmpty(Phieunhapkho.MaYeucau))
                    {
                        // Ki?m tra xem d� c� phi?u xu?t kho cho y�u c?u n�y chua
                        var existingPhieuxuatkho = _context.phieuxuatkho
                            .FirstOrDefault(px => px.MaYeucau == Phieunhapkho.MaYeucau);
                        
                        if (existingPhieuxuatkho == null)
                        {
                            // L?y th�ng tin y�u c?u ban d?u
                            var yeucauBanDau = _context.yeucau
                                .FirstOrDefault(y => y.MaYeucau == Phieunhapkho.MaYeucau);
                            
                            if (yeucauBanDau != null)
                            {
                                // T?o m� phi?u xu?t kho duy nh?t
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
                                
                                // T?o phi?u xu?t kho
                                var newPhieuxuatkho = new phieuxuatkho
                                {
                                    MaXuatkho = MaXuatkho,
                                    MaYeucau = Phieunhapkho.MaYeucau,
                                    MaDuan = Phieunhapkho.MaDuan,
                                    MaNguoidung = Phieunhapkho.MaNguoidung,
                                    NgayXuatkho = DateTime.Now,
                                    TrangThai = "Ch? x�c nh?n"
                                };
                                _context.phieuxuatkho.Add(newPhieuxuatkho);
                                _context.SaveChanges();
                                
                                // L?y danh s�ch v?t tu y�u c?u ban d?u
                                var danhSachVatTuYC = _context.vtyeucau
                                    .Where(vt => vt.VTMaYeucau == Phieunhapkho.MaYeucau)
                                    .ToList();
                                
                                // T?o v?t tu trong phi?u xu?t kho d?a tr�n v?t tu trong phi?u nh?p kho
                                foreach (var VTPhieunhapkho in VTPhieunhapkholist)
                                {
                                    // T�m v?t tu tuong ?ng trong y�u c?u ban d?u
                                    var vtYeucau = danhSachVatTuYC.FirstOrDefault(vt => 
                                        vt.MaSanpham == VTPhieunhapkho.MaSanpham &&
                                        vt.YCMakho == VTPhieunhapkho.Makho);
                                    
                                    if (vtYeucau != null)
                                    {
                                        // L?y th�ng tin t? kho t?ng d? d?m b?o d�ng th�ng tin
                                        var khotong = _context.khotongs.FirstOrDefault(k => 
                                            k.MaSanpham == VTPhieunhapkho.MaSanpham &&
                                            k.Makho == VTPhieunhapkho.Makho);
                                        
                                        if (khotong != null)
                                        {
                                            // T�nh s? lu?ng xu?t kho (l?y s? lu?ng nh? nh?t gi?a y�u c?u v� s? lu?ng nh?p)
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
                                                TrangThai = "Ch? x�c nh?n"
                                            };
                                            _context.vtphieuxuatkho.Add(newVTPhieuxuatkho);
                                        }
                                    }
                                }
                                
                                _context.SaveChanges();
                                
                                // Sau khi t?o phi?u xu?t kho, ki?m tra t?n kho v� t? d?ng chuy?n tr?ng th�i nhu phi?u xu?t kho co b?n
                                var VTPhieuxuatkhoList = _context.vtphieuxuatkho
                                    .Where(vt => vt.MaXuatkho == MaXuatkho)
                                    .ToList();
                                
                                bool duHang = true;
                                var vatTuThieu = new List<vtphieuxuatkho>();
                                
                                foreach (var VTxuatkho in VTPhieuxuatkhoList)
                                {
                                    var khotong = _context.khotongs.FirstOrDefault(k => k.Makho == VTxuatkho.Makho && k.MaSanpham == VTxuatkho.MaSanpham);
                                    // Ki?m tra ch?t ch?: kh�ng c� h�ng, s? lu?ng = 0, ho?c kh�ng d? s? lu?ng ? kh�ng cho xu?t
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
                                    // �? h�ng ? t? d?ng chuy?n sang "�ang chu?n b? h�ng" (v� h�ng v?a nh?p v�o n�n ch?c ch?n d?)
                                    newPhieuxuatkho.TrangThai = "�ang chu?n b? h�ng";
                                    newPhieuxuatkho.NgayChuanBi = DateTime.Now;
                                    _context.phieuxuatkho.Update(newPhieuxuatkho);
                                    
                                    // C?p nh?t tr?ng th�i v?t tu
                                    foreach (var VTxuatkho in VTPhieuxuatkhoList)
                                    {
                                        VTxuatkho.TrangThai = "�ang chu?n b? h�ng";
                                        _context.vtphieuxuatkho.Update(VTxuatkho);
                                    }
                                    
                                    _context.SaveChanges();
                                    Console.WriteLine($"�� t? d?ng t?o phi?u xu?t kho {MaXuatkho} cho y�u c?u {Phieunhapkho.MaYeucau} v� chuy?n sang tr?ng th�i '�ang chu?n b? h�ng'");
                                }
                                else
                                {
                                    // Thi?u h�ng (tru?ng h?p n�y hi?m v� v?a nh?p v�o, nhung d? an to�n)
                                    newPhieuxuatkho.TrangThai = "Thi?u h�ng";
                                    newPhieuxuatkho.GhiChu = "Kh�ng d? s? lu?ng t?n kho.";
                                    _context.phieuxuatkho.Update(newPhieuxuatkho);
                                    _context.SaveChanges();
                                    Console.WriteLine($"�� t? d?ng t?o phi?u xu?t kho {MaXuatkho} cho y�u c?u {Phieunhapkho.MaYeucau} nhung thi?u h�ng");
                                }
                            }
                        }
                    }
                }
                else if (Phieunhapkho.TrangThai == "�� nh?p kho" && boPhan2 == "BP kho")
                {
                    // Khi tr?ng th�i l� "�� nh?p kho" v� ngu?i nh?n x�c nh?n nh?n h�ng
                    // M?I tr? t? kho d? �n/c� nh�n (s?n lu?ng th?a du?c tr? l?i)
                    Phieunhapkho.TrangThai = "�� x�c nh?n nh?n h�ng";
                    
                    foreach (var VTPhieunhapkho in VTPhieunhapkholist)
                    {
                        // Tr? t? kho d? �n/c� nh�n khi ngu?i nh?n x�c nh?n nh?n h�ng
                        bool isFromDuanOrCaNhan = false;
                        
                        // Ki?m tra t? d? �n: N?u c� MaDuan v� c� v?t tu trong kho d? �n
                        if (!string.IsNullOrEmpty(Phieunhapkho.MaDuan) && !string.IsNullOrEmpty(VTPhieunhapkho.MaSanpham))
                        {
                            var vtXuatKhoItems = (from vt in _context.vtphieuxuatkho
                                                  join px in _context.phieuxuatkho on vt.MaXuatkho equals px.MaXuatkho
                                                  where px.MaDuan == Phieunhapkho.MaDuan
                                                     && vt.MaSanpham == VTPhieunhapkho.MaSanpham
                                                     && (vt.TrangThai == "�� x�c nh?n nh?n h�ng" 
                                                         || vt.TrangThai == "�� l?y h�ng"
                                                         || vt.TrangThai == "�� xu?t kho")
                                                     && (vt.SL ?? 0) > 0
                                                  orderby vt.ID ascending
                                                  select vt)
                                                  .ToList();
                            
                            if (vtXuatKhoItems.Any())
                            {
                                isFromDuanOrCaNhan = true;
                                // Tr? t? kho d? �n
                                int slCanTra = VTPhieunhapkho.SL ?? 0;
                                foreach (var vtItem in vtXuatKhoItems)
                                {
                                    if (slCanTra <= 0) break;
                                    
                                    int slHienTai = vtItem.SL ?? 0;
                                    int slTru = Math.Min(slHienTai, slCanTra);
                                    vtItem.SL = slHienTai - slTru;
                                    
                                    if ((vtItem.SL ?? 0) <= 0)
                                    {
                                        vtItem.TrangThai = "�� tr? kho";
                                    }
                                    
                                    _context.vtphieuxuatkho.Update(vtItem);
                                    slCanTra -= slTru;
                                }
                            }
                        }
                        
                        // Ki?m tra t? c� nh�n: N?u kh�ng c� MaDuan v� c� v?t tu trong kho c� nh�n
                        if (!isFromDuanOrCaNhan && string.IsNullOrEmpty(Phieunhapkho.MaDuan) && !string.IsNullOrEmpty(VTPhieunhapkho.MaSanpham) && !string.IsNullOrEmpty(Phieunhapkho.MaNguoidung))
                        {
                            var khoCaNhanItem = _context.khonguoidungs
                                .FirstOrDefault(k => k.NDMaNguoidung == Phieunhapkho.MaNguoidung 
                                                   && k.MaSanpham == VTPhieunhapkho.MaSanpham 
                                                   && (k.TrangThai == "�ang mu?n" || k.TrangThai == "�ang s? d?ng")
                                                   && (k.SL ?? 0) >= (VTPhieunhapkho.SL ?? 0));
                            
                            if (khoCaNhanItem != null)
                            {
                                isFromDuanOrCaNhan = true;
                                // Tr? t? kho c� nh�n
                                khoCaNhanItem.SL -= VTPhieunhapkho.SL ?? 0;
                                if (khoCaNhanItem.SL <= 0)
                                {
                                    khoCaNhanItem.TrangThai = "�� tr?";
                                }
                                _context.khonguoidungs.Update(khoCaNhanItem);
                            }
                        }
                        
                        VTPhieunhapkho.TrangThai = "�� x�c nh?n nh?n h�ng";
                        _context.vtphieunhapkho.Update(VTPhieunhapkho);
                    }
                }
                else if (Phieunhapkho.TrangThai == "�� x�c nh?n nh?n h�ng")
                {
                    // Ho�n th�nh phi?u nh?p kho
                    Phieunhapkho.TrangThai = "Ho�n th�nh";
                    
                    foreach (var VTPhieunhapkho in VTPhieunhapkholist)
                    {
                        VTPhieunhapkho.TrangThai = "Ho�n th�nh";
                        _context.vtphieunhapkho.Update(VTPhieunhapkho);
                    }
                }

                _context.phieunhapkho.Update(Phieunhapkho);
            }
            else if (action == "reject")
            {
                Phieunhapkho.TrangThai = $"{chucVu2} - �� t? ch?i";
                foreach (var vt in VTPhieunhapkholist)
                {
                    vt.TrangThai = $"{chucVu2} - �� t? ch?i";
                    _context.vtphieunhapkho.Update(vt);
                }
                _context.phieunhapkho.Update(Phieunhapkho);
            }
            
            _context.SaveChanges();
            return RedirectToAction("Phieunhapkho", "Yeucau", new { area = "NhanvienKetoan" });
        }

        [HttpPost]
        public IActionResult Taophieuxuatkhobyphieunhapkho(string MaNhapkho, phieuxuatkho phieuxuatkho, vtphieuxuatkho vtphieuxuatkho, phieunhapkho phieunhapkho, vtphieunhapkho vtphieunhapkho)
        {
            var Phieunhapkho = _context.phieunhapkho.FirstOrDefault(p => p.MaNhapkho == MaNhapkho);
            var VTPhieunhapkholist = _context.vtphieunhapkho.Where(vt => vt.MaNhapkho == MaNhapkho).ToList();

            int STT = 0;
            string MaXuatkho;
            // T?o m� phi?u nh?p kho duy nh?t
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
                TrangThai = "�ang chu?n b? h�ng"
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
                    TrangThai = "�ang chu?n b? h�ng",
                };
                _context.vtphieuxuatkho.Add(newvtphieuxuatkho);
            }
            _context.SaveChanges();

            return RedirectToAction("Phieunhapkho", "Yeucau", new { area = "NhanvienKetoan" });
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
                    Phieu.TrangThai = $"{chucVu} - �� t? ch?i";
                    _context.yeucau.Update(Phieu);

                    var Listvtyeucau = _context.vtyeucau.Where(p => p.VTMaYeucau == Ma).ToList();
                    foreach (var VTyeucau in Listvtyeucau)
                    {
                        VTyeucau.TrangThai = $"{chucVu} - �� t? ch?i";
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
                    Phieumuahang.TrangThai = $"{chucVu} - �� t? ch?i";
                    _context.phieumuahang.Update(Phieumuahang);

                    var Listvtmuahang = _context.vtphieumuahang.Where(p => p.MaMuahang == Ma).ToList();
                    foreach (var VTmuahang in Listvtmuahang)
                    {
                        VTmuahang.TrangThai = $"{chucVu} - �� t? ch?i";
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

            return RedirectToAction("Yeucau", "Yeucau", new { area = "NhanvienKetoan" });
        }

        public IActionResult XacnhanNhanHang()
        {
            var currentUserId = HttpContext.Session.GetString("MaNguoidung");

            // L?y c�c y�u c?u m� k? thu?t vi�n n�y d� t?o
            var yeuCauList = _context.yeucau
                .Where(y => y.YCMaNguoidung == currentUserId)
                .Select(y => y.MaYeucau)
                .ToList();

            // L?y phi?u xu?t kho li�n quan t?i c�c y�u c?u d�
            // Hi?n th? c? phi?u dang ch? x�c nh?n v� d� x�c nh?n
            var PhieuxuatkhoList = _context.phieuxuatkho
                .Where(p => yeuCauList.Contains(p.MaYeucau)
                         && (p.TrangThai == "Ch? ngu?i y�u c?u x�c nh?n" 
                             || p.TrangThai == "�� x�c nh?n nh?n h�ng"))
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
        // X�C NH?N H�NG
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
            var tongSoLuongDaCamKet = _context.vtphieuxuatkho
                .Where(vt => phieuXuatDaCamKet.Contains(vt.MaXuatkho) 
                    && vt.Makho == makho 
                    && vt.MaSanpham == masanpham)
                .Sum(vt => vt.SL ?? 0);

            return tongSoLuongDaCamKet;
        }

        [HttpPost]
        public IActionResult XacnhanNhanHang(string MaXuatkho)
        {
            var phieu = _context.phieuxuatkho.FirstOrDefault(p => p.MaXuatkho == MaXuatkho);

            if (phieu != null && phieu.TrangThai == "Ch? ngu?i y�u c?u x�c nh?n")
            {
                phieu.TrangThai = "�� x�c nh?n nh?n h�ng";
                phieu.NgayXacNhanNhan = DateTime.Now;
                _context.phieuxuatkho.Update(phieu);

                // ? C?p nh?t tr?ng th�i v?t tu trong phi?u xu?t kho
                var VTphieuxuatkhoList = _context.vtphieuxuatkho
                    .Where(vt => vt.MaXuatkho == MaXuatkho)
                    .ToList();

                foreach (var vt in VTphieuxuatkhoList)
                {
                    // C?p nh?t tr?ng th�i v?t tu th�nh "�� x�c nh?n nh?n h�ng"
                    vt.TrangThai = "�� x�c nh?n nh?n h�ng";
                    vt.NgayNhapkho = DateTime.Now;
                    _context.vtphieuxuatkho.Update(vt);
                    
                    // Tr? kho t?ng khi x�c nh?n nh?n h�ng - KI?M TRA CH?T CH? S? LU?NG
                    var khotong = _context.khotongs.FirstOrDefault(k => k.Makho == vt.Makho && k.MaSanpham == vt.MaSanpham);
                    if (khotong != null)
                    {
                        // TUY?T �?I KH�NG cho ph�p xu?t n?u h?t h�ng ho?c kh�ng d? s? lu?ng
                        // Tính số lượng hàng đã cam kết từ các phiếu xuất khác (loại trừ phiếu hiện tại vì nó đang được xác nhận)
                        int soLuongDaCamKetKhac = TinhSoLuongDaCamKet(vt.Makho ?? "", vt.MaSanpham ?? "", MaXuatkho);
                        
                        // Số lượng khả dụng = Tồn kho - Số lượng đã cam kết từ các phiếu khác
                        int soLuongKhaDung = (khotong.SL ?? 0) - soLuongDaCamKetKhac;
                        
                        // TUYỆT ĐỐI KHÔNG cho phép xuất nếu hết hàng hoặc không đủ số lượng
                        if (soLuongKhaDung <= 0 || soLuongKhaDung < vt.SL)
                        {
                            TempData["ErrorMessage"] = $"Kh�ng th? xu?t kho: V?t tu {vt.TenSanpham} kh�ng d? s? lu?ng trong kho (T?n kho: {khotong.SL}, Y�u c?u: {vt.SL})";
                            return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "NhanvienKetoan" });
                        }
                        
                        khotong.SL -= vt.SL;
                        _context.khotongs.Update(khotong);
                    }
                    else
                    {
                        TempData["ErrorMessage"] = $"Kh�ng th? xu?t kho: V?t tu {vt.TenSanpham} kh�ng t?n t?i trong kho";
                        return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "NhanvienKetoan" });
                    }
                    
                    // ch? x? l� n?u phi?u n�y kh�ng c� d? �n
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
                                TrangThai = "�ang mu?n",
                                NgayNhapkho = DateTime.Now
                            };
                            _context.khonguoidungs.Add(newItem);
                        }
                    }
                }

                _context.SaveChanges();

                TempData["SuccessMessage"] = "X�c nh?n nh?n h�ng th�nh c�ng!";
                return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "NhanvienKetoan" });
            }

            TempData["ErrorMessage"] = "Phi?u kh�ng h?p l? ho?c d� du?c x�c nh?n!";
            return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "NhanvienKetoan" });
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

        // Action t? d?ng d?ng b? khi load trang (g?i t? JavaScript)
        [HttpGet]
        public IActionResult AutoDongBoTrangThai()
        {
            try
            {
                var currentUserId = HttpContext.Session.GetString("MaNguoidung");
                
                // L?y c�c y�u c?u m� k? thu?t vi�n n�y d� t?o
                var yeuCauList = _context.yeucau
                    .Where(y => y.YCMaNguoidung == currentUserId)
                    .Select(y => y.MaYeucau)
                    .ToList();

                // L?y c�c phi?u d� x�c nh?n nh?n h�ng
                var phieuxuatkhoList = _context.phieuxuatkho
                    .Where(p => yeuCauList.Contains(p.MaYeucau)
                             && p.TrangThai == "�� x�c nh?n nh?n h�ng")
                    .ToList();

                int updatedCount = 0;
                foreach (var phieu in phieuxuatkhoList)
                {
                    var VTphieuxuatkhoList = _context.vtphieuxuatkho
                        .Where(vt => vt.MaXuatkho == phieu.MaXuatkho
                                 && vt.TrangThai != "�� x�c nh?n nh?n h�ng"
                                 && vt.TrangThai != "�� xu?t kho")
                        .ToList();

                    foreach (var vt in VTphieuxuatkhoList)
                    {
                        vt.TrangThai = "�� x�c nh?n nh?n h�ng";
                        _context.vtphieuxuatkho.Update(vt);
                        updatedCount++;
                    }
                }

                _context.SaveChanges();
                return Json(new { success = true, message = $"�� d?ng b? {updatedCount} v?t tu!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "L?i: " + ex.Message });
            }
        }

        // In phiếu mua hàng
        [HttpGet]
        public IActionResult InPhieumuahang(string MaMuahang)
        {
            if (string.IsNullOrEmpty(MaMuahang))
            {
                return NotFound();
            }

            var phieumuahang = _context.phieumuahang
                .FirstOrDefault(p => p.MaMuahang == MaMuahang);

            if (phieumuahang == null)
            {
                return NotFound();
            }

            var vtphieumuahang = _context.vtphieumuahang
                .Where(vt => vt.MaMuahang == MaMuahang)
                .ToList();

            var yeucau = _context.yeucau
                .FirstOrDefault(y => y.MaYeucau == phieumuahang.MaYeucau);

            ViewBag.Phieumuahang = phieumuahang;
            ViewBag.VTPhieumuahang = vtphieumuahang;
            ViewBag.Yeucau = yeucau;

            return View();
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

            var yeucau = _context.yeucau
                .FirstOrDefault(y => y.MaYeucau == phieunhapkho.MaYeucau);

            ViewBag.Phieunhapkho = phieunhapkho;
            ViewBag.VTPhieunhapkho = vtphieunhapkho;
            ViewBag.Yeucau = yeucau;

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

            var yeucau = _context.yeucau
                .FirstOrDefault(y => y.MaYeucau == phieuxuatkho.MaYeucau);

            ViewBag.Phieuxuatkho = phieuxuatkho;
            ViewBag.VTPhieuxuatkho = vtphieuxuatkho;
            ViewBag.Yeucau = yeucau;

            return View();
        }

    }
}

