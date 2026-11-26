using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shopson.Data;
using Shopson.DichVu;
using Shopson.Models;

namespace Shopson.Areas.QuanTri.Controllers;

public class CuocTroChuyenVM
{
    public NguoiDung Khach { get; set; } = null!;
    public int SoChuaDoc { get; set; }
    public TinNhan? TinCuoi { get; set; }
}

/// <summary>Tin nhắn từ khách: danh sách cuộc trò chuyện (có số tin chưa đọc) và trả lời từng khách.</summary>
public class TinNhanController : QuanTriController
{
    private readonly ShopSonContext _db;
    private readonly DichVuAnh _anh;

    public TinNhanController(ShopSonContext db, DichVuAnh anh)
    {
        _db = db;
        _anh = anh;
    }

    public async Task<IActionResult> Index()
    {
        // Chỉ lấy tin TRÒ CHUYỆN (bỏ thông báo tự động) để danh sách không bị thông báo đơn hàng làm trôi
        var tatCa = await _db.TinNhans.AsNoTracking().Where(t => t.MaNguoiGui != null && !t.LaThongBao).ToListAsync();
        var dsMa = tatCa.Select(t => t.MaNguoiGui!.Value).Distinct().ToList();
        var khach = await _db.NguoiDungs.AsNoTracking().Where(n => dsMa.Contains(n.MaNguoiDung)).ToListAsync();

        var ds = khach.Select(k => new CuocTroChuyenVM
        {
            Khach = k,
            SoChuaDoc = tatCa.Count(t => t.MaNguoiGui == k.MaNguoiDung && !t.LaAdmin && !t.DaDoc),
            TinCuoi = tatCa.Where(t => t.MaNguoiGui == k.MaNguoiDung).OrderByDescending(t => t.NgayGui).FirstOrDefault()
        }).OrderByDescending(c => c.SoChuaDoc > 0).ThenByDescending(c => c.TinCuoi?.NgayGui).ToList();
        return View(ds);
    }

    public async Task<IActionResult> HoiThoai(int id)
    {
        var khach = await _db.NguoiDungs.AsNoTracking().FirstOrDefaultAsync(n => n.MaNguoiDung == id);
        if (khach == null) return RedirectToAction(nameof(Index));

        var chuaDoc = await _db.TinNhans.Where(t => t.MaNguoiGui == id && !t.LaAdmin && !t.DaDoc).ToListAsync();
        foreach (var t in chuaDoc) t.DaDoc = true;
        if (chuaDoc.Count > 0) await _db.SaveChangesAsync();

        ViewBag.Khach = khach;
        return View(await _db.TinNhans.AsNoTracking().Where(t => t.MaNguoiGui == id && !t.LaThongBao).OrderBy(t => t.NgayGui).ToListAsync());
    }

    [HttpPost]
    public async Task<IActionResult> TraLoi(int id, string? noiDung, IFormFile? anh)
    {
        noiDung = (noiDung ?? "").Trim();
        string? duongDanAnh = await _anh.LuuAnhAsync(anh, "tinnhan");
        if (DichVuAnh.CoChonAnh(anh) && duongDanAnh == null)
        {
            BaoLoi(DichVuAnh.ThongBaoAnhSai);
            return RedirectToAction(nameof(HoiThoai), new { id });
        }
        if (noiDung != "" || duongDanAnh != null)
        {
            _db.TinNhans.Add(new TinNhan { MaNguoiGui = id, TenNguoiGui = "ShopSon", NoiDung = noiDung, HinhAnh = duongDanAnh, NgayGui = DateTime.Now, LaAdmin = true, DaDoc = false });
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(HoiThoai), new { id });
    }
}
