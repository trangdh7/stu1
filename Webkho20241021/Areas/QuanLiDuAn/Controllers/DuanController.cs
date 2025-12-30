using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Webkho_20241021.Areas.QuanLiDuAn.Data;
using Webkho_20241021.Models;
using Webkho_20241021.Models.ViewModels;

namespace Webkho_20241021.Areas.QuanLiDuAn.Controllers
{
    [Area("QuanLiDuAn")]
    [Authorize(Roles = "Quản lí dự án")]
    public class DuanController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DuanController(ApplicationDbContext context)
        {
            _context = context;
        }
        public IActionResult Duan()
        {
            var Duanlist = _context.duans.ToList();
            var Khoduanlist = _context.khoduans.ToList();
            ViewBag.CurrentUserId = HttpContext.Session.GetString("MaNguoidung");
            var model = new Duanviewmodel
            {
                Duan = Duanlist,
                KhoDuan = Khoduanlist
            };
            return View(model);
        }
        public IActionResult ThemDuan()
        {
            var Tennguoidunglist = _context.nguoidungs
                              .Select(n => new { n.TenNguoidung, n.MaNguoidung })  // Lấy cả MaNguoidung
                              .ToList();

            ViewBag.Tennguoidunglist = Tennguoidunglist;
            return View();
        }
        public IActionResult GetVTDuan(string MaDuan)
        {
            var vatTuList = from vt in _context.vtphieuxuatkho
                            join px in _context.phieuxuatkho on vt.MaXuatkho equals px.MaXuatkho
                            where px.MaDuan == MaDuan
                                  && (vt.TrangThai == "Đã xác nhận nhận hàng"
                                      || vt.TrangThai == "Đã xuất kho"
                                      || px.TrangThai == "Đã xác nhận nhận hàng"
                                      || px.TrangThai == "Hoàn thành")
                            select new
                            {
                                TenSanpham = vt.TenSanpham,
                                MaSanpham = vt.MaSanpham,
                                DAMakho = vt.Makho,
                                HangSX = vt.HangSX,
                                NhaCC = vt.NhaCC,
                                SL = vt.SL,
                                DonVi = vt.DonVi,
                                NgayNhapkho = vt.NgayNhapkho ?? px.NgayXacNhanNhan,
                                NgayBaohanh = vt.NgayBaohanh,
                                ThoiGianBH = vt.ThoiGianBH,
                                TrangThai = vt.TrangThai ?? "Đã xác nhận nhận hàng"
                            };

            var result = vatTuList.GroupBy(v => new { v.MaSanpham, v.DAMakho })
                                  .Select(g => g.First())
                                  .ToList();

            return Json(result);
        }

        [HttpPost]
        public IActionResult ThemDuanSQL(duans duans)
        {
            // Kiểm tra trùng mã dự án
            bool isExist = _context.duans.Any(d => d.MaDuan == duans.MaDuan);

            if (isExist)
            {
                ModelState.AddModelError("MaDuan", "Mã dự án đã tồn tại!");
                // Load lại dữ liệu dropdown
                ViewBag.Tennguoidunglist = _context.nguoidungs.ToList();
                return View("ThemDuan");
            }

            duans.TrangThai = "Chờ";
            _context.duans.Add(duans);
            _context.SaveChanges();

            return RedirectToAction("Duan", "Duan", new { area = "QuanLiDuAn" });
        }



