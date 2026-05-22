using Microsoft.EntityFrameworkCore;
using DocuTrack.Core.Models;

namespace DocuTrack.Infrastructure.Data
{
    public class DocuTrackDbContext : DbContext
    {
        public DocuTrackDbContext(DbContextOptions<DocuTrackDbContext> options)
            : base(options)
        {
        }
        public DbSet<College> Colleges { get; set; }

        public DbSet<RoutingTemplate> RoutingTemplates { get; set; }
        public DbSet<Document> Documents { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<RoutingEvent> RoutingEvents { get; set; } = null!;
        public DbSet<Department> Departments { get; set; } = null!;
        public DbSet<PushSubscription> PushSubscriptions { get; set; } = null!;
        public DbSet<QrSession> QrSessions { get; set; } = null!;

        public DbSet<WorkflowRule> WorkflowRules { get; set; } = null!;
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<TrustedDevice> TrustedDevices { get; set; }

        public DbSet<ApiKey> ApiKeys { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)

        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Document>(b =>
            {
                b.HasKey(d => d.Id);
                b.HasOne(d => d.Owner)
                 .WithMany()
                 .HasForeignKey(d => d.OwnerId)
                 .OnDelete(DeleteBehavior.SetNull);
            });
            
            modelBuilder.Entity<PushSubscription>(b =>
            {
                b.HasKey(p => p.Id);
            });

            modelBuilder.Entity<QrSession>(b =>
            {
                b.HasKey(q => q.Id);
            });
            modelBuilder.Entity<WorkflowRule>(b =>
            {
                b.HasKey(w => w.Id);
                b.HasOne(w => w.AssignToUser)
                 .WithMany()
                 .HasForeignKey(w => w.AssignToUserId)
                 .OnDelete(DeleteBehavior.SetNull);
            });
            modelBuilder.Entity<User>(b =>
            {
                b.HasKey(u => u.Id);
                b.HasOne(u => u.Department)
                 .WithMany(d => d.Users)
                 .HasForeignKey(u => u.DepartmentId)
                 .OnDelete(DeleteBehavior.SetNull);
            });

            modelBuilder.Entity<RoutingEvent>(b =>
            {
                b.HasKey(r => r.Id);
                b.HasOne<User>()
                 .WithMany()
                 .HasForeignKey(r => r.FromUserId)
                 .OnDelete(DeleteBehavior.ClientSetNull);

                b.HasOne<User>()
                 .WithMany()
                 .HasForeignKey(r => r.ToUserId)
                 .OnDelete(DeleteBehavior.ClientSetNull);

                b.HasOne<Document>()
                 .WithMany(d => d.RoutingHistory)
                 .HasForeignKey(r => r.DocumentId)
                 .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Department>(b =>
            {
                b.HasKey(d => d.Id);
                b.HasMany(d => d.Users)
                 .WithOne(u => u.Department)
                 .HasForeignKey(u => u.DepartmentId)
                 .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}