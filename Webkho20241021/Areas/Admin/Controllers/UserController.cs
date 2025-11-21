using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Webkho_20241021.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Webkho_20241021.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class UserController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;
        private readonly SignInManager<User> _signInManager;

        public UserController(
            UserManager<User> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context,
            SignInManager<User> signInManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _signInManager = signInManager;
        }

        // Danh sách user
        public async Task<IActionResult> DanhSachUser(int page = 1, int pageSize = 20, string q = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;

            var query = _userManager.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var keyword = q.Trim();
                query = query.Where(u =>
                    (u.UserName ?? "").Contains(keyword) ||
                    (u.Name ?? "").Contains(keyword) ||
                    (u.manv ?? "").Contains(keyword) ||
                    (u.Email ?? "").Contains(keyword) ||
                    (u.PhoneNumber ?? "").Contains(keyword)
                );
            }

            var total = query.Count();
            var users = query
                .OrderBy(u => u.UserName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // Lấy roles cho mỗi user
            var usersWithRoles = new List<object>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var nguoidung = _context.nguoidungs.FirstOrDefault(nd => nd.MaNguoidung == user.manv);
                usersWithRoles.Add(new
                {
                    User = user,
                    Roles = roles,
                    NguoiDung = nguoidung
                });
            }

            ViewBag.Page = page;
            ViewBag.TotalPages = (int)System.Math.Ceiling(total / (double)pageSize);
            ViewBag.Q = q;
            ViewBag.AllRoles = _roleManager.Roles.Select(r => r.Name).ToList();

            return View(usersWithRoles);
        }

        // Tạo tài khoản - GET
        public IActionResult TaoTaiKhoan()
        {
            ViewBag.AllRoles = _roleManager.Roles.Select(r => r.Name).ToList();
            return View();
        }

        // Tạo tài khoản - POST
        [HttpPost]
        public async Task<IActionResult> TaoTaiKhoan(string Name, string MaNV, string Chucvu, string Bophan, string Username, string Password, string ConfirmPassword, string Email, string PhoneNumber, string[] SelectedRoles)
        {
            if (Password != ConfirmPassword)
            {
                ModelState.AddModelError("", "Mật khẩu và xác nhận mật khẩu không khớp");
                ViewBag.AllRoles = _roleManager.Roles.Select(r => r.Name).ToList();
                return View();
            }

            if (await _userManager.FindByNameAsync(Username) != null)
            {
                ModelState.AddModelError("", "Tên đăng nhập đã tồn tại");
                ViewBag.AllRoles = _roleManager.Roles.Select(r => r.Name).ToList();
                return View();
            }

            var existingNguoiDung = await _context.nguoidungs
                .AsNoTracking()
                .FirstOrDefaultAsync(nd => nd.MaNguoidung == MaNV);

            if (existingNguoiDung != null)
            {
                ModelState.AddModelError("", "Mã nhân viên đã tồn tại");
                ViewBag.AllRoles = _roleManager.Roles.Select(r => r.Name).ToList();
                return View();
            }

            var user = new User
            {
                UserName = Username,
                Name = Name,
                manv = MaNV,
                Chucvu = Chucvu,
                Bophan = Bophan,
                Email = Email,
                PhoneNumber = PhoneNumber
            };

            var result = await _userManager.CreateAsync(user, Password);

            if (result.Succeeded)
            {
                // Thêm roles cho user
                if (SelectedRoles != null && SelectedRoles.Length > 0)
                {
                    foreach (var roleName in SelectedRoles)
                    {
                        if (!string.IsNullOrWhiteSpace(roleName))
                        {
                            if (!await _roleManager.RoleExistsAsync(roleName))
                            {
                                await _roleManager.CreateAsync(new IdentityRole(roleName));
                            }
                            await _userManager.AddToRoleAsync(user, roleName);
                        }
                    }
                }
                else
                {
                    // Không chọn role mới => gán lại role mặc định dựa trên chức vụ/bộ phận
                    string defaultRole = null;

                    if (Chucvu == "Giám đốc")
                    {
                        defaultRole = "Giám đốc";
                    }
                    else if (Chucvu == "Admin")
                    {
                        defaultRole = "Admin";
                    }
                    else if (Chucvu == "Quản lí dự án")
                    {
                        defaultRole = "Quản lí dự án";
                    }
                    else if (!string.IsNullOrWhiteSpace(Chucvu) && !string.IsNullOrWhiteSpace(Bophan))
                    {
                        defaultRole = $"{Chucvu}-{Bophan}";
                    }

                    if (!string.IsNullOrWhiteSpace(defaultRole))
                    {
                        if (!await _roleManager.RoleExistsAsync(defaultRole))
                        {
                            await _roleManager.CreateAsync(new IdentityRole(defaultRole));
                        }
                        await _userManager.AddToRoleAsync(user, defaultRole);
                    }
                }

                // Tạo bản ghi trong bảng nguoidungs
                var nguoidung = new nguoidungs
                {
                    TenNguoidung = Name,
                    MaNguoidung = MaNV,
                    Chucvu = Chucvu,
                    Bophan = Bophan
                };

                _context.nguoidungs.Add(nguoidung);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Tạo tài khoản thành công!";
                return RedirectToAction("DanhSachUser", "User", new { area = "Admin" });
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            ViewBag.AllRoles = _roleManager.Roles.Select(r => r.Name).ToList();
            return View();
        }

        // Sửa user - GET
        [HttpGet]
        public async Task<IActionResult> SuaUser(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var roles = await _userManager.GetRolesAsync(user);
            var nguoidung = _context.nguoidungs.FirstOrDefault(nd => nd.MaNguoidung == user.manv);

            ViewBag.UserRoles = roles;
            ViewBag.AllRoles = _roleManager.Roles.Select(r => r.Name).ToList();
            ViewBag.NguoiDung = nguoidung;

            return View(user);
        }

        // Sửa user - POST
        [HttpPost]
        public async Task<IActionResult> SuaUser(string id, string Name, string MaNV, string Chucvu, string Bophan, string Email, string PhoneNumber, string[] SelectedRoles)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            user.Name = Name;
            user.manv = MaNV;
            user.Chucvu = Chucvu;
            user.Bophan = Bophan;
            user.Email = Email;
            user.PhoneNumber = PhoneNumber;

            var result = await _userManager.UpdateAsync(user);

            if (result.Succeeded)
            {
                // Cập nhật roles
                var currentRoles = await _userManager.GetRolesAsync(user);
                await _userManager.RemoveFromRolesAsync(user, currentRoles);

                if (SelectedRoles != null && SelectedRoles.Length > 0)
                {
                    foreach (var roleName in SelectedRoles)
                    {
                        if (!string.IsNullOrWhiteSpace(roleName))
                        {
                            if (!await _roleManager.RoleExistsAsync(roleName))
                            {
                                await _roleManager.CreateAsync(new IdentityRole(roleName));
                            }
                            await _userManager.AddToRoleAsync(user, roleName);
                        }
                    }
                }

                // Cập nhật bảng nguoidungs
                var nguoidung = _context.nguoidungs.FirstOrDefault(nd => nd.MaNguoidung == user.manv);
                if (nguoidung != null)
                {
                    nguoidung.TenNguoidung = Name;
                    nguoidung.Chucvu = Chucvu;
                    nguoidung.Bophan = Bophan;
                    _context.nguoidungs.Update(nguoidung);
                }
                else
                {
                    nguoidung = new nguoidungs
                    {
                        TenNguoidung = Name,
                        MaNguoidung = MaNV,
                        Chucvu = Chucvu,
                        Bophan = Bophan
                    };
                    _context.nguoidungs.Add(nguoidung);
                }
                await _context.SaveChangesAsync();

                TempData["Success"] = "Cập nhật thông tin user thành công!";
                return RedirectToAction("DanhSachUser", "User", new { area = "Admin" });
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            var roles = await _userManager.GetRolesAsync(user);
            ViewBag.UserRoles = roles;
            ViewBag.AllRoles = _roleManager.Roles.Select(r => r.Name).ToList();
            return View(user);
        }

        // Đổi mật khẩu - GET
        [HttpGet]
        public IActionResult DoiMatKhau(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            ViewBag.UserId = id;
            return View();
        }

        // Đổi mật khẩu - POST
        [HttpPost]
        public async Task<IActionResult> DoiMatKhau(string id, string NewPassword, string ConfirmPassword)
        {
            if (NewPassword != ConfirmPassword)
            {
                ModelState.AddModelError("", "Mật khẩu và xác nhận mật khẩu không khớp");
                ViewBag.UserId = id;
                return View();
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, NewPassword);

            if (result.Succeeded)
            {
                TempData["Success"] = "Đổi mật khẩu thành công!";
                return RedirectToAction("DanhSachUser", "User", new { area = "Admin" });
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            ViewBag.UserId = id;
            return View();
        }

        // Xóa user
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> XoaUser([FromBody] JsonElement data)
        {
            try
            {
                string id = null;
                if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("id", out JsonElement idElement))
                {
                    id = idElement.GetString();
                }

                if (string.IsNullOrEmpty(id))
                {
                    return Json(new { success = false, message = "Thiếu ID user" });
                }

                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy user" });
                }

                // Xóa tất cả roles của user trước
                var userRoles = await _userManager.GetRolesAsync(user);
                if (userRoles.Any())
                {
                    var removeRolesResult = await _userManager.RemoveFromRolesAsync(user, userRoles);
                    if (!removeRolesResult.Succeeded)
                    {
                        var errors = string.Join(", ", removeRolesResult.Errors.Select(e => e.Description));
                        return Json(new { success = false, message = $"Không thể xóa roles của user: {errors}" });
                    }
                }

                // Xóa trong bảng nguoidungs
                var nguoidung = _context.nguoidungs.FirstOrDefault(nd => nd.MaNguoidung == user.manv);
                if (nguoidung != null)
                {
                    _context.nguoidungs.Remove(nguoidung);
                    await _context.SaveChangesAsync();
                }

                // Xóa user
                var result = await _userManager.DeleteAsync(user);
                if (result.Succeeded)
                {
                    return Json(new { success = true, message = "Xóa user thành công" });
                }
                else
                {
                    // Hiển thị chi tiết lỗi
                    var errorMessages = string.Join(", ", result.Errors.Select(e => e.Description));
                    return Json(new { success = false, message = $"Không thể xóa user: {errorMessages}" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }
    }
}

