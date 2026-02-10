$(document).ready(function () {
    const pathSegments = window.location.pathname.split('/');
    const area = pathSegments.length > 1 ? pathSegments[1] : '';
    
    // Chỉ Trưởng BP mua hàng được phép "Gửi báo giá" (lưu DB theo nút gửi)
    if (area !== 'TruongBPMuahang') {
        $('#submitPhieumuahang').hide();
    }

    const firstRow = $('.table tbody tr').first();
    if (firstRow.length > 0) {
        const Mamuahang = firstRow.data('mamuahang');
        const link = firstRow.find('td').eq(2).find('a');
        const trangThai = firstRow.data('trangthai') || (link.length ? link.data('trangthai') : '') || '';
        if (Mamuahang) {
            showVTmuahang(Mamuahang, trangThai);
        }
    }
    if (typeof getThongbaoData === 'function') {
        getThongbaoData();
    }
    setActiveMenu();

    // Filter Mã VT cho bảng chi tiết
    $(document).on('input', '#filterMavt', function () {
        applyFilterMavt();
    });
    
    // Xử lý click vào hàng
    $(document).on('click', '.clickable-row', function() {
        const $row = $(this);
        const MaMuahang = $row.data('mamuahang');
        const link = $row.find('td').eq(2).find('a');
        const trangThai = $row.data('trangthai') || (link.length ? link.data('trangthai') : '') || '';
        if (MaMuahang) {
            showVTmuahang(MaMuahang, trangThai);
        }
    });

    // Excel-like column filters (list + detail)
    if (window.ExcelTableFilter) {
        window.ExcelTableFilter.init('.table', { excludeHeaderTexts: ['Thao tác'] });
        window.ExcelTableFilter.init('.tablethietbi', { excludeHeaderTexts: ['Thao tác'] });
    }
});

const ROW_HIGHLIGHT_COLOR = "#2d9f3c";
const ROW_HIGHLIGHT_TEXT_COLOR = "#ffffff";

function escapeHtml(input) {
    if (input === null || input === undefined) return '';
    return String(input).replace(/[&<>"']/g, function (ch) {
        switch (ch) {
            case '&': return '&amp;';
            case '<': return '&lt;';
            case '>': return '&gt;';
            case '"': return '&quot;';
            case "'": return '&#39;';
            default: return ch;
        }
    });
}

/** Dòng con chia lô = copy nguyên dòng gốc; SL, Đơn giá, Ngày có hàng là input (mỗi đợt có thể giá khác). readOnly = true khi đã gửi báo giá: chỉ hiển thị, không input. */
function buildSplitRowHtml(maSanpham, opts) {
    const ten = (opts.ten || '').toString().trim();
    const ma = (maSanpham || '').toString().trim();
    const hang = (opts.hang || '').toString().trim();
    const ncc = (opts.ncc || '').toString().trim();
    const donVi = (opts.donVi || 'Không xác định').toString().trim();
    const slValue = (opts.slValue != null && opts.slValue !== '') ? String(opts.slValue) : '';
    const ngayValue = (opts.ngayValue != null && opts.ngayValue !== '') ? String(opts.ngayValue) : '';
    const ngayTTValue = (opts.ngayThanhToanValue != null && opts.ngayThanhToanValue !== '') ? String(opts.ngayThanhToanValue) : '';
    const donGiaDisplay = (opts.donGiaValue != null && opts.donGiaValue !== '' && Number(opts.donGiaValue) > 0)
        ? String(Number(opts.donGiaValue)).replace(/\B(?=(\d{3})+(?!\d))/g, '.') : '';
    const thanhTienDisplay = (opts.thanhTienValue != null && opts.thanhTienValue !== '' && Number(opts.thanhTienValue) > 0)
        ? Number(opts.thanhTienValue).toLocaleString('vi-VN') : '0';
    const esc = escapeHtml;
    const readOnly = !!opts.readOnly;
    if (readOnly) {
        return `<tr class="vt-split-row vt-split-row-readonly" data-parent="${esc(ma)}">
            <td style="color:#666;">↳</td>
            <td>${esc(ten)}</td>
            <td>${esc(ma)}</td>
            <td>${esc(hang)}</td>
            <td>${esc(ncc) || '-'}</td>
            <td>${esc(slValue) || '-'}</td>
            <td>${esc(donVi)}</td>
            <td>${esc(donGiaDisplay) || '-'}</td>
            <td>${esc(thanhTienDisplay)}</td>
            <td>${esc(ngayTTValue) || '-'}</td>
            <td>${esc(ngayValue) || '-'}</td>
            <td></td>
            <td></td>
        </tr>`;
    }
    return `<tr class="vt-split-row" data-parent="${esc(ma)}">
        <td style="color:#666;">↳</td>
        <td>${esc(ten)}</td>
        <td>${esc(ma)}</td>
        <td>${esc(hang)}</td>
        <td>${esc(ncc) || '-'}</td>
        <td style="white-space:nowrap;">
            <input type="text" class="SplitSL" placeholder="SL" style="width: 56px;" value="${esc(slValue)}" />
            <button type="button" class="btn-split-remove" title="Xóa dòng">−</button>
        </td>
        <td>${esc(donVi)}</td>
        <td style="white-space:nowrap;">
            <input type="text" class="SplitDonGia" placeholder="Nhập giá" style="width: 90px;" value="${esc(donGiaDisplay)}" />
        </td>
        <td class="SplitThanhTien">0</td>
        <td style="white-space:nowrap;">
            <input type="text" class="SplitNgayThanhToan" placeholder="dd/MM/yyyy" style="width: 100px;" value="${esc(ngayTTValue)}" />
        </td>
        <td style="white-space:nowrap;">
            <input type="text" class="SplitNgay" placeholder="dd/MM/yyyy" style="width: 110px;" value="${esc(ngayValue)}" />
        </td>
        <td></td>
        <td></td>
    </tr>`;
}

