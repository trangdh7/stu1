$(document).ready(function () {
    // Gọi hàm showVTYeucau với mã yêu cầu của hàng đầu tiên khi trang được tải
    const firstRow = $('.table tbody tr').first(); // Lấy hàng đầu tiên trong bảng
    if (firstRow.length > 0) {
        const MaYeucau = firstRow.find('td').eq(2).text().trim(); // Lấy mã yêu cầu từ cột đầu tiên
        showVTYeucau(MaYeucau); // Gọi hàm hiển thị thiết bị
    }
});

// Hàm hiển thị thiết bị theo mã yêu cầu
function showVTYeucau(MaYeucau) {
    console.log("Mã yêu cầu được chọn:", MaYeucau); // Kiểm tra mã yêu cầu

    // Lấy area từ đường dẫn hiện tại
    const pathSegments = window.location.pathname.split('/');
    const area = pathSegments.length > 1 ? pathSegments[1] : ''; // Giả sử area là segment đầu tiên sau dấu '/'

    // Xây dựng URL động dựa trên area hiện tại
    const url = `/${area}/Yeucau/GetVTYeucau`;

    $.ajax({
        url: url, // Sử dụng URL động
        method: 'GET',
        data: { MaYeucau: MaYeucau }, // Gửi mã yêu cầu
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
                        <td colspan="8" style="text-align:center;">Không có dữ liệu vật tư.</td>
                    </tr>`
                );
            }

            // Highlight hàng tương ứng trong bảng
            // Giả sử mã yêu cầu nằm trong cột đầu tiên của bảng
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
