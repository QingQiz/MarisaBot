using System.Linq;
using Marisa.EntityFrameworkCore.Entity.Audit;
using Marisa.EntityFrameworkCore.Entity.Plugin.Arcaea;
using Marisa.EntityFrameworkCore.Entity.Plugin.MaiMaiDx;
using Marisa.EntityFrameworkCore.Entity.Plugin.Shared;
using Microsoft.EntityFrameworkCore;

namespace Marisa.EntityFrameworkCore;

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