function fetchNhaCCGoiY(q, $dropdown, $input) {
    $('.ncc-dropdown').hide();
    const url = '/TruongBPMuahang/NhaCungCap/GetNhaCCGoiY?q=' + encodeURIComponent(q || '');
    $.ajax({
        url: url,
        method: 'GET',
        success: function (data) {
            if (Array.isArray(data) && data.length > 0) {
                let html = '';
                data.forEach(function (item) {
                    const val = (item || '').toString();
                    if (val) {
                        html += '<div class="ncc-suggestion-item" data-value="' + escapeHtml(val) + '" style="padding: 8px 12px; cursor: pointer; border-bottom: 1px solid #eee;" onmouseover="this.style.backgroundColor=\'#f0f7ff\'" onmouseout="this.style.backgroundColor=\'\'">' + escapeHtml(val) + '</div>';
                    }
                });
                $dropdown.html(html).show();
            } else {
                $dropdown.html('<div style="padding: 12px; color: #666;">Không có gợi ý</div>').show();
            }
        },
        error: function () {
            $dropdown.html('<div style="padding: 12px; color: #c00;">Lỗi tải gợi ý</div>').show();
        }
    });
}

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

    // Validate chia lô trước khi submit
    let splitInvalid = false;
    $('.tablethietbi tbody tr.vt-data-row').each(function () {
        const $row = $(this);
        const ok = validateSplitForItem($row, { showAlert: false });
        if (!ok) {
            splitInvalid = true;
            return false; // break
        }
    });
    if (splitInvalid) {
        alert('Có vật tư đang chia lô vượt quá số lượng gốc. Vui lòng chỉnh lại trước khi gửi.');
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
                const key = makeDraftKey(selectedMamuahang, MaSanpham);
                savedInputValues[key] = inputValue;
                // Lưu vào biến global để restore khi rebuild table
                if (inputValue) {
                    savedInputValuesForRestore[key] = inputValue;
                }
            }
        }
    });

    const vtmuahangData = [];
    const vtSplitsPayload = [];
    let itemsWithoutPrice = [];
    let pricedItemsCount = 0;
    let invalidDateInput = false;
    
    $('.tablethietbi tbody tr').each(function () {
        // Bỏ qua hàng tổng tiền
        if ($(this).hasClass('tong-tien-row')) {
            return;
        }
        // Bỏ qua các dòng chia lô (dòng con)
        if ($(this).hasClass('vt-split-row')) {
            return;
        }
        
        const cells = $(this).find('td');
        const priceInput = $(this).find('.DonGia input');
        
        // Chỉ xử lý các hàng có input giá (có thể nhập giá)
        if (cells.length >= 2 && priceInput.length > 0) {
            const inputValue = priceInput.val();
            // Xử lý giá trị có thể chứa dấu chấm hoặc dấu phẩy
            // Với định dạng VNĐ, chỉ giữ lại chữ số (bỏ dấu . ngăn cách hàng nghìn)
            let cleanValue = inputValue ? inputValue.replace(/[^\d]/g, '') : '';
            const DonGia = cleanValue ? parseInt(cleanValue, 10) || 0 : 0;
            const SL = parseFloat(priceInput.data('sl')) || 0;
            
            // Lấy MaSanpham từ cột thứ 3 (index 2) trong bảng
            const MaSanpham = $(this).find('td').eq(2).text().trim();
            const TenSanpham = $(this).find('td').eq(1).text().trim();

            // Nếu số lượng = 0, không cần nhập, bỏ qua
            if (SL === 0) {
                return;
            }

            // Lấy thêm dữ liệu nhập (NCC/Ngày/Ghi chú) để chỉ lưu DB khi bấm Gửi
            const nhaCC = ($(this).find('.NhaCCInput').val() || '').toString().trim();
            const ngayThanhToanDisplay = ($(this).find('.NgayThanhToanInput').val() || '').toString().trim();
            const ngayCoHangDisplayRaw = ($(this).find('.NgayCoHangInput').val() || '').toString().trim();
            const ghiChuRaw = ($(this).find('.GhiChuInput').val() || '').toString();

            // Nếu có chia lô: build lịch giao hàng từ các dòng con
            let ngayCoHangDisplay = ngayCoHangDisplayRaw;
            let ghiChu = ghiChuRaw;
            const $parentRow = $(this);
            const splitState = getSplitStateForItem($parentRow);
            if (splitState && splitState.maSanpham && splitState.splits && splitState.splits.length > 0) {
                const valid = validateSplitForItem($parentRow, { showAlert: true });
                if (!valid) {
                    invalidDateInput = true;
                    return false; // break
                }

                // Lọc các dòng có SL > 0 và có ngày hợp lệ (đơn giá từng đợt tùy chọn)
                const normalized = splitState.splits
                    .map(x => ({
                        sl: x.sl || 0,
                        ngay: normalizeDisplayDateStr(x.ngay),
                        donGia: x.donGia != null && x.donGia > 0 ? x.donGia : null
                    }))
                    .filter(x => x.sl > 0 && x.ngay);

                if (normalized.length === 0) {
                    alert(`Bạn đã tạo chia lô cho vật tư ${TenSanpham || MaSanpham} nhưng chưa nhập đủ SL và Ngày có hàng.`);
                    invalidDateInput = true;
                    return false; // break
                }

                // Set Ngày có hàng = ngày sớm nhất (để server có dữ liệu 1 ngày đại diện)
                normalized.sort((a, b) => compareDisplayDates(a.ngay, b.ngay));
                ngayCoHangDisplay = normalized[0].ngay;

                // Build payload lịch có hàng để lưu DB (mỗi đợt có thể có đơn giá riêng)
                vtSplitsPayload.push({
                    maSanpham: MaSanpham,
                    lines: normalized.map(x => ({
                        sl: x.sl,
                        ngayCoHang: convertDisplayDateToServerGlobal(x.ngay),
                        donGia: x.donGia
                    }))
                });
            } else {
                // Nếu trước đó đã có lịch (load từ DB) nhưng giờ user xóa hết -> gửi empty để xóa DB
                const hadSplit = ($parentRow.data('hasSplit') || '') === '1';
                if (hadSplit) {
                    vtSplitsPayload.push({
                        maSanpham: MaSanpham,
                        lines: []
                    });
                }
            }

            // Validate ngày nếu người dùng nhập tay
            let ngayThanhToan = '';
            if (ngayThanhToanDisplay) {
                ngayThanhToan = convertDisplayDateToServerGlobal(ngayThanhToanDisplay);
                if (!ngayThanhToan) {
                    alert(`Ngày thanh toán không hợp lệ ở vật tư ${TenSanpham || MaSanpham}. Vui lòng nhập theo dd/MM/yyyy.`);
                    invalidDateInput = true;
                    return false; // break $.each
                }
            }
            let ngayCoHang = '';
            if (ngayCoHangDisplay) {
                ngayCoHang = convertDisplayDateToServerGlobal(ngayCoHangDisplay);
                if (!ngayCoHang) {
                    alert(`Ngày có hàng không hợp lệ ở vật tư ${TenSanpham || MaSanpham}. Vui lòng nhập theo dd/MM/yyyy.`);
                    invalidDateInput = true;
                    return false; // break $.each
                }
            }

            const payloadItem = {
                MaMuahang: selectedMamuahang,
                MaSanpham: MaSanpham
            };

            if (DonGia > 0 && SL > 0) {
                const ThanhTien = SL * DonGia;
                payloadItem.DonGia = DonGia;
                payloadItem.ThanhTien = ThanhTien;
                pricedItemsCount++;
            } else if (SL > 0) {
                // Ghi nhận các mục chưa có giá (để thông báo)
                itemsWithoutPrice.push({
                    ten: TenSanpham || MaSanpham,
                    ma: MaSanpham,
                    sl: SL
                });
            }

            if (nhaCC) payloadItem.NhaCC = nhaCC;
            if (ngayThanhToan) payloadItem.NgayThanhToanBPMuahang = ngayThanhToan;
            if (ngayCoHang) payloadItem.NgayCoHang = ngayCoHang;
            if (ghiChu && ghiChu.trim()) payloadItem.GhiChuBPMuahang = ghiChu.trim();

            // Chỉ gửi các dòng có dữ liệu (giá hoặc các trường khác)
            if (
                payloadItem.DonGia != null ||
                payloadItem.NhaCC != null ||
                payloadItem.NgayThanhToanBPMuahang != null ||
                payloadItem.NgayCoHang != null ||
                payloadItem.GhiChuBPMuahang != null
            ) {
                vtmuahangData.push(payloadItem);
            }
        }
    });

    if (invalidDateInput) {
        return;
    }
    
    // Kiểm tra nếu không có dữ liệu nào để gửi
    if (pricedItemsCount === 0) {
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
        VTphieumuahang: vtmuahangData,
        VTphieumuahangSplits: vtSplitsPayload
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
                        const key = makeDraftKey(selectedMamuahang, MaSanpham);
                        if (MaSanpham && savedInputValues[key] !== undefined) {
                            priceInput.val(savedInputValues[key]);
                            // Lưu lại vào biến global để restore khi rebuild table
                            savedInputValuesForRestore[key] = savedInputValues[key];
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
                    const key = makeDraftKey(selectedMamuahang, MaSanpham);
                    if (MaSanpham && savedInputValues[key] !== undefined) {
                        priceInput.val(savedInputValues[key]);
                        // Lưu lại vào biến global để restore khi rebuild table
                        savedInputValuesForRestore[key] = savedInputValues[key];
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

// Draft nhập liệu theo từng vật tư (chỉ dùng để giữ dữ liệu trên UI trước khi bấm "Gửi báo giá")
// Key: `${MaMuahang}__${MaSanpham}`
const phieuMuaHangDraft = {};
function makeDraftKey(maMuahang, maSanpham) {
    return `${maMuahang || ''}__${maSanpham || ''}`;
}
function getDraft(maMuahang, maSanpham) {
    return phieuMuaHangDraft[makeDraftKey(maMuahang, maSanpham)] || {};
}
function setDraft(maMuahang, maSanpham, patch) {
    const key = makeDraftKey(maMuahang, maSanpham);
    phieuMuaHangDraft[key] = Object.assign({}, phieuMuaHangDraft[key] || {}, patch || {});
}

// Lọc bảng chi tiết VT theo Mã VT
function applyFilterMavt() {
    var v = ($('#filterMavt').val() || '').trim().toLowerCase();
    $('.tablethietbi tbody tr.vt-data-row').each(function () {
        var mavt = ($(this).data('mavt') || '').toString();
        const show = (!v || mavt.indexOf(v) >= 0);
        $(this).toggle(show);
        // Toggle các dòng chia lô đi theo dòng cha
        const maSanpham = ($(this).data('masanpham') || '').toString();
        if (maSanpham) {
            $(`.tablethietbi tbody tr.vt-split-row[data-parent="${CSS.escape(maSanpham)}"]`).toggle(show);
        }
    });
    updateTongTien();
}

function parseVNNumber(input) {
    if (input == null) return 0;
    const raw = String(input).replace(/[^\d]/g, '');
    return raw ? (parseInt(raw, 10) || 0) : 0;
}

function normalizeDisplayDateStr(s) {
    // chấp nhận dd/MM/yyyy (1-2 chữ số) -> pad 2
    if (!s) return '';
    const m = String(s).trim().match(/^(\d{1,2})\/(\d{1,2})\/(\d{4})$/);
    if (!m) return '';
    const dd = m[1].padStart(2, '0');
    const mm = m[2].padStart(2, '0');
    return `${dd}/${mm}/${m[3]}`;
}

function compareDisplayDates(a, b) {
    // a,b: dd/MM/yyyy -> so sánh theo thời gian
    const na = normalizeDisplayDateStr(a);
    const nb = normalizeDisplayDateStr(b);
    if (!na && !nb) return 0;
    if (!na) return 1;
    if (!nb) return -1;
    const [d1, m1, y1] = na.split('/').map(x => parseInt(x, 10));
    const [d2, m2, y2] = nb.split('/').map(x => parseInt(x, 10));
    const ta = new Date(y1, m1 - 1, d1).getTime();
    const tb = new Date(y2, m2 - 1, d2).getTime();
    return ta - tb;
}

function getSplitStateForItem($parentRow) {
    const maSanpham = ($parentRow.data('masanpham') || '').toString();
    const slGoc = parseFloat($parentRow.data('sl')) || 0;
    const splits = [];
    if (!maSanpham) return { maSanpham: '', slGoc, splits };
    $(`.tablethietbi tbody tr.vt-split-row[data-parent="${CSS.escape(maSanpham)}"]`).each(function () {
        const $r = $(this);
        const sl = parseVNNumber($r.find('.SplitSL').val());
        const ngay = normalizeDisplayDateStr($r.find('.SplitNgay').val());
        const donGiaRaw = ($r.find('.SplitDonGia').val() || '').toString().replace(/[^\d]/g, '');
        const donGia = donGiaRaw ? (parseInt(donGiaRaw, 10) || null) : null;
        splits.push({ sl, ngay, donGia });
    });
    return { maSanpham, slGoc, splits };
}

function validateSplitForItem($parentRow, { showAlert } = { showAlert: false }) {
    const state = getSplitStateForItem($parentRow);
    if (!state.maSanpham) return true;
    if (!state.splits.length) {
        // không chia lô -> hiện nút + để có thể thêm
        $parentRow.find('.split-hint').remove();
        $parentRow.find('.NgayCoHangInput').prop('disabled', false);
        $parentRow.find('.btn-split-add').show();
        return true;
    }

    const sum = state.splits.reduce((acc, x) => acc + (x.sl || 0), 0);
    const ok = sum <= state.slGoc;

    // Đủ rồi (sum >= slGoc) thì ẩn nút +, còn dư thì vẫn hiện
    if (sum >= state.slGoc) {
        $parentRow.find('.btn-split-add').hide();
    } else {
        $parentRow.find('.btn-split-add').show();
    }

    // hint còn lại
    const remain = Math.max(0, state.slGoc - sum);
    const hintText = ok
        ? `Đã chia: ${sum}/${state.slGoc} (còn ${remain})`
        : `Vượt SL: ${sum}/${state.slGoc}`;
    const $slCell = $parentRow.find('td.sl-cell').length ? $parentRow.find('td.sl-cell') : $parentRow.find('td').eq(5);
    $slCell.find('.split-hint').remove();
    $slCell.append(`<span class="split-hint" style="${ok ? '' : 'color:#c00;font-weight:600;'}">${hintText}</span>`);

    // Khi chia lô thì khóa input Ngày có hàng ở dòng cha (tránh nhập 2 nơi)
    $parentRow.find('.NgayCoHangInput').prop('disabled', true);

    if (!ok && showAlert) {
        const ten = ($parentRow.find('td').eq(1).text() || '').trim();
        const ma = ($parentRow.find('td').eq(2).text() || '').trim();
        alert(`Tổng SL chia lô của "${ten || ma}" đang vượt quá SL gốc (${state.slGoc}). Vui lòng chỉnh lại.`);
    }
    return ok;
}

function updateSplitThanhTien($row) {
    if (!$row || !$row.length) return;
    const sl = parseVNNumber($row.find('.SplitSL').val());
    const raw = ($row.find('.SplitDonGia').val() || '').toString().replace(/[^\d]/g, '');
    const donGia = raw ? (parseInt(raw, 10) || 0) : 0;
    const tt = (sl && donGia > 0) ? (sl * donGia) : 0;
    $row.find('.SplitThanhTien').text(tt > 0 ? tt.toLocaleString('vi-VN') : '0');
}

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
            
            // Hiển thị header text và bảng chi tiết cho tất cả areas (kể cả khi đã ấn nút thu nhỏ trước đó)
            $('.bodyyeucau-thietbi').show();
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
                // Đảm bảo action-buttons cũng được hiển thị
                $('#action-buttons').show();
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
                        // Lấy từ cột trạng thái (cột thứ 8, index 7)
                        const trangThaiCell = $(this).find('td').eq(7);
                        trangThaiFromTable = trangThaiCell.text().trim();
                        return false;
                    }
                });
                
                // Nếu không tìm thấy từ bảng, dùng trạng thái từ tham số
                if (!trangThaiFromTable && trangThaiPhieu) {
                    trangThaiFromTable = trangThaiPhieu;
                }
                
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
                        // Lấy từ cột trạng thái (cột thứ 8, index 7)
                        const trangThaiCell = $(this).find('td').eq(7);
                        trangThaiFromTable = trangThaiCell.text().trim();
                        return false;
                    }
                });
                
                // Nếu không tìm thấy từ bảng, dùng trạng thái từ tham số
                if (!trangThaiFromTable && trangThaiPhieu) {
                    trangThaiFromTable = trangThaiPhieu;
                }
                
                // Cho Trưởng BP mua hàng: hiển thị khi trạng thái = "Chờ thanh toán" (duyệt thanh toán) hoặc "Đã thanh toán" (nhận hàng)
                if (trangThaiFromTable === 'Chờ thanh toán' || trangThaiFromTable === 'Đã thanh toán') {
                    $('#approvePhieumuahang').show();
                    $('#rejectPhieumuahang').show();
                    $('#action-buttons').show();
                } else {
                    // Ẩn nút duyệt/từ chối
                    $('#approvePhieumuahang').hide();
                    $('#rejectPhieumuahang').hide();
                    // Hiển thị action-buttons nếu nút "Gửi báo giá" đang visible
                    if ($('#submitPhieumuahang').is(':visible')) {
                        $('#action-buttons').show();
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
                // Chỉ bộ phận mua hàng (Trưởng BP + Nhân viên mua hàng) được nhập dữ liệu chi tiết
                const isPurchaseArea = (area === 'TruongBPMuahang' || area === 'NhanvienMuahang');
                const canInputPriceForPhieu = isPurchaseArea && 
                    (trangThaiPhieu === 'Đang chờ báo giá' || (trangThaiPhieu && trangThaiPhieu.includes('Đã từ chối')));
                const isGiamdoc = area === 'Giamdoc';
                // Đã gửi báo giá: ẩn nút + và dòng con chỉ hiển thị (không input)
                const isDaGuiBaoGia = trangThaiPhieu && trangThaiPhieu !== 'Đang chờ báo giá' && !(trangThaiPhieu && trangThaiPhieu.includes('Đã từ chối'));
                // BP Mua hàng chỉ có thể chỉnh sửa khi chưa gửi báo giá (trạng thái = "Đang chờ báo giá" hoặc "Đã từ chối")
                // Giám đốc chỉ có thể chỉnh sửa khi trạng thái = "Đã báo giá" (chưa duyệt), sau khi duyệt (trạng thái = "Chờ thanh toán" trở đi) thì không cho sửa
                const canEditNgayThanhToanForBPMuahang = isPurchaseArea && 
                    (trangThaiPhieu === 'Đang chờ báo giá' || (trangThaiPhieu && trangThaiPhieu.includes('Đã từ chối')));
                // Giám đốc chỉ được chỉnh sửa khi trạng thái = "Đã báo giá", các trạng thái sau khi duyệt (Chờ thanh toán, Đã thanh toán, Đã nhận hàng) thì không cho sửa
                const trangThaiKhongChoPhepSuaGiamdoc = ['Chờ thanh toán', 'Đã thanh toán', 'Đã nhận hàng'];
                const canEditNgayThanhToanForGiamdoc = isGiamdoc && 
                    trangThaiPhieu === 'Đã báo giá' && 
                    !trangThaiKhongChoPhepSuaGiamdoc.includes(trangThaiPhieu);
                const canEditNgayThanhToan = canEditNgayThanhToanForGiamdoc || canEditNgayThanhToanForBPMuahang;

                let STT = 1;
                data.forEach(function (item) {
                    const maSanpham = (item.maSanpham || item.MaSanpham || '').toString().trim();
                    const draft = getDraft(Mamuahang, maSanpham);

                    // Cho phép nhập giá cho từng mục nếu:
                    // 1. Area là mua hàng VÀ
                    // 2. (Trạng thái phiếu = "Đang chờ báo giá" HOẶC mục này có trạng thái "Đang chờ báo giá")
                    const itemTrangThai = (item.trangThai || '').trim();
                    const canInputPriceForItem = isPurchaseArea && 
                        (canInputPriceForPhieu || itemTrangThai === 'Đang chờ báo giá');

                    // BP mua hàng được nhập Nhà cung cấp (NCC) cho từng vật tư khi:
                    // - Phiếu đang chờ báo giá / bị từ chối, hoặc
                    // - Chính vật tư này đang chờ báo giá (trường hợp báo giá một phần)
                    const nhaCCValueRaw = (item.nhaCC != null ? item.nhaCC : (item.NhaCC != null ? item.NhaCC : ''));
                    const nhaCCValue =
                        Object.prototype.hasOwnProperty.call(draft, 'nhaCC')
                            ? (draft.nhaCC || '').toString()
                            : (nhaCCValueRaw || '').toString();
                    const canEditNhaCCForItem = isPurchaseArea &&
                        (canInputPriceForPhieu || itemTrangThai === 'Đang chờ báo giá');
                    const nhaCCCell = canEditNhaCCForItem
                        ? `<div class="ncc-input-wrapper" style="position: relative; display: flex; align-items: center;">
                            <input type="text"
                                   class="NhaCCInput"
                                   data-mamuahang="${escapeHtml(Mamuahang)}"
                                   data-masanpham="${escapeHtml(maSanpham)}"
                                   value="${escapeHtml(nhaCCValue)}"
                                   placeholder="Nhập NCC"
                                   style="flex: 1; padding-right: 28px;" />
                            <button type="button" class="ncc-goi-y-btn" title="Gợi ý nhà cung cấp" style="position: absolute; right: 4px; background: none; border: none; cursor: pointer; padding: 4px; color: #666;">
                                <i class='bx bx-list-check' style="font-size: 18px;"></i>
                            </button>
                            <div class="ncc-dropdown" style="display: none; position: absolute; top: 100%; left: 0; right: 0; max-height: 200px; overflow-y: auto; background: white; border: 1px solid #ccc; border-radius: 4px; box-shadow: 0 4px 12px rgba(0,0,0,0.15); z-index: 1000;"></div>
                           </div>`
                        : (nhaCCValue ? escapeHtml(nhaCCValue) : '-');
                    
                    let donGiaCell = '';
                    // Kiểm tra xem có giá trị đã lưu không (để restore khi rebuild table)
                    const savedValue = savedInputValuesForRestore[makeDraftKey(Mamuahang, maSanpham)];
                    const displayValue =
                        (draft.donGiaDisplay !== undefined)
                            ? draft.donGiaDisplay
                            : (savedValue !== undefined ? savedValue : (item.donGia != null ? item.donGia : null));
                    
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

                    // Chuẩn bị ô Ngày thanh toán - hiển thị cả hai giá trị
                    const formatDate = (dateRaw) => {
                        if (!dateRaw) return '';
                        const d = new Date(dateRaw);
                        if (isNaN(d)) return '';
                        const mm = String(d.getMonth() + 1).padStart(2, '0');
                        const dd = String(d.getDate()).padStart(2, '0');
                        const yyyy = d.getFullYear();
                        // Trả về chuỗi phục vụ gửi server (yyyy-MM-dd)
                        return `${yyyy}-${mm}-${dd}`;
                    };
                    
                    const formatDateDisplay = (dateRaw) => {
                        if (!dateRaw) return '';
                        const d = new Date(dateRaw);
                        if (isNaN(d)) return '';
                        const mm = String(d.getMonth() + 1).padStart(2, '0');
                        const dd = String(d.getDate()).padStart(2, '0');
                        const yyyy = d.getFullYear();
                        return `${dd}/${mm}/${yyyy}`;
                    };

                    const ngayThanhToanBPMuahangRaw = item.ngayThanhToanBPMuahang || item.NgayThanhToanBPMuahang;
                    const ngayThanhToanGiamdocRaw = item.ngayThanhToanGiamdoc || item.NgayThanhToanGiamdoc;
                    
                    let ngayThanhToanCell = '';
                    if (canEditNgayThanhToan) {
                        // Hiển thị input cho người dùng hiện tại và hiển thị giá trị của bên kia
                        if (isGiamdoc) {
                            // Giám đốc: hiển thị input của mình và giá trị BP Mua hàng
                            const ngayThanhToanGiamdocValue = formatDateDisplay(ngayThanhToanGiamdocRaw);
                            const ngayThanhToanBPMuahangDisplay = formatDateDisplay(ngayThanhToanBPMuahangRaw);
                            ngayThanhToanCell = `
                                <div style="display: flex; flex-direction: column; gap: 4px;">
                                    <input type="text"
                                           class="NgayThanhToanInput"
                                           data-mamuahang="${Mamuahang}"
                                           data-masanpham="${item.maSanpham || ''}"
                                           value="${ngayThanhToanGiamdocValue}"
                                           placeholder="dd/MM/yyyy"
                                           style="width: 100%;" />
                                    ${ngayThanhToanBPMuahangDisplay ? `<small style="color: #c00; font-size: 11px; font-weight: bold;">BP Mua hàng: Ngày ${ngayThanhToanBPMuahangDisplay} cần thanh toán</small>` : ''}
                                </div>`;
                        } else if (isPurchaseArea) {
                            // BP Mua hàng: hiển thị input của mình và giá trị Giám đốc
                            const ngayThanhToanBPMuahangValue =
                                (draft.ngayThanhToanDisplay !== undefined)
                                    ? (draft.ngayThanhToanDisplay || '')
                                    : formatDateDisplay(ngayThanhToanBPMuahangRaw);
                            const ngayThanhToanGiamdocDisplay = formatDateDisplay(ngayThanhToanGiamdocRaw);
                            ngayThanhToanCell = `
                                <div style="display: flex; flex-direction: column; gap: 4px;">
                                    <input type="text"
                                           class="NgayThanhToanInput"
                                           data-mamuahang="${Mamuahang}"
                                           data-masanpham="${maSanpham}"
                                           value="${ngayThanhToanBPMuahangValue}"
                                           placeholder="dd/MM/yyyy"
                                           style="width: 100%;" />
                                    ${ngayThanhToanGiamdocDisplay ? `<small style="color: #1565c0; font-size: 11px; font-weight: bold;">Giám đốc: Ngày ${ngayThanhToanGiamdocDisplay} cần thanh toán</small>` : ''}
                                </div>`;
                        }
                    } else {
                        // Chỉ hiển thị (read-only)
                        const ngayThanhToanBPMuahangDisplay = formatDateDisplay(ngayThanhToanBPMuahangRaw);
                        const ngayThanhToanGiamdocDisplay = formatDateDisplay(ngayThanhToanGiamdocRaw);
                        let displayText = [];
                        if (ngayThanhToanBPMuahangDisplay) displayText.push(`<strong style="color: #c00;">BP Mua hàng: Ngày ${ngayThanhToanBPMuahangDisplay} cần thanh toán</strong>`);
                        if (ngayThanhToanGiamdocDisplay) displayText.push(`<strong style="color: #1565c0;">Giám đốc: Ngày ${ngayThanhToanGiamdocDisplay} cần thanh toán</strong>`);
                        ngayThanhToanCell = displayText.length > 0 ? displayText.join('<br>') : '-';
                    }

                    // Chuẩn bị ô Ghi chú - hiển thị cả hai giá trị
                    // BP Mua hàng chỉ có thể chỉnh sửa khi chưa gửi báo giá
                    // Giám đốc chỉ có thể chỉnh sửa khi trạng thái = "Đã báo giá" (chưa duyệt), sau khi duyệt (trạng thái = "Chờ thanh toán" trở đi) thì không cho sửa
                    const canEditGhiChuForBPMuahang = isPurchaseArea && 
                        (trangThaiPhieu === 'Đang chờ báo giá' || (trangThaiPhieu && trangThaiPhieu.includes('Đã từ chối')));
                    // Giám đốc chỉ được chỉnh sửa khi trạng thái = "Đã báo giá", các trạng thái sau khi duyệt (Chờ thanh toán, Đã thanh toán, Đã nhận hàng) thì không cho sửa
                    const trangThaiKhongChoPhepSuaGiamdocGhiChu = ['Chờ thanh toán', 'Đã thanh toán', 'Đã nhận hàng'];
                    const canEditGhiChuForGiamdoc = isGiamdoc && 
                        trangThaiPhieu === 'Đã báo giá' && 
                        !trangThaiKhongChoPhepSuaGiamdocGhiChu.includes(trangThaiPhieu);
                    const canEditGhiChu = canEditGhiChuForGiamdoc || canEditGhiChuForBPMuahang;
                    const ghiChuBPMuahangValue =
                        (draft.ghiChu !== undefined)
                            ? (draft.ghiChu || '')
                            : (item.ghiChuBPMuahang || item.GhiChuBPMuahang || '');
                    const ghiChuGiamdocValue = item.ghiChuGiamdoc || item.GhiChuGiamdoc || '';
                    // Kiểm tra nếu ghi chú BP Mua hàng chứa "Ngày 29/1 thanh toán ạ" thì dùng màu đỏ
                    const containsPaymentNote = ghiChuBPMuahangValue && ghiChuBPMuahangValue.includes('Ngày 29/1 thanh toán ạ');
                    const ghiChuBPMuahangColor = containsPaymentNote ? '#c00' : '#666';
                    let ghiChuCell = '';
                    if (canEditGhiChu) {
                        // Hiển thị input cho người dùng hiện tại và hiển thị ghi chú của bên kia
                        if (isGiamdoc) {
                            // Giám đốc: hiển thị input của mình và ghi chú BP Mua hàng
                            ghiChuCell = `
                                <div style="display: flex; flex-direction: column; gap: 4px;">
                                    <input type="text"
                                           class="GhiChuInput"
                                           data-mamuahang="${Mamuahang}"
                                           data-masanpham="${item.maSanpham || ''}"
                                           value="${ghiChuGiamdocValue}"
                                           placeholder="Nhập ghi chú" 
                                           style="width: 100%;" />
                                    ${ghiChuBPMuahangValue ? `<small style="color: ${ghiChuBPMuahangColor}; font-size: 11px;">BP Mua hàng: ${ghiChuBPMuahangValue}</small>` : ''}
                                </div>`;
                        } else if (isPurchaseArea) {
                            // BP Mua hàng: hiển thị input của mình và ghi chú Giám đốc
                            ghiChuCell = `
                                <div style="display: flex; flex-direction: column; gap: 4px;">
                                    <input type="text"
                                           class="GhiChuInput"
                                           data-mamuahang="${Mamuahang}"
                                           data-masanpham="${maSanpham}"
                                           value="${ghiChuBPMuahangValue}"
                                           placeholder="Nhập ghi chú" 
                                           style="width: 100%;" />
                                    ${ghiChuGiamdocValue ? `<small style="color: #1565c0; font-size: 11px; font-weight: bold;">Giám đốc: ${ghiChuGiamdocValue}</small>` : ''}
                                </div>`;
                        }
                    } else {
                        // Chỉ hiển thị (read-only)
                        let displayText = [];
                        if (ghiChuBPMuahangValue) {
                            const containsPaymentNote = ghiChuBPMuahangValue.includes('Ngày 29/1 thanh toán ạ');
                            const ghiChuBPMuahangColor = containsPaymentNote ? '#c00' : '';
                            const styleAttr = ghiChuBPMuahangColor ? ` style="color: ${ghiChuBPMuahangColor};"` : '';
                            displayText.push(`<span${styleAttr}>BP Mua hàng: ${ghiChuBPMuahangValue}</span>`);
                        }
                        if (ghiChuGiamdocValue) displayText.push(`<strong style="color: #1565c0;">Giám đốc: ${ghiChuGiamdocValue}</strong>`);
                        ghiChuCell = displayText.length > 0 ? displayText.join('<br>') : '-';
                    }

                    // Chuẩn bị ô Ngày có hàng - chỉ BP Mua hàng được chọn, các bộ phận khác chỉ xem
                    // BP Mua hàng chỉ có thể chỉnh sửa khi chưa gửi báo giá
                    const canEditNgayCoHang = isPurchaseArea && 
                        (trangThaiPhieu === 'Đang chờ báo giá' || (trangThaiPhieu && trangThaiPhieu.includes('Đã từ chối')));
                    const ngayCoHangRaw = item.ngayCoHang || item.NgayCoHang;
                    let ngayCoHangCell = '';
                    if (canEditNgayCoHang) {
                        // BP Mua hàng: hiển thị input để chọn ngày
                        const ngayCoHangValue =
                            (draft.ngayCoHangDisplay !== undefined)
                                ? (draft.ngayCoHangDisplay || '')
                                : formatDateDisplay(ngayCoHangRaw);
                        ngayCoHangCell = `
                            <input type="text"
                                   class="NgayCoHangInput"
                                   data-mamuahang="${Mamuahang}"
                                   data-masanpham="${maSanpham}"
                                   value="${ngayCoHangValue}"
                                   placeholder="dd/MM/yyyy"
                                   style="width: 100%;" />`;
                    } else {
                        // Các bộ phận khác: chỉ hiển thị (read-only)
                        const ngayCoHangDisplay = formatDateDisplay(ngayCoHangRaw);
                        ngayCoHangCell = ngayCoHangDisplay || '-';
                    }

                    // Nút chia lô: CHỈ cho bộ phận mua hàng
                    // (area = 'TruongBPMuahang' hoặc 'NhanvienMuahang'),
                    // và chỉ hiện khi chưa gửi báo giá và SL > 0; khi gửi rồi thì bỏ dấu + và ô input (coi như xong)
                    const splitBtnHtml = isPurchaseArea && !isDaGuiBaoGia && (parseFloat(item.sl) || 0) > 0
                        ? ` <button type="button"
                                   class="btn-split-add btn-split-add-inline"
                                   title="Chia lô theo ngày có hàng"
                                   data-masanpham="${escapeHtml(maSanpham)}"
                                   data-sl="${escapeHtml(item.sl)}">+</button>`
                        : '';

                    let row = `
                    <tr class="vt-data-row"
                        data-mavt="${escapeHtml((maSanpham || '').toLowerCase())}"
                        data-masanpham="${escapeHtml(maSanpham)}"
                        data-sl="${escapeHtml(item.sl)}"
                        data-has-split="${(item.lichCoHang && item.lichCoHang.length) ? '1' : '0'}">
                        <td>${STT++}</td>
                        <td>${item.tenSanpham || 'Không xác định'}</td>
                        <td>${item.maSanpham || 'Không xác định'}</td>
                        <td>${item.hangSX || 'Không xác định'}</td>
                        <td>${nhaCCCell}</td>
                        <td class="sl-cell" style="white-space:nowrap;">${item.sl}${splitBtnHtml}</td>
                        <td>${item.donVi || 'Không xác định'}</td>
                        <td>${donGiaCell}</td>
                        <td>
                            ${item.thanhTien != null
                            ? `<span class="ThanhTien">${item.thanhTien.toLocaleString('vi-VN')}</span>`
                            : `<span class="ThanhTien">0</span>`}
                        </td>
                        <td>${ngayThanhToanCell}</td>
                        <td>${ngayCoHangCell}</td>
                        <td>${ghiChuCell}</td>
                        <td>${item.trangThai}</td>
                    </tr>`;
                    $('.tablethietbi tbody').append(row);

                    // Nếu có lịch chia lô từ DB thì render các dòng con = copy nguyên dòng (Tên, Mã VT, Hãng, NCC, ĐV), SL + NCH input
                    if (item.lichCoHang && Array.isArray(item.lichCoHang) && item.lichCoHang.length > 0) {
                        const $parentRow = $('.tablethietbi tbody tr.vt-data-row').last();
                        let $insertAfter = $parentRow;
                        const ten = item.tenSanpham || 'Không xác định';
                        const hang = item.hangSX || 'Không xác định';
                        const ncc = (item.nhaCC || item.NhaCC || '').toString().trim() || '-';
                        const donVi = item.donVi || 'Không xác định';
                        item.lichCoHang.forEach(function (l) {
                            const slLine = l.sl != null ? l.sl : (l.SL != null ? l.SL : 0);
                            const ngayRaw = l.ngayCoHang || l.NgayCoHang;
                            const ngayDisplay = (function (dateRaw) {
                                if (!dateRaw) return '';
                                const d = new Date(dateRaw);
                                if (isNaN(d)) return '';
                                const mm = String(d.getMonth() + 1).padStart(2, '0');
                                const dd = String(d.getDate()).padStart(2, '0');
                                const yyyy = d.getFullYear();
                                return `${dd}/${mm}/${yyyy}`;
                            })(ngayRaw);
                            const donGiaVal = l.donGia != null ? l.donGia : (l.DonGia != null ? l.DonGia : '');
                            const thanhTienVal = (slLine && donGiaVal) ? (slLine * Number(donGiaVal)) : (l.thanhTien != null ? l.thanhTien : (l.ThanhTien != null ? l.ThanhTien : ''));
                            const ngayTTRaw = l.ngayThanhToan || l.NgayThanhToan;
                            const ngayTTDisplay = ngayTTRaw ? (function (dateRaw) {
                                const d = new Date(dateRaw);
                                if (isNaN(d)) return '';
                                const mm = String(d.getMonth() + 1).padStart(2, '0');
                                const dd = String(d.getDate()).padStart(2, '0');
                                return `${dd}/${mm}/${d.getFullYear()}`;
                            })(ngayTTRaw) : '';

                            const splitRowHtml = buildSplitRowHtml(maSanpham, {
                                ten, hang, ncc, donVi,
                                slValue: slLine, ngayValue: ngayDisplay, donGiaValue: donGiaVal,
                                ngayThanhToanValue: ngayTTDisplay, thanhTienValue: thanhTienVal,
                                readOnly: isDaGuiBaoGia
                            });
                            $insertAfter.after(splitRowHtml);
                            $insertAfter = $insertAfter.next();

                            if (!isDaGuiBaoGia && typeof flatpickr !== 'undefined') {
                                ['SplitNgayThanhToan', 'SplitNgay'].forEach(function (cls) {
                                    const el = $insertAfter.find('.' + cls)[0];
                                    if (el) {
                                        if (el._flatpickr) el._flatpickr.destroy();
                                        flatpickr(el, {
                                            dateFormat: "d/m/Y",
                                            locale: "vn",
                                            allowInput: true,
                                            clickOpens: true,
                                            onChange: function () { $insertAfter.find('.' + cls).trigger('change'); }
                                        });
                                    }
                                });
                            }
                            if (!isDaGuiBaoGia) updateSplitThanhTien($insertAfter);
                        });
                        validateSplitForItem($parentRow);
                    }
                });

                // Thêm hàng tổng tiền
                $('.tablethietbi tbody').append(`
                    <tr class="tong-tien-row">
                        <td colspan="10" style="text-align:center; font-weight:bold;">Tổng tiền:</td>
                        <td class="tong-tien" colspan="4" style="font-weight:bold;">0</td>
                    </tr>
                `);
                applyFilterMavt();
                updateTongTien();
                attachEventHandlers();

                // Re-sync excel filters after AJAX rebuild
                if (window.ExcelTableFilter) {
                    window.ExcelTableFilter.sync(document.querySelector('.tablethietbi'));
                }
            } else {
                $('.tablethietbi tbody').append(`
                    <tr>
                        <td colspan="13" style="text-align:center;">Không có dữ liệu vật tư.</td>
                    </tr>
                `);
                $('#action-buttons').hide();
            }

            let $rowToHighlight = $('.table tbody tr').filter(function () {
                return $(this).data('mamuahang') === Mamuahang;
            }).first();
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
        // Chỉ cho phép phím điều khiển và số (0-9) – không cho nhập dấu chấm/phẩy
        const keyCode = e.which || e.keyCode;

        // Cho phép phím điều khiển: backspace, tab, enter, delete, escape, mũi tên
        if (
            keyCode === 8 || keyCode === 9 || keyCode === 13 || keyCode === 27 ||
            keyCode === 46 || (keyCode >= 35 && keyCode <= 40)
        ) {
            return true;
        }

        // Cho phép số (0-9)
        if (keyCode >= 48 && keyCode <= 57) {
            return true;
        }

        // Chặn tất cả các ký tự khác
        e.preventDefault();
        return false;
    });

    // Xử lý khi paste hoặc nhập - loại bỏ ký tự không hợp lệ và format theo VNĐ
    $('.tablethietbi tbody').on('input paste', '.DonGia input', function () {
        const $input = $(this);
        let raw = $input.val() || '';

        // Chỉ giữ lại chữ số
        raw = raw.replace(/[^0-9]/g, '');

        if (!raw) {
            $input.val('');
            const $rowEmpty = $input.closest('tr');
            $rowEmpty.find('.ThanhTien').text('0');
            updateTongTien();
            return;
        }

        const donGiaInt = parseInt(raw, 10) || 0;

        // Hiển thị với dấu . ngăn cách hàng nghìn (định dạng vi-VN)
        $input.val(donGiaInt.toLocaleString('vi-VN'));

        const $row = $input.closest('tr');
        const sl = parseFloat($input.data('sl')) || 0;
        const thanhTien = sl * donGiaInt;

        $row.find('.ThanhTien').text(thanhTien.toLocaleString('vi-VN'));
        updateTongTien();

        // Lưu draft đơn giá theo từng vật tư để không mất dữ liệu khi rebuild table
        const maSanpham = $row.find('td').eq(2).text().trim();
        if (selectedMamuahang && maSanpham) {
            setDraft(selectedMamuahang, maSanpham, { donGiaDisplay: $input.val() || '' });
        }
    });

    // Helper: chuyển từ dd/MM/yyyy -> yyyy-MM-dd để gửi server
    function convertDisplayDateToServer(dateStr) {
        if (!dateStr) return '';
        const parts = dateStr.split('/');
        if (parts.length !== 3) return '';
        const [dd, mm, yyyy] = parts;
        if (dd.length !== 2 || mm.length !== 2 || yyyy.length !== 4) return '';
        return `${yyyy}-${mm}-${dd}`;
    }

    // Cập nhật ngày thanh toán khi chọn / nhập ngày (định dạng hiển thị dd/MM/yyyy)
    $('.tablethietbi tbody').on('change', '.NgayThanhToanInput', function () {
        const $input = $(this);
        const maMuahang = $input.data('mamuahang');
        const maSanpham = $input.data('masanpham');
        const ngayThanhToanDisplay = ($input.val() || '').trim(); // dd/MM/yyyy hoặc rỗng
        const ngayThanhToan = convertDisplayDateToServer(ngayThanhToanDisplay);

        if (!maMuahang || !maSanpham) {
            return;
        }

        // Lưu draft (để chỉ lưu DB khi bấm Gửi báo giá)
        setDraft(maMuahang, maSanpham, { ngayThanhToanDisplay: ngayThanhToanDisplay });

        const pathSegments = window.location.pathname.split('/');
        const area = pathSegments.length > 1 ? pathSegments[1] : '';
        if (area === 'TruongBPMuahang') {
            return;
        }
        const url = `/${area}/Yeucau/CapNhatNgayThanhToan`;

        $.ajax({
            url: url,
            method: 'POST',
            data: {
                MaMuahang: maMuahang,
                MaSanpham: maSanpham,
                NgayThanhToan: ngayThanhToan
            },
            success: function (res) {
                if (!res || !res.success) {
                    alert(res && res.message ? res.message : 'Cập nhật ngày thanh toán thất bại.');
                }
            },
            error: function () {
                alert('Không thể cập nhật ngày thanh toán.');
            }
        });
    });

    // Cập nhật ghi chú khi người dùng chỉnh sửa
    $('.tablethietbi tbody').on('blur', '.GhiChuInput', function () {
        const $input = $(this);
        const maMuahang = $input.data('mamuahang');
        const maSanpham = $input.data('masanpham');
        const ghiChu = $input.val() || '';

        if (!maMuahang || !maSanpham) {
            return;
        }

        // Lưu draft (để chỉ lưu DB khi bấm Gửi báo giá)
        setDraft(maMuahang, maSanpham, { ghiChu: ghiChu });

        const pathSegments = window.location.pathname.split('/');
        const area = pathSegments.length > 1 ? pathSegments[1] : '';
        if (area === 'TruongBPMuahang') {
            return;
        }
        const url = `/${area}/Yeucau/CapNhatGhiChuPhieumuahang`;

        $.ajax({
            url: url,
            method: 'POST',
            data: {
                MaMuahang: maMuahang,
                MaSanpham: maSanpham,
                GhiChu: ghiChu
            },
            success: function (res) {
                if (!res || !res.success) {
                    alert(res && res.message ? res.message : 'Cập nhật ghi chú thất bại.');
                }
            },
            error: function () {
                alert('Không thể cập nhật ghi chú.');
            }
        });
    });

    // Cập nhật Nhà cung cấp (NCC) khi BP Mua hàng nhập
    $('.tablethietbi tbody').on('focus', '.NhaCCInput', function () {
        const $input = $(this);
        $input.data('prev', ($input.val() || '').toString());
    });

    // Gợi ý NCC - hiện danh sách khi gõ hoặc click icon
    let nccGoiYTimeout = null;
    $('.tablethietbi tbody').on('input keyup', '.NhaCCInput', function () {
        const $input = $(this);
        const $wrapper = $input.closest('.ncc-input-wrapper');
        const $dropdown = $wrapper.find('.ncc-dropdown');
        const q = ($input.val() || '').trim();

        clearTimeout(nccGoiYTimeout);
        nccGoiYTimeout = setTimeout(function () {
            fetchNhaCCGoiY(q, $dropdown, $input);
        }, 200);
    });

    $('.tablethietbi tbody').on('click', '.ncc-goi-y-btn', function (e) {
        e.preventDefault();
        e.stopPropagation();
        $('.ncc-dropdown').hide();
        const $wrapper = $(this).closest('.ncc-input-wrapper');
        const $input = $wrapper.find('.NhaCCInput');
        const $dropdown = $wrapper.find('.ncc-dropdown');
        const q = ($input.val() || '').trim();
        if ($dropdown.is(':visible')) {
            $dropdown.hide();
        } else {
            fetchNhaCCGoiY(q, $dropdown, $input);
        }
    });

    $('.tablethietbi tbody').on('click', '.ncc-dropdown .ncc-suggestion-item', function (e) {
        e.preventDefault();
        const value = $(this).data('value') || $(this).text();
        const $wrapper = $(this).closest('.ncc-input-wrapper');
        $wrapper.find('.NhaCCInput').val(value);
        $wrapper.find('.ncc-dropdown').hide();
    });

    $(document).on('click', function (e) {
        if (!$(e.target).closest('.ncc-input-wrapper').length) {
            $('.ncc-dropdown').hide();
        }
    });

    $('.tablethietbi tbody').on('blur', '.NhaCCInput', function () {
        const $input = $(this);
        const maMuahang = $input.data('mamuahang');
        const maSanpham = $input.data('masanpham');
        const nhaCC = ($input.val() || '').toString().trim();
        const prev = ($input.data('prev') || '').toString().trim();

        if (!maMuahang || !maSanpham) {
            return;
        }

        // Không gọi API nếu không đổi
        if (nhaCC === prev) {
            return;
        }

        // Lưu draft (để chỉ lưu DB khi bấm Gửi báo giá)
        setDraft(maMuahang, maSanpham, { nhaCC: nhaCC });

        const pathSegments = window.location.pathname.split('/');
        const area = pathSegments.length > 1 ? pathSegments[1] : '';
        if (area === 'TruongBPMuahang') {
            // Trưởng BP mua hàng: chỉ lưu vào draft, chờ bấm "Gửi báo giá" mới lưu DB
            // Cập nhật prev để không bị loop cảnh báo khi focus/blur lại
            $input.data('prev', nhaCC);
            return;
        }
        const url = `/${area}/Yeucau/CapNhatNhaCCPhieumuahang`;

        $.ajax({
            url: url,
            method: 'POST',
            data: {
                MaMuahang: maMuahang,
                MaSanpham: maSanpham,
                NhaCC: nhaCC
            },
            success: function (res) {
                if (!res || !res.success) {
                    alert(res && res.message ? res.message : 'Cập nhật nhà cung cấp thất bại.');
                    // Restore về giá trị cũ nếu fail
                    $input.val(prev);
                } else {
                    $input.data('prev', nhaCC);
                }
            },
            error: function () {
                alert('Không thể cập nhật nhà cung cấp.');
                $input.val(prev);
            }
        });
    });

    // Cập nhật ngày có hàng khi BP Mua hàng chọn / nhập ngày (định dạng hiển thị dd/MM/yyyy)
    $('.tablethietbi tbody').on('change', '.NgayCoHangInput', function () {
        const $input = $(this);
        const maMuahang = $input.data('mamuahang');
        const maSanpham = $input.data('masanpham');
        const ngayCoHangDisplay = ($input.val() || '').trim(); // dd/MM/yyyy hoặc rỗng
        const ngayCoHang = convertDisplayDateToServer(ngayCoHangDisplay);

        if (!maMuahang || !maSanpham) {
            return;
        }

        // Lưu draft (để chỉ lưu DB khi bấm Gửi báo giá)
        setDraft(maMuahang, maSanpham, { ngayCoHangDisplay: ngayCoHangDisplay });

        const pathSegments = window.location.pathname.split('/');
        const area = pathSegments.length > 1 ? pathSegments[1] : '';
        if (area === 'TruongBPMuahang') {
            return;
        }
        const url = `/${area}/Yeucau/CapNhatNgayCoHang`;

        $.ajax({
            url: url,
            method: 'POST',
            data: {
                MaMuahang: maMuahang,
                MaSanpham: maSanpham,
                NgayCoHang: ngayCoHang
            },
            success: function (res) {
                if (!res || !res.success) {
                    alert(res && res.message ? res.message : 'Cập nhật ngày có hàng thất bại.');
                }
            },
            error: function () {
                alert('Không thể cập nhật ngày có hàng.');
            }
        });
    });

    // ===== Chia lô (split) theo ngày có hàng =====
    // Thêm dòng chia lô ngay dưới dòng cha
    $('.tablethietbi tbody').off('click.splitAdd').on('click.splitAdd', '.btn-split-add', function (e) {
        e.preventDefault();
        e.stopPropagation();
        const $btn = $(this);
        const $parentRow = $btn.closest('tr.vt-data-row');
        const maSanpham = ($btn.data('masanpham') || $parentRow.data('masanpham') || '').toString();
        const slGoc = parseFloat($btn.data('sl')) || (parseFloat($parentRow.data('sl')) || 0);
        if (!maSanpham) return;

        // Copy nội dung từ dòng gốc: Tên, Mã VT, Hãng, NCC, ĐV
        const ten = $parentRow.find('td').eq(1).text().trim();
        const hang = $parentRow.find('td').eq(3).text().trim();
        const ncc = ($parentRow.find('.NhaCCInput').val() || $parentRow.find('td').eq(4).text() || '').toString().trim();
        const donVi = $parentRow.find('td').eq(6).text().trim();

        // Tìm dòng con cuối cùng của cùng mã (đi từ dòng gốc xuống lần lượt) để chèn ngay sau nó — bấm + nhiều lần được
        let $after = $parentRow;
        let $next = $parentRow.next();
        while ($next.length && $next.hasClass('vt-split-row')) {
            const p = ($next.attr('data-parent') || $next.data('parent') || '').toString();
            if (p !== maSanpham) break;
            $after = $next;
            $next = $next.next();
        }

        const splitRowHtml = buildSplitRowHtml(maSanpham, { ten, hang, ncc, donVi, slValue: '', ngayValue: '', donGiaValue: '' });
        $after.after(splitRowHtml);

        const $newRow = $after.next();
        if (typeof flatpickr !== 'undefined') {
            ['SplitNgayThanhToan', 'SplitNgay'].forEach(function (cls) {
                const el = $newRow.find('.' + cls)[0];
                if (el) {
                    flatpickr(el, {
                        dateFormat: "d/m/Y",
                        locale: "vn",
                        allowInput: true,
                        clickOpens: true,
                        onChange: function () { $newRow.find('.' + cls).trigger('change'); }
                    });
                }
            });
        }

        const state = getSplitStateForItem($parentRow);
        const sum = state.splits.reduce((acc, x) => acc + (x.sl || 0), 0);
        const remain = Math.max(0, slGoc - sum);
        $newRow.find('.SplitSL').val(remain > 0 ? String(remain) : '');

        validateSplitForItem($parentRow);
    });

    // Xóa dòng chia lô
    $('.tablethietbi tbody').off('click.splitRemove').on('click.splitRemove', '.btn-split-remove', function (e) {
        e.preventDefault();
        const $row = $(this).closest('tr.vt-split-row');
        const parent = ($row.data('parent') || '').toString();
        $row.remove();
        if (parent) {
            const $parentRow = $(`.tablethietbi tbody tr.vt-data-row[data-masanpham="${CSS.escape(parent)}"]`).first();
            if ($parentRow.length) validateSplitForItem($parentRow);
        }
    });

    // Validate khi nhập SL / ngày; cập nhật Thành tiền khi SL hoặc Đơn giá thay đổi
    $('.tablethietbi tbody').off('input.splitSL').on('input.splitSL', '.SplitSL', function () {
        const $row = $(this).closest('tr.vt-split-row');
        const v = ($(this).val() || '').toString().replace(/[^\d]/g, '');
        $(this).val(v);
        updateSplitThanhTien($row);
        const parent = ($row.data('parent') || '').toString();
        if (parent) {
            const $parentRow = $(`.tablethietbi tbody tr.vt-data-row[data-masanpham="${CSS.escape(parent)}"]`).first();
            if ($parentRow.length) validateSplitForItem($parentRow);
        }
    });
    $('.tablethietbi tbody').off('input.splitDonGia').on('input.splitDonGia', '.SplitDonGia', function () {
        const $row = $(this).closest('tr.vt-split-row');
        updateSplitThanhTien($row);
    });
    $('.tablethietbi tbody').off('change.splitNgay').on('change.splitNgay', '.SplitNgay', function () {
        const $row = $(this).closest('tr.vt-split-row');
        const parent = ($row.data('parent') || '').toString();
        if (parent) {
            const $parentRow = $(`.tablethietbi tbody tr.vt-data-row[data-masanpham="${CSS.escape(parent)}"]`).first();
            if ($parentRow.length) validateSplitForItem($parentRow);
        }
    });

    // Khởi tạo Flatpickr cho các input ngày (định dạng dd/MM/yyyy)
    // Kiểm tra xem flatpickr đã được load chưa
    if (typeof flatpickr !== 'undefined') {
        // Khởi tạo lại cho các input mới được render
        $('.NgayThanhToanInput').each(function() {
            const $input = $(this);
            // Nếu đã có flatpickr instance thì destroy trước
            if ($input[0]._flatpickr) {
                $input[0]._flatpickr.destroy();
            }
            flatpickr($input[0], {
                dateFormat: "d/m/Y",
                locale: "vn",
                allowInput: true,
                clickOpens: true,
                onChange: function(selectedDates, dateStr, instance) {
                    // Trigger change event để lưu vào database
                    $input.trigger('change');
                }
            });
        });

        $('.NgayCoHangInput').each(function() {
            const $input = $(this);
            // Nếu đã có flatpickr instance thì destroy trước
            if ($input[0]._flatpickr) {
                $input[0]._flatpickr.destroy();
            }
            flatpickr($input[0], {
                dateFormat: "d/m/Y",
                locale: "vn",
                allowInput: true,
                clickOpens: true,
                onChange: function(selectedDates, dateStr, instance) {
                    // Trigger change event để lưu vào database
                    $input.trigger('change');
                }
            });
        });
    }
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
    if (typeof getThongbaoData === 'function') {
        getThongbaoData();
    }
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
    
    if (window.confirm("Bạn có chắc chắn muốn duyệt phiếu mua hàng này?")) {
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
    }
});

