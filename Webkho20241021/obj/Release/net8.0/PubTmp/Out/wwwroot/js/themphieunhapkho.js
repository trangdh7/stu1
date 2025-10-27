document.getElementById("mayeucau").addEventListener("change", async function () {
    const mayeucau = this.value;
    if (!mayeucau) return;

    try {
        const pathSegments = window.location.pathname.split('/');
        const area = pathSegments.length > 1 ? pathSegments[1] : '';

        const response = await fetch(`/${area}/Yeucau/GetDataByMaYeucau?mayeucau=${mayeucau}`);
        if (response.ok) {
            const data = await response.json();

            document.getElementById("ycmanguoidung").value = data.maNguoidung || "";
            document.getElementById("maduan").value = data.maDuan || "";

            // ?i?n thông tin v?t t? vào b?ng
            const tableBody = document.getElementById("table-body");
            tableBody.innerHTML = ""; // Xóa n?i dung c?

            data.vtPhieuMuaHang.forEach((item, index) => {
                const row = `
                    <tr>
                        <td>${index + 1}</td>
                        <td><input type="text" name="TenSanpham[]" value="${item.tenSanpham}" readonly /></td>
                        <td><input type="text" name="MaSanpham[]" value="${item.maSanpham}" readonly /></td>
                        <td><input type="text" name="Makho[]" value="${item.makho}" readonly /></td>
                        <td><input type="text" name="HangSX[]" value="${item.hangSX}" readonly /></td>
                        <td><input type="text" name="NhaCC[]" value="${item.nhaCC}" readonly /></td>
                        <td><input type="number" name="SL[]" value="${item.sl}" readonly /></td>
                        <td><input type="text" name="DonVi[]" value="${item.donVi}" readonly /></td>
                    </tr>
                `;
                tableBody.insertAdjacentHTML("beforeend", row);
            });
        } else {
            alert("Không th? l?y d? li?u. Vui lòng th? l?i!");
        }
    } catch (error) {
        console.error("L?i khi l?y d? li?u:", error);
    }
});
