$(document).ready(function () {
    const firstRow = $('.table tbody tr').first();
    if (firstRow.length > 0) {
        const Mamuahang = firstRow.find('td').eq(1).text().trim();
        showVTmuahang(Mamuahang);
    }
});

$('#submitPhieumuahang').click(function () {
    if (!selectedMamuahang) {
        alert("Vui lòng chọn mã mua hàng trước khi gửi.");
        return;
    }

    const vtmuahangData = [];
    $('.tablethietbi tbody tr').each(function () {
        const cells = $(this).find('td');

        // Kiểm tra dòng có đủ dữ liệu không (ví dụ, có ít nhất 2 ô)
        if (cells.length >= 2) {
            const MaMuahang = cells.eq(1).text().trim();
            const DonGia = parseFloat($(this).find('.DonGia input').val()) || 0;
            const SL = parseFloat($(this).find('.DonGia input').data('sl')) || 0;
            const ThanhTien = SL * DonGia;

            // Kiểm tra nếu giá trị hợp lệ
            if (MaMuahang && DonGia > 0 && SL > 0) {
                console.log("Mã mua hàng:", MaMuahang, "Đơn giá:", DonGia, "Số lượng:", SL, "Thành tiền:", ThanhTien);

                vtmuahangData.push({
                    MaMuahang: selectedMamuahang,
                    DonGia: DonGia,
                    ThanhTien: ThanhTien
                });
            }
        }
    });

    const Phieumuahangviewmodel = {
        MaMuahang: selectedMamuahang,
        Vtphieumuahang: vtmuahangData
    };

    // Kiểm tra dữ liệu gửi đi
    console.log("Dữ liệu gửi đi:", Phieumuahangviewmodel);

    fetch("/Giamdoc/Yeucau/ThemPhieumuahangSQL", {
        method: "POST",
        body: JSON.stringify(Phieumuahangviewmodel),
        headers: {
            "Content-Type": "application/json"
        }
    })
        .then(response => response.json())
        .then(data => {
            alert("Gửi dữ liệu thành công!");
            location.reload();  // Tải lại trang sau khi gửi thành công
        })
        .catch(error => {
            console.error("Lỗi:", error);
            alert("Gửi dữ liệu thất bại.");
        });
});



let selectedMamuahang = "";

function showVTmuahang(Mamuahang) {
    selectedMamuahang = Mamuahang;
    console.log("Mã mua hàng được chọn:", Mamuahang);

    const pathSegments = window.location.pathname.split('/');
    const area = pathSegments.length > 1 ? pathSegments[1] : '';
    const url = `/${area}/Yeucau/GetVTPhieumuahang`;

    $.ajax({
        url: url,
        method: 'GET',
        data: { Mamuahang: Mamuahang },
        success: function (data) {
            console.log(data);

            $('.tablethietbi tbody').empty();
            $('.table tbody tr').removeClass('highlight');

            if (data && data.length > 0) {
                let STT = 1;
                data.forEach(function (item) {
                    let row = `
                    <tr>
                        <td>${STT++}</td>
                        <td>${item.tenSanpham || 'Không xác định'}</td>
                        <td>${item.maSanpham || 'Không xác định'}</td>
                        <td>${item.makho || 'Không xác định'}</td>
                        <td>${item.hangSX || 'Không xác định'}</td>
                        <td>${item.nhaCC || 'Không xác định'}</td>
                        <td>${item.sl}</td>
                        <td>${item.donVi || 'Không xác định'}</td>
                        <td>
                            ${item.donGia != null
                            ? `<span class="DonGia">${item.donGia.toLocaleString('vi-VN')}</span>` // Hiển thị dạng text nếu không null
                            : `<span class="DonGia"><input type="text" placeholder="Nhập giá" class="form-control" data-sl="${item.sl}" /></span>` // Hiển thị input nếu null
                        }
                        </td>
                        <td>
                            ${item.thanhTien != null
                            ? `<span class="ThanhTien">${item.thanhTien.toLocaleString('vi-VN')}</span>`
                            : `<span class="ThanhTien">0</span>`
                        }
                        </td>                            
                        <td>${item.trangThai}</td>
                    </tr>`;
                    $('.tablethietbi tbody').append(row);
                });

                // Thêm hàng tổng tiền vào cuối bảng ngay lập tức
                $('.tablethietbi tbody').append(`
                    <tr class="tong-tien-row">
                        <td colspan="8" style="text-align:center; font-weight:bold;">Tổng tiền:</td>
                        <td class="tong-tien" colspan="3" style="font-weight:bold;">0</td>
                    </tr>
                `);
                updateTongTien();

                // Gắn sự kiện theo dõi cho toàn bộ bảng
                attachEventHandlers();
            } else {
                $('.tablethietbi tbody').append(`
                    <tr>
                        <td colspan="11" style="text-align:center;">Không có dữ liệu vật tư.</td>
                    </tr>
                `);
            }

            // Highlight dòng được chọn
            $('.table tbody tr').each(function () {
                if ($(this).find('td').eq(1).text().trim() === Mamuahang) {
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

// Hàm gắn sự kiện xử lý tự động
function attachEventHandlers() {
    $('.tablethietbi tbody').on('input', '.DonGia input', function () {
        const $row = $(this).closest('tr');
        const sl = parseInt($(this).data('sl')) || 0; // Đảm bảo lấy số nguyên
        const donGia = parseInt($(this).val()) || 0; // Đảm bảo lấy số nguyên

        console.log("Số lượng:", sl, "Đơn giá:", donGia); // Kiểm tra giá trị gốc

        const thanhTien = sl * donGia; // Tính toán
        console.log("Thành tiền (tính toán):", thanhTien); // Kiểm tra kết quả tính toán

        // Cập nhật cột Thành Tiền
        $row.find('.ThanhTien').text(thanhTien.toLocaleString('vi-VN'));

        // Cập nhật Tổng Tiền
        updateTongTien();
    });
}

function updateTongTien() {
    let tongTien = 0;

    $('.tablethietbi .ThanhTien').each(function () {
        const thanhTienText = $(this).text().trim();
        const thanhTien = parseInt(thanhTienText.replace(/[^\d]/g, '')) || 0;

        tongTien += thanhTien;
    });

    // Cập nhật tổng tiền trong bảng
    $('.tong-tien').text(tongTien.toLocaleString('vi-VN', { style: 'currency', currency: 'VND' }));
}