// Xử lý click vào header "Ngày Thanh Toán", "Ngày có hàng" hoặc "Ghi chú" để hiện popup điền hàng loạt
$(document).on('click', '.header-clickable', function() {
    const field = $(this).data('field');
    const pathSegments = window.location.pathname.split('/');
    const area = pathSegments.length > 1 ? pathSegments[1] : '';
    
    // Chỉ cho phép ở Giamdoc và TruongBPMuahang
    if (area !== 'Giamdoc' && area !== 'TruongBPMuahang') {
        return;
    }
    
    // Kiểm tra trạng thái phiếu để xác định có cho phép chỉnh sửa không
    let trangThaiPhieu = '';
    if (selectedMamuahang) {
        $('.table tbody tr').each(function() {
            const link = $(this).find('td').eq(1).find('a');
            if (link.text().trim() === selectedMamuahang) {
                trangThaiPhieu = link.data('trangthai') || '';
                if (!trangThaiPhieu) {
                    // Lấy từ cột trạng thái (cột cuối cùng)
                    const trangThaiCell = $(this).find('td').last();
                    trangThaiPhieu = trangThaiCell.text().trim();
                }
                return false; // break loop
            }
        });
    }
    
    // Nếu là Giám đốc, chỉ cho phép khi trạng thái = "Đã báo giá"
    if (area === 'Giamdoc' && (field === 'ngaythanhtoan' || field === 'ghichu')) {
        // Chỉ cho phép chỉnh sửa khi trạng thái = "Đã báo giá"
        if (trangThaiPhieu !== 'Đã báo giá') {
            alert('Không thể chỉnh sửa khi phiếu đã được duyệt. Chỉ có thể chỉnh sửa khi trạng thái là "Đã báo giá".');
            return;
        }
    }
    
    if (field === 'ngaythanhtoan') {
        $('#popupNgayThanhToan').fadeIn(200);
        $('#inputNgayThanhToan').focus();
    } else if (field === 'ngaycohang') {
        $('#popupNgayCoHang').fadeIn(200);
        $('#inputNgayCoHang').focus();
    } else if (field === 'ghichu') {
        $('#popupGhiChu').fadeIn(200);
        $('#inputGhiChu').focus();
    }
});

