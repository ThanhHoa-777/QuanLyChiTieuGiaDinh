using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChiTieuGiaDinh.Models
{
    public enum LoaiMucTieuTietKiem
    {
        [Display(Name = "Cá nhân")]
        CaNhan = 1,

        [Display(Name = "Hộ gia đình")]
        HoGiaDinh = 2,

        [Display(Name = "Nhóm tự do")]
        NhomTuDo = 3
    }

    public enum TrangThaiMucTieu
    {
        [Display(Name = "Đang tích lũy")]
        DangTichLuy = 1,

        [Display(Name = "Đang chi tiêu")]
        DangChiTieu = 2,

        [Display(Name = "Đã hoàn thành")]
        DaHoanThanh = 3,

        [Display(Name = "Đã hủy")]
        DaHuy = 4
    }

    [Table("MucTieuTietKiem")]
    public class MucTieuTietKiem
    {
        [Key]
        public int MaMucTieu { get; set; }

        public int? MaGiaDinh { get; set; } // Nullable nếu là Nhóm Tự do hoặc Cá nhân

        [Required]
        public int MaTruongNhom { get; set; } // Người khởi tạo / Trưởng nhóm

        [Required(ErrorMessage = "Vui lòng nhập tên mục tiêu.")]
        [StringLength(200)]
        [Display(Name = "Tên mục tiêu")]
        public string TenMucTieu { get; set; } = string.Empty;

        [Display(Name = "Loại mục tiêu")]
        public LoaiMucTieuTietKiem LoaiMucTieu { get; set; } = LoaiMucTieuTietKiem.CaNhan;

        [Required(ErrorMessage = "Vui lòng nhập số tiền mục tiêu.")]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Số tiền mục tiêu phải lớn hơn 0.")]
        [Display(Name = "Tổng số tiền cần tích lũy")]
        public decimal SoTienMucTieu { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Số tiền đã tích lũy (Số dư quỹ)")]
        public decimal SoTienHienCo { get; set; } = 0;

        [Display(Name = "Ngày bắt đầu")]
        public DateTime NgayBatDau { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "Vui lòng chọn ngày hạn hoàn thành.")]
        [Display(Name = "Hạn hoàn thành")]
        public DateTime NgayHan { get; set; }

        [StringLength(500)]
        [Display(Name = "Ảnh đại diện mục tiêu")]
        public string? AnhDaiDienUrl { get; set; }

        [Display(Name = "Kích hoạt Ví thanh toán")]
        public bool IsActiveWallet { get; set; } = false; // BR49: Chuyển mục tiêu thành Ví thanh toán

        [Display(Name = "Trạng thái")]
        public TrangThaiMucTieu TrangThai { get; set; } = TrangThaiMucTieu.DangTichLuy;

        // Foreign Keys & Navigation Properties
        [ForeignKey("MaGiaDinh")]
        public virtual HoGiaDinh? HoGiaDinh { get; set; }

        [ForeignKey("MaTruongNhom")]
        public virtual NguoiDung? TruongNhom { get; set; }

        public virtual ICollection<ThanhVienMucTieu> ThanhVienMucTieus { get; set; }
            = new List<ThanhVienMucTieu>();

        public virtual ICollection<LichSuTietKiem> LichSuTietKiems { get; set; }
            = new List<LichSuTietKiem>();
    }
}