using AIWork.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
namespace AIWork.Infrastructure;
public static class MigrationSafety
{
    public static async Task Upgrade(WorkDb db,string data)
    {
        if(!(await db.Database.GetPendingMigrationsAsync()).Any()){await db.Database.MigrateAsync();return;}
        var applied=(await db.Database.GetAppliedMigrationsAsync()).Any();
        if(applied)
        {
            var dir=Path.Combine(data,"Backups");Directory.CreateDirectory(dir);var stamp=DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");var snapshot=Path.Combine(dir,"pre-migration-"+stamp+".db");var probePath=Path.Combine(dir,"upgrade-verified-"+stamp+".db");
            await db.Database.OpenConnectionAsync();using(var dest=new SqliteConnection($"Data Source={snapshot};Pooling=False")){dest.Open();((SqliteConnection)db.Database.GetDbConnection()).BackupDatabase(dest);}File.Copy(snapshot,probePath,false);
            using var probe=new WorkDb(new DbContextOptionsBuilder<WorkDb>().UseSqlite($"Data Source={probePath};Foreign Keys=True;Pooling=False;Default Timeout=10").Options);await probe.Database.OpenConnectionAsync();var before=Fingerprint(probe.Database.GetDbConnection());await probe.Database.MigrateAsync();var after=Fingerprint(probe.Database.GetDbConnection());if(before!=after)throw new InvalidOperationException("Migration probe changed existing project/task IDs.");using var check=probe.Database.GetDbConnection().CreateCommand();check.CommandText="PRAGMA quick_check";if((string?)check.ExecuteScalar()!="ok")throw new InvalidOperationException("Migration probe quick_check failed.");
        }
        await db.Database.MigrateAsync();
    }
    private static string Fingerprint(System.Data.Common.DbConnection c)
    {
        var text=new StringBuilder();foreach(var table in new[]{"Projects","Tasks"}){using var cmd=c.CreateCommand();cmd.CommandText=$"SELECT Id FROM {table} ORDER BY Id";using var reader=cmd.ExecuteReader();while(reader.Read())text.Append(table).Append(':').Append(reader.GetString(0)).Append(';');}return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }
}
