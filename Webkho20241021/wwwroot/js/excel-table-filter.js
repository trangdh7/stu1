/**
 * Excel-like column filter for plain HTML tables (no DataTables).
 * - Adds a small filter button to each header cell (th/td in thead).
 * - Popup supports: search values, select all/none, multi-select, sort asc/desc, clear.
 * - Applies client-side filtering by toggling tbody rows.
 *
 * Usage:
 *   ExcelTableFilter.init('.table', { excludeHeaderTexts: ['Thao tác'] });
 *   ExcelTableFilter.sync(document.querySelector('.table')); // after rows changed
 */
(function (window, $) {
    'use strict';

    if (!$) {
        // jQuery is required in this codebase.
        return;
    }

    const NS = '__excelTableFilter';
    const DEFAULTS = {
        // Skip adding filter buttons for these header text labels.
        excludeHeaderTexts: ['Thao tác', 'Actions', 'Action'],
        // Skip columns by 0-based index.
        excludeColumnIndexes: [],
        // If true: ignore the first column when it's STT/TT.
        autoSkipIndexColumn: true,
        // Debounce for search in popup
        searchDebounceMs: 120
    };

    function normText(s) {
        return (s == null ? '' : String(s)).replace(/\s+/g, ' ').trim();
    }

    function getHeaderCells(table) {
        const $t = $(table);
        const $row = $t.find('thead tr').first();
        return $row.children('th,td');
    }

    function isExcludedColumn(state, headerText, colIdx) {
        if (state.opts.excludeColumnIndexes && state.opts.excludeColumnIndexes.indexOf(colIdx) >= 0) return true;
        const t = headerText.toLowerCase();
        for (const x of (state.opts.excludeHeaderTexts || [])) {
            if (t === String(x).toLowerCase()) return true;
        }
        if (state.opts.autoSkipIndexColumn && colIdx === 0) {
            // Skip common index columns
            if (t === 'stt' || t === 'tt' || t === '#') return true;
        }
        return false;
    }

    function getCellValue($cell) {
        // Prefer input/select values if present, else text
        const $input = $cell.find('input,select,textarea').first();
        if ($input.length) {
            return normText($input.val());
        }
        // If the cell contains icons/buttons, text() still captures labels if any.
        return normText($cell.text());
    }

    function ensureState(table, opts) {
        const el = table;
        if (!el[NS]) {
            el[NS] = {
                opts: $.extend(true, {}, DEFAULTS, opts || {}),
                // filters[colIdx] = Set<string> selected values; null => no filter
                filters: {},
                // cached unique values per column (rebuilt on sync)
                values: {},
                // popup dom
                $popup: null,
                openCol: null
            };
        } else if (opts) {
            // merge options if init called again
            el[NS].opts = $.extend(true, {}, el[NS].opts, opts);
        }
        return el[NS];
    }

    function buildValuesForColumn(table, colIdx) {
        const values = new Set();
        $(table).find('tbody tr').each(function () {
            const $row = $(this);
            // ignore fully hidden rows? we include both visible and hidden so filter options remain complete
            const $cells = $row.children('td,th');
            const $cell = $cells.eq(colIdx);
            if ($cell.length === 0) return;
            const v = getCellValue($cell);
            values.add(v === '' ? '(Trống)' : v);
        });
        return Array.from(values).sort((a, b) => a.localeCompare(b, 'vi', { sensitivity: 'base' }));
    }

    function applyFilters(table) {
        const state = ensureState(table);
        const activeCols = Object.keys(state.filters)
            .map(k => parseInt(k, 10))
            .filter(k => state.filters[k] && state.filters[k].size > 0);

        if (activeCols.length === 0) {
            $(table).find('tbody tr').show();
            updateHeaderIndicators(table);
            return;
        }

        $(table).find('tbody tr').each(function () {
            const $row = $(this);
            const $cells = $row.children('td,th');
            let ok = true;
            for (const colIdx of activeCols) {
                const $cell = $cells.eq(colIdx);
                const raw = $cell.length ? getCellValue($cell) : '';
                const v = raw === '' ? '(Trống)' : raw;
                const selected = state.filters[colIdx];
                if (selected && selected.size > 0 && !selected.has(v)) {
                    ok = false;
                    break;
                }
            }
            $row.toggle(ok);
        });

        updateHeaderIndicators(table);
    }

    function updateHeaderIndicators(table) {
        const state = ensureState(table);
        const $headers = getHeaderCells(table);
        $headers.each(function (i) {
            const $h = $(this);
            const $btn = $h.find('.excel-filter-btn');
            if ($btn.length === 0) return;
            const isActive = state.filters[i] && state.filters[i].size > 0;
            $btn.toggleClass('is-active', !!isActive);
        });
    }

    function closePopup(state) {
        if (state.$popup) {
            state.$popup.hide();
        }
        state.openCol = null;
    }

    function ensurePopup(state) {
        if (state.$popup && state.$popup.length) return state.$popup;

        const $p = $(`
            <div class="excel-filter-popup" style="display:none;">
              <div class="excel-filter-popup-inner">
                <div class="excel-filter-popup-top">
                  <input type="text" class="excel-filter-search" placeholder="Tìm trong cột..." />
                </div>
                <div class="excel-filter-popup-actions">
                  <button type="button" class="excel-filter-action" data-act="selectAll">Chọn tất cả</button>
                  <button type="button" class="excel-filter-action" data-act="clear">Bỏ lọc</button>
                  <button type="button" class="excel-filter-action" data-act="sortAsc">Sort A→Z</button>
                  <button type="button" class="excel-filter-action" data-act="sortDesc">Sort Z→A</button>
                </div>
                <div class="excel-filter-values"></div>
                <div class="excel-filter-popup-bottom">
                  <button type="button" class="excel-filter-apply">Áp dụng</button>
                  <button type="button" class="excel-filter-cancel">Đóng</button>
                </div>
              </div>
            </div>
        `);

        $('body').append($p);
        state.$popup = $p;

        // global close on click outside
        $(document).on('mousedown', function (e) {
            if (!state.$popup || !state.$popup.is(':visible')) return;
            const $target = $(e.target);
            if ($target.closest('.excel-filter-popup').length) return;
            if ($target.closest('.excel-filter-btn').length) return;
            closePopup(state);
        });

        // esc closes
        $(document).on('keydown', function (e) {
            if (e.key === 'Escape') {
                closePopup(state);
            }
        });

        return $p;
    }

    function renderPopupForColumn(table, colIdx, anchorEl) {
        const state = ensureState(table);
        const $headers = getHeaderCells(table);
        const headerText = normText($headers.eq(colIdx).text());
        if (isExcludedColumn(state, headerText, colIdx)) return;

        // Rebuild values cache for this column
        state.values[colIdx] = buildValuesForColumn(table, colIdx);

        const $popup = ensurePopup(state);
        state.openCol = colIdx;

        const selected = state.filters[colIdx] ? new Set(Array.from(state.filters[colIdx])) : null;

        const values = state.values[colIdx] || [];
        const $values = $popup.find('.excel-filter-values');
        $values.empty();

        const makeItem = (v) => {
            const id = `excel_filter_${Date.now()}_${Math.random().toString(16).slice(2)}`;
            const isChecked = selected ? selected.has(v) : true; // default: all selected when opening first time
            return $(`
              <label class="excel-filter-item">
                <input type="checkbox" class="excel-filter-check" value="" />
                <span class="excel-filter-item-text"></span>
              </label>
            `).attr('for', id).data('value', v).find('input')
                .prop('checked', isChecked)
                .end()
                .find('.excel-filter-item-text')
                .text(v)
                .end();
        };

        values.forEach(v => $values.append(makeItem(v)));

        // reset search
        const $search = $popup.find('.excel-filter-search');
        $search.val('');

        // position near anchor
        const rect = anchorEl.getBoundingClientRect();
        const top = rect.bottom + window.scrollY + 6;
        const left = rect.left + window.scrollX;
        $popup.css({ top: top + 'px', left: left + 'px' }).show();

        // title-ish via placeholder? Keep simple
        $search.attr('placeholder', `Lọc: ${headerText || 'Cột'}`);

        // bind events for this render
        $popup.off('click.excelFilter').on('click.excelFilter', '.excel-filter-action', function () {
            const act = $(this).data('act');
            if (act === 'selectAll') {
                $popup.find('.excel-filter-check:visible').prop('checked', true);
            } else if (act === 'clear') {
                // clear filter for this column
                state.filters[colIdx] = new Set(); // means none selected -> treat as no filter
                delete state.filters[colIdx];
                applyFilters(table);
                closePopup(state);
            } else if (act === 'sortAsc' || act === 'sortDesc') {
                const asc = act === 'sortAsc';
                const items = $values.children('.excel-filter-item').get();
                items.sort((a, b) => {
                    const va = normText($(a).data('value'));
                    const vb = normText($(b).data('value'));
                    const cmp = va.localeCompare(vb, 'vi', { sensitivity: 'base' });
                    return asc ? cmp : -cmp;
                });
                $values.empty().append(items);
            }
        });

        $popup.off('click.applyCancel')
            .on('click.applyCancel', '.excel-filter-apply', function () {
                const checked = new Set();
                $popup.find('.excel-filter-item').each(function () {
                    const $item = $(this);
                    const v = $item.data('value');
                    const isChecked = $item.find('.excel-filter-check').prop('checked');
                    if (isChecked) checked.add(v);
                });
                // If all are checked => no filter (like Excel)
                if (checked.size === (state.values[colIdx] || []).length) {
                    delete state.filters[colIdx];
                } else {
                    state.filters[colIdx] = checked;
                }
                applyFilters(table);
                closePopup(state);
            })
            .on('click.applyCancel', '.excel-filter-cancel', function () {
                closePopup(state);
            });

        // search filter (debounced)
        let t = null;
        $search.off('input.search').on('input.search', function () {
            clearTimeout(t);
            t = setTimeout(function () {
                const q = normText($search.val()).toLowerCase();
                $values.children('.excel-filter-item').each(function () {
                    const v = normText($(this).data('value')).toLowerCase();
                    $(this).toggle(!q || v.indexOf(q) >= 0);
                });
            }, state.opts.searchDebounceMs || 120);
        });
    }

    function addButtons(table) {
        const state = ensureState(table);
        const $headers = getHeaderCells(table);
        $headers.each(function (i) {
            const $h = $(this);
            const headerText = normText($h.text());
            if (isExcludedColumn(state, headerText, i)) return;

            // Avoid double insert
            if ($h.find('.excel-filter-btn').length) return;

            // Ensure header cell has positioning context
            if ($h.css('position') === 'static') {
                $h.css('position', 'relative');
            }

            const $btn = $(`
                <button type="button" class="excel-filter-btn" title="Lọc cột">
                  <span class="excel-filter-icon">▾</span>
                </button>
            `);
            $btn.on('click', function (e) {
                e.preventDefault();
                e.stopPropagation();
                const st = ensureState(table);
                // toggle same column
                if (st.$popup && st.$popup.is(':visible') && st.openCol === i) {
                    closePopup(st);
                    return;
                }
                renderPopupForColumn(table, i, this);
            });
            $h.append($btn);
        });
        updateHeaderIndicators(table);
    }

    function initOne(table, opts) {
        if (!table) return;
        const state = ensureState(table, opts);
        addButtons(table);
        // initial values cache (lazy per column), but we can build now for active filter UI
        updateHeaderIndicators(table);
        // apply any existing filters
        applyFilters(table);
        return state;
    }

    function findTables(selectorOrEl) {
        if (!selectorOrEl) return [];
        if (typeof selectorOrEl === 'string') return $(selectorOrEl).toArray();
        if (selectorOrEl instanceof Element) return [selectorOrEl];
        if (selectorOrEl && selectorOrEl.length && selectorOrEl[0] instanceof Element) return $(selectorOrEl).toArray();
        return [];
    }

    const ExcelTableFilter = {
        init: function (selectorOrEl, opts) {
            const tables = findTables(selectorOrEl);
            tables.forEach(t => initOne(t, opts));
        },
        sync: function (selectorOrEl) {
            // Re-add buttons if headers re-rendered, and re-apply filters
            const tables = findTables(selectorOrEl);
            tables.forEach(t => {
                ensureState(t); // keep existing selections
                addButtons(t);
                applyFilters(t);
            });
        },
        clearAll: function (selectorOrEl) {
            const tables = findTables(selectorOrEl);
            tables.forEach(t => {
                const state = ensureState(t);
                state.filters = {};
                applyFilters(t);
            });
        }
    };

    window.ExcelTableFilter = ExcelTableFilter;
})(window, window.jQuery);

