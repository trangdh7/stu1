// Lấy mã nhân viên từ session (giả định đã được set)
let currentUser = null;

// Xử lý khi thay đổi loại nhập kho
function handleLoaiNhapkhoChange() {
    const loai = document.getElementById("loainhapkho").value;
    const maduanSelect = document.getElementById("maduan");
    const tableBody = document.getElementById("table-body");
    const searchSection = document.getElementById("searchSection");
    
    // Ẩn section search khi đổi loại
    if (searchSection) {
        searchSection.style.display = "none";
    }
    
    // Xóa dữ liệu cũ
    tableBody.innerHTML = '<tr><td colspan="9" style="text-align:center;">Chọn dự án hoặc đợi load dữ liệu...</td></tr>';
    
    if (loai === "duan") {
        // Từ dự án: cho phép chọn mã dự án
        maduanSelect.removeAttribute("disabled");
        maduanSelect.setAttribute("required", "required");
    } else if (loai === "canhan") {
        // Từ cá nhân: không cần mã dự án
        maduanSelect.removeAttribute("required");
        maduanSelect.value = "";
        // Load vật tư từ kho cá nhân
        loadVatTuTuKhoCaNhan();
    } else {
        maduanSelect.removeAttribute("required");
        maduanSelect.setAttribute("disabled", "disabled");
    }
}

// Xử lý khi chọn mã dự án
function handleDuanChange() {
    const maduan = document.getElementById("maduan").value;
    const loai = document.getElementById("loainhapkho").value;
    
    if (!maduan || loai !== "duan") {
        const tableBody = document.getElementById("table-body");
        const searchSection = document.getElementById("searchSection");
        if (searchSection) {
            searchSection.style.display = "none";
        }
        tableBody.innerHTML = '<tr><td colspan="9" style="text-align:center;">Chọn dự án để xem vật tư...</td></tr>';
        return;
    }
    
    console.log("Loading vật tư cho dự án:", maduan);
    loadVatTuTuKhoDuan(maduan);
}

// Load vật tư từ kho dự án
async function loadVatTuTuKhoDuan(maduan) {
    try {
        const pathSegments = window.location.pathname.split('/');
        const area = pathSegments.length > 1 ? pathSegments[1] : '';
        
        console.log(`Fetching: /${area}/Yeucau/GetDataByMaDuan?maduan=${maduan}`);
        
        const response = await fetch(`/${area}/Yeucau/GetDataByMaDuan?maduan=${encodeURIComponent(maduan)}`);
        
        if (!response.ok) {
            const errorText = await response.text();
            console.error("Response error:", response.status, errorText);
            alert(`Không thể lấy dữ liệu từ kho dự án (${response.status}). Vui lòng thử lại!`);
            return;
        }
        
        const data = await response.json();
        console.log("Received data:", data);
        
        // Hiển thị thông tin debug nếu có
        if (data.debug) {
            console.log("=== DEBUG INFO ===");
            console.log("Total khoduans records:", data.debug.totalRecords);
            console.log("Records with matching DAMaDuan:", data.debug.matchingDuanCount);
            console.log("Distinct TrangThai values:", data.debug.distinctStatuses);
            console.log("Records with SL > 0:", data.debug.withSL);
            console.log("Returned items count:", data.debug.returnedCount);
            console.log("==================");
            
            // Cảnh báo nếu có dữ liệu nhưng không trả về
            if (data.debug.matchingDuanCount > 0 && data.debug.returnedCount === 0) {
                let msg = `Tìm thấy ${data.debug.matchingDuanCount} bản ghi với mã dự án "${maduan}", nhưng không có bản ghi nào có SL > 0.\n\n`;
                msg += `Các trạng thái có trong database: ${data.debug.distinctStatuses.join(", ") || "NULL"}\n\n`;
                msg += "Có thể vật tư có SL = 0 hoặc NULL. Vui lòng kiểm tra database.";
                alert(msg);
            } else if (data.debug.matchingDuanCount === 0) {
                alert(`Không tìm thấy bản ghi nào với mã dự án "${maduan}" trong bảng khoduans.\n\nVui lòng kiểm tra:\n1. Mã dự án có đúng không?\n2. Có dữ liệu trong bảng khoduans chưa?\n3. DAMaDuan có giá trị trong database không?`);
            }
        }
        
        // Hiển thị lỗi nếu có
        if (data.error) {
            console.error("Server error:", data.error);
            alert("Lỗi server: " + data.error);
            return;
        }
        
        // Cập nhật mã nhân viên
        if (document.getElementById("ycmanguoidung")) {
            document.getElementById("ycmanguoidung").value = data.maNguoidung || "";
        }
        
        // Load vật tư vào bảng
        const items = data.vtPhieuMuaHang || [];
        console.log("Items to load:", items.length);
        
        if (items.length > 0) {
            loadItemsToTable(items, true); // true = hiển thị section search
        } else {
            // Ẩn section search khi không có dữ liệu
            const searchSection = document.getElementById("searchSection");
            if (searchSection) {
                searchSection.style.display = "none";
            }
            
            // Hiển thị thông báo chi tiết hơn
            const tableBody = document.getElementById("table-body");
            tableBody.innerHTML = `
                <tr>
                    <td colspan="9" style="text-align:center; padding: 20px;">
                        <div style="color: #999;">
                            Không có vật tư nào trong kho dự án này.<br>
                            <small style="font-size: 12px;">
                                Mã dự án: ${maduan}<br>
                                ${data.debug ? `Tìm thấy ${data.debug.matchingDuanCount} bản ghi nhưng không có SL > 0` : 'Vui lòng kiểm tra dữ liệu trong database'}
                            </small>
                        </div>
                    </td>
                </tr>
            `;
        }
    } catch (error) {
        console.error("Lỗi khi lấy dữ liệu:", error);
        alert("Có lỗi xảy ra khi lấy dữ liệu: " + error.message);
    }
}

