using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChiTieuGiaDinh.Models
{
    [Table("LoiMoiTietKiem")]
    public class LoiMoiTietKiem
    {
        [Key]
        public int MaLoiMoi { get; set; }

        public int MaMucTieu { get; set; }

        public int MaNguoiMoi { get; set; } // Trưởng nhóm

        [StringLength(150)]
        [Display(Name = "Email người nhận")]
        public string? EmailNhan { get; set; }

        [StringLength(20)]
        [Display(Name = "Số điện thoại nhận")]
        public string? SoDienThoaiNhan { get; set; }

        [StringLength(100)]
        [Display(Name = "Mã token chia sẻ / QR Code")]
        public string TokenMoi { get; set; } = Guid.NewGuid().ToString("N");

        [Display(Name = "Trạng thái lời mời")]
        public TrangThaiThamGia TrangThai { get; set; } = TrangThaiThamGia.ChoXacNhan;

        [Display(Name = "Ngày tạo")]
        public DateTime NgayTao { get; set; } = DateTime.Now;

        [Display(Name = "Ngày hết hạn token")]
        public DateTime NgayHetHan { get; set; } = DateTime.Now.AddDays(7);

        // Navigation Properties
        [ForeignKey("MaMucTieu")]
        public virtual MucTieuTietKiem? MucTieuTietKiem { get; set; }

        [ForeignKey("MaNguoiMoi")]
        public virtual NguoiDung? NguoiMoi { get; set; }
    }
}