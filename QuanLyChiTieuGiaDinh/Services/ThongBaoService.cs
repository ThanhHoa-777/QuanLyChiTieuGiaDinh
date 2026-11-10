using Microsoft.EntityFrameworkCore;
using QuanLyChiTieuGiaDinh.Data;
using QuanLyChiTieuGiaDinh.Models;

namespace QuanLyChiTieuGiaDinh.Services
{
    public class ThongBaoService
    {
        private readonly ApplicationDbContext _context;

        public ThongBaoService(ApplicationDbContext context)
        {
            _context = context;
        }

        // =====================================================
        // 1. TẠO THÔNG BÁO CHUẨN (DÙNG ENUM)
        // =====================================================
        public async Task TaoThongBaoAsync(
            int maNguoiDung,
            string tieuDe,
            string noiDung,
            PhamViThongBao phamVi = PhamViThongBao.CaNhan,
            LoaiThongBao loaiThongBao = LoaiThongBao.HeThong,
            string? duLieuLienKet = null)
        {
            var thongBao = new ThongBao
            {
                MaNguoiDung = maNguoiDung,
                TieuDe = tieuDe,
                NoiDung = noiDung,
                PhamVi = phamVi,
                LoaiThongBao = loaiThongBao,
                DuLieuLienKet = duLieuLienKet,
                DaDoc = false,
                IsThaoTacXong = false,
                NgayTao = DateTime.Now
            };

            _context.ThongBaos.Add(thongBao);
            await _context.SaveChangesAsync();
        }

        // =====================================================
        // 2. OVERLOAD CHO CODE CỦ (TRUYỀN STRING)
        // =====================================================
        public async Task TaoThongBaoAsync(
            int maNguoiDung,
            string tieuDe,
            string noiDung,
            string loaiThongBao = "HeThong")
        {
            var loaiEnum = Enum.TryParse<LoaiThongBao>(loaiThongBao, true, out var result)
                ? result
                : LoaiThongBao.HeThong;

            await TaoThongBaoAsync(maNguoiDung, tieuDe, noiDung, PhamViThongBao.CaNhan, loaiEnum, null);
        }

        // =====================================================
        // 2.1 OVERLOAD MỚI (TRUYỀN STRING + PHẠM VI)
        // =====================================================
        public async Task TaoThongBaoAsync(
            int maNguoiDung,
            string tieuDe,
            string noiDung,
            string loaiThongBao,
            PhamViThongBao phamVi)
        {
            var loaiEnum = Enum.TryParse<LoaiThongBao>(loaiThongBao, true, out var result)
                ? result
                : LoaiThongBao.HeThong;

            await TaoThongBaoAsync(maNguoiDung, tieuDe, noiDung, phamVi, loaiEnum, null);
        }

        // =====================================================
        // 3. THÔNG BÁO CHO TOÀN HỘ GIA ĐÌNH
        // =====================================================
        public async Task TaoThongBaoGiaDinhAsync(
            int maGiaDinh,
            string tieuDe,
            string noiDung,
            LoaiThongBao loaiThongBao,
            string? duLieuLienKet = null,
            int? ngoaiTruMaNguoiDung = null)
        {
            var danhSachThanhVien = await _context.ThanhVienGiaDinhs
                .Where(x => x.MaGiaDinh == maGiaDinh)
                .Select(x => x.MaNguoiDung)
                .ToListAsync();

            foreach (var maNguoiDung in danhSachThanhVien)
            {
                if (ngoaiTruMaNguoiDung.HasValue && maNguoiDung == ngoaiTruMaNguoiDung.Value)
                    continue;

                await TaoThongBaoAsync(
                    maNguoiDung,
                    tieuDe,
                    noiDung,
                    PhamViThongBao.GiaDinh,
                    loaiThongBao,
                    duLieuLienKet);
            }
        }
    }
}