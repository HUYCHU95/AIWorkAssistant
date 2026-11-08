using AIWork.Domain;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.IO.Compression;
namespace AIWork.Infrastructure;
public record CheckedAttachment(string Name,string Extension,string Mime,byte[] Bytes,string Hash,string? Text);
public class AttachmentService(WorkDb db,string directory)
{
    public static CheckedAttachment Check(string fileName,byte[] bytes)
    {
        var name=Path.GetFileName(fileName);var ext=Path.GetExtension(name).ToLowerInvariant();if(name.Length>200||name!=fileName||name.Contains('\\')||name.Contains('/')||name.Contains(':')||name.Contains('\0')||bytes.Length==0||bytes.Length>5*1024*1024)throw new ValidationException("Tên/đường dẫn hoặc kích thước tệp không hợp lệ (tối đa 5 MB).");
        var mime=ext switch{".txt"=>"text/plain",".pdf"=>"application/pdf",".docx"=>"application/vnd.openxmlformats-officedocument.wordprocessingml.document",".xlsx"=>"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",".webp"=>"image/webp",".gif"=>"image/gif",".png"=>"image/png",".jpg" or ".jpeg"=>"image/jpeg",_=>throw new ValidationException("Định dạng không được phép.")};
        string? text=null;
        if(ext==".txt"){try{text=new UTF8Encoding(false,true).GetString(bytes);if(text.Contains('\0'))throw new ValidationException("Tệp text chứa dữ liệu nhị phân.");if(text.Length>60000)text=null;}catch(DecoderFallbackException){throw new ValidationException("Tệp text cần mã hóa UTF-8.");}}
        else if(ext==".pdf"&&!bytes.AsSpan().StartsWith("%PDF-"u8))throw new ValidationException("Chữ ký PDF không hợp lệ.");
        else if(ext==".png"&&!bytes.AsSpan().StartsWith(new byte[]{137,80,78,71,13,10,26,10}))throw new ValidationException("Chữ ký PNG không hợp lệ.");
        else if((ext==".jpg"||ext==".jpeg")&&!bytes.AsSpan().StartsWith(new byte[]{255,216,255}))throw new ValidationException("Chữ ký JPEG không hợp lệ.");
        else if(ext==".webp"&&!(bytes.Length>12&&bytes.AsSpan(0,4).SequenceEqual("RIFF"u8)&&bytes.AsSpan(8,4).SequenceEqual("WEBP"u8)))throw new ValidationException("Chữ ký WebP không hợp lệ.");
        else if(ext==".gif"&&!(bytes.AsSpan().StartsWith("GIF87a"u8)||bytes.AsSpan().StartsWith("GIF89a"u8)))throw new ValidationException("Chữ ký GIF không hợp lệ.");
        else if(ext is ".docx" or ".xlsx")
        {
            try{using var stream=new MemoryStream(bytes);using var zip=new ZipArchive(stream);if(zip.Entries.Count>1000||zip.Entries.Sum(e=>e.Length)>20*1024*1024||zip.GetEntry("[Content_Types].xml")==null||zip.Entries.Any(e=>e.FullName.Contains("vbaProject",StringComparison.OrdinalIgnoreCase))||!zip.Entries.Any(e=>e.FullName.StartsWith(ext==".docx"?"word/":"xl/",StringComparison.Ordinal)))throw new ValidationException("Tệp Office không hợp lệ hoặc chứa macro/quá lớn.");}catch(InvalidDataException){throw new ValidationException("Tệp Office không đúng ZIP signature.");}
        }
        return new(name,ext,mime,bytes,Convert.ToHexString(SHA256.HashData(bytes)),text);
    }
    public async Task<DocumentRecord> Store(Guid sourceId,CheckedAttachment file)
    {
        var duplicate=db.Documents.Local.FirstOrDefault(d=>d.SourceId==sourceId&&d.Sha256==file.Hash)??await db.Documents.FirstOrDefaultAsync(d=>d.SourceId==sourceId&&d.Sha256==file.Hash);if(duplicate!=null)return duplicate;
        var extracted=DocumentExtraction.Read(file);
        Directory.CreateDirectory(directory);var stored=Guid.NewGuid().ToString("N")+file.Extension;await File.WriteAllBytesAsync(Path.Combine(directory,stored),file.Bytes);
        var doc=new DocumentRecord{SourceId=sourceId,OriginalFileName=file.Name,StoredName=stored,ContentType=file.Mime,LengthBytes=file.Bytes.Length,Sha256=file.Hash,ExtractedText=extracted.Text,ExtractionStatus=extracted.Status};db.Documents.Add(doc);return doc;
    }
    public string PathFor(DocumentRecord doc)
    {
        if(Path.GetFileName(doc.StoredName)!=doc.StoredName||doc.StoredName.Contains('\\')||doc.StoredName.Contains('/'))throw new ValidationException("Đường dẫn lưu trữ không hợp lệ.");
        return Path.Combine(directory,doc.StoredName);
    }
}
public class InboxService(WorkDb db,TrackingService tracking)
{
    public async Task<InboxItem> Capture(SourceKind kind,string title,string body,DateTime? occurred,string? sender,bool confidential,Guid? projectId,string key)
    {
        if(!Guid.TryParse(key,out _))throw new ValidationException("Capture key không hợp lệ.");var old=await db.InboxItems.SingleOrDefaultAsync(i=>i.CaptureKey==key);if(old!=null)return old;
        await tracking.ValidateLinks(projectId,null,null);var source=tracking.Capture(kind,title,body,occurred,sender,confidential);var item=new InboxItem{SourceId=source.Id,ProjectId=projectId,CaptureKey=key};db.InboxItems.Add(item);if(projectId!=null)db.SourceLinks.Add(new SourceLink{SourceId=source.Id,EntityType="Project",EntityId=projectId.Value});await db.SaveChangesAsync();return item;
    }
    public async Task<int> ExtractChecklist(Guid id)
    {
        var item=await db.InboxItems.FindAsync(id)??throw new ValidationException("Không có Inbox item.");var source=await db.Sources.FindAsync(item.SourceId)??throw new ValidationException("Nguồn bị thiếu.");var created=0;var lineNumber=0;
        foreach(var raw in source.OriginalText.Split('\n'))
        {
            lineNumber++;var match=Regex.Match(raw.Trim(),@"^(?:-\s*(?:\[\s*\])?\s*|\d+[.)]\s+|TASK:\s*|Việc:\s*)(.+)$",RegexOptions.IgnoreCase);if(!match.Success)continue;var title=match.Groups[1].Value.Trim();if(title.Length==0||title.Length>500)continue;
            var key="inbox:"+source.Id+":"+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));if(await db.Actions.AnyAsync(a=>a.IdempotencyKey==key)||db.Actions.Local.Any(a=>a.IdempotencyKey==key))continue;
            db.Actions.Add(new ActionReceipt{OriginSourceId=source.Id,ToolName="create_task",ArgumentsJson=JsonSerializer.Serialize(new ActionArguments{Title=title,Body=$"Nguồn {source.Id}; dòng {lineNumber}; trích nguyên văn: {raw}",ProjectId=item.ProjectId,SourceId=source.Id},ActionService.Json),IdempotencyKey=key,Summary=$"Rà soát dòng {lineNumber}; chưa rõ PIC/ngày hạn. Chưa tạo công việc.",RiskLevel="A2"});created++;
        }
        item.Status="NeedsReview";item.ReviewReason=created>0?$"Trích checklist theo quy tắc cục bộ, {created} đề xuất mới. Chưa gọi AI; cần duyệt từng mục.":"Không có checklist mới rõ ràng. Chưa chạy AI hoặc suy đoán nhiệm vụ; bạn có thể tạo hồ sơ thủ công.";await db.SaveChangesAsync();return created;
    }
}
public class MemoryService(WorkDb db,TrackingService tracking)
{
    public async Task<MemoryFact> Add(MemoryFact input)
    {
        TrackingService.Validate(input);if(input.SubjectType is not("Project" or "Task" or "TrackingEntry"))throw new ValidationException("Loại chủ thể không hợp lệ.");await tracking.Link(input.SourceId,input.SubjectType,input.SubjectId);
        var existing=await db.MemoryFacts.Where(f=>f.SubjectType==input.SubjectType&&f.SubjectId==input.SubjectId&&f.Predicate==input.Predicate&&f.IsCurrent).ToListAsync();
        var same=existing.FirstOrDefault(f=>f.ValueText==input.ValueText&&f.SourceId==input.SourceId&&f.EffectiveAtUtc==input.EffectiveAtUtc);if(same!=null)return same;
        input.Verification=existing.Any(f=>f.ValueText!=input.ValueText)?Verification.Conflicted:Verification.Reported;input.IsCurrent=true;input.ConflictsWithId=existing.FirstOrDefault(f=>f.ValueText!=input.ValueText)?.Id;
        input.ConfidenceNotes="Nguồn báo cáo; chưa phải xác nhận giao/nhận/nghiệm thu.";db.MemoryFacts.Add(input);await db.SaveChangesAsync();return input;
    }
    public async Task Verify(Guid id,int version,string reason)
    {
        if(string.IsNullOrWhiteSpace(reason)||reason.Length>1000)throw new ValidationException("Cần lý do/bằng chứng xác minh.");await using var tx=await db.Database.BeginTransactionAsync();var fact=await db.MemoryFacts.FindAsync(id)??throw new ValidationException("Không tìm thấy dữ kiện.");if(fact.Version!=version||fact.Verification==Verification.Superseded)throw new ValidationException("Dữ kiện đã thay đổi.");db.Entry(fact).Property(f=>f.Version).OriginalValue=version;
        var others=await db.MemoryFacts.Where(f=>f.Id!=id&&f.SubjectType==fact.SubjectType&&f.SubjectId==fact.SubjectId&&f.Predicate==fact.Predicate&&f.IsCurrent).ToListAsync();
        foreach(var old in others){var before=JsonSerializer.Serialize(old);old.IsCurrent=false;old.Verification=Verification.Superseded;old.SupersededById=fact.Id;db.MemoryRevisions.Add(new MemoryRevision{FactId=old.Id,BeforeJson=before,AfterJson=JsonSerializer.Serialize(old),Reason=reason});}
        var oldJson=JsonSerializer.Serialize(fact);fact.Verification=Verification.Verified;fact.ConfidenceNotes=reason;db.MemoryRevisions.Add(new MemoryRevision{FactId=fact.Id,BeforeJson=oldJson,AfterJson=JsonSerializer.Serialize(fact),Reason=reason});await db.SaveChangesAsync();await tx.CommitAsync();
    }
}