// Load vật tư từ kho cá nhân
async function loadVatTuTuKhoCaNhan() {
    try {
        const pathSegments = window.location.pathname.split('/');
        const area = pathSegments.length > 1 ? pathSegments[1] : '';
        
        const response = await fetch(`/${area}/Yeucau/GetDataKhoCaNhan`);
        if (response.ok) {
            const data = await response.json();
            
            // Cập nhật mã nhân viên
            document.getElementById("ycmanguoidung").value = data.maNguoidung || "";
            
            // Load vật tư vào bảng (từ kho cá nhân - không hiển thị section search)
            loadItemsToTable(data.vtKhoCaNhan || [], false);
        } else {
            alert("Không thể lấy dữ liệu từ kho cá nhân. Vui lòng thử lại!");
        }
    } catch (error) {
        console.error("Lỗi khi lấy dữ liệu:", error);
        alert("Có lỗi xảy ra khi lấy dữ liệu!");
    }
}

// Lưu toàn bộ dữ liệu vật tư từ dự án
let allItemsFromDuan = [];

// Load vật tư vào bảng
function loadItemsToTable(items, showSearchSection = false) {
    const tableBody = document.getElementById("table-body");
    const searchSection = document.getElementById("searchSection");
    const searchVatTuInput = document.getElementById("searchVatTuInput");
    
    // Lưu toàn bộ dữ liệu nếu load từ dự án
    if (showSearchSection && items) {
        allItemsFromDuan = items;
        searchSection.style.display = "block";
        if (searchVatTuInput) {
            searchVatTuInput.value = "";
        }
    } else {
        allItemsFromDuan = [];
        searchSection.style.display = "none";
    }
    
    // Render bảng
    renderTable(items);
}

// Render bảng với dữ liệu
function renderTable(items) {
    const tableBody = document.getElementById("table-body");
    tableBody.innerHTML = ""; // Xóa nội dung cũ
    
    if (!items || items.length === 0) {
        tableBody.innerHTML = '<tr><td colspan="9" style="text-align:center;">Không có vật tư nào.</td></tr>';
        return;
    }
    
    items.forEach((item, index) => {
        // Escape HTML để tránh XSS
        const escapeHtml = (str) => {
            if (!str) return '';
            const div = document.createElement('div');
            div.textContent = str;
            return div.innerHTML;
        };
        
        const row = `
            <tr>
                <td><input type="checkbox" class="select-item" /></td>
                <td>${index + 1}</td>
                <td><input type="text" name="TenSanpham" value="${escapeHtml(item.tenSanpham || '')}" readonly /></td>
                <td><input type="text" name="MaSanpham" value="${escapeHtml(item.maSanpham || '')}" readonly /></td>
                <td><input type="text" name="Makho" value="${escapeHtml(item.makho || '')}" readonly /></td>
                <td><input type="text" name="HangSX" value="${escapeHtml(item.hangSX || '')}" readonly /></td>
                <td><input type="text" name="NhaCC" value="${escapeHtml(item.nhaCC || '')}" readonly /></td>
                <td><input type="number" name="SL" value="${item.sl || 0}" min="1" max="${item.sl || 0}" placeholder="Nhập số lượng (tối đa: ${item.sl || 0})" /></td>
                <td><input type="text" name="DonVi" value="${escapeHtml(item.donVi || '')}" readonly /></td>
            </tr>
        `;
        tableBody.insertAdjacentHTML("beforeend", row);
    });
}

// Filter vật tư theo tên
function filterVatTu(searchTerm) {
    if (!searchTerm || searchTerm.trim() === "") {
        // Hiển thị tất cả nếu không có từ khóa
        renderTable(allItemsFromDuan);
        return;
    }
    
    const searchLower = searchTerm.toLowerCase().trim();
    const filtered = allItemsFromDuan.filter(item => {
        const tenSanpham = (item.tenSanpham || '').toLowerCase();
        return tenSanpham.includes(searchLower);
    });
    
    renderTable(filtered);
}

// Đảm bảo mã nhân viên được điền tự động khi load trang
document.addEventListener("DOMContentLoaded", function() {
    const pathSegments = window.location.pathname.split('/');
    const area = pathSegments.length > 1 ? pathSegments[1] : '';
    
    // Lấy mã nhân viên từ session (cần tạo endpoint hoặc lấy từ hidden field)
    fetch(`/${area}/Yeucau/GetCurrentUser`)
        .then(response => response.json())
        .then(data => {
            if (data.maNguoidung) {
                document.getElementById("ycmanguoidung").value = data.maNguoidung;
            }
        })
        .catch(error => console.error("Lỗi khi lấy mã nhân viên:", error));
    
    // Event listener cho search input
    const searchVatTuInput = document.getElementById("searchVatTuInput");
    if (searchVatTuInput) {
        let searchTimeout;
        searchVatTuInput.addEventListener('input', function() {
            const searchTerm = this.value.trim();
            clearTimeout(searchTimeout);
            searchTimeout = setTimeout(() => {
                filterVatTu(searchTerm);
            }, 300); // Debounce 300ms
        });
    }
});
