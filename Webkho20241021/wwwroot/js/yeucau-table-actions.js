/* Nút thu nhỏ / phóng to bảng chi tiết - dùng cho trang Phieu*, không dùng yeucau.js */
$(document).ready(function () {
    $(document).on('click', '.btn-minimize-table', function () {
        var $nav = $(this).closest('.bodyyeucau-thietbi');
        $nav.removeClass('table-maximized');
        $nav.find('.btn-maximize-table').show();
        $nav.find('.btn-restore-table').hide();
        $nav.hide();
    });
    $(document).on('click', '.btn-maximize-table', function () {
        var $nav = $(this).closest('.bodyyeucau-thietbi');
        $nav.addClass('table-maximized');
        $nav.find('.btn-maximize-table').hide();
        $nav.find('.btn-restore-table').show();
        if (typeof syncTableThietbiStickyHeader === 'function') syncTableThietbiStickyHeader();
    });
    $(document).on('click', '.btn-restore-table', function () {
        var $nav = $(this).closest('.bodyyeucau-thietbi');
        $nav.removeClass('table-maximized');
        $nav.find('.btn-restore-table').hide();
        $nav.find('.btn-maximize-table').show();
        if (typeof syncTableThietbiStickyHeader === 'function') syncTableThietbiStickyHeader();
    });
});
