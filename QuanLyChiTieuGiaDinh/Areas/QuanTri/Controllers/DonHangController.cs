using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shopson.Data;
using Shopson.DichVu;
using Shopson.Models;

namespace Shopson.Areas.QuanTri.Controllers;

/// <summary>Quản lý đơn hàng: lọc theo trạng thái, xem chi tiết, giao hàng, hủy, nhận tiền chuyển khoản, hoàn tiền, cập nhật kho.</summary>
public class DonHangController : QuanTriController
{
    private readonly ShopSonContext _db;
    private readonly DichVuDonHang _donHang;

    public DonHangController(ShopSonContext db, DichVuDonHang donHang)
    {
        _db = db;
        _donHang = donHang;
    }

    public async Task<IActionResult> Index(string? tt)
    {
        await _donHang.TuDongCapNhatAsync();
        var truyVan = _db.DonHangs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(tt))
        {
            // Hai trạng thái chờ giao được gộp chung như bản PHP
            if (tt == HangSo.XacNhan) truyVan = truyVan.Where(d => d.TrangThai == HangSo.XacNhan || d.TrangThai == HangSo.ChoXacNhan);
            else truyVan = truyVan.Where(d => d.TrangThai == tt);
        }
        ViewBag.TrangThaiLoc = tt ?? "";
        ViewBag.SoKhieuNai = await _db.DonHangs.CountAsync(d => d.DaKhieuNai && d.TrangThai != HangSo.HoanThanh && d.TrangThai != HangSo.GiaoThatBai);
        ViewBag.DonCanChuY = await _donHang.LayDonCanChuYAsync();

