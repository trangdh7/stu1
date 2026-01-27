using Webkho_20241021.Models;

namespace Webkho_20241021.Services
{
    /// <summary>
    /// Service để xử lý logic thông báo
    /// </summary>
    public class ThongbaoService
    {
        private readonly ApplicationDbContext _context;

        public ThongbaoService(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Lấy dữ liệu thông báo cho người dùng
        /// </summary>
        public object GetThongBao(string chucVu, string boPhan, string maNv)
        {
            return new
            {
                thongbaomuahangcount = GetThongBaoMuaHang(boPhan, chucVu),
                thongbaoxuatkhocount = GetThongBaoXuatKho(boPhan),
                thongbaonhapkhocount = GetThongBaoNhapKho(boPhan),
                thongbaoyeucaucount = GetThongBaoYeucau(boPhan, maNv)
            };
        }

        /// <summary>
        /// Đếm thông báo mua hàng
        /// </summary>
        private int GetThongBaoMuaHang(string boPhan, string chucVu)
        {
            if (boPhan == "BP mua hàng")
            {
                return _context.phieumuahang.Count(p => p.TrangThai == "Đang chờ báo giá");
            }
            else if (boPhan == "BP kế toán")
            {
                return _context.phieumuahang.Count(p => p.TrangThai == "Chờ thanh toán");
            }
            else if (boPhan == "BP kỹ thuật" && chucVu == "Giám đốc")
            {
                return _context.phieumuahang.Count(p => p.TrangThai == "Đã báo giá");
            }

            return 0;
        }

        /// <summary>
        /// Đếm thông báo xuất kho
        /// </summary>
        private int GetThongBaoXuatKho(string boPhan)
        {
            if (boPhan == "BP kho")
            {
                return _context.phieuxuatkho.Count(p => 
                    p.TrangThai != "Hoàn thành");
            }

            return 0;
        }

        /// <summary>
        /// Đếm thông báo nhập kho
        /// </summary>
        private int GetThongBaoNhapKho(string boPhan)
        {
            if (boPhan == "BP kho")
            {
                return _context.phieunhapkho.Count(p => 
                    p.TrangThai == "Chờ nhập kho" || 
                    p.TrangThai == "Sẵn sàng nhập kho");
            }

            return 0;
        }

        /// <summary>
        /// Đếm thông báo yêu cầu
        /// </summary>
        private int GetThongBaoYeucau(string boPhan, string maNv)
        {
            if (boPhan == "BP kho")
            {
                // Đếm phiếu xuất kho đang chờ kho xác nhận và xử lý
                var phieuXuatChoXuLy = _context.phieuxuatkho
                    .Where(p => (string.IsNullOrEmpty(p.MaYeucau) || !p.MaYeucau.StartsWith("NHAPKHO_")))
                    .Count(p => p.TrangThai == "Chờ xác nhận"
                             || p.TrangThai == "Đang chuẩn bị hàng"
                             || p.TrangThai == "Chờ người yêu cầu xác nhận");

                // Đếm yêu cầu chờ duyệt kho
                var yeucauChoDuyet = _context.yeucau
                    .Count(y => y.TrangThai == "Chờ Trưởng BP kho duyệt" 
                             || y.TrangThai == "Chờ Trưởng BP-BP kho duyệt"
                             || y.TrangThai == "Chờ Trưởng Phòng bộ phận BP kho duyệt");

                // Đếm vật tư chờ xuất kho (trạng thái "Đã duyệt" nhưng chưa có phiếu xuất)
                    int vtChoXuat = 0;
                try
                {
                    vtChoXuat = _context.vtyeucau
                        .Count(v => v.TrangThai == "Đã duyệt" 
                                 && !_context.vtphieuxuatkho.Any(vt => vt.MaYeucau == v.VTMaYeucau && vt.MaSanpham == v.MaSanpham));
                }
                catch
                {
                    // Nếu bảng vtphieuxuatkho không tồn tại, chỉ đếm các yêu cầu đã duyệt
                    vtChoXuat = _context.vtyeucau
                        .Count(v => v.TrangThai == "Đã duyệt");
                }

                return phieuXuatChoXuLy + yeucauChoDuyet + vtChoXuat;
            }
            else
            {
                // Các bộ phận khác: đếm yêu cầu chờ duyệt
                var maduanquanli = _context.duans
                    .Where(d => d.MaNguoiQLDA == maNv)
                    .Select(d => d.MaDuan)
                    .ToList();

                int qldaYeucauCount = _context.yeucau
                    .Count(p => p.TrangThai == "Chờ quản lý dự án duyệt" && maduanquanli.Contains(p.YCMaDuan));

                int duyetYeucauCount = _context.yeucau
                    .Count(p => p.TrangThai == ("Chờ Trưởng Phòng bộ phận " + boPhan + " duyệt")
                             || p.TrangThai == ("Chờ Trưởng BP-" + boPhan + " duyệt"));

                return duyetYeucauCount + qldaYeucauCount;
            }
        }

    }
}

