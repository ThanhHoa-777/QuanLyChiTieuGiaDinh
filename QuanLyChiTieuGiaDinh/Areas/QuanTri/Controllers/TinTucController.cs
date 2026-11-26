using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shopson.Data;
using Shopson.DichVu;
using Shopson.Models;

namespace Shopson.Areas.QuanTri.Controllers;

/// <summary>Quản lý tin tức làm đẹp: danh sách, thêm, sửa, xóa.</summary>
public class TinTucController : QuanTriController
{
    private readonly ShopSonContext _db;

    public TinTucController(ShopSonContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index() => View(await _db.TinTucs.AsNoTracking().OrderByDescending(t => t.MaTinTuc).ToListAsync());

    [HttpGet]
    public IActionResult Them() => View("Form", new TinTuc { NgayDang = DateOnly.FromDateTime(DateTime.Now) });

    [HttpPost]
    public async Task<IActionResult> Them(string? tieuDe, string? noiDung, DateTime? ngayDang)
    {
        if (string.IsNullOrWhiteSpace(tieuDe)) { BaoLoi("Tiêu đề không được để trống!"); return RedirectToAction(nameof(Them)); }
        _db.TinTucs.Add(new TinTuc
        {
            TieuDe = tieuDe.Trim(), NoiDung = noiDung,
            NgayDang = DateOnly.FromDateTime(ngayDang ?? DateTime.Now), MaNguoiDung = User.MaNguoiDung()
        });
        await _db.SaveChangesAsync();
        BaoThanhCong("✅ Đã đăng tin tức.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Sua(int id)
    {
        var tin = await _db.TinTucs.AsNoTracking().FirstOrDefaultAsync(t => t.MaTinTuc == id);
        return tin == null ? RedirectToAction(nameof(Index)) : View("Form", tin);
    }

    [HttpPost]
    public async Task<IActionResult> Sua(int id, string? tieuDe, string? noiDung, DateTime? ngayDang)
    {
        var tin = await _db.TinTucs.FirstOrDefaultAsync(t => t.MaTinTuc == id);
        if (tin == null) return RedirectToAction(nameof(Index));
        if (string.IsNullOrWhiteSpace(tieuDe)) { BaoLoi("Tiêu đề không được để trống!"); return RedirectToAction(nameof(Sua), new { id }); }
        tin.TieuDe = tieuDe.Trim(); tin.NoiDung = noiDung;
        if (ngayDang != null) tin.NgayDang = DateOnly.FromDateTime(ngayDang.Value);
        await _db.SaveChangesAsync();
        BaoThanhCong("✅ Đã cập nhật tin tức.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Xoa(int id)
    {
        var tin = await _db.TinTucs.FirstOrDefaultAsync(t => t.MaTinTuc == id);
        if (tin != null) { _db.TinTucs.Remove(tin); await _db.SaveChangesAsync(); BaoThanhCong("Đã xóa tin tức."); }
        return RedirectToAction(nameof(Index));
    }
}
