function getThongbaoDatalayout() {
    // Danh sách các area hợp lệ
    const validAreas = ['NhanvienKho', 'NhanvienKetoan', 'NhanvienMuahang', 'NhanvienKythuat', 
                        'TruongBPKho', 'TruongBPKetoan', 'TruongBPMuahang', 'TruongBPKythuat',
                        'Giamdoc', 'Admin'];
    
    // Lấy area từ data attribute của body
    let area = $('body').data('area') || '';
    
    // Nếu area không hợp lệ, thử lấy từ URL
    if (!area || !validAreas.includes(area)) {
        const pathSegments = window.location.pathname.split('/').filter(s => s);
        // Tìm segment đầu tiên là area hợp lệ
        for (let segment of pathSegments) {
            if (validAreas.includes(segment)) {
                area = segment;
                break;
            }
        }
    }
    
    // Nếu vẫn không có, thử lấy từ các link trong sidebar
    if (!area || !validAreas.includes(area)) {
        $('.sidebar a[href*="/"]').each(function() {
            const href = $(this).attr('href');
            if (href) {
                const match = href.match(/\/([^\/]+)\//);
                if (match && match[1] && validAreas.includes(match[1])) {
                    area = match[1];
                    return false; // break loop
                }
            }
        });
    }
    
    const url = area && validAreas.includes(area) ? `/${area}/Yeucau/GetDulieuThongbaolayout` : '/Yeucau/GetDulieuThongbaolayout';

    $.ajax({
        url: url,
        method: 'GET',
        success: function (data) {
            console.log("Dữ liệu thông báo của layout là:", data);

            // Tính tổng thông báo
            const tongthongbao =
                (data.thongbaoyeucaucount || 0) +
                (data.thongbaomuahangcount || 0) +
                (data.thongbaonhapkhocount || 0) +
                (data.thongbaoxuatkhocount || 0);

            if (tongthongbao > 0) {
                $('.Yeucau .badge').addClass('show');
                $('.Yeucau .notification').text(tongthongbao);
            } else {
                $('.Yeucau .badge').removeClass('show');
            }

            // Thông báo xác nhận nhận hàng
            if (data.thongbaoxacnhannhanhangcount > 0) {
                $('.menu-xacnhannhanhang .badge').addClass('show');
                $('.menu-xacnhannhanhang .notification').text(data.thongbaoxacnhannhanhangcount);
            } else {
                $('.menu-xacnhannhanhang .badge').removeClass('show');
            }
        },
        error: function (xhr, status, error) {
            console.error("Lỗi lấy thông báo:", error);
            alert("Không thể lấy dữ liệu thông báo. Lỗi: " + error);
        }
    });
}