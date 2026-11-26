using Microsoft.EntityFrameworkCore;
using Shopson.Data;
using Shopson.Models;

namespace Shopson.DichVu;

/// <summary>
/// XỬ LÝ ĐƠN HÀNG - trái tim của cửa hàng (gộp từ thuvien.php, donhang_capnhat.php,
/// donhang_xacnhan_nhan.php, donhang_huy.php, donhang_hoan_tien.php, ...).
///
/// Vòng đời đơn hàng:
///   XAC_NHAN (chờ giao) -> DANG_GIAO -> DA_GIAO_CHO_XAC_NHAN -> HOAN_THANH
///                    \-> HUY                          \-> GIAO_THAT_BAI
///
/// Quy tắc quan trọng:
///   - Tồn kho chỉ bị TRỪ khi admin chuyển sang DANG_GIAO.
///   - DaBan (số đã bán) và điểm tích lũy chỉ được CỘNG khi đơn HOAN_THANH.
///   - Hủy đơn KHÔNG tự hoàn kho; chỉ hoàn kho khi admin bấm "Cập nhật lại kho" sau GIAO_THAT_BAI.
/// </summary>
public class DichVuDonHang
{
    private readonly ShopSonContext _db;
    private readonly DichVuFlashSale _flashSale;
    private readonly DichVuTichDiem _tichDiem;
    private readonly DichVuCongNo _congNo;
    private readonly DichVuEmail _email;

    public DichVuDonHang(ShopSonContext db, DichVuFlashSale flashSale, DichVuTichDiem tichDiem, DichVuCongNo congNo, DichVuEmail email)
    {
        _email = email;
        _db = db;
        _flashSale = flashSale;
        _tichDiem = tichDiem;
        _congNo = congNo;
    }

    // =====================================================================
    // 1. TỰ ĐỘNG CẬP NHẬT TRẠNG THÁI THEO THỜI GIAN
    // =====================================================================

    /// <summary>Gọi mỗi khi mở trang đơn hàng (và định kỳ bởi dịch vụ nền) để đơn tự chuyển trạng thái theo thời gian.</summary>
    public async Task TuDongCapNhatAsync()
    {
        await TuDongHuyDonChuyenKhoanAsync();

        var bayGio = DateTime.Now;

        // ĐANG GIAO -> ĐÃ GIAO CHỜ XÁC NHẬN: sau 3 ngày, nếu khách chưa bấm "Đã nhận hàng"
        var moc3Ngay = bayGio.AddDays(-HangSo.NgaySangDaGiao);
        var dsDangGiao = await _db.DonHangs.Where(d =>
            d.TrangThai == HangSo.DangGiao && !d.KhachXacNhanNhan &&
            d.NgayBatDauGiao != null && d.NgayBatDauGiao <= moc3Ngay).ToListAsync();
        foreach (var don in dsDangGiao)
        {
            don.TrangThai = HangSo.DaGiaoChoXacNhan;
            don.NgayTuDongGiao = bayGio;
            ThongBaoKhach(don, HangSo.EmailDaGiao, $"📦 Đơn hàng #{don.MaDonHang} đã được giao. Nếu đã nhận đủ hàng, bạn hãy bấm \"Đã nhận hàng\" trong mục Đơn hàng. " +
                               $"Có vấn đề thì bấm \"Khiếu nại\" trong {HangSo.NgayDuocKhieuNai} ngày; sau {HangSo.NgayTuHoanThanh} ngày đơn tự hoàn thành.");
        }
        if (dsDangGiao.Count > 0) await _db.SaveChangesAsync();

        // ĐÃ GIAO -> HOÀN THÀNH: sau 2 ngày, nếu khách không khiếu nại
        var moc2Ngay = bayGio.AddDays(-HangSo.NgayTuHoanThanh);
        var dsDaGiao = await _db.DonHangs.Where(d =>
            d.TrangThai == HangSo.DaGiaoChoXacNhan && !d.DaKhieuNai &&
            d.NgayTuDongGiao != null && d.NgayTuDongGiao <= moc2Ngay).ToListAsync();
        foreach (var don in dsDaGiao)
        {
            await HoanThanhDonAsync(don, khachXacNhan: false);
        }
        if (dsDaGiao.Count > 0) await _db.SaveChangesAsync();
    }

