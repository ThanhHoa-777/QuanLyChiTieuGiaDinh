namespace Shopson.DichVu;

/// <summary>
/// Các con số cấu hình của cửa hàng (giống các biến cố định trong code PHP).
/// Muốn đổi luật chơi chỉ cần sửa ở đây một chỗ.
/// </summary>
public static class HangSo
{
    // ----- Flash sale -----
    public const int LuotFlashToiDa = 20;      // mỗi sản phẩm chỉ bán tối đa 20 lượt flash sale / ngày
    public const int FlashToiDaMoiKhach = 3;   // mỗi khách chỉ mua tối đa 3 sản phẩm flash / 1 mã sản phẩm / ngày (tính chung tài khoản + SĐT)
    public const int SoSanPhamFlashMoiNgay = 6; // mỗi ngày bốc ngẫu nhiên 6 sản phẩm
    public const int PhanTramFlashMacDinh = 10; // giảm 10%

    // ----- Giảm giá khi bán chậm -----
    // Sau SoThangXetChamBan tháng kể từ ngày bắt đầu bán, nếu số lượng bán ra (đơn đã hoàn thành)
    // chưa đạt TyLeBanMucTieu % so với lượng hàng đang có đầu kỳ thì tự giảm theo % RIÊNG của sản phẩm (SanPham.PhanTramChamBan).
    // Khi bán đủ tỷ lệ thì giá tự quay về bình thường. Chủ shop có thể tắt cho từng sản phẩm.
    public const int SoThangXetChamBan = 3;
    public const int TyLeBanMucTieu = 30;
    public const int PhanTramGiamChamBan = 10;  // chỉ là MẶC ĐỊNH cho sản phẩm mới; admin chỉnh riêng từng sản phẩm ở form sản phẩm
    public const int GiamToiDaMacDinh = 30;    // mỗi sản phẩm mặc định giảm tối đa 30% so với giá bán

    // ----- Gợi ý giá bán cho admin (chỉ gợi ý, admin vẫn tự đổi được) -----
    public const int LoiNhuanToiThieu = 10;    // sau khi giảm TỐI ĐA vẫn còn lãi ít nhất 10% so với giá nhập
    public const int BuocLamTronGia = 1000;    // làm tròn giá gợi ý lên bội của 1.000đ

    // ----- Công nợ nhà cung cấp -----
    public const int SoNgayHanNoNcc = 30;      // hạn trả nợ = ngày nhập + 30 ngày (quá hạn thì tô đỏ, ưu tiên trả trước)
    public const int PhanTramTraToiThieu = 10; // mỗi lần trả ít nhất 10% nợ gốc của phiếu (trừ khi số còn nợ nhỏ hơn)

    // ----- Điểm thưởng khi đánh giá sản phẩm -----
    public const int DiemDanhGia = 20;                    // mỗi lượt đánh giá (1 sản phẩm trong 1 đơn) được +20 điểm
    public const string GhiChuDiemDanhGia = "⭐ Đánh giá"; // đầu ghi chú LichSuDiem của điểm đánh giá (để phân biệt với điểm đơn hàng)

    // ----- Điều chỉnh kho (phiếu DieuChinhKho) -----
    public const string DieuChinhHong = "HONG";
    public const string DieuChinhHetHan = "HET_HAN";
    public const string DieuChinhMat = "MAT";
    public const string DieuChinhKiemKe = "KIEM_KE";   // loại duy nhất được TĂNG kho (đếm thực tế thấy dư)

    /// <summary>Tên hiển thị của từng loại điều chỉnh kho.</summary>
    public static string TenLoaiDieuChinh(string loai) => loai switch
    {
        DieuChinhHong => "🔻 Hỏng / vỡ",
        DieuChinhHetHan => "🔻 Hết hạn",
        DieuChinhMat => "🔻 Mất / thất lạc",
        DieuChinhKiemKe => "⚖ Kiểm kê lệch",
        _ => loai
    };

    // ----- Cảnh báo kho -----
    public const int NguongSapHet = 5;         // tồn kho từ 1 đến 5 -> báo "Chỉ còn X sản phẩm"

    // ----- Vận chuyển -----
    public const decimal NguongMienPhiShip = 500000; // đơn từ 500.000đ miễn phí ship
    public const decimal PhiShipMacDinh = 30000;     // dưới ngưỡng: phí ship 30.000đ

    // ----- Thời gian xử lý đơn hàng tự động -----
    public const int GioChoChuyenKhoan = 24;   // đơn chuyển khoản quá 24 giờ chưa xác nhận tiền -> tự hủy
    public const int NgaySangDaGiao = 3;       // ĐANG GIAO sau 3 ngày -> ĐÃ GIAO CHỜ XÁC NHẬN
    public const int NgayTuHoanThanh = 2;      // ĐÃ GIAO sau 2 ngày không khiếu nại -> HOÀN THÀNH
    public const int NgayDuocDanhGia = 3;      // sau khi hoàn thành, khách có 3 ngày để đánh giá
    public const int NgayDuocKhieuNai = 2;     // khách có 2 ngày để khiếu nại sau khi đơn "đã giao"

    // ----- Phân trang -----
    public const int SanPhamMoiTrang = 10;

    // ----- Danh sách loại son -----
    public static readonly string[] DanhSachLoaiSon = { "Son Thỏi", "Son Kem Lì", "Son Tint", "Son Dưỡng", "Son Bóng" };