// Helper global: chuẩn hóa ngày từ popup (yyyy-MM-dd hoặc dd/MM/yyyy) -> dd/MM/yyyy để điền vào bảng và gửi server
function normalizePopupDateToDisplay(dateStr) {
    if (!dateStr || typeof dateStr !== 'string') return '';
    const trimmed = dateStr.trim();
    if (!trimmed) return '';
    var isoMatch = trimmed.match(/^(\d{4})-(\d{2})-(\d{2})$/);
    if (isoMatch) {
        return isoMatch[3] + '/' + isoMatch[2] + '/' + isoMatch[1];
    }
    var displayMatch = trimmed.match(/^(\d{1,2})\/(\d{1,2})\/(\d{4})$/);
    if (displayMatch) {
        var d = displayMatch[1].length === 1 ? '0' + displayMatch[1] : displayMatch[1];
        var m = displayMatch[2].length === 1 ? '0' + displayMatch[2] : displayMatch[2];
        return d + '/' + m + '/' + displayMatch[3];
    }
    return '';
}

// Helper global: dd/MM/yyyy -> yyyy-MM-dd (dùng trong apply* khi đã có chuỗi dd/MM/yyyy)
function convertDisplayDateToServerGlobal(dateStr) {
    if (!dateStr) return '';
    var parts = dateStr.split('/');
    if (parts.length !== 3) return '';
    var dd = parts[0], mm = parts[1], yyyy = parts[2];
    if (dd.length !== 2 || mm.length !== 2 || yyyy.length !== 4) return '';
    return yyyy + '-' + mm + '-' + dd;
}

