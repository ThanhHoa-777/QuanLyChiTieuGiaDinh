using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shopson.Data;
using Shopson.DichVu;
using Shopson.Models;

namespace Shopson.Areas.QuanTri.Controllers;

/// <summary>Quản lý người dùng: xem, sửa thông tin, khóa/mở khóa, đặt lại mật khẩu, xóa (chỉ khi chưa có hoạt động).</summary>
public class NguoiDungController : QuanTriController
{
    private readonly ShopSonContext _db;

    public NguoiDungController(ShopSonContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index() => View(await _db.NguoiDungs.AsNoTracking().OrderBy(n => n.MaNguoiDung).ToListAsync());

    [HttpGet]
    public async Task<IActionResult> Sua(int id)
    {
        var nd = await _db.NguoiDungs.AsNoTracking().FirstOrDefaultAsync(n => n.MaNguoiDung == id);
        return nd == null ? RedirectToAction(nameof(Index)) : View(nd);
    }

    [HttpPost]
    public async Task<IActionResult> Sua(int id, string? tenNguoiDung, string? email, string? soDienThoai, string? diaChi, int quyenHan)
    {
        var nd = await _db.NguoiDungs.FirstOrDefaultAsync(n => n.MaNguoiDung == id);
        if (nd == null) return RedirectToAction(nameof(Index));
        if (string.IsNullOrWhiteSpace(tenNguoiDung)) { BaoLoi("Họ tên không được để trống!"); return RedirectToAction(nameof(Sua), new { id }); }
        if (!string.IsNullOrWhiteSpace(email) && !DichVuEmail.EmailHopLe(email)) { BaoLoi("Email không hợp lệ (ví dụ đúng: ten@gmail.com)!"); return RedirectToAction(nameof(Sua), new { id }); }

        nd.TenNguoiDung = tenNguoiDung.Trim();
        nd.Email = (email ?? "").Trim();
        nd.SoDienThoai = (soDienThoai ?? "").Trim();
        nd.DiaChi = (diaChi ?? "").Trim();
        if (id != User.MaNguoiDung()) // không cho tự đổi quyền của chính mình (tránh mất quyền admin)
            nd.QuyenHan = quyenHan == HangSo.QuyenQuanTri ? HangSo.QuyenQuanTri : HangSo.QuyenKhachHang;
        await _db.SaveChangesAsync();
        BaoThanhCong("✅ Đã cập nhật người dùng.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Khoa(int id)
    {
        if (id == User.MaNguoiDung()) { BaoLoi("Bạn không thể tự khóa tài khoản của mình!"); return RedirectToAction(nameof(Index)); }
        var nd = await _db.NguoiDungs.FirstOrDefaultAsync(n => n.MaNguoiDung == id);
        if (nd != null)
        {
            nd.Khoa = !nd.Khoa;
            await _db.SaveChangesAsync();
            BaoThanhCong(nd.Khoa ? $"🔒 Đã khóa tài khoản {nd.TenDangNhap}." : $"🔓 Đã mở khóa tài khoản {nd.TenDangNhap}.");
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Tính năng mới: đặt lại mật khẩu về 123456 khi khách quên.</summary>
    [HttpPost]
    public async Task<IActionResult> DatLaiMatKhau(int id)
    {
        var nd = await _db.NguoiDungs.FirstOrDefaultAsync(n => n.MaNguoiDung == id);
        if (nd != null)
        {
            nd.MatKhau = TienIch.MaHoaMd5("123456");
            await _db.SaveChangesAsync();
            BaoThanhCong($"🔑 Đã đặt lại mật khẩu của {nd.TenDangNhap} về 123456. Hãy nhắc khách đổi mật khẩu sau khi đăng nhập.");
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Xoa(int id)
    {
        if (id == User.MaNguoiDung()) { BaoLoi("Bạn không thể tự xóa tài khoản của mình!"); return RedirectToAction(nameof(Index)); }
        bool coHoatDong = await _db.DonHangs.AnyAsync(d => d.MaNguoiDung == id) || await _db.DanhGia.AnyAsync(g => g.MaNguoiDung == id)
                          || await _db.LichSuDiems.AnyAsync(l => l.MaNguoiDung == id) || await _db.NhapKhos.AnyAsync(n => n.NguoiNhap == id)
                          || await _db.CongNos.AnyAsync(c => c.MaNguoiDung == id);
        if (coHoatDong)
        {
            BaoLoi("Không xóa được: người dùng này đã có đơn hàng / đánh giá / lịch sử. Hãy khóa tài khoản thay vì xóa.");
            return RedirectToAction(nameof(Index));
        }
        var nd = await _db.NguoiDungs.FirstOrDefaultAsync(n => n.MaNguoiDung == id);
        if (nd != null)
        {
            _db.GioHangs.RemoveRange(await _db.GioHangs.Where(g => g.MaNguoiDung == id).ToListAsync());
            _db.TinNhans.RemoveRange(await _db.TinNhans.Where(t => t.MaNguoiGui == id).ToListAsync());
            foreach (var tin in await _db.TinTucs.Where(t => t.MaNguoiDung == id).ToListAsync()) tin.MaNguoiDung = null;
            _db.NguoiDungs.Remove(nd);
            await _db.SaveChangesAsync();
            BaoThanhCong("Đã xóa người dùng.");
        }
        return RedirectToAction(nameof(Index));
    }
}
