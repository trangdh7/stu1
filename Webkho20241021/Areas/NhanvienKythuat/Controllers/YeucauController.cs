using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Webkho_20241021.Models;
using Webkho_20241021.Areas.NhanvienKythuat.Data;
using System;
using System.Linq;


namespace Webkho_20241021.Areas.NhanvienKythuat.Controllers
{
    [Area("NhanvienKythuat")]
    [Authorize(Roles = "Nhân viên-BP kỹ thuật")]
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

            var Maduanquanli = _context.duans
                .Where(d => d.MaNguoiQLDA == maNv)
                .Select(d => d.MaDuan)
                .ToList();
            int QLDAyeucaucount = _context.yeucau.Count(p => p.TrangThai == "Quản lí dự án" && Maduanquanli.Contains(p.YCMaDuan));
            int Duyetyeucaucount = _context.yeucau.Count(p => p.TrangThai == (chucVu + "-" + boPhan));
            int thongbaoyeucaucount = Duyetyeucaucount + QLDAyeucaucount;

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
            var Duanlist = _context.duans
                          .Select(y => new { y.MaDuan, y.TrangThai })
                          .ToList();

            ViewBag.Duanlist = Duanlist;
            
            // Lấy mã nhân viên từ session để điền vào form
            var maNv = HttpContext.Session.GetString("MaNguoidung");
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

