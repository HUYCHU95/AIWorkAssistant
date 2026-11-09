using System.Net;
using System.Text.RegularExpressions;
using AIWork.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
namespace AIWork.Tests;

public class HttpTests
{
    [Fact]
    public async Task FullMvcWorkflowIsProtectedValidatedAndPersistent()
    {
        var folder = Path.Combine(Path.GetTempPath(), "AIWorkHttp-" + Guid.NewGuid()); Directory.CreateDirectory(folder);
        using (var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<WorkDb>>(); services.RemoveAll<WorkDb>();
            services.AddDbContext<WorkDb>(o => o.UseSqlite($"Data Source={Path.Combine(folder, "http.db")};Foreign Keys=True;Pooling=False"));
            services.RemoveAll<AppPaths>(); services.AddSingleton(new AppPaths(folder, folder));
            foreach (var sub in new[] { "Imports", "Backups", "Attachments" }) Directory.CreateDirectory(Path.Combine(folder, sub));
        })))
        {
            using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
            var setup = await client.GetAsync("/Tasks"); Assert.Equal(HttpStatusCode.Redirect, setup.StatusCode); Assert.Contains("Setup", setup.Headers.Location!.ToString());
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/Account/Setup", new FormUrlEncodedContent(new Dictionary<string, string> { { "UserName", "tester" }, { "Password", "test-password-123!" } }))).StatusCode);
            var password = "Test-only-" + Guid.NewGuid().ToString("N");
            var response = await Post(client, "/Account/Setup", new() { { "UserName", "tester" }, { "Password", password } }); Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            foreach (var url in new[] { "/", "/Projects", "/Tasks", "/Workstreams", "/Import", "/Settings", "/Tracking", "/Sources", "/Dependencies", "/Tracking/Edit", "/Sources/Create", "/Chat", "/Integrations", "/Actions", "/Inbox", "/Inbox/Capture", "/Memory", "/Memory/Create", "/Planner", "/Maintenance" }) Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode);
            Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/Analysis")).StatusCode);
            Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/Appearance")).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsync("/Analysis/Stage",new FormUrlEncodedContent(new Dictionary<string,string>()))).StatusCode);
            using var stage=new MultipartFormDataContent();stage.Add(new StringContent(await Token(client,"/Analysis")),"__RequestVerificationToken");var stageKey=Guid.NewGuid().ToString();stage.Add(new StringContent(stageKey),"requestKey");stage.Add(new StringContent("Task"),"targetKind");stage.Add(new StringContent("true"),"confidential");stage.Add(new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("Local synthetic evidence")),"files","evidence.txt");response=await client.PostAsync("/Analysis/Stage",stage);Assert.Equal(HttpStatusCode.OK,response.StatusCode);var staged=System.Text.Json.Nodes.JsonNode.Parse(await response.Content.ReadAsStringAsync())!;var draftId=staged["id"]!.GetValue<string>();var stagedSourceId=staged["sourceId"]!.GetValue<string>();
            response=await Post(client,"/Analysis/Analyze",new(){{"id",draftId}},"/Analysis");Assert.Contains("Blocked",await response.Content.ReadAsStringAsync());
            response=await Post(client,"/Analysis/Stage",new(){{"requestKey",stageKey},{"targetKind","Task"},{"text","retry"}},"/Analysis");Assert.Equal(draftId,System.Text.Json.Nodes.JsonNode.Parse(await response.Content.ReadAsStringAsync())!["id"]!.GetValue<string>());
            Assert.Contains("evidence.txt",await client.GetStringAsync("/Sources/Details/"+stagedSourceId));
            var exp=new AIWork.Domain.ExperienceSettings{Theme="Dark",PageAccentsJson="{\"Tasks\":\"#123456\"}"};var appearance=new Dictionary<string,string>{{"Theme","Dark"},{"SendKey","Enter"},{"HomeAccent",exp.HomeAccent},{"AIAccent",exp.AIAccent},{"WorkAccent",exp.WorkAccent},{"PlanningAccent",exp.PlanningAccent},{"SettingsAccent",exp.SettingsAccent},{"PageAccentsJson",exp.PageAccentsJson}};response=await Post(client,"/Appearance/Save",appearance,"/Appearance");Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);Assert.Contains("data-theme=\"dark\"",await client.GetStringAsync("/"));Assert.Contains("#123456",await client.GetStringAsync("/Appearance/Styles"));
            response=await Post(client,"/Chat/Create",new(){{"title","SSE HTTP"}},"/Chat");var conversationId=response.Headers.Location!.ToString().Split('/').Last();response=await Post(client,"/Chat/Stream",new(){{"id",conversationId},{"requestId",Guid.NewGuid().ToString()},{"text","Offline SSE"}},"/Chat/Details/"+conversationId);Assert.Equal("text/event-stream",response.Content.Headers.ContentType!.MediaType);var sse=await response.Content.ReadAsStringAsync();Assert.Contains("event: ack",sse);Assert.Contains("event: error",sse);Assert.Contains("Offline SSE",await client.GetStringAsync("/Chat/Messages/"+conversationId));
            Assert.Contains("kpis",await client.GetStringAsync("/Dashboard/Data"));
            Assert.Contains("1.1.1",await client.GetStringAsync("/System/Version"));
            foreach(var formPath in new[]{"/Projects/Edit","/Tasks/Edit","/Workstreams/Edit","/Tracking/Edit","/Inbox/Capture","/Sources/Create","/Memory/Create"}){var formHtml=await client.GetStringAsync(formPath);Assert.Contains("AI Analyze / Auto Fill",formHtml);Assert.Contains("analysis-auto",formHtml);Assert.Single(Regex.Matches(formHtml,"data-ai-entry"));}
            var ws = Guid.NewGuid(); response = await Post(client, "/Workstreams/Edit/" + ws, new() { { "Id", ws.ToString() }, { "Code", "WS-TEST" }, { "Name", "Test stream" }, { "Status", "0" }, { "Version", "0" } }); Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var project = Guid.NewGuid(); response = await Post(client, "/Projects/Edit/" + project, new() { { "Id", project.ToString() }, { "Code", "P-TEST" }, { "Name", "Test project" }, { "WorkstreamId", ws.ToString() }, { "Version", "0" } }); Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var task = Guid.NewGuid(); var fields = new Dictionary<string, string> { { "Id", task.ToString() }, { "Title", "HTTP task" }, {"analysisSourceId",stagedSourceId}, { "ProjectId", project.ToString() }, { "DueDate", "2026-11-01" }, { "DueDateType", "0" }, { "Status", "0" }, { "Priority", "2" }, { "Version", "0" } };
            response = await Post(client, "/Tasks/Edit/" + task, fields); Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            fields["Status"] = "3"; fields["Version"] = "1"; response = await Post(client, "/Tasks/Edit/" + task, fields); Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            fields["Title"] = "stale write"; response = await Post(client, "/Tasks/Edit/" + task, fields); Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Contains("validation-summary-errors", await response.Content.ReadAsStringAsync());
            fields["DueDateType"] = ""; fields["Version"] = "2"; response = await Post(client, "/Tasks/Edit/" + task, fields); Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Contains("validation-summary-errors", await response.Content.ReadAsStringAsync());
            var independent = Guid.NewGuid(); response = await Post(client, "/Tasks/Edit/" + independent, new() { { "Id", independent.ToString() }, { "Title", "Independent task" }, { "Status", "0" }, { "Priority", "1" }, { "Version", "0" } }); Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            response = await Post(client, "/Tasks/Archive/" + independent, new() { { "version", "1" } }); Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            foreach (var url in new[] { "/Workstreams/Details/" + ws, "/Projects/Details/" + project, "/Tasks/Details/" + task, "/Tasks?due=overdue", "/Tasks?status=Blocked" }) Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(url)).StatusCode);
            response = await Post(client, "/Projects/Archive/" + project, new() { { "version", "1" } });
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            fields["Title"] = "HTTP task"; fields["DueDateType"] = "0"; fields["Version"] = "2"; fields["Status"] = "4";
            response = await Post(client, "/Tasks/Edit/" + task, fields);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var editPage = await client.GetStringAsync("/Tasks/Edit/" + task);
            Assert.Contains(project.ToString(), editPage);
            Assert.Contains("selected", editPage);
            using var upload = new MultipartFormDataContent(); upload.Add(new StringContent(await Token(client, "/Import")), "__RequestVerificationToken"); var workbook = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../sample-data/9_projects_nov_2026.xlsx")); upload.Add(new ByteArrayContent(await File.ReadAllBytesAsync(workbook)), "file", "sample.xlsx");
            response = await client.PostAsync("/Import/Preview", upload); Assert.Equal(HttpStatusCode.OK, response.StatusCode); var html = await response.Content.ReadAsStringAsync(); Assert.Contains("60", html); var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"token\" value=\"([^\"]+)\"").Groups[1].Value); Assert.NotEmpty(token);
            response = await client.PostAsync("/Import/Confirm", new FormUrlEncodedContent(new Dictionary<string, string> { { "token", token }, { "__RequestVerificationToken", Extract(html) } })); Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            using var inboxUpload = new MultipartFormDataContent();
            inboxUpload.Add(new StringContent(await Token(client,"/Inbox/Capture")),"__RequestVerificationToken");
            inboxUpload.Add(new StringContent(Guid.NewGuid().ToString()),"CaptureKey");
            inboxUpload.Add(new StringContent("HTTP meeting <script>"),"Title");
            inboxUpload.Add(new StringContent("1. Review drawing\n2. Ask for supplier reply\n3. Check quality evidence\n<script>must stay text</script>"),"Body");
            inboxUpload.Add(new StringContent("2"),"Kind");
            inboxUpload.Add(new StringContent("true"),"IsConfidential");
            inboxUpload.Add(new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes("safe attachment")),"Files","note.txt");
            response=await client.PostAsync("/Inbox/Capture",inboxUpload);Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
            var inboxPath=response.Headers.Location!.ToString();var inboxHtml=await client.GetStringAsync(inboxPath);Assert.Contains("&lt;script&gt;",inboxHtml);Assert.DoesNotContain("<script>must stay text",inboxHtml);
            var inboxId=Guid.Parse(inboxPath.Split('/').Last());response=await Post(client,"/Inbox/Extract/"+inboxId,new(),inboxPath);Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
            Guid downloadId;Guid sourceId;
            using(var reviewScope=factory.Services.CreateScope())
            {
                var reviewDb=reviewScope.ServiceProvider.GetRequiredService<WorkDb>();var inboxItem=await reviewDb.InboxItems.FindAsync(inboxId);sourceId=inboxItem!.SourceId;downloadId=(await reviewDb.Documents.SingleAsync(d=>d.SourceId==sourceId)).Id;
                foreach(var action in await reviewDb.Actions.Where(a=>a.OriginSourceId==sourceId).ToListAsync()){response=await Post(client,"/Actions/Approve/"+action.Id,new(){{"version",action.Version.ToString()}},"/Actions/Details/"+action.Id);Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);}
            }
            using(var metadataScope=factory.Services.CreateScope())
            {
                var metadataDb=metadataScope.ServiceProvider.GetRequiredService<WorkDb>();var source=await metadataDb.Sources.FindAsync(sourceId);
                response=await Post(client,"/Sources/UpdateMetadata/"+sourceId,new(){{"version",source!.Version.ToString()},{"status","Classified"},{"isConfidential","true"}},"/Sources/Details/"+sourceId);Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
                await metadataDb.Entry(source).ReloadAsync();Assert.Equal("Classified",source.Status);Assert.Contains("<script>",source.OriginalText);Assert.True(source.IsConfidential);
            }
            response=await client.GetAsync("/Documents/Download/"+downloadId);Assert.Equal(HttpStatusCode.OK,response.StatusCode);Assert.Equal("safe attachment",await response.Content.ReadAsStringAsync());
            response=await Post(client,"/Memory/Create",new(){{"SubjectType","Project"},{"SubjectId",project.ToString()},{"Predicate","quality_report"},{"ValueText","Reported OK, technical work still open"},{"SourceId",sourceId.ToString()}});Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
            response=await Post(client,"/Planner/Schedule",new(){{"taskId",task.ToString()},{"scheduledLocal",AIWork.Application.WorkRules.Vietnam is {} zone?TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow.AddDays(1),zone).ToString("yyyy-MM-ddTHH:mm"):""}},"/Planner");Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
            response = await Post(client, "/Settings/Backup", new(), "/Settings"); Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal("application/zip", response.Content.Headers.ContentType!.MediaType);
            response = await Post(client, "/Account/Logout", new(), "/Settings"); Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            response = await client.GetAsync("/Tasks"); Assert.Equal(HttpStatusCode.Redirect, response.StatusCode); Assert.Contains("Login", response.Headers.Location!.ToString());
            Assert.Equal(HttpStatusCode.Redirect,(await client.GetAsync("/Documents/Download/"+downloadId)).StatusCode);
            response = await Post(client, "/Account/Login", new() { { "UserName", "tester" }, { "Password", password }, { "RememberMe", "true" } }); Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var rememberedCookie=response.Headers.GetValues("Set-Cookie").Single(c=>c.StartsWith("AIWork.Local=",StringComparison.Ordinal));
            Assert.Contains("expires=",rememberedCookie,StringComparison.OrdinalIgnoreCase);Assert.Contains("httponly",rememberedCookie,StringComparison.OrdinalIgnoreCase);Assert.Contains("samesite=strict",rememberedCookie,StringComparison.OrdinalIgnoreCase);
            var expiryText=Regex.Match(rememberedCookie,@"expires=([^;]+)",RegexOptions.IgnoreCase).Groups[1].Value;var expiry=DateTimeOffset.Parse(expiryText,System.Globalization.CultureInfo.InvariantCulture);
            Assert.InRange(expiry-DateTimeOffset.UtcNow,TimeSpan.FromDays(29.9),TimeSpan.FromDays(30.1));
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Tasks/Details/" + task)).StatusCode);
            using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<WorkDb>(); Assert.Equal(65, await db.Tasks.CountAsync()); Assert.True(await db.AuditEvents.AnyAsync(a => a.EntityId == task.ToString() && a.Action == "Update")); Assert.Equal("HTTP task", (await db.Tasks.FindAsync(task))!.Title);Assert.True(await db.SourceLinks.AnyAsync(l=>l.SourceId==Guid.Parse(stagedSourceId)&&l.EntityId==task));Assert.Contains("/Sources/Details/"+stagedSourceId,await client.GetStringAsync("/Tasks/Details/"+task));
            var replacementPassword="Test-only-new-"+Guid.NewGuid().ToString("N");
            response=await Post(client,"/Profile/ChangePassword",new(){{"CurrentPassword",password},{"NewPassword",replacementPassword},{"ConfirmPassword",replacementPassword}},"/Profile");Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
            Assert.Equal(HttpStatusCode.Redirect,(await client.GetAsync("/Tasks")).StatusCode);
            response=await Post(client,"/Account/Login",new(){{"UserName","tester"},{"Password",password}});Assert.Equal(HttpStatusCode.OK,response.StatusCode);
            response=await Post(client,"/Account/Login",new(){{"UserName","tester"},{"Password",replacementPassword}});Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
            var sessionCookie=response.Headers.GetValues("Set-Cookie").Single(c=>c.StartsWith("AIWork.Local=",StringComparison.Ordinal));Assert.DoesNotContain("expires=",sessionCookie,StringComparison.OrdinalIgnoreCase);
            Assert.False(await db.AuditEvents.AnyAsync(a=>(a.BeforeJson!=null&&a.BeforeJson.Contains(replacementPassword))||(a.AfterJson!=null&&a.AfterJson.Contains(replacementPassword))));
        }
        Directory.Delete(folder, true);
    }
    private static string Extract(string html) => WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
    private static async Task<string> Token(HttpClient client, string path) => Extract(await client.GetStringAsync(path));
    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, Dictionary<string, string> data, string? tokenPage = null)
    {
        var page = tokenPage ?? (path.Contains("/Edit/") ? path[..path.LastIndexOf('/')] : path.Contains("/Archive/") ? path.Replace("/Archive/", "/Details/") : path);
        data["__RequestVerificationToken"] = await Token(client, page);
        return await client.PostAsync(path, new FormUrlEncodedContent(data));
    }
}
