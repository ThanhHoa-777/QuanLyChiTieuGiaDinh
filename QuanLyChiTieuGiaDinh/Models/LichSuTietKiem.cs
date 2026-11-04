using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyChiTieuGiaDinh.Models
{
    public enum LoaiGiaoDichQuy
    {
        [Display(Name = "Nạp tiền tích lũy")]
        NapTien = 1,

        [Display(Name = "Thanh toán chi tiêu")]
        ThanhToanChi = 2,

        [Display(Name = "Hoàn tiền khi hủy")]
        HoanTien = 3
    }

    [Table("LichSuTietKiem")]
    public class LichSuTietKiem
    {
        [Key]
        public int MaLichSuTietKiem { get; set; }

        public int MaMucTieu { get; set; }

        public int MaNguoiThucHien { get; set; } // Thành viên thực hiện nạp hoặc chi

        public int? MaTaiKhoanNguon { get; set; } // Ví cá nhân thực hiện nạp/nhận hoàn tiền (Nullable khi chi từ Ví Quỹ)

        public int? MaDanhMuc { get; set; } // Danh mục chi (Dùng khi LoaiGiaoDich = ThanhToanChi)

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Số tiền phải lớn hơn 0.")]
        [Display(Name = "Số tiền")]
        public decimal SoTien { get; set; }

        [Display(Name = "Loại giao dịch")]
        public LoaiGiaoDichQuy LoaiGiaoDich { get; set; } = LoaiGiaoDichQuy.NapTien;

        [StringLength(255)]
        [Display(Name = "Ghi chú / Nội dung")]
        public string? GhiChu { get; set; }

        [StringLength(500)]
        [Display(Name = "Ảnh hóa đơn / Biên lai")]
        public string? AnhHoaDonUrl { get; set; }

        [Display(Name = "Ngày thực hiện")]
        public DateTime NgayThucHien { get; set; } = DateTime.Now;

        // Navigation Properties
        [ForeignKey("MaMucTieu")]
        public virtual MucTieuTietKiem? MucTieuTietKiem { get; set; }

        [ForeignKey("MaNguoiThucHien")]
        public virtual NguoiDung? NguoiThucHien { get; set; }

        [ForeignKey("MaTaiKhoanNguon")]
        public virtual TaiKhoanTien? TaiKhoanNguon { get; set; }

        [ForeignKey("MaDanhMuc")]
        public virtual DanhMuc? DanhMuc { get; set; }
    }
}