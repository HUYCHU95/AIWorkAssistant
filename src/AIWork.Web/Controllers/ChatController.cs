using AIWork.Application;
using AIWork.Domain;
using AIWork.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
namespace AIWork.Web.Controllers;
public class ChatController(WorkDb db,ChatService chat):ManagementController(db)
{
    private static readonly SemaphoreSlim Gate=new(1,1);
    public async Task<IActionResult> Index(string? q,Guid? projectId,Guid? taskId){await Lists();ViewBag.DefaultProject=projectId;ViewBag.DefaultTask=taskId;ViewBag.Unavailable=await chat.Unavailable();return View(await Db.Conversations.Where(c=>c.ArchivedAtUtc==null&&(q==null||c.Title.Contains(q)||Db.Messages.Any(m=>m.ConversationId==c.Id&&m.Content.Contains(q)))).OrderByDescending(c=>c.UpdatedAtUtc).Take(50).ToListAsync());}
    [HttpPost]public async Task<IActionResult> Create(string title,Guid? projectId,Guid? taskId)
    {
        if(string.IsNullOrWhiteSpace(title)||title.Length>200)return BadRequest("Tên cuộc trò chuyện tối đa 200 ký tự.");
        if(projectId!=null&&!await Db.Projects.AnyAsync(p=>p.Id==projectId)||taskId!=null&&!await Db.Tasks.AnyAsync(t=>t.Id==taskId))return BadRequest("Ngữ cảnh không hợp lệ.");
        if(projectId!=null&&taskId!=null&&!await Db.Tasks.AnyAsync(t=>t.Id==taskId&&t.ProjectId==projectId))return BadRequest("Ngữ cảnh xung đột.");
        var c=new Conversation{Title=title.Trim(),ProjectId=projectId,TaskId=taskId};Db.Conversations.Add(c);await Db.SaveChangesAsync();return RedirectToAction("Details",new{id=c.Id});
    }
    [HttpPost]public async Task<IActionResult> Ask(string text,CancellationToken cancellationToken)
    {
        if(string.IsNullOrWhiteSpace(text)||text.Length>6000)return BadRequest("Nhập câu hỏi 1–6000 ký tự.");
        if(!await Gate.WaitAsync(0,cancellationToken)){TempData["Message"]="Một yêu cầu AI đang chạy.";return RedirectToAction("Index","Home");}
        try{var conversation=new Conversation{Title=text.Trim()[..Math.Min(text.Trim().Length,100)]};Db.Conversations.Add(conversation);await Db.SaveChangesAsync(cancellationToken);await chat.Send(conversation.Id,text,cancellationToken);return RedirectToAction("Details",new{id=conversation.Id});}finally{Gate.Release();}
    }
    public async Task<IActionResult> Details(Guid id){var c=await Db.Conversations.FindAsync(id);if(c==null)return NotFound();ViewBag.Messages=await Db.Messages.Where(m=>m.ConversationId==id).OrderBy(m=>m.CreatedAtUtc).ToListAsync();ViewBag.Unavailable=await chat.Unavailable();ViewBag.Usage=await Db.AIUsages.OrderByDescending(u=>u.CreatedAtUtc).Take(10).ToListAsync();return View(c);}
    [HttpPost]public async Task<IActionResult> Send(Guid id,string text,CancellationToken cancellationToken)
    {
        if(!await Gate.WaitAsync(0,cancellationToken)){TempData["Message"]="Một yêu cầu AI đang chạy. Đợi kết quả trước khi gửi tiếp.";return RedirectToAction("Details",new{id});}
        try{await chat.Send(id,text,cancellationToken);}catch(ArgumentException ex){TempData["Message"]=ex.Message;}finally{Gate.Release();}return RedirectToAction("Details",new{id});
    }
}
public class IntegrationSettings
{
    public AppPreferences Preferences{get;set;}=new();
    [DataType(DataType.Password),StringLength(2048)]public string? ApiKey{get;set;}
    [DataType(DataType.Password),StringLength(2048)]public string? SmtpPassword{get;set;}
}
public class IntegrationsController(WorkDb db,ISecretStore secrets):ManagementController(db)
{
    public async Task<IActionResult> Index(){ViewBag.KeyConfigured=!string.IsNullOrEmpty(secrets.Read("openai"));ViewBag.SmtpConfigured=!string.IsNullOrEmpty(secrets.Read("smtp"));return View(new IntegrationSettings{Preferences=await Db.Preferences.SingleOrDefaultAsync()??new()});}
    [HttpPost]public async Task<IActionResult> Save(IntegrationSettings input)
    {
        if(!ModelState.IsValid){TempData["Message"]="Cài đặt không hợp lệ. Không lưu bí mật; kiểm tra model, email và giới hạn.";return RedirectToAction("Index");}
        if(input.Preferences.SmtpEnabled&&(!input.Preferences.SmtpTls||string.IsNullOrWhiteSpace(input.Preferences.SmtpHost)||string.IsNullOrWhiteSpace(input.Preferences.SmtpFrom)||string.IsNullOrWhiteSpace(input.Preferences.SmtpRecipient))){TempData["Message"]="SMTP yêu cầu TLS, host, from và người nhận cụ thể.";return RedirectToAction("Index");}
        if(input.Preferences.AIEnabled&&string.IsNullOrWhiteSpace(input.Preferences.Model)){TempData["Message"]="Cần chọn model khi bật AI.";return RedirectToAction("Index");}
        try{if(!string.IsNullOrWhiteSpace(input.ApiKey))secrets.Write("openai",input.ApiKey.Trim());if(!string.IsNullOrWhiteSpace(input.SmtpPassword))secrets.Write("smtp",input.SmtpPassword);}catch(ArgumentException){TempData["Message"]="Định dạng bí mật không hợp lệ.";return RedirectToAction("Index");}
        var p=await Db.Preferences.SingleOrDefaultAsync();var verified=p?.SmtpVerifiedRecipient;var host=p?.SmtpHost;var recipient=p?.SmtpRecipient;if(p==null){p=new AppPreferences();Db.Preferences.Add(p);}var id=p.Id;Db.Entry(p).CurrentValues.SetValues(input.Preferences);p.Id=id;p.SmtpVerifiedRecipient=host==p.SmtpHost&&recipient==p.SmtpRecipient?verified:null;if(p.SmtpAutomaticReminders&&p.SmtpVerifiedRecipient!=p.SmtpRecipient)p.SmtpAutomaticReminders=false;
        Db.Audit("ConfigureIntegrations","Application","local","Changed local preferences; secrets omitted.");await Db.SaveChangesAsync();TempData["Message"]="Đã lưu cài đặt. Bí mật không hiển thị lại; chưa thực hiện cuộc gọi AI hay gửi email.";return RedirectToAction("Index");
    }
}



