using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChiTieuGiaDinh.Models
{
    public class GiaoDich
    {
        [Key]
        public int MaGiaoDich { get; set; }

        // =========================================================
        // TÀI KHOẢN SỬ DỤNG
        // =========================================================

        public int MaTaiKhoan { get; set; }

        // =========================================================
        // DANH MỤC
        // =========================================================

        public int MaDanhMuc { get; set; }

        // =========================================================
        // NGƯỜI THỰC HIỆN / NGƯỜI TẠO GIAO DỊCH
        // =========================================================

        public int MaNguoiThucHien { get; set; }

        // =========================================================
        // SỐ TIỀN
        // =========================================================

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Range(
            0.01,
            double.MaxValue,
            ErrorMessage = "Số tiền phải lớn hơn 0."
        )]
        [Display(Name = "Số tiền")]
        public decimal SoTien { get; set; }

        // =========================================================
        // LOẠI GIAO DỊCH
        // =========================================================

        [Required]
        [StringLength(20)]
        [Display(Name = "Loại giao dịch")]
        public string LoaiGiaoDich { get; set; } = "Chi";

        // =========================================================
        // NGÀY GIAO DỊCH
        // =========================================================

        [Display(Name = "Ngày giao dịch")]
        public DateTime NgayGiaoDich { get; set; } = DateTime.Now;

        // =========================================================
        // GHI CHÚ
        // =========================================================

        [StringLength(500)]
        [Display(Name = "Ghi chú")]
        public string? GhiChu { get; set; }

        // =========================================================
        // ẢNH HÓA ĐƠN
        // =========================================================

        [StringLength(500)]
        [Display(Name = "Ảnh hóa đơn")]
        public string? AnhHoaDon { get; set; }

        


       

        public int? MaDinhKy { get; set; }

        // =========================================================
        // TRẠNG THÁI GIAO DỊCH
        // =========================================================
        // HoatDong : Giao dịch thực tế đang hoạt động
        // DaXoa    : Đã bị xóa mềm (Soft Delete - BR31)
        // =========================================================

        [Required]
        [StringLength(20)]
        [Display(Name = "Trạng thái")]
        public string TrangThai { get; set; } = "HoatDong";

        // =========================================================
        // QUAN HỆ NAVIGATIONS
        // =========================================================

        [ForeignKey(nameof(MaTaiKhoan))]
        public virtual TaiKhoanTien? TaiKhoanTien { get; set; }

        [ForeignKey(nameof(MaDanhMuc))]
        public virtual DanhMuc? DanhMuc { get; set; }

        [ForeignKey(nameof(MaNguoiThucHien))]
        public virtual NguoiDung? NguoiThucHien { get; set; }

        [ForeignKey(nameof(MaDinhKy))]
        public virtual GiaoDichDinhKy? GiaoDichDinhKy { get; set; }
    }
}