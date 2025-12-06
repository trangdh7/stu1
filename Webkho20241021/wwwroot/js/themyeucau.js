document.addEventListener("DOMContentLoaded", function () {
    const tableBody = document.getElementById("table-body");
    const fileInput = document.getElementById("excel-upload");
    const ngayCanHangDefault = document.getElementById("ngaycanhang");
    const feedbackEl = document.getElementById("excel-feedback");
    const hiddenInputsWrapper = document.getElementById("excel-hidden-inputs");
    let currentRows = [];
    const areaSegment = window.location.pathname.split("/")[1] || "";
    const areaBasePath = areaSegment ? `/${areaSegment}/Yeucau` : "/Yeucau";
    const requestSelect = document.getElementById("tenyeucau");
    const khoDataUrl = areaSegment
        ? `/${areaSegment}/Yeucau/GetKhoTongData`
        : "/Yeucau/GetKhoTongData";
    const khoDataState = {
        data: [],
        loaded: false,
        loading: false
    };

    if (!tableBody || !fileInput) {
        return;
    }

    loadKhoTongData();

    const HEADER_LOOKAHEAD = 3;

    const headerAliases = {
        ten: [
            "ten",
            "tenthietbi",
            "tenthietbihanghoa",
            "tenhanghoa",
            "tenvattu",
            "tenvatlieu"
        ],
        ma: ["mavt", "mavattu", "mahanghoa", "ma"],
        hang: ["hangsx", "hangsanxuat", "hang", "nhasx"],
        donvi: ["donvi", "dv", "dvt", "donvitinh"],
        slcu: ["slcu", "cu", "soluongcu", "old"],
        slmoi: ["slmoi", "moi", "soluongmoi", "new"],
        nhacc: ["nhacungcap", "ncc"],
        ngay: ["ngaycanhang", "ngaycan", "ngaynhan", "ngaycan"],
        ghichu: ["ghichu", "note", "lydo", "mota", "chuthich"]
    };
    
    
    function normalizeHeader(text) {
        return text
            ? text
                  .toString()
                  .trim()
                  .toLowerCase()
                  .normalize("NFD")
                  .replace(/[\u0300-\u036f]/g, "")
                  .replace(/đ/g, "d")
                  .replace(/[^a-z0-9]/g, "")
            : "";
    }

    const requestRedirectMap = {
        yeucaunhapkho: "ThemPhieunhapkho"
    };

    function handleRequestRedirect(event) {
        const normalizedValue = normalizeHeader(event.target.value || "");
        const targetRoute = requestRedirectMap[normalizedValue];
        if (!targetRoute) {
            return;
        }
        const targetUrl = `${areaBasePath}/${targetRoute}`;
        window.location.href = targetUrl;
    }

    if (requestSelect) {
        requestSelect.addEventListener("change", handleRequestRedirect);
    }

    function normalizeCode(value) {
        return value ? value.toString().trim().toLowerCase() : "";
    }

    function loadKhoTongData() {
        if (khoDataState.loaded || khoDataState.loading) {
            return;
        }
        khoDataState.loading = true;
        fetch(khoDataUrl, {
            headers: {
                Accept: "application/json"
            }
        })
            .then((response) => {
                if (!response.ok) {
                    throw new Error("Không thể tải dữ liệu kho tổng.");
                }
                return response.json();
            })
            .then((data) => {
                if (Array.isArray(data)) {
                    khoDataState.data = data.map((item) => ({
                        tenSanpham: item?.tenSanpham || "",
                        maSanpham: item?.maSanpham || "",
                        hangSX: item?.hangSX || "",
                        donVi: item?.donVi || "",
                        makho: item?.makho || "",
                        nhaCC: item?.nhaCC || ""
                    }));
                } else {
                    khoDataState.data = [];
                }
            })
            .catch((error) => {
                console.error("Lỗi tải dữ liệu kho tổng:", error);
            })
            .finally(() => {
                khoDataState.loaded = true;
            });
    }

    function findKhoItemByCode(code) {
        const normalized = normalizeCode(code);
        if (!normalized) return null;
        return (
            khoDataState.data.find(
                (item) => normalizeCode(item.maSanpham) === normalized
            ) || null
        );
    }

    function applyKhoData(rowData) {
        const matchedItem = findKhoItemByCode(rowData.MaSanpham);
        if (!matchedItem) {
            return rowData;
        }
        return {
            ...rowData,
            TenSanpham: matchedItem.tenSanpham || rowData.TenSanpham,
            MaSanpham: matchedItem.maSanpham || rowData.MaSanpham,
            HangSX: matchedItem.hangSX || rowData.HangSX,
            DonVi: matchedItem.donVi || rowData.DonVi,
            NhaCC: rowData.NhaCC || matchedItem.nhaCC || "",
            YCMakho: matchedItem.makho || rowData.YCMakho || "",
            hasKhoMatch: true
        };
    }

    function matchesAlias(normalizedHeader, aliasList) {
        return aliasList.some((alias) => {
            if (!alias || !normalizedHeader) return false;
            if (normalizedHeader === alias) {
                return true;
            }
            if (
                alias.length >= normalizedHeader.length &&
                alias.includes(normalizedHeader) &&
                normalizedHeader.length >= 3
            ) {
                return true;
            }
            return false;
        });
    }

    function mapHeaders(headerRows) {
        const mapping = {};
        const scores = {};
        if (!headerRows || !headerRows.length) {
            return mapping;
        }
        const maxColumns = Math.max(
            ...headerRows.map((row) => (row ? row.length : 0))
        );
        for (let col = 0; col < maxColumns; col++) {
            for (let rowIdx = headerRows.length - 1; rowIdx >= 0; rowIdx--) {
                const row = headerRows[rowIdx] || [];
                const cell = row[col];
                const normalized = normalizeHeader(cell);
                if (!normalized) {
                    continue;
                }
                Object.entries(headerAliases).forEach(([key, aliases]) => {
                    if (!matchesAlias(normalized, aliases)) {
                        return;
                    }
                    const depthScore = (headerRows.length - rowIdx) * 5;
                    const score = normalized.length + depthScore;
                    if (scores[key] === undefined || score > scores[key]) {
                        scores[key] = score;
                        mapping[key] = col;
                    }
                });
            }
        }
        return mapping;
    }

    function tryFillMissingColumns(mapping, headerRows) {
        if (!headerRows || !headerRows.length) return mapping;
        const maxColumns = Math.max(
            ...headerRows.map((row) => (row ? row.length : 0))
        );
        const headerHasText = (colIndex) =>
            headerRows.some((row) => {
                if (!row || row[colIndex] === undefined) return false;
                return normalizeHeader(row[colIndex]) !== "";
            });

        const orderedKeys = [
            "ten",
            "ma",
            "hang",
            "donvi",
            "slcu",
            "slmoi",
            "ngay",
            "nhacc",
            "ghichu"
        ];

        let lastKnownColumn = undefined;
        orderedKeys.forEach((key) => {
            if (mapping[key] !== undefined) {
                lastKnownColumn = mapping[key];
                return;
            }
            if (lastKnownColumn === undefined) {
                return;
            }
            let candidate = lastKnownColumn + 1;
            while (candidate < maxColumns && !headerHasText(candidate)) {
                candidate++;
            }
            if (candidate < maxColumns) {
                mapping[key] = candidate;
                lastKnownColumn = candidate;
            }
        });
        return mapping;
    }

    function setFeedback(message, isError = false) {
        if (!feedbackEl) return;
        feedbackEl.textContent = message || "";
        feedbackEl.classList.toggle("error", isError);
        feedbackEl.classList.toggle("success", !isError);
    }

    function clearTable() {
        tableBody.innerHTML = "";
        if (hiddenInputsWrapper) {
            hiddenInputsWrapper.innerHTML = "";
        }
    }

    function escapeHtml(text) {
        if (text === null || text === undefined) return "";
        return text
            .toString()
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#039;");
    }

    function parseDateValue(value) {
        if (!value) return "";
        if (typeof value === "number") {
            const dateObj = XLSX.SSF.parse_date_code(value);
            if (!dateObj) return "";
            const jsDate = new Date(Date.UTC(dateObj.y, dateObj.m - 1, dateObj.d));
            return jsDate.toISOString().slice(0, 10);
        }
        const parsed = new Date(value);
        if (isNaN(parsed.getTime())) {
            return "";
        }
        return parsed.toISOString().slice(0, 10);
    }

    function isValidFutureDate(dateString) {
        if (!dateString) return true; // Cho phép để trống
        const date = new Date(dateString);
        const today = new Date();
        today.setHours(0, 0, 0, 0);
        date.setHours(0, 0, 0, 0);
        return date >= today;
    }

    function parseQuantity(value) {
        if (value === null || value === undefined || value === "") return "";
        const num = Number(value);
        if (isNaN(num)) {
            return value.toString();
        }
        return num.toString();
    }

    function appendHiddenInput(name, value) {
        if (!hiddenInputsWrapper) return;
        const input = document.createElement("input");
        input.type = "hidden";
        input.name = name;
        input.value = value ?? "";
        hiddenInputsWrapper.appendChild(input);
    }

    const hasQuantityValue = (value) =>
        !(value === undefined || value === null || value === "");

    function resolveFinalQuantity(rowData) {
        if (!rowData) return "";
        if (hasQuantityValue(rowData.SLMoi)) {
            return rowData.SLMoi;
        }
        if (hasQuantityValue(rowData.SLCu)) {
            return rowData.SLCu;
        }
        return "";
    }

    function appendRowHiddenInputs(rowData) {
        appendHiddenInput("TenSanpham", rowData.TenSanpham);
        appendHiddenInput("MaSanpham", rowData.MaSanpham);
        appendHiddenInput("YCMakho", rowData.YCMakho || "");
        appendHiddenInput("HangSX", rowData.HangSX);
        appendHiddenInput("DonVi", rowData.DonVi);
        appendHiddenInput("SLCu", rowData.SLCu);
        const finalQuantity = rowData.SLToSave ?? resolveFinalQuantity(rowData);
        appendHiddenInput("SL", finalQuantity);
        appendHiddenInput("NhaCC", rowData.NhaCC);
        appendHiddenInput("VTNgayCanHang", rowData.NgayCanHang);
        appendHiddenInput("GhiChu", rowData.GhiChu || "");
    }

    async function renderRows(rows, updateState = true) {
        clearTable();
        
        // Giới hạn số dòng tối đa để tránh quá tải
        const MAX_ROWS = 5000;
        if (rows.length > MAX_ROWS) {
            setFeedback(`Cảnh báo: Tệp có ${rows.length} dòng, chỉ hiển thị ${MAX_ROWS} dòng đầu tiên.`, true);
            rows = rows.slice(0, MAX_ROWS);
        }

        // Render theo batch để tránh block UI
        const RENDER_BATCH_SIZE = 100;
        
        for (let i = 0; i < rows.length; i++) {
            const row = rows[i];
            const tr = document.createElement("tr");
            const finalQuantity = row.SLToSave ?? resolveFinalQuantity(row);
            tr.innerHTML = `
                <td>${i + 1}</td>
                <td>${escapeHtml(row.TenSanpham)}</td>
                <td>${escapeHtml(row.MaSanpham)}</td>
                <td>${escapeHtml(row.HangSX)}</td>
                <td>${escapeHtml(row.DonVi)}</td>
                <td>${escapeHtml(row.SLCu)}</td>
                <td>${escapeHtml(finalQuantity)}</td>
                <td>${escapeHtml(row.NgayCanHang)}</td>
                <td>${escapeHtml(row.NhaCC)}</td>
                <td><input type="text" class="ghichu-input" data-index="${i}" value="${escapeHtml(row.GhiChu || '')}" placeholder="Nhập ghi chú (tùy chọn)" style="width: 100%; padding: 4px;" /></td>
            `;
            
            // Thêm event listener cho input ghi chú
            const ghiChuInput = tr.querySelector('.ghichu-input');
            if (ghiChuInput) {
                ghiChuInput.addEventListener('change', function() {
                    const rowIndex = parseInt(this.getAttribute('data-index'));
                    if (currentRows[rowIndex]) {
                        currentRows[rowIndex].GhiChu = this.value;
                        // Cập nhật hidden input
                        const hiddenInputs = hiddenInputsWrapper.querySelectorAll(`input[name="GhiChu"]`);
                        if (hiddenInputs[rowIndex]) {
                            hiddenInputs[rowIndex].value = this.value;
                        }
                    }
                });
            }
            tableBody.appendChild(tr);
            appendRowHiddenInputs(row);

            // Mỗi 100 dòng, cho phép UI update
            if ((i + 1) % RENDER_BATCH_SIZE === 0) {
                // Cho phép browser render
                await new Promise(resolve => {
                    if (window.requestAnimationFrame) {
                        requestAnimationFrame(() => {
                            setTimeout(resolve, 0);
                        });
                    } else {
                        setTimeout(resolve, 0);
                    }
                });
            }
        }
        
        if (updateState) {
            currentRows = rows.map((row) => ({ ...row }));
        }
    }

    function updateRowsDefaultDate(newDate) {
        if (!currentRows.length) return;
        let updated = false;
        currentRows = currentRows.map((row) => {
            if (row.hasManualDate) {
                return row;
            }
            if (row.NgayCanHang === newDate) {
                return row;
            }
            updated = true;
            return { ...row, NgayCanHang: newDate };
        });
        if (updated) {
            renderRows(currentRows, false).catch(err => console.error("Lỗi render:", err));
        }
    }

    function findHeaderRowIndex(sheetRows) {
        let bestIndex = -1;
        let bestScore = 0;

        sheetRows.forEach((row, index) => {
            if (!row || !row.length) return;
            const matchedKeys = new Set();
            row.forEach((cell) => {
                const normalized = normalizeHeader(cell);
                if (!normalized) return;
                Object.entries(headerAliases).forEach(([key, aliases]) => {
                    if (matchesAlias(normalized, aliases)) {
                        matchedKeys.add(key);
                    }
                });
            });
            if (!matchedKeys.has("ten")) {
                return;
            }
            if (matchedKeys.size > bestScore) {
                bestScore = matchedKeys.size;
                bestIndex = index;
            }
        });

        return bestIndex;
    }

    function processSheet(sheetRows) {
        if (!sheetRows || !sheetRows.length) {
            setFeedback("Tệp Excel không có dữ liệu.", true);
            clearTable();
            return;
        }

        const headerRowIndex = findHeaderRowIndex(sheetRows);
        if (headerRowIndex === -1) {
            setFeedback("Không tìm thấy dòng tiêu đề hợp lệ trong tệp.", true);
            clearTable();
            return;
        }

        const headerRows = sheetRows.slice(
            headerRowIndex,
            Math.min(sheetRows.length, headerRowIndex + HEADER_LOOKAHEAD)
        );
        const headerRow = headerRows[0] || [];
        const dataRows = sheetRows.slice(headerRowIndex + 1);
        if (!headerRow.length) {
            setFeedback("Không đọc được tiêu đề cột trong tệp.", true);
            clearTable();
            return;
        }

        const headerIndex = mapHeaders(headerRows);
        const requiredKeys = ["ten", "ma", "hang", "donvi", "slmoi"];
        const missingKeys = requiredKeys.filter(
            (key) => headerIndex[key] === undefined
        );
        if (missingKeys.length) {
            const missingLabels = {
                ten: "Tên thiết bị/hàng hóa",
                ma: "Mã VT",
                hang: "Hãng SX",
                donvi: "ĐV",
                slmoi: "SL Mới"
            };
            const missingText = missingKeys
                .map((key) => missingLabels[key] || key)
                .join(", ");
            setFeedback(`Thiếu cột bắt buộc: ${missingText}.`, true);
            clearTable();
            return;
        }

        const headerTexts = {};
        Object.entries(headerIndex).forEach(([key, colIndex]) => {
            for (let i = 0; i < headerRows.length; i++) {
                const cell = headerRows[i] && headerRows[i][colIndex];
                if (cell === undefined || cell === null) continue;
                const text = cell.toString().trim();
                if (text) {
                    headerTexts[key] = text;
                    break;
                }
            }
        });

        const normalizedHeaderTexts = Object.fromEntries(
            Object.entries(headerTexts).map(([key, text]) => [
                key,
                normalizeHeader(text)
            ])
        );

        const defaultDate = ngayCanHangDefault ? ngayCanHangDefault.value : "";
        const today = new Date().toISOString().slice(0, 10);
        let hasInvalidDate = false;

        const rows = dataRows
            .map((row) => {
                const getValue = (key) =>
                    headerIndex[key] !== undefined ? row[headerIndex[key]] : "";
                const parsedDate = parseDateValue(getValue("ngay")) || defaultDate;
                let rowData = {
                    TenSanpham: (getValue("ten") || "").toString().trim(),
                    MaSanpham: (getValue("ma") || "").toString().trim(),
                    HangSX: (getValue("hang") || "").toString().trim(),
                    DonVi: (getValue("donvi") || "").toString().trim(),
                    SLCu: parseQuantity(getValue("slcu")),
                    SLMoi: parseQuantity(getValue("slmoi")),
                    NhaCC: (getValue("nhacc") || "").toString().trim(),
                    NgayCanHang: parsedDate,
                    GhiChu: (getValue("ghichu") || "").toString().trim(),
                    YCMakho: "",
                    hasManualDate: Boolean(getValue("ngay"))
                };
                rowData.SLToSave = resolveFinalQuantity(rowData);
                rowData = applyKhoData(rowData);
                if (!rowData.hasManualDate) {
                    rowData.NgayCanHang = defaultDate;
                }
                // Kiểm tra ngày hợp lệ (phải là ngày tương lai)
                if (rowData.NgayCanHang && !isValidFutureDate(rowData.NgayCanHang)) {
                    hasInvalidDate = true;
                    rowData.NgayCanHang = today; // Tự động đặt về ngày hôm nay nếu là ngày quá khứ
                }
                return rowData;
            })
            .filter((row) => {
                if (!row.TenSanpham) return false;
                if (
                    normalizedHeaderTexts.slmoi &&
                    normalizeHeader(row.SLMoi) === normalizedHeaderTexts.slmoi
                ) {
                    return false;
                }
                if (
                    normalizedHeaderTexts.slcu &&
                    normalizeHeader(row.SLCu) === normalizedHeaderTexts.slcu
                ) {
                    return false;
                }
                const isNumericName = /^[0-9]+(\.[0-9]+)*$/.test(row.TenSanpham);
                if (isNumericName) return false;
                const otherFields = [
                    row.MaSanpham,
                    row.HangSX,
                    row.DonVi,
                    row.SLCu,
                    row.SLMoi,
                    row.NhaCC
                ];
                const hasOtherData = otherFields.some(
                    (value) => value !== null && value !== ""
                );
                return hasOtherData;
            });

        if (!rows.length) {
            setFeedback("Không tìm thấy dữ liệu hợp lệ trong tệp.", true);
            clearTable();
            return;
        }

        renderRows(rows).then(() => {
            if (hasInvalidDate) {
                setFeedback(`Đã nhập ${rows.length} dòng từ tệp Excel. Lưu ý: Một số ngày cần hàng trong quá khứ đã được tự động điều chỉnh thành ngày hôm nay.`, false);
            } else {
                setFeedback(`Đã nhập ${rows.length} dòng từ tệp Excel.`, false);
            }
        });
    }

    // Xử lý sheet theo batch để tránh block UI với file lớn
    async function processSheetAsync(sheetRows) {
        if (!sheetRows || !sheetRows.length) {
            setFeedback("Tệp Excel không có dữ liệu.", true);
            clearTable();
            return;
        }

        const headerRowIndex = findHeaderRowIndex(sheetRows);
        if (headerRowIndex === -1) {
            setFeedback("Không tìm thấy dòng tiêu đề hợp lệ trong tệp.", true);
            clearTable();
            return;
        }

        const headerRows = sheetRows.slice(
            headerRowIndex,
            Math.min(sheetRows.length, headerRowIndex + HEADER_LOOKAHEAD)
        );
        const headerRow = headerRows[0] || [];
        const dataRows = sheetRows.slice(headerRowIndex + 1);
        if (!headerRow.length) {
            setFeedback("Không đọc được tiêu đề cột trong tệp.", true);
            clearTable();
            return;
        }

        const headerIndex = mapHeaders(headerRows);
        const requiredKeys = ["ten", "ma", "hang", "donvi", "slmoi"];
        const missingKeys = requiredKeys.filter(
            (key) => headerIndex[key] === undefined
        );
        if (missingKeys.length) {
            const missingLabels = {
                ten: "Tên thiết bị/hàng hóa",
                ma: "Mã VT",
                hang: "Hãng SX",
                donvi: "ĐV",
                slmoi: "SL Mới"
            };
            const missingText = missingKeys
                .map((key) => missingLabels[key] || key)
                .join(", ");
            setFeedback(`Thiếu cột bắt buộc: ${missingText}.`, true);
            clearTable();
            return;
        }

        const headerTexts = {};
        Object.entries(headerIndex).forEach(([key, colIndex]) => {
            for (let i = 0; i < headerRows.length; i++) {
                const cell = headerRows[i] && headerRows[i][colIndex];
                if (cell === undefined || cell === null) continue;
                const text = cell.toString().trim();
                if (text) {
                    headerTexts[key] = text;
                    break;
                }
            }
        });

        const normalizedHeaderTexts = Object.fromEntries(
            Object.entries(headerTexts).map(([key, text]) => [
                key,
                normalizeHeader(text)
            ])
        );

        const defaultDate = ngayCanHangDefault ? ngayCanHangDefault.value : "";
        const today = new Date().toISOString().slice(0, 10);
        let hasInvalidDate = false;

        // Xử lý từng batch để không block UI
        const BATCH_SIZE = 100; // Xử lý 100 dòng mỗi lần
        let allRows = [];
        let processedCount = 0;

        const processBatch = async (startIndex) => {
            const endIndex = Math.min(startIndex + BATCH_SIZE, dataRows.length);
            const batch = dataRows.slice(startIndex, endIndex);

            // Xử lý batch hiện tại
            const batchRows = batch
                .map((row) => {
                    const getValue = (key) =>
                        headerIndex[key] !== undefined ? row[headerIndex[key]] : "";
                    const parsedDate = parseDateValue(getValue("ngay")) || defaultDate;
                    let rowData = {
                        TenSanpham: (getValue("ten") || "").toString().trim(),
                        MaSanpham: (getValue("ma") || "").toString().trim(),
                        HangSX: (getValue("hang") || "").toString().trim(),
                        DonVi: (getValue("donvi") || "").toString().trim(),
                        SLCu: parseQuantity(getValue("slcu")),
                        SLMoi: parseQuantity(getValue("slmoi")),
                        NhaCC: (getValue("nhacc") || "").toString().trim(),
                        NgayCanHang: parsedDate,
                        GhiChu: (getValue("ghichu") || "").toString().trim(),
                        YCMakho: "",
                        hasManualDate: Boolean(getValue("ngay"))
                    };
                    rowData.SLToSave = resolveFinalQuantity(rowData);
                    rowData = applyKhoData(rowData);
                    if (!rowData.hasManualDate) {
                        rowData.NgayCanHang = defaultDate;
                    }
                    // Kiểm tra ngày hợp lệ (phải là ngày tương lai)
                    if (rowData.NgayCanHang && !isValidFutureDate(rowData.NgayCanHang)) {
                        hasInvalidDate = true;
                        rowData.NgayCanHang = today;
                    }
                    return rowData;
                })
                .filter((row) => {
                    if (!row.TenSanpham) return false;
                    if (
                        normalizedHeaderTexts.slmoi &&
                        normalizeHeader(row.SLMoi) === normalizedHeaderTexts.slmoi
                    ) {
                        return false;
                    }
                    if (
                        normalizedHeaderTexts.slcu &&
                        normalizeHeader(row.SLCu) === normalizedHeaderTexts.slcu
                    ) {
                        return false;
                    }
                    const isNumericName = /^[0-9]+(\.[0-9]+)*$/.test(row.TenSanpham);
                    if (isNumericName) return false;
                    const otherFields = [
                        row.MaSanpham,
                        row.HangSX,
                        row.DonVi,
                        row.SLCu,
                        row.SLMoi,
                        row.NhaCC
                    ];
                    const hasOtherData = otherFields.some(
                        (value) => value !== null && value !== ""
                    );
                    return hasOtherData;
                });

            allRows = allRows.concat(batchRows);
            processedCount = endIndex;

            // Cập nhật feedback với tiến trình
            if (dataRows.length > BATCH_SIZE) {
                const progress = Math.round((processedCount / dataRows.length) * 100);
                setFeedback(`Đang xử lý: ${processedCount}/${dataRows.length} dòng (${progress}%)...`, false);
            }

            // Nếu còn dữ liệu, xử lý batch tiếp theo
            if (endIndex < dataRows.length) {
                // Sử dụng requestAnimationFrame hoặc setTimeout để cho phép UI update
                await new Promise(resolve => {
                    if (window.requestAnimationFrame) {
                        requestAnimationFrame(() => {
                            setTimeout(resolve, 0);
                        });
                    } else {
                        setTimeout(resolve, 0);
                    }
                });
                return processBatch(endIndex);
            } else {
                // Đã xử lý xong tất cả
                if (!allRows.length) {
                    setFeedback("Không tìm thấy dữ liệu hợp lệ trong tệp.", true);
                    clearTable();
                    return;
                }

                await renderRows(allRows);
                if (hasInvalidDate) {
                    setFeedback(`Đã nhập ${allRows.length} dòng từ tệp Excel. Lưu ý: Một số ngày cần hàng trong quá khứ đã được tự động điều chỉnh thành ngày hôm nay.`, false);
                } else {
                    setFeedback(`Đã nhập ${allRows.length} dòng từ tệp Excel.`, false);
                }
            }
        };

        // Bắt đầu xử lý từ batch đầu tiên
        await processBatch(0);
    }

    function handleFileChange(event) {
        const file = event.target.files[0];
        if (!file) {
            setFeedback("Chưa chọn tệp Excel.", true);
            clearTable();
            return;
        }

        if (!/\.(xlsx|xls|xlsm)$/i.test(file.name)) {
            setFeedback("Vui lòng chọn tệp Excel (.xlsx, .xls, .xlsm).", true);
            fileInput.value = "";
            clearTable();
            return;
        }

        // Giới hạn kích thước file: 10MB
        const maxFileSize = 10 * 1024 * 1024; // 10MB
        if (file.size > maxFileSize) {
            setFeedback("Tệp quá lớn. Vui lòng chọn tệp nhỏ hơn 10MB.", true);
            fileInput.value = "";
            clearTable();
            return;
        }

        setFeedback("Đang xử lý tệp Excel, vui lòng đợi...", false);
        const isLegacyXls = /\.xls$/i.test(file.name) && !/\.xlsx$/i.test(file.name);
        const reader = new FileReader();

        reader.onload = function (e) {
            try {
                const data = e.target.result;
                // Xử lý file trong background để không block UI
                setTimeout(() => {
                    try {
                        const workbook = XLSX.read(isLegacyXls ? data : new Uint8Array(data), {
                            type: isLegacyXls ? "binary" : "array"
                        });
                        if (!workbook.SheetNames.length) {
                            setFeedback("Tệp không chứa sheet nào.", true);
                            clearTable();
                            return;
                        }
                        const sheet = workbook.Sheets[workbook.SheetNames[0]];
                        const sheetRows = XLSX.utils.sheet_to_json(sheet, {
                            header: 1,
                            defval: "",
                            blankrows: false
                        });
                        // Xử lý sheet theo batch để tránh block UI
                        processSheetAsync(sheetRows);
                    } catch (error) {
                        console.error("Lỗi khi đọc tệp Excel:", error);
                        setFeedback(
                            `Không thể đọc tệp Excel. ${error && error.message ? error.message : "Vui lòng kiểm tra lại."}`,
                            true
                        );
                        clearTable();
                    }
                }, 0);
            } catch (error) {
                console.error("Lỗi khi đọc tệp Excel:", error);
                setFeedback(
                    `Không thể đọc tệp Excel. ${error && error.message ? error.message : "Vui lòng kiểm tra lại."}`,
                    true
                );
                clearTable();
            }
        };
        reader.onerror = function () {
            setFeedback("Đã xảy ra lỗi khi đọc tệp. Vui lòng thử lại.", true);
            clearTable();
        };

        if (isLegacyXls) {
            reader.readAsBinaryString(file);
        } else {
            reader.readAsArrayBuffer(file);
        }
    }

    function ensureXlsxLibrary(readyCallback) {
        if (typeof window.XLSX !== "undefined") {
            readyCallback();
            return;
        }

        const script = document.createElement("script");
        script.src =
            "https://cdnjs.cloudflare.com/ajax/libs/xlsx/0.18.5/xlsx.full.min.js";
        script.async = true;
        script.onload = readyCallback;
        script.onerror = function () {
            setFeedback("Không thể tải thư viện XLSX. Vui lòng kiểm tra kết nối.", true);
        };
        document.head.appendChild(script);
    }

    ensureXlsxLibrary(() => {
        fileInput.addEventListener("change", handleFileChange);
    });

    if (ngayCanHangDefault) {
        ngayCanHangDefault.addEventListener("change", (event) => {
            const newDate = event.target.value || "";
            if (newDate && !isValidFutureDate(newDate)) {
                const today = new Date().toISOString().slice(0, 10);
                event.target.value = today;
                setFeedback("Ngày cần hàng phải là ngày tương lai. Đã tự động điều chỉnh thành ngày hôm nay.", true);
                setTimeout(() => {
                    setFeedback("", false);
                }, 3000);
                updateRowsDefaultDate(today);
            } else {
            updateRowsDefaultDate(newDate);
            }
        });
    }

    // Validation khi submit form
    const form = document.querySelector("form");
    if (form) {
        form.addEventListener("submit", function(event) {
            // Kiểm tra ngày cần hàng chính
            if (ngayCanHangDefault && ngayCanHangDefault.value) {
                if (!isValidFutureDate(ngayCanHangDefault.value)) {
                    event.preventDefault();
                    setFeedback("Ngày cần hàng phải là ngày tương lai. Vui lòng chọn lại ngày hợp lệ.", true);
                    ngayCanHangDefault.focus();
                    return false;
                }
            }

            // Kiểm tra các ngày cần hàng từ Excel
            const dateInputs = hiddenInputsWrapper.querySelectorAll('input[name="VTNgayCanHang"]');
            let hasInvalidDateInForm = false;
            dateInputs.forEach((input) => {
                if (input.value && !isValidFutureDate(input.value)) {
                    hasInvalidDateInForm = true;
                }
            });

            if (hasInvalidDateInForm) {
                event.preventDefault();
                setFeedback("Có một số ngày cần hàng trong quá khứ. Vui lòng kiểm tra và sửa lại trước khi gửi.", true);
                return false;
            }
        });
    }
});
