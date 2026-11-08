using AIWork.Domain;
using Microsoft.EntityFrameworkCore;
namespace AIWork.Infrastructure;
public partial class WorkDb
{
    public DbSet<InboxItem> InboxItems=>Set<InboxItem>();public DbSet<DocumentRecord> Documents=>Set<DocumentRecord>();public DbSet<MemoryFact> MemoryFacts=>Set<MemoryFact>();public DbSet<MemoryRevision> MemoryRevisions=>Set<MemoryRevision>();
    partial void ConfigureMemory(ModelBuilder m)
    {
        m.Entity<InboxItem>().HasIndex(i=>i.CaptureKey).IsUnique();m.Entity<InboxItem>().HasOne<SourceRecord>().WithMany().HasForeignKey(i=>i.SourceId).OnDelete(DeleteBehavior.Restrict);m.Entity<InboxItem>().HasOne<Project>().WithMany().HasForeignKey(i=>i.ProjectId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<DocumentRecord>().HasIndex(d=>d.StoredName).IsUnique();m.Entity<DocumentRecord>().HasOne<SourceRecord>().WithMany().HasForeignKey(d=>d.SourceId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<MemoryFact>().HasIndex(f=>new{f.SubjectType,f.SubjectId,f.Predicate,f.IsCurrent});m.Entity<MemoryFact>().HasOne<SourceRecord>().WithMany().HasForeignKey(f=>f.SourceId).OnDelete(DeleteBehavior.Restrict);m.Entity<MemoryFact>().HasOne<MemoryFact>().WithMany().HasForeignKey(f=>f.SupersededById).OnDelete(DeleteBehavior.Restrict);m.Entity<MemoryFact>().HasOne<MemoryFact>().WithMany().HasForeignKey(f=>f.ConflictsWithId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<MemoryRevision>().HasOne<MemoryFact>().WithMany().HasForeignKey(f=>f.FactId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<MemoryFact>().Property(f=>f.Version).IsConcurrencyToken();m.Entity<InboxItem>().Property(f=>f.Version).IsConcurrencyToken();
    }
}
