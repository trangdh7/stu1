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

            if (tongthongbao > 0) {
                $('.Danhsachyeucau .badge-trangchu').addClass('show');
                $('.Danhsachyeucau .notification').text(tongthongbao);
            } else {
                $('.Danhsachyeucau .badge-trangchu').removeClass('show');
            }
        },
        error: function (xhr, status, error) {
            console.error("Lỗi lấy thông báo:", error);
            alert("Không thể lấy dữ liệu thông báo. Lỗi: " + error);
        }
    });
}