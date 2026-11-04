using Microsoft.AspNetCore.Identity; // Đã thêm
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using QuanLyChiTieuGiaDinh.Models;
using QuanLyChiTieuGiaDinh.Data;

namespace QuanLyChiTieuGiaDinh.Data
{
    // ĐÃ SỬA: Chỉ kế thừa IdentityDbContext (không có <ApplicationUser>) để sử dụng IdentityUser mặc định
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // GIỮ NGUYÊN TOÀN BỘ DBSET CỦA BẠN
        public DbSet<NguoiDung> NguoiDungs { get; set; }
        public DbSet<HoGiaDinh> HoGiaDinhs { get; set; }
        public DbSet<ThanhVienGiaDinh> ThanhVienGiaDinhs { get; set; }
        public DbSet<LoiMoiGiaDinh> LoiMoiGiaDinhs { get; set; }
        public DbSet<YeuCauRoiHo> YeuCauRoiHos { get; set; }
        public DbSet<LichSuChuyenChuHo> LichSuChuyenChuHos { get; set; }
        public DbSet<TaiKhoanTien> TaiKhoanTiens { get; set; }
        public DbSet<DanhMuc> DanhMucs { get; set; }
        public DbSet<GiaoDich> GiaoDichs { get; set; }
        public DbSet<NganSach> NganSachs { get; set; }
        public DbSet<LichSuHanMuc> LichSuHanMucs { get; set; }
        public DbSet<GiaoDichDinhKy> GiaoDichDinhKys { get; set; }

        // MỤC TIÊU TIẾT KIỆM (SAVINGS GOALS MODULE)
        public DbSet<MucTieuTietKiem> MucTieuTietKiems { get; set; }
        public DbSet<ThanhVienMucTieu> ThanhVienMucTieus { get; set; }
        public DbSet<LichSuTietKiem> LichSuTietKiems { get; set; }
        public DbSet<LoiMoiTietKiem> LoiMoiTietKiems { get; set; }
        public DbSet<ThongBao> ThongBaos { get; set; }
        public DbSet<NhatKyHeThong> NhatKyHeThongs { get; set; }

        public DbSet<HanMucChiTieu> HanMucChiTieus { get; set; }
        public DbSet<YeuCauHoanTien> YeuCauHoanTiens { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder); // Bắt buộc phải có khi dùng Identity

            // =====================================================
            // INDEXES & UNIQUE CONSTRAINTS
            // =====================================================

            builder.Entity<HoGiaDinh>()
                .HasIndex(x => x.MaChuHo)
                .IsUnique()
                .HasFilter("[TrangThai] = N'HoatDong'");

            builder.Entity<ThanhVienGiaDinh>()
                .HasIndex(x => x.MaNguoiDung)
                .IsUnique()
                .HasFilter("[TrangThai] = N'DaXacNhan'");

            builder.Entity<ThanhVienGiaDinh>()
                .HasIndex(x => new { x.MaGiaDinh, x.MaNguoiDung })
                .IsUnique();

            builder.Entity<DanhMuc>()
                .HasIndex(x => new { x.MaGiaDinh, x.TenDanhMucClean, x.LoaiDanhMuc })
                .HasDatabaseName("IX_DanhMuc_MaGiaDinh_TenClean_Loai");

            // =====================================================
            // HO GIA DINH & THANH VIEN RELATIONSHIPS
            // =====================================================

            builder.Entity<HoGiaDinh>()
                .HasOne(h => h.ChuHo)
                .WithMany() // ĐÃ SỬA: Để trống WithMany() để không bị lỗi nếu ChuHo là IdentityUser
                .HasForeignKey(h => h.MaChuHo)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ThanhVienGiaDinh>()
                .HasOne(t => t.HoGiaDinh)
                .WithMany(h => h.ThanhVienGiaDinhs)
                .HasForeignKey(t => t.MaGiaDinh)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<ThanhVienGiaDinh>()
                .HasOne(t => t.NguoiDung)
                .WithMany() // ĐÃ SỬA: Để trống WithMany() 
                .HasForeignKey(t => t.MaNguoiDung)
                .OnDelete(DeleteBehavior.Restrict);

            // =====================================================
            // HO GIA DINH AUXILIARY ENTITIES
            // =====================================================

