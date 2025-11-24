# -*- coding: utf-8 -*-
import re

file_path = r"H:\Webkho20241021\Webkho20241021\Webkho20241021\Areas\QuanLiDuAn\Controllers\YeucauController.cs"

# Đọc file với nhiều encoding khác nhau để tìm encoding đúng
encodings = ['utf-8', 'windows-1252', 'iso-8859-1', 'cp1252']

content = None
for enc in encodings:
    try:
        with open(file_path, 'r', encoding=enc, errors='ignore') as f:
            content = f.read()
        print(f"Read file with encoding: {enc}")
        break
    except:
        continue

if content is None:
    print("Could not read file")
    exit(1)

# Thay thế các chuỗi bị lỗi - sử dụng các pattern linh hoạt hơn
replacements = [
    # Tìm các chuỗi có chứa "nh?n hng" hoặc biến thể
    (r'"\s*[^\"]*nh[^\"]*n[^\"]*h[^\"]*ng"', '"Đã nhận hàng"'),
    (r'"\s*[^\"]*ang[^\"]*mua[^\"]*h[^\"]*ng"', '"Đang mua hàng"'),
    (r'"\s*[^\"]*ang[^\"]*ch[^\"]*[^\"]*bo[^\"]*gi[^\"]*"', '"Đang chờ báo giá"'),
    (r'"\s*[^\"]*bo[^\"]*gi[^\"]*"', '"Đã báo giá"'),
    (r'"Ch[^\"]*[^\"]*thanh[^\"]*ton"', '"Chờ thanh toán"'),
    (r'"\s*[^\"]*thanh[^\"]*ton"', '"Đã thanh toán"'),
    (r'"BP\s*mua[^\"]*h[^\"]*ng"', '"BP mua hàng"'),
    (r'"BP\s*k[^\"]*[^\"]*ton"', '"BP kế toán"'),
    (r'"Ch[^\"]*[^\"]*ly[^\"]*h[^\"]*ng"', '"Chờ lấy hàng"'),
    (r'"\s*[^\"]*ang[^\"]*chu[^\"]*n[^\"]*b[^\"]*[^\"]*h[^\"]*ng"', '"Đang chuẩn bị hàng"'),
    (r'"\s*[^\"]*xu[^\"]*t[^\"]*kho"', '"Đã xuất kho"'),
    (r'"\s*[^\"]*ly[^\"]*h[^\"]*ng"', '"Đã lấy hàng"'),
    (r'"Ch[^\"]*[^\"]*ngu[^\"]*i[^\"]*yu[^\"]*cu[^\"]*xc[^\"]*nh[^\"]*n"', '"Chờ người yêu cầu xác nhận"'),
    (r'"\s*[^\"]*xc[^\"]*nh[^\"]*n[^\"]*nh[^\"]*n[^\"]*h[^\"]*ng"', '"Đã xác nhận nhận hàng"'),
    (r'"Hon[^\"]*thnh"', '"Hoàn thành"'),
    (r'"\s*[^\"]*ang[^\"]*mu[^\"]*n"', '"Đang mượn"'),
    (r'"\s*[^\"]*ang[^\"]*s[^\"]*[^\"]*d[^\"]*ng"', '"Đang sử dụng"'),
    (r'"Ch[^\"]*[^\"]*nh[^\"]*p[^\"]*kho"', '"Chờ nhập kho"'),
    (r'"S[^\"]*n[^\"]*s[^\"]*ng[^\"]*nh[^\"]*p[^\"]*kho"', '"Sẵn sàng nhập kho"'),
    (r'"\s*[^\"]*nh[^\"]*p[^\"]*kho"', '"Đã nhập kho"'),
    (r'"Qu[^\"]*n[^\"]*l[^\"]*[^\"]*d[^\"]*[^\"]*n"', '"Quản lí dự án"'),
    (r'"Gim[^\"]*dc"', '"Giám đốc"'),
    (r'"\s*[^\"]*duy[^\"]*t"', '"Đã duyệt"'),
    (r'"\s*[^\"]*t[^\"]*[^\"]*ch[^\"]*i"', '"Đã từ chối"'),
    (r'"Tn[^\"]*kho"', '"Tồn kho"'),
]

# Thực hiện thay thế
for pattern, replacement in replacements:
    content = re.sub(pattern, replacement, content, flags=re.IGNORECASE)

# Ghi lại file với UTF-8
with open(file_path, 'w', encoding='utf-8') as f:
    f.write(content)

print("Fixed encoding for file")

