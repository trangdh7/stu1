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
        <td><input type="number" name="SL" placeholder="Số lượng" /></td>
        <td><input type="text" name="DonVi" placeholder="Đơn vị" /></td>
    `;

        // Gắn sự kiện tìm kiếm cho ô input mới trong hàng
        const tenSanphamInput = newRow.querySelector("input[name='TenSanpham']");
        if (tenSanphamInput) {
            tenSanphamInput.addEventListener("input", searchProducts);
        }

        tableBody.appendChild(newRow);
    }


    // Kiểm tra các ô input trong hàng cuối cùng
    function checkLastRowInputs() {
        const lastRow = tableBody.lastElementChild;
        if (!lastRow) return;

        // Lấy tất cả các input trong hàng cuối cùng (trừ ô "Mã kho")
        const inputs = Array.from(lastRow.querySelectorAll("input")).filter(input => input.name !== 'YCMakho');

        // Kiểm tra xem tất cả các ô ngoài "Mã kho" có đầy đủ thông tin không
        const allFilled = inputs.every(input => input.value.trim() !== "");

        // Nếu tất cả các ô đã đầy đủ thì thêm hàng mới
        if (allFilled) {
            addNewRow();
        }
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

                                // Kiểm tra để thêm hàng mới nếu tất cả thông tin đã đầy đủ
                                checkLastRowInputs();
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
    tenSanphamInput.addEventListener("input", searchProducts);

    // Lắng nghe sự kiện thay đổi input để kiểm tra các ô của hàng cuối cùng
    tableBody.addEventListener("input", checkLastRowInputs);

    // Không tự động thêm hàng đầu tiên khi trang vừa tải, chỉ thêm khi người dùng điền đủ thông tin
});
