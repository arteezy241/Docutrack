using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using DocuTrack.Infrastructure.Data;

namespace DocuTrack.Infrastructure
{
    public class DocuTrackDbContextFactory : IDesignTimeDbContextFactory<DocuTrackDbContext>
    {
        public DocuTrackDbContext CreateDbContext(string[] args)
        {
            var builder = new DbContextOptionsBuilder<DocuTrackDbContext>();
            var connectionString = "Data Source=docutrack.db";
            builder.UseSqlite(connectionString);

            return new DocuTrackDbContext(builder.Options);
        }
    }
}
