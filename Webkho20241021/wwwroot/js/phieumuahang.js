$(document).ready(function () {
    const pathSegments = window.location.pathname.split('/');
    const area = pathSegments.length > 1 ? pathSegments[1] : '';
    
    // Ẩn nút "Gửi báo giá" nếu không phải area mua hàng (Trưởng BP hoặc Nhân viên mua hàng)
    if (area !== 'TruongBPMuahang' && area !== 'NhanvienMuahang') {
        $('#submitPhieumuahang').hide();
    }

    const firstRow = $('.table tbody tr').first();
    if (firstRow.length > 0) {
        const link = firstRow.find('td').eq(1).find('a');
        const Mamuahang = link.text().trim();
        const trangThai = link.data('trangthai') || '';
        showVTmuahang(Mamuahang, trangThai);
    }
    getThongbaoData();
    setActiveMenu();
    
    // Xử lý click vào hàng
    $(document).on('click', '.clickable-row', function() {
        const MaMuahang = $(this).data('mamuahang');
        const link = $(this).find('td').eq(1).find('a');
        const trangThai = link.data('trangthai') || '';
        if (MaMuahang) {
            showVTmuahang(MaMuahang, trangThai);
        }
    });
});

const ROW_HIGHLIGHT_COLOR = "#2d9f3c";
const ROW_HIGHLIGHT_TEXT_COLOR = "#ffffff";

