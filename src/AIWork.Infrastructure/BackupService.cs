using AIWork.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
namespace AIWork.Infrastructure;
public record BackupFile(string Path,long Length,string Sha256);
public class PackageManifest
{
    public string AppVersion{get;set;}="1.0.0";public string SchemaVersion{get;set;}="";public DateTime CreatedAtUtc{get;set;}=DateTime.UtcNow;public List<BackupFile> Files{get;set;}=[];
}
public record PendingRestore(string Token,string PackageSha256,string ApprovedBy);
public class BackupService(WorkDb db,string data)
{
    public async Task<string> Create(string prefix="AIWork")
    {
        var dir=Path.Combine(data,"Backups");Directory.CreateDirectory(dir);var stamp=DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")+"-"+Guid.NewGuid().ToString("N")[..6];var snapshot=Path.Combine(dir,stamp+".db");var zipPath=Path.Combine(dir,prefix+"-"+stamp+".zip");
        await db.Database.OpenConnectionAsync();using(var destination=new SqliteConnection($"Data Source={snapshot};Pooling=False")){destination.Open();((SqliteConnection)db.Database.GetDbConnection()).BackupDatabase(destination);using var check=destination.CreateCommand();check.CommandText="PRAGMA quick_check";if((string?)check.ExecuteScalar()!="ok")throw new ValidationException("Snapshot database không hợp lệ.");}
        var manifest=new PackageManifest{SchemaVersion=(await db.Database.GetAppliedMigrationsAsync()).LastOrDefault()??""};
        using(var zip=ZipFile.Open(zipPath,ZipArchiveMode.Create))
        {
            Add(zip,snapshot,"Data/AIWork.db",manifest);
            var attachments=Path.Combine(data,"Attachments");if(Directory.Exists(attachments))foreach(var file in Directory.EnumerateFiles(attachments,"*",SearchOption.AllDirectories))Add(zip,file,"Data/Attachments/"+Path.GetRelativePath(attachments,file).Replace('\\','/'),manifest);
            var entry=zip.CreateEntry("manifest.json");using var writer=new StreamWriter(entry.Open());await writer.WriteAsync(JsonSerializer.Serialize(manifest));
        }
        File.Delete(snapshot);var hash=HashFile(zipPath);db.BackupManifests.Add(new BackupManifest{FileName=Path.GetFileName(zipPath),Sha256=hash,SchemaVersion=manifest.SchemaVersion});db.Audit("BackupVerified","Application","local","Online SQLite snapshot + attachment hashes; secrets excluded.");await db.SaveChangesAsync();return zipPath;
    }
    private static void Add(ZipArchive zip,string file,string path,PackageManifest manifest){var info=new FileInfo(file);manifest.Files.Add(new(path,info.Length,HashFile(file)));zip.CreateEntryFromFile(file,path,CompressionLevel.Fastest);}
    public static string HashFile(string path){using var f=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(f));}
    public static PackageManifest Validate(string package,string extractTo,IReadOnlySet<string> supportedMigrations)
    {
        using var zip=ZipFile.OpenRead(package);if(zip.Entries.Count>5000||zip.Entries.Sum(e=>e.Length)>500L*1024*1024)throw new ValidationException("Backup vượt giới hạn 500MB/5000 tệp.");
        var entries=new Dictionary<string,ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        foreach(var e in zip.Entries)
        {
            var path=e.FullName;if(path.StartsWith('/')||path.Contains('\\')||path.Contains(':')||path.Split('/').Any(x=>x is ".." or "." or "")||(e.ExternalAttributes>>16&0xF000)==0xA000||!entries.TryAdd(path,e))throw new ValidationException("Backup có đường dẫn/trùng tên không an toàn.");
            if(path!="manifest.json"&&path!="Data/AIWork.db"&&!path.StartsWith("Data/Attachments/",StringComparison.Ordinal))throw new ValidationException("Backup chứa tệp ngoài DB/attachments.");
        }
        if(!entries.TryGetValue("manifest.json",out var manifestEntry)||manifestEntry.Length>1024*1024)throw new ValidationException("Thiếu manifest hợp lệ. Backup V0.1 cần phục hồi offline theo README cũ.");
        using var manifestStream=manifestEntry.Open();var manifest=JsonSerializer.Deserialize<PackageManifest>(manifestStream)??throw new ValidationException("Manifest không hợp lệ.");
        if(!supportedMigrations.Contains(manifest.SchemaVersion)||manifest.Files.Count!=entries.Count-1||manifest.Files.Select(f=>f.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=manifest.Files.Count||!manifest.Files.Any(f=>f.Path=="Data/AIWork.db"))throw new ValidationException("Schema/tập tệp backup không tương thích.");
        Directory.CreateDirectory(extractTo);var root=Path.GetFullPath(extractTo)+Path.DirectorySeparatorChar;
        foreach(var f in manifest.Files)
        {
            if(!entries.TryGetValue(f.Path,out var entry)||entry.Length!=f.Length)throw new ValidationException("Manifest không khớp tệp.");var dest=Path.GetFullPath(Path.Combine(extractTo,f.Path.Replace('/',Path.DirectorySeparatorChar)));if(!dest.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new ValidationException("Đường dẫn thoát staging.");Directory.CreateDirectory(Path.GetDirectoryName(dest)!);using(var input=entry.Open())using(var output=File.Create(dest))input.CopyTo(output);if(HashFile(dest)!=f.Sha256)throw new ValidationException("Checksum backup không khớp.");
        }
        using var dbCheck=new SqliteConnection($"Data Source={Path.Combine(extractTo,"Data","AIWork.db")};Mode=ReadOnly;Pooling=False;Foreign Keys=True");dbCheck.Open();
        using(var cmd=dbCheck.CreateCommand()){cmd.CommandText="PRAGMA quick_check";if((string?)cmd.ExecuteScalar()!="ok")throw new ValidationException("Database quick_check thất bại.");cmd.CommandText="PRAGMA foreign_key_check";using var result=cmd.ExecuteReader();if(result.Read())throw new ValidationException("Database FK check thất bại.");}
        using(var cmd=dbCheck.CreateCommand()){cmd.CommandText="SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId";using var reader=cmd.ExecuteReader();string latest="";while(reader.Read()){latest=reader.GetString(0);if(!supportedMigrations.Contains(latest))throw new ValidationException("Database chứa migration không hỗ trợ.");}if(latest!=manifest.SchemaVersion)throw new ValidationException("Schema thực tế không khớp manifest.");}
        using(var cmd=dbCheck.CreateCommand())
        {
            cmd.CommandText="SELECT count(*) FROM sqlite_master WHERE type='table' AND name='Documents'";
            if(Convert.ToInt32(cmd.ExecuteScalar())>0)
            {
                cmd.CommandText="SELECT StoredName,Sha256,LengthBytes FROM Documents WHERE ArchivedAtUtc IS NULL";using var reader=cmd.ExecuteReader();
                while(reader.Read()){var stored=reader.GetString(0);var entry=manifest.Files.FirstOrDefault(f=>f.Path=="Data/Attachments/"+stored);if(entry==null||entry.Sha256!=reader.GetString(1)||entry.Length!=reader.GetInt64(2))throw new ValidationException("Attachment reference/checksum không khớp database.");}
            }
        }
        return manifest;
    }
    public async Task QueueRestore(string token,string actor)
    {
        if(!Guid.TryParseExact(token,"N",out _))throw new ValidationException("Token không hợp lệ.");var dir=Path.Combine(data,"RestoreQueue",token);var package=Path.Combine(dir,"package.zip");Validate(package,Path.Combine(dir,"verified"),db.Database.GetMigrations().ToHashSet());
        await Create("pre-restore");db.Audit("RestoreQueued","Application","local","Verified package staged; offline swap next startup; previous files retained.");await db.SaveChangesAsync();var request=new PendingRestore(token,HashFile(package),actor);await File.WriteAllTextAsync(Path.Combine(data,"RestoreQueue","request.json"),JsonSerializer.Serialize(request));
    }
    public static void ApplyPendingOffline(string data,IReadOnlySet<string> supportedMigrations)
    {
        var requestPath=Path.Combine(data,"RestoreQueue","request.json");if(!File.Exists(requestPath))return;var request=JsonSerializer.Deserialize<PendingRestore>(File.ReadAllText(requestPath))??throw new ValidationException("Restore request không hợp lệ.");if(!Guid.TryParseExact(request.Token,"N",out _))throw new ValidationException("Restore token không hợp lệ.");
        var dir=Path.Combine(data,"RestoreQueue",request.Token);var package=Path.Combine(dir,"package.zip");if(HashFile(package)!=request.PackageSha256)throw new ValidationException("Restore package đã thay đổi.");var stage=Path.Combine(dir,"offline-verified");Validate(package,stage,supportedMigrations);
        var live=Path.Combine(data,"AIWork.db");var attachments=Path.Combine(data,"Attachments");var rollback=Path.Combine(data,"Backups","pre-restore-files-"+request.Token);if(Directory.Exists(rollback))throw new ValidationException("Restore checkpoint đã tồn tại; cần kiểm tra thủ công.");Directory.CreateDirectory(rollback);SqliteConnection.ClearAllPools();
        if(File.Exists(live)){using var c=new SqliteConnection($"Data Source={live};Pooling=False");c.Open();using var cmd=c.CreateCommand();cmd.CommandText="PRAGMA wal_checkpoint(TRUNCATE)";cmd.ExecuteNonQuery();}
        var movedDb=false;var movedAttachments=false;var newDb=false;var newAttachments=false;
        try
        {
            if(File.Exists(live)){File.Move(live,Path.Combine(rollback,"AIWork.db"));movedDb=true;}foreach(var suffix in new[]{"-wal","-shm"})if(File.Exists(live+suffix))File.Move(live+suffix,Path.Combine(rollback,"AIWork.db"+suffix));
            if(Directory.Exists(attachments)){Directory.Move(attachments,Path.Combine(rollback,"Attachments"));movedAttachments=true;}
            File.Move(Path.Combine(stage,"Data","AIWork.db"),live);newDb=true;var stagedAttachments=Path.Combine(stage,"Data","Attachments");if(Directory.Exists(stagedAttachments))Directory.Move(stagedAttachments,attachments);else Directory.CreateDirectory(attachments);newAttachments=true;
            using(var c=new SqliteConnection($"Data Source={live};Pooling=False"))
            {
                c.Open();using var cmd=c.CreateCommand();cmd.CommandText="SELECT count(*) FROM sqlite_master WHERE type='table' AND name='Preferences'";if(Convert.ToInt32(cmd.ExecuteScalar())>0){cmd.CommandText="PRAGMA table_info(Preferences)";var columns=new HashSet<string>();using(var r=cmd.ExecuteReader())while(r.Read())columns.Add(r.GetString(1));var assignments=new[]{"AIEnabled=0","AllowExternalAIProcessing=0","SmtpEnabled=0","SmtpAutomaticReminders=0","SmtpVerifiedRecipient=NULL"}.Where(a=>columns.Contains(a.Split('=')[0]));cmd.CommandText="UPDATE Preferences SET "+string.Join(',',assignments);cmd.ExecuteNonQuery();}
                cmd.CommandText="PRAGMA table_info(Users)";var hasStamp=false;using(var r=cmd.ExecuteReader())while(r.Read())if(r.GetString(1)=="SecurityStamp")hasStamp=true;if(hasStamp){cmd.CommandText="UPDATE Users SET SecurityStamp=$stamp";cmd.Parameters.AddWithValue("$stamp",Guid.NewGuid().ToString("N"));cmd.ExecuteNonQuery();}
            }
            using(var auditConnection=new SqliteConnection($"Data Source={live};Pooling=False"))
            {
                auditConnection.Open();using var audit=auditConnection.CreateCommand();audit.CommandText="INSERT INTO AuditEvents(Id,OccurredAtUtc,Actor,Action,EntityType,EntityId,AfterJson) VALUES($id,$utc,$actor,'RestoreApplied','Application','local',$details)";audit.Parameters.AddWithValue("$id",Guid.NewGuid());audit.Parameters.AddWithValue("$utc",DateTime.UtcNow);audit.Parameters.AddWithValue("$actor",request.ApprovedBy);audit.Parameters.AddWithValue("$details","Offline restore verified; previous files retained; external processing disabled.");audit.ExecuteNonQuery();
            }
            File.Move(requestPath,Path.Combine(dir,"applied-request.json"));File.AppendAllText(Path.Combine(data,"Logs","restore.log"),$"{DateTime.UtcNow:O} Offline restore applied; previous database/attachments retained. External processing disabled.\n");
        }
        catch
        {
            foreach(var suffix in new[]{"-wal","-shm"})if(newDb&&File.Exists(live+suffix))File.Move(live+suffix,Path.Combine(dir,"failed-restored.db"+suffix));
            if(newDb&&File.Exists(live))File.Move(live,Path.Combine(dir,"failed-restored.db"));if(newAttachments&&Directory.Exists(attachments))Directory.Move(attachments,Path.Combine(dir,"failed-attachments"));if(movedDb){File.Move(Path.Combine(rollback,"AIWork.db"),live);foreach(var suffix in new[]{"-wal","-shm"})if(File.Exists(Path.Combine(rollback,"AIWork.db"+suffix)))File.Move(Path.Combine(rollback,"AIWork.db"+suffix),live+suffix);}if(movedAttachments)Directory.Move(Path.Combine(rollback,"Attachments"),attachments);throw;
        }
    }
}



