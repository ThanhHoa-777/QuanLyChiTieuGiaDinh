using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChiTieuGiaDinh.Models
{
    public class HoGiaDinh
    {
        [Key]
        public int MaGiaDinh { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên gia đình.")]
        [StringLength(150, ErrorMessage = "Tên gia đình không được vượt quá 150 ký tự.")]
        [Display(Name = "Tên gia đình")]
        public string TenGiaDinh { get; set; } = string.Empty;

        // Mã người dùng tạo hộ và giữ vai trò chủ hộ.
        [Required]
        public int MaChuHo { get; set; }

        // Trạng thái hộ gia đình: "HoatDong", "DaGiaiTan"
        [Required]
        [StringLength(30)]
        [Display(Name = "Trạng thái")]
        public string TrangThai { get; set; } = "HoatDong";

        // Thời điểm tạo hộ gia đình.
        [Display(Name = "Ngày tạo")]
        public DateTime NgayTao { get; set; } = DateTime.Now;

        // Thời điểm giải tán hộ gia đình (nếu có)
        [Display(Name = "Ngày giải tán")]
        public DateTime? NgayGiaiTan { get; set; }

        // =====================================================
        // Navigation Properties
        // =====================================================

        // Thông tin người làm chủ hộ.
        [ForeignKey(nameof(MaChuHo))]
        public virtual NguoiDung? ChuHo { get; set; }

        // Danh sách thành viên thuộc hộ gia đình
        public virtual ICollection<ThanhVienGiaDinh> ThanhVienGiaDinhs { get; set; } = new List<ThanhVienGiaDinh>();

        // Danh sách lời mời gia đình
        public virtual ICollection<LoiMoiGiaDinh> LoiMoiGiaDinhs { get; set; } = new List<LoiMoiGiaDinh>();

        // Danh sách yêu cầu rời hộ
        public virtual ICollection<YeuCauRoiHo> YeuCauRoiHos { get; set; } = new List<YeuCauRoiHo>();

        // Lịch sử chuyển quyền chủ hộ
        public virtual ICollection<LichSuChuyenChuHo> LichSuChuyenChuHos { get; set; } = new List<LichSuChuyenChuHo>();
    }
}