using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shopson.Data;
using Shopson.DichVu;
using Shopson.Models;

namespace Shopson.Areas.QuanTri.Controllers;

/// <summary>Quản lý sản phẩm: danh sách, thêm, sửa (có tải ảnh lên), xóa.</summary>
public class SanPhamController : QuanTriController
{
    private static readonly string[] DuoiAnhHopLe = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    private readonly ShopSonContext _db;
    private readonly IWebHostEnvironment _moiTruong;
    private readonly DichVuGia _gia;

    public SanPhamController(ShopSonContext db, IWebHostEnvironment moiTruong, DichVuGia gia)
    {
        _db = db;
        _moiTruong = moiTruong;
        _gia = gia;
    }

    public async Task<IActionResult> Index(string? tuKhoa)
    {
        tuKhoa = (tuKhoa ?? "").Trim();
        var truyVan = _db.SanPhams.AsNoTracking().Include(s => s.MaThuongHieuNavigation).AsQueryable();
        if (tuKhoa != "") truyVan = truyVan.Where(s => s.TenSanPham.Contains(tuKhoa) || s.MaThuongHieuNavigation.TenThuongHieu.Contains(tuKhoa));
        ViewBag.TuKhoa = tuKhoa;
        var ds = await truyVan.OrderByDescending(s => s.MaSanPham).ToListAsync();
        ViewBag.Gia = await _gia.TinhNhieuAsync(ds);   // giá đang áp dụng thật cho từng sản phẩm (flash, chậm bán, mức tối đa)
        return View(ds);
    }

