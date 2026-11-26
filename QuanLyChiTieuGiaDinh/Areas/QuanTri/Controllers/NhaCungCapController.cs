using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shopson.Data;
using Shopson.Models;

namespace Shopson.Areas.QuanTri.Controllers;

/// <summary>Danh sách nhà cung cấp: thêm, sửa, xóa (không xóa được nếu đã có phiếu nhập).</summary>
public class NhaCungCapController : QuanTriController
{
    private readonly ShopSonContext _db;

    public NhaCungCapController(ShopSonContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index() => View(await _db.NhaCungCaps.AsNoTracking().OrderBy(n => n.MaNcc).ToListAsync());

    [HttpPost]
    public async Task<IActionResult> Them(string? tenNcc, string? dienThoai, string? diaChi, string? ghiChu)
    {
        if (string.IsNullOrWhiteSpace(tenNcc)) { BaoLoi("Tên nhà cung cấp không được để trống!"); return RedirectToAction(nameof(Index)); }
        _db.NhaCungCaps.Add(new NhaCungCap { TenNcc = tenNcc.Trim(), DienThoai = (dienThoai ?? "").Trim(), DiaChi = (diaChi ?? "").Trim(), GhiChu = ghiChu });
        await _db.SaveChangesAsync();
        BaoThanhCong("✅ Đã thêm nhà cung cấp.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Sua(int id)
    {
        var ncc = await _db.NhaCungCaps.FirstOrDefaultAsync(n => n.MaNcc == id);
        return ncc == null ? RedirectToAction(nameof(Index)) : View(ncc);
    }

    [HttpPost]
    public async Task<IActionResult> Sua(int id, string? tenNcc, string? dienThoai, string? diaChi, string? ghiChu)
    {
        var ncc = await _db.NhaCungCaps.FirstOrDefaultAsync(n => n.MaNcc == id);
        if (ncc == null) return RedirectToAction(nameof(Index));
        if (string.IsNullOrWhiteSpace(tenNcc)) { BaoLoi("Tên nhà cung cấp không được để trống!"); return RedirectToAction(nameof(Sua), new { id }); }
        ncc.TenNcc = tenNcc.Trim(); ncc.DienThoai = (dienThoai ?? "").Trim(); ncc.DiaChi = (diaChi ?? "").Trim(); ncc.GhiChu = ghiChu;
        await _db.SaveChangesAsync();
        BaoThanhCong("✅ Đã cập nhật nhà cung cấp.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Xoa(int id)
    {
        if (await _db.NhapKhos.AnyAsync(n => n.MaNcc == id) || await _db.CongNos.AnyAsync(c => c.MaNcc == id))
        {
            BaoLoi("Không xóa được: nhà cung cấp này đã có phiếu nhập / công nợ.");
            return RedirectToAction(nameof(Index));
        }
        var ncc = await _db.NhaCungCaps.FirstOrDefaultAsync(n => n.MaNcc == id);
        if (ncc != null) { _db.NhaCungCaps.Remove(ncc); await _db.SaveChangesAsync(); BaoThanhCong("Đã xóa nhà cung cấp."); }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Thêm nhanh nhà cung cấp từ form tạo phiếu nhập (gọi bằng JavaScript, trả về JSON) - không phải rời trang.</summary>
    [HttpPost]
    public async Task<IActionResult> ThemNhanh(string? tenNcc, string? dienThoai, string? diaChi)
    {
        string ten = (tenNcc ?? "").Trim();
        if (ten == "") return Json(new { thanhCong = false, thongBao = "Tên nhà cung cấp không được để trống!" });
        if (await _db.NhaCungCaps.AnyAsync(n => n.TenNcc == ten))
            return Json(new { thanhCong = false, thongBao = "Nhà cung cấp này đã có trong danh sách!" });
        var ncc = new NhaCungCap { TenNcc = ten, DienThoai = (dienThoai ?? "").Trim(), DiaChi = (diaChi ?? "").Trim() };
        _db.NhaCungCaps.Add(ncc);
        await _db.SaveChangesAsync();
        return Json(new { thanhCong = true, ma = ncc.MaNcc, ten = ncc.TenNcc });
    }
}
