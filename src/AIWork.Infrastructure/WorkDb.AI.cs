using AIWork.Domain;
using Microsoft.EntityFrameworkCore;
namespace AIWork.Infrastructure;
public partial class WorkDb
{
    public DbSet<Conversation> Conversations=>Set<Conversation>();
    public DbSet<ChatMessage> Messages=>Set<ChatMessage>();
    public DbSet<AIUsage> AIUsages=>Set<AIUsage>();
    public DbSet<AppPreferences> Preferences=>Set<AppPreferences>();
    partial void ConfigureAI(ModelBuilder m)
    {
        m.Entity<Conversation>().HasOne<Project>().WithMany().HasForeignKey(c=>c.ProjectId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<Conversation>().HasOne<WorkTask>().WithMany().HasForeignKey(c=>c.TaskId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ChatMessage>().HasOne<Conversation>().WithMany().HasForeignKey(c=>c.ConversationId).OnDelete(DeleteBehavior.Restrict);
        m.Entity<ChatMessage>().HasIndex(c=>new{c.ConversationId,c.CreatedAtUtc});
        m.Entity<AIUsage>().HasIndex(u=>u.CorrelationId);
    }
}
