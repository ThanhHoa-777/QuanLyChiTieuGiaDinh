using Microsoft.EntityFrameworkCore;
using Shopson.Data;
using Shopson.Models;

namespace Shopson.DichVu;

/// <summary>
/// THÔNG BÁO "ƯU ĐÃI KÉP": sản phẩm hôm nay VỪA đang Flash Sale VỪA đang giảm giá vì bán chậm.
/// Không phải ngày nào cũng có, nên chỉ gửi khi thật sự có sản phẩm trùng cả 2 loại giảm,
/// mỗi ngày tối đa 1 lần, gửi vào tab "Thông báo" của mọi khách hàng (tài khoản không bị khóa).
/// Được dịch vụ chạy nền gọi 10 phút / lần, nên khi có danh sách flash hôm nay là khách nhận được.
/// </summary>
public class DichVuUuDaiKep
{
    public const string DauThongBao = "🔥 ƯU ĐÃI KÉP HÔM NAY";   // mở đầu nội dung, dùng để biết hôm nay đã gửi chưa

    private readonly ShopSonContext _db;
    private readonly DichVuGia _gia;

    public DichVuUuDaiKep(ShopSonContext db, DichVuGia gia)
    {
        _db = db;
        _gia = gia;
    }

    public async Task GuiThongBaoNeuCoAsync()
    {
        var homNay = TienIch.HomNay();
        var dauNgay = DateTime.Today;

        // 1. Hôm nay đã gửi rồi thì thôi
        bool daGui = await _db.TinNhans.AnyAsync(t => t.LaThongBao && t.NgayGui >= dauNgay && t.NoiDung.StartsWith(DauThongBao));
        if (daGui) return;

        // 2. Chỉ xét khi danh sách flash hôm nay ĐÃ có (không tự bốc danh sách mới ở đây)
        var dsMaFlash = await _db.FlashSaleNgays.AsNoTracking().Where(f => f.NgaySale == homNay).Select(f => f.MaSanPham).ToListAsync();
        if (dsMaFlash.Count == 0) return;

        // 3. Tính giá: giữ sản phẩm vừa Flash vừa giảm chậm bán
        var dsSanPham = await _db.SanPhams.AsNoTracking().Include(s => s.MaThuongHieuNavigation)
            .Where(DichVuSanPham.DangBan).Where(s => dsMaFlash.Contains(s.MaSanPham)).ToListAsync();
        if (dsSanPham.Count == 0) return;
        var dsGia = await _gia.TinhNhieuAsync(dsSanPham);
        var dsKep = dsSanPham.Where(s => dsGia[s.MaSanPham].LaFlash && dsGia[s.MaSanPham].LaGiamChamBan).ToList();
        if (dsKep.Count == 0) return;   // hôm nay không có sản phẩm nào trùng cả 2 -> không gửi

        // 4. Soạn nội dung
        var dong = dsKep.Select(s =>
        {
            var g = dsGia[s.MaSanPham];
            int phanTram = (int)Math.Round((1 - g.GiaCuoi / g.GiaThuong) * 100);
            return $"• {s.TenSanPham}: chỉ {TienIch.DinhDangTien(g.GiaCuoi)} (giá gốc {TienIch.DinhDangTien(g.GiaThuong)}, giảm {phanTram}%)";
        });
        string noiDung = $"{DauThongBao} ({homNay:dd/MM})\n" +
                         "Các sản phẩm đang vừa Flash Sale vừa có Ưu đãi đặc biệt:\n" +
                         string.Join("\n", dong) +
                         $"\nMỗi khách tối đa {HangSo.FlashToiDaMoiKhach} sản phẩm giá flash / sản phẩm, số lượng có hạn - vào mục Flash Sale để mua ngay!";

        // 5. Gửi cho mọi khách hàng
        var dsKhach = await _db.NguoiDungs.AsNoTracking()
            .Where(n => n.QuyenHan == HangSo.QuyenKhachHang && !n.Khoa).Select(n => n.MaNguoiDung).ToListAsync();
        foreach (int ma in dsKhach)
        {
            _db.TinNhans.Add(new TinNhan
            {
                MaNguoiGui = ma, TenNguoiGui = "ShopSon", NoiDung = noiDung,
                NgayGui = DateTime.Now, LaAdmin = true, DaDoc = false, LaThongBao = true
            });
        }
        await _db.SaveChangesAsync();
    }
}
