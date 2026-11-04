using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChiTieuGiaDinh.Models
{
    public class GiaoDichDinhKy
    {
        [Key]
        public int MaDinhKy { get; set; }

        public int MaTaiKhoan { get; set; }

        public int MaDanhMuc { get; set; }

        public int MaNguoiThucHien { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Số tiền phải lớn hơn 0.")]
        [Display(Name = "Số tiền dự kiến")]
        public decimal SoTien { get; set; }

        [Required]
        [StringLength(20)]
        [Display(Name = "Loại giao dịch")]
        public string LoaiGiaoDich { get; set; } = "Chi";

        // =========================================================
        // CHU KỲ & NGÀY LẮP (GD-01, GD-02)
        // =========================================================
        // ChuKy: "HangThang", "HangNam"
        // NgayLapTrongChuKy: Ngày lặp lại trong tháng (1 - 31)
        // IsBienDong: true = Khoản chi biến động (điện, nước...)
        //             false = Khoản chi cố định (tiền nhà, internet...)
        // =========================================================

        [Required]
        [StringLength(20)]
        [Display(Name = "Chu kỳ")]
        public string ChuKy { get; set; } = "HangThang";

        [Display(Name = "Ngày lặp trong chu kỳ")]
        [Range(1, 31, ErrorMessage = "Ngày lặp phải từ 1 đến 31.")]
        public int NgayLapTrongChuKy { get; set; } = 1;

        [Display(Name = "Số tiền biến động")]
        public bool IsBienDong { get; set; } = false;

        [Display(Name = "Ngày bắt đầu")]
        public DateTime NgayBatDau { get; set; } = DateTime.Now;

        [Display(Name = "Ngày kết thúc")]
        public DateTime? NgayKetThuc { get; set; }

        [Display(Name = "Ngày chạy tiếp theo")]
        public DateTime NgayChayTiepTheo { get; set; }

        [StringLength(500)]
        [Display(Name = "Ghi chú")]
        public string? GhiChu { get; set; }

        // =========================================================
        // TRẠNG THÁI CẤU HÌNH (GD-03)
        // =========================================================
        // HoatDong : Đang hoạt động (Active)
        // TamDung  : Tạm dừng nhắc nhở (Paused)
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

        // Danh sách các giao dịch thực tế đã phát sinh từ cấu hình lặp này (BR30)
        public virtual ICollection<GiaoDich> GiaoDichs { get; set; } = new List<GiaoDich>();
    }
}