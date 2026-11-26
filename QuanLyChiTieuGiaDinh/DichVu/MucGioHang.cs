namespace Shopson.DichVu;

/// <summary>Một dòng trong giỏ hàng (lưu trong Session dạng JSON, giống $_SESSION['GioHang'] của PHP).</summary>
public class MucGioHang
{
    public int MaSanPham { get; set; }
    public string TenSanPham { get; set; } = "";
    public string ThuongHieu { get; set; } = "";
    public decimal GiaBan { get; set; }          // giá khách phải trả (đã tính flash sale / giảm chậm bán nếu có)
    public decimal GiaTruocGiam { get; set; }    // giá thường (trước khi giảm) để gạch ngang khi đang giảm
    public string HinhAnh { get; set; } = "";
    public int SoLuong { get; set; } = 1;
    public bool LaFlashSale { get; set; }
    public string NgayFlashSale { get; set; } = ""; // dạng yyyy-MM-dd, rỗng nếu không flash sale
    public int PhanTramFlash { get; set; }
    public bool HetLuotFlash { get; set; }       // có flash sale hôm nay nhưng đã bán đủ 20 lượt
    public bool LaGiamChamBan { get; set; }      // đang được giảm vì bán chậm
    public int TonKho { get; set; }              // SỐ CÒN BÁN ĐƯỢC (tồn kho - hàng giữ cho đơn chờ giao), cập nhật mỗi lần đồng bộ giá, để chặn mua quá số lượng
    public bool NgungBan { get; set; }           // admin đã ẩn sản phẩm / thương hiệu (vẫn giữ trong giỏ, hiện mờ)

    public decimal ThanhTien => GiaBan * SoLuong;

    /// <summary>Tạo bản sao với số lượng khác (không sửa dòng gốc đang nằm trong giỏ hàng).</summary>
    public MucGioHang SaoChep(int soLuong)
    {
        var banSao = (MucGioHang)MemberwiseClone();
        banSao.SoLuong = soLuong;
        return banSao;
    }
}