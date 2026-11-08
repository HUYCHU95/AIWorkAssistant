using AIWork.Domain;
using AIWork.Application;
using AIWork.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

var root = AppContext.BaseDirectory;
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = root, WebRootPath = Path.Combine(root, "wwwroot") });
// Configuration and mutable files always follow the executable, regardless of cwd.
var data = Path.Combine(root, "Data");
foreach (var folder in new[] { "", "Attachments", "Backups", "Logs", "Keys", "Imports" }) Directory.CreateDirectory(Path.Combine(data, folder));
FileStream? instanceLock = null;
try { instanceLock = new FileStream(Path.Combine(data, "instance.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
catch (IOException) { Console.Error.WriteLine("AIWork đang chạy với thư mục Data này. Hãy đóng bản đang chạy trước."); return; }
using var heldLock = instanceLock;
var port = builder.Configuration.GetValue<int?>("LocalPort") ?? 5088;
if (port is < 1024 or > 65535) throw new InvalidOperationException("LocalPort phải từ 1024 đến 65535.");
builder.WebHost.ConfigureKestrel(k => { k.Listen(System.Net.IPAddress.Parse("127.0.0.1"), port); k.Limits.MaxRequestBodySize = 32 * 1024 * 1024; });
var cs = new SqliteConnectionStringBuilder { DataSource = Path.Combine(data, "AIWork.db"), ForeignKeys = true, DefaultTimeout = 10 }.ToString();
builder.Services.AddDbContext<WorkDb>(o => o.UseSqlite(cs));
builder.Services.AddScoped<WorkbookImporter>();
builder.Services.AddScoped<TrackingService>();
builder.Services.AddSingleton<ISecretStore>(sp => new WindowsSecretStore(Path.Combine(sp.GetRequiredService<AppPaths>().Data,"Secrets")));
builder.Services.AddHttpClient<IAIProvider,OpenAIResponsesProvider>(client=>client.Timeout=TimeSpan.FromSeconds(120))
    .ConfigurePrimaryHttpMessageHandler(()=>new HttpClientHandler{AllowAutoRedirect=false});
builder.Services.AddScoped<ReadTools>();
builder.Services.AddScoped<ActionService>();
builder.Services.AddScoped<InboxService>();
builder.Services.AddScoped<MemoryService>();
builder.Services.AddScoped<AttachmentService>(sp=>new AttachmentService(sp.GetRequiredService<WorkDb>(),Path.Combine(sp.GetRequiredService<AppPaths>().Data,"Attachments")));
builder.Services.AddScoped<IChatTools,ControlledTools>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<SourceContentService>();
builder.Services.AddScoped<AnalysisService>();
builder.Services.AddScoped<PlannerService>();
builder.Services.AddScoped<MailService>();
builder.Services.AddScoped<IMailTransport,SmtpTransport>();
builder.Services.AddScoped<BackupService>(sp=>new BackupService(sp.GetRequiredService<WorkDb>(),sp.GetRequiredService<AppPaths>().Data));
builder.Services.AddHostedService<AIWork.Web.PlannerWorker>();
builder.Services.AddSingleton(new AppPaths(root, data));
builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(data, "Keys"))).SetApplicationName("AIWorkAssistant.V01");
builder.Services.AddScoped<IPasswordHasher<LocalUser>, PasswordHasher<LocalUser>>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(o => { o.LoginPath = "/Account/Login"; o.Cookie.Name = "AIWork.Local"; o.Cookie.HttpOnly = true; o.Cookie.SameSite = SameSiteMode.Strict; o.ExpireTimeSpan = TimeSpan.FromHours(8); o.SlidingExpiration = true;
    o.Events.OnValidatePrincipal=async context=>{var db=context.HttpContext.RequestServices.GetRequiredService<WorkDb>();var user=await db.Users.AsNoTracking().SingleOrDefaultAsync();if(user==null||user.UserName!=context.Principal?.Identity?.Name||user.SecurityStamp!=context.Principal?.FindFirst("security_stamp")?.Value)context.RejectPrincipal();};
});
builder.Services.AddAuthorization();
builder.Services.AddControllersWithViews(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
var app = builder.Build();
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<WorkDb>();
    var startupData=scope.ServiceProvider.GetRequiredService<AppPaths>().Data;
    BackupService.ApplyPendingOffline(startupData,db.Database.GetMigrations().ToHashSet());
    var marker=Path.Combine(startupData,"database.initialized");
    if(File.Exists(marker)&&!File.Exists(db.Database.GetDbConnection().DataSource))throw new InvalidOperationException("Previously initialized database missing; refusing to recreate.");
    await db.Database.OpenConnectionAsync();
    await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
    await MigrationSafety.Upgrade(db,startupData);
    var initialUser=await db.Users.SingleOrDefaultAsync();if(initialUser!=null&&string.IsNullOrWhiteSpace(initialUser.SecurityStamp)){initialUser.SecurityStamp=Guid.NewGuid().ToString("N");db.Audit("UpgradeSessionStamp","LocalUser","1");await db.SaveChangesAsync();}
    await File.WriteAllTextAsync(marker,"AIWork database initialized; do not recreate automatically.");
}
catch (Exception ex)
{
    var message = $"{DateTime.UtcNow:O} Startup/migration failed: {ex.GetType().Name}. Database preserved.\n";
    File.AppendAllText(Path.Combine(data, "Logs", "startup.log"), message);
    Console.Error.WriteLine("Không thể mở/nâng cấp Data/AIWork.db. Dữ liệu được giữ nguyên. Kiểm tra quyền thư mục, dung lượng, bản sao lưu và Data/Logs/startup.log.");
    throw;
}
app.UseExceptionHandler("/Home/Error");
app.UseStatusCodePagesWithReExecute("/Home/Status", "?code={0}");
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Content-Security-Policy"] = "default-src 'self'; style-src 'self'; script-src 'self'; img-src 'self' data: blob:; frame-ancestors 'none'; form-action 'self'";
    await next();
});
app.UseStaticFiles(); app.UseRouting(); app.UseAuthentication();
app.Use(async (ctx, next) =>
{
    var db = ctx.RequestServices.GetRequiredService<WorkDb>(); db.Actor = ctx.User.Identity?.Name ?? "System";
    if (!ctx.Request.Path.StartsWithSegments("/Appearance/Styles") && !ctx.Request.Path.StartsWithSegments("/Account") && !ctx.Request.Path.StartsWithSegments("/Home/Error") && !ctx.Request.Path.StartsWithSegments("/Home/Status") && !await db.Users.AnyAsync()) { ctx.Response.Redirect("/Account/Setup"); return; }
    await next();
});
CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("vi-VN");
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("vi-VN");
app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
await app.RunAsync();
public record AppPaths(string Root, string Data);
public partial class Program { }
