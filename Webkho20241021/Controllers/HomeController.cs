using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Webkho_20241021.Models;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Authentication;

namespace Webkho_20241021.Controllers
{
    public class HomeController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly RoleManager<IdentityRole> _roleManager;

        public HomeController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            ApplicationDbContext context,
            RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _roleManager = roleManager;
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
                else
                {
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
                // Gán thông tin vào ViewData để hiển thị trên giao diện nếu cần
                ViewData["UserRoles"] = string.Join(", ", roles);
            }

            await _signInManager.SignInAsync(user, isPersistent: false);

            var nguoiDung = _context.nguoidungs.FirstOrDefault(nd => nd.MaNguoidung == user.manv);
            if (nguoiDung != null)
            {
                HttpContext.Session.SetString("TenNguoidung", nguoiDung.TenNguoidung);
                HttpContext.Session.SetString("MaNguoidung", nguoiDung.MaNguoidung);
                HttpContext.Session.SetString("Bophan", nguoiDung.Bophan);
                HttpContext.Session.SetString("Chucvu", nguoiDung.Chucvu);
                HttpContext.Session.SetString("Email", user.Email);
                HttpContext.Session.SetString("Phone", user.PhoneNumber);
            }

            if (user.Chucvu == "Giám đốc")
            {
                return RedirectToAction("Trangchu", "Home", new { area = "Giamdoc" });
            }
            else if (user.Chucvu == "Nhân viên" && user.Bophan == "BP kỹ thuật")
            {
                return RedirectToAction("Trangchu", "Home", new { area = "NhanvienKythuat" });
            }
            else if (user.Chucvu == "Trưởng BP" && user.Bophan == "BP kỹ thuật")
            {
                return RedirectToAction("Trangchu", "Home", new { area = "TruongBPKythuat" });
            }
            else if (user.Chucvu == "Trưởng BP" && user.Bophan == "BP kho")
            {
                return RedirectToAction("Trangchu", "Home", new { area = "TruongBPKho" });
            }
            else if (user.Chucvu == "Trưởng BP" && user.Bophan == "BP kế toán")
            {
                return RedirectToAction("Trangchu", "Home", new { area = "TruongBPKetoan" });
            }
            else if (user.Chucvu == "Trưởng BP" && user.Bophan == "BP mua hàng")
            {
                return RedirectToAction("Trangchu", "Home", new { area = "TruongBPMuahang" });
            }

            return RedirectToAction("Trangchu", "Home");
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();
            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            Response.Cookies.Delete(".AspNetCore.Identity.Application");
            return RedirectToAction("Dangnhap", "Home");
        }

        public IActionResult Trangchu()
        {
            return View("Trangchu");
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
    }
}
