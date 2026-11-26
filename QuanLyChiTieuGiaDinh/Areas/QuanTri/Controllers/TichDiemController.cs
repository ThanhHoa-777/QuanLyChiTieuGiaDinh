using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shopson.Data;
using Shopson.DichVu;
using Shopson.Models;

namespace Shopson.Areas.QuanTri.Controllers;

/// <summary>Xem điểm tích lũy và hạng của từng khách hàng, cùng lịch sử cộng điểm.</summary>
public class TichDiemController : QuanTriController
{
    private readonly ShopSonContext _db;

    public TichDiemController(ShopSonContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var ds = await _db.NguoiDungs.AsNoTracking().Where(n => n.QuyenHan == HangSo.QuyenKhachHang).OrderByDescending(n => n.TongDiem).ToListAsync();
        return View(ds);
    }

    public async Task<IActionResult> LichSu(int id)
    {
        var khach = await _db.NguoiDungs.AsNoTracking().FirstOrDefaultAsync(n => n.MaNguoiDung == id);
        if (khach == null) return RedirectToAction(nameof(Index));
        ViewBag.Khach = khach;
        return View(await _db.LichSuDiems.AsNoTracking().Where(l => l.MaNguoiDung == id).OrderByDescending(l => l.NgayGhi).ToListAsync());
    }
}