    /// <summary>Đơn chuyển khoản quá 24 giờ mà admin chưa xác nhận nhận tiền -> tự hủy và nhắn tin cho khách.</summary>
    private async Task TuDongHuyDonChuyenKhoanAsync()
    {
        var hanChuyenKhoan = DateTime.Now.AddHours(-HangSo.GioChoChuyenKhoan);
        var dsHuy = await _db.DonHangs.Where(d =>
            d.PhuongThucThanhToan == HangSo.ThanhToanChuyenKhoan && !d.DaNhanTien &&
            (d.TrangThai == HangSo.XacNhan || d.TrangThai == HangSo.ChoXacNhan) &&
            d.NgayThanhToanCk != null && d.NgayThanhToanCk <= hanChuyenKhoan).ToListAsync();

        foreach (var don in dsHuy)
        {
            GhiNhanHuy(don, HangSo.HuyBoiHeThong, $"quá {HangSo.GioChoChuyenKhoan} giờ chưa xác nhận chuyển khoản");
            await _flashSale.HoanLuotAsync(don.MaDonHang);
            await TraLuotMaGiamGiaAsync(don);
            ThongBaoKhach(don, HangSo.EmailHuy,
                $"Đơn hàng #{don.MaDonHang} đã bị hủy tự động do quá 24 giờ chưa xác nhận chuyển khoản. " +
                "Nếu bạn ĐÃ chuyển khoản, hãy nhắn tin cho cửa hàng kèm ảnh chụp giao dịch và số tài khoản + tên chủ tài khoản để được hoàn tiền.");
        }
        if (dsHuy.Count > 0) await _db.SaveChangesAsync();
    }

    // =====================================================================
    // 2. TIN NHẮN HỆ THỐNG
    // =====================================================================

    /// <summary>Thêm tin nhắn từ cửa hàng gửi tới khách (chưa lưu, người gọi SaveChanges).</summary>
    public void ThemTinNhanHeThong(int maNguoiDung, string noiDung, string tenNguoiGui = "ShopSon")
    {
        if (maNguoiDung <= 0 || string.IsNullOrWhiteSpace(noiDung)) return;
        _db.TinNhans.Add(new TinNhan
        {
            MaNguoiGui = maNguoiDung,
            TenNguoiGui = tenNguoiGui,
            NoiDung = noiDung,
            NgayGui = DateTime.Now,
            LaAdmin = true,
            DaDoc = false,
            LaThongBao = true   // thông báo hệ thống -> tab "Thông báo", không lẫn vào phần trò chuyện
        });
    }

    /// <summary>
    /// Gửi THÔNG BÁO về đơn hàng cho khách (lưu dạng tin nhắn từ cửa hàng, hiện ở mục Tin Nhắn và số chưa đọc trên tên khách).
    /// Khách vãng lai không có tài khoản nên không nhận được tin nhắn. Chưa lưu, người gọi SaveChanges.
    /// </summary>
    public void ThongBaoKhach(DonHang don, string noiDung)
    {
        if (don.MaNguoiDung != null) ThemTinNhanHeThong(don.MaNguoiDung.Value, noiDung);
    }

    /// <summary>
    /// Như trên + GỬI EMAIL tới email khách điền lúc đặt đơn (DonHang.Email) -> khách vãng lai cũng nhận được.
    /// loaiEmail: HangSo.EmailDaGiao, HangSo.EmailHuy... (dùng làm tiêu đề thư và ghi nhật ký).
    /// Email gửi ngầm, lỗi gửi không làm hỏng việc xử lý đơn.
    /// </summary>
    public void ThongBaoKhach(DonHang don, string loaiEmail, string noiDung)
    {
        ThongBaoKhach(don, noiDung);
        string tieuDe = $"ShopSon - Đơn hàng #{don.MaDonHang}: {HangSo.TieuDeEmail(loaiEmail)}";
        string html = DichVuEmail.KhungThu(tieuDe, $"<p>Chào {System.Net.WebUtility.HtmlEncode(don.HoTen)},</p>" + DichVuEmail.VanBanSangHtml(noiDung));
        _email.GuiNgam(don.Email, tieuDe, html, loaiEmail, don.MaDonHang, don.MaNguoiDung);
    }

    // =====================================================================
    // 3. HOÀN THÀNH ĐƠN
    // =====================================================================