            builder.Entity<LoiMoiGiaDinh>(entity =>
            {
                entity.HasOne(l => l.HoGiaDinh)
                    .WithMany(h => h.LoiMoiGiaDinhs)
                    .HasForeignKey(l => l.MaGiaDinh)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(l => l.NguoiGui)
                    .WithMany()
                    .HasForeignKey(l => l.MaNguoiGui)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<YeuCauRoiHo>(entity =>
            {
                entity.HasOne(y => y.HoGiaDinh)
                    .WithMany(h => h.YeuCauRoiHos)
                    .HasForeignKey(y => y.MaGiaDinh)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(y => y.NguoiDung)
                    .WithMany()
                    .HasForeignKey(y => y.MaNguoiDung)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(y => y.NguoiDuyetGiaDinh)
                    .WithMany()
                    .HasForeignKey(y => y.NguoiDuyet)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<LichSuChuyenChuHo>(entity =>
            {
                entity.HasOne(l => l.HoGiaDinh)
                    .WithMany(h => h.LichSuChuyenChuHos)
                    .HasForeignKey(l => l.MaGiaDinh)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(l => l.ChuHoCu)
                    .WithMany()
                    .HasForeignKey(l => l.MaChuHoCu)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(l => l.ChuHoMoi)
                    .WithMany()
                    .HasForeignKey(l => l.MaChuHoMoi)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // =====================================================
            // FINANCIAL & BUDGET ENTITIES
            // =====================================================

            builder.Entity<TaiKhoanTien>()
                .HasOne(t => t.HoGiaDinh)
                .WithMany()
                .HasForeignKey(t => t.MaGiaDinh)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<TaiKhoanTien>()
                .HasOne(t => t.NguoiSoHuu)
                .WithMany()
                .HasForeignKey(t => t.MaNguoiSoHuu)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<DanhMuc>()
                .HasOne(d => d.HoGiaDinh)
                .WithMany()
                .HasForeignKey(d => d.MaGiaDinh)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<DanhMuc>()
                .HasOne(d => d.NguoiTao)
                .WithMany()
                .HasForeignKey(d => d.MaNguoiTao)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<DanhMuc>()
                .HasOne(d => d.DanhMucCha)
                .WithMany(d => d.DanhMucCon)
                .HasForeignKey(d => d.MaDanhMucCha)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<GiaoDich>()
                .HasOne(g => g.TaiKhoanTien)
                .WithMany(g => g.GiaoDichs)
                .HasForeignKey(g => g.MaTaiKhoan)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<GiaoDich>()
                .HasOne(g => g.DanhMuc)
                .WithMany(d => d.GiaoDichs)
                .HasForeignKey(g => g.MaDanhMuc)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<GiaoDich>()
                .HasOne(g => g.NguoiThucHien)
                .WithMany()
                .HasForeignKey(g => g.MaNguoiThucHien)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<GiaoDich>()
                .HasOne(g => g.GiaoDichDinhKy)
                .WithMany(d => d.GiaoDichs)
                .HasForeignKey(g => g.MaDinhKy)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<NganSach>()
                .HasOne(n => n.HoGiaDinh)
                .WithMany()
                .HasForeignKey(n => n.MaGiaDinh)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<NganSach>()
                .HasOne(n => n.DanhMuc)
                .WithMany()
                .HasForeignKey(n => n.MaDanhMuc)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<NganSach>()
                .HasOne(n => n.NguoiTao)
                .WithMany()
                .HasForeignKey(n => n.MaNguoiTao)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<LichSuHanMuc>()
                .HasOne(l => l.NganSach)
                .WithMany()
                .HasForeignKey(l => l.MaNganSach)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<LichSuHanMuc>()
                .HasOne(l => l.NguoiThayDoi)
                .WithMany()
                .HasForeignKey(l => l.MaNguoiThayDoi)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<GiaoDichDinhKy>()
                .HasOne(g => g.TaiKhoanTien)
                .WithMany()
                .HasForeignKey(g => g.MaTaiKhoan)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<GiaoDichDinhKy>()
                .HasOne(g => g.DanhMuc)
                .WithMany()
                .HasForeignKey(g => g.MaDanhMuc)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<GiaoDichDinhKy>()
                .HasOne(g => g.NguoiThucHien)
                .WithMany()
                .HasForeignKey(g => g.MaNguoiThucHien)
                .OnDelete(DeleteBehavior.Restrict);

            // =====================================================
            // SAVINGS GOALS RELATIONSHIPS
            // =====================================================

            builder.Entity<MucTieuTietKiem>()
                .HasOne(m => m.HoGiaDinh)
                .WithMany()
                .HasForeignKey(m => m.MaGiaDinh)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<MucTieuTietKiem>()
                .HasOne(m => m.TruongNhom)
                .WithMany()
                .HasForeignKey(m => m.MaTruongNhom)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ThanhVienMucTieu>(entity =>
            {
                entity.HasOne(t => t.MucTieuTietKiem)
                    .WithMany(m => m.ThanhVienMucTieus)
                    .HasForeignKey(t => t.MaMucTieu)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(t => t.NguoiDung)
                    .WithMany()
                    .HasForeignKey(t => t.MaNguoiDung)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<LichSuTietKiem>(entity =>
            {
                entity.HasOne(l => l.MucTieuTietKiem)
                    .WithMany(m => m.LichSuTietKiems)
                    .HasForeignKey(l => l.MaMucTieu)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(l => l.NguoiThucHien)
                    .WithMany()
                    .HasForeignKey(l => l.MaNguoiThucHien)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(l => l.TaiKhoanNguon)
                    .WithMany()
                    .HasForeignKey(l => l.MaTaiKhoanNguon)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(l => l.DanhMuc)
                    .WithMany()
                    .HasForeignKey(l => l.MaDanhMuc)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<LoiMoiTietKiem>(entity =>
            {
                entity.HasOne(l => l.MucTieuTietKiem)
                    .WithMany()
                    .HasForeignKey(l => l.MaMucTieu)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(l => l.NguoiMoi)
                    .WithMany()
                    .HasForeignKey(l => l.MaNguoiMoi)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // =====================================================
            // NOTIFICATIONS & LOGS
            // =====================================================

            builder.Entity<ThongBao>()
                .HasOne(t => t.NguoiDung)
                .WithMany()
                .HasForeignKey(t => t.MaNguoiDung)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<NhatKyHeThong>()
                .HasOne(n => n.NguoiDung)
                .WithMany()
                .HasForeignKey(n => n.MaNguoiDung)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}