    // ----- Trạng thái đơn hàng (dùng tên hằng cho dễ đọc, không gõ chuỗi sai) -----
    // Đơn được XÁC NHẬN TỰ ĐỘNG lúc đặt (giá, kho, mã giảm giá đã kiểm tra ở máy chủ) -> đơn mới luôn là XacNhan ("Chờ giao hàng").
    // ChoXacNhan là giá trị cũ của bản PHP (admin duyệt tay), chỉ giữ để đọc đơn cũ; mọi chỗ xử lý coi nó giống XacNhan
    // (xem TrangThaiChuaGiao). Đơn chuyển khoản "chờ xác nhận TIỀN" dùng cờ DonHang.DaNhanTien, không dùng trạng thái riêng.
    public const string ChoXacNhan = "CHO_XAC_NHAN";
    public const string XacNhan = "XAC_NHAN";
    public const string DangGiao = "DANG_GIAO";
    public const string DaGiaoChoXacNhan = "DA_GIAO_CHO_XAC_NHAN";
    public const string HoanThanh = "HOAN_THANH";
    public const string GiaoThatBai = "GIAO_THAT_BAI";
    public const string Huy = "HUY";

    // ----- Phương thức thanh toán -----
    public const string ThanhToanCOD = "COD";
    public const string ThanhToanChuyenKhoan = "CHUYEN_KHOAN";

    // ----- Loại đối tượng của bảng CongNo (bảng đã gộp công nợ khách + NCC) -----
    public const string CongNoKhachHang = "KHACH_HANG";
    public const string CongNoNhaCungCap = "NCC";

    // ----- Loại ghi chú công nợ -----
    public const string DaNhanTienCK = "DA_NHAN_TIEN_CK";
    public const string HoanTienCK = "HOAN_TIEN_CK";
    public const string PhatSinh = "PHAT_SINH";
    public const string ThanhToan = "THANH_TOAN";

    // ----- Quyền hạn -----
    public const int QuyenQuanTri = 1;
    public const int QuyenKhachHang = 2;

    // Nhóm trạng thái "chưa giao hàng" (khách còn hủy được)
    public static readonly string[] TrangThaiChuaGiao = { ChoXacNhan, XacNhan };

    // Đơn chuyển khoản đã nhận tiền mà ở các trạng thái này (và chưa hoàn tiền) -> shop đang GIỮ tiền của khách (công nợ khách)
    public static readonly string[] TrangThaiGiuTienKhach = { ChoXacNhan, XacNhan, DangGiao, DaGiaoChoXacNhan, Huy, GiaoThatBai };

    // ----- Cảnh báo đơn chậm xử lý (trang admin) -----
    public const int NgayCanhBaoChuaGiao = 2;   // đơn chờ giao quá 2 ngày (tính từ lúc đặt / lúc nhận tiền CK) -> cảnh báo
    public const int NgayCanhBaoKhieuNai = 1;   // khiếu nại quá 1 ngày chưa xử lý -> cảnh báo
    public const int GioCanhBaoSapHuyCK = 6;

    // ----- Lý do hủy đơn (phần đầu của DonHang.LyDoHuy, cho biết AI hủy) -----
    public const string HuyBoiKhach = "Khách hủy";
    public const string HuyBoiCuaHang = "Cửa hàng hủy";
    public const string HuyBoiHeThong = "Hệ thống tự hủy";    // đơn CK chưa nhận tiền còn dưới 6 giờ là tự hủy -> cảnh báo admin kiểm tra ngân hàng

    // ----- Email (loại email ghi vào NhatKyEmail.LoaiEmail) -----
    public const string EmailDatDon = "DAT_DON";
    public const string EmailNhanTien = "NHAN_TIEN";
    public const string EmailDangGiao = "DANG_GIAO";
    public const string EmailDaGiao = "DA_GIAO";
    public const string EmailHuy = "HUY";
    public const string EmailGiaoThatBai = "GIAO_THAT_BAI";
    public const string EmailHoanTien = "HOAN_TIEN";
    public const string EmailKhieuNai = "KHIEU_NAI";
    public const string EmailMaKhoiPhuc = "MA_KHOI_PHUC";
    public const string EmailDoiMatKhau = "DOI_MAT_KHAU";
    public const string EmailDoiEmail = "DOI_EMAIL";

    /// <summary>Tên tiếng Việt của loại email (làm tiêu đề thư và hiện ở trang Nhật ký email).</summary>
    public static string TieuDeEmail(string loai) => loai switch
    {
        EmailDatDon => "Đặt hàng thành công",
        EmailNhanTien => "Đã nhận tiền chuyển khoản",
        EmailDangGiao => "Đang giao hàng",
        EmailDaGiao => "Đã giao hàng",
        EmailHuy => "Đơn hàng đã hủy",
        EmailGiaoThatBai => "Giao hàng không thành công",
        EmailHoanTien => "Đã hoàn tiền",
        EmailKhieuNai => "Đã nhận khiếu nại",
        EmailMaKhoiPhuc => "Mã khôi phục mật khẩu",
        EmailDoiMatKhau => "Mật khẩu đã được đổi",
        EmailDoiEmail => "Email tài khoản đã thay đổi",
        _ => loai
    };

    // ----- Quên mật khẩu (mã 6 số gửi email) -----
    public const int PhutHetHanMaKhoiPhuc = 10;     // mã có hiệu lực 10 phút
    public const int SoLanNhapSaiToiDa = 5;         // nhập sai 5 lần -> mã bị hủy, phải xin mã mới
    public const int GiayChoGuiLaiMa = 60;          // 60 giây mới được gửi lại mã
    public const int SoMaToiDaMoiGio = 5;           // 1 tài khoản tối đa 5 mã / giờ (chống spam hộp thư)
}
