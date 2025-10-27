document.addEventListener("DOMContentLoaded", function () {
    const searchInput = document.getElementById("timkiem");
    const hangsxFilter = document.getElementById("hangsx-filter");
    const table = document.querySelector(".tablekhotong");
    const rows = table.querySelectorAll("tbody tr");

    searchInput.addEventListener("keyup", filterTable);
    hangsxFilter.addEventListener("change", filterTable);

    function filterTable() {
        const searchFilter = searchInput.value.toLowerCase();
        const hangsxFilterValue = hangsxFilter.value;

        rows.forEach(row => {
            const productName = row.cells[0].textContent.toLowerCase(); // Tên sản phẩm
            const productCode = row.cells[1].textContent.toLowerCase(); // Mã sản phẩm
            const hangSX = row.cells[3].textContent; // Hãng sản xuất

            const matchesSearch = productName.includes(searchFilter) || productCode.includes(searchFilter);
            const matchesHangsx = hangsxFilterValue === "" || hangSX === hangsxFilterValue;

            if (matchesSearch && matchesHangsx) {
                row.style.display = ""; // Hiện hàng
            } else {
                row.style.display = "none"; // Ẩn hàng
            }
        });
    }
});
