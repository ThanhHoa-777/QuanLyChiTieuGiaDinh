using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Shopson.Data;
using Shopson.DichVu;
using Shopson.Models;

namespace Shopson.Areas.QuanTri.Controllers;

public class DongXuatKho
{
    public int MaDonHang { get; set; }
    public string HoTen { get; set; } = "";
    public DateTime NgayXuat { get; set; }
    public string TrangThai { get; set; } = "";
    public int TongSoLuong { get; set; }
    public bool LaKhach { get; set; }   // đơn của khách vãng lai (không có tài khoản)
}

public class LichSuKhoVM
{
    public List<NhapKho> PhieuNhap { get; set; } = new();
    public List<DongXuatKho> DonXuat { get; set; } = new();
    public List<DieuChinhKho> DieuChinh { get; set; } = new();   // phiếu điều chỉnh kho gần nhất
}

/// <summary>Trang "Điều chỉnh kho": form lập phiếu + lịch sử các phiếu (lọc được theo sản phẩm).</summary>
public class DieuChinhKhoVM
{
    public List<SanPham> DsSanPham { get; set; } = new();
    public List<DieuChinhKho> LichSu { get; set; } = new();
    public int MaSanPhamChon { get; set; }
    public decimal TongHaoHut => LichSu.Sum(d => d.GiaTriHaoHut);
}

/// <summary>Dữ liệu cho trang chi tiết một phiếu nhập: phiếu + các dòng sản phẩm + công nợ (nợ gốc, các lần trả, còn nợ).</summary>
public class ChiTietPhieuNhapVM
{
    public NhapKho Phieu { get; set; } = null!;
    public DongNoPhieu? No { get; set; }             // null = phiếu không ghi nợ (không chọn NCC, hoặc trả ngay khi nhập)
    public CongNo? DongTraNgay { get; set; }         // phiếu trả ngay khi nhập: dòng THANH_TOAN đó
    public decimal SoDuKhaDung { get; set; }         // doanh thu khả dụng hiện tại
    public int TongSoLuong => Phieu.ChiTietNhapKhos.Sum(c => c.SoLuong);
}

/// <summary>Kho hàng: phiếu nhập, tạo phiếu nhập (cộng tồn kho + ghi công nợ NCC), lịch sử nhập/xuất, tồn kho.</summary>
public class KhoController : QuanTriController
{
    private readonly ShopSonContext _db;
    private readonly DichVuCongNo _congNo;

    public KhoController(ShopSonContext db, DichVuCongNo congNo)
    {
        _db = db;
        _congNo = congNo;
    }

    /// <summary>Danh sách phiếu nhập (mới nhất trước), mỗi phiếu hiện luôn tên các sản phẩm. Lọc được theo nhà cung cấp.</summary>
    public async Task<IActionResult> Index(int maNcc = 0)
    {
        var truyVan = _db.NhapKhos.AsNoTracking().Include(n => n.MaNccNavigation)
            .Include(n => n.ChiTietNhapKhos).ThenInclude(c => c.MaSanPhamNavigation)
            .AsQueryable();
        if (maNcc > 0) truyVan = truyVan.Where(n => n.MaNcc == maNcc);

        ViewBag.MaNcc = maNcc;
        ViewBag.DanhSachNcc = await _db.NhaCungCaps.AsNoTracking().OrderBy(n => n.MaNcc).ToListAsync();
        // Công nợ từng phiếu (để hiện "Đã trả một phần x%", hạn trả, quá hạn)
        ViewBag.NoTheoPhieu = (await _congNo.LayNoTheoPhieuAsync(maNcc: maNcc)).ToDictionary(d => d.MaNhapKho);
        var ds = await truyVan.OrderByDescending(n => n.NgayNhap).ThenByDescending(n => n.MaNhapKho).ToListAsync();
        return View(ds);
    }

