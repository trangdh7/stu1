$(document).ready(function () {
    // Hàm thêm hàng mới vào bảng
    function addNewRow() {
        var lastRow = $(".form-table tbody tr:last");
        var newRow = lastRow.clone(); // Nhân bản hàng cuối cùng
        var newIndex = parseInt(lastRow.find("td:first").text()) + 1; // Tăng STT
        newRow.find("input").val(""); // Xóa giá trị của các ô nhập
        newRow.find("td:first").text(newIndex); // Cập nhật STT
        $(".form-table tbody").append(newRow); // Thêm hàng mới vào bảng
        console.log("Thêm hàng mới:", newRow); // Ghi log hàng mới
    }

    // Sự kiện khi người dùng nhập dữ liệu
    $(document).on("input", "input[type='text'], input[type='number'], input[type='date']", function () {
        var $currentRow = $(this).closest("tr");
        var allFilled = true;

        // Kiểm tra xem tất cả ô trong hàng đã được điền chưa
        $currentRow.find("input").each(function () {
            if ($(this).val() === "") {
                allFilled = false;
                return false; // Thoát vòng lặp nếu tìm thấy ô trống
            }
        });

        console.log("Current Row Inputs:", $currentRow.find("input").map(function () { return $(this).val(); }).get());
        console.log("All Filled:", allFilled);

        // Nếu tất cả ô đã được điền, thêm hàng mới
        if (allFilled) {
            addNewRow(); // Thêm hàng mới
        }
    });
});
