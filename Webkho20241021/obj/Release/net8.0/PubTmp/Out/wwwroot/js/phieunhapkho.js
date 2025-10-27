$(document).ready(function () {
    const firstRow = $('.table tbody tr').first(); 
    if (firstRow.length > 0) {
        const Manhapkho = firstRow.find('td').eq(1).text().trim();
        showVTnhapkho(Manhapkho); 
    }
});

function showVTnhapkho(Manhapkho) {
    console.log("Mã nhập kho được chọn:", Manhapkho); 

    const pathSegments = window.location.pathname.split('/');   
    const area = pathSegments.length > 1 ? pathSegments[1] : ''; // Giả sử area là segment đầu tiên sau dấu '/'

    const url = `/${area}/Yeucau/GetVTPhieunhapkho`;

    $.ajax({
        url: url, // Sử dụng URL động
        method: 'GET',
        data: { Manhapkho: Manhapkho }, // Gửi mã yêu cầu
        success: function (data) {
            console.log(data); // Kiểm tra dữ liệu nhận được

            $('.tablethietbi tbody').empty();

            $('.table tbody tr').removeClass('highlight');

            if (data && data.length > 0) {
                let STT = 1;
                data.forEach(function (item) {
                    let row = `<tr>
                        <td>${STT++}</td>
                        <td>${item.tenSanpham || 'Không xác định'}</td>
                        <td>${item.maSanpham || 'Không xác định'}</td>
                        <td>${item.makho || 'Không xác định'}</td>
                        <td>${item.hangSX || 'Không xác định'}</td>
                        <td>${item.nhaCC || 'Không xác định'}</td>
                        <td>${item.sl}</td>
                        <td>${item.donVi || 'Không xác định'}</td>
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

            $('.table tbody tr').each(function () {
                if ($(this).find('td').eq(1).text().trim() === Manhapkho) { 
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