    /// <summary>Xem chi tiết một phiếu nhập: nhà cung cấp, người nhập, từng sản phẩm (số lượng, giá nhập, thành tiền) và công nợ của phiếu.</summary>
    public async Task<IActionResult> ChiTiet(int id)
    {
        var phieu = await _db.NhapKhos.AsNoTracking()
            .Include(n => n.MaNccNavigation)
            .Include(n => n.NguoiNhapNavigation)
            .Include(n => n.ChiTietNhapKhos).ThenInclude(c => c.MaSanPhamNavigation)
            .FirstOrDefaultAsync(n => n.MaNhapKho == id);
        if (phieu == null)
        {
            BaoLoi($"Không tìm thấy phiếu nhập #{id}!");
            return RedirectToAction(nameof(Index));
        }

        var vm = new ChiTietPhieuNhapVM
        {
            Phieu = phieu,
            No = (await _congNo.LayNoTheoPhieuAsync(maNhapKho: id)).FirstOrDefault(),
            SoDuKhaDung = await _congNo.SoDuKhaDungAsync()
        };
        if (vm.No == null)
        {
            vm.DongTraNgay = await _db.CongNos.AsNoTracking()
                .FirstOrDefaultAsync(c => c.LoaiDoiTuong == HangSo.CongNoNhaCungCap && c.MaNhapKho == id && c.LoaiGhiChu == HangSo.ThanhToan);
        }
        return View(vm);
    }

    /// <summary>Form tạo phiếu nhập. maSanPham > 0: chọn sẵn sản phẩm đó ở dòng đầu (dùng ngay sau khi thêm sản phẩm mới).</summary>
    [HttpGet]
    public async Task<IActionResult> Them(decimal? khongDu, int maSanPham = 0)
    {
        ViewBag.ChonSanPham = maSanPham;
        ViewBag.DanhSachNcc = await _db.NhaCungCaps.AsNoTracking().OrderBy(n => n.TenNcc).ToListAsync();
        // Sản phẩm đã ẩn (hoặc thuộc thương hiệu đã ẩn) thì không nhập thêm
        ViewBag.DanhSachSanPham = await _db.SanPhams.AsNoTracking()
            .Where(s => !s.NgungKinhDoanh && !s.MaThuongHieuNavigation.NgungKinhDoanh).OrderBy(s => s.TenSanPham).ToListAsync();
        ViewBag.SoDuKhaDung = await _congNo.SoDuKhaDungAsync();
        if (khongDu != null) ViewBag.CanhBao = $"Doanh thu khả dụng không đủ để thanh toán ngay, còn thiếu {TienIch.DinhDangTien(khongDu.Value)}. Hãy bỏ tick \"Đã thanh toán\" để ghi nợ.";
        return View();
    }

