$(document).ready(function () {
    // Gọi hàm showVTYeucau với mã yêu cầu của hàng đầu tiên khi trang được tải
    const firstRow = $('.table tbody tr').first(); // Lấy hàng đầu tiên trong bảng
    if (firstRow.length > 0) {
        const MaYeucau = firstRow.find('td').eq(2).text().trim(); // Lấy mã yêu cầu từ cột đầu tiên
        showVTYeucau(MaYeucau); // Gọi hàm hiển thị thiết bị
    }

    // Gọi hàm thông báo ngay khi trang load
    getThongbaoData();
    
    // Gọi lại sau 1 giây để đảm bảo DOM đã sẵn sàng
    setTimeout(function() {
        getThongbaoData();
    }, 1000);
    
    setActiveMenu();
});

function showVTYeucau(MaYeucau) {
    console.log("Mã yêu cầu được chọn:", MaYeucau); // Kiểm tra mã yêu cầu

    const pathSegments = window.location.pathname.split('/');
    const area = pathSegments.length > 1 ? pathSegments[1] : ''; 

    const url = `/${area}/Yeucau/GetVTYeucau`;

    // Đảm bảo bảng chi tiết sản phẩm được hiển thị
    $('.bodyyeucau-thietbi').show();
    $('.tablethietbi').show();

    $.ajax({
        url: url, 
        method: 'GET',
        data: { MaYeucau: MaYeucau }, 
        success: function (data) {
            console.log(data); // Kiểm tra dữ liệu nhận được

            $('.tablethietbi tbody').empty();

            $('.table tbody tr').removeClass('highlight');

            if (data && data.length > 0) {
                let STT = 1;
                data.forEach(function (item) {
                    // Tạo một dòng mới
                    let row = `<tr>
                        <td>${STT++}</td>
                        <td>${item.tenSanpham || 'Không xác định'}</td>
                        <td>${item.maSanpham || 'Không xác định'}</td>
                        <td>${item.ycMakho || 'Không xác định'}</td>
                        <td>${item.hangSX || 'Không xác định'}</td>
                        <td>${item.nhaCC || 'Không xác định'}</td>
                        <td>${item.sl}</td>
                        <td>${item.donVi || 'Không xác định'}</td>
                        <td>${item.trangThai}</td>
                    </tr>`;
                    $('.tablethietbi tbody').append(row);
                });
            } else {
                // Hiển thị thông báo nếu không có dữ liệu
                $('.tablethietbi tbody').append(
                    `<tr>
                        <td colspan="9" style="text-align:center;">Không có dữ liệu vật tư.</td>
                    </tr>`
                );
            }

            // Highlight hàng tương ứng trong bảng
            $('.table tbody tr').each(function () {
                if ($(this).find('td').eq(2).text().trim() === MaYeucau) { // So sánh với cột thứ hai
                    $(this).addClass('highlight'); // Thêm class highlight cho hàng tương ứng
                }
            });
        },
        error: function (xhr, status, error) {
            console.error("Lỗi:", error); // Ghi lỗi vào console
            alert("Không thể lấy dữ liệu vật tư. Lỗi: " + error); // Thông báo lỗi
        }
    });
}

// Hàm lấy dữ liệu thông báo
function getThongbaoData() {
    const pathSegments = window.location.pathname.split('/');
    const area = pathSegments.length > 1 ? pathSegments[1] : ''; 
    const url = area ? `/${area}/Yeucau/GetDulieuThongbao` : '/Yeucau/GetDulieuThongbao';

    $.ajax({
        url: url,
        method: 'GET',
        success: function (data) {
            console.log("Dữ liệu thông báo yêu cầu là:", data);

            // Cập nhật thông báo mua hàng
            console.log("Thông báo mua hàng count:", data.thongbaomuahangcount);
            if (data.thongbaomuahangcount > 0) {
                $('.menu-muahang .badge').addClass('show');
                $('.menu-muahang .notification').text(data.thongbaomuahangcount);
                console.log("Đã hiển thị badge mua hàng với số:", data.thongbaomuahangcount);
            } else {
                $('.menu-muahang .badge').removeClass('show');
            }

            // Cập nhật thông báo phiếu xuất kho
            console.log("Thông báo xuất kho count:", data.thongbaoxuatkhocount);
            if (data.thongbaoxuatkhocount > 0) {
                $('.menu-xuatkho .badge').addClass('show');
                $('.menu-xuatkho .notification').text(data.thongbaoxuatkhocount);
                console.log("Đã hiển thị badge xuất kho với số:", data.thongbaoxuatkhocount);
            } else {
                $('.menu-xuatkho .badge').removeClass('show');
            }

            // Cập nhật thông báo phiếu nhập kho
            console.log("Thông báo nhập kho count:", data.thongbaonhapkhocount);
            if (data.thongbaonhapkhocount > 0) {
                $('.menu-nhapkho .badge').addClass('show');
                $('.menu-nhapkho .notification').text(data.thongbaonhapkhocount);
                console.log("Đã hiển thị badge nhập kho với số:", data.thongbaonhapkhocount);
            } else {
                $('.menu-nhapkho .badge').removeClass('show');
            }

            // Cập nhật thông báo yêu cầu
            console.log("Thông báo yêu cầu count:", data.thongbaoyeucaucount);
            var badgeElement = $('.menu-yeucau .badge');
            var notificationElement = $('.menu-yeucau .notification');
            console.log("Badge element found:", badgeElement.length);
            console.log("Notification element found:", notificationElement.length);
            
            if (data.thongbaoyeucaucount > 0) {
                if (badgeElement.length > 0) {
                    badgeElement.addClass('show');
                    notificationElement.text(data.thongbaoyeucaucount);
                    console.log("Đã hiển thị badge yêu cầu với số:", data.thongbaoyeucaucount);
                } else {
                    console.error("Không tìm thấy badge element!");
                }
            } else {
                badgeElement.removeClass('show');
                console.log("Đã ẩn badge yêu cầu");
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

function setActiveMenu() {
    const pathSegments = window.location.pathname.split('/');
    const currentPage = pathSegments[pathSegments.length - 1]; // Lấy tên trang hiện tại từ URL

    // Loại bỏ lớp active khỏi tất cả các liên kết menu
    $('.menu-kho a').removeClass('active');

    // So sánh và thêm lớp active vào liên kết tương ứng
    if (currentPage === 'Yeucau') {
        $('.menu-yeucau a').addClass('active');
        $('.menu-yeucau').addClass('active-bg');
    } else if (currentPage === 'Phieumuahang') {
        $('.menu-muahang a').addClass('active');
        $('.menu-muahang').addClass('active-bg');
    } else if (currentPage === 'Phieuxuatkho') {
        $('.menu-xuatkho a').addClass('active');
        $('.menu-xuatkho').addClass('active-bg');
    } else if (currentPage === 'Phieunhapkho') {
        $('.menu-nhapkho a').addClass('active');
        $('.menu-nhapkho').addClass('active-bg');
    }
}

// Gọi hàm getThongbaoData khi trang được tải
$(document).ready(function () {
    getThongbaoData();
});

