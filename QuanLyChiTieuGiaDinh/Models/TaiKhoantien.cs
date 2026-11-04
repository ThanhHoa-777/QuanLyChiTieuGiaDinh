using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChiTieuGiaDinh.Models
{
    public class TaiKhoanTien
    {
        [Key]
        public int MaTaiKhoan { get; set; }

        public int MaGiaDinh { get; set; }

        // MaNguoiSoHuu = null nếu đây là Quỹ gia đình chung (BR17, BR18)
        public int? MaNguoiSoHuu { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên tài khoản.")]
        [StringLength(100, ErrorMessage = "Tên tài khoản không được vượt quá 100 ký tự.")]
        [Display(Name = "Tên tài khoản")]
        public string TenTaiKhoan { get; set; } = string.Empty;

        [Required]
        [StringLength(30)]
        [Display(Name = "Hình thức tài khoản")]
        public string LoaiTaiKhoan { get; set; } = "TienMat"; // TienMat, NganHang, ViDienTu...

        // Phân loại ví theo đặc tả: "QuyChung" hoặc "ViCaNhan" (BR17, BR20)
        [Required]
        [StringLength(30)]
        [Display(Name = "Loại ví")]
        public string LoaiVi { get; set; } = "ViCaNhan";

        // Cờ đánh dấu tài khoản gia đình (Quỹ chung)
        [Display(Name = "Là tài khoản gia đình")]
        public bool LaTaiKhoanGiaDinh { get; set; } = false;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Số dư ban đầu")]
        [Range(0, double.MaxValue, ErrorMessage = "Số dư ban đầu phải lớn hơn hoặc bằng 0.")]
        public decimal SoDuBanDau { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Số dư hiện tại")]
        public decimal SoDuHienTai { get; set; }

        [StringLength(500)]
        [Display(Name = "Ghi chú")]
        public string? GhiChu { get; set; }

        [Display(Name = "Trạng thái hoạt động")]
        public bool TrangThai { get; set; } = true;

        // =========================================================
        // QUAN HỆ
        // =========================================================

        [ForeignKey(nameof(MaGiaDinh))]
        public virtual HoGiaDinh? HoGiaDinh { get; set; }

        [ForeignKey(nameof(MaNguoiSoHuu))]
        public virtual NguoiDung? NguoiSoHuu { get; set; }

        public virtual ICollection<GiaoDich> GiaoDichs { get; set; } = new List<GiaoDich>();
    }
}