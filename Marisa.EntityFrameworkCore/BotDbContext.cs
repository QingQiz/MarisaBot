using System.Linq;
using Marisa.EntityFrameworkCore.Entity;
using Marisa.EntityFrameworkCore.Entity.Plugin;
using Marisa.EntityFrameworkCore.Entity.Plugin.Arcaea;
using Marisa.EntityFrameworkCore.Entity.Plugin.Chunithm;
using Marisa.EntityFrameworkCore.Entity.Plugin.MaiMaiDx;
using Marisa.EntityFrameworkCore.Entity.Plugin.Ongeki;
using Marisa.EntityFrameworkCore.Entity.Plugin.Osu;
using Microsoft.EntityFrameworkCore;

namespace Marisa.EntityFrameworkCore;

public class BotDbContext : DbContext
{
    public DbSet<CommandFilter> CommandFilters { get; set; }
    public DbSet<ChunithmGuess> ChunithmGuesses { get; set; }
    public DbSet<ChunithmBind> ChunithmBinds { get; set; }
    public DbSet<OngekiGuess> OngekiGuesses { get; set; }
    public DbSet<MaiMaiDxGuess> MaiMaiDxGuesses { get; set; }
    public DbSet<MaiMaiDxBind> MaiMaiBinds { get; set; }
    public DbSet<ArcaeaGuess> ArcaeaGuesses { get; set; }
    public DbSet<BlackList> BlackLists { get; set; }
    public DbSet<OsuBind> OsuBinds { get; set; }
    public DbSet<OsuUserHistory> OsuUserHistories { get; set; }
    public DbSet<Meal> Meals { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        options.UseSqlite(@"Data Source=F:\MarisaBotTemp\QQBOT_DB.db");
    }
}

public static class DbContextExt
{
    public static void InsertOrUpdate<T>(this DbSet<T> context, T value) where T : HaveUId
    {
        if (!context.Any(t => t.UId == value.UId))
        {
            context.Add(value);
            return;
        }

        context.Update(value);
    }
}