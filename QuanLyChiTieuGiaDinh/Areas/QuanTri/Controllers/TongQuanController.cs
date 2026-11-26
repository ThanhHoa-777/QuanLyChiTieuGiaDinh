using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shopson.Data;
using Shopson.DichVu;
using Shopson.Models;

namespace Shopson.Areas.QuanTri.Controllers;

public class TongQuanVM
{
    public int TongSanPham, TongThuongHieu, TongNguoiDung, TongDon, DonChoGiao, DonHoanThanh, TinChuaDoc, DonChuyenKhoanChoXacNhan, SoKhieuNai;
    public decimal DoanhThuThang, CongNoKhachHang, CongNoNcc, SoDuKhaDung;
    public List<DonHang> DonGanDay { get; set; } = new();
    public List<DonCanChuY> DonCanChuY { get; set; } = new();   // đơn chờ giao quá lâu, khiếu nại tồn đọng, chưa hoàn tiền
}

/// <summary>Trang tổng quan: các con số thống kê của cửa hàng.</summary>
public class TongQuanController : QuanTriController
{
    private readonly ShopSonContext _db;
    private readonly DichVuDonHang _donHang;
    private readonly DichVuCongNo _congNo;

    public TongQuanController(ShopSonContext db, DichVuDonHang donHang, DichVuCongNo congNo)
    {
        _db = db;
        _donHang = donHang;
        _congNo = congNo;
    }

    public async Task<IActionResult> Index()
    {
        await _donHang.TuDongCapNhatAsync();
        var homNay = DateTime.Now;
        var dauThang = new DateTime(homNay.Year, homNay.Month, 1);
        var vm = new TongQuanVM
        {
            TongSanPham = await _db.SanPhams.CountAsync(),
            TongThuongHieu = await _db.ThuongHieus.CountAsync(),
            TongNguoiDung = await _db.NguoiDungs.CountAsync(),
            TongDon = await _db.DonHangs.CountAsync(),
            DonChoGiao = await _db.DonHangs.CountAsync(d => HangSo.TrangThaiChuaGiao.Contains(d.TrangThai)),   // gồm cả trạng thái cũ CHO_XAC_NHAN
            DonHoanThanh = await _db.DonHangs.CountAsync(d => d.TrangThai == HangSo.HoanThanh),
            DoanhThuThang = await _db.DonHangs.Where(d => d.TrangThai == HangSo.HoanThanh && (d.NgayHoanThanh ?? d.NgayCapNhat) >= dauThang).SumAsync(d => d.TongTien),
            TinChuaDoc = await _db.TinNhans.CountAsync(t => !t.LaAdmin && !t.DaDoc),
            DonChuyenKhoanChoXacNhan = await _db.DonHangs.CountAsync(d => d.PhuongThucThanhToan == HangSo.ThanhToanChuyenKhoan && !d.DaNhanTien
                && d.TrangThai != HangSo.Huy && d.TrangThai != HangSo.HoanThanh),
            SoKhieuNai = await _db.DonHangs.CountAsync(d => d.DaKhieuNai && d.TrangThai != HangSo.HoanThanh && d.TrangThai != HangSo.GiaoThatBai),
            CongNoKhachHang = await _congNo.CongNoKhachHangAsync(),
            CongNoNcc = await _congNo.TongCongNoNccAsync(),
            SoDuKhaDung = await _congNo.SoDuKhaDungAsync(),
            DonGanDay = await _db.DonHangs.AsNoTracking().OrderByDescending(d => d.NgayDat).Take(8).ToListAsync(),
            DonCanChuY = await _donHang.LayDonCanChuYAsync()
        };
        return View(vm);
    }
}