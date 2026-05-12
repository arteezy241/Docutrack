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

        public DbSet<Document> Documents { get; set; } = null!;
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<RoutingEvent> RoutingEvents { get; set; } = null!;
        public DbSet<Department> Departments { get; set; } = null!;
        public DbSet<PushSubscription> PushSubscriptions { get; set; } = null!;

        public DbSet<WorkflowRule> WorkflowRules { get; set; } = null!;

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
                 .WithOne()
                 .OnDelete(DeleteBehavior.SetNull);
            });
        }
    }
}