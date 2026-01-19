using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Webkho_20241021.Models;
using Webkho_20241021.Services;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

namespace Webkho_20241021.Controllers
{
    public class HomeController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly EmailService _emailService;
        private readonly IConfiguration _configuration;

        public HomeController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            ApplicationDbContext context,
            RoleManager<IdentityRole> roleManager,
            EmailService emailService,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _roleManager = roleManager;
            _emailService = emailService;
            _configuration = configuration;
        }

        public IActionResult Dangnhap()
        {
            return View("Dangnhap");
        }

        public IActionResult Create()
        {
            return View("Create");
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser(string Name, string MaNV, string Chucvu, string Bophan, string Username, string Password, string ConfirmPassword, string Email, string PhoneNumber)
        {
            if (Password != ConfirmPassword)
            {
                ModelState.AddModelError("", "Mật khẩu và xác nhận mật khẩu không khớp");
                return View("Create");
            }

            if (await _userManager.FindByNameAsync(Username) != null)
            {
                ModelState.AddModelError("", "Tên đăng nhập đã tồn tại");
                return View("Create");
            }

            var existingNguoiDung = await _context.nguoidungs
                .AsNoTracking()
                .FirstOrDefaultAsync(nd => nd.MaNguoidung == MaNV);

            if (existingNguoiDung != null)
            {
                ModelState.AddModelError("", "Mã nhân viên đã tồn tại");
                return View("Create");
            }

            var user = new User
            {
                UserName = Username,
                Email = Email,
                PhoneNumber = PhoneNumber,
                Name = Name,
                manv = MaNV,
                Chucvu = Chucvu,
                Bophan = Bophan
            };

            var result = await _userManager.CreateAsync(user, Password);

            if (result.Succeeded)
            {
                // Xử lý các trường hợp đặc biệt: Giám đốc và Admin
                if (Chucvu == "Giám đốc")
                {
                    if (!await _roleManager.RoleExistsAsync(Chucvu))
                    {
                        // Tạo vai trò mới
                        var roleResult = await _roleManager.CreateAsync(new IdentityRole(Chucvu));

                        // Kiểm tra xem việc tạo vai trò có thành công không
                        if (!roleResult.Succeeded)
                        {
                            // Hiển thị lỗi nếu việc tạo vai trò thất bại
                            foreach (var error in roleResult.Errors)
                            {
                                ModelState.AddModelError("", error.Description);
                            }
                            return View("Create");
                        }
                    }
                    await _userManager.AddToRoleAsync(user, Chucvu);
                }
                else if (Chucvu == "Admin")
                {
                    // Admin role không có bộ phận, chỉ là "Admin"
                    string adminRole = "Admin";
                    if (!await _roleManager.RoleExistsAsync(adminRole))
                    {
                        var roleResult = await _roleManager.CreateAsync(new IdentityRole(adminRole));

                        if (!roleResult.Succeeded)
                        {
                            foreach (var error in roleResult.Errors)
                            {
                                ModelState.AddModelError("", error.Description);
                            }
                            return View("Create");
                        }
                    }
                    await _userManager.AddToRoleAsync(user, adminRole);
                }
                else if (Chucvu == "Quản lí dự án")
                {
                    // Quản lí dự án role
                    string qldaRole = "Quản lí dự án";
                    if (!await _roleManager.RoleExistsAsync(qldaRole))
                    {
                        var roleResult = await _roleManager.CreateAsync(new IdentityRole(qldaRole));

                        if (!roleResult.Succeeded)
                        {
                            foreach (var error in roleResult.Errors)
                            {
                                ModelState.AddModelError("", error.Description);
                            }
                            return View("Create");
                        }
                    }
                    await _userManager.AddToRoleAsync(user, qldaRole);
                }
                else
                {
                    // Các trường hợp khác: tạo role theo format "Chucvu-Bophan"
                    string combinedRole = $"{Chucvu}-{Bophan}";

                    if (!await _roleManager.RoleExistsAsync(combinedRole))
                    {
                        var roleResult = await _roleManager.CreateAsync(new IdentityRole(combinedRole));

                        if (!roleResult.Succeeded)
                        {
                            foreach (var error in roleResult.Errors)
                            {
                                ModelState.AddModelError("", error.Description);
                            }
                            return View("Create");
                        }
                    }
                    await _userManager.AddToRoleAsync(user, combinedRole);
                }                              
                              
                var nguoidung = new nguoidungs
                {
                    TenNguoidung = Name,
                    MaNguoidung = MaNV,
                    Chucvu = Chucvu,
                    Bophan = Bophan
                };

                _context.nguoidungs.Add(nguoidung);
                await _context.SaveChangesAsync();

                return RedirectToAction("Dangnhap", "Home");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return View("Create");
        }

        [HttpPost]
        public async Task<IActionResult> Login(string Username, string Password)
        {
            var user = await _userManager.FindByNameAsync(Username);

            if (user == null || !(await _userManager.CheckPasswordAsync(user, Password)))
            {
                ViewData["ErrorMessage"] = "Tên đăng nhập hoặc mật khẩu không đúng";
                return View("Dangnhap");
            }

            // Lấy danh sách vai trò của người dùng
            var roles = await _userManager.GetRolesAsync(user);
            foreach (var role in roles)
            {
                Console.WriteLine($"User {Username} has role: {role}");
            }

            await _signInManager.SignInAsync(user, isPersistent: false);

            var nguoiDung = _context.nguoidungs.FirstOrDefault(nd => nd.MaNguoidung == user.manv);
            if (nguoiDung != null)
            {
                HttpContext.Session.SetString("TenNguoidung", nguoiDung.TenNguoidung);
                HttpContext.Session.SetString("MaNguoidung", nguoiDung.MaNguoidung);
                HttpContext.Session.SetString("Bophan", nguoiDung.Bophan);
                HttpContext.Session.SetString("Chucvu", nguoiDung.Chucvu);
                HttpContext.Session.SetString("Email", user.Email ?? "");
                HttpContext.Session.SetString("Phone", user.PhoneNumber ?? "");
            }

            // Lưu thông tin user vào session để dùng cho ChonRole
            HttpContext.Session.SetString("UserId", user.Id);
            HttpContext.Session.SetString("Username", user.UserName ?? "");

            // Nếu user có nhiều roles, hiển thị màn hình chọn role
            if (roles.Count > 1)
            {
                ViewBag.Roles = roles;
                ViewBag.Username = user.UserName;
                return View("ChonRole");
            }
            // Nếu chỉ có 1 role, tự động chọn role đó
            else if (roles.Count == 1)
            {
                return await SetRole(roles[0]);
            }

            // Nếu không có role, redirect về trang chủ
            return RedirectToAction("Trangchu", "Home");
        }

        [HttpPost]
        public async Task<IActionResult> SetRole(string selectedRole)
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Dangnhap", "Home");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return RedirectToAction("Dangnhap", "Home");
            }

            // Kiểm tra role có hợp lệ không
            var userRoles = await _userManager.GetRolesAsync(user);
            if (!userRoles.Contains(selectedRole))
            {
                ViewData["ErrorMessage"] = "Role không hợp lệ";
                ViewBag.Roles = userRoles;
                ViewBag.Username = user.UserName;
                return View("ChonRole");
            }

            // Lưu role được chọn vào session
            HttpContext.Session.SetString("SelectedRole", selectedRole);

            // Parse role để lấy thông tin Chucvu và Bophan
            string chucvu = "";
            string bophan = "";

            if (selectedRole == "Giám đốc")
            {
                chucvu = "Giám đốc";
            }
            else if (selectedRole == "Admin")
            {
                chucvu = "Admin";
            }
            else
            {
                // Format: "Chucvu-Bophan" (ví dụ: "Trưởng BP-BP kho")
                var parts = selectedRole.Split('-');
                if (parts.Length >= 2)
                {
                    chucvu = parts[0];
                    bophan = string.Join("-", parts.Skip(1)); // Xử lý trường hợp có nhiều dấu -
                }
            }

            // Cập nhật session với thông tin role được chọn
            if (!string.IsNullOrEmpty(chucvu))
            {
                HttpContext.Session.SetString("Chucvu", chucvu);
            }
            if (!string.IsNullOrEmpty(bophan))
            {
                HttpContext.Session.SetString("Bophan", bophan);
            }

            // Log để debug
            Console.WriteLine($"=== SetRole START ===");
            Console.WriteLine($"SetRole: User {user.UserName}, selectedRole: '{selectedRole}'");
            var currentRoles = await _userManager.GetRolesAsync(user);
            Console.WriteLine($"SetRole: User {user.UserName} has roles: [{string.Join(", ", currentRoles)}]");
            Console.WriteLine($"SetRole: Checking if user is in role '{selectedRole}': {await _userManager.IsInRoleAsync(user, selectedRole)}");
            
            // Kiểm tra role trong database có đúng format không
            var roleInDb = await _roleManager.FindByNameAsync(selectedRole);
            if (roleInDb != null)
            {
                Console.WriteLine($"SetRole: Role '{selectedRole}' found in database. NormalizedName: '{roleInDb.NormalizedName}'");
            }
            else
            {
                Console.WriteLine($"SetRole: WARNING - Role '{selectedRole}' NOT found in database!");
            }

            // Lưu lại session data trước khi sign out (để tránh mất dữ liệu)
            var tenNguoidung = HttpContext.Session.GetString("TenNguoidung");
            var maNguoidung = HttpContext.Session.GetString("MaNguoidung");
            var bophanSession = HttpContext.Session.GetString("Bophan");
            var chucvuSession = HttpContext.Session.GetString("Chucvu");
            var email = HttpContext.Session.GetString("Email");
            var phone = HttpContext.Session.GetString("Phone");
            var username = HttpContext.Session.GetString("Username");

            // Sign out và sign in lại để đảm bảo roles được load vào claims
            // Điều này quan trọng để [Authorize(Roles = "...")] hoạt động đúng
            Console.WriteLine($"SetRole: Signing out user...");
            await _signInManager.SignOutAsync();
            
            Console.WriteLine($"SetRole: Signing in user again...");
            await _signInManager.SignInAsync(user, isPersistent: false);
            
            // Kiểm tra claims sau khi sign in
            var claimsPrincipal = await _signInManager.CreateUserPrincipalAsync(user);
            var roleClaims = claimsPrincipal.Claims.Where(c => c.Type == System.Security.Claims.ClaimTypes.Role).ToList();
            Console.WriteLine($"SetRole: User claims after sign in - Roles: [{string.Join(", ", roleClaims.Select(c => c.Value))}]");
            
            // Khôi phục lại session data
            if (!string.IsNullOrEmpty(tenNguoidung)) HttpContext.Session.SetString("TenNguoidung", tenNguoidung);
            if (!string.IsNullOrEmpty(maNguoidung)) HttpContext.Session.SetString("MaNguoidung", maNguoidung);
            if (!string.IsNullOrEmpty(bophanSession)) HttpContext.Session.SetString("Bophan", bophanSession);
            if (!string.IsNullOrEmpty(chucvuSession)) HttpContext.Session.SetString("Chucvu", chucvuSession);
            if (!string.IsNullOrEmpty(email)) HttpContext.Session.SetString("Email", email);
            if (!string.IsNullOrEmpty(phone)) HttpContext.Session.SetString("Phone", phone);
            if (!string.IsNullOrEmpty(username)) HttpContext.Session.SetString("Username", username);
            HttpContext.Session.SetString("UserId", user.Id);
            HttpContext.Session.SetString("SelectedRole", selectedRole);
            
            Console.WriteLine($"SetRole: User {user.UserName} signed in again, session restored, redirecting to area...");
            Console.WriteLine($"=== SetRole END ===");

            // Redirect dựa trên role được chọn
            return RedirectToArea(selectedRole);
        }

        [HttpGet]
        public async Task<IActionResult> DoiRole()
        {
            var userId = HttpContext.Session.GetString("UserId");
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Dangnhap", "Home");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return RedirectToAction("Dangnhap", "Home");
            }

            var roles = await _userManager.GetRolesAsync(user);
            if (roles.Count <= 1)
            {
                // Nếu chỉ có 1 role, không cần đổi
                var currentRole = HttpContext.Session.GetString("SelectedRole");
                if (!string.IsNullOrEmpty(currentRole))
                {
                    return RedirectToArea(currentRole);
                }
            }

            ViewBag.Roles = roles;
            ViewBag.Username = user.UserName;
            ViewBag.CurrentRole = HttpContext.Session.GetString("SelectedRole");
            return View("ChonRole");
        }

        private IActionResult RedirectToArea(string role)
        {
            // Log role để debug
            Console.WriteLine($"RedirectToArea called with role: '{role}'");

            // Normalize role: trim và loại bỏ khoảng trắng thừa
            string normalizedRole = role?.Trim() ?? "";

            // So sánh role (case-insensitive và trim)
            if (string.Equals(normalizedRole, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Redirecting to Admin area");
                return RedirectToAction("Trangchu", "Home", new { area = "Admin" });
            }
            else if (string.Equals(normalizedRole, "Giám đốc", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Redirecting to Giamdoc area");
                return RedirectToAction("Trangchu", "Home", new { area = "Giamdoc" });
            }
            else if (normalizedRole.Contains("Nhân viên") && normalizedRole.Contains("BP kỹ thuật"))
            {
                Console.WriteLine($"Redirecting to NhanvienKythuat area");
                return RedirectToAction("Trangchu", "Home", new { area = "NhanvienKythuat" });
            }
            else if (normalizedRole.Contains("Trưởng BP") && normalizedRole.Contains("BP kỹ thuật"))
            {
                Console.WriteLine($"Redirecting to TruongBPKythuat area");
                return RedirectToAction("Trangchu", "Home", new { area = "TruongBPKythuat" });
            }
            else if (normalizedRole.Contains("Trưởng BP") && normalizedRole.Contains("BP kho"))
            {
                Console.WriteLine($"Redirecting to TruongBPKho area");
                return RedirectToAction("Trangchu", "Home", new { area = "TruongBPKho" });
            }
            else if (normalizedRole.Contains("Trưởng BP") && normalizedRole.Contains("BP kế toán"))
            {
                Console.WriteLine($"Redirecting to TruongBPKetoan area");
                return RedirectToAction("Trangchu", "Home", new { area = "TruongBPKetoan" });
            }
            else if (normalizedRole.Contains("Trưởng BP") && normalizedRole.Contains("BP mua hàng"))
            {
                Console.WriteLine($"Redirecting to TruongBPMuahang area");
                return RedirectToAction("Trangchu", "Home", new { area = "TruongBPMuahang" });
            }
            else if (normalizedRole == "Nhân viên-BP kho" || 
                     normalizedRole == "Nhân viên kho" ||
                     (normalizedRole.Contains("Nhân viên") && normalizedRole.Contains("BP kho")) ||
                     (normalizedRole.Contains("Nhân viên") && normalizedRole.Contains("kho") && !normalizedRole.Contains("kỹ thuật") && !normalizedRole.Contains("kế toán") && !normalizedRole.Contains("mua hàng")))
            {
                Console.WriteLine($"=== RedirectToArea: Nhân viên kho ===");
                Console.WriteLine($"RedirectToArea: normalizedRole = '{normalizedRole}', original role = '{role}'");
                Console.WriteLine($"RedirectToArea: About to redirect to /NhanvienKho/Home/Trangchu");
                return RedirectToAction("Trangchu", "Home", new { area = "NhanvienKho" });
            }
            else if (normalizedRole.Contains("Nhân viên") && normalizedRole.Contains("BP kế toán"))
            {
                Console.WriteLine($"Redirecting to NhanvienKetoan area");
                return RedirectToAction("Trangchu", "Home", new { area = "NhanvienKetoan" });
            }
            else if (normalizedRole.Contains("Nhân viên") && normalizedRole.Contains("BP mua hàng"))
            {
                Console.WriteLine($"Redirecting to NhanvienMuahang area");
                return RedirectToAction("Trangchu", "Home", new { area = "NhanvienMuahang" });
            }
            else if (normalizedRole == "Quản lí dự án" || normalizedRole.Contains("Quản lí dự án"))
            {
                Console.WriteLine($"Redirecting to QuanLiDuAn area");
                return RedirectToAction("Trangchu", "Home", new { area = "QuanLiDuAn" });
            }

            // Nếu role không khớp, log và redirect về trang đăng nhập với thông báo lỗi
            Console.WriteLine($"Warning: Unknown role '{role}' (normalized: '{normalizedRole}') - redirecting to login");
            TempData["ErrorMessage"] = $"Không tìm thấy khu vực cho role: {role}. Vui lòng liên hệ quản trị viên.";
            return RedirectToAction("Dangnhap", "Home");
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            Response.Cookies.Delete(".AspNetCore.Identity.Application");
            return RedirectToAction("Dangnhap", "Home");
        }

        // Action để admin thêm role cho user (có thể gọi từ database hoặc admin panel)
        [HttpPost]
        [Authorize(Roles = "Giám đốc,Admin")]
        public async Task<IActionResult> AddRoleToUser(string userId, string roleName)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(roleName))
            {
                return Json(new { success = false, message = "Thiếu thông tin userId hoặc roleName" });
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return Json(new { success = false, message = "Không tìm thấy user" });
            }

            // Kiểm tra role có tồn tại không
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                var roleResult = await _roleManager.CreateAsync(new IdentityRole(roleName));
                if (!roleResult.Succeeded)
                {
                    return Json(new { success = false, message = "Không thể tạo role" });
                }
            }

            // Kiểm tra user đã có role này chưa
            if (await _userManager.IsInRoleAsync(user, roleName))
            {
                return Json(new { success = false, message = "User đã có role này" });
            }

            var result = await _userManager.AddToRoleAsync(user, roleName);
            if (result.Succeeded)
            {
                return Json(new { success = true, message = "Đã thêm role thành công" });
            }

            return Json(new { success = false, message = "Không thể thêm role" });
        }

        // Action để admin xóa role khỏi user
        [HttpPost]
        [Authorize(Roles = "Giám đốc,Admin")]
        public async Task<IActionResult> RemoveRoleFromUser(string userId, string roleName)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(roleName))
            {
                return Json(new { success = false, message = "Thiếu thông tin userId hoặc roleName" });
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return Json(new { success = false, message = "Không tìm thấy user" });
            }

            if (!await _userManager.IsInRoleAsync(user, roleName))
            {
                return Json(new { success = false, message = "User không có role này" });
            }

            var result = await _userManager.RemoveFromRoleAsync(user, roleName);
            if (result.Succeeded)
            {
                return Json(new { success = true, message = "Đã xóa role thành công" });
            }

            return Json(new { success = false, message = "Không thể xóa role" });
        }

        public IActionResult Trangchu()
        {
            // Kiểm tra xem đang ở area nào (nếu có)
            var currentArea = HttpContext.Request.RouteValues["area"]?.ToString();
            
            // Nếu đã ở trong một area rồi, không redirect nữa (tránh vòng lặp)
            // Action này chỉ dành cho route không có area, nếu đã ở area thì không nên gọi action này
            if (!string.IsNullOrEmpty(currentArea))
            {
                // Đã ở trong area, không làm gì cả (để area controller xử lý)
                // Nhưng vì action này được gọi từ main controller, nên redirect về trang đăng nhập
                return RedirectToAction("Dangnhap", "Home");
            }
            
            // Nếu không ở area (đang ở /Home/Trangchu), kiểm tra xem user đã chọn role chưa
            var selectedRole = HttpContext.Session.GetString("SelectedRole");
            if (!string.IsNullOrEmpty(selectedRole))
            {
                // Redirect đến area tương ứng với role đã chọn (chỉ redirect một lần)
                Console.WriteLine($"Redirecting from /Home/Trangchu to area for role: {selectedRole}");
                return RedirectToArea(selectedRole);
            }
            
            // Nếu chưa có role, kiểm tra xem user có đăng nhập không
            var userId = HttpContext.Session.GetString("UserId");
            if (!string.IsNullOrEmpty(userId))
            {
                // User đã đăng nhập nhưng chưa chọn role, redirect về trang đăng nhập
                return RedirectToAction("Dangnhap", "Home");
            }
            
            // Nếu chưa đăng nhập, redirect về trang đăng nhập
            return RedirectToAction("Dangnhap", "Home");
        }

        [AllowAnonymous]
        public async Task<IActionResult> DebugInfo()
        {
            var rolesInClaimsList = new List<string>();
            var allClaimsList = new List<object>();
            var rolesInDatabaseList = new List<string>();
            
            if (User?.Claims != null)
            {
                rolesInClaimsList = User.Claims
                    .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
                    .Select(c => c.Value)
                    .ToList();
                    
                allClaimsList = User.Claims
                    .Select(c => new { Type = c.Type, Value = c.Value })
                    .Cast<object>()
                    .ToList();
            }

            if (User?.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(HttpContext.Session.GetString("UserId")))
            {
                var userId = HttpContext.Session.GetString("UserId");
                var user = await _userManager.FindByIdAsync(userId);
                if (user != null)
                {
                    var roles = await _userManager.GetRolesAsync(user);
                    rolesInDatabaseList = roles.ToList();
                    
                    if (User?.Claims != null)
                    {
                        rolesInClaimsList = User.Claims
                            .Where(c => c.Type == System.Security.Claims.ClaimTypes.Role)
                            .Select(c => c.Value)
                            .ToList();
                            
                        allClaimsList = User.Claims
                            .Select(c => new { Type = c.Type, Value = c.Value })
                            .Cast<object>()
                            .ToList();
                    }
                }
            }

            var debugInfo = new
            {
                IsAuthenticated = User?.Identity?.IsAuthenticated ?? false,
                UserName = User?.Identity?.Name ?? HttpContext.Session.GetString("Username") ?? "N/A",
                UserId = HttpContext.Session.GetString("UserId") ?? "N/A",
                SelectedRole = HttpContext.Session.GetString("SelectedRole") ?? "N/A",
                Chucvu = HttpContext.Session.GetString("Chucvu") ?? "N/A",
                Bophan = HttpContext.Session.GetString("Bophan") ?? "N/A",
                TenNguoidung = HttpContext.Session.GetString("TenNguoidung") ?? "N/A",
                RolesInClaims = rolesInClaimsList,
                RolesInDatabase = rolesInDatabaseList,
                AllClaims = allClaimsList
            };

            return Json(debugInfo, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }

        public IActionResult DanhSachDoNhanVien()
        {
            // Lấy danh sách vật phẩm nhân viên mới được cấp
            // Dựa trên bảng vtphieuxuatkho - các vật tư đã xuất kho (phát cho nhân viên)
            var danhSachDoNhanVien = _context.vtphieuxuatkho
                .Where(vt => vt.TrangThai == "Đã phát" || vt.TrangThai == "Hoàn thành")
                .OrderByDescending(vt => vt.NgayNhapkho)
                .ToList();

            return View("DanhSachDoNhanVien", danhSachDoNhanVien);
        }

        public IActionResult VatTuMoi()
        {
            var items = _context.vtphieuxuatkho
                .Where(v => v.LoaiCapPhat == "ChoNhanVienMoi")
                .OrderByDescending(v => v.NgayNhapkho)
                .ToList();
            return View("VatTuMoi", items);
        }

        [AllowAnonymous]
        public IActionResult NotFound()
        {
            Response.StatusCode = 404;
            return View("NotFound");
        }

        [AllowAnonymous]
        public IActionResult TestEmail()
        {
            // Hiển thị thông tin cấu hình email
            var emailConfig = new
            {
                FromEmail = _configuration["EmailSettings:FromEmail"],
                SmtpServer = _configuration["EmailSettings:SmtpServer"],
                SmtpPort = _configuration["EmailSettings:SmtpPort"],
                FromPassword = string.IsNullOrEmpty(_configuration["EmailSettings:FromPassword"]) 
                    ? "(chưa cấu hình)" 
                    : $"Đã cấu hình (độ dài: {_configuration["EmailSettings:FromPassword"].Length})",
                StuEmailSettings = new
                {
                    SmtpServer = _configuration["EmailSettings:StuEmailSettings:SmtpServer"],
                    SmtpPort = _configuration["EmailSettings:StuEmailSettings:SmtpPort"],
                    FromPassword = string.IsNullOrEmpty(_configuration["EmailSettings:StuEmailSettings:FromPassword"])
                        ? "(sẽ lấy từ EmailSettings)"
                        : $"Đã cấu hình riêng (độ dài: {_configuration["EmailSettings:StuEmailSettings:FromPassword"].Length})"
                }
            };

            ViewBag.EmailConfig = emailConfig;
            return View("TestEmail");
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> SendTestEmail(string testEmail)
        {
            if (string.IsNullOrEmpty(testEmail))
            {
                return Json(new { success = false, message = "Vui lòng nhập email để test" });
            }

            try
            {
                var subject = "Test Email - Hệ thống Quản lý Kho";
                var body = $@"
                    <html>
                    <body style='font-family: Arial, sans-serif; padding: 20px;'>
                        <h2 style='color: #2c3e50;'>Email Test Thành Công!</h2>
                        <p>Đây là email test từ hệ thống quản lý kho.</p>
                        <p><strong>Thời gian:</strong> {DateTime.Now:dd/MM/yyyy HH:mm:ss}</p>
                        <p><strong>Server:</strong> {Environment.MachineName}</p>
                        <hr style='border: none; border-top: 1px solid #e0e0e0; margin: 20px 0;' />
                        <p style='color: #7f8c8d; font-size: 12px;'>Nếu bạn nhận được email này, nghĩa là cấu hình email trên server đã hoạt động đúng.</p>
                    </body>
                    </html>";

                var result = await _emailService.SendEmailAsync(testEmail, subject, body);

                if (result)
                {
                    return Json(new { success = true, message = $"Email đã được gửi thành công đến {testEmail}. Vui lòng kiểm tra hộp thư." });
                }
                else
                {
                    return Json(new { success = false, message = "Không thể gửi email. Vui lòng kiểm tra logs và cấu hình." });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi khi gửi email: {ex.Message}" });
            }
        }
    }
}
