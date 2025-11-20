function getThongbaoDatatrangchu() {
    // Danh sách các area hợp lệ
    const validAreas = ['NhanvienKho', 'NhanvienKetoan', 'NhanvienMuahang', 'NhanvienKythuat', 
                        'TruongBPKho', 'TruongBPKetoan', 'TruongBPMuahang', 'TruongBPKythuat',
                        'Giamdoc', 'Admin', 'QuanLiDuAn'];
    
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
    
    // Nếu vẫn không có, thử lấy từ các link trong trang
    if (!area || !validAreas.includes(area)) {
        $('a[href*="/"]').each(function() {
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
    
    const url = area && validAreas.includes(area) ? `/${area}/Yeucau/GetDulieuThongbaotrangchu` : '/Yeucau/GetDulieuThongbaotrangchu';

    $.ajax({
        url: url,
        method: 'GET',
        success: function (data) {
            console.log("Dữ liệu thông báo của trang chủ là:", data);

            // Tính tổng thông báo
            const tongthongbao =
                (data.thongbaoyeucaucount || 0) +
                (data.thongbaomuahangcount || 0) +
                (data.thongbaonhapkhocount || 0) +
                (data.thongbaoxuatkhocount || 0);

            console.log("Tổng thông báo:", tongthongbao);
            
            var badgeElement = $('.Danhsachyeucau .badge-trangchu');
            var notificationElement = $('.Danhsachyeucau .notification');
            
            console.log("Badge element found:", badgeElement.length);
            console.log("Notification element found:", notificationElement.length);

            if (tongthongbao > 0) {
                if (badgeElement.length > 0) {
                    badgeElement.addClass('show');
                    notificationElement.text(tongthongbao);
                    console.log("Đã hiển thị badge với số:", tongthongbao);
                } else {
                    console.error("Không tìm thấy badge element!");
                }
            } else {
                badgeElement.removeClass('show');
                console.log("Đã ẩn badge");
            }
        },
        error: function (xhr, status, error) {
            console.error("Lỗi lấy thông báo:", error);
            alert("Không thể lấy dữ liệu thông báo. Lỗi: " + error);
        }
    });
}