// Đóng popup Ngày Thanh Toán
function closePopupNgayThanhToan() {
    $('#popupNgayThanhToan').fadeOut(200);
    $('#inputNgayThanhToan').val('');
}

// Đóng popup Ngày có hàng
function closePopupNgayCoHang() {
    $('#popupNgayCoHang').fadeOut(200);
    $('#inputNgayCoHang').val('');
}

// Đóng popup Ghi chú
function closePopupGhiChu() {
    $('#popupGhiChu').fadeOut(200);
    $('#inputGhiChu').val('');
}

// Áp dụng Ngày Thanh Toán cho tất cả các hàng (popup: chấp nhận dd/MM/yyyy hoặc yyyy-MM-dd từ input type="date")
function applyNgayThanhToan() {
    var raw = ($('#inputNgayThanhToan').val() || '').trim();
    if (!raw) {
        alert('Vui lòng nhập ngày thanh toán.');
        return;
    }
    var ngayThanhToanDisplay = normalizePopupDateToDisplay(raw);
    if (!ngayThanhToanDisplay) {
        alert('Định dạng ngày thanh toán không hợp lệ. Vui lòng nhập theo dạng dd/MM/yyyy hoặc chọn ngày từ ô chọn.');
        return;
    }
    var serverDate = convertDisplayDateToServerGlobal(ngayThanhToanDisplay);
    if (!serverDate) {
        alert('Định dạng ngày thanh toán không hợp lệ. Vui lòng nhập theo dạng dd/MM/yyyy.');
        return;
    }

    var count = 0;
    $('.tablethietbi tbody tr').each(function () {
        if ($(this).hasClass('tong-tien-row')) return;
        var $input = $(this).find('.NgayThanhToanInput');
        if ($input.length > 0) {
            $input.val(ngayThanhToanDisplay);
            $input.trigger('change');
            count++;
        }
    });
    if (count > 0) {
        alert('Đã điền ngày thanh toán cho ' + count + ' vật tư.');
        closePopupNgayThanhToan();
    } else {
        alert('Không tìm thấy vật tư nào để điền ngày thanh toán.');
    }
}