        var ds = await truyVan.OrderByDescending(d => d.NgayDat).ToListAsync();
        return View(ds);
    }

    public async Task<IActionResult> ChiTiet(int id)
    {
        await _donHang.TuDongCapNhatAsync();
        var don = await _db.DonHangs.AsNoTracking().Include(d => d.ChiTietDonHangs).Include(d => d.MaNguoiDungNavigation)
            .FirstOrDefaultAsync(d => d.MaDonHang == id);
        if (don == null)
        {
            BaoLoi("Không tìm thấy đơn hàng!");
            return RedirectToAction(nameof(Index));
        }
        return View(don);
    }

    private Task<DonHang?> LayDonAsync(int id) => _db.DonHangs.FirstOrDefaultAsync(d => d.MaDonHang == id);

    /// <summary>Bấm "Giao hàng": chuyển ĐANG GIAO và trừ tồn kho.</summary>
    [HttpPost]
    public async Task<IActionResult> GiaoHang(int id)
    {
        var don = await LayDonAsync(id);
        if (don == null) return RedirectToAction(nameof(Index));
        if (!HangSo.TrangThaiChuaGiao.Contains(don.TrangThai)) { BaoLoi("Đơn này không thể chuyển sang giao hàng!"); return RedirectToAction(nameof(ChiTiet), new { id }); }
        if (!DichVuDonHang.CoTheGiao(don)) { BaoLoi("Đơn chuyển khoản chưa nhận được tiền: hãy xác nhận đã nhận tiền trước khi giao hàng!"); return RedirectToAction(nameof(ChiTiet), new { id }); }

        var loi = await _donHang.ChuyenDangGiaoAsync(don);
        if (loi.Count > 0)
            BaoLoi("Chưa giao được, không đủ hàng: " + string.Join(" ", loi) + " Hãy tạo phiếu nhập thêm hàng rồi giao, hoặc nhắn khách / hủy đơn.");
        else
            BaoThanhCong($"🚚 Đơn #{id} đã chuyển sang ĐANG GIAO và đã trừ tồn kho.");
        return RedirectToAction(nameof(ChiTiet), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Huy(int id, string? lyDo)
    {
        var don = await LayDonAsync(id);
        if (don == null) return RedirectToAction(nameof(Index));
        lyDo = (lyDo ?? "").Trim();
        if (lyDo == "") { BaoLoi("Vui lòng nhập lý do hủy đơn!"); return RedirectToAction(nameof(ChiTiet), new { id }); }

        if (await _donHang.AdminHuyDonAsync(don, lyDo)) BaoThanhCong($"Đã hủy đơn #{id} và gửi tin nhắn cho khách.");
        else BaoLoi("Đơn này đã giao nên không thể hủy!");
        return RedirectToAction(nameof(ChiTiet), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> HoanThanh(int id)
    {
        var don = await LayDonAsync(id);
        if (don != null && (don.TrangThai == HangSo.DangGiao || don.TrangThai == HangSo.DaGiaoChoXacNhan))
        {
            await _donHang.AdminHoanThanhAsync(don);
            BaoThanhCong($"✅ Đơn #{id} đã HOÀN THÀNH (đã cộng điểm và số đã bán).");
        }
        else BaoLoi("Chỉ đơn đang giao hoặc đã giao mới hoàn thành được.");
        return RedirectToAction(nameof(ChiTiet), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> GiaoThatBai(int id)
    {
        var don = await LayDonAsync(id);
        if (don != null && (don.TrangThai == HangSo.DangGiao || don.TrangThai == HangSo.DaGiaoChoXacNhan))
        {
            await _donHang.GiaoThatBaiAsync(don);
            BaoThanhCong($"Đơn #{id} đã chuyển sang GIAO THẤT BẠI.");
        }
        else BaoLoi("Chỉ đơn đang giao hoặc đã giao mới chuyển giao thất bại được.");
        return RedirectToAction(nameof(ChiTiet), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> XacNhanTien(int id)
    {
        var don = await LayDonAsync(id);
        if (don != null && await _donHang.XacNhanNhanTienAsync(don)) BaoThanhCong($"💰 Đã xác nhận nhận tiền chuyển khoản đơn #{id}.");
        else BaoLoi("Không thể xác nhận (không phải đơn chuyển khoản hoặc đã xác nhận rồi).");
        return RedirectToAction(nameof(ChiTiet), new { id });
    }

    /// <summary>Đơn CK đã hủy nhưng thực tế khách đã chuyển tiền -> ghi nhận để hoàn tiền.</summary>
    [HttpPost]
    public async Task<IActionResult> NhanTienSauHuy(int id)
    {
        var don = await LayDonAsync(id);
        if (don != null && await _donHang.XacNhanNhanTienSauHuyAsync(don))
            BaoThanhCong($"💰 Đã ghi nhận tiền chuyển khoản của đơn #{id} (đơn vẫn ở trạng thái Hủy). Hỏi số tài khoản của khách rồi hoàn tiền bên dưới.");
        else BaoLoi("Không thể ghi nhận: chỉ áp dụng cho đơn chuyển khoản đã hủy và chưa ghi nhận tiền.");
        return RedirectToAction(nameof(ChiTiet), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> HoanTien(int id, string? soTaiKhoan, string? tenChuTaiKhoan)
    {
        var don = await LayDonAsync(id);
        // Đơn admin hủy chưa có STK: lấy STK admin vừa nhập (khách gửi qua tin nhắn) rồi mới hoàn tiền
        if (don != null && DichVuDonHang.CanNhapTaiKhoanHoan(don))
        {
            soTaiKhoan = (soTaiKhoan ?? "").Trim();
            tenChuTaiKhoan = (tenChuTaiKhoan ?? "").Trim();
            if (soTaiKhoan == "" || tenChuTaiKhoan == "")
            {
                BaoLoi("Vui lòng nhập số tài khoản và tên chủ tài khoản của khách (xem trong tin nhắn) trước khi xác nhận hoàn tiền!");
                return RedirectToAction(nameof(ChiTiet), new { id });
            }
            don.StkhoanTien = soTaiKhoan;
            don.TenTkhoanTien = tenChuTaiKhoan;
        }
        if (don != null && await _donHang.HoanTienChuyenKhoanAsync(don)) BaoThanhCong($"💸 Đã ghi nhận hoàn tiền cho đơn #{id} và trừ công nợ.");
        else BaoLoi("Đơn này chưa đủ điều kiện hoàn tiền.");
        return RedirectToAction(nameof(ChiTiet), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> CapNhatKho(int id)
    {
        var don = await LayDonAsync(id);
        if (don != null && await _donHang.CapNhatLaiKhoAsync(don)) BaoThanhCong($"📦 Đã cộng lại tồn kho cho đơn #{id}.");
        else BaoLoi("Không thể cập nhật kho (đơn chưa giao thất bại hoặc đã cập nhật rồi).");
        return RedirectToAction(nameof(ChiTiet), new { id });
    }

    /// <summary>In hóa đơn của bất kỳ đơn nào (dành cho admin).</summary>
    [HttpGet]
    public async Task<IActionResult> InHoaDon(int id)
    {
        var don = await _db.DonHangs.AsNoTracking().Include(d => d.ChiTietDonHangs).FirstOrDefaultAsync(d => d.MaDonHang == id);
        if (don == null) return RedirectToAction(nameof(Index));
        var voucher = string.IsNullOrEmpty(don.MaGiamGia) ? null : await _db.MaGiamGia.AsNoTracking().FirstOrDefaultAsync(v => v.MaCode == don.MaGiamGia);
        return View(Shopson.ViewModels.HoaDonVM.Tao(don, voucher));
    }

    /// <summary>Xuất danh sách đơn hàng (đang lọc theo trạng thái) ra file Excel (.csv).</summary>
    [HttpGet]
    public async Task<IActionResult> XuatExcel(string? tt)
    {
        var truyVan = _db.DonHangs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(tt))
        {
            if (tt == HangSo.XacNhan) truyVan = truyVan.Where(d => d.TrangThai == HangSo.XacNhan || d.TrangThai == HangSo.ChoXacNhan);
            else truyVan = truyVan.Where(d => d.TrangThai == tt);
        }
        var ds = await truyVan.OrderByDescending(d => d.NgayDat).ToListAsync();
        return TaoFileCsv($"DonHang_{DateTime.Now:yyyyMMdd_HHmm}.csv",
            new[] { "Mã đơn", "Ngày đặt", "Khách hàng", "Loại khách", "Số điện thoại", "Trạng thái", "Thanh toán", "Mã giảm giá", "Tiền giảm", "Tổng tiền", "Lý do hủy" },
            ds.Select(d => new[]
            {
                d.MaDonHang.ToString(), d.NgayDat.ToString("dd/MM/yyyy HH:mm"), d.HoTen, d.MaNguoiDung == null ? "Khách" : "Thành viên",
                d.SoDienThoai, DichVuDonHang.TenTrangThaiAdmin(d.TrangThai),
                d.PhuongThucThanhToan == HangSo.ThanhToanChuyenKhoan ? "Chuyển khoản" : "COD",
                d.MaGiamGia ?? "", ((long)d.TienGiam).ToString(), ((long)d.TongTien).ToString(),
                d.TrangThai == HangSo.Huy ? (d.LyDoHuy ?? "") : ""
            }));
    }
}