    /// <summary>
    /// Đánh dấu đơn HOÀN THÀNH: ghi ngày hoàn thành, cộng điểm tích lũy cho khách, cộng số đã bán (DaBan) cho sản phẩm.
    /// Dùng chung cho 3 trường hợp: khách bấm "Đã nhận hàng", admin ép hoàn thành, hệ thống tự hoàn thành.
    /// Chưa SaveChanges - người gọi tự lưu.
    /// </summary>
    public async Task HoanThanhDonAsync(DonHang don, bool khachXacNhan)
    {
        if (don.TrangThai == HangSo.HoanThanh) return; // đã hoàn thành rồi, tránh cộng 2 lần

        don.TrangThai = HangSo.HoanThanh;
        don.NgayHoanThanh = DateTime.Now;
        don.KhachXacNhanNhan = khachXacNhan;

        await _tichDiem.CongDiemDonHangAsync(don.MaDonHang);

        var dsChiTiet = await _db.ChiTietDonHangs.Where(c => c.MaDonHang == don.MaDonHang).ToListAsync();
        foreach (var ct in dsChiTiet)
        {
            var sp = await _db.SanPhams.FirstOrDefaultAsync(s => s.MaSanPham == ct.MaSanPham);
            if (sp != null) sp.DaBan += ct.SoLuong;
        }

        int soDiem = HangThanhVien.TinhDiem(dsChiTiet.Sum(c => c.GiaBan * c.SoLuong));
        ThongBaoKhach(don, $"✅ Đơn hàng #{don.MaDonHang} đã hoàn thành. Bạn được cộng {soDiem} điểm tích lũy. " +
                           $"Bạn có thể đánh giá sản phẩm trong {HangSo.NgayDuocDanhGia} ngày (nút \"⭐ Đánh giá\" ở mục Đơn hàng). Cảm ơn bạn đã mua sắm!");
    }