// Áp dụng Ngày có hàng cho tất cả các hàng (popup: chấp nhận dd/MM/yyyy hoặc yyyy-MM-dd từ input type="date")
function applyNgayCoHang() {
    var raw = ($('#inputNgayCoHang').val() || '').trim();
    if (!raw) {
        alert('Vui lòng nhập ngày có hàng.');
        return;
    }
    var ngayCoHangDisplay = normalizePopupDateToDisplay(raw);
    if (!ngayCoHangDisplay) {
        alert('Định dạng ngày có hàng không hợp lệ. Vui lòng nhập theo dạng dd/MM/yyyy hoặc chọn ngày từ ô chọn.');
        return;
    }
    var serverDate = convertDisplayDateToServerGlobal(ngayCoHangDisplay);
    if (!serverDate) {
        alert('Định dạng ngày có hàng không hợp lệ. Vui lòng nhập theo dạng dd/MM/yyyy.');
        return;
    }

    var count = 0;
    $('.tablethietbi tbody tr').each(function () {
        if ($(this).hasClass('tong-tien-row')) return;
        var $input = $(this).find('.NgayCoHangInput');
        if ($input.length > 0) {
            $input.val(ngayCoHangDisplay);
            $input.trigger('change');
            count++;
        }
    });
    if (count > 0) {
        alert('Đã điền ngày có hàng cho ' + count + ' vật tư.');
        closePopupNgayCoHang();
    } else {
        alert('Không tìm thấy vật tư nào để điền ngày có hàng.');
    }
}