            try
            {
                // Lấy vật tư từ vtphieuxuatkho (đã xuất kho) kết hợp với phieuxuatkho theo MaDuan
                // Các vật tư đã được xuất kho cho dự án này có thể được trả lại
                var khoDuanItems = (from vt in _context.vtphieuxuatkho
                                   join px in _context.phieuxuatkho on vt.MaXuatkho equals px.MaXuatkho
                                   where px.MaDuan == maduan 
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

            return RedirectToAction("Yeucau", "Yeucau", new { area = "NhanvienKythuat" });

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

            return RedirectToAction("Yeucau", "Yeucau", new { area = "NhanvienKythuat" });
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
                return RedirectToAction("Yeucau", "Yeucau", new { area = "NhanvienKythuat" });
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
                    NgayXuatkho = DateTime.Now,
                    TrangThai = "Đang chuẩn bị hàng"
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


            return RedirectToAction("Yeucau", "Yeucau", new { area = "NhanvienKythuat" });
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
            return RedirectToAction("Phieuxuatkho", "Yeucau", new { area = "NhanvienKythuat" });
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
            return RedirectToAction("Phieumuahang", "Yeucau", new { area = "NhanvienKythuat" });
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

            return RedirectToAction("Phieumuahang", "Yeucau", new { area = "NhanvienKythuat" });
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
            // Lưu session ngay từ đầu để đảm bảo không bị mất khi có exception
            var maNv = HttpContext.Session.GetString("MaNguoidung");
            if (string.IsNullOrEmpty(maNv))
            {
                TempData["Error"] = "Session đã hết hạn. Vui lòng đăng nhập lại!";
                return RedirectToAction("Login", "Home", new { area = "" });
            }
            
            // Lưu area để dùng trong catch block
            string currentArea = "NhanvienKythuat";
            
            try
            {

                // Kiểm tra dữ liệu đầu vào
                if (TenSanpham == null || TenSanpham.Length == 0)
                {
                    TempData["Error"] = "Vui lòng nhập ít nhất một vật tư!";
                    return RedirectToAction("ThemPhieunhapkho", "Yeucau", new { area = "NhanvienKythuat" });
                }

                if (string.IsNullOrEmpty(LoaiNhapkho))
                {
                    TempData["Error"] = "Vui lòng chọn loại nhập kho!";
                    return RedirectToAction("ThemPhieunhapkho", "Yeucau", new { area = "NhanvienKythuat" });
                }

                // maNv đã được lấy ở trên (ngoài try block để đảm bảo không bị mất)
                if (string.IsNullOrEmpty(phieunhapkho.MaNguoidung))
                {
                    phieunhapkho.MaNguoidung = maNv;
                }

                // Tính toán số lượng các phần tử
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
                phieunhapkho.NgayNhapkho = DateTime.Now;
                
                // Thiết lập trạng thái ban đầu theo quy trình duyệt
                // Nếu có dự án: gửi đến Trưởng dự án
                // Nếu không có dự án (cá nhân): gửi đến Giám đốc
                if (!string.IsNullOrEmpty(phieunhapkho.MaDuan))
                {
                    phieunhapkho.TrangThai = "Quản lí dự án"; // Trưởng dự án duyệt
                }
                else
                {
                    phieunhapkho.TrangThai = "Giám đốc"; // Giám đốc duyệt
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
                                    .AsEnumerable() // Switch to in-memory để dùng case-insensitive
                                    .FirstOrDefault(d => d.MaDuan != null && 
                                                       d.MaDuan.Equals(phieunhapkho.MaDuan, StringComparison.OrdinalIgnoreCase));
                            }
                            
                            if (duanExists != null)
                            {
                                // Dùng giá trị từ database để đảm bảo đúng case
                                ycMaDuan = duanExists.MaDuan;
                                Console.WriteLine($"Found project: '{duanExists.MaDuan}' for input '{phieunhapkho.MaDuan}'");
                            }
                            else
                            {
                                // Log warning và liệt kê các mã dự án có sẵn để debug
                                var allDuans = _context.duans.Select(d => d.MaDuan).ToList();
                                Console.WriteLine($"Warning: Mã dự án '{phieunhapkho.MaDuan}' không tồn tại trong bảng duans.");
                                Console.WriteLine($"Available project codes: {string.Join(", ", allDuans)}");
                                // Đặt YCMaDuan = null thay vì empty string để tránh foreign key violation
                            }
                        }
                        
                        // Lấy thông tin người dùng từ bảng nguoidungs
                        var nguoiDung = _context.nguoidungs.FirstOrDefault(n => n.MaNguoidung == maNv);
                        string tenNguoiDung = nguoiDung?.TenNguoidung ?? "";
                        string boPhanNguoiDung = nguoiDung?.Bophan ?? "";
                        
                        // Tạo yeucau đặc biệt mới
                        var newYeucauDacBiet = new yeucau
                        {
                            MaYeucau = maYeucauDacBiet,
                            TenYeucau = "Yêu cầu nhập kho",
                            YCMaNguoidung = maNv,
                            NguoiYeucau = tenNguoiDung,
                            Bophan = boPhanNguoiDung,
                            YCMaDuan = ycMaDuan, // NULL nếu không có hoặc không tồn tại trong duans
                            NgayYeucau = DateTime.Now,
                            TrangThai = "Đã duyệt" // Trạng thái đã duyệt để không hiển thị trong danh sách yêu cầu thường
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
                return RedirectToAction("Phieunhapkho", "Yeucau", new { area = "NhanvienKythuat" });
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
            
            // Lấy thông tin dự án (nếu có)
            var duan = !string.IsNullOrEmpty(Phieunhapkho.MaDuan) 
                ? _context.duans.FirstOrDefault(d => d.MaDuan == Phieunhapkho.MaDuan) 
                : null;

            if (action == "approve")
            {
                // Workflow duyệt:
                // 1. "Quản lí dự án" (nếu có dự án) -> Trưởng dự án duyệt -> "Giám đốc"
                // 2. "Giám đốc" -> Giám đốc duyệt -> "Chờ nhập kho"
                // 3. "Chờ nhập kho" -> Kho xử lý -> "Đã nhập kho" và cộng vào kho tổng

                if (Phieunhapkho.TrangThai == "Quản lí dự án")
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
                else if (Phieunhapkho.TrangThai == "Giám đốc")
                {
                    // Giám đốc duyệt
                    if (chucVu2 == "Giám đốc")
                    {
                        Phieunhapkho.TrangThai = "Chờ nhập kho";
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
            return RedirectToAction("Phieunhapkho", "Yeucau", new { area = "NhanvienKythuat" });
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

            return RedirectToAction("Phieunhapkho", "Yeucau", new { area = "NhanvienKythuat" });
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

            return RedirectToAction("Yeucau", "Yeucau", new { area = "NhanvienKythuat" });
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
                            return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "NhanvienKythuat" });
                        }
                        
                        khotong.SL -= vt.SL;
                        _context.khotongs.Update(khotong);
                    }
                    else
                    {
                        TempData["ErrorMessage"] = $"Không thể xuất kho: Vật tư {vt.TenSanpham} không tồn tại trong kho";
                        return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "NhanvienKythuat" });
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
                return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "NhanvienKythuat" });
            }

            TempData["ErrorMessage"] = "Phiếu không hợp lệ hoặc đã được xác nhận!";
            return RedirectToAction("XacnhanNhanHang", "Yeucau", new { area = "NhanvienKythuat" });
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



    }
}
