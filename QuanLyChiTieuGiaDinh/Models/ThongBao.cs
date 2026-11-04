using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChiTieuGiaDinh.Models
{
    public enum PhamViThongBao
    {
        [Display(Name = "Cá nhân")]
        CaNhan = 1,

        [Display(Name = "Gia đình")]
        GiaDinh = 2
    }

    public enum LoaiThongBao
    {
        [Display(Name = "Hệ thống")]
        HeThong = 0,

        [Display(Name = "Lời mời")]
        LoiMoi = 1,

        [Display(Name = "Cảnh báo ngân sách")]
        CanhBaoNganSach = 2,

        [Display(Name = "Phê duyệt")]
        PheDuyet = 3,

        [Display(Name = "Chia sẻ chi phí")]
        SplitExpense = 4,

        [Display(Name = "Biến động số dư")]
        BienDongSoDu = 5,

        [Display(Name = "Giao dịch định kỳ")]
        DinhKy = 6,

        [Display(Name = "Yêu cầu thoát hộ")]
        YeuCauThoatHo = 7
    }

    [Table("ThongBaos")]
    public class ThongBao
    {
        [Key]
        public int MaThongBao { get; set; }

        [Required]
        public int MaNguoiDung { get; set; }

        [Display(Name = "Phạm vi")]
        public PhamViThongBao PhamVi { get; set; } = PhamViThongBao.CaNhan; // BR51

        [Display(Name = "Loại thông báo")]
        public LoaiThongBao LoaiThongBao { get; set; } = LoaiThongBao.HeThong;

        [Required]
        [StringLength(200)]
        [Display(Name = "Tiêu đề")]
        public string TieuDe { get; set; } = string.Empty;

        [Required]
        [StringLength(1000)]
        [Display(Name = "Nội dung")]
        public string NoiDung { get; set; } = string.Empty;

        [StringLength(1000)]
        [Display(Name = "Dữ liệu liên kết (JSON)")]
        public string? DuLieuLienKet { get; set; } // Lưu JSON e.g. {"MaHoGiaDinh": 1, "MaMucTieu": 5}

        [Display(Name = "Đã đọc")]
        public bool DaDoc { get; set; } = false;

        [Display(Name = "Đã thao tác xong")]
        public bool IsThaoTacXong { get; set; } = false; // BR53: Tự động ẩn nút sau khi tương tác

        [Display(Name = "Ngày tạo")]
        public DateTime NgayTao { get; set; } = DateTime.Now;

        // Navigation Property
        [ForeignKey("MaNguoiDung")]
        public virtual NguoiDung? NguoiDung { get; set; }
    }
}