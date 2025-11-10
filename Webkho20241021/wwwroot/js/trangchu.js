function getThongbaoDatatrangchu() {
    const pathSegments = window.location.pathname.split('/');
    const area = pathSegments.length > 1 ? pathSegments[1] : '';
    const url = area ? `/${area}/Yeucau/GetDulieuThongbaotrangchu` : '/Yeucau/GetDulieuThongbaotrangchu';

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