using Microsoft.EntityFrameworkCore;
using QuanLyChiTieuGiaDinh.Data;
using QuanLyChiTieuGiaDinh.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace QuanLyChiTieuGiaDinh.Services
{
    public class TuDongLapNganSachService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TuDongLapNganSachService> _logger;

        public TuDongLapNganSachService(IServiceProvider serviceProvider, ILogger<TuDongLapNganSachService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                        var homNay = DateTime.Now;
                        int thangHienTai = homNay.Month;
                        int namHienTai = homNay.Year;

                        // Tìm các ngân sách đang hoạt động (TrangThai == "Active") của tháng trước
                        int thangTruoc = thangHienTai == 1 ? 12 : thangHienTai - 1;
                        int namThangTruoc = thangHienTai == 1 ? namHienTai - 1 : namHienTai;

                        var danhSachNganSachCu = await context.NganSachs
                            .Where(x => x.TrangThai == "Active" && x.Thang == thangTruoc && x.Nam == namThangTruoc)
                            .ToListAsync(stoppingToken);

                        foreach (var nsCu in danhSachNganSachCu)
                        {
                            // Kiểm tra tháng hiện tại đã có ngân sách tương ứng chưa
                            bool daCoThangMoi = await context.NganSachs.AnyAsync(x =>
                                x.MaGiaDinh == nsCu.MaGiaDinh &&
                                x.MaDanhMuc == nsCu.MaDanhMuc &&
                                x.IsGiaDinh == nsCu.IsGiaDinh &&
                                (nsCu.IsGiaDinh || x.MaNguoiTao == nsCu.MaNguoiTao) &&
                                x.Thang == thangHienTai &&
                                x.Nam == namHienTai &&
                                x.TrangThai == "Active", stoppingToken);

                            if (!daCoThangMoi)
                            {
                                var ngayBatDau = new DateTime(namHienTai, thangHienTai, 1);
                                var ngayKetThuc = ngayBatDau.AddMonths(1).AddDays(-1);

                                // Xử lý đổi tên từ T(tháng cũ) sang T(tháng mới)
                                string tenMoi = nsCu.TenNganSach;
                                if (tenMoi.Contains($"T{thangTruoc}"))
                                {
                                    tenMoi = tenMoi.Replace($"T{thangTruoc}", $"T{thangHienTai}");
                                }
                                else
                                {
                                    tenMoi = $"{tenMoi} T{thangHienTai}/{namHienTai}";
                                }

                                var nsMoi = new NganSach
                                {
                                    MaGiaDinh = nsCu.MaGiaDinh,
                                    MaNguoiTao = nsCu.MaNguoiTao,
                                    TenNganSach = tenMoi,
                                    MaDanhMuc = nsCu.MaDanhMuc,
                                    SoTienHanMuc = nsCu.SoTienHanMuc,
                                    ChuKy = nsCu.ChuKy,
                                    NgayBatDauChuKy = nsCu.NgayBatDauChuKy,
                                    Thang = thangHienTai,
                                    Nam = namHienTai,
                                    NgayBatDau = ngayBatDau,
                                    NgayKetThuc = ngayKetThuc,
                                    NguongCanhBaoPhanTram = nsCu.NguongCanhBaoPhanTram,
                                    IsGiaDinh = nsCu.IsGiaDinh,
                                    TrangThai = "Active",
                                    NgayTao = DateTime.Now
                                };

                                context.NganSachs.Add(nsMoi);
                            }
                        }

                        await context.SaveChangesAsync(stoppingToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi xảy ra khi tự động lặp ngân sách.");
                }

                // Chạy kiểm tra định kỳ mỗi 12 tiếng một lần
                await Task.Delay(TimeSpan.FromHours(12), stoppingToken);
            }
        }
    }
}