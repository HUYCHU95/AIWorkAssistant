using AIWork.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Text.Json;
namespace AIWork.Web.Controllers;
public partial class ChatController
{
    private static readonly ConcurrentDictionary<Guid,(Guid Conversation,CancellationTokenSource Token)> Active=new();
    public async Task<IActionResult> Receipts(Guid id)=>Json(await Db.Actions.AsNoTracking().Where(a=>a.ConversationId==id).OrderBy(a=>a.CreatedAtUtc).Select(a=>new{a.Id,a.ToolName,a.Status,a.Summary,a.EntityType,a.EntityId,a.OriginSourceId,a.Version}).ToListAsync());
    public async Task<IActionResult> Messages(Guid id)=>Json(await Db.Messages.AsNoTracking().Where(m=>m.ConversationId==id).OrderBy(m=>m.CreatedAtUtc).Select(m=>new{m.Id,m.Role,m.Content,m.IsError,m.Model,m.CreatedAtUtc,m.CorrelationId,sourceId=Db.ChatTurns.Where(t=>t.UserMessageId==m.Id).Select(t=>t.SourceId).FirstOrDefault()}).ToListAsync());
    [HttpPost]public IActionResult Stop(Guid id,Guid requestId){if(Active.TryGetValue(requestId,out var running)&&running.Conversation==id){running.Token.Cancel();return Json(new{status="CancellationRequested"});}return Json(new{status="NotRunning"});}
    [HttpPost]public async Task Stream(Guid id,Guid requestId,string text,Guid? sourceId,CancellationToken cancellationToken)
    {
        if(requestId==Guid.Empty||string.IsNullOrWhiteSpace(text)||text.Length>6000||!await Db.Conversations.AnyAsync(c=>c.Id==id,cancellationToken)){Response.StatusCode=400;return;}
        if(!await Gate.WaitAsync(0,cancellationToken)){Response.StatusCode=409;return;}
        using var stop=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);stop.CancelAfter(TimeSpan.FromMinutes(2));
        Active[requestId]=(id,stop);Response.ContentType="text/event-stream";Response.Headers.CacheControl="no-cache";Response.Headers["X-Accel-Buffering"]="no";
        async Task Emit(string name,object value){if(!cancellationToken.IsCancellationRequested){await Response.WriteAsync("event: "+name+"\ndata: "+JsonSerializer.Serialize(value)+"\n\n",cancellationToken);await Response.Body.FlushAsync(cancellationToken);}}
        try{await chat.Send(id,text,stop.Token,requestId,Emit,sourceId);await Emit("end",new{status="Stored"});}catch(ArgumentException ex){await Emit("error",new{message=ex.Message});}finally{Active.TryRemove(requestId,out _);Gate.Release();}
    }
}
