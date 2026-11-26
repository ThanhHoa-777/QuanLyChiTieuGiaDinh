using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shopson.Data;
using Shopson.Models;

namespace Shopson.DichVu;

/// <summary>
/// GIỎ HÀNG (chuyển từ giohang_*.php).
/// - Giỏ hàng lưu trong Session (khách chưa đăng nhập vẫn dùng được).
/// - Khách đã đăng nhập: giỏ được lưu thêm vào bảng GioHang để lần sau đăng nhập vẫn còn.
/// - Giá luôn được tính lại bằng DichVuGia (flash sale, giảm chậm bán, mức giảm tối đa).
/// - Không cho thêm / tăng số lượng vượt quá tồn kho.
/// </summary>
public class DichVuGioHang
{
    private const string KhoaGioHang = "GioHang";
    private const string KhoaMuaNgay = "MuaNgayTam";
    private const string KhoaDaChon = "SPDaChon";
    private const string KhoaGioThanhToan = "GioHangThanhToan";
    private const string KhoaMaGiamGia = "MaGiamGia";
    private const string KhoaVuaThem = "SPVuaThem";
    private const string KhoaDaNapGio = "DaNapGioDb";   // đánh dấu: Session này đã nạp giỏ của tài khoản từ DB

    private readonly IHttpContextAccessor _httpContext;
    private readonly ShopSonContext _db;
    private readonly DichVuGia _gia;

    public DichVuGioHang(IHttpContextAccessor httpContext, ShopSonContext db, DichVuGia gia)
    {
        _httpContext = httpContext;
        _db = db;
        _gia = gia;
    }

    private ISession PhienLamViec => _httpContext.HttpContext!.Session;

    // ---------------------------------------------------------------------
    // Đọc / ghi Session
    // ---------------------------------------------------------------------
    public Dictionary<int, MucGioHang> Lay()
    {
        var json = PhienLamViec.GetString(KhoaGioHang);
        if (string.IsNullOrEmpty(json)) return new Dictionary<int, MucGioHang>();
        return JsonSerializer.Deserialize<Dictionary<int, MucGioHang>>(json) ?? new Dictionary<int, MucGioHang>();
    }

    public void Luu(Dictionary<int, MucGioHang> gioHang)
    {
        PhienLamViec.SetString(KhoaGioHang, JsonSerializer.Serialize(gioHang));
    }

    public int DemSoSanPham() => Lay().Count;

    /// <summary>Tổng số lượng sản phẩm trong giỏ (số hiện trên biểu tượng giỏ hàng).</summary>
    public int TongSoLuong() => Lay().Values.Sum(m => m.SoLuong);

    public static decimal TinhTong(IEnumerable<MucGioHang> dsMuc) => dsMuc.Sum(m => m.GiaBan * m.SoLuong);

    public MucGioHang? LayMuaNgay()
    {
        var json = PhienLamViec.GetString(KhoaMuaNgay);
        return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<MucGioHang>(json);
    }
    public void LuuMuaNgay(MucGioHang? muc)
    {
        if (muc == null) PhienLamViec.Remove(KhoaMuaNgay);
        else PhienLamViec.SetString(KhoaMuaNgay, JsonSerializer.Serialize(muc));
    }

    public List<int> LayDanhSachDaChon()
    {
        var json = PhienLamViec.GetString(KhoaDaChon);
        return string.IsNullOrEmpty(json) ? new List<int>() : JsonSerializer.Deserialize<List<int>>(json) ?? new List<int>();
    }
    public void LuuDanhSachDaChon(List<int>? ds)
    {
        if (ds == null) PhienLamViec.Remove(KhoaDaChon);
        else PhienLamViec.SetString(KhoaDaChon, JsonSerializer.Serialize(ds));
    }

    public Dictionary<int, MucGioHang> LayGioThanhToan()
    {
        var json = PhienLamViec.GetString(KhoaGioThanhToan);
        if (string.IsNullOrEmpty(json)) return new Dictionary<int, MucGioHang>();
        return JsonSerializer.Deserialize<Dictionary<int, MucGioHang>>(json) ?? new Dictionary<int, MucGioHang>();
    }
    public void LuuGioThanhToan(Dictionary<int, MucGioHang>? gio)
    {
        if (gio == null) PhienLamViec.Remove(KhoaGioThanhToan);
        else PhienLamViec.SetString(KhoaGioThanhToan, JsonSerializer.Serialize(gio));
    }

    public int LayVuaThem()
    {
        int ma = PhienLamViec.GetInt32(KhoaVuaThem) ?? 0;
        PhienLamViec.Remove(KhoaVuaThem);
        return ma;
    }
    public void DatVuaThem(int ma) => PhienLamViec.SetInt32(KhoaVuaThem, ma);