    /// <summary>
    /// Lưu phiếu nhập: BẮT BUỘC chọn nhà cung cấp và giá nhập > 0 (để mọi phiếu đều có nguồn hàng và ghi được công nợ).
    /// Cộng tồn kho từng sản phẩm, rồi ghi công nợ nhà cung cấp:
    ///  - Thanh toán ngay: phải đủ doanh thu khả dụng, ghi THANH_TOAN
    ///  - Chưa thanh toán: ghi PHAT_SINH (nợ)
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Them(int maNcc, string? ghiChu, bool daThanhToan,
        List<int>? maSanPham, List<int>? soLuong, List<decimal>? giaNhap)
    {
        maSanPham ??= new(); soLuong ??= new(); giaNhap ??= new();

        // Gom các dòng hợp lệ
        var cacDong = new List<(int MaSP, int SL, decimal Gia)>();
        for (int i = 0; i < maSanPham.Count; i++)
        {
            int sl = i < soLuong.Count ? soLuong[i] : 0;
            decimal gia = i < giaNhap.Count ? giaNhap[i] : 0;
            if (maSanPham[i] > 0 && sl > 0) cacDong.Add((maSanPham[i], sl, gia));
        }
        if (maNcc <= 0 || !await _db.NhaCungCaps.AnyAsync(n => n.MaNcc == maNcc))
        {
            BaoLoi("Vui lòng chọn nhà cung cấp! (Chưa có thì bấm \"➕ Mới\" để thêm nhanh.)");
            return RedirectToAction(nameof(Them));
        }
        if (cacDong.Count == 0)
        {
            BaoLoi("Vui lòng chọn ít nhất 1 sản phẩm và số lượng lớn hơn 0!");
            return RedirectToAction(nameof(Them));
        }
        if (cacDong.Any(d => d.Gia <= 0))
        {
            BaoLoi("Giá nhập của mỗi sản phẩm phải lớn hơn 0!");
            return RedirectToAction(nameof(Them));
        }

        decimal tongTien = cacDong.Sum(d => d.SL * d.Gia);

        // Kiểm tra đủ tiền khi thanh toán ngay
        if (daThanhToan && tongTien > 0 && maNcc > 0)
        {
            decimal conLai = await _congNo.SoDuKhaDungAsync();
            if (conLai < tongTien)
            {
                return RedirectToAction(nameof(Them), new { khongDu = Math.Round(tongTien - conLai) });
            }
        }

        await using var giaoDich = await _db.Database.BeginTransactionAsync();
        var phieu = new NhapKho
        {
            MaNcc = maNcc > 0 ? maNcc : null,
            NgayNhap = DateTime.Now,
            NguoiNhap = User.MaNguoiDung(),
            TongTien = tongTien,
            DaThanhToan = daThanhToan,
            GhiChu = string.IsNullOrWhiteSpace(ghiChu) ? null : ghiChu.Trim()
        };
        _db.NhapKhos.Add(phieu);
        await _db.SaveChangesAsync();

        var canhBaoGia = new List<string>();   // sản phẩm có giá bán thấp hơn giá gợi ý theo giá nhập mới
        foreach (var d in cacDong)
        {
            _db.ChiTietNhapKhos.Add(new ChiTietNhapKho { MaNhapKho = phieu.MaNhapKho, MaSanPham = d.MaSP, SoLuong = d.SL, GiaNhap = d.Gia });
            var sp = await _db.SanPhams.FirstOrDefaultAsync(s => s.MaSanPham == d.MaSP);
            if (sp != null)
            {
                sp.TonKho += d.SL;
                if (d.Gia > 0)
                {
                    sp.GiaVon = d.Gia;   // ghi nhận giá nhập gần nhất làm giá vốn (dùng để gợi ý giá bán ở trang sản phẩm)
                    decimal giaGoiY = DichVuGia.GoiYGiaBan(sp.GiaVon, sp.GiamToiDa);
                    if (giaGoiY > 0 && sp.GiaBan < giaGoiY)
                        canhBaoGia.Add($"{sp.TenSanPham} (giá bán {TienIch.DinhDangTien(sp.GiaBan)}, gợi ý {TienIch.DinhDangTien(giaGoiY)})");
                }
            }
        }

        if (tongTien > 0 && maNcc > 0)
        {
            _db.CongNos.Add(new CongNo
            {
                LoaiDoiTuong = HangSo.CongNoNhaCungCap,
                MaNcc = maNcc,
                MaNhapKho = phieu.MaNhapKho,
                LoaiGhiChu = daThanhToan ? HangSo.ThanhToan : HangSo.PhatSinh,
                SoTien = tongTien,
                NgayGhi = DateTime.Now,
                GhiChu = daThanhToan ? "Nhập hàng - thanh toán ngay" : "Nhập hàng chưa thanh toán"
            });
        }
        await _db.SaveChangesAsync();
        await giaoDich.CommitAsync();

        string loiNhacGia = canhBaoGia.Count == 0 ? "" : " 💡 Nên xem lại giá bán để khi giảm tối đa vẫn có lãi: " + string.Join("; ", canhBaoGia) + ".";
        BaoThanhCong($"✅ Đã tạo phiếu nhập #{phieu.MaNhapKho} và cộng tồn kho." + loiNhacGia);
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Lịch sử kho: 30 phiếu nhập gần nhất và 30 đơn xuất kho (đơn đang giao / hoàn thành) gần nhất.</summary>
    public async Task<IActionResult> LichSu()
    {
        var vm = new LichSuKhoVM
        {
            PhieuNhap = await _db.NhapKhos.AsNoTracking().Include(n => n.MaNccNavigation).Include(n => n.ChiTietNhapKhos)
                .OrderByDescending(n => n.NgayNhap).Take(30).ToListAsync()
        };
        var donXuat = await _db.DonHangs.AsNoTracking().Include(d => d.ChiTietDonHangs)
            .Where(d => d.TrangThai == HangSo.DangGiao || d.TrangThai == HangSo.HoanThanh)
            .OrderByDescending(d => d.NgayCapNhat).Take(30).ToListAsync();
        vm.DieuChinh = await _db.DieuChinhKhos.AsNoTracking().Include(d => d.MaSanPhamNavigation)
            .OrderByDescending(d => d.NgayTao).Take(30).ToListAsync();
        vm.DonXuat = donXuat.Select(d => new DongXuatKho
        {
            MaDonHang = d.MaDonHang,
            HoTen = d.HoTen,
            LaKhach = d.MaNguoiDung == null,
            NgayXuat = d.NgayCapNhat,
            TrangThai = d.TrangThai,
            TongSoLuong = d.ChiTietDonHangs.Sum(c => c.SoLuong)
        }).ToList();
        return View(vm);
    }

    // =====================================================================
    // ĐIỀU CHỈNH KHO: hàng hỏng / hết hạn / mất / kiểm kê lệch
    // =====================================================================

    /// <summary>Form lập phiếu điều chỉnh + lịch sử phiếu. maSanPham > 0: chọn sẵn sản phẩm và chỉ xem lịch sử của sản phẩm đó.</summary>
    public async Task<IActionResult> DieuChinh(int maSanPham = 0)
    {
        var lichSu = _db.DieuChinhKhos.AsNoTracking()
            .Include(d => d.MaSanPhamNavigation).Include(d => d.MaNguoiTaoNavigation).AsQueryable();
        if (maSanPham > 0) lichSu = lichSu.Where(d => d.MaSanPham == maSanPham);

        var vm = new DieuChinhKhoVM
        {
            MaSanPhamChon = maSanPham,
            DsSanPham = await _db.SanPhams.AsNoTracking().OrderBy(s => s.TenSanPham).ToListAsync(),
            LichSu = await lichSu.OrderByDescending(d => d.NgayTao).ThenByDescending(d => d.MaDieuChinh).Take(100).ToListAsync()
        };
        return View(vm);
    }

    /// <summary>
    /// Lập phiếu điều chỉnh kho. Quy tắc:
    ///   - Hỏng / hết hạn / mất: chỉ GIẢM kho, không giảm quá tồn kho hiện có.
    ///   - Kiểm kê: được giảm hoặc tăng (đếm thực tế khác số trên máy).
    ///   - Bắt buộc ghi chú. Lưu tồn trước / tồn sau / giá vốn / người lập để đối chiếu.
    ///   - KHÔNG đụng tới tiền: nợ NCC và doanh thu giữ nguyên (hàng đã nhập thì vẫn phải trả tiền).
    /// Giảm xong mà kho không đủ cho các đơn "Chờ giao" thì cảnh báo, liệt kê các đơn cần xử lý.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> DieuChinh(int maSanPham, string? loai, string? huong, int soLuong, string? ghiChu)
    {
        ghiChu = (ghiChu ?? "").Trim();
        var cacLoai = new[] { HangSo.DieuChinhHong, HangSo.DieuChinhHetHan, HangSo.DieuChinhMat, HangSo.DieuChinhKiemKe };
        var sp = await _db.SanPhams.FirstOrDefaultAsync(s => s.MaSanPham == maSanPham);

        string? loi = null;
        if (sp == null) loi = "Vui lòng chọn sản phẩm!";
        else if (loai == null || !cacLoai.Contains(loai)) loi = "Vui lòng chọn loại điều chỉnh!";
        else if (soLuong <= 0) loi = "Số lượng phải lớn hơn 0!";
        else if (ghiChu == "") loi = "Vui lòng ghi rõ lý do (ví dụ: \"Gãy 1 cây khi đóng gói đơn #30\")!";

        // Chỉ kiểm kê mới được tăng; các loại còn lại luôn là giảm
        bool laTang = loai == HangSo.DieuChinhKiemKe && huong == "tang";
        if (loi == null && !laTang && soLuong > sp!.TonKho)
            loi = $"Không thể giảm {soLuong}: \"{sp.TenSanPham}\" chỉ còn {sp.TonKho} trong kho.";
        if (loi != null)
        {
            BaoLoi(loi);
            return RedirectToAction(nameof(DieuChinh), new { maSanPham });
        }

        int thayDoi = laTang ? soLuong : -soLuong;
        var phieu = new DieuChinhKho
        {
            MaSanPham = sp!.MaSanPham,
            SoLuong = thayDoi,
            LoaiDieuChinh = loai!,
            GhiChu = ghiChu,
            TonTruoc = sp.TonKho,
            TonSau = sp.TonKho + thayDoi,
            GiaVon = sp.GiaVon,
            MaNguoiTao = User.MaNguoiDung(),
            NgayTao = DateTime.Now
        };
        sp.TonKho = phieu.TonSau;
        _db.DieuChinhKhos.Add(phieu);
        await _db.SaveChangesAsync();

        BaoThanhCong($"✅ Đã lập phiếu điều chỉnh #{phieu.MaDieuChinh}: \"{sp.TenSanPham}\" {phieu.TonTruoc} → {phieu.TonSau} " +
                     $"({HangSo.TenLoaiDieuChinh(phieu.LoaiDieuChinh)}, giá trị {TienIch.DinhDangTien(Math.Abs(phieu.GiaTriHaoHut))}).");

        // Kho sau khi giảm có còn đủ cho các đơn đang chờ giao không?
        var donCho = await _db.ChiTietDonHangs.AsNoTracking()
            .Where(c => c.MaSanPham == sp.MaSanPham && HangSo.TrangThaiChuaGiao.Contains(c.MaDonHangNavigation.TrangThai))
            .Select(c => new { c.MaDonHang, c.SoLuong }).ToListAsync();
        int canGiao = donCho.Sum(c => c.SoLuong);
        if (canGiao > sp.TonKho)
        {
            BaoLoi($"⚠ Kho \"{sp.TenSanPham}\" còn {sp.TonKho} nhưng các đơn chờ giao cần {canGiao}: " +
                   string.Join(", ", donCho.Select(c => "#" + c.MaDonHang).Distinct()) +
                   ". Hãy tạo phiếu nhập thêm hàng, hoặc nhắn khách để hủy đơn (đơn chuyển khoản đã nhận tiền thì hoàn tiền).");
        }
        return RedirectToAction(nameof(DieuChinh), new { maSanPham });
    }

    public async Task<IActionResult> TonKho()
    {
        var ds = await _db.SanPhams.AsNoTracking().Include(s => s.MaThuongHieuNavigation).OrderBy(s => s.TonKho).ToListAsync();
        // Số đang giữ cho đơn chờ giao -> khách chỉ thấy "còn bán được" = tồn kho - đang giữ
        ViewBag.DangGiu = await DichVuSanPham.LaySoDangGiuAsync(_db, ds.Select(s => s.MaSanPham));
        return View(ds);
    }

    /// <summary>Xuất báo cáo tồn kho ra file Excel (.csv), kèm giá nhập và giá trị tồn.</summary>
    [HttpGet]
    public async Task<IActionResult> XuatTonKho()
    {
        var ds = await _db.SanPhams.AsNoTracking().Include(s => s.MaThuongHieuNavigation).OrderBy(s => s.TonKho).ToListAsync();
        return TaoFileCsv($"TonKho_{DateTime.Now:yyyyMMdd_HHmm}.csv",
            new[] { "Mã SP", "Sản phẩm", "Thương hiệu", "Loại", "Tồn kho", "Đã bán", "Giá nhập", "Giá bán", "Giá trị tồn (theo giá nhập)", "Tình trạng" },
            ds.Select(s => new[]
            {
                s.MaSanPham.ToString(), s.TenSanPham, s.MaThuongHieuNavigation.TenThuongHieu, s.LoaiSon,
                s.TonKho.ToString(), s.DaBan.ToString(), ((long)s.GiaVon).ToString(), ((long)s.GiaBan).ToString(),
                ((long)(s.GiaVon * s.TonKho)).ToString(),
                s.TonKho <= 0 ? "Hết hàng" : s.TonKho <= HangSo.NguongSapHet ? "Sắp hết" : "Còn hàng"
            }));
    }
}