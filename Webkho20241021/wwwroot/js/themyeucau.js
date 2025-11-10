document.addEventListener("DOMContentLoaded", function () {
    const tableBody = document.getElementById("table-body");
    const searchResultsContainer = document.getElementById("searchResults");

    // Thêm một hàng mới vào bảng
    function addNewRow() {
        const newRow = document.createElement("tr");
        const rowCount = tableBody.rows.length + 1;

        newRow.innerHTML = `
        <td>${rowCount}</td>
        <td><input type="text" class="TenSanpham" name="TenSanpham" placeholder="Tên vật tư"/></td>
        <td><input type="text" name="MaSanpham" placeholder="Mã vật tư" /></td>
        <td><input type="text" name="YCMakho" placeholder="Mã kho" readonly/></td>
        <td><input type="text" name="HangSX" placeholder="Hãng SX" /></td>
        <td><input type="text" name="NhaCC" placeholder="Nhà cung cấp" /></td>
        <td><input type="number" name="SL" placeholder="Số lượng" min="1" step="1" onkeypress="return (event.charCode >= 48 && event.charCode <= 57)" oninput="this.value = this.value.replace(/[^0-9]/g, ''); if(this.value && parseInt(this.value) < 1) this.value = 1;" required /></td>
        <td><input type="text" name="DonVi" placeholder="Đơn vị" required /></td>
        <td><button type="button" class="btn-remove-row" onclick="removeRow(this)">Xóa</button></td>
    `;

        // Gắn sự kiện tìm kiếm cho ô input mới trong hàng
        const tenSanphamInput = newRow.querySelector("input[name='TenSanpham']");
        if (tenSanphamInput) {
            tenSanphamInput.addEventListener("input", searchProducts);
        }

        tableBody.appendChild(newRow);
        updateRowNumbers();
        // Chỉ gọi getThongbaoData nếu hàm tồn tại (tránh lỗi nếu không load yeucau.js)
        if (typeof getThongbaoData === 'function') {
            getThongbaoData();
        }
    }

    // Cập nhật số thứ tự các hàng và hiển thị/ẩn nút xóa
    function updateRowNumbers() {
        const rows = tableBody.querySelectorAll('tr');
        rows.forEach((row, index) => {
            row.querySelector('td:first-child').textContent = index + 1;
            // Hiển thị nút xóa cho các hàng từ hàng thứ 2 trở đi
            const removeBtn = row.querySelector('.btn-remove-row');
            if (removeBtn) {
                if (rows.length > 1 && index > 0) {
                    removeBtn.style.display = 'inline-block';
                } else {
                    removeBtn.style.display = 'none';
                }
            }
        });
    }

    // Xóa hàng
    window.removeRow = function(button) {
        const row = button.closest('tr');
        if (tableBody.rows.length > 1) {
            row.remove();
            updateRowNumbers();
        } else {
            alert('Phải có ít nhất 1 hàng vật tư!');
        }
    };

    // Gắn sự kiện cho nút thêm hàng
    const btnAddRow = document.getElementById('btn-add-row');
    if (btnAddRow) {
        btnAddRow.addEventListener('click', addNewRow);
    }

    // Hàm tìm kiếm sản phẩm
    // Hàm tìm kiếm sản phẩm
    function searchProducts(event) {
        const currentRow = event.target.closest('tr'); // Lấy hàng của input hiện tại
        const tenSanphamInput = currentRow.querySelector("input[name='TenSanpham']");
        const searchValue = tenSanphamInput ? tenSanphamInput.value : "";
        const pathSegments = window.location.pathname.split('/');
        const area = pathSegments.length > 1 ? pathSegments[1] : ''; // Giả sử area là segment đầu tiên sau dấu '/'

        const url = `/${area}/Yeucau/TimKiem?timkiem=${searchValue}`;

        if (searchValue.length > 0) {
            fetch(url)
                .then(response => response.json())
                .then(data => {
                    searchResultsContainer.style.display = "table-row-group";
                    searchResultsContainer.innerHTML = "";

                    if (data && data.length > 0) {
                        data.forEach((item, index) => {
                            const row = document.createElement("tr");
                            row.classList.add("search-row");
                            row.innerHTML = `
                            <td>${index + 1}</td>
                            <td>${item.tenSanpham || 'Không xác định'}</td>
                            <td>${item.maSanpham || 'Không xác định'}</td>
                            <td>${item.makho || 'Không xác định'}</td>
                            <td>${item.hangSX || 'Không xác định'}</td>
                            <td>${item.nhaCC || 'Không xác định'}</td>
                            <td>${item.sl === 0 ? 0 : item.sl || 'Không xác định'}</td>
                            <td>${item.donVi || 'Không xác định'}</td>
                        `;

                            row.addEventListener("click", () => {
                                // Điền dữ liệu vào ô input của hàng hiện tại
                                currentRow.querySelector("input[name='TenSanpham']").value = item.tenSanpham || '';
                                currentRow.querySelector("input[name='MaSanpham']").value = item.maSanpham || '';
                                currentRow.querySelector("input[name='YCMakho']").value = item.makho || '';
                                currentRow.querySelector("input[name='HangSX']").value = item.hangSX || '';
                                currentRow.querySelector("input[name='NhaCC']").value = item.nhaCC || '';
                                currentRow.querySelector("input[name='SL']").value = item.sl || '';
                                currentRow.querySelector("input[name='DonVi']").value = item.donVi || '';

                                // Ẩn bảng kết quả tìm kiếm
                                searchResultsContainer.style.display = "none";
                            });

                            searchResultsContainer.appendChild(row);
                        });
                    } 
                })
                .catch(error => {
                    console.error("Lỗi khi tìm kiếm sản phẩm:", error);
                    searchResultsContainer.innerHTML = "<tr><td colspan='8'>Lỗi khi tải dữ liệu</td></tr>";
                });
        } else {
            searchResultsContainer.innerHTML = "";
            searchResultsContainer.style.display = "none";
        }
    }


    // Lắng nghe sự kiện nhập liệu trong ô Tên sản phẩm để tìm kiếm khi người dùng nhập
    const tenSanphamInput = document.querySelector("input[name='TenSanpham']");
    if (tenSanphamInput) {
        tenSanphamInput.addEventListener("input", searchProducts);
    }

    // Xử lý chuyển hướng khi chọn "Yêu cầu nhập kho"
    const tenyeucauSelect = document.getElementById("tenyeucau");
    if (tenyeucauSelect) {
        tenyeucauSelect.addEventListener("change", function() {
            if (this.value === "Yêu cầu nhập kho") {
                window.location.href = "/NhanvienKythuat/Yeucau/ThemPhieunhapkho";
            }
        });
    }

    // Cập nhật số thứ tự và nút xóa khi trang load
    updateRowNumbers();
});