    public void XoaMaGiamGia() => PhienLamViec.Remove(KhoaMaGiamGia);

    // ---------------------------------------------------------------------
    // Thêm sản phẩm (có kiểm tra tồn kho ngay từ lúc thêm)
    // ---------------------------------------------------------------------

    /// <summary>Gán giá (đã tính flash sale / giảm chậm bán) từ KetQuaGia vào một dòng giỏ hàng.</summary>
    private static void GanGia(MucGioHang muc, KetQuaGia gia)
    {
        muc.GiaBan = gia.GiaCuoi;
        muc.GiaTruocGiam = gia.GiaThuong;
        muc.LaFlashSale = gia.LaFlash;
        muc.PhanTramFlash = gia.LaFlash ? gia.PhanTramFlash : 0;
        muc.NgayFlashSale = gia.LaFlash ? TienIch.HomNay().ToString("yyyy-MM-dd") : "";
        muc.HetLuotFlash = gia.HetLuotFlash;
        muc.LaGiamChamBan = gia.LaGiamChamBan;
    }

    /// <summary>Tạo một dòng giỏ hàng từ sản phẩm với giá hiện tại. Không tìm thấy sản phẩm thì trả null.</summary>
    public async Task<MucGioHang?> TaoMucAsync(int maSanPham, int soLuong)
    {
        var sp = await _db.SanPhams.AsNoTracking().Include(s => s.MaThuongHieuNavigation)
            .FirstOrDefaultAsync(s => s.MaSanPham == maSanPham);
        if (sp == null) return null;
        return await TaoMucTuSanPhamAsync(sp, soLuong);
    }

    private async Task<MucGioHang> TaoMucTuSanPhamAsync(SanPham sp, int soLuong)
    {
        var muc = new MucGioHang
        {
            MaSanPham = sp.MaSanPham,
            TenSanPham = sp.TenSanPham,
            ThuongHieu = sp.MaThuongHieuNavigation?.TenThuongHieu ?? "",
            HinhAnh = sp.HinhAnh,
            SoLuong = Math.Max(1, soLuong),
            TonKho = await DichVuSanPham.SoConBanAsync(_db, sp)   // số còn bán được, không phải tồn kho trên kệ
        };
        GanGia(muc, await _gia.TinhMotAsync(sp));
        return muc;
    }

    /// <summary>
    /// "Mua ngay": tạo dòng thanh toán riêng (không đụng vào giỏ). Chặn nếu số lượng vượt quá tồn kho.
    /// Trả về (dòng, thông báo lỗi). Có lỗi thì dòng = null.
    /// </summary>
    public async Task<(MucGioHang? Muc, string? Loi)> TaoMucMuaNgayAsync(int maSanPham, int soLuong)
    {
        var sp = await _db.SanPhams.AsNoTracking().Include(s => s.MaThuongHieuNavigation)
            .FirstOrDefaultAsync(s => s.MaSanPham == maSanPham);
        if (sp == null) return (null, "Sản phẩm không tồn tại!");
        if (DichVuSanPham.BiAn(sp)) return (null, "Sản phẩm đã ngừng kinh doanh!");
        int conBan = await DichVuSanPham.SoConBanAsync(_db, sp);
        if (conBan <= 0) return (null, "Sản phẩm đã hết hàng!");
        if (soLuong > conBan) return (null, $"Chỉ còn {conBan} sản phẩm, bạn không thể mua {soLuong}!");
        return (await TaoMucTuSanPhamAsync(sp, soLuong), null);
    }