        [HttpPost]
        public async Task<IActionResult> Xuliduan(string MaDuan, string action)
        {

            var duan = await _context.duans.FirstOrDefaultAsync(d => d.MaDuan == MaDuan);
            if (duan == null)
            {
                return NotFound();
            }
            var currentUserId = HttpContext.Session.GetString("MaNguoidung");
            if (string.IsNullOrWhiteSpace(currentUserId) ||
                string.IsNullOrWhiteSpace(duan.MaNguoiQLDA) ||
                !string.Equals(currentUserId.Trim(), duan.MaNguoiQLDA.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Chỉ người quản lý dự án được phép cập nhật tiến trình dự án này.";
                return RedirectToAction("Duan", "Duan", new { area = "QuanLiDuAn" });
            }
            if (action == "start" && duan.TrangThai == "Chờ")
            {
                duan.NgayBatdau = DateTime.Now;
                duan.TrangThai = "Đang triển khai";
            }
            else if (action == "end" && duan.TrangThai == "Đang triển khai")
            {
                duan.NgayKetthuc = DateTime.Now;
                duan.TrangThai = "Đã hoàn thành";
            }

            _context.Update(duan);
            await _context.SaveChangesAsync();

            return RedirectToAction("Duan", "Duan", new { area = "QuanLiDuAn" });
        }

        // Hiển thị chi tiết vật tư đã cấp phát cho dự án
        public IActionResult ChiTietVatTuDuan(string MaDuan, string? maNguoiNhan, string? keyword)
        {
            if (string.IsNullOrWhiteSpace(MaDuan))
            {
                return NotFound();
            }

            var duan = _context.duans.FirstOrDefault(d => d.MaDuan == MaDuan);
            if (duan == null)
            {
                return NotFound();
            }

            // Lấy danh sách vật tư đã được cấp phát cho dự án.
            // Một số bản ghi vtphieuxuatkho không lưu MaYeucau nên chỉ join theo MaXuatkho để tránh mất dữ liệu.
            var vatTuQuery = from vt in _context.vtphieuxuatkho
                             join px in _context.phieuxuatkho
                                 on vt.MaXuatkho equals px.MaXuatkho
                             join nd in _context.nguoidungs on px.MaNguoidung equals nd.MaNguoidung into ndGroup
                             from nd in ndGroup.DefaultIfEmpty()
                             where px.MaDuan == MaDuan
                                   && (vt.TrangThai == "Đã xác nhận nhận hàng"
                                       || vt.TrangThai == "Đã xuất kho"
                                       || px.TrangThai == "Đã xác nhận nhận hàng"
                                       || px.TrangThai == "Hoàn thành"
                                       || px.TrangThai == "Đã xuất kho")
                             select new VatTuDuAnItemViewModel
                             {
                                 TenSanpham = vt.TenSanpham,
                                 MaSanpham = vt.MaSanpham,
                                 DAMakho = vt.Makho,
                                 HangSX = vt.HangSX,
                                 NhaCC = vt.NhaCC,
                                 SL = vt.SL,
                                 DonVi = vt.DonVi,
                                 NgayNhapkho = vt.NgayNhapkho ?? px.NgayXacNhanNhan,
                                 NgayBaohanh = vt.NgayBaohanh,
                                 ThoiGianBH = vt.ThoiGianBH,
                                 TrangThai = vt.TrangThai ?? px.TrangThai ?? "Đã xác nhận nhận hàng",
                                 MaXuatkho = vt.MaXuatkho,
                                 MaYeucau = vt.MaYeucau,
                                 MaNguoidung = px.MaNguoidung,
                                 MaNguoiNhan = px.MaNguoidung,
                                 TenNguoiNhan = nd != null ? nd.TenNguoidung : (px.MaNguoidung ?? "Chưa xác định"),
                                 NgayXuatkho = px.NgayXuatkho,
                                 NgayXacNhanNhan = px.NgayXacNhanNhan
                             };

            var nguoiNhanSummaries = vatTuQuery
                .GroupBy(v => new
                {
                    Key = (v.MaNguoidung ?? v.MaNguoiNhan ?? string.Empty).Trim(),
                    Ten = v.TenNguoiNhan ?? (v.MaNguoidung ?? v.MaNguoiNhan ?? "Chưa xác định")
                })
                .Select(g => new NguoiNhanVatTuSummary
                {
                    MaNguoi = g.Key.Key,
                    TenNguoi = g.Key.Ten,
                    TongSoLuong = g.Sum(x => x.SL ?? 0),
                    SoLoaiVatTu = g.Count()
                })
                .OrderByDescending(x => x.TongSoLuong)
                .ThenBy(x => x.TenNguoi)
                .ToList();

            var filteredVatTu = vatTuQuery.ToList();
            string selectedTenNguoi = string.Empty;
            if (!string.IsNullOrWhiteSpace(maNguoiNhan))
            {
                var normalized = maNguoiNhan.Trim();
                filteredVatTu = filteredVatTu
                    .Where(v => string.Equals((v.MaNguoidung ?? v.MaNguoiNhan ?? string.Empty).Trim(), normalized, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                selectedTenNguoi = nguoiNhanSummaries
                    .FirstOrDefault(x => string.Equals(x.MaNguoi?.Trim(), normalized, StringComparison.OrdinalIgnoreCase))?.TenNguoi
                    ?? normalized;
            }

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var lower = keyword.Trim().ToLower();
                filteredVatTu = filteredVatTu.Where(v =>
                        (v.TenSanpham ?? string.Empty).ToLower().Contains(lower) ||
                        (v.MaSanpham ?? string.Empty).ToLower().Contains(lower) ||
                        (v.DAMakho ?? string.Empty).ToLower().Contains(lower) ||
                        (v.HangSX ?? string.Empty).ToLower().Contains(lower) ||
                        (v.NhaCC ?? string.Empty).ToLower().Contains(lower))
                    .ToList();
            }

            filteredVatTu = filteredVatTu
                .OrderByDescending(v => v.NgayXuatkho)
                .ThenBy(v => v.TenSanpham)
                .ToList();

            // Xác định trạng thái dựa trên số lượng nếu chưa có trạng thái rõ ràng
            foreach (var vt in filteredVatTu)
            {
                // Nếu trạng thái là null, rỗng, hoặc là "Đã xác nhận nhận hàng" thì xác định lại dựa trên số lượng
                if (string.IsNullOrWhiteSpace(vt.TrangThai) || 
                    vt.TrangThai == "Đã xác nhận nhận hàng")
                {
                    if ((vt.SL ?? 0) > 0)
                    {
                        vt.TrangThai = "Đã xuất kho";
                    }
                    else
                    {
                        vt.TrangThai = "Đã trả kho";
                    }
                }
                // Nếu trạng thái không phải "Đã trả kho" hoặc "Đã xuất kho", xác định lại dựa trên số lượng
                else if (vt.TrangThai != "Đã trả kho" && vt.TrangThai != "Đã xuất kho")
                {
                    if ((vt.SL ?? 0) > 0)
                    {
                        vt.TrangThai = "Đã xuất kho";
                    }
                    else
                    {
                        vt.TrangThai = "Đã trả kho";
                    }
                }
            }

            var viewModel = new ChiTietVatTuDuanViewModel
            {
                Duan = duan,
                VatTuList = filteredVatTu,
                NguoiNhanSummaries = nguoiNhanSummaries,
                SelectedMaNguoiNhan = maNguoiNhan,
                SelectedTenNguoiNhan = selectedTenNguoi,
                SearchKeyword = keyword
            };

            return View(viewModel);
        }
    }
}