    /// <summary>Khách bấm "Đã nhận hàng" (chỉ khi đơn đang giao hoặc đã giao chờ xác nhận).</summary>
    public async Task<bool> KhachXacNhanNhanHangAsync(DonHang don)
    {
        if (don.TrangThai != HangSo.DangGiao && don.TrangThai != HangSo.DaGiaoChoXacNhan) return false;
        await HoanThanhDonAsync(don, khachXacNhan: true);
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>Admin ép hoàn thành đơn thủ công.</summary>
    public async Task AdminHoanThanhAsync(DonHang don)
    {
        if (don.TrangThai == HangSo.HoanThanh) return;
        await HoanThanhDonAsync(don, khachXacNhan: false);
        await _db.SaveChangesAsync();
    }

    // =====================================================================
    // 4. HỦY ĐƠN
    // =====================================================================

    /// <summary>Đơn bị hủy (khách hủy, admin hủy, tự hủy) thì trả lại 1 lượt dùng cho mã giảm giá của đơn đó.</summary>
    private async Task TraLuotMaGiamGiaAsync(DonHang don)
    {
        if (string.IsNullOrEmpty(don.MaGiamGia)) return;
        var ma = await _db.MaGiamGia.FirstOrDefaultAsync(v => v.MaCode == don.MaGiamGia);
        if (ma != null && ma.SoLanDung > 0) ma.SoLanDung -= 1;
    }

    /// <summary>Đơn chỉ hủy được khi chưa giao hàng.</summary>
    public static bool CoTheHuy(DonHang don) => HangSo.TrangThaiChuaGiao.Contains(don.TrangThai);

    /// <summary>Đơn chuyển khoản mà cửa hàng ĐÃ xác nhận nhận tiền thì khi hủy khách phải nhập số tài khoản để được hoàn tiền.</summary>
    public static bool HuyCanSoTaiKhoan(DonHang don) => don.PhuongThucThanhToan == HangSo.ThanhToanChuyenKhoan && don.DaNhanTien;

    /// <summary>
    /// Khách hủy đơn. COD hoặc chuyển khoản CHƯA được xác nhận tiền: hủy luôn (số tài khoản không bắt buộc).
    /// Chuyển khoản ĐÃ xác nhận tiền: bắt buộc nhập số tài khoản để cửa hàng hoàn tiền.
    /// Trả về thông báo lỗi (null nếu thành công).
    /// </summary>
    /// <summary>
    /// Chuyển đơn sang HỦY và LƯU LÝ DO + THỜI ĐIỂM hủy (hiện ở chi tiết đơn của admin và thẻ đơn của khách).
    /// Lý do lưu dạng "Người hủy: nội dung", ví dụ "Cửa hàng hủy: Hết hàng màu khách chọn".
    /// </summary>
    private static void GhiNhanHuy(DonHang don, string nguoiHuy, string? noiDung)
    {
        noiDung = (noiDung ?? "").Trim();
        string lyDo = noiDung == "" ? nguoiHuy : $"{nguoiHuy}: {noiDung}";
        don.TrangThai = HangSo.Huy;
        don.LyDoHuy = lyDo.Length > 500 ? lyDo.Substring(0, 500) : lyDo;
        don.NgayHuy = DateTime.Now;
    }

    public async Task<string?> KhachHuyDonAsync(DonHang don, string? soTaiKhoan, string? tenChuTaiKhoan, string? lyDo = null)
    {
        if (!CoTheHuy(don)) return "Đơn hàng này không thể hủy nữa.";

        soTaiKhoan = (soTaiKhoan ?? "").Trim();
        tenChuTaiKhoan = (tenChuTaiKhoan ?? "").Trim();
        if (HuyCanSoTaiKhoan(don) && (soTaiKhoan == "" || tenChuTaiKhoan == ""))
        {
            return "Vui lòng nhập số tài khoản và tên chủ tài khoản để cửa hàng hoàn tiền!";
        }

        GhiNhanHuy(don, HangSo.HuyBoiKhach, lyDo);
        don.StkhoanTien = soTaiKhoan;
        don.TenTkhoanTien = tenChuTaiKhoan;
        // Không hoàn kho khi hủy (kho chưa bị trừ vì chưa giao). Chỉ trả lại lượt flash sale.
        await _flashSale.HoanLuotAsync(don.MaDonHang);
        await TraLuotMaGiamGiaAsync(don);
        ThongBaoKhach(don, HangSo.EmailHuy, HuyCanSoTaiKhoan(don)
            ? $"Bạn đã hủy đơn hàng #{don.MaDonHang}. Cửa hàng sẽ hoàn {TienIch.DinhDangTien(don.TongTien)} vào tài khoản {soTaiKhoan} ({tenChuTaiKhoan}) và báo lại cho bạn."
            : $"Bạn đã hủy đơn hàng #{don.MaDonHang}." + (soTaiKhoan != "" ? $" Nếu bạn đã chuyển khoản, cửa hàng sẽ kiểm tra và hoàn tiền vào tài khoản {soTaiKhoan}." : ""));
        await _db.SaveChangesAsync();
        return null;
    }

    /// <summary>Admin hủy đơn (chưa giao) kèm lý do, gửi tin nhắn cho khách.</summary>
    public async Task<bool> AdminHuyDonAsync(DonHang don, string lyDo)
    {
        if (!CoTheHuy(don)) return false;

        GhiNhanHuy(don, HangSo.HuyBoiCuaHang, lyDo);
        don.AdminHuy = true;
        await _flashSale.HoanLuotAsync(don.MaDonHang);
        await TraLuotMaGiamGiaAsync(don);

        string tin;
        if (don.PhuongThucThanhToan == HangSo.ThanhToanChuyenKhoan)
        {
            tin = $"Đơn hàng #{don.MaDonHang} của bạn đã bị hủy.\nLý do: {lyDo}\n" +
                  "Nếu bạn đã chuyển khoản, vui lòng gửi số tài khoản ngân hàng để cửa hàng hoàn tiền. Xin lỗi vì sự bất tiện này!";
        }
        else
        {
            tin = $"Đơn hàng #{don.MaDonHang} của bạn đã bị hủy.\nLý do: {lyDo}\nXin lỗi vì sự bất tiện này!";
        }
        ThongBaoKhach(don, HangSo.EmailHuy, tin);
        await _db.SaveChangesAsync();
        return true;
    }

    // =====================================================================
    // 5. GIAO HÀNG
    // =====================================================================

    /// <summary>Được giao hàng khi đơn chưa giao VÀ (COD, hoặc chuyển khoản đã được xác nhận nhận tiền).</summary>
    public static bool CoTheGiao(DonHang don) =>
        HangSo.TrangThaiChuaGiao.Contains(don.TrangThai)
        && (don.PhuongThucThanhToan != HangSo.ThanhToanChuyenKhoan || don.DaNhanTien);

    /// <summary>
    /// Admin bấm "Giao hàng": chuyển ĐANG GIAO, ghi ngày bắt đầu giao và TRỪ TỒN KHO từng sản phẩm.
    /// Sản phẩm nào KHÔNG ĐỦ tồn kho thì KHÔNG cho giao (giữ nguyên "Chờ giao", không trừ gì),
    /// để tránh giao hàng mà kho không giảm rồi sau đó "Cập nhật lại kho" cộng dư.
    /// Trả về danh sách lỗi: rỗng = giao thành công.
    /// </summary>
    public async Task<List<string>> ChuyenDangGiaoAsync(DonHang don)
    {
        var loi = new List<string>();
        if (!CoTheGiao(don)) { loi.Add("Đơn này chưa thể giao."); return loi; }

        var dsChiTiet = await _db.ChiTietDonHangs.Where(c => c.MaDonHang == don.MaDonHang).ToListAsync();

        // Bước 1: kiểm tra đủ hàng cho TẤT CẢ sản phẩm trước (cộng dồn nếu 1 sản phẩm có 2 dòng: giá flash + giá thường)
        var dsSanPham = new Dictionary<int, SanPham>();
        foreach (var nhom in dsChiTiet.GroupBy(c => c.MaSanPham))
        {
            var sp = await _db.SanPhams.FirstOrDefaultAsync(s => s.MaSanPham == nhom.Key);
            int canGiao = nhom.Sum(c => c.SoLuong);
            if (sp == null) { loi.Add($"Sản phẩm mã {nhom.Key} không còn tồn tại."); continue; }
            if (sp.TonKho < canGiao) loi.Add($"\"{sp.TenSanPham}\" chỉ còn {sp.TonKho} nhưng đơn cần {canGiao}.");
            dsSanPham[nhom.Key] = sp;
        }
        if (loi.Count > 0) return loi;   // thiếu hàng: không trừ kho, không đổi trạng thái

        // Bước 2: đủ hết mới trừ kho
        foreach (var ct in dsChiTiet) dsSanPham[ct.MaSanPham].TonKho -= ct.SoLuong;

        don.TrangThai = HangSo.DangGiao;
        don.NgayBatDauGiao = DateTime.Now;
        ThongBaoKhach(don, HangSo.EmailDangGiao, $"🚚 Đơn hàng #{don.MaDonHang} đang được giao đến bạn. Khi nhận được hàng, bạn hãy bấm \"Đã nhận hàng\" trong mục Đơn hàng.");
        await _db.SaveChangesAsync();
        return loi;
    }

    /// <summary>
    /// Giao hàng thất bại: không tính doanh thu, không cộng điểm.
    /// Đơn chuyển khoản đã nhận tiền: CHƯA trừ công nợ ở đây (tiền vẫn đang nằm ở shop).
    /// Chỉ khi admin thật sự chuyển trả và bấm "Đã hoàn tiền cho khách" thì mới ghi hoàn tiền.
    /// </summary>
    public async Task GiaoThatBaiAsync(DonHang don)
    {
        if (don.TrangThai == HangSo.HoanThanh || don.TrangThai == HangSo.Huy) return;

        don.TrangThai = HangSo.GiaoThatBai;
        bool canHoanTien = don.PhuongThucThanhToan == HangSo.ThanhToanChuyenKhoan && don.DaNhanTien && !don.DaHoanTien;
        ThongBaoKhach(don, HangSo.EmailGiaoThatBai, $"❌ Đơn hàng #{don.MaDonHang} giao không thành công." +
            (canHoanTien ? $" Cửa hàng sẽ hoàn lại {TienIch.DinhDangTien(don.TongTien)} cho bạn; nếu chưa gửi số tài khoản, bạn hãy nhắn số tài khoản và tên chủ tài khoản ở tab \"💬 Trò chuyện với shop\"." : ""));
        await _db.SaveChangesAsync();
    }

    /// <summary>Khách trả hàng về cửa hàng sau giao thất bại -> admin bấm "Cập nhật lại kho" để cộng tồn kho lại (chỉ 1 lần).</summary>
    public async Task<bool> CapNhatLaiKhoAsync(DonHang don)
    {
        if (don.TrangThai != HangSo.GiaoThatBai || don.DaCapNhatKho) return false;

        var dsChiTiet = await _db.ChiTietDonHangs.Where(c => c.MaDonHang == don.MaDonHang).ToListAsync();
        foreach (var ct in dsChiTiet)
        {
            var sp = await _db.SanPhams.FirstOrDefaultAsync(s => s.MaSanPham == ct.MaSanPham);
            if (sp != null) sp.TonKho += ct.SoLuong;
        }
        don.DaCapNhatKho = true;
        await _db.SaveChangesAsync();
        return true;
    }

    // =====================================================================
    // 6. TIỀN CHUYỂN KHOẢN
    // =====================================================================

    /// <summary>Admin xác nhận đã nhận tiền chuyển khoản -> ghi công nợ khách (nếu đơn còn đang xử lý).</summary>
    public async Task<bool> XacNhanNhanTienAsync(DonHang don)
    {
        if (don.PhuongThucThanhToan != HangSo.ThanhToanChuyenKhoan || don.DaNhanTien || don.TrangThai == HangSo.Huy) return false;

        don.DaNhanTien = true;
        don.DaGhiCongNo = true;

        var daKetThuc = new[] { HangSo.HoanThanh, HangSo.GiaoThatBai, HangSo.Huy };
        if (!daKetThuc.Contains(don.TrangThai))
        {
            await _congNo.GhiCongNoKhachAsync(don);
        }
        ThongBaoKhach(don, HangSo.EmailNhanTien, $"💰 Cửa hàng đã nhận được {TienIch.DinhDangTien(don.TongTien)} chuyển khoản cho đơn hàng #{don.MaDonHang}. Đơn sẽ sớm được giao.");
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Đơn CK đã HỦY (tự hủy sau 24 giờ hoặc admin hủy) khi shop CHƯA bấm nhận tiền,
    /// nhưng thực tế khách đã chuyển -> hiện nút "Đã nhận tiền (sau khi hủy)" để ghi nhận rồi hoàn tiền.
    /// </summary>
    public static bool CoTheNhanTienSauHuy(DonHang don) =>
        don.PhuongThucThanhToan == HangSo.ThanhToanChuyenKhoan && don.TrangThai == HangSo.Huy
        && !don.DaNhanTien && !don.DaHoanTien;

    /// <summary>
    /// Ghi nhận tiền khách đã chuyển cho đơn ĐÃ HỦY. Đơn vẫn giữ trạng thái Hủy (không khôi phục vì lượt flash sale,
    /// lượt mã đã trả lại và giá có thể đã đổi), doanh thu không đổi.
    /// Ghi công nợ khách (shop đang giữ tiền) -> đơn hiện ở mục "Chưa hoàn tiền" và có nút hoàn tiền như các đơn hủy khác.
    /// </summary>
    public async Task<bool> XacNhanNhanTienSauHuyAsync(DonHang don)
    {
        if (!CoTheNhanTienSauHuy(don)) return false;

        don.DaNhanTien = true;
        don.DaGhiCongNo = true;
        await _congNo.GhiCongNoKhachAsync(don);   // khách vãng lai (không có tài khoản) thì hàm này tự bỏ qua
        ThongBaoKhach(don, HangSo.EmailNhanTien,
            $"💰 Cửa hàng đã kiểm tra và nhận được {TienIch.DinhDangTien(don.TongTien)} bạn chuyển cho đơn hàng #{don.MaDonHang} (đơn đã bị hủy). " +
            "Vui lòng nhắn tin số tài khoản và tên chủ tài khoản để cửa hàng hoàn lại tiền. Nếu vẫn muốn mua, bạn hãy đặt đơn mới.");
        await _db.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Đơn đã HỦY, shop còn giữ tiền chuyển khoản nhưng CHƯA có số tài khoản khách
    /// (thường do admin hủy thay khách) -> trang chi tiết hiện 2 ô để admin nhập STK khách gửi qua tin nhắn.
    /// </summary>
    public static bool CanNhapTaiKhoanHoan(DonHang don) =>
        don.PhuongThucThanhToan == HangSo.ThanhToanChuyenKhoan && don.DaNhanTien && !don.DaHoanTien
        && don.TrangThai == HangSo.Huy && string.IsNullOrWhiteSpace(don.StkhoanTien);

    /// <summary>Điều kiện để hiện nút "Hoàn tiền" trên trang chi tiết đơn của admin.</summary>
    public static bool CoTheHoanTien(DonHang don)
    {
        if (don.PhuongThucThanhToan != HangSo.ThanhToanChuyenKhoan || !don.DaNhanTien || don.DaHoanTien) return false;
        if (don.TrangThai == HangSo.GiaoThatBai) return true;
        if (don.TrangThai == HangSo.Huy) return !string.IsNullOrWhiteSpace(don.StkhoanTien); // đơn hủy: phải có STK khách để chuyển
        if (don.TrangThai == HangSo.DaGiaoChoXacNhan) return don.DaKhieuNai;               // đã giao: phải có khiếu nại
        return false;
    }

    /// <summary>
    /// Admin xác nhận đã hoàn tiền cho khách (chỉ đơn chuyển khoản).
    /// Đơn đã bắt đầu giao -> GIAO_THAT_BAI, chưa giao -> HUY. Luôn trừ công nợ khách.
    /// </summary>
    public async Task<bool> HoanTienChuyenKhoanAsync(DonHang don)
    {
        if (!CoTheHoanTien(don)) return false;

        if (don.TrangThai != HangSo.GiaoThatBai)
        {
            don.TrangThai = don.NgayBatDauGiao != null ? HangSo.GiaoThatBai : HangSo.Huy;
        }
        don.DaHoanTien = true;

        if (don.MaNguoiDung != null && don.DaNhanTien)
        {
            await _congNo.TruCongNoKhachAsync(don);
        }
        string taiKhoan = string.IsNullOrWhiteSpace(don.StkhoanTien) ? "" : $" vào tài khoản {don.StkhoanTien}";
        ThongBaoKhach(don, HangSo.EmailHoanTien, $"💸 Cửa hàng đã hoàn {TienIch.DinhDangTien(don.TongTien)} cho đơn hàng #{don.MaDonHang}{taiKhoan}. " +
                           "Nếu chưa nhận được tiền, bạn hãy nhắn ở tab \"💬 Trò chuyện với shop\" để cửa hàng kiểm tra.");
        await _db.SaveChangesAsync();
        return true;
    }

    // =====================================================================
    // 7. KHIẾU NẠI
    // =====================================================================

    /// <summary>Khách được khiếu nại khi đơn "đã giao chờ xác nhận", chưa khiếu nại, chưa bấm nhận hàng và còn trong 2 ngày.</summary>
    public static bool CoTheKhieuNai(DonHang don, out int giayConLai)
    {
        giayConLai = 0;
        if (don.TrangThai != HangSo.DaGiaoChoXacNhan || don.KhachXacNhanNhan || don.DaKhieuNai || don.NgayTuDongGiao == null)
            return false;
        giayConLai = (int)(don.NgayTuDongGiao.Value.AddDays(HangSo.NgayDuocKhieuNai) - DateTime.Now).TotalSeconds;
        return giayConLai > 0;
    }

    /// <summary>Khách gửi khiếu nại (có thể kèm 1 ảnh minh chứng: đường dẫn ảnh đã lưu, rỗng = không có ảnh).</summary>
    public async Task<bool> GuiKhieuNaiAsync(DonHang don, string noiDung, string anhKhieuNai = "")
    {
        if (!CoTheKhieuNai(don, out _) || string.IsNullOrWhiteSpace(noiDung)) return false;
        don.DaKhieuNai = true;
        don.NgayKhieuNai = DateTime.Now;
        don.NoiDungKhieuNai = noiDung.Trim();
        don.AnhKhieuNai = anhKhieuNai;
        ThongBaoKhach(don, HangSo.EmailKhieuNai, $"📝 Cửa hàng đã nhận khiếu nại của bạn cho đơn hàng #{don.MaDonHang} và sẽ liên hệ trong 24h. " +
                           "Bạn có thể gửi thêm thông tin hoặc hình ảnh ở tab \"💬 Trò chuyện với shop\".");
        await _db.SaveChangesAsync();
        return true;
    }

    // =====================================================================
    // 8. HIỂN THỊ TRẠNG THÁI
    // =====================================================================

    /// <summary>Tên trạng thái phía KHÁCH (gộp bớt: chờ xác nhận = chờ giao; đã giao chờ xác nhận = đang giao).</summary>
    public static (string Ten, string Lop) TrangThaiKhach(string trangThai) => trangThai switch
    {
        HangSo.ChoXacNhan or HangSo.XacNhan => ("Chờ giao hàng", "cho"),
        HangSo.DangGiao or HangSo.DaGiaoChoXacNhan => ("Đang giao", "giao"),
        HangSo.HoanThanh => ("Hoàn thành", "ok"),
        HangSo.Huy => ("Đã hủy", "huy"),
        HangSo.GiaoThatBai => ("Giao hàng thất bại", "that-bai"),
        _ => (trangThai, "cho")
    };

    /// <summary>Tên trạng thái đầy đủ phía ADMIN.</summary>
    public static string TenTrangThaiAdmin(string trangThai) => trangThai switch
    {
        HangSo.ChoXacNhan or HangSo.XacNhan => "Chờ giao hàng",
        HangSo.DangGiao => "Đang giao",
        HangSo.DaGiaoChoXacNhan => "Đã giao — chờ xác nhận",
        HangSo.HoanThanh => "Hoàn thành",
        HangSo.Huy => "Đã hủy",
        HangSo.GiaoThatBai => "Giao hàng thất bại",
        _ => trangThai
    };

    /// <summary>Màu huy hiệu Bootstrap cho trang admin.</summary>
    public static string MauTrangThaiAdmin(string trangThai) => trangThai switch
    {
        HangSo.ChoXacNhan or HangSo.XacNhan => "warning text-dark",
        HangSo.DangGiao => "info text-dark",
        HangSo.DaGiaoChoXacNhan => "warning",
        HangSo.HoanThanh => "success",
        HangSo.Huy => "danger",
        HangSo.GiaoThatBai => "danger",
        _ => "secondary"
    };

    // =====================================================================
    // 9. CẢNH BÁO ĐƠN CẦN CHÚ Ý (hiện ở Tổng quan và Quản lý đơn hàng)
    // =====================================================================

    /// <summary>
    /// Tìm các đơn đang bị "bỏ quên" và gợi ý cách xử lý:
    ///   1) Chờ giao quá 2 ngày (COD, hoặc CK đã nhận tiền) mà chưa bấm Giao hàng.
    ///   2) Có khiếu nại quá 1 ngày mà đơn vẫn chưa kết thúc.
    ///   3) Đơn đã hủy / giao thất bại, khách đã chuyển khoản nhưng shop chưa hoàn tiền.
    /// </summary>
    public async Task<List<DonCanChuY>> LayDonCanChuYAsync()
    {
        var ketQua = new List<DonCanChuY>();
        var mocGiao = DateTime.Now.AddDays(-HangSo.NgayCanhBaoChuaGiao);
        var mocKhieuNai = DateTime.Now.AddDays(-HangSo.NgayCanhBaoKhieuNai);

        // 0) Đơn chuyển khoản chưa nhận tiền, còn dưới 6 giờ là tự hủy
        //    (đặt lúc T thì tự hủy lúc T + 24 giờ -> cảnh báo khi đã qua T + 18 giờ)
        var mocSapHuy = DateTime.Now.AddHours(-(HangSo.GioChoChuyenKhoan - HangSo.GioCanhBaoSapHuyCK));
        var donSapHuy = await _db.DonHangs.AsNoTracking()
            .Where(d => d.PhuongThucThanhToan == HangSo.ThanhToanChuyenKhoan && !d.DaNhanTien
                     && HangSo.TrangThaiChuaGiao.Contains(d.TrangThai)
                     && d.NgayThanhToanCk != null && d.NgayThanhToanCk <= mocSapHuy)
            .OrderBy(d => d.NgayThanhToanCk).ToListAsync();
        foreach (var d in donSapHuy)
        {
            var gioHuy = d.NgayThanhToanCk!.Value.AddHours(HangSo.GioChoChuyenKhoan);
            ketQua.Add(new DonCanChuY
            {
                Don = d,
                VanDe = $"Đơn chuyển khoản sắp tự hủy lúc {gioHuy:HH:mm dd/MM} (chưa xác nhận nhận tiền)",
                GoiY = $"Mở app ngân hàng tìm khoản {TienIch.DinhDangTien(d.TongTien)} nội dung \"Thanh toan don hang {d.MaDonHang}\". " +
                       "Đã nhận: bấm \"Xác nhận đã nhận tiền\". Chưa nhận: có thể nhắn nhắc khách hoặc để hệ thống tự hủy."
            });
        }

        // 1) Chờ giao quá lâu
        var donChuaGiao = await _db.DonHangs.AsNoTracking()
            .Where(d => HangSo.TrangThaiChuaGiao.Contains(d.TrangThai) && d.NgayDat < mocGiao
                     && (d.PhuongThucThanhToan != HangSo.ThanhToanChuyenKhoan || d.DaNhanTien))
            .OrderBy(d => d.NgayDat).ToListAsync();
        foreach (var d in donChuaGiao)
            ketQua.Add(new DonCanChuY
            {
                Don = d,
                VanDe = $"Chờ giao đã {(int)(DateTime.Now - d.NgayDat).TotalDays} ngày",
                GoiY = "Còn hàng: bấm \"Giao hàng\" ngay. Hết hàng: tạo phiếu nhập rồi giao, hoặc nhắn khách hẹn ngày giao / đề nghị hủy" +
                       (d.DaNhanTien ? " (đơn CK đã nhận tiền: hủy xong nhớ bấm \"Đã hoàn tiền\")." : ".")
            });

        // 2) Khiếu nại chưa xử lý
        var donKhieuNai = await _db.DonHangs.AsNoTracking()
            .Where(d => d.DaKhieuNai && d.NgayKhieuNai < mocKhieuNai
                     && d.TrangThai != HangSo.HoanThanh && d.TrangThai != HangSo.GiaoThatBai)
            .OrderBy(d => d.NgayKhieuNai).ToListAsync();
        foreach (var d in donKhieuNai)
            ketQua.Add(new DonCanChuY
            {
                Don = d,
                VanDe = "Khiếu nại quá 1 ngày chưa xử lý",
                GoiY = "Nhắn tin với khách để thỏa thuận. Khách đồng ý nhận hàng: bấm \"Hoàn thành đơn\". " +
                       "Hàng lỗi / khách trả hàng: bấm \"Giao thất bại\" → \"Cập nhật lại kho\" → hoàn tiền nếu đơn CK."
            });

        // 3) Chưa hoàn tiền chuyển khoản
        var donChuaHoan = await _db.DonHangs.AsNoTracking()
            .Where(d => d.PhuongThucThanhToan == HangSo.ThanhToanChuyenKhoan && d.DaNhanTien && !d.DaHoanTien
                     && (d.TrangThai == HangSo.Huy || d.TrangThai == HangSo.GiaoThatBai))
            .OrderBy(d => d.NgayCapNhat).ToListAsync();
        foreach (var d in donChuaHoan)
            ketQua.Add(new DonCanChuY
            {
                Don = d,
                VanDe = "Chưa hoàn tiền chuyển khoản cho khách",
                GoiY = "Chuyển khoản lại " + TienIch.DinhDangTien(d.TongTien) +
                       (string.IsNullOrEmpty(d.StkhoanTien) ? " (khách chưa để lại số tài khoản: nhắn tin hỏi khách)" : " vào TK " + d.StkhoanTien) +
                       " rồi bấm \"Đã hoàn tiền\"."
            });
        return ketQua;
    }
}

/// <summary>Một đơn cần admin chú ý: vấn đề gì và nên làm gì.</summary>
public class DonCanChuY
{
    public DonHang Don { get; set; } = null!;
    public string VanDe { get; set; } = "";
    public string GoiY { get; set; } = "";
}
