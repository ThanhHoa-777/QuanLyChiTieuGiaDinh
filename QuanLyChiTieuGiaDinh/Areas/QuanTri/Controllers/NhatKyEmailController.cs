using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shopson.Data;
using Shopson.DichVu;

namespace Shopson.Areas.QuanTri.Controllers;

/// <summary>Xem nhật ký các email hệ thống đã gửi (thành công / lỗi). Không có nội dung thư, không có mã khôi phục.</summary>
public class NhatKyEmailController : QuanTriController
{
    private readonly ShopSonContext _db;
    private readonly IConfiguration _cauHinh;

    public NhatKyEmailController(ShopSonContext db, IConfiguration cauHinh)
    {
        _db = db;
        _cauHinh = cauHinh;
    }

    /// <summary>200 email gần nhất; lọc theo loại email và chỉ xem email lỗi.</summary>
    public async Task<IActionResult> Index(string? loai, bool chiLoi = false)
    {
        var truyVan = _db.NhatKyEmails.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(loai)) truyVan = truyVan.Where(n => n.LoaiEmail == loai);
        if (chiLoi) truyVan = truyVan.Where(n => !n.ThanhCong);

        ViewBag.Loai = loai;
        ViewBag.ChiLoi = chiLoi;
        ViewBag.TongDaGui = await _db.NhatKyEmails.CountAsync(n => n.ThanhCong);
        ViewBag.TongLoi = await _db.NhatKyEmails.CountAsync(n => !n.ThanhCong);
        ViewBag.DaCauHinh = !string.IsNullOrWhiteSpace(_cauHinh["Email:DiaChiGui"]) && !string.IsNullOrWhiteSpace(_cauHinh["Email:MatKhauUngDung"]);
        ViewBag.DiaChiGui = _cauHinh["Email:DiaChiGui"] ?? "";
        return View(await truyVan.OrderByDescending(n => n.NgayGui).Take(200).ToListAsync());
    }
}
