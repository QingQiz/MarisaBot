using System.Linq;
using Microsoft.EntityFrameworkCore;
using QQBot.EntityFrameworkCore.Entity.Audit;
using QQBot.EntityFrameworkCore.Entity.Plugin.Arcaea;
using QQBot.EntityFrameworkCore.Entity.Plugin.MaiMaiDx;
using QQBot.EntityFrameworkCore.Entity.Plugin.Shared;

namespace QQBot.EntityFrameworkCore
{
    public class BotDbContext : DbContext
    {
        public DbSet<AuditLog> Logs { get; set; }
        public DbSet<MaiMaiDxGuess> MaiMaiDxGuesses { get; set; }
        public DbSet<ArcaeaGuess> ArcaeaGuesses { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseSqlServer(@"Server=localhost; Database=QQBOT_DB; Trusted_Connection=True");
        }
    }

    public static class DbContextExt
    {
        public static void InsertOrUpdate<T>(this DbSet<T> context, T value) where T : SongGuess
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