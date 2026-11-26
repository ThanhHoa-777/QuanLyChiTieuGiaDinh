namespace Shopson.DichVu;

/// <summary>
/// Dịch vụ chạy nền (tính năng MỚI): cứ 10 phút tự chạy "TuDongCapNhat" một lần
/// để đơn hàng vẫn tự hủy / tự chuyển trạng thái / tự hoàn thành đúng hạn,
/// kể cả khi không có ai mở trang web. 
/// </summary>
public class TuDongCapNhatNen : BackgroundService
{
    private readonly IServiceScopeFactory _phamVi;
    private readonly ILogger<TuDongCapNhatNen> _nhatKy;

    public TuDongCapNhatNen(IServiceScopeFactory phamVi, ILogger<TuDongCapNhatNen> nhatKy)
    {
        _phamVi = phamVi;
        _nhatKy = nhatKy;
    }

    protected override async Task ExecuteAsync(CancellationToken dungLai)
    {
        // Chờ 30 giây cho ứng dụng khởi động xong
        try { await Task.Delay(TimeSpan.FromSeconds(30), dungLai); } catch (TaskCanceledException) { return; }

        while (!dungLai.IsCancellationRequested)
        {
            try
            {
                using var pham = _phamVi.CreateScope();
                var donHang = pham.ServiceProvider.GetRequiredService<DichVuDonHang>();
                await donHang.TuDongCapNhatAsync();

                // Có sản phẩm vừa Flash Sale vừa giảm chậm bán hôm nay thì báo cho khách (mỗi ngày 1 lần)
                var uuDaiKep = pham.ServiceProvider.GetRequiredService<DichVuUuDaiKep>();
                await uuDaiKep.GuiThongBaoNeuCoAsync();
            }
            catch (Exception loi)
            {
                _nhatKy.LogWarning(loi, "Lỗi khi tự động cập nhật đơn hàng");
            }

            try { await Task.Delay(TimeSpan.FromMinutes(10), dungLai); } catch (TaskCanceledException) { return; }
        }
    }
}
