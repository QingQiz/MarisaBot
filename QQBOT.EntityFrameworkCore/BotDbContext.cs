using System.Linq;
using Microsoft.EntityFrameworkCore;
using QQBot.EntityFrameworkCore.Entity.Audit;
using QQBot.EntityFrameworkCore.Entity.Plugin;
using QQBot.EntityFrameworkCore.Entity.Plugin.MaiMaiDx;

namespace QQBot.EntityFrameworkCore
{
    public class BotDbContext : DbContext
    {
        public DbSet<AuditLog> Logs { get; set; }
        public DbSet<Timer> Timers { get; set; }

        public DbSet<MaiMaiDxGuess> MaiMaiDxGuesses { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseSqlServer(@"Server=localhost; Database=QQBOT_DB; Trusted_Connection=True");
        }
    }

    public static class DbContextExt
    {
        public static void InsertOrUpdate(this DbSet<MaiMaiDxGuess> context, MaiMaiDxGuess value)
        {
            if (!context.Any(t => t.UId == value.UId))
            {
                context.Add(value);
                return;
            }

            context.Update(value);
        }
    }
}