    /// <summary>Thêm vào giỏ: đã có thì cộng dồn số lượng. Không cho vượt quá tồn kho. Đã đăng nhập thì lưu luôn vào DB.</summary>
    public async Task<KetQuaGio> ThemVaoGioAsync(int maSanPham, int soLuong, int maNguoiDung)
    {
        soLuong = Math.Max(1, soLuong);
        var sp = await _db.SanPhams.AsNoTracking().Include(s => s.MaThuongHieuNavigation)
            .FirstOrDefaultAsync(s => s.MaSanPham == maSanPham);
        if (sp == null) return KetQuaGio.Loi("Sản phẩm không tồn tại!");
        if (DichVuSanPham.BiAn(sp)) return KetQuaGio.Loi("Sản phẩm đã ngừng kinh doanh!", 0, 0);
        int conBan = await DichVuSanPham.SoConBanAsync(_db, sp);   // tồn kho - hàng đang giữ cho đơn chờ giao
        if (conBan <= 0) return KetQuaGio.Loi("Sản phẩm đã hết hàng!", 0, 0);

        var gio = Lay();
        int daCo = gio.TryGetValue(maSanPham, out var cu) ? cu.SoLuong : 0;
        if (daCo + soLuong > conBan)
        {
            int conThem = conBan - daCo;
            string thongBao = conThem <= 0
                ? $"Bạn đã có đủ {daCo} sản phẩm trong giỏ — chỉ còn {conBan}, không thể thêm nữa!"
                : $"Chỉ còn {conBan} sản phẩm (bạn đã có {daCo} trong giỏ), chỉ thêm được tối đa {conThem} nữa!";
            return KetQuaGio.Loi(thongBao, daCo, conBan);
        }

        var muc = await TaoMucTuSanPhamAsync(sp, soLuong);
        if (cu != null)
        {
            muc.SoLuong = cu.SoLuong + soLuong;   // cộng dồn, giá lấy theo giá mới nhất
        }
        gio[maSanPham] = muc;
        Luu(gio);
        DatVuaThem(maSanPham);
        if (maNguoiDung > 0) await LuuMotDongVaoDbAsync(maNguoiDung, muc);

        string loiNhan = "🛍 Đã thêm sản phẩm vào giỏ hàng!";
        int conLai = conBan - muc.SoLuong;
        if (conLai <= HangSo.NguongSapHet) loiNhan += conLai > 0 ? $" (Chỉ còn {conBan} sản phẩm)" : " (Bạn đã lấy hết số hàng còn lại)";
        return KetQuaGio.Ok(loiNhan, muc.SoLuong, conBan, TongSoLuong());
    }

    private async Task LuuMotDongVaoDbAsync(int maNguoiDung, MucGioHang muc)
    {
        var dongCu = await _db.GioHangs.Where(g => g.MaNguoiDung == maNguoiDung && g.MaSanPham == muc.MaSanPham).ToListAsync();
        _db.GioHangs.RemoveRange(dongCu);
        await _db.SaveChangesAsync();                       // xóa dòng cũ trước (tránh đụng ràng buộc UNIQUE)
        _db.GioHangs.Add(TaoDongDb(maNguoiDung, muc));
        await _db.SaveChangesAsync();
    }

    private static GioHang TaoDongDb(int maNguoiDung, MucGioHang muc)
    {
        return new GioHang
        {
            MaNguoiDung = maNguoiDung,
            MaSanPham = muc.MaSanPham,
            GiaBan = muc.GiaBan,
            SoLuong = muc.SoLuong,
            LaFlashSale = muc.LaFlashSale,
            NgayFlashSale = muc.LaFlashSale && DateOnly.TryParse(muc.NgayFlashSale, out var ngay) ? ngay : null,
            PhanTramFlash = muc.PhanTramFlash,
            NgayCapNhat = DateTime.Now   // ghi lại lúc lưu
        };
    }

    // ---------------------------------------------------------------------
    // Xóa / cập nhật
    // ---------------------------------------------------------------------
    public async Task XoaAsync(int maSanPham, int maNguoiDung)
    {
        var gio = Lay();
        gio.Remove(maSanPham);
        Luu(gio);
        if (maNguoiDung > 0)
        {
            var dong = await _db.GioHangs.Where(g => g.MaNguoiDung == maNguoiDung && g.MaSanPham == maSanPham).ToListAsync();
            _db.GioHangs.RemoveRange(dong);
            await _db.SaveChangesAsync();
        }
    }

    /// <summary>
    /// Đổi số lượng của MỘT dòng trong giỏ (khách bấm + / − hoặc gõ số). Tự chặn ở mức 1 và ở tồn kho.
    /// Thông báo (nếu có) cho biết số lượng đã bị chỉnh lại.
    /// </summary>
    public async Task<KetQuaGio> DatSoLuongAsync(int maSanPham, int soLuong, int maNguoiDung)
    {
        var gio = Lay();
        if (!gio.TryGetValue(maSanPham, out var muc)) return KetQuaGio.Loi("Sản phẩm không còn trong giỏ hàng!");

        var sp = await _db.SanPhams.AsNoTracking().Include(s => s.MaThuongHieuNavigation).FirstOrDefaultAsync(s => s.MaSanPham == maSanPham);
        int kho = sp == null ? 0 : await DichVuSanPham.SoConBanAsync(_db, sp);   // số còn bán được
        if (kho <= 0) return KetQuaGio.Loi("Sản phẩm đã hết hàng!", muc.SoLuong, 0);

        string? thongBao = null;
        if (soLuong < 1) soLuong = 1;
        if (soLuong > kho)
        {
            soLuong = kho;
            thongBao = $"Chỉ còn {kho} sản phẩm!";
        }
        muc.SoLuong = soLuong;
        muc.TonKho = kho;
        Luu(gio);
        if (maNguoiDung > 0) await LuuMotDongVaoDbAsync(maNguoiDung, muc);

        var kq = KetQuaGio.Ok(thongBao ?? "", soLuong, kho, TongSoLuong());
        kq.BiChinh = thongBao != null;
        return kq;
    }

