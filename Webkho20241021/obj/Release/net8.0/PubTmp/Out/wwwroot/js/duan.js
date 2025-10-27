$(document).ready(function () {
    // Gọi hàm showVTDuan với mã yêu cầu của hàng đầu tiên khi trang được tải
    const firstRow = $('.table tbody tr').first(); // Lấy hàng đầu tiên trong bảng
    if (firstRow.length > 0) {
        const MaDuan = firstRow.find('td').eq(1).text().trim(); 
        showVTDuan(Maduan); 
    }
});

// Hàm hiển thị thiết bị theo mã yêu cầu
function showVTDuan(MaDuan) {
    console.log("Mã yêu cầu được chọn:", MaDuan); // Kiểm tra mã yêu cầu

    const pathSegments = window.location.pathname.split('/');
    const area = pathSegments.length > 1 ? pathSegments[1] : ''; // Giả sử area là segment đầu tiên sau dấu '/'

    // Xây dựng URL động dựa trên area hiện tại
    const url = `/${area}/Yeucau/GetVTYeucau`;

    // Gửi yêu cầu AJAX để lấy dữ liệu vật tư tương ứng
    $.ajax({
        url: url,
        method: 'GET',
        data: { MaDuan: MaDuan }, // Gửi mã yêu cầu
        success: function (data) {
            console.log(data); 
            $('.tablethietbi tbody').empty();

            $('.table tbody tr').removeClass('highlight');

            if (data && data.length > 0) {
                data.forEach(function (item) {
                    // Tạo một dòng mới
                    let row = `<tr>
                        <td>${item.tenSanpham || 'Không xác định'}</td>
                        <td>${item.maSanpham || 'Không xác định'}</td>
                        <td>${item.daMakho || 'Không xác định'}</td>
                        <td>${item.hangSX || 'Không xác định'}</td>
                        <td>${item.nhaCC || 'Không xác định'}</td>
                        <td>${item.sl}</td>
                        <td>${item.donVi || 'Không xác định'}</td>
                        <td>${item.ngayNhapkho || 'Không xác định'}</td>
                        <td>${item.ngayBaohanh || 'Không xác định'}</td>
                        <td>${item.thoiGianBH || 'Không xác định'}</td>
                        <td>${item.trangthai}</td>
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
                if ($(this).find('td').eq(1).text().trim() === MaDuan) { // So sánh với cột thứ hai
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

function updateMaNguoiQuanLi() {
    const nguoiQuanLiSelect = document.getElementById("nguoiquanli");
    const maNguoiQuanLiSelect = document.getElementById("manguoiquanli");

    const selectedOption = nguoiQuanLiSelect.options[nguoiQuanLiSelect.selectedIndex];
    const maNguoiQuanLi = selectedOption.getAttribute("data-manguoiquanli");

    // Xóa các option hiện tại trong mã người quản lý
    maNguoiQuanLiSelect.innerHTML = "";

    if (maNguoiQuanLi) {
        // Thêm option mới với mã người quản lý tương ứng
        const option = document.createElement("option");
        option.value = maNguoiQuanLi;
        option.text = maNguoiQuanLi;
        maNguoiQuanLiSelect.appendChild(option);
    }
}
