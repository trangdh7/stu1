$(document).ready(function () {
    // Tự động load dữ liệu vật tư cho hàng đầu tiên khi trang load
    setTimeout(function() {
        console.log("Đang tìm hàng đầu tiên để load dữ liệu...");
        const firstRow = $('.table tbody tr').first(); 
        console.log("Số hàng tìm thấy:", firstRow.length);
        
        if (firstRow.length > 0) {
            // Lấy mã nhập kho từ link trong cột thứ 2 (td:eq(1))
            const link = firstRow.find('td').eq(1).find('a');
            const Manhapkho = link.length > 0 ? link.text().trim() : firstRow.find('td').eq(1).text().trim();
            console.log("Mã nhập kho từ text:", Manhapkho);
            if (Manhapkho) {
                showVTnhapkho(Manhapkho);
            }
        } else {
            console.log("Không tìm thấy hàng nào trong bảng");
        }
    }, 300); // Tăng thời gian chờ để đảm bảo DOM đã load xong
    
    getThongbaoData();
    setActiveMenu();
    
    // Xử lý click vào hàng
    $(document).on('click', '.clickable-row', function() {
        const MaNhapkho = $(this).data('manhapkho');
        if (MaNhapkho) {
            showVTnhapkho(MaNhapkho);
        }
    });
});

const ROW_HIGHLIGHT_COLOR = "#2d9f3c";
const ROW_HIGHLIGHT_TEXT_COLOR = "#ffffff";

function applyNhapKhoRowHighlight($row) {
    const $rows = $('.table tbody tr');
    $rows.removeClass('highlight');
    $rows.find('td').css({
        backgroundColor: '',
        color: ''
    });
    $rows.find('a').css('color', '');
    $rows.find('i').css('color', '');

    if ($row && $row.length) {
        $row.addClass('highlight');
        $row.find('td').css({
            backgroundColor: ROW_HIGHLIGHT_COLOR,
            color: ROW_HIGHLIGHT_TEXT_COLOR
        });
        $row.find('a').css('color', ROW_HIGHLIGHT_TEXT_COLOR);
        $row.find('i').css('color', ROW_HIGHLIGHT_TEXT_COLOR);
    }
}

let selectedManhapkho = "";

