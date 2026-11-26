using Microsoft.EntityFrameworkCore;
using Shopson.Data;
using Shopson.Models;

namespace Shopson.DichVu;

/// <summary>Cộng điểm tích lũy khi đơn hàng hoàn thành (chuyển từ CongDiemDonHangTuChiTiet của PHP).</summary>
public class DichVuTichDiem
{
    private readonly ShopSonContext _db;

    public DichVuTichDiem(ShopSonContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Cộng điểm cho khách theo tổng tiền hàng của đơn (GiaBan x SoLuong, chưa tính phí ship và giảm giá).
    /// Mỗi đơn chỉ được cộng điểm 1 lần. Tự nâng hạng khách theo tổng điểm mới.
    /// Chỉ thay đổi trong bộ nhớ, người gọi chịu trách nhiệm SaveChanges.
    /// </summary>
    public async Task CongDiemDonHangAsync(int maDonHang)
    {
        // Đã cộng điểm cho đơn này rồi thì bỏ qua (không tính các dòng điểm thưởng đánh giá cũng gắn mã đơn)
        bool daCong = await _db.LichSuDiems.AnyAsync(l => l.MaDonHang == maDonHang && !l.GhiChu.StartsWith(HangSo.GhiChuDiemDanhGia));
        if (daCong) return;

        var don = await _db.DonHangs.FirstOrDefaultAsync(d => d.MaDonHang == maDonHang);
        if (don == null || don.MaNguoiDung == null) return; // khách vãng lai không có điểm

        var dsChiTiet = await _db.ChiTietDonHangs.AsNoTracking().Where(c => c.MaDonHang == maDonHang).ToListAsync();
        decimal tongGoc = dsChiTiet.Sum(c => c.GiaBan * c.SoLuong);
        int soDiem = HangThanhVien.TinhDiem(tongGoc);
        if (soDiem <= 0) return;

        var khach = await _db.NguoiDungs.FirstOrDefaultAsync(n => n.MaNguoiDung == don.MaNguoiDung);
        if (khach == null) return;

        _db.LichSuDiems.Add(new LichSuDiem
        {
            MaNguoiDung = khach.MaNguoiDung,
            MaDonHang = maDonHang,
            SoDiemCong = soDiem,
            GhiChu = $"Đơn hàng #{maDonHang} hoàn thành",
            NgayGhi = DateTime.Now
        });

        khach.TongDiem += soDiem;
        khach.HangKhachHang = HangThanhVien.XepHang(khach.TongDiem);
    }

    /// <summary>
    /// Cộng điểm thưởng khi khách đánh giá 1 sản phẩm của 1 đơn (HangSo.DiemDanhGia điểm).
    /// Dòng lịch sử gắn mã đơn để bấm xem lại được; ghi chú bắt đầu bằng HangSo.GhiChuDiemDanhGia.
    /// Tự nâng hạng. Người gọi chịu trách nhiệm SaveChanges.
    /// </summary>
    public async Task CongDiemDanhGiaAsync(int maNguoiDung, int maDonHang, string tenSanPham)
    {
        var khach = await _db.NguoiDungs.FirstOrDefaultAsync(n => n.MaNguoiDung == maNguoiDung);
        if (khach == null) return;

        _db.LichSuDiems.Add(new LichSuDiem
        {
            MaNguoiDung = maNguoiDung,
            MaDonHang = maDonHang,
            SoDiemCong = HangSo.DiemDanhGia,
            GhiChu = $"{HangSo.GhiChuDiemDanhGia} \"{TienIch.CatChuoi(tenSanPham, 120)}\" (đơn #{maDonHang})",
            NgayGhi = DateTime.Now
        });
        khach.TongDiem += HangSo.DiemDanhGia;
        khach.HangKhachHang = HangThanhVien.XepHang(khach.TongDiem);
    }
}