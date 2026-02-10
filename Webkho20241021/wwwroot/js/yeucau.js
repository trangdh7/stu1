$(document).ready(function () {
    initializeYeucauFilters();

    // Gọi hàm showVTYeucau với mã yêu cầu của hàng đầu tiên (sau khi filter áp dụng)
    const firstRow = $('.table tbody tr:visible').first();
    if (firstRow.length > 0) {
        const colIdx = getYeucauColumnIndexes();
        const MaYeucau = firstRow.find('td').eq(colIdx.ma).find('a').text().trim()
                        || firstRow.find('td').eq(colIdx.ma).text().trim();
        const NguoiYeucau = firstRow.find('td').eq(colIdx.nguoi).text().trim();
        if (MaYeucau) {
            showVTYeucau(MaYeucau, NguoiYeucau);
            // Đồng thời highlight luôn hàng đầu tiên để người dùng thấy đang xem yêu cầu nào
            if (typeof applyTableRowHighlight === 'function') {
                applyTableRowHighlight(firstRow);
            }
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

    // Sticky header 2 dòng cho bảng vật tư
    syncTableThietbiStickyHeader();
    $(window).on('resize', function () {
        syncTableThietbiStickyHeader();
    });

    // Nút thu nhỏ / phóng to bảng chi tiết (dùng chung toàn bộ trang Yeucau)
    $(document).on('click', '.btn-minimize-table', function () {
        var $nav = $(this).closest('.bodyyeucau-thietbi');
        $nav.removeClass('table-maximized');
        $nav.find('.btn-maximize-table').show();
        $nav.find('.btn-restore-table').hide();
        $nav.hide();
    });
    $(document).on('click', '.btn-maximize-table', function () {
        var $nav = $(this).closest('.bodyyeucau-thietbi');
        $nav.addClass('table-maximized');
        $nav.find('.btn-maximize-table').hide();
        $nav.find('.btn-restore-table').show();
        if (typeof syncTableThietbiStickyHeader === 'function') syncTableThietbiStickyHeader();
    });
    $(document).on('click', '.btn-restore-table', function () {
        var $nav = $(this).closest('.bodyyeucau-thietbi');
        $nav.removeClass('table-maximized');
        $nav.find('.btn-restore-table').hide();
        $nav.find('.btn-maximize-table').show();
        if (typeof syncTableThietbiStickyHeader === 'function') syncTableThietbiStickyHeader();
    });
});

const ROW_HIGHLIGHT_COLOR = "#2d9f3c";
const ROW_HIGHLIGHT_TEXT_COLOR = "#ffffff";

// Xác định vị trí các cột trong bảng yêu cầu dựa trên header
function getYeucauColumnIndexes() {
    const $headerCells = $('.Tableyeucau .table thead tr').first().children('th,td');
    // Giá trị mặc định (cấu trúc cũ: không có cột "Chọn")
    const indexes = {
        chon: 0,
        stt: 0,
        ten: 1,
        ma: 2,
        nguoi: 3,
        status: 8
    };

    $headerCells.each(function (i) {
        const text = $(this).text().trim().toLowerCase();
        if (text === 'stt') {
            indexes.stt = i;
        } else if (text === 'tên yêu cầu') {
            indexes.ten = i;
        } else if (text === 'mã yêu cầu') {
            indexes.ma = i;
        } else if (text === 'người yêu cầu') {
            indexes.nguoi = i;
        } else if (text === 'trạng thái') {
            indexes.status = i;
        } else if (text === 'chọn') {
            indexes.chon = i;
        }
    });

    return indexes;
}

// Sticky header (2 dòng) cho bảng vật tư: đo chiều cao dòng 1 để dòng 2 bám đúng vị trí
function syncTableThietbiStickyHeader() {
    try {
        const table = document.querySelector('.tablethietbi');
        if (!table) return;
        const firstRow = table.querySelector('thead tr:first-child');
        if (!firstRow) return;
        const h = firstRow.getBoundingClientRect().height || 0;
        if (h > 0) {
            table.style.setProperty('--tablethietbi-header-row1-height', `${h}px`);
        }
    } catch (e) {
        // no-op
    }
}

function applyTableRowHighlight($row) {
    // Dùng chung cho bảng yêu cầu: xóa hết highlight cũ, tô lại cho hàng được chọn
    const $rows = $('.Tableyeucau .table tbody tr');

    // Gỡ class + màu cũ
    $rows.removeClass('active-row');
    $rows.find('td').css({
        backgroundColor: '',
        color: ''
    });
    $rows.find('a').css('color', '');

    if ($row && $row.length) {
        // Ghi log để debug khi cần
        // console.log('[applyTableRowHighlight] set active for row index', $row.index());

        // Gắn class + màu mới cho hàng được chọn
        $row.addClass('active-row');
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
    syncTableThietbiStickyHeader();

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

            // Cập nhật link tải xuống danh sách vật tư (nếu có)
            var $downloadLink = $('#btnDownloadYeucauVatTu');
            if ($downloadLink.length && MaYeucau) {
                $downloadLink.attr('href', '/' + area + '/Yeucau/ExportYeucauVatTuExcel?MaYeucau=' + encodeURIComponent(MaYeucau));
            }

        $('.tablethietbi tbody').empty();

        if (data && data.length > 0) {
            // Sắp xếp để các vật tư có cùng mã VT đứng gần nhau
            data.sort(function (a, b) {
                const maA = (a.maSanpham || a.MaSanpham || '').toString().trim();
                const maB = (b.maSanpham || b.MaSanpham || '').toString().trim();
                return maA.localeCompare(maB);
            });

            let STT = 1;
                const hasTonKhoColumn = $('.tablethietbi thead td, .tablethietbi thead th').filter(function() { return $(this).text().trim() === 'Tồn kho'; }).length > 0;
                const formatNumberOrDash = (value) => (value === null || value === undefined || value === '' ? '-' : value);
                const formatDate = (val) => (val ? new Date(val).toLocaleDateString('vi-VN') : '-');
                const formatDateTime = (val) => (val ? new Date(val).toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }) : '-');

                data.forEach(function (item) {
                    var isRejected = (item.trangThai && (item.trangThai.toLowerCase().indexOf('từ chối') !== -1)) || (item.TrangThai && (item.TrangThai.toLowerCase().indexOf('từ chối') !== -1));
                    var ghiChuColor = isRejected ? '#f44336' : 'inherit';
                    const tenSanpham = item.tenSanpham || item.TenSanpham || '';
                    const maSanpham = item.maSanpham || item.MaSanpham || '';
                    const ttExcel = (item.tt ?? item.TT ?? '').toString().trim();
                    const sttFallback = (STT++).toString();
                    const ttDisplay = ttExcel || sttFallback;
                    const hangSX = item.hangSX || item.HangSX || '';
                    const nhaCC = item.nhaCC || item.NhaCC || '';
                    const slCu = item.slCu ?? item.SLCu;
                    const slMoi = item.slMoi ?? item.SLMoi;
                    const slTong = item.sl ?? item.SL ?? slMoi;
                    const donVi = item.donVi || item.DonVi || '';
                    const ngayCan = item.ngayCanHang || item.NgayCanHang ? formatDate(item.ngayCanHang || item.NgayCanHang) : '-';
                    const ngayCoHang = item.ngayCoHang || item.NgayCoHang ? formatDate(item.ngayCoHang || item.NgayCoHang) : '-';
                    const trangThai = item.trangThai || item.TrangThai || '';
                    const ghiChu = item.ghiChu || item.GhiChu || '-';
                    const tonKho = item.tonKho ?? item.TonKho ?? 0;
                    const rawSlThieu = item.slThieu ?? item.SlThieu;
                    const slThieu = rawSlThieu != null
                        ? Math.max(0, rawSlThieu)
                        : Math.max(0, (slMoi ?? 0) - tonKho);
                    const slDaXuat = item.slDaXuat ?? item.SlDaXuat;
                    const ngayDuyet = item.ngayDuyet || item.NgayDuyet ? formatDateTime(item.ngayDuyet || item.NgayDuyet) : '-';

                    let row;
                    if (hasTonKhoColumn) {
                        row = `<tr class="${isRejected ? 'rejected-row' : ''}">
                            <td>${ttDisplay}</td>
                            <td>${tenSanpham}</td>
                            <td>${maSanpham}</td>
                            <td>${hangSX}</td>
                            <td>${nhaCC}</td>
                            <td style="text-align: center;">${formatNumberOrDash(slCu)}</td>
                            <td style="text-align: center;">${formatNumberOrDash(slMoi)}</td>
                            <td style="text-align: center;">${formatNumberOrDash(slThieu)}</td>
                            <td style="text-align: center;">${slDaXuat != null ? slDaXuat : '-'}</td>
                            <td style="text-align: center;">${formatNumberOrDash(tonKho)}</td>
                            <td hidden style="text-align: center;">${formatNumberOrDash(slTong)}</td>
                            <td>${donVi || '-'}</td>
                            <td>${ngayCan}</td>
                            <td>${ngayCoHang}</td>
                            <td>${trangThai}</td>
                            <td style="color: ${ghiChuColor};">${ghiChu}</td>
                            <td>${ngayDuyet}</td>
                        </tr>`;
                    } else {
                        row = `<tr class="${isRejected ? 'rejected-row' : ''}">
                            <td>${ttDisplay}</td>
                            <td>${tenSanpham}</td>
                            <td>${maSanpham}</td>
                            <td>${hangSX}</td>
                            <td>${nhaCC}</td>
                            <td style="text-align: center;">${formatNumberOrDash(slCu)}</td>
                            <td style="text-align: center;">${formatNumberOrDash(slMoi)}</td>
                            <td hidden style="text-align: center;">${formatNumberOrDash(slTong)}</td>
                            <td>${donVi || '-'}</td>
                            <td>${ngayCan}</td>
                            <td>${ngayCoHang}</td>
                            <td>${trangThai}</td>
                            <td style="color: ${ghiChuColor};">${ghiChu}</td>
                        </tr>`;
                    }
                    $('.tablethietbi tbody').append(row);
                });
                syncTableThietbiStickyHeader();
            } else {
                const colSpan = $('.tablethietbi thead td, .tablethietbi thead th').filter(function() { return $(this).text().trim() === 'Tồn kho'; }).length > 0 ? 16 : 12;
                $('.tablethietbi tbody').append(
                    `<tr>
                        <td colspan="${colSpan}" style="text-align:center;">Không có dữ liệu vật tư.</td>
                    </tr>`
                );
            }

            // Highlight hàng tương ứng trong bảng
            let $rowToHighlight = $();
            $('.table tbody tr').each(function () {
            const colIdx = getYeucauColumnIndexes();
            if ($(this).find('td').eq(colIdx.ma).text().trim() === MaYeucau) {
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

    // Cập nhật dropdown trạng thái dựa trên dữ liệu hiện tại khi khởi tạo
    updateStatusFilterOptions();

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

function updateStatusFilterOptions() {
    const $statusFilter = $('#statusFilter');
    if (!$statusFilter.length) {
        return;
    }

    const colIdx = getYeucauColumnIndexes();

    // Thu thập tất cả trạng thái từ các hàng đang hiển thị
    const statusSet = new Set();
    $('.table tbody tr:visible').each(function () {
        const statusText = $(this).find('td').eq(colIdx.status).text().trim();
        if (statusText) {
            statusSet.add(statusText);
        }
    });

    // Lưu giá trị hiện tại
    const currentValue = $statusFilter.val();

    // Xóa tất cả options trừ "Tất cả"
    $statusFilter.find('option:not([value=""])').remove();

    // Sắp xếp và thêm các trạng thái mới
    const sortedStatuses = Array.from(statusSet).sort();
    sortedStatuses.forEach(function (status) {
        $statusFilter.append($('<option></option>').attr('value', status).text(status));
    });

    // Khôi phục giá trị đã chọn nếu vẫn còn tồn tại
    if (currentValue && $statusFilter.find('option[value="' + currentValue + '"]').length > 0) {
        $statusFilter.val(currentValue);
    } else {
        $statusFilter.val('');
    }
}

function filterYeucauTable() {
    const keyword = ($('#timkiem').val() || '').toLowerCase().trim();
    const statusValue = ($('#statusFilter').val() || '').toLowerCase().trim();
    let visibleCount = 0;

    const colIdx = getYeucauColumnIndexes();

    $('.table tbody tr').each(function () {
        const $row = $(this);
        const rowText = $row.text().toLowerCase();
        const statusText = $row.find('td').eq(colIdx.status).text().toLowerCase();

        const matchesKeyword = !keyword || rowText.includes(keyword);
        const matchesStatus = !statusValue || statusText.indexOf(statusValue) !== -1;

        const shouldShow = matchesKeyword && matchesStatus;
        $row.toggle(shouldShow);

        if (shouldShow) {
            visibleCount++;
        }
    });

    // Sắp xếp lại các hàng: "Đã từ chối" xuống cuối cùng
    const $tbody = $('.table tbody');
    const $rows = $tbody.find('tr').toArray();
    
    // Tách thành 2 nhóm: không phải "Đã từ chối" và "Đã từ chối"
    const normalRows = [];
    const rejectedRows = [];
    
    $rows.forEach(function(row) {
        const $row = $(row);
        const statusText = $row.find('td').eq(8).text().trim().toLowerCase();
        const isRejected = statusText.indexOf('đã từ chối') !== -1;
        
        if (isRejected) {
            rejectedRows.push(row);
        } else {
            normalRows.push(row);
        }
    });
    
    // Xóa tất cả hàng khỏi DOM
    $tbody.empty();
    
    // Thêm lại: hàng thường trước, hàng "Đã từ chối" sau
    normalRows.forEach(function(row) {
        $tbody.append(row);
    });
    rejectedRows.forEach(function(row) {
        $tbody.append(row);
    });
    
    // Cập nhật lại số thứ tự (STT) sau khi sắp xếp
    let stt = 1;
    $tbody.find('tr').each(function() {
        // Cột STT có thể không phải là cột đầu nếu có thêm cột "Chọn"
        $(this).find('td').eq(colIdx.stt).text(stt);
        stt++;
    });

    // Cập nhật dropdown trạng thái dựa trên các hàng đang hiển thị
    updateStatusFilterOptions();

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

