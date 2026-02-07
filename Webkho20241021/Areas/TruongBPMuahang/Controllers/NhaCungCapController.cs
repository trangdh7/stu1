using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Webkho_20241021.Models;

namespace Webkho_20241021.Areas.TruongBPMuahang.Controllers
{
    [Area("TruongBPMuahang")]
    [Authorize(Roles = "Trưởng BP-BP mua hàng")]
    public class NhaCungCapController : Controller
    {
        private readonly ApplicationDbContext _context;

        public NhaCungCapController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var list = _context.NhaCungCap.OrderBy(n => n.TenNhaCC).ToList();
            return View(list);
        }

        public IActionResult Create()
        {
            return View(new NhaCungCap());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(NhaCungCap model)
        {
            if (string.IsNullOrWhiteSpace(model.TenNhaCC))
            {
                ModelState.AddModelError("TenNhaCC", "Tên nhà cung cấp không được để trống");
            }

            if (ModelState.IsValid)
            {
                var existing = _context.NhaCungCap.FirstOrDefault(n => 
                    n.TenNhaCC.Trim().ToLower() == model.TenNhaCC.Trim().ToLower());
                if (existing != null)
                {
                    ModelState.AddModelError("TenNhaCC", "Nhà cung cấp này đã tồn tại");
                    return View(model);
                }

                model.NgayTao = DateTime.Now;
                _context.NhaCungCap.Add(model);
                _context.SaveChanges();
                TempData["Message"] = "Thêm nhà cung cấp thành công!";
                return RedirectToAction("Index");
            }
            return View(model);
        }

        public IActionResult Edit(int id)
        {
            var item = _context.NhaCungCap.Find(id);
            if (item == null) return NotFound();
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(int id, NhaCungCap model)
        {
            if (id != model.ID) return NotFound();

            if (string.IsNullOrWhiteSpace(model.TenNhaCC))
            {
                ModelState.AddModelError("TenNhaCC", "Tên nhà cung cấp không được để trống");
            }

            if (ModelState.IsValid)
            {
                var existing = _context.NhaCungCap.FirstOrDefault(n => 
                    n.ID != id && n.TenNhaCC.Trim().ToLower() == model.TenNhaCC.Trim().ToLower());
                if (existing != null)
                {
                    ModelState.AddModelError("TenNhaCC", "Nhà cung cấp này đã tồn tại");
                    return View(model);
                }

                var item = _context.NhaCungCap.Find(id);
                if (item == null) return NotFound();

                item.TenNhaCC = model.TenNhaCC.Trim();
                item.GhiChu = model.GhiChu?.Trim();
                item.NgayCapNhat = DateTime.Now;

                _context.SaveChanges();
                TempData["Message"] = "Cập nhật nhà cung cấp thành công!";
                return RedirectToAction("Index");
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            var item = _context.NhaCungCap.Find(id);
            if (item == null) return NotFound();

            _context.NhaCungCap.Remove(item);
            _context.SaveChanges();
            TempData["Message"] = "Xóa nhà cung cấp thành công!";
            return RedirectToAction("Index");
        }

        /// <summary>
        /// Gợi ý nhà cung cấp: chỉ lấy từ bảng NhaCungCap. Gõ chữ → hiện danh sách gợi ý.
        /// </summary>
        [HttpGet]
        public IActionResult GetNhaCCGoiY(string q)
        {
            var query = _context.NhaCungCap.AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var keyword = q.Trim().ToLower();
                query = query.Where(n => n.TenNhaCC.ToLower().Contains(keyword));
            }

            var list = query
                .OrderBy(n => n.TenNhaCC)
                .Take(50)
                .Select(n => n.TenNhaCC)
                .Distinct()
                .ToList();

            return Json(list);
        }
    }
}