    /// <summary>Lưu số lượng từ form giỏ hàng (lúc bấm "Thanh toán"). Số lượng &lt;= 0 thì xóa dòng đó.</summary>
    public async Task CapNhatSoLuongAsync(Dictionary<int, int> soLuongMoi, int maNguoiDung)
    {
        var gio = Lay();
        foreach (var cap in soLuongMoi)
        {
            if (cap.Value <= 0) gio.Remove(cap.Key);
            else if (gio.ContainsKey(cap.Key)) gio[cap.Key].SoLuong = cap.Value;
        }
        Luu(gio);
        if (maNguoiDung > 0) await LuuToanBoVaoDbAsync(maNguoiDung);
    }

    // ---------------------------------------------------------------------
    // Đồng bộ với DB khi đăng nhập / đăng xuất
    // ---------------------------------------------------------------------

    /// <summary>
    /// Đăng nhập xong: nạp giỏ hàng đã lưu trong DB vào Session (cộng dồn nếu trùng sản phẩm).
    /// Nếu lúc còn là khách đã có sản phẩm trong giỏ thì ghi luôn giỏ đã gộp xuống DB (không đợi đến lúc đăng xuất).
    /// Trả về số dòng giỏ lúc còn là khách và số dòng sau khi gộp, để trang đăng nhập hiện thông báo.
    /// </summary>
    public async Task<(int SoDongKhach, int SoDongSau)> NapTuDbAsync(int maNguoiDung)
    {
        var dsDb = await _db.GioHangs.AsNoTracking()
            .Include(g => g.MaSanPhamNavigation).ThenInclude(s => s.MaThuongHieuNavigation)
            .Where(g => g.MaNguoiDung == maNguoiDung).ToListAsync();
        var gio = Lay();
        int soDongKhach = gio.Count;   // giỏ khi còn là khách (trước khi gộp)
        foreach (var dong in dsDb)
        {
            if (gio.TryGetValue(dong.MaSanPham, out var daCo))
            {
                daCo.SoLuong += dong.SoLuong;
            }
            else
            {
                gio[dong.MaSanPham] = new MucGioHang
                {
                    MaSanPham = dong.MaSanPham,
                    TenSanPham = dong.MaSanPhamNavigation.TenSanPham,
                    ThuongHieu = dong.MaSanPhamNavigation.MaThuongHieuNavigation.TenThuongHieu,
                    GiaBan = dong.GiaBan,
                    HinhAnh = dong.MaSanPhamNavigation.HinhAnh,
                    SoLuong = dong.SoLuong,
                    TonKho = dong.MaSanPhamNavigation.TonKho
                    // giá, flash sale... sẽ được tính lại bằng DongBoGiaAsync khi khách mở giỏ hàng
                };
            }
        }
        Luu(gio);
        PhienLamViec.SetInt32(KhoaDaNapGio, maNguoiDung);             // đánh dấu đã nạp, không nạp lại lần nữa
        if (soDongKhach > 0) await LuuToanBoVaoDbAsync(maNguoiDung);   // ghi giỏ đã gộp xuống DB ngay
        return (soDongKhach, gio.Count);
    }

    /// <summary>
    /// Gọi ở MỌI request của khách hàng đã đăng nhập (middleware trong Program.cs).
    /// Cookie đăng nhập còn hiệu lực nhưng Session là Session MỚI (máy chủ khởi động lại, Session hết hạn,
    /// trình duyệt khôi phục phiên cũ...) thì giỏ trong Session đang trống -> nạp lại giỏ từ DB.
    /// Nếu không nạp: khách thấy giỏ trống, và lần bấm "Thanh toán" / đặt hàng kế tiếp sẽ lưu đè giỏ trống xuống DB,
    /// làm MẤT giỏ đã lưu.
    /// </summary>
    public async Task DamBaoDaNapAsync(int maNguoiDung)
    {
        if (maNguoiDung <= 0) return;
        if (PhienLamViec.GetInt32(KhoaDaNapGio) == maNguoiDung) return;   // Session này đã nạp rồi
        await NapTuDbAsync(maNguoiDung);
    }

