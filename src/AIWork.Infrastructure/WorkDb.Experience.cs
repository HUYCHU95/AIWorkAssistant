using AIWork.Domain;
using Microsoft.EntityFrameworkCore;
namespace AIWork.Infrastructure;
public partial class WorkDb
{
    public DbSet<ExperienceSettings> Experience=>Set<ExperienceSettings>();
    public DbSet<AnalysisDraft> AnalysisDrafts=>Set<AnalysisDraft>();
    public DbSet<ChatTurn> ChatTurns=>Set<ChatTurn>();
    partial void ConfigureExperience(ModelBuilder m)
    {
        m.Entity<AnalysisDraft>().HasIndex(d=>d.RequestKey).IsUnique();m.Entity<AnalysisDraft>().HasOne<SourceRecord>().WithMany().HasForeignKey(d=>d.SourceId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ChatTurn>().HasIndex(t=>t.RequestId).IsUnique();m.Entity<ChatTurn>().HasOne<Conversation>().WithMany().HasForeignKey(t=>t.ConversationId).OnDelete(DeleteBehavior.Restrict);m.Entity<ChatTurn>().HasOne<SourceRecord>().WithMany().HasForeignKey(t=>t.SourceId).OnDelete(DeleteBehavior.Restrict);
    }
}