function applyPurchaseRowHighlight($row) {
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

// Xử lý khi nhấn nút "Gửi phiếu mua hàng"
$('#submitPhieumuahang').click(function () {
    if (!selectedMamuahang) {
        alert("Vui lòng chọn mã mua hàng trước khi gửi.");
        return;
    }

    // Lưu lại tất cả giá trị đã nhập trước khi submit để có thể restore nếu fail
    const savedInputValues = {};
    $('.tablethietbi tbody tr').each(function () {
        if ($(this).hasClass('tong-tien-row')) {
            return;
        }
        const priceInput = $(this).find('.DonGia input');
        if (priceInput.length > 0) {
            const MaSanpham = $(this).find('td').eq(2).text().trim();
            if (MaSanpham) {
                const inputValue = priceInput.val();
                savedInputValues[MaSanpham] = inputValue;
                // Lưu vào biến global để restore khi rebuild table
                if (inputValue) {
                    savedInputValuesForRestore[MaSanpham] = inputValue;
                }
            }
        }
    });

    const vtmuahangData = [];
    let itemsWithoutPrice = [];
    
    $('.tablethietbi tbody tr').each(function () {
        // Bỏ qua hàng tổng tiền
        if ($(this).hasClass('tong-tien-row')) {
            return;
        }
        
        const cells = $(this).find('td');
        const priceInput = $(this).find('.DonGia input');
        
        // Chỉ xử lý các hàng có input giá (có thể nhập giá)
        if (cells.length >= 2 && priceInput.length > 0) {
            const inputValue = priceInput.val();
            // Xử lý giá trị có thể chứa dấu chấm hoặc dấu phẩy
            let cleanValue = inputValue ? inputValue.replace(/[^\d.]/g, '') : '';
            const DonGia = cleanValue ? parseFloat(cleanValue) || 0 : 0;
            const SL = parseFloat(priceInput.data('sl')) || 0;
            
            // Lấy MaSanpham từ cột thứ 3 (index 2) trong bảng
            const MaSanpham = $(this).find('td').eq(2).text().trim();
            const TenSanpham = $(this).find('td').eq(1).text().trim();

            // Nếu số lượng = 0, không cần nhập, bỏ qua
            if (SL === 0) {
                return;
            }
            
            // Chỉ thêm vào dữ liệu nếu có giá hợp lệ (cho phép báo giá một phần)
            if (DonGia > 0 && SL > 0) {
                const ThanhTien = SL * DonGia;
                vtmuahangData.push({
                    MaMuahang: selectedMamuahang,
                    MaSanpham: MaSanpham,
                    DonGia: DonGia,
                    ThanhTien: ThanhTien
                });
            } else if (SL > 0) {
                // Ghi nhận các mục chưa có giá (để thông báo)
                itemsWithoutPrice.push({
                    ten: TenSanpham || MaSanpham,
                    ma: MaSanpham,
                    sl: SL
                });
            }
        }
    });
    
    // Kiểm tra nếu không có dữ liệu nào để gửi
    if (vtmuahangData.length === 0) {
        alert("Vui lòng nhập ít nhất một đơn giá trước khi gửi!");
        return;
    }
    
    // Thông báo nếu có mục chưa nhập giá (nhưng vẫn cho phép gửi)
    if (itemsWithoutPrice.length > 0) {
        const missingList = itemsWithoutPrice.map(item => 
            `- ${item.ten} (Mã: ${item.ma}, Số lượng: ${item.sl})`
        ).join('\n');
        const confirmMessage = `Bạn đang gửi báo giá cho ${vtmuahangData.length} vật tư.\n\nCác vật tư chưa có giá (có thể bổ sung sau):\n${missingList}\n\nBạn có muốn tiếp tục gửi báo giá một phần không?`;
        if (!confirm(confirmMessage)) {
            return;
        }
    }

    const Phieumuahangviewmodel = {
        MaMuahang: selectedMamuahang,
        VTphieumuahang: vtmuahangData
    };

    const pathSegments = window.location.pathname.split('/');
    const area = pathSegments.length > 1 ? pathSegments[1] : '';
    const url = `/${area}/Yeucau/ThemPhieumuahangSQL`;

    // Disable nút submit để tránh double submit
    const $submitBtn = $('#submitPhieumuahang');
    $submitBtn.prop('disabled', true);
    const originalText = $submitBtn.text();
    $submitBtn.text('Đang gửi...');

    fetch(url, {
        method: "POST",
        body: JSON.stringify(Phieumuahangviewmodel),
        headers: {
            "Content-Type": "application/json"
        }
    })
        .then(response => response.json())
        .then(data => {
            // Re-enable nút submit
            $submitBtn.prop('disabled', false);
            $submitBtn.text(originalText);

            if (data.success) {
                // Clear saved values khi submit thành công
                savedInputValuesForRestore = {};
                alert("Gửi dữ liệu thành công!");
                location.reload();
            } else {
                // Nếu fail, restore lại giá trị đã nhập
                $('.tablethietbi tbody tr').each(function () {
                    if ($(this).hasClass('tong-tien-row')) {
                        return;
                    }
                    const priceInput = $(this).find('.DonGia input');
                    if (priceInput.length > 0) {
                        const MaSanpham = $(this).find('td').eq(2).text().trim();
                        if (MaSanpham && savedInputValues[MaSanpham] !== undefined) {
                            priceInput.val(savedInputValues[MaSanpham]);
                            // Lưu lại vào biến global để restore khi rebuild table
                            savedInputValuesForRestore[MaSanpham] = savedInputValues[MaSanpham];
                            // Trigger input event để cập nhật thành tiền
                            priceInput.trigger('input');
                        }
                    }
                });
                alert(data.message || "Gửi dữ liệu thất bại.");
            }
        })
        .catch(error => {
            // Re-enable nút submit
            $submitBtn.prop('disabled', false);
            $submitBtn.text(originalText);

            // Restore lại giá trị đã nhập
            $('.tablethietbi tbody tr').each(function () {
                if ($(this).hasClass('tong-tien-row')) {
                    return;
                }
                const priceInput = $(this).find('.DonGia input');
                if (priceInput.length > 0) {
                    const MaSanpham = $(this).find('td').eq(2).text().trim();
                    if (MaSanpham && savedInputValues[MaSanpham] !== undefined) {
                        priceInput.val(savedInputValues[MaSanpham]);
                        // Lưu lại vào biến global để restore khi rebuild table
                        savedInputValuesForRestore[MaSanpham] = savedInputValues[MaSanpham];
                        // Trigger input event để cập nhật thành tiền
                        priceInput.trigger('input');
                    }
                }
            });
            console.error("Lỗi:", error);
            alert("Gửi dữ liệu thất bại.");
        });
});

let selectedMamuahang = "";
// Lưu giá trị đã nhập để restore khi rebuild table
let savedInputValuesForRestore = {};

// Hiển thị vật tư theo mã mua hàng
function showVTmuahang(Mamuahang, trangThaiPhieu) {
    selectedMamuahang = Mamuahang;

    const pathSegments = window.location.pathname.split('/');
    const area = pathSegments.length > 1 ? pathSegments[1] : '';
    const url = `/${area}/Yeucau/GetVTPhieumuahang`;

    // Nếu không có trạng thái được truyền vào, lấy từ data attribute của link hoặc từ cột trạng thái
    if (!trangThaiPhieu) {
        $('.table tbody tr').each(function() {
            const link = $(this).find('td').eq(1).find('a');
            if (link.text().trim() === Mamuahang) {
                trangThaiPhieu = link.data('trangthai') || '';
                if (!trangThaiPhieu) {
                    // Lấy từ cột trạng thái (cột cuối cùng)
                    const trangThaiCell = $(this).find('td').last();
                    trangThaiPhieu = trangThaiCell.text().trim();
                }
            }
        });
    }

    $.ajax({
        url: url,
        method: 'GET',
        data: { Mamuahang: Mamuahang },
        success: function (response) {
            $('.tablethietbi tbody').empty();
            
            // Xử lý response mới (có items) hoặc cũ (mảng trực tiếp)
            let data = response.items || response;
            let tenNguoiYeuCau = response.tenNguoiYeuCau || '';
            
            // Hiển thị header text cho tất cả areas
            if (Mamuahang && tenNguoiYeuCau) {
                $('#phieumuahang-header-text').text(`Yêu cầu mua hàng ${Mamuahang} của ${tenNguoiYeuCau}`);
                $('#phieumuahang-header').show();
            } else if (Mamuahang) {
                $('#phieumuahang-header-text').text(`Yêu cầu mua hàng ${Mamuahang}`);
                $('#phieumuahang-header').show();
            } else {
                $('#phieumuahang-header').hide();
            }
            
            // Hiển thị action buttons dựa trên điều kiện
            const isGiamdoc = area === 'Giamdoc';
            const isBPMuahang = area === 'TruongBPMuahang' || area === 'NhanvienMuahang';
            const isBPKetoan = area === 'TruongBPKetoan' || area === 'NhanvienKetoan';
            
            // Hiển thị nút "Gửi báo giá" cho BP mua hàng khi:
            // - Trạng thái phiếu = "Đang chờ báo giá" hoặc chứa "Đã từ chối"
            // - Hoặc có ít nhất một mục có trạng thái "Đang chờ báo giá" (cho phép bổ sung báo giá)
            const hasItemsAwaitingQuote = data && data.some(item => {
                const itemTrangThai = (item.trangThai || '').trim();
                return itemTrangThai === 'Đang chờ báo giá';
            });
            if (isBPMuahang && data && data.length > 0 && 
                (trangThaiPhieu === 'Đang chờ báo giá' || 
                 (trangThaiPhieu && trangThaiPhieu.includes('Đã từ chối')) ||
                 hasItemsAwaitingQuote)) {
                $('#submitPhieumuahang').show();
            } else {
                $('#submitPhieumuahang').hide();
            }
            
            // Hiển thị nút duyệt/từ chối CHỈ cho Giám đốc khi có ít nhất một mục đã báo giá
            let hasItemsWithQuote = false;
            if (isGiamdoc) {
                // Kiểm tra xem có mục nào đã báo giá không
                // Xử lý cả camelCase và PascalCase property names
                hasItemsWithQuote = data && data.length > 0 && data.some(item => {
                    // Kiểm tra trạng thái (cả camelCase và PascalCase)
                    const itemTrangThai = (item.trangThai || item.TrangThai || '').toString().trim();
                    
                    // Kiểm tra đơn giá (cả camelCase và PascalCase)
                    const donGiaRaw = item.donGia != null ? item.donGia : item.DonGia;
                    const donGia = donGiaRaw != null ? parseFloat(donGiaRaw) : null;
                    const hasPrice = donGia != null && !isNaN(donGia) && donGia > 0;
                    
                    // Kiểm tra trạng thái
                    const isQuoted = itemTrangThai === 'Đã báo giá';
                    
                    const result = isQuoted && hasPrice;
                    
                    // Debug log cho các mục có trạng thái hoặc giá
                    if (isQuoted || hasPrice || itemTrangThai) {
                        console.log('Item check:', {
                            maSanpham: item.maSanpham || item.MaSanpham,
                            trangThai: itemTrangThai,
                            donGiaRaw: donGiaRaw,
                            donGia: donGia,
                            isQuoted: isQuoted,
                            hasPrice: hasPrice,
                            result: result
                        });
                    }
                    
                    return result;
                });
                
                console.log('Giamdoc check:', {
                    isGiamdoc: isGiamdoc,
                    area: area,
                    dataLength: data ? data.length : 0,
                    trangThaiPhieu: trangThaiPhieu,
                    hasItemsWithQuote: hasItemsWithQuote,
                    shouldShow: data && data.length > 0 && (trangThaiPhieu === 'Đã báo giá' || hasItemsWithQuote),
                    approveButtonExists: $('#approvePhieumuahang').length > 0
                });
                
                // Cho phép duyệt khi: trạng thái phiếu = "Đã báo giá" HOẶC có ít nhất một mục đã báo giá
                if (data && data.length > 0 && (trangThaiPhieu === 'Đã báo giá' || hasItemsWithQuote)) {
                    if ($('#approvePhieumuahang').length > 0) {
                        $('#approvePhieumuahang').show();
                        $('#rejectPhieumuahang').show();
                        $('#action-buttons').show();
                        console.log('✓ Showing approve/reject buttons for Giamdoc');
                    } else {
                        console.warn('⚠ Approve button not found in DOM');
                    }
                } else {
                    $('#approvePhieumuahang').hide();
                    $('#rejectPhieumuahang').hide();
                    // Chỉ hiển thị action-buttons nếu có nút "Gửi báo giá"
                    if ($('#submitPhieumuahang').is(':visible')) {
                        $('#action-buttons').show();
                    } else {
                        $('#action-buttons').hide();
                    }
                    console.log('✗ Hiding approve/reject buttons for Giamdoc');
                }
            }
            
            // Hiển thị nút duyệt/từ chối cho BP kế toán
            if (isBPKetoan) {
                // Lấy trạng thái từ cột trạng thái trong bảng chính
                let trangThaiFromTable = '';
                $('.table tbody tr').each(function() {
                    const link = $(this).find('td').eq(1).find('a');
                    if (link.text().trim() === Mamuahang) {
                        // Lấy từ cột trạng thái (cột thứ 7, index 6)
                        const trangThaiCell = $(this).find('td').eq(6);
                        trangThaiFromTable = trangThaiCell.text().trim();
                        return false;
                    }
                });
                
                // Cho BP kế toán: hiển thị khi trạng thái = "Chờ thanh toán"
                if (trangThaiFromTable === 'Chờ thanh toán') {
                    if ($('#approveRejectButtons').length > 0) {
                        $('#approveRejectButtons').css('display', 'flex');
                    }
                } else {
                    $('#approveRejectButtons').hide();
                }
            }
            // Hiển thị nút duyệt/từ chối cho Trưởng BP mua hàng
            else if (area === 'TruongBPMuahang') {
                // Lấy trạng thái từ cột trạng thái trong bảng chính
                let trangThaiFromTable = '';
                $('.table tbody tr').each(function() {
                    const link = $(this).find('td').eq(1).find('a');
                    if (link.text().trim() === Mamuahang) {
                        // Lấy từ cột trạng thái (cột thứ 7, index 6)
                        const trangThaiCell = $(this).find('td').eq(6);
                        trangThaiFromTable = trangThaiCell.text().trim();
                        return false;
                    }
                });
                
                // Cho Trưởng BP mua hàng: hiển thị khi trạng thái = "Đã thanh toán"
                if (trangThaiFromTable === 'Đã thanh toán') {
                    $('#approvePhieumuahang').show();
                    $('#rejectPhieumuahang').show();
                    $('#action-buttons').show();
                } else {
                    $('#approvePhieumuahang').hide();
                    $('#rejectPhieumuahang').hide();
                    if ($('#submitPhieumuahang').is(':visible')) {
                        $('#action-buttons').show();
                    } else {
                        $('#action-buttons').hide();
                    }
                }
            } else if (!isGiamdoc) {
                // Với các area khác, chỉ hiển thị nút "Gửi báo giá" nếu có
                $('#approvePhieumuahang').hide();
                $('#rejectPhieumuahang').hide();
                if ($('#submitPhieumuahang').is(':visible')) {
                    $('#action-buttons').show();
                } else {
                    $('#action-buttons').hide();
                }
            }
            
            if (data && data.length > 0) {
                // Kiểm tra xem có thể nhập đơn giá không
                // Cho phép nhập khi: area là mua hàng (Trưởng BP hoặc Nhân viên) và (trạng thái phiếu = "Đang chờ báo giá" hoặc chứa "Đã từ chối")
                const isPurchaseArea = (area === 'TruongBPMuahang' || area === 'NhanvienMuahang');
                const canInputPriceForPhieu = isPurchaseArea && 
                    (trangThaiPhieu === 'Đang chờ báo giá' || (trangThaiPhieu && trangThaiPhieu.includes('Đã từ chối')));

                let STT = 1;
                data.forEach(function (item) {
                    // Cho phép nhập giá cho từng mục nếu:
                    // 1. Area là mua hàng VÀ
                    // 2. (Trạng thái phiếu = "Đang chờ báo giá" HOẶC mục này có trạng thái "Đang chờ báo giá")
                    const itemTrangThai = (item.trangThai || '').trim();
                    const canInputPriceForItem = isPurchaseArea && 
                        (canInputPriceForPhieu || itemTrangThai === 'Đang chờ báo giá');
                    
                    let donGiaCell = '';
                    // Kiểm tra xem có giá trị đã lưu không (để restore khi rebuild table)
                    const savedValue = savedInputValuesForRestore[item.maSanpham];
                    const displayValue = savedValue !== undefined ? savedValue : (item.donGia != null ? item.donGia : null);
                    
                    if (canInputPriceForItem) {
                        // Cho phép nhập đơn giá
                        if (displayValue != null) {
                            // Format lại giá trị nếu là số
                            const formattedValue = typeof displayValue === 'string' ? displayValue : displayValue.toLocaleString('vi-VN');
                            donGiaCell = `<span class="DonGia"><input type="text" value="${formattedValue}" placeholder="Nhập giá" class="form-control" data-sl="${item.sl}" /></span>`;
                        } else {
                            donGiaCell = `<span class="DonGia"><input type="text" placeholder="Nhập giá" class="form-control" data-sl="${item.sl}" /></span>`;
                        }
                    } else {
                        // Hiển thị số (read-only) khi không được phép nhập
                        if (displayValue != null) {
                            const formattedValue = typeof displayValue === 'string' ? displayValue : displayValue.toLocaleString('vi-VN');
                            donGiaCell = `<span class="DonGia">${formattedValue}</span>`;
                        } else {
                            donGiaCell = `<span class="DonGia">-</span>`;
                        }
                    }

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
                        <td>${donGiaCell}</td>
                        <td>
                            ${item.thanhTien != null
                            ? `<span class="ThanhTien">${item.thanhTien.toLocaleString('vi-VN')}</span>`
                            : `<span class="ThanhTien">0</span>`}
                        </td>
                        <td>${item.trangThai}</td>
                    </tr>`;
                    $('.tablethietbi tbody').append(row);
                });

                // Thêm hàng tổng tiền
                $('.tablethietbi tbody').append(`
                    <tr class="tong-tien-row">
                        <td colspan="8" style="text-align:center; font-weight:bold;">Tổng tiền:</td>
                        <td class="tong-tien" colspan="3" style="font-weight:bold;">0</td>
                    </tr>
                `);
                updateTongTien();
                attachEventHandlers();
            } else {
                $('.tablethietbi tbody').append(`
                    <tr>
                        <td colspan="11" style="text-align:center;">Không có dữ liệu vật tư.</td>
                    </tr>
                `);
                $('#action-buttons').hide();
            }

            let $rowToHighlight = $();
            $('.table tbody tr').each(function () {
                if ($(this).find('td').eq(1).text().trim() === Mamuahang) {
                    $rowToHighlight = $(this);
                    return false;
                }
            });
            applyPurchaseRowHighlight($rowToHighlight);
        },
        error: function (xhr, status, error) {
            alert("Không thể lấy dữ liệu vật tư. Lỗi: " + error);
        }
    });
}

// Gắn sự kiện cho bảng
function attachEventHandlers() {
    // Xử lý validation khi nhập giá - chỉ cho phép số dương
    $('.tablethietbi tbody').on('keypress', '.DonGia input', function (e) {
        // Cho phép các phím điều khiển: backspace (8), tab (9), enter (13), delete (46), escape (27)
        // Cho phép dấu chấm (46 hoặc 110 hoặc 190) và số (48-57)
        const keyCode = e.which || e.keyCode;
        const char = String.fromCharCode(keyCode);
        
        // Cho phép phím điều khiển
        if (keyCode === 8 || keyCode === 9 || keyCode === 13 || keyCode === 27 || keyCode === 46) {
            return true;
        }
        
        // Cho phép số (0-9)
        if (keyCode >= 48 && keyCode <= 57) {
            return true;
        }
        
        // Cho phép dấu chấm (.) nhưng chỉ một lần
        if ((keyCode === 46 || keyCode === 110 || keyCode === 190) && $(this).val().indexOf('.') === -1) {
            return true;
        }
        
        // Chặn tất cả các ký tự khác
        e.preventDefault();
        return false;
    });

    // Xử lý khi paste hoặc nhập - loại bỏ ký tự không hợp lệ
    $('.tablethietbi tbody').on('input paste', '.DonGia input', function (e) {
        let value = $(this).val();
        // Loại bỏ tất cả ký tự không phải số và dấu chấm
        value = value.replace(/[^0-9.]/g, '');
        // Loại bỏ nhiều dấu chấm, chỉ giữ lại một
        const parts = value.split('.');
        if (parts.length > 2) {
            value = parts[0] + '.' + parts.slice(1).join('');
        }
        // Đảm bảo không có số âm
        if (value.startsWith('-')) {
            value = value.replace('-', '');
        }
        $(this).val(value);
    });

    // Xử lý tính toán khi nhập giá
    $('.tablethietbi tbody').on('input', '.DonGia input', function () {
        const $row = $(this).closest('tr');
        const sl = parseFloat($(this).data('sl')) || 0;
        let inputValue = $(this).val().replace(/[^\d.]/g, '');
        const donGia = parseFloat(inputValue) || 0;
        
        // Đảm bảo giá không âm
        if (donGia < 0 || isNaN(donGia)) {
            $(this).val('0');
            return;
        }
        
        const thanhTien = sl * donGia;

        $row.find('.ThanhTien').text(thanhTien.toLocaleString('vi-VN'));    
        updateTongTien();
    });
}

// Cập nhật tổng tiền
function updateTongTien() {
    let tongTien = 0;

    $('.tablethietbi .ThanhTien').each(function () {
        const thanhTienText = $(this).text().trim();
        const thanhTien = parseInt(thanhTienText.replace(/[^\d]/g, '')) || 0;

        tongTien += thanhTien;
    });

    $('.tong-tien').text(tongTien.toLocaleString('vi-VN', { style: 'currency', currency: 'VND' }));
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

// Hàm in phiếu mua hàng
function inPhieuMuaHang() {
    if (!selectedMamuahang) {
        alert("Vui lòng chọn mã mua hàng trước khi in.");
        return;
    }
    
    const pathSegments = window.location.pathname.split('/');
    const area = pathSegments.length > 1 ? pathSegments[1] : '';
    const url = `/${area}/Yeucau/InPhieumuahang?MaMuahang=${selectedMamuahang}`;
    
    window.open(url, '_blank');
}

// Xử lý nút duyệt phiếu mua hàng
$(document).on('click', '#approvePhieumuahang', function() {
    if (!selectedMamuahang) {
        alert("Vui lòng chọn mã mua hàng trước khi duyệt.");
        return;
    }
    
    if (!confirm("Bạn có chắc chắn muốn duyệt phiếu mua hàng này?")) {
        return;
    }
    
    const pathSegments = window.location.pathname.split('/');
    const area = pathSegments.length > 1 ? pathSegments[1] : '';
    const url = `/${area}/Yeucau/XuLyPhieumuahang`;
    
    // Tạo form để submit
    const form = $('<form>', {
        method: 'POST',
        action: url
    });
    
    form.append($('<input>', {
        type: 'hidden',
        name: 'MaMuahang',
        value: selectedMamuahang
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

// Xử lý nút từ chối phiếu mua hàng
$(document).on('click', '#rejectPhieumuahang', function() {
    if (!selectedMamuahang) {
        alert("Vui lòng chọn mã mua hàng trước khi từ chối.");
        return;
    }
    
    if (!confirm("Bạn có chắc chắn muốn từ chối phiếu mua hàng này?")) {
        return;
    }
    
    const pathSegments = window.location.pathname.split('/');
    const area = pathSegments.length > 1 ? pathSegments[1] : '';
    const url = `/${area}/Yeucau/XuLyPhieumuahang`;
    
    // Tạo form để submit
    const form = $('<form>', {
        method: 'POST',
        action: url
    });
    
    form.append($('<input>', {
        type: 'hidden',
        name: 'MaMuahang',
        value: selectedMamuahang
    }));
    
    form.append($('<input>', {
        type: 'hidden',
        name: 'action',
        value: 'reject'
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