function showVTnhapkho(Manhapkho) {
    if (!Manhapkho || Manhapkho.trim() === '') {
        console.error("Mã nhập kho không hợp lệ:", Manhapkho);
        $('.tablethietbi tbody').html('<tr><td colspan="10" style="text-align:center;">Mã nhập kho không hợp lệ.</td></tr>');
        return;
    }

    selectedManhapkho = Manhapkho;
    console.log("Mã nhập kho được chọn:", Manhapkho); 

    const pathSegments = window.location.pathname.split('/');   
    const area = pathSegments.length > 1 ? pathSegments[1] : ''; // Giả sử area là segment đầu tiên sau dấu '/'

    const url = `/${area}/Yeucau/GetVTPhieunhapkho`;
    console.log("URL gọi API:", url);
    console.log("Tham số:", { MaNhapkho: Manhapkho });

    $.ajax({
        url: url, // Sử dụng URL động
        method: 'GET',
        data: { MaNhapkho: Manhapkho }, // Sửa tên tham số để khớp với controller
        success: function (response) {
            console.log("Dữ liệu nhận được từ API:", response); 
            
            // Xử lý response mới (có items) hoặc cũ (mảng trực tiếp)
            let data = response.items || response;
            let tenNguoiYeuCau = response.tenNguoiYeuCau || '';
            
            console.log("Số lượng vật tư:", data ? (Array.isArray(data) ? data.length : 0) : 0);
            
            // Hiển thị header text cho tất cả areas
            if (Manhapkho && tenNguoiYeuCau) {
                $('#phieunhapkho-header-text').text(`Phiếu nhập kho ${Manhapkho} của ${tenNguoiYeuCau}`);
                $('#phieunhapkho-header').show();
            } else if (Manhapkho) {
                $('#phieunhapkho-header-text').text(`Phiếu nhập kho ${Manhapkho}`);
                $('#phieunhapkho-header').show();
            } else {
                $('#phieunhapkho-header').hide();
            }

            $('.tablethietbi tbody').empty();

            if (data && Array.isArray(data) && data.length > 0) {
                let STT = 1;
                data.forEach(function (item) {
                    console.log("Xử lý vật tư:", item);
                    const pathSegments = window.location.pathname.split('/');
                    const area = pathSegments.length > 1 ? pathSegments[1] : '';
                    // Hỗ trợ cả camelCase và PascalCase
                    const makho = item.makho || item.Makho || item.makHo || '';
                    const tenSanpham = item.tenSanpham || item.TenSanpham || item.tenSanPham || '';
                    const maSanpham = item.maSanpham || item.MaSanpham || item.maSanPham || '';
                    const hangSX = item.hangSX || item.HangSX || item.hangSx || '';
                    const nhaCC = item.nhaCC || item.NhaCC || item.nhaCc || '';
                    const ngayNhapkho = item.ngayNhapkho || item.NgayNhapkho || item.ngayNhapKho || '';
                    const sl = item.sl || item.SL || item.sL || 0;
                    const donVi = item.donVi || item.DonVi || item.donVi || '';
                    const trangThai = item.trangThai || item.TrangThai || item.trangThai || '';
                    
                    const normalizedStatus = (trangThai || '').toString().toLowerCase();
                    const canPrint = normalizedStatus.includes('đã nhập kho') || normalizedStatus.includes('da nhap kho') || normalizedStatus.includes('hoàn thành') || normalizedStatus.includes('hoan thanh');
                    const printButton = canPrint && makho && makho !== 'Không xác định'
                        ? `<button class="btn-print-makho" 
                                    data-makho="${makho}" 
                                    data-tensp="${tenSanpham}" 
                                    data-masp="${maSanpham}" 
                                    data-hangsx="${hangSX}" 
                                    data-nhacc="${nhaCC}"
                                    data-ngay="${ngayNhapkho}"
                                    style="background-color: #28a745; color: white; border: none; padding: 5px 10px; border-radius: 4px; cursor: pointer;">
                                🖨️ In mã kho
                            </button>`
                        : '';

                    let row = `<tr>
                        <td>${STT++}</td>
                        <td>${tenSanpham}</td>
                        <td>${maSanpham}</td>
                        <td>
                            <span class="makho-link" 
                                  data-makho="${makho}" 
                                  data-tensp="${tenSanpham}" 
                                  data-masp="${maSanpham}" 
                                  data-hangsx="${hangSX}" 
                                  data-nhacc="${nhaCC}"
                                  data-ngay="${ngayNhapkho}"
                                  style="cursor: pointer; color: #007bff; text-decoration: underline;">
                                ${makho}
                            </span>
                        </td>
                        <td>${hangSX || 'Không xác định'}</td>
                        <td>${nhaCC || 'Không xác định'}</td>
                        <td>${sl}</td>
                        <td>${donVi || 'Không xác định'}</td>
                        <td>${trangThai || 'Chưa xác định'}</td>
                    </tr>`;
                    $('.tablethietbi tbody').append(row);
                });
                
                // Thêm event handler cho nút in mã kho
                $('.btn-print-makho').on('click', function(e) {
                    e.stopPropagation();
                    const makho = $(this).data('makho');
                    const tenSanpham = $(this).data('tensp');
                    const maSanpham = $(this).data('masp');
                    const hangSX = $(this).data('hangsx');
                    const nhaCC = $(this).data('nhacc');
                    const ngayNhapkho = $(this).data('ngay');
                    
                    if (makho && makho !== 'Không xác định') {
                        const pathSegments = window.location.pathname.split('/');
                        const area = pathSegments.length > 1 ? pathSegments[1] : '';
                        const url = `/${area}/Home/InTem?makho=${encodeURIComponent(makho)}&tenSanpham=${encodeURIComponent(tenSanpham)}&maSanpham=${encodeURIComponent(maSanpham)}&hangSX=${encodeURIComponent(hangSX)}&nhaCC=${encodeURIComponent(nhaCC)}&ngayNhapkho=${encodeURIComponent(ngayNhapkho)}`;
                        window.open(url, '_blank');
                    } else {
                        alert('Không có mã kho để in!');
                    }
                });
                
                // Thêm event handler cho click vào mã kho (chỉ hiển thị thông tin, không mở phiếu in)
                $('.makho-link').on('click', function(e) {
                    e.stopPropagation();
                    const makho = $(this).data('makho');
                    const tenSanpham = $(this).data('tensp');
                    const maSanpham = $(this).data('masp');
                    const hangSX = $(this).data('hangsx');
                    const nhaCC = $(this).data('nhacc');
                    
                    if (makho && makho !== 'Không xác định') {
                        // Hiển thị thông tin vật tư trong modal hoặc alert
                        const info = `Thông tin vật tư:\n\n` +
                                   `Mã kho: ${makho}\n` +
                                   `Tên sản phẩm: ${tenSanpham}\n` +
                                   `Mã sản phẩm: ${maSanpham}\n` +
                                   `Hãng SX: ${hangSX}\n` +
                                   `Nhà cung cấp: ${nhaCC}\n\n` +
                                   `Sử dụng nút "In mã kho" để in tem.`;
                        alert(info);
                    }
                });
            } else {
                // Hiển thị thông báo nếu không có dữ liệu
                $('.tablethietbi tbody').append(
                    `<tr>
                        <td colspan="9" style="text-align:center;">Không có dữ liệu vật tư.</td>
                    </tr>`
                );
            }

            let $rowToHighlight = $();
            let trangThaiPhieu = "";
            $('.table tbody tr').each(function () {
                // Tìm link trong cột thứ 2 (td:eq(1)) và so sánh text
                const link = $(this).find('td').eq(1).find('a');
                const linkText = link.length > 0 ? link.text().trim() : $(this).find('td').eq(1).text().trim();
                if (linkText === Manhapkho) {
                    $rowToHighlight = $(this);
                    // Lấy trạng thái từ cột trạng thái (cột thứ 7, index 6)
                    const trangThaiCell = $(this).find('td').eq(6);
                    trangThaiPhieu = trangThaiCell.find('span').text().trim() || trangThaiCell.text().trim();
                    return false;
                }
            });
            applyNhapKhoRowHighlight($rowToHighlight);
            
            // Hiển thị/ẩn nút "Nhập kho" dựa trên trạng thái
            if (trangThaiPhieu === "Chờ nhập kho") {
                $('#btn-nhapkho').show();
            } else {
                $('#btn-nhapkho').hide();
            }
        },
        error: function (xhr, status, error) {
            console.error("Lỗi khi gọi API:", error);
            console.error("Status:", status);
            console.error("Response:", xhr.responseText);
            console.error("Status Code:", xhr.status);
            
            $('.tablethietbi tbody').html(
                `<tr>
                    <td colspan="10" style="text-align:center; color: red;">
                        Lỗi khi tải dữ liệu vật tư. Vui lòng thử lại.<br>
                        <small>Mã lỗi: ${xhr.status} - ${error}</small>
                    </td>
                </tr>`
            );
            
            // Không hiển thị alert để tránh làm phiền người dùng
            // alert("Không thể lấy dữ liệu vật tư. Lỗi: " + error); 
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

// Xử lý nút "Nhập kho"
$(document).on('click', '#btn-nhapkho', function() {
    if (!selectedManhapkho) {
        alert("Vui lòng chọn phiếu nhập kho trước.");
        return;
    }
    
    if (!confirm("Bạn có chắc chắn muốn duyệt nhập kho cho phiếu " + selectedManhapkho + "?")) {
        return;
    }
    
    const pathSegments = window.location.pathname.split('/');
    const area = pathSegments.length > 1 ? pathSegments[1] : '';
    const url = `/${area}/Yeucau/Xuliphieunhapkho`;
    
    // Tạo form để submit
    const form = $('<form>', {
        method: 'POST',
        action: url
    });
    
    form.append($('<input>', {
        type: 'hidden',
        name: 'MaNhapkho',
        value: selectedManhapkho
    }));
    
    form.append($('<input>', {
        type: 'hidden',
        name: 'action',
        value: 'approve'
    }));
    
    // Thêm token chống CSRF nếu có
    const token = $('input[name="__RequestVerificationToken"]').val();
    if (token) {
        form.append($('<input>', {
            type: 'hidden',
            name: '__RequestVerificationToken',
            value: token
        }));
    }
    
    $('body').append(form);
    form.submit();
});

// Gọi hàm getThongbaoData khi trang được tải
$(document).ready(function () {
    getThongbaoData();
    // Ẩn nút "Nhập kho" ban đầu
    $('#btn-nhapkho').hide();
});