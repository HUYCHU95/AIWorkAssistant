using System.ComponentModel.DataAnnotations;
namespace AIWork.Domain;
public class InboxItem:Record
{
    public Guid SourceId{get;set;}
    public Guid? ProjectId{get;set;}
    public string Status{get;set;}="NeedsReview";
    public string ReviewReason{get;set;}="PIC/ngày hạn và nội dung cần người dùng kiểm tra.";
    public string CaptureKey{get;set;}="";
    public DateTime? ReviewedAtUtc{get;set;}
}
public class DocumentRecord:Record
{
    public Guid SourceId{get;set;}
    public string OriginalFileName{get;set;}="";
    public string StoredName{get;set;}="";
    public string ContentType{get;set;}="application/octet-stream";
    public long LengthBytes{get;set;}
    public string Sha256{get;set;}="";
    public string ExtractionStatus{get;set;}="Unsupported — file stored safely, not read";
    public string? ExtractedText{get;set;}
}
public class MemoryFact:Record
{
    [Required,StringLength(30)]public string SubjectType{get;set;}="Project";
    public Guid SubjectId{get;set;}
    [Required,StringLength(100)]public string Predicate{get;set;}="";
    [Required,StringLength(4000)]public string ValueText{get;set;}="";
    public Guid SourceId{get;set;}
    public DateTime? EffectiveAtUtc{get;set;}
    public Verification Verification{get;set;}
    public bool IsCurrent{get;set;}=true;
    public Guid? SupersededById{get;set;}
    public Guid? ConflictsWithId{get;set;}
    [StringLength(1000)]public string? ConfidenceNotes{get;set;}
}
public class MemoryRevision:Record
{
    public Guid FactId{get;set;}
    public string Reason{get;set;}="";
    public string BeforeJson{get;set;}="";
    public string AfterJson{get;set;}="";
}
