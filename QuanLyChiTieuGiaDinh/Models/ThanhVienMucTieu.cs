using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChiTieuGiaDinh.Models
{
    public enum TrangThaiThamGia
    {
        [Display(Name = "Chờ xác nhận")]
        ChoXacNhan = 1,

        [Display(Name = "Đã tham gia")]
        DaThamGia = 2,

        [Display(Name = "Đã từ chối")]
        TuChoi = 3
    }

    [Table("ThanhVienMucTieu")]
    public class ThanhVienMucTieu
    {
        [Key]
        public int MaThanhVienMucTieu { get; set; }

        public int MaMucTieu { get; set; }

        public int MaNguoiDung { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Chỉ tiêu phải đóng góp")]
        public decimal ChiTieuCanDong { get; set; } = 0; // BR46: Phân bổ chỉ tiêu

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Số tiền đã đóng")]
        public decimal SoTienDaDong { get; set; } = 0;

        [Display(Name = "Đã hoàn thành chỉ tiêu")]
        public bool DaHoanThanhChiTieu { get; set; } = false; // BR47: Đạt 100% chỉ tiêu cá nhân

        [Display(Name = "Trạng thái tham gia")]
        public TrangThaiThamGia TrangThai { get; set; } = TrangThaiThamGia.DaThamGia;

        [Display(Name = "Ngày tham gia")]
        public DateTime NgayThamGia { get; set; } = DateTime.Now;

        // Navigation Properties
        [ForeignKey("MaMucTieu")]
        public virtual MucTieuTietKiem? MucTieuTietKiem { get; set; }

        [ForeignKey("MaNguoiDung")]
        public virtual NguoiDung? NguoiDung { get; set; }
    }
}