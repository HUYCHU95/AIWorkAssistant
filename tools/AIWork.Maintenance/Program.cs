using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using AIWork.Infrastructure;
if(args.Length<2)throw new ArgumentException("snapshot|inspect|upgrade <database> [snapshot destination]");
var path=Path.GetFullPath(args[1]);if(!File.Exists(path))throw new FileNotFoundException("Database missing; refusing to create",path);
using var source=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=path,Mode=args[0]=="upgrade"?SqliteOpenMode.ReadWrite:SqliteOpenMode.ReadOnly,Pooling=false,ForeignKeys=true,DefaultTimeout=10}.ToString());source.Open();
if(args[0]=="snapshot"){using var target=new SqliteConnection($"Data Source={Path.GetFullPath(args[2])};Pooling=False");target.Open();source.BackupDatabase(target);}
if(args[0]=="upgrade"){using var db=new WorkDb(new DbContextOptionsBuilder<WorkDb>().UseSqlite(source).Options);db.Database.Migrate();}
foreach(var table in new[]{"Projects","Tasks","AuditEvents","__EFMigrationsHistory"}){using var command=source.CreateCommand();command.CommandText=$"SELECT count(*) FROM \"{table}\"";try{Console.WriteLine($"{table}: {command.ExecuteScalar()}");}catch(SqliteException){Console.WriteLine($"{table}: unavailable");}}
using var check=source.CreateCommand();check.CommandText="PRAGMA quick_check";Console.WriteLine("quick_check: "+check.ExecuteScalar());
foreach(var table in new[]{"Projects","Tasks"}){using var command=source.CreateCommand();command.CommandText=$"SELECT Id FROM {table} ORDER BY Id";var ids=new System.Text.StringBuilder();using(var r=command.ExecuteReader())while(r.Read())ids.Append(r.GetString(0)).Append(';');Console.WriteLine(table+"IdSha256: "+Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(ids.ToString()))));}
