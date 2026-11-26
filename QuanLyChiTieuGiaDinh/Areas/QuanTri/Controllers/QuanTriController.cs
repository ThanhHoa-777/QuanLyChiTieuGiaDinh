using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Shopson.Areas.QuanTri.Controllers;

/// <summary>
/// Lớp cha của mọi controller trong khu quản trị.
/// [Authorize(Roles = "QuanTri")]: chỉ tài khoản Admin (QuyenHan = 1) mới vào được, người khác bị chuyển về trang đăng nhập.
/// </summary>
[Area("QuanTri")]
[Authorize(Roles = "QuanTri")]
public abstract class QuanTriController : Controller
{
    protected void BaoThanhCong(string noiDung) => TempData["ThongBaoOK"] = noiDung;
    protected void BaoLoi(string noiDung) => TempData["ThongBaoLoi"] = noiDung;

    /// <summary>
    /// Tạo file CSV mở được bằng Excel (có BOM UTF-8 nên tiếng Việt không bị lỗi font).
    /// Mỗi ô được bọc trong dấu nháy kép, dấu nháy bên trong được nhân đôi.
    /// </summary>
    protected FileContentResult TaoFileCsv(string tenFile, string[] tieuDe, IEnumerable<string[]> cacDong)
    {
        string Thoat(string? giaTri) => "\"" + GiuDangChu(giaTri ?? "").Replace("\"", "\"\"") + "\"";
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(string.Join(",", tieuDe.Select(Thoat)));
        foreach (var dong in cacDong) sb.AppendLine(string.Join(",", dong.Select(Thoat)));
        var bytes = new System.Text.UTF8Encoding(true).GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray(); //in  BOM UTF-8 để Excel nhận dạng tiếng Việt
        return File(bytes, "text/csv; charset=utf-8", tenFile);
    }

    /// <summary>
    /// Excel tự đổi một số ô CSV sang kiểu khác làm hỏng dữ liệu:
    ///  - số điện thoại "0912345678" thành số 9.12E+08 (mất số 0 đầu);
    ///  - ngày "05/11/2026" bị hiểu nhầm thành 11/05 (máy để kiểu Mỹ), ngày nào hiểu được thành kiểu ngày
    ///    thì hiện ######## khi cột hẹp, ngày nào không hiểu thì thành chữ -> cột lộn xộn.
    /// Cách chữa: ghi ô đó dạng công thức ="giá trị" để Excel giữ NGUYÊN là chữ.
    /// </summary>
    private static string GiuDangChu(string giaTri)
    {
        bool laSoDienThoai = System.Text.RegularExpressions.Regex.IsMatch(giaTri, @"^0\d{8,}$");
        bool laNgay = System.Text.RegularExpressions.Regex.IsMatch(giaTri, @"^\d{1,2}/\d{1,2}/\d{4}( \d{1,2}:\d{2})?$")
                   || System.Text.RegularExpressions.Regex.IsMatch(giaTri, @"^\d{1,2}/\d{4}$");   // tháng/năm
        return laSoDienThoai || laNgay ? "=\"" + giaTri + "\"" : giaTri;
    }
}