    /// <summary>
    /// Ẩn / hiện sản phẩm (ngừng kinh doanh). Ẩn: khách không thấy, không thêm giỏ, không đặt được;
    /// đơn hàng, phiếu nhập, đánh giá cũ vẫn giữ nguyên. Dùng thay cho Xóa khi sản phẩm đã có dữ liệu.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> AnHien(int id)
    {
        var sp = await _db.SanPhams.FirstOrDefaultAsync(s => s.MaSanPham == id);
        if (sp != null)
        {
            sp.NgungKinhDoanh = !sp.NgungKinhDoanh;
            await _db.SaveChangesAsync();
            BaoThanhCong(sp.NgungKinhDoanh ? $"🙈 Đã ẨN \"{sp.TenSanPham}\" (ngừng kinh doanh, khách không còn thấy)." : $"👁 Đã HIỆN lại \"{sp.TenSanPham}\".");
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Chủ shop bật / tắt chế độ tự giảm giá khi sản phẩm bán chậm.</summary>
    [HttpPost]
    public async Task<IActionResult> BatTatGiam(int id)
    {
        var sp = await _db.SanPhams.FirstOrDefaultAsync(s => s.MaSanPham == id);
        if (sp != null)
        {
            sp.TuDongGiam = !sp.TuDongGiam;
            await _db.SaveChangesAsync();
            BaoThanhCong(sp.TuDongGiam ? $"✅ Đã BẬT giảm giá chậm bán cho \"{sp.TenSanPham}\"." : $"⏸ Đã TẮT giảm giá chậm bán cho \"{sp.TenSanPham}\" (giá về bình thường).");
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Nạp danh sách thương hiệu cho form, và cho biết sản phẩm đã từng được nhập kho chưa
    /// (đã nhập kho thì giá nhập do phiếu nhập quyết định, form chỉ hiển thị chứ không cho sửa).</summary>
    private async Task NapDanhSachChonAsync(int maSanPham = 0)
    {
        // Thương hiệu đã ẩn thì không chọn được nữa (trừ thương hiệu hiện tại của sản phẩm đang sửa)
        int maThHienTai = maSanPham > 0 ? await _db.SanPhams.Where(s => s.MaSanPham == maSanPham).Select(s => s.MaThuongHieu).FirstOrDefaultAsync() : 0;
        ViewBag.DanhSachThuongHieu = await _db.ThuongHieus.AsNoTracking()
            .Where(t => !t.NgungKinhDoanh || t.MaThuongHieu == maThHienTai).OrderBy(t => t.TenThuongHieu).ToListAsync();
        ViewBag.DaCoPhieuNhap = maSanPham > 0 && await _db.ChiTietNhapKhos.AnyAsync(c => c.MaSanPham == maSanPham);
    }

    [HttpGet]
    public async Task<IActionResult> Them()
    {
        await NapDanhSachChonAsync();
        return View("Form", new SanPham { TonKho = 0, DanhGia = 0m, LaHangMoi = true, GiamToiDa = HangSo.GiamToiDaMacDinh, TuDongGiam = true, PhanTramChamBan = HangSo.PhanTramGiamChamBan });
    }

    [HttpPost]
    public async Task<IActionResult> Them(SanPham sp, IFormFile? anh)
    {
        if (!HopLe(sp, out string loi)) { BaoLoi(loi); await NapDanhSachChonAsync(); return View("Form", sp); }
        string? duongDan = await LuuAnhAsync(anh);
        if (anh != null && anh.Length > 0 && duongDan == null) { BaoLoi("Ảnh không hợp lệ (chỉ nhận jpg, png, gif, webp tối đa 5MB)!"); await NapDanhSachChonAsync(); return View("Form", sp); }

        sp.MaSanPham = 0;
        sp.TonKho = 0;               // sản phẩm mới luôn tồn kho 0: muốn có hàng phải tạo PHIẾU NHẬP (để có giá nhập thật và công nợ NCC)
        sp.NgayTao = DateTime.Now;   // mốc bắt đầu bán, dùng để xét giảm giá chậm bán
        sp.HinhAnh = duongDan ?? "";
        sp.DaBan = 0; sp.SoDanhGia = 0; sp.DanhGia = 0m;
        sp.MoTa = string.IsNullOrWhiteSpace(sp.MoTa) ? null : sp.MoTa;
        _db.SanPhams.Add(sp);
        await _db.SaveChangesAsync();
        BaoThanhCong($"✅ Đã thêm sản phẩm mới \"{sp.TenSanPham}\" (tồn kho 0)." + CanhBaoLoiNhuan(sp)
                     + " Bước tiếp theo: tạo phiếu nhập để có hàng bán.");
        // Chuyển sang trang tạo phiếu nhập, chọn sẵn sản phẩm vừa thêm và giá nhập dự kiến
        return RedirectToAction("Them", "Kho", new { maSanPham = sp.MaSanPham });
    }

    [HttpGet]
    public async Task<IActionResult> Sua(int id)
    {
        var sp = await _db.SanPhams.AsNoTracking().FirstOrDefaultAsync(s => s.MaSanPham == id);
        if (sp == null) return RedirectToAction(nameof(Index));
        await NapDanhSachChonAsync(id);
        return View("Form", sp);
    }

    [HttpPost]
    public async Task<IActionResult> Sua(int id, SanPham form, IFormFile? anh)
    {
        var sp = await _db.SanPhams.FirstOrDefaultAsync(s => s.MaSanPham == id);
        if (sp == null) return RedirectToAction(nameof(Index));
        if (!HopLe(form, out string loi)) { BaoLoi(loi); await NapDanhSachChonAsync(id); form.MaSanPham = id; form.HinhAnh = sp.HinhAnh; form.TonKho = sp.TonKho; return View("Form", form); }

        string? duongDan = await LuuAnhAsync(anh);
        if (anh != null && anh.Length > 0 && duongDan == null) { BaoLoi("Ảnh không hợp lệ (chỉ nhận jpg, png, gif, webp tối đa 5MB)!"); await NapDanhSachChonAsync(id); form.MaSanPham = id; form.HinhAnh = sp.HinhAnh; form.TonKho = sp.TonKho; return View("Form", form); }

        sp.TenSanPham = form.TenSanPham.Trim();
        sp.MaThuongHieu = form.MaThuongHieu;
        sp.GiaBan = form.GiaBan;
        // Tồn kho KHÔNG sửa ở đây: chỉ thay đổi qua phiếu nhập (cộng) và giao hàng (trừ).
        // Giá nhập: đã có phiếu nhập thì lấy theo phiếu nhập gần nhất, chưa có thì cho sửa giá dự kiến.
        bool daCoPhieuNhap = await _db.ChiTietNhapKhos.AnyAsync(c => c.MaSanPham == id);
        if (!daCoPhieuNhap) sp.GiaVon = form.GiaVon;
        sp.LoaiSon = form.LoaiSon ?? "";
        sp.LaHangMoi = form.LaHangMoi;
        sp.GiamToiDa = form.GiamToiDa;
        sp.TuDongGiam = form.TuDongGiam;
        sp.PhanTramChamBan = form.PhanTramChamBan;
        if (form.NgayTao.Year > 2000) sp.NgayTao = form.NgayTao.Date;
        sp.MoTa = string.IsNullOrWhiteSpace(form.MoTa) ? null : form.MoTa;
        if (duongDan != null) sp.HinhAnh = duongDan;   // không chọn ảnh mới thì giữ ảnh cũ
        await _db.SaveChangesAsync();
        BaoThanhCong("✅ Đã cập nhật sản phẩm." + CanhBaoLoiNhuan(sp));
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Xoa(int id)
    {
        bool dangDuocDung = await _db.ChiTietDonHangs.AnyAsync(c => c.MaSanPham == id)
                            || await _db.ChiTietNhapKhos.AnyAsync(c => c.MaSanPham == id)
                            || await _db.DanhGia.AnyAsync(g => g.MaSanPham == id)
                            || await _db.DieuChinhKhos.AnyAsync(d => d.MaSanPham == id);
        if (dangDuocDung)
        {
            BaoLoi("Không xóa được: sản phẩm đã có trong đơn hàng, phiếu nhập, phiếu điều chỉnh kho hoặc đánh giá. Hãy bấm \"Ẩn\" để ngừng bán (dữ liệu cũ vẫn giữ nguyên).");
            return RedirectToAction(nameof(Index));
        }
        var sp = await _db.SanPhams.FirstOrDefaultAsync(s => s.MaSanPham == id);
        if (sp != null)
        {
            var dongGio = await _db.GioHangs.Where(g => g.MaSanPham == id).ToListAsync();
            var dongFlash = await _db.FlashSaleNgays.Where(f => f.MaSanPham == id).ToListAsync();
            _db.GioHangs.RemoveRange(dongGio);
            _db.FlashSaleNgays.RemoveRange(dongFlash);
            _db.SanPhams.Remove(sp);
            await _db.SaveChangesAsync();
            BaoThanhCong("Đã xóa sản phẩm.");
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Cảnh báo (KHÔNG chặn) khi giá bán thấp hơn giá gợi ý: nếu giảm tối đa thì lãi bao nhiêu, hoặc có bị lỗ không.
    /// Trả về chuỗi rỗng nếu ổn hoặc chưa biết giá nhập.
    /// </summary>
    private static string CanhBaoLoiNhuan(SanPham sp)
    {
        if (sp.GiaVon <= 0 || sp.GiaBan <= 0) return "";
        decimal giaSan = DichVuGia.TinhGiaSan(sp.GiaBan, sp.GiamToiDa);
        decimal laiToiDa = (giaSan - sp.GiaVon) / sp.GiaVon * 100m;   // lãi % khi bị giảm hết cỡ
        decimal goiY = DichVuGia.GoiYGiaBan(sp.GiaVon, sp.GiamToiDa);
        if (laiToiDa < 0) return $" ⚠ Lưu ý: nếu giảm tối đa {sp.GiamToiDa}% thì giá còn {TienIch.DinhDangTien(giaSan)}, THẤP HƠN giá nhập {TienIch.DinhDangTien(sp.GiaVon)} (lỗ). Giá gợi ý: {TienIch.DinhDangTien(goiY)}.";
        if (laiToiDa < HangSo.LoiNhuanToiThieu) return $" ⚠ Lưu ý: nếu giảm tối đa {sp.GiamToiDa}% thì lãi chỉ còn {laiToiDa:0.#}%. Giá gợi ý: {TienIch.DinhDangTien(goiY)}.";
        return "";
    }

    private static bool HopLe(SanPham sp, out string loi)
    {
        loi = "";
        if (string.IsNullOrWhiteSpace(sp.TenSanPham)) loi = "Tên sản phẩm không được để trống!";
        else if (sp.MaThuongHieu <= 0) loi = "Vui lòng chọn thương hiệu!";
        else if (sp.GiaBan < 0 || sp.GiaVon < 0) loi = "Giá không được âm!";
        else if (sp.TonKho < 0) loi = "Tồn kho không được âm!";
        else if (sp.GiamToiDa < 0 || sp.GiamToiDa > 70) loi = "Mức giảm tối đa phải từ 0% đến 70%!";
        // % giảm chậm bán: từ 1% và không vượt mức giảm tối đa (vượt thì giá cũng bị chặn ở giá sàn, đặt cao hơn là vô nghĩa)
        else if (sp.TuDongGiam && (sp.PhanTramChamBan < 1 || sp.PhanTramChamBan > sp.GiamToiDa))
            loi = $"% giảm khi bán chậm phải từ 1% đến mức giảm tối đa ({sp.GiamToiDa}%)!";
        else if (sp.PhanTramChamBan < 0 || sp.PhanTramChamBan > 70) loi = "% giảm khi bán chậm phải từ 0% đến 70%!";
        return loi == "";
    }

    /// <summary>Lưu ảnh vào wwwroot/hinhanh với tên ngẫu nhiên, trả về "hinhanh/tên-file" (giống cách bản PHP lưu vào DB).</summary>
    private async Task<string?> LuuAnhAsync(IFormFile? anh)
    {
        if (anh == null || anh.Length == 0) return null;
        string duoi = Path.GetExtension(anh.FileName).ToLower();
        if (!DuoiAnhHopLe.Contains(duoi) || anh.Length > 5 * 1024 * 1024) return null;

        string ten = $"sp_{DateTimeOffset.Now.ToUnixTimeSeconds()}_{Random.Shared.Next(1000, 9999)}{duoi}";
        string thuMuc = Path.Combine(_moiTruong.WebRootPath, "hinhanh");
        Directory.CreateDirectory(thuMuc);
        using (var luong = new FileStream(Path.Combine(thuMuc, ten), FileMode.Create))
        {
            await anh.CopyToAsync(luong);
        }
        return "hinhanh/" + ten;
    }
}