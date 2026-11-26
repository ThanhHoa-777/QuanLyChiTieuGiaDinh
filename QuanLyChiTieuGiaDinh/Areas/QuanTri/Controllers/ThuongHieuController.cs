using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shopson.Data;
using Shopson.Models;

namespace Shopson.Areas.QuanTri.Controllers;

/// <summary>Quản lý thương hiệu (không xóa được nếu còn sản phẩm thuộc thương hiệu đó).</summary>
public class ThuongHieuController : QuanTriController
{
    private readonly ShopSonContext _db;

    public ThuongHieuController(ShopSonContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.SoSanPham = await _db.SanPhams.GroupBy(s => s.MaThuongHieu).Select(g => new { g.Key, Dem = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Dem);
        return View(await _db.ThuongHieus.AsNoTracking().OrderBy(t => t.MaThuongHieu).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> Them(string? tenThuongHieu, string? moTa)
    {
        if (string.IsNullOrWhiteSpace(tenThuongHieu)) { BaoLoi("Tên thương hiệu không được để trống!"); return RedirectToAction(nameof(Index)); }
        _db.ThuongHieus.Add(new ThuongHieu { TenThuongHieu = tenThuongHieu.Trim(), MoTa = string.IsNullOrWhiteSpace(moTa) ? null : moTa.Trim() });
        await _db.SaveChangesAsync();
        BaoThanhCong("✅ Đã thêm thương hiệu.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Sua(int id)
    {
        var th = await _db.ThuongHieus.AsNoTracking().FirstOrDefaultAsync(t => t.MaThuongHieu == id);
        return th == null ? RedirectToAction(nameof(Index)) : View(th);
    }

    [HttpPost]
    public async Task<IActionResult> Sua(int id, string? tenThuongHieu, string? moTa)
    {
        var th = await _db.ThuongHieus.FirstOrDefaultAsync(t => t.MaThuongHieu == id);
        if (th == null) return RedirectToAction(nameof(Index));
        if (string.IsNullOrWhiteSpace(tenThuongHieu)) { BaoLoi("Tên thương hiệu không được để trống!"); return RedirectToAction(nameof(Sua), new { id }); }
        th.TenThuongHieu = tenThuongHieu.Trim();
        th.MoTa = string.IsNullOrWhiteSpace(moTa) ? null : moTa.Trim();
        await _db.SaveChangesAsync();
        BaoThanhCong("✅ Đã cập nhật thương hiệu.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Xoa(int id)
    {
        if (await _db.SanPhams.AnyAsync(s => s.MaThuongHieu == id))
        {
            BaoLoi("Không xóa được: vẫn còn sản phẩm thuộc thương hiệu này. Hãy bấm \"Ẩn\" để ngừng kinh doanh thương hiệu.");
            return RedirectToAction(nameof(Index));
        }
        var th = await _db.ThuongHieus.FirstOrDefaultAsync(t => t.MaThuongHieu == id);
        if (th != null) { _db.ThuongHieus.Remove(th); await _db.SaveChangesAsync(); BaoThanhCong("Đã xóa thương hiệu."); }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Ẩn / hiện thương hiệu. Ẩn thì MỌI sản phẩm của thương hiệu này cũng bị ẩn với khách (không xem, không mua được),
    /// nhưng dữ liệu cũ (đơn hàng, phiếu nhập) vẫn giữ nguyên. Hiện lại thì sản phẩm hiện lại (trừ sản phẩm đã tự ẩn riêng).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AnHien(int id)
    {
        var th = await _db.ThuongHieus.FirstOrDefaultAsync(t => t.MaThuongHieu == id);
        if (th == null) return RedirectToAction(nameof(Index));
        th.NgungKinhDoanh = !th.NgungKinhDoanh;
        await _db.SaveChangesAsync();
        BaoThanhCong(th.NgungKinhDoanh
            ? $"🙈 Đã ẨN thương hiệu \"{th.TenThuongHieu}\": các sản phẩm của thương hiệu này không còn hiện với khách."
            : $"👁 Đã HIỆN lại thương hiệu \"{th.TenThuongHieu}\".");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Thêm nhanh thương hiệu từ form sản phẩm (gọi bằng JavaScript, trả về JSON) - không phải rời trang.</summary>
    [HttpPost]
    public async Task<IActionResult> ThemNhanh(string? tenThuongHieu, string? moTa)
    {
        string ten = (tenThuongHieu ?? "").Trim();
        if (ten == "") return Json(new { thanhCong = false, thongBao = "Tên thương hiệu không được để trống!" });
        if (await _db.ThuongHieus.AnyAsync(t => t.TenThuongHieu == ten))
            return Json(new { thanhCong = false, thongBao = "Thương hiệu này đã có trong danh sách!" });
        var th = new ThuongHieu { TenThuongHieu = ten, MoTa = (moTa ?? "").Trim() };
        _db.ThuongHieus.Add(th);
        await _db.SaveChangesAsync();
        return Json(new { thanhCong = true, ma = th.MaThuongHieu, ten = th.TenThuongHieu });
    }
}
