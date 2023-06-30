using System.Linq;
using Marisa.EntityFrameworkCore.Entity;
using Marisa.EntityFrameworkCore.Entity.Plugin.Arcaea;
using Marisa.EntityFrameworkCore.Entity.Plugin.Chunithm;
using Marisa.EntityFrameworkCore.Entity.Plugin.MaiMaiDx;
using Marisa.EntityFrameworkCore.Entity.Plugin.Osu;
using Marisa.EntityFrameworkCore.Entity.Plugin.Shared;
using Microsoft.EntityFrameworkCore;

namespace Marisa.EntityFrameworkCore;

public class BotDbContext : DbContext
{
    public DbSet<CommandFilter> CommandFilters { get; set; }
    public DbSet<ChunithmGuess> ChunithmGuesses { get; set; }
    public DbSet<MaiMaiDxGuess> MaiMaiDxGuesses { get; set; }
    public DbSet<ArcaeaGuess> ArcaeaGuesses { get; set; }
    public DbSet<BlackList> BlackLists { get; set; }
    public DbSet<OsuBind> OsuBinds { get; set; }
    public DbSet<OsuUserHistory> OsuUserHistories { get; set; }

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