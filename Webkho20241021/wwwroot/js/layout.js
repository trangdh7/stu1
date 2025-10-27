function getThongbaoDatalayout() {
    const pathSegments = window.location.pathname.split('/');
    const area = pathSegments.length > 1 ? pathSegments[1] : '';
    const url = area ? `/${area}/Yeucau/GetDulieuThongbaolayout` : '/Yeucau/GetDulieuThongbaolayout';

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
        },
        error: function (xhr, status, error) {
            console.error("Lỗi lấy thông báo:", error);
            alert("Không thể lấy dữ liệu thông báo. Lỗi: " + error);
        }
    });
}