// Áp dụng Ghi chú cho tất cả các hàng
function applyGhiChu() {
    const ghiChu = $('#inputGhiChu').val();
    
    // Cho phép ghi chú rỗng
    // Điền ghi chú cho tất cả các hàng có input ghi chú
    let count = 0;
    $('.tablethietbi tbody tr').each(function() {
        if ($(this).hasClass('tong-tien-row')) {
            return;
        }
        
        const $input = $(this).find('.GhiChuInput');
        if ($input.length > 0) {
            // Cập nhật giá trị input
            $input.val(ghiChu);

            // Dùng blur handler để:
            // - Lưu draft (TruongBPMuahang)
            // - Hoặc lưu DB (các area khác, nếu có)
            $input.trigger('blur');
            
            count++;
        }
    });
    
    if (count > 0) {
        alert(`Đã điền ghi chú cho ${count} vật tư.`);
        closePopupGhiChu();
    } else {
        alert('Không tìm thấy vật tư nào để điền ghi chú.');
    }
}

// Xử lý nút từ chối phiếu mua hàng
$(document).on('click', '#rejectPhieumuahang', function() {
    if (!selectedMamuahang) {
        alert("Vui lòng chọn mã mua hàng trước khi từ chối.");
        return;
    }
    
    if (window.confirm("Bạn có chắc chắn muốn từ chối phiếu mua hàng này?")) {
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
    }
});