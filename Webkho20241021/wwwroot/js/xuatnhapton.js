document.addEventListener("DOMContentLoaded", () => {
    const panel = document.querySelector("[data-detail-panel]");
    if (!panel) {
        return;
    }

    const overlay = panel.querySelector(".inventory-detail-overlay");
    const closeButtons = panel.querySelectorAll("[data-close-detail]");
    const body = panel.querySelector("[data-detail-body]");
    const title = panel.querySelector("[data-detail-title]");
    const code = panel.querySelector("[data-detail-code]");
    const summaryIn = panel.querySelector("[data-summary-in]");
    const summaryOut = panel.querySelector("[data-summary-out]");
    const summaryStock = panel.querySelector("[data-summary-stock]");

    const triggers = document.querySelectorAll("[data-detail-trigger]");

    const togglePanel = (show) => {
        if (show) {
            panel.classList.add("active");
        } else {
            panel.classList.remove("active");
        }
    };

    const moneyFormatter = new Intl.NumberFormat("vi-VN", {
        style: "currency",
        currency: "VND",
        maximumFractionDigits: 0
    });

    const formatQuantity = (value, unit) => {
        const amount = typeof value === "number" ? value : Number(value ?? 0);
        return unit ? `${amount} ${unit}` : `${amount}`;
    };

    const formatCurrency = (value) => {
        if (value === null || value === undefined || isNaN(Number(value))) {
            return moneyFormatter.format(0);
        }
        return moneyFormatter.format(Number(value));
    };

    const renderRows = (transactions) => {
        if (!transactions || transactions.length === 0) {
            body.innerHTML = `<tr><td colspan="10">Không có giao dịch nào cho vật tư này.</td></tr>`;
            return;
        }

        body.innerHTML = transactions.map((item, index) => {
            const ngay = item.ngay ? new Date(item.ngay).toLocaleDateString("vi-VN") : "--";
            return `
                <tr>
                    <td>${index + 1}</td>
                    <td>${ngay}</td>
                    <td>${item.maChungTu ?? ""}</td>
                    <td>${item.loai ?? ""}</td>
                    <td>${item.doiTuong ?? ""}</td>
                    <td>${item.tkDoiUng ?? ""}</td>
                    <td>${item.maKho ?? ""}</td>
                    <td>${formatCurrency(item.donGia)}</td>
                    <td>${item.soLuong ?? 0} ${item.donVi ?? ""}</td>
                    <td>${formatCurrency(item.thanhTien)}</td>
                    <td>${item.ghiChu ?? ""}</td>
                </tr>
            `;
        }).join("");
    };

    const updateSummary = (summary, fallbackStock, fallbackUnit) => {
        const unit = summary?.donVi || fallbackUnit || "";
        summaryIn.textContent = formatQuantity(summary?.tongNhap ?? 0, unit);
        summaryOut.textContent = formatQuantity(summary?.tongXuat ?? 0, unit);
        const stock = summary?.tonKho ?? fallbackStock ?? 0;
        summaryStock.textContent = formatQuantity(stock, unit);
    };

    triggers.forEach(trigger => {
        trigger.addEventListener("click", () => {
            const url = trigger.dataset.detailUrl;
            if (!url) {
                return;
            }
            const itemName = trigger.dataset.detailName || "Chi tiết vật tư";
            const itemCode = trigger.dataset.detailCode || "--";
            const tonKho = Number(trigger.dataset.detailTon ?? 0);
            const donVi = trigger.dataset.detailDonvi || "";

            title.textContent = itemName;
            code.textContent = itemCode;
            body.innerHTML = `<tr><td colspan="11">Đang tải dữ liệu...</td></tr>`;
            updateSummary(null, tonKho, donVi);
            togglePanel(true);

            fetch(url, { method: "GET", headers: { "Accept": "application/json" } })
                .then(res => res.json())
                .then(result => {
                    if (!result || result.success !== true) {
                        const message = result?.message || "Không tải được dữ liệu. Vui lòng thử lại.";
                        body.innerHTML = `<tr><td colspan="11">${message}</td></tr>`;
                        return;
                    }

                    updateSummary(result.data?.summary, tonKho, donVi);
                    renderRows(result.data?.transactions);
                })
                .catch(() => {
                    body.innerHTML = `<tr><td colspan="11">Có lỗi xảy ra khi tải dữ liệu.</td></tr>`;
                });
        });
    });

    const closePanel = () => togglePanel(false);

    closeButtons.forEach(btn => btn.addEventListener("click", closePanel));
    overlay?.addEventListener("click", closePanel);

    document.addEventListener("keydown", (evt) => {
        if (evt.key === "Escape" && panel.classList.contains("active")) {
            closePanel();
        }
    });

    document.querySelectorAll("[data-money]").forEach(cell => {
        const value = Number(cell.textContent);
        cell.textContent = formatCurrency(isNaN(value) ? 0 : value);
    });
});

