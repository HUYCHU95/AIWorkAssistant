using AIWork.Application;
using AIWork.Domain;
using AIWork.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using Xunit;
namespace AIWork.Tests;

public class DomainTests
{
    [Fact]
    public void DeadlinesAndProgressUseRealFacts()
    {
        var today = new DateOnly(2026, 11, 9); var t = new WorkTask { Title = "Check", DueDate = today.AddDays(-1), DueDateType = DeadlineType.Suggested };
        Assert.True(t.IsOverdue(today)); t.Status = WorkStatus.Done; WorkRules.Transition(t, WorkStatus.Todo); Assert.NotNull(t.CompletedAtUtc); Assert.False(t.IsOverdue(today));
        t.Status = WorkStatus.Cancelled; WorkRules.Transition(t, WorkStatus.Done); Assert.Null(t.CompletedAtUtc); Assert.False(t.IsOverdue(today));
        Assert.Equal("N/A", WorkRules.Progress([t])); t.Status = WorkStatus.Todo; t.DueDate = null; Assert.False(t.IsOverdue(today));
        Assert.Equal("0/1 (0%)", WorkRules.Progress([t]));
        var errors = new List<ValidationResult>(); Assert.False(Validator.TryValidateObject(t, new ValidationContext(t), errors, true));
        t.DueDateType = null; t.ProjectId = Guid.NewGuid(); t.WorkstreamId = Guid.NewGuid(); errors.Clear(); Assert.False(Validator.TryValidateObject(t, new ValidationContext(t), errors, true));
    }
    [Fact]
    public async Task ImportIsIdempotentAuditedPersistentAndBackupRestores()
    {
        var folder = Path.Combine(Path.GetTempPath(), "AIWorkTests-" + Guid.NewGuid()); Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "test.db"); var options = new DbContextOptionsBuilder<WorkDb>().UseSqlite($"Data Source={path};Foreign Keys=True;Default Timeout=10;Pooling=False").Options;
        var workbook = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../sample-data/9_projects_nov_2026.xlsx"));
        using (var db = new WorkDb(options))
        {
            await db.Database.MigrateAsync(); db.Actor = "test-user"; var importer = new WorkbookImporter(db); using var file = File.OpenRead(workbook); var preview = importer.Preview(file, "sample.xlsx");
            Assert.Equal(9, preview.Projects.Count); Assert.Equal(60, preview.Tasks.Count); Assert.Contains(preview.Tasks, t => t.Type == DeadlineType.Suggested); Assert.DoesNotContain(preview.Tasks, t => t.Type == DeadlineType.Committed);
            var batch = await importer.Confirm(preview); Assert.Equal(69, batch.CreatedCount); Assert.Equal(60, await db.Tasks.CountAsync());
            Assert.All(await db.Tasks.ToListAsync(), t => { Assert.Equal(WorkStatus.Todo, t.Status); Assert.Null(t.OwnerText); Assert.Null(t.CompletedAtUtc); });
            var task = await db.Tasks.FirstAsync(); task.Title = "User edit retained"; task.Status = WorkStatus.Blocked; await db.SaveChangesAsync();
            batch = await importer.Confirm(preview); Assert.Equal(0, batch.CreatedCount); Assert.Equal(69, batch.SkippedCount); Assert.Equal("User edit retained", task.Title);
            Assert.Equal(2, await db.ImportBatches.CountAsync()); Assert.True(await db.AuditEvents.AnyAsync(a => a.Actor == "test-user" && a.Action == "Update" && a.BeforeJson != null));
            Assert.All(await db.Projects.ToListAsync(), p => { Assert.Null(p.CurrentTargetDate); Assert.Null(p.OwnerText); Assert.Equal(ProjectStatus.Planned, p.Status); });
            await db.Database.OpenConnectionAsync(); using var backup = new SqliteConnection($"Data Source={Path.Combine(folder, "restore.db")};Pooling=False"); backup.Open(); ((SqliteConnection)db.Database.GetDbConnection()).BackupDatabase(backup);
        }
        using (var restarted = new WorkDb(options)) { Assert.Equal(9, await restarted.Projects.CountAsync()); Assert.Equal(60, await restarted.Tasks.CountAsync()); Assert.True(await restarted.Tasks.AnyAsync(t => t.Title == "User edit retained")); }
        using (var restored = new WorkDb(new DbContextOptionsBuilder<WorkDb>().UseSqlite($"Data Source={Path.Combine(folder, "restore.db")};Pooling=False").Options)) { Assert.Equal(60, await restored.Tasks.CountAsync()); Assert.Equal("ok", await Scalar(restored, "PRAGMA integrity_check")); }
        using (var first = new WorkDb(options)) using (var second = new WorkDb(options))
        {
            var a = await first.Tasks.FirstAsync(); var b = await second.Tasks.FindAsync(a.Id); a.NextAction = "Window A"; await first.SaveChangesAsync(); b!.NextAction = "Window B"; await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        }
        Directory.Delete(folder, true);
    }
    private static async Task<string> Scalar(WorkDb db, string sql) { await db.Database.OpenConnectionAsync(); using var c = db.Database.GetDbConnection().CreateCommand(); c.CommandText = sql; return (string)(await c.ExecuteScalarAsync())!; }
    [Fact] public void ImportRejectsMalformedWorkbook() { using var db = new WorkDb(new DbContextOptionsBuilder<WorkDb>().UseSqlite("Data Source=:memory:").Options); using var stream = new MemoryStream([1, 2, 3]); Assert.ThrowsAny<Exception>(() => new WorkbookImporter(db).Preview(stream, "bad.xlsx")); }
}
