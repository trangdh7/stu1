$(document).ready(function () {
    const firstRow = $('.table tbody tr').first(); 
    if (firstRow.length > 0) {
        const Maxuatkho = firstRow.find('td').eq(1).find('a').text().trim() || firstRow.find('td').eq(1).text().trim();
        if (Maxuatkho) {
            showVTxuatkho(Maxuatkho); 
        }
    }
    if (typeof getThongbaoData === 'function') {
        getThongbaoData();
    }
    setActiveMenu();
    
    // Xử lý click vào hàng
    $(document).on('click', '.clickable-row', function() {
        const MaXuatkho = $(this).data('maxuatkho');
        if (MaXuatkho) {
            showVTxuatkho(MaXuatkho);
        }
    });
});

const ROW_HIGHLIGHT_COLOR = "#2d9f3c";
const ROW_HIGHLIGHT_TEXT_COLOR = "#ffffff";

function applyXuatKhoRowHighlight($row) {
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

// Hàm hiển thị thiết bị theo mã yêu cầu
function showVTxuatkho(Maxuatkho) {
    console.log("Mã xuất kho được chọn:", Maxuatkho); // Kiểm tra mã yêu cầu

    const pathSegments = window.location.pathname.split('/');
    const area = pathSegments.length > 1 ? pathSegments[1] : ''; // Giả sử area là segment đầu tiên sau dấu '/'

    // Tìm hàng tương ứng và lấy thông tin trạng thái
    let $selectedRow = $('.clickable-row').filter(function() {
        return $(this).data('maxuatkho') === Maxuatkho;
    });
    
    // Lấy trạng thái từ cột trạng thái trong bảng
    const $trangThaiCell = $selectedRow.find('td').eq(6); // Cột thứ 7 (index 6) là cột Trạng thái
    let trangThaiText = '';
    let hasButton = false;
    
    // Kiểm tra xem có nút không
    if ($trangThaiCell.find('button').length > 0) {
        hasButton = true;
        // Lấy text từ span hoặc từ button title
        const $span = $trangThaiCell.find('span.status-waiting');
        if ($span.length > 0) {
            trangThaiText = $span.text().trim();
        } else {
            trangThaiText = $trangThaiCell.find('button').attr('title') || '';
        }
    } else {
        trangThaiText = $trangThaiCell.text().trim();
    }
    
    // Hiển thị nút xuất kho dưới bảng chi tiết
    displayXuatKhoButton(Maxuatkho, $selectedRow, area);

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

// Hàm hiển thị nút xuất kho dưới bảng chi tiết
function displayXuatKhoButton(MaXuatkho, $row, area) {
    const $buttonContainer = $('#xuatkho-button-container');
    const $buttonContent = $('#xuatkho-button-content');
    
    // Lấy thông tin từ data attributes
    const trangThai = $row.data('trangthai') || '';
    const bophan = $row.data('bophan') || '';
    
    // Lấy cột trạng thái để kiểm tra có nút không
    const $trangThaiCell = $row.find('td').eq(6); // Cột Trạng thái
    const $spanWithButton = $trangThaiCell.find('span[data-has-button="true"]');
    const $spanWithButtonMuahang = $trangThaiCell.find('span[data-has-button-muahang="true"]');
    const $statusSpan = $trangThaiCell.find('span.status-waiting');
    
    let html = '';
    
    // Kiểm tra xem có cần hiển thị nút tạo phiếu mua hàng không
    if ($spanWithButtonMuahang.length > 0 && bophan == "BP kho") {
        const maXuatKho = $spanWithButtonMuahang.data('ma-xuatkho') || MaXuatkho;
        const action = $spanWithButtonMuahang.data('action') || 'taophieumuahang';
        
        // Tạo form với nút có text "Tạo Phiếu Mua Hàng"
        html = `
            <form action="/${area}/Yeucau/TaoPhieuMuaHangChoNhanVienMuahang" method="post" style="display: inline-block;">
                <input type="hidden" name="MaXuatkho" value="${maXuatKho}" />
                <button type="submit" class="btn-icon approve-btn" style="padding: 15px 30px; background-color: #007bff; color: white; border: none; border-radius: 6px; cursor: pointer; font-size: 18px; font-weight: bold; min-width: 200px; white-space: nowrap;">
                    <i class="bx bx-cart-add" style="font-size: 20px; vertical-align: middle;"></i> Tạo Phiếu Mua Hàng
                </button>
            </form>
        `;
    }
    // Kiểm tra xem có cần hiển thị nút xuất kho không
    else if ($spanWithButton.length > 0 && bophan == "BP kho") {
        const maXuatKho = $spanWithButton.data('ma-xuatkho') || MaXuatkho;
        const action = $spanWithButton.data('action') || 'approve';
        
        // Tạo form với nút có text "Xuất Kho"
        html = `
            <form action="/${area}/Yeucau/Xuliphieuxuatkho" method="post" style="display: inline-block;">
                <input type="hidden" name="MaXuatkho" value="${maXuatKho}" />
                <button type="submit" name="action" value="${action}" class="btn-icon approve-btn" style="padding: 15px 30px; background-color: #28a745; color: white; border: none; border-radius: 6px; cursor: pointer; font-size: 18px; font-weight: bold; min-width: 150px; white-space: nowrap;">
                    <i class="bx bxs-check-circle" style="font-size: 20px; vertical-align: middle;"></i> Xuất Kho
                </button>
            </form>
        `;
    } else if ($statusSpan.length > 0) {
        // Hiển thị trạng thái dạng text
        html = `<span style="color: ${$statusSpan.css('color')}; font-weight: bold; font-size: 16px;">${$statusSpan.text()}</span>`;
    } else {
        // Hiển thị text trạng thái thông thường
        const trangThaiText = $trangThaiCell.text().trim();
        html = `<span style="color: #666; font-size: 16px;">${trangThaiText}</span>`;
    }
    
    $buttonContent.html(html);
    $buttonContainer.show();
}

// Hàm load dữ liệu vật tư
function loadVTData(Maxuatkho, url, area) {
    $.ajax({
        url: url,
        method: 'GET',
        data: { MaXuatkho: Maxuatkho }, // Sử dụng đúng tên tham số
        success: function (response) {
            console.log(response); // Kiểm tra dữ liệu nhận được

            $('.tablethietbi tbody').empty();
            
            // Xử lý response mới (có items) hoặc cũ (mảng trực tiếp)
            let data = response.items || response;
            let tenNguoiYeuCau = response.tenNguoiYeuCau || '';
            let maYeucau = response.maYeucau || '';
            
            // Hiển thị header text cho tất cả areas
            // Ưu tiên hiển thị theo mã yêu cầu giống màn Yeucau: "Yêu cầu vật tư [MaYeucau] của [Người]"
            if (maYeucau && tenNguoiYeuCau) {
                $('#phieuxuatkho-header-text').text(`Yêu cầu vật tư ${maYeucau} của ${tenNguoiYeuCau}`);
                $('#phieuxuatkho-header').show();
            } else if (maYeucau) {
                $('#phieuxuatkho-header-text').text(`Yêu cầu vật tư ${maYeucau}`);
                $('#phieuxuatkho-header').show();
            } else if (Maxuatkho && tenNguoiYeuCau) {
                // Fallback: nếu không có mã yêu cầu thì dùng mã xuất kho
                $('#phieuxuatkho-header-text').text(`Yêu cầu xuất kho ${Maxuatkho} của ${tenNguoiYeuCau}`);
                $('#phieuxuatkho-header').show();
            } else if (Maxuatkho) {
                $('#phieuxuatkho-header-text').text(`Yêu cầu xuất kho ${Maxuatkho}`);
                $('#phieuxuatkho-header').show();
            } else {
                $('#phieuxuatkho-header').hide();
            }

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
                        <td><span style="background-color:${bgColor}; color:black; padding:2px 6px; border-radius:3px; font-size:11px;">${item.trangThai || '-'}</span></td>
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

            let $rowToHighlight = $();
            $('.table tbody tr').each(function () {
                const $link = $(this).find('td').eq(1).find('a');
                const maXuatKhoText = $link.length > 0 ? $link.text().trim() : $(this).find('td').eq(1).text().trim();
                if (maXuatKhoText === Maxuatkho) {
                    $rowToHighlight = $(this);
                    return false;
                }
            });
            applyXuatKhoRowHighlight($rowToHighlight);
            
            // Cập nhật nút xuất kho khi load lại dữ liệu
            if ($rowToHighlight.length > 0) {
                displayXuatKhoButton(Maxuatkho, $rowToHighlight, area);
            }
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
    if (typeof getThongbaoData === 'function') {
        getThongbaoData();
    }
});