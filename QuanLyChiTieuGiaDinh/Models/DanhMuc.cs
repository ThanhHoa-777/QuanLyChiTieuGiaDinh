using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChiTieuGiaDinh.Models
{
    public class DanhMuc
    {
        [Key]
        public int MaDanhMuc { get; set; }

        public int? MaGiaDinh { get; set; }

        // Người khởi tạo danh mục (Dùng để phân quyền BR33 - BR37)
        public int? MaNguoiTao { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên danh mục.")]
        [StringLength(100, ErrorMessage = "Tên danh mục không được vượt quá 100 ký tự.")]
        [Display(Name = "Tên danh mục")]
        public string TenDanhMuc { get; set; } = string.Empty;

        // Tên danh mục đã làm sạch (loại bỏ dấu, chuyển chữ thường) để check trùng BR34
        [StringLength(100)]
        public string TenDanhMucClean { get; set; } = string.Empty;

        // Loại danh mục: "ThuNhap" hoặc "ChiTieu"
        // Không đặt mặc định "ChiTieu" ở đây: nếu form không gửi field này thì phải báo lỗi,
        // tránh việc âm thầm lưu sai loại. Giá trị chọn sẵn cho form đã set ở Create (GET).
        [Required(ErrorMessage = "Vui lòng chọn loại danh mục.")]
        [StringLength(30)]
        [Display(Name = "Loại danh mục")]
        public string LoaiDanhMuc { get; set; } = string.Empty;

        [StringLength(100)]
        [Display(Name = "Biểu tượng")]
        public string? BieuTuong { get; set; }

        [StringLength(20)]
        [Display(Name = "Màu sắc")]
        public string? MauSac { get; set; } = "#6c757d";

        [StringLength(20)]
        [Display(Name = "Trạng thái")]
        public string TrangThai { get; set; } = "Active";

        // =========================================================
        // QUAN HỆ NAVIGATIONS
        // =========================================================

        [ForeignKey(nameof(MaGiaDinh))]
        public virtual HoGiaDinh? HoGiaDinh { get; set; }

        [ForeignKey(nameof(MaNguoiTao))]
        public virtual NguoiDung? NguoiTao { get; set; }

        public virtual ICollection<GiaoDich> GiaoDichs { get; set; } = new List<GiaoDich>();
    }
}