$(document).ready(function () {
    initializeYeucauFilters();

    // Gọi hàm showVTYeucau với mã yêu cầu của hàng đầu tiên (sau khi filter áp dụng)
    const firstRow = $('.table tbody tr:visible').first();
    if (firstRow.length > 0) {
        const MaYeucau = firstRow.find('td').eq(2).find('a').text().trim() || firstRow.find('td').eq(2).text().trim();
        const NguoiYeucau = firstRow.find('td').eq(3).text().trim();
        if (MaYeucau) {
            showVTYeucau(MaYeucau, NguoiYeucau);
        }
    }

    // Gọi hàm thông báo ngay khi trang load
    if (typeof getThongbaoData === 'function') {
        getThongbaoData();
    }
    
    // Gọi lại sau 1 giây để đảm bảo DOM đã sẵn sàng
    setTimeout(function() {
        if (typeof getThongbaoData === 'function') {
            getThongbaoData();
        }
    }, 1000);
    
    setActiveMenu();
});

const ROW_HIGHLIGHT_COLOR = "#2d9f3c";
const ROW_HIGHLIGHT_TEXT_COLOR = "#ffffff";

function applyTableRowHighlight($row) {
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

function showVTYeucau(MaYeucau, NguoiYeucau) {
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
        success: function (response) {
            console.log(response); // Kiểm tra dữ liệu nhận được

            // Xử lý response mới (có items) hoặc cũ (mảng trực tiếp)
            let data = response.items || response;
            let tenNguoiYeuCau = response.tenNguoiYeuCau || NguoiYeucau || '';
            
            // Hiển thị thông tin yêu cầu
            if (MaYeucau && tenNguoiYeuCau) {
                $('#yeucauInfo').show();
                $('#yeucauInfoText').text('Yêu cầu vật tư ' + MaYeucau + ' của ' + tenNguoiYeuCau);
            } else if (MaYeucau) {
                $('#yeucauInfo').show();
                $('#yeucauInfoText').text('Yêu cầu vật tư ' + MaYeucau);
            } else {
                $('#yeucauInfo').hide();
            }

            $('.tablethietbi tbody').empty();

            if (data && data.length > 0) {
                let STT = 1;
                data.forEach(function (item) {
                    // Xác định màu cho ghi chú (đỏ nếu bị từ chối)
                    var ghiChuColor = (item.trangThai && (item.trangThai.indexOf('Đã từ chối') !== -1 || item.trangThai.indexOf('từ chối') !== -1)) ? '#f44336' : 'inherit';
                    // Hỗ trợ cả camelCase và PascalCase
                    const tenSanpham = item.tenSanpham || item.TenSanpham || '';
                    const maSanpham = item.maSanpham || item.MaSanpham || '';
                    const hangSX = item.hangSX || item.HangSX || '';
                    const nhaCC = item.nhaCC || item.NhaCC || '';
                    const slCu = item.slCu ?? item.SLCu;
                    const slMoi = item.slMoi ?? item.SLMoi;
                    const slTong = item.sl ?? item.SL ?? slMoi;
                    const donVi = item.donVi || item.DonVi || '';
                    const ngayCan = item.ngayCanHang || item.NgayCanHang
                        ? new Date(item.ngayCanHang || item.NgayCanHang).toLocaleDateString('vi-VN')
                        : '-';
                    const trangThai = item.trangThai || item.TrangThai || '';
                    const ghiChu = item.ghiChu || item.GhiChu || '-';

                    const formatNumberOrDash = (value) => {
                        return value === null || value === undefined || value === '' ? '-' : value;
                    };

                    // Tạo một dòng mới khớp tiêu đề bảng
                    let row = `<tr>
                        <td>${STT++}</td>
                        <td>${tenSanpham}</td>
                        <td>${maSanpham}</td>
                        <td>${hangSX}</td>
                        <td>${nhaCC}</td>
                        <td style="text-align: center;">${formatNumberOrDash(slCu)}</td>
                        <td style="text-align: center;">${formatNumberOrDash(slMoi)}</td>
                        <td style="text-align: center;">${formatNumberOrDash(slTong)}</td>
                        <td>${donVi || '-'}</td>
                        <td>${ngayCan}</td>
                        <td>${trangThai}</td>
                        <td style="color: ${ghiChuColor};">${ghiChu}</td>
                    </tr>`;
                    $('.tablethietbi tbody').append(row);
                });
            } else {
                // Hiển thị thông báo nếu không có dữ liệu
                $('.tablethietbi tbody').append(
                    `<tr>
                        <td colspan="10" style="text-align:center;">Không có dữ liệu vật tư.</td>
                    </tr>`
                );
            }

            // Highlight hàng tương ứng trong bảng
            let $rowToHighlight = $();
            $('.table tbody tr').each(function () {
                if ($(this).find('td').eq(2).text().trim() === MaYeucau) {
                    $rowToHighlight = $(this);
                    return false;
                }
            });
            applyTableRowHighlight($rowToHighlight);
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
    if (typeof getThongbaoData === 'function') {
        getThongbaoData();
    }
});

function initializeYeucauFilters() {
    const $searchInput = $('#timkiem');
    const $statusFilter = $('#statusFilter');

    if (!$searchInput.length && !$statusFilter.length) {
        return;
    }

    const triggerFilter = function () {
        filterYeucauTable();
    };

    if ($searchInput.length) {
        $searchInput.on('input', triggerFilter);
    }
    if ($statusFilter.length) {
        $statusFilter.on('change', triggerFilter);
    }

    filterYeucauTable();
}

function filterYeucauTable() {
    const keyword = ($('#timkiem').val() || '').toLowerCase().trim();
    const statusValue = ($('#statusFilter').val() || '').toLowerCase().trim();
    let visibleCount = 0;

    $('.table tbody tr').each(function () {
        const $row = $(this);
        const rowText = $row.text().toLowerCase();
        const statusText = $row.find('td').eq(8).text().toLowerCase();

        const matchesKeyword = !keyword || rowText.includes(keyword);
        const matchesStatus = !statusValue || statusText.indexOf(statusValue) !== -1;

        const shouldShow = matchesKeyword && matchesStatus;
        $row.toggle(shouldShow);

        if (shouldShow) {
            visibleCount++;
        }
    });

    const hasData = visibleCount > 0;
    $('#noYeucauMessage').toggle(!hasData);

    if (!hasData) {
        applyTableRowHighlight($());
    } else {
        const $currentHighlight = $('.table tbody tr.highlight:visible').first();
        if (!$currentHighlight.length) {
            applyTableRowHighlight($('.table tbody tr:visible').first());
        }
    }
}

