using Microsoft.EntityFrameworkCore;
using QQBOT.EntityFrameworkCore.Entity.Audit;

namespace QQBOT.EntityFrameworkCore
{
    public class BotDbContext : DbContext
    {
        public DbSet<AuditLog> Logs { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlServer(@"Server=localhost; Database=QQBOT_DB; Trusted_Connection=True");
    }
}