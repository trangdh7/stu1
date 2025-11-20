document.addEventListener("DOMContentLoaded", () => {
    const table = document.querySelector('[data-table="tongkho"]');
    if (!table) {
        return;
    }

    const rows = Array.from(table.querySelectorAll("tbody tr"));
    const searchInput = document.getElementById("timkiem");
    const hangsxFilter = document.getElementById("hangsx-filter");
    const nhaCCFilter = document.getElementById("nhacc-filter");

    const listeners = [
        { el: searchInput, event: "input" },
        { el: hangsxFilter, event: "change" },
        { el: nhaCCFilter, event: "change" }
    ];

    listeners.forEach(({ el, event }) => {
        if (el) {
            el.addEventListener(event, applyFilters);
        }
    });

    function applyFilters() {
        const searchValue = (searchInput?.value ?? "").trim().toLowerCase();
        const hangsxValue = hangsxFilter?.value ?? "";
        const nhaCCValue = nhaCCFilter?.value ?? "";

        rows.forEach(row => {
            const name = row.dataset.ten?.toLowerCase() ?? "";
            const code = row.dataset.ma?.toLowerCase() ?? "";
            const hangsx = row.dataset.hangsx ?? "";
            const nhacc = row.dataset.nhacc ?? "";

            const matchesSearch =
                !searchValue ||
                name.includes(searchValue) ||
                code.includes(searchValue);

            const matchesHangsx = !hangsxValue || hangsx === hangsxValue;
            const matchesNhaCC = !nhaCCValue || nhacc === nhaCCValue;

            row.style.display = matchesSearch && matchesHangsx && matchesNhaCC ? "" : "none";
        });
    }
});
