using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIWork.Domain;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace AIWork.Infrastructure;

public record ProjectRow(int Row, string Code, string Name, string Workstream, string Source);
public record TaskRow(int Row, string Key, string ProjectName, string Title, DateOnly? Due, DeadlineType? Type, Priority Priority, string Source);
public class ImportPreview
{
    public List<ProjectRow> Projects { get; set; } = [];
    public List<TaskRow> Tasks { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
    public string Hash { get; set; } = "";
    public string FileName { get; set; } = "";
}
public class WorkbookImporter(WorkDb db)
{
    public ImportPreview Preview(Stream input, string fileName)
    {
        using var bytes = new MemoryStream(); input.CopyTo(bytes);
        if (bytes.Length > 5 * 1024 * 1024) throw new InvalidDataException("Tệp tối đa 5 MB.");
        bytes.Position = 0;
        using (var zip = new ZipArchive(bytes, ZipArchiveMode.Read, true))
            if (zip.Entries.Count > 200 || zip.Entries.Sum(e => e.Length) > 20 * 1024 * 1024) throw new InvalidDataException("Workbook vượt giới hạn giải nén.");
        bytes.Position = 0;
        var result = new ImportPreview { Hash = Convert.ToHexString(SHA256.HashData(bytes.ToArray())), FileName = Path.GetFileName(fileName) };
        using var book = new XLWorkbook(bytes);
        var sheet = book.Worksheets.FirstOrDefault(s => s.Name == "Quan_ly_9_du_an") ?? throw new InvalidDataException("Cần sheet Quan_ly_9_du_an theo mẫu.");
        string Text(int row, int col)
        {
            var cell = sheet.Cell(row, col);
            if (cell.HasFormula) throw new InvalidDataException($"Ô {cell.Address}: không cho phép công thức ở cột dữ liệu nhập.");
            var text = cell.GetString().Trim();
            if (text.Length > 3000) throw new InvalidDataException($"Ô {cell.Address}: nội dung quá dài.");
            return text;
        }
        if (Text(8, 3) != "Tên dự án" || Text(20, 3) != "Công việc cần làm" || Text(20, 14) != "Loại deadline") throw new InvalidDataException("Tiêu đề/cấu trúc không khớp mapping 9projects-v1.");
        for (int r = 9; r <= 17; r++)
        {
            var number = Text(r, 1); var name = Text(r, 3);
            if (!int.TryParse(number, out var id) || id is < 1 or > 9 || name.Length == 0 || name.Length > 300) throw new InvalidDataException($"Dòng {r}: dự án không hợp lệ.");
            result.Projects.Add(new(r, $"AUTO-{id:00}", name, Text(r, 2), $"Excel dòng {r}; PIC dự kiến: {Text(r, 7)}; mốc nguồn (chưa cam kết): {Text(r, 6)}; trạng thái nguồn chưa xác minh: {Text(r, 9)}; ghi chú: {Text(r, 14)}"));
        }
        for (int r = 21; r <= 80; r++)
        {
            var key = Text(r, 1); var name = Text(r, 2); var title = Text(r, 3);
            if (key.Length == 0 || title.Length == 0 || title.Length > 500 || !result.Projects.Any(p => p.Name == name)) throw new InvalidDataException($"Dòng {r}: thiếu ID/tên việc hoặc không tìm thấy dự án.");
            var dateCell = sheet.Cell(r, 4); DateOnly? due = null;
            if (!dateCell.IsEmpty())
            {
                if (dateCell.HasFormula) throw new InvalidDataException($"D{r}: công thức không được nhập.");
                if (dateCell.DataType == XLDataType.DateTime) due = DateOnly.FromDateTime(dateCell.GetDateTime());
                else if (dateCell.DataType == XLDataType.Number) due = DateOnly.FromDateTime(DateTime.FromOADate(dateCell.GetDouble()));
                else if (DateOnly.TryParseExact(Text(r, 4), "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) due = d;
                else result.Warnings.Add($"D{r}: ngày không rõ; giữ trống, xem thông tin nguồn.");
            }
            var typeText = Text(r, 14);
            // Only an explicit source plan maps to Planned. No imported date becomes a commitment.
            DeadlineType? type = due == null ? null : typeText == "Theo kế hoạch" ? DeadlineType.Planned : DeadlineType.Suggested;
            if (typeText != "Theo kế hoạch" && !typeText.Contains("Dự kiến")) result.Warnings.Add($"N{r}: loại hạn '{typeText}' chưa nhận diện; giữ dạng gợi ý.");
            var priorityText = Text(r, 7);
            var priority = priorityText switch { "Rất cao" => Priority.Critical, "Cao" => Priority.High, "Trung bình" => Priority.Normal, "Thấp" => Priority.Low, _ => Priority.Normal };
            if (priorityText is not ("Rất cao" or "Cao" or "Trung bình" or "Thấp")) result.Warnings.Add($"G{r}: ưu tiên chưa nhận diện, dùng Bình thường.");
            if (Text(r, 8) != "Chưa bắt đầu" || Text(r, 9).Length > 0) result.Warnings.Add($"Dòng {r}: trạng thái/ngày hoàn thành cần xác minh; nhập Chưa bắt đầu.");
            result.Tasks.Add(new(r, key, name, title, due, type, priority, $"Excel ID {key}, dòng {r}; PIC dự kiến: {Text(r, 5)}; kết quả cần có: {Text(r, 6)}; trạng thái nguồn: {Text(r, 8)}; ngày hoàn thành nguồn: {Text(r, 9)}; ngày hạn nguồn: {Text(r, 4)}; loại hạn nguồn: {typeText}; rủi ro: {Text(r, 10)}; ghi chú: {Text(r, 11)}"));
        }
        if (result.Projects.Select(p => p.Code).Distinct().Count() != 9 || result.Tasks.Select(t => t.Key).Distinct().Count() != 60) throw new InvalidDataException("ID bị trùng hoặc sai số lượng 9 dự án / 60 đầu việc.");
        if (result.Projects.Any(p => p.Workstream.Length == 0 || p.Workstream.Length > 200 || p.Source.Length > 4000) || result.Tasks.Any(t => t.Source.Length > 4000)) throw new InvalidDataException("Tên luồng hoặc thông tin nguồn vượt giới hạn trường lưu trữ.");
        result.Warnings.Insert(0, "PIC dự kiến giữ trong nguồn, không tự gán người phụ trách. Trạng thái nhập: Planned/Todo; chưa xác minh hoàn thành. Mốc dự án giữ nguyên văn, không chuyển thành ngày cam kết. Không nhập các công thức KPI J:M.");
        return result;
    }
    public async Task<ImportBatch> Confirm(ImportPreview preview)
    {
        using var tx = await db.Database.BeginTransactionAsync();
        var batch = new ImportBatch { SourceFileName = preview.FileName, FileSha256 = preview.Hash, WarningsJson = JsonSerializer.Serialize(preview.Warnings) };
        var projects = await db.Projects.ToListAsync();
        var streams = await db.Workstreams.ToListAsync();
        var map = new Dictionary<string, Guid>();
        foreach (var row in preview.Projects)
        {
            var project = projects.SingleOrDefault(p => p.Code == row.Code);
            if (project != null && project.Name != row.Name) throw new InvalidDataException($"Mã {row.Code} đã tồn tại với tên khác. Không ghi đè.");
            if (project == null)
            {
                var stream = streams.FirstOrDefault(w => w.Name == row.Workstream);
                if (stream == null)
                {
                    var code = "WS-" + Fingerprint(row.Workstream)[..12];
                    stream = new Workstream { Name = row.Workstream, Code = code }; streams.Add(stream); db.Workstreams.Add(stream);
                }
                project = new Project { Code = row.Code, Name = row.Name, WorkstreamId = stream.Id, SourceLabel = row.Source, Status = ProjectStatus.Planned };
                projects.Add(project); db.Projects.Add(project); batch.CreatedCount++;
            }
            else batch.SkippedCount++;
            map[row.Name] = project.Id;
        }
        var keys = (await db.Tasks.Where(t => t.ImportFingerprint != null).Select(t => t.ImportFingerprint!).ToListAsync()).ToHashSet();
        foreach (var row in preview.Tasks)
        {
            var key = Fingerprint("9projects-v1:" + row.Key);
            if (!keys.Add(key)) { batch.SkippedCount++; continue; }
            db.Tasks.Add(new WorkTask { Title = row.Title, ProjectId = map[row.ProjectName], DueDate = row.Due, DueDateType = row.Type, Priority = row.Priority, SourceLabel = row.Source, ImportFingerprint = key }); batch.CreatedCount++;
        }
        db.ImportBatches.Add(batch); db.Audit("Import", "ImportBatch", batch.Id.ToString(), JsonSerializer.Serialize(batch));
        await db.SaveChangesAsync(); await tx.CommitAsync(); return batch;
    }
    private static string Fingerprint(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}

