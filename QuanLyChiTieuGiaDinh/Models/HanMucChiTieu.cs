using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChiTieuGiaDinh.Models
{
    public class HanMucChiTieu
    {
        [Key]
        public int MaHanMuc { get; set; }

        [Required]
        public int MaTaiKhoan { get; set; } // Liên kết tới quỹ chung (Tài khoản tiền)

        [Required]
        public int MaThanhVien { get; set; } // Liên kết tới người dùng (thành viên bị giới hạn)

        [Required]
        [Range(0, double.MaxValue, ErrorMessage = "Hạn mức phải lớn hơn hoặc bằng 0")]
        public decimal SoTienHanMuc { get; set; }

        [Required]
        public DateTime NgayBatDau { get; set; } = DateTime.Now;

        [Required]
        public DateTime HanChot { get; set; }

        public bool TrangThai { get; set; } = true; // true: Đang áp dụng, false: Đã vô hiệu hóa

        public bool DaThongBao { get; set; } = false; // Đánh dấu nếu hệ thống đã gửi thông báo hết hạn

        // Navigation Properties (Tùy chọn, giúp EF Core truy xuất dữ liệu dễ hơn)
        [ForeignKey("MaTaiKhoan")]
        public virtual TaiKhoanTien? TaiKhoanTien { get; set; }

        [ForeignKey("MaThanhVien")]
        public virtual NguoiDung? ThanhVien { get; set; }
    }
}