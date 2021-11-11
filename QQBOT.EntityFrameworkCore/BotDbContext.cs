using Microsoft.EntityFrameworkCore;
using QQBOT.EntityFrameworkCore.Entity.Audit;
using QQBOT.EntityFrameworkCore.Entity.Plugin;

namespace QQBOT.EntityFrameworkCore
{
    public class BotDbContext : DbContext
    {
        public DbSet<AuditLog> Logs { get; set; }
        public DbSet<Timer> Timers { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseSqlServer(@"Server=localhost; Database=QQBOT_DB; Trusted_Connection=True");
    }
}