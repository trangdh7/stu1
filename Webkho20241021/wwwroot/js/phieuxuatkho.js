$(document).ready(function () {
    const firstRow = $('.table tbody tr').first(); 
    if (firstRow.length > 0) {
        const Maxuatkho = firstRow.find('td').eq(1).text().trim();
        showVTxuatkho(Maxuatkho); 
    }
    getThongbaoData();
    setActiveMenu();
});

// Hàm hiển thị thiết bị theo mã yêu cầu
function showVTxuatkho(Maxuatkho) {
    console.log("Mã xuất kho được chọn:", Maxuatkho); // Kiểm tra mã yêu cầu

    const pathSegments = window.location.pathname.split('/');
    const area = pathSegments.length > 1 ? pathSegments[1] : ''; // Giả sử area là segment đầu tiên sau dấu '/'

    // Đồng bộ trạng thái vật tư trước khi hiển thị
    const syncUrl = `/${area}/Yeucau/DongsBoTrangThaiVatTu`;
    const url = `/${area}/Yeucau/GetVTPhieuxuatkho`;

    // Đồng bộ trạng thái vật tư trước khi hiển thị
    $.ajax({
        url: syncUrl,
        method: 'POST',
        data: { MaXuatkho: Maxuatkho },
        success: function (syncResult) {
            console.log("Đồng bộ trạng thái:", syncResult);
            // Đợi một chút để đảm bảo database đã cập nhật
            setTimeout(function() {
                loadVTData(Maxuatkho, url, area);
            }, 100);
        },
        error: function (xhr, status, error) {
            console.error("Lỗi đồng bộ:", error);
            // Nếu đồng bộ thất bại, vẫn tiếp tục hiển thị dữ liệu
            loadVTData(Maxuatkho, url, area);
        }
    });
}

// Hàm load dữ liệu vật tư
function loadVTData(Maxuatkho, url, area) {
    $.ajax({
        url: url,
        method: 'GET',
        data: { MaXuatkho: Maxuatkho }, // Sử dụng đúng tên tham số
        success: function (data) {
            console.log(data); // Kiểm tra dữ liệu nhận được

            $('.tablethietbi tbody').empty();

            $('.table tbody tr').removeClass('highlight');

            if (data && data.length > 0) {
                let STT = 1;
                data.forEach(function (item) {
                    // Xác định màu sắc theo trạng thái
                    let bgColor = '#4caf50'; // Mặc định xanh lá
                    if (item.trangThai === 'Đang chuẩn bị hàng') {
                        bgColor = '#2196f3'; // Xanh dương
                    } else if (item.trangThai === 'Đã xác nhận nhận hàng') {
                        bgColor = '#4caf50'; // Xanh lá
                    } else if (item.trangThai === 'Đã xuất kho') {
                        bgColor = '#4caf50'; // Xanh lá
                    } else if (item.trangThai === 'Hoàn thành') {
                        bgColor = '#4caf50'; // Xanh lá
                    }

                    let row = `<tr>
                        <td>${STT++}</td>
                        <td>${item.tenSanpham || 'Không xác định'}</td>
                        <td>${item.maSanpham || 'Không xác định'}</td>
                        <td>${item.makho || 'Không xác định'}</td>
                        <td>${item.hangSX || 'Không xác định'}</td>
                        <td>${item.nhaCC || 'Không xác định'}</td>
                        <td>${item.sl}</td>
                        <td>${item.donVi || 'Không xác định'}</td>
                        <td><span style="background-color:${bgColor}; color:white; padding:2px 6px; border-radius:3px; font-size:11px;">${item.trangThai || '-'}</span></td>
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

            $('.table tbody tr').each(function () {
                if ($(this).find('td').eq(1).text().trim() === Maxuatkho) { 
                    $(this).addClass('highlight'); 
                }
            });
        },
        error: function (xhr, status, error) {
            console.error("Lỗi:", error); 
            alert("Không thể lấy dữ liệu vật tư. Lỗi: " + error); 
        }
    });
}

function getThongbaoData() {
    const pathSegments = window.location.pathname.split('/');
    const area = pathSegments.length > 1 ? pathSegments[1] : '';
    const url = area ? `/${area}/Yeucau/GetDulieuThongbao` : '/Yeucau/GetDulieuThongbao';

    $.ajax({
        url: url,
        method: 'GET',
        success: function (data) {
            console.log("Dữ liệu thông báo:", data);

            // Cập nhật thông báo mua hàng
            if (data.thongbaomuahangcount > 0) {
                $('.menu-muahang .badge').addClass('show');
                $('.menu-muahang .notification').text(data.thongbaomuahangcount);
            } else {
                $('.menu-muahang .badge').removeClass('show');
            }

            // Cập nhật thông báo phiếu xuất kho
            if (data.thongbaoxuatkhocount > 0) {
                $('.menu-xuatkho .badge').addClass('show');
                $('.menu-xuatkho .notification').text(data.thongbaoxuatkhocount);
            } else {
                $('.menu-xuatkho .badge').removeClass('show');
            }

            // Cập nhật thông báo phiếu nhập kho
            if (data.thongbaonhapkhocount > 0) {
                $('.menu-nhapkho .badge').addClass('show');
                $('.menu-nhapkho .notification').text(data.thongbaonhapkhocount);
            } else {
                $('.menu-nhapkho .badge').removeClass('show');
            }

            // Cập nhật thông báo yêu cầu
            if (data.thongbaoyeucaucount > 0) {
                $('.menu-yeucau .badge').addClass('show');
                $('.menu-yeucau .notification').text(data.thongbaoyeucaucount);
            } else {
                $('.menu-yeucau .badge').removeClass('show');
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