    /// <summary>Đăng xuất: lưu toàn bộ giỏ trong Session xuống DB (xóa giỏ cũ rồi ghi giỏ mới).</summary>
    public async Task LuuToanBoVaoDbAsync(int maNguoiDung)
    {
        var gio = Lay();
        var dongCu = await _db.GioHangs.Where(g => g.MaNguoiDung == maNguoiDung).ToListAsync();
        _db.GioHangs.RemoveRange(dongCu);
        await _db.SaveChangesAsync();                       // xóa giỏ cũ trước, rồi mới ghi giỏ mới
        foreach (var muc in gio.Values)
        {
            _db.GioHangs.Add(TaoDongDb(maNguoiDung, muc));
        }
        await _db.SaveChangesAsync();
    }

    // ---------------------------------------------------------------------
    // Đồng bộ giá và tồn kho (dùng cho trang giỏ hàng và trang thanh toán)
    // ---------------------------------------------------------------------

    /// <summary>
    /// Tính lại giá từng dòng bằng DichVuGia (giá hiện tại trong DB + flash sale hôm nay + giảm chậm bán + mức giảm tối đa)
    /// và cập nhật tồn kho hiện tại của từng dòng.
    /// </summary>
    public async Task DongBoGiaAsync(Dictionary<int, MucGioHang> gio)
    {
        if (gio.Count == 0) return;
        var dsMa = gio.Keys.ToList();
        var dsSanPham = await _db.SanPhams.AsNoTracking().Include(s => s.MaThuongHieuNavigation).Where(s => dsMa.Contains(s.MaSanPham)).ToListAsync();
        var dsGia = await _gia.TinhNhieuAsync(dsSanPham);
        var dangGiu = await DichVuSanPham.LaySoDangGiuAsync(_db, dsMa);

        foreach (var sp in dsSanPham)
        {
            var muc = gio[sp.MaSanPham];
            GanGia(muc, dsGia[sp.MaSanPham]);
            muc.NgungBan = DichVuSanPham.BiAn(sp);
            // TonKho của dòng giỏ = SỐ CÒN BÁN ĐƯỢC (tồn kho - đang giữ cho đơn chờ giao); bị ẩn thì coi như hết hàng
            muc.TonKho = muc.NgungBan ? 0 : Math.Max(0, sp.TonKho - dangGiu.GetValueOrDefault(sp.MaSanPham));
        }
    }

    /// <summary>
    /// Sau khi đồng bộ: dòng nào vượt tồn kho thì hạ về đúng tồn kho.
    /// Dòng HẾT HÀNG thì GIỮ LẠI trong giỏ (hiện mờ, không chọn được) như Shopee - có hàng lại thì mua tiếp được.
    /// Trả về danh sách câu cảnh báo để hiện cho khách. Người gọi chịu trách nhiệm lưu giỏ.
    /// </summary>
    public static List<string> DieuChinhTheoKho(Dictionary<int, MucGioHang> gio)
    {
        var canhBao = new List<string>();
        foreach (var muc in gio.Values.ToList())
        {
            if (muc.TonKho <= 0) continue;   // hết hàng: giữ nguyên trong giỏ, trang giỏ hàng tự làm mờ
            if (muc.SoLuong > muc.TonKho)
            {
                canhBao.Add($"\"{muc.TenSanPham}\" chỉ còn {muc.TonKho} sản phẩm, số lượng đã được điều chỉnh.");
                muc.SoLuong = muc.TonKho;
            }
        }
        return canhBao;
    }
}

/// <summary>Kết quả của thao tác thêm / đổi số lượng trong giỏ (trả về cho trang web hoặc cho AJAX).</summary>
public class KetQuaGio
{
    public bool ThanhCong { get; set; }
    public string ThongBao { get; set; } = "";
    public int SoLuongTrongGio { get; set; }   // số lượng của chính sản phẩm đó trong giỏ
    public int TonKho { get; set; }
    public int TongSoLuongGio { get; set; }    // tổng số lượng cả giỏ (hiện trên biểu tượng giỏ)
    public bool BiChinh { get; set; }          // số lượng bị chỉnh lại vì vượt kho

    public static KetQuaGio Ok(string thongBao, int soLuong, int tonKho, int tongGio) =>
        new() { ThanhCong = true, ThongBao = thongBao, SoLuongTrongGio = soLuong, TonKho = tonKho, TongSoLuongGio = tongGio };

    public static KetQuaGio Loi(string thongBao, int soLuong = 0, int tonKho = 0) =>
        new() { ThanhCong = false, ThongBao = thongBao, SoLuongTrongGio = soLuong, TonKho = tonKho };
}