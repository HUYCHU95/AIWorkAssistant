using AIWork.Domain;
using AIWork.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
namespace AIWork.Web.Controllers;
public class AnalysisController(WorkDb db,TrackingService tracking,AttachmentService attachments,AnalysisService analysis):ManagementController(db)
{
    private static readonly SemaphoreSlim Gate=new(1,1);
    public IActionResult Index()=>View();
    [HttpPost,RequestSizeLimit(30*1024*1024)]
    public async Task<IActionResult> Stage(string requestKey,string targetKind,string? text,bool confidential=true,List<IFormFile>? files=null,CancellationToken cancellationToken=default)
    {
        if(!Guid.TryParse(requestKey,out _)||!AnalysisService.Kinds.Contains(targetKind)||text?.Length>60000)return BadRequest("Yêu cầu không hợp lệ.");
        var old=await Db.AnalysisDrafts.SingleOrDefaultAsync(d=>d.RequestKey==requestKey);if(old!=null)return await Result(old.Id);
        files??=[];if(files.Count>8||files.Sum(f=>f.Length)>25*1024*1024||files.Any(f=>f.Length>5*1024*1024))return BadRequest("Tối đa 8 file, mỗi file 5 MB và tổng 25 MB.");
        if(files.Count==0&&string.IsNullOrWhiteSpace(text))return BadRequest("Nhập nội dung hoặc chọn file.");
        try
        {
            var checkedFiles=new List<CheckedAttachment>();foreach(var f in files){using var ms=new MemoryStream();await f.CopyToAsync(ms,cancellationToken);checkedFiles.Add(AttachmentService.Check(f.FileName,ms.ToArray()));}
            await using var tx=await Db.Database.BeginTransactionAsync(cancellationToken);
            var source=tracking.Capture(SourceKind.File,"Tài liệu nhập · "+DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"),string.IsNullOrWhiteSpace(text)?"File gốc: "+string.Join(", ",files.Select(f=>f.FileName)):text,null,null,confidential);
            foreach(var f in checkedFiles)await attachments.Store(source.Id,f);
            var draft=new AnalysisDraft{RequestKey=requestKey,SourceId=source.Id,TargetKind=targetKind};Db.AnalysisDrafts.Add(draft);await Db.SaveChangesAsync(cancellationToken);await tx.CommitAsync(cancellationToken);return await Result(draft.Id);
        }catch(ValidationException ex){return BadRequest(ex.Message);}
    }
    public async Task<IActionResult> Result(Guid id)
    {
        var draft=await Db.AnalysisDrafts.FindAsync(id);if(draft==null)return NotFound();var docs=await Db.Documents.Where(d=>d.SourceId==draft.SourceId).Select(d=>new{d.Id,d.OriginalFileName,d.ExtractionStatus,d.ExtractedText}).ToListAsync();
        return Json(new{draft.Id,draft.SourceId,draft.Status,draft.ErrorCode,result=draft.ResultJson==null?null:JsonSerializer.Deserialize<AnalysisResult>(draft.ResultJson),documents=docs,sourceHref="/Sources/Details/"+draft.SourceId});
    }
    [HttpPost]public async Task<IActionResult> Analyze(Guid id,CancellationToken cancellationToken)
    {
        if(!await Gate.WaitAsync(0,cancellationToken))return Conflict("Một phân tích khác đang chạy.");try{await analysis.Analyze(id,cancellationToken);return await Result(id);}catch(ValidationException ex){return BadRequest(ex.Message);}finally{Gate.Release();}
    }
    [HttpPost]public async Task<IActionResult> Proposals([FromBody]AnalysisReview input)
    {
        if(!ModelState.IsValid)return BadRequest("Đề xuất không hợp lệ.");try{return Json(await analysis.Proposals(input.Id,input.Items));}catch(ValidationException ex){return BadRequest(ex.Message);}
    }
}
public class AnalysisReview{public Guid Id{get;set;}public List<AnalysisSuggestion> Items{get;set;}=[];}
