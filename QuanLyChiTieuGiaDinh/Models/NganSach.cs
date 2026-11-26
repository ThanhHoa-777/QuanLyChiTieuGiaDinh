using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChiTieuGiaDinh.Models
{
    public enum ChuKyNganSach
    {
        [Display(Name = "Hàng tuần")]
        HangTuan = 0,

        [Display(Name = "Hàng tháng")]
        HangThang = 1,

        [Display(Name = "Hàng năm")]
        HangNam = 2
    }

    public class NganSach
    {
        [Key]
        public int MaNganSach { get; set; }

        [Required(ErrorMessage = "Tên ngân sách không được để trống.")]
        [StringLength(100, ErrorMessage = "Tên ngân sách không quá 100 ký tự.")]
        [Display(Name = "Tên ngân sách")]
        public string TenNganSach { get; set; } = string.Empty;

        // True = Ngân sách Gia đình (Ví Quỹ chung); False = Ngân sách Cá nhân (Ví riêng) [BR38, BR39]
        [Display(Name = "Ngân sách Gia đình")]
        public bool IsGiaDinh { get; set; } = false;

        public int MaGiaDinh { get; set; }

        // ID Người sở hữu / Tạo ngân sách
        [Required]
        public int MaNguoiTao { get; set; }

        // Null = Tất cả danh mục (Ngân sách tổng)
        [Display(Name = "Danh mục chi tiêu")]
        public int? MaDanhMuc { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số tiền hạn mức.")]
        [Column(TypeName = "decimal(18,2)")]
        [Range(1000, double.MaxValue, ErrorMessage = "Hạn mức phải từ 1.000 VNĐ trở lên.")]
        [Display(Name = "Số tiền hạn mức")]
        public decimal SoTienHanMuc { get; set; }

        [Required]
        [Display(Name = "Chu kỳ")]
        public ChuKyNganSach ChuKy { get; set; } = ChuKyNganSach.HangThang;

        [Display(Name = "Ngày bắt đầu chu kỳ")]
        [Range(1, 31, ErrorMessage = "Ngày bắt đầu chu kỳ phải từ 1 đến 31.")]
        public int NgayBatDauChuKy { get; set; } = 1;

        [Required]
        [Range(1, 12, ErrorMessage = "Tháng phải từ 1 đến 12.")]
        [Display(Name = "Tháng")]
        public int Thang { get; set; }

        [Required]
        [Range(2000, 2100, ErrorMessage = "Năm không hợp lệ.")]
        [Display(Name = "Năm")]
        public int Nam { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Ngày bắt đầu")]
        public DateTime NgayBatDau { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Ngày kết thúc")]
        public DateTime NgayKetThuc { get; set; }

        [Display(Name = "Ngưỡng cảnh báo (%)")]
        [Range(1, 100, ErrorMessage = "Ngưỡng cảnh báo phải từ 1% đến 100%.")]
        public int NguongCanhBaoPhanTram { get; set; } = 80;

        [Display(Name = "Trạng thái")]
        public string TrangThai { get; set; } = "Active"; // Active | Deleted

        public DateTime NgayTao { get; set; } = DateTime.Now;

        // =========================================================
        // THUỘC TÍNH TÍNH TOÁN DÙNG CHO VIEW (KHÔNG LƯU TRONG DB)
        // =========================================================
        [NotMapped]
        [Display(Name = "Số tiền đã sử dụng")]
        public decimal SoTienDaSuDung { get; set; } = 0;

        // Navigation Properties
        [ForeignKey("MaGiaDinh")]
        public virtual HoGiaDinh? HoGiaDinh { get; set; }

        [ForeignKey("MaNguoiTao")]
        public virtual NguoiDung? NguoiTao { get; set; }

        [ForeignKey("MaDanhMuc")]
        public virtual DanhMuc? DanhMuc { get; set; }
    }
}