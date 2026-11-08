using AIWork.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
namespace AIWork.Web.Controllers;

[Authorize]
public class ImportController(WorkDb db, WorkbookImporter importer, AppPaths paths) : Controller
{
    public async Task<IActionResult> Index() => View(await db.ImportBatches.OrderByDescending(b => b.ImportedAtUtc).Take(20).ToListAsync());
    [HttpPost, RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Preview(IFormFile? file)
    {
        if (file == null || file.Length == 0 || file.Length > 5 * 1024 * 1024 || !Path.GetExtension(file.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase)) { TempData["Message"] = "Chọn tệp .xlsx từ 1 byte đến 5 MB."; return RedirectToAction("Index"); }
        try
        {
            using var bytes = new MemoryStream(); await file.CopyToAsync(bytes); bytes.Position = 0;
            var preview = importer.Preview(bytes, file.FileName); var token = Guid.NewGuid().ToString("N");
            await System.IO.File.WriteAllBytesAsync(Path.Combine(paths.Data, "Imports", token + ".xlsx"), bytes.ToArray());
            TempData["ImportToken"] = token; TempData["ImportName"] = preview.FileName; TempData["ImportHash"] = preview.Hash;
            ViewBag.Token = token; return View(preview);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { TempData["Message"] = "Không thể đọc workbook. Kiểm tra đúng mẫu, ô dữ liệu, định dạng và giới hạn. " + (ex is InvalidDataException ? ex.Message : "Tệp không hợp lệ."); return RedirectToAction("Index"); }
    }
    [HttpPost]
    public async Task<IActionResult> Confirm(string token)
    {
        var expected = TempData["ImportToken"] as string; var name = TempData["ImportName"] as string; var hash = TempData["ImportHash"] as string;
        if (expected == null || token != expected || !Guid.TryParseExact(token, "N", out _)) return BadRequest("Bản xem trước đã hết hạn. Hãy tải lại tệp.");
        var path = Path.Combine(paths.Data, "Imports", token + ".xlsx");
        try
        {
            using var file = System.IO.File.OpenRead(path); var preview = importer.Preview(file, name ?? "Workbook.xlsx");
            if (preview.Hash != hash) return BadRequest("Tệp đã thay đổi. Hãy xem trước lại.");
            var batch = await importer.Confirm(preview); TempData["Message"] = $"Đã nhập {batch.CreatedCount} dự án/đầu việc mới, bỏ qua {batch.SkippedCount} bản ghi đã tồn tại. Xem lịch sử lô nhập bên dưới.";
        }
        catch (Exception ex) when (ex is InvalidDataException or DbUpdateException or IOException) { TempData["Message"] = "Không nhập dữ liệu. Tệp hết hạn, mã bị xung đột hoặc database đang bận; hãy xem trước lại."; }
        finally { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); }
        return RedirectToAction("Index");
    }
}
[Authorize]
public class SettingsController(BackupService backups) : Controller
{
    public IActionResult Index() => View();
    [HttpPost]
    public async Task<IActionResult> Backup()
    {
        var zipPath = await backups.Create();
        return PhysicalFile(zipPath, "application/zip", Path.GetFileName(zipPath));
    }
}



