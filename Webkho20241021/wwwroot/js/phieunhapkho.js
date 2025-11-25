$(document).ready(function () {
    // Tự động load dữ liệu vật tư cho hàng đầu tiên khi trang load
    setTimeout(function() {
        console.log("Đang tìm hàng đầu tiên để load dữ liệu...");
        const firstRow = $('.table tbody tr').first(); 
        console.log("Số hàng tìm thấy:", firstRow.length);
        
        if (firstRow.length > 0) {
            const ManhapkhoLink = firstRow.find('.manhapkho-link');
            console.log("Tìm thấy link:", ManhapkhoLink.length);
            
            if (ManhapkhoLink.length > 0) {
                const Manhapkho = ManhapkhoLink.data('manhapkho') || ManhapkhoLink.text().trim();
                console.log("Mã nhập kho từ link:", Manhapkho);
                if (Manhapkho) {
                    showVTnhapkho(Manhapkho); 
                }
            } else {
                // Nếu không tìm thấy link, thử lấy từ cột thứ 2 (cột mã nhập kho)
                const manhapkhoText = firstRow.find('td').eq(1).text().trim();
                console.log("Mã nhập kho từ text:", manhapkhoText);
                if (manhapkhoText) {
                    showVTnhapkho(manhapkhoText);
                }
            }
        } else {
            console.log("Không tìm thấy hàng nào trong bảng");
        }
    }, 300); // Tăng thời gian chờ để đảm bảo DOM đã load xong
    
    getThongbaoData();
    setActiveMenu();
    
    // Thêm event handler cho click vào mã nhập kho - load dữ liệu và highlight row
    $(document).on('click', '.manhapkho-link', function(e) {
        e.preventDefault(); // Ngăn navigation ngay lập tức để load dữ liệu trước
        const Manhapkho = $(this).data('manhapkho') || $(this).text().trim();
        
        if (Manhapkho) {
            // Load dữ liệu vật tư
            showVTnhapkho(Manhapkho);
        }
    });
    
    // Thêm event handler cho double-click để xem chi tiết
    $(document).on('dblclick', '.manhapkho-link', function(e) {
        const href = $(this).attr('href');
        if (href) {
            window.location.href = href;
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

    if ($row && $row.length) {
        $row.addClass('highlight');
        $row.find('td').css({
            backgroundColor: ROW_HIGHLIGHT_COLOR,
            color: ROW_HIGHLIGHT_TEXT_COLOR
        });
        $row.find('a').css('color', ROW_HIGHLIGHT_TEXT_COLOR);
    }
}

function showVTnhapkho(Manhapkho) {
    if (!Manhapkho || Manhapkho.trim() === '') {
        console.error("Mã nhập kho không hợp lệ:", Manhapkho);
        $('.tablethietbi tbody').html('<tr><td colspan="10" style="text-align:center;">Mã nhập kho không hợp lệ.</td></tr>');
        return;
    }

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
        success: function (data) {
            console.log("Dữ liệu nhận được từ API:", data); 
            console.log("Số lượng vật tư:", data ? data.length : 0);

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
            $('.table tbody tr').each(function () {
                const linkText = $(this).find('.manhapkho-link').text().trim();
                if (linkText === Manhapkho) {
                    $rowToHighlight = $(this);
                    return false;
                }
            });
            applyNhapKhoRowHighlight($rowToHighlight);
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

// Gọi hàm getThongbaoData khi trang được tải
$(document).ready(function () {
    getThongbaoData();
});