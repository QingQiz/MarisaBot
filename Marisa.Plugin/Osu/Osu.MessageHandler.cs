using Flurl.Http;
using Marisa.EntityFrameworkCore;
using Marisa.EntityFrameworkCore.Entity.Plugin.Osu;
using Marisa.Plugin.Shared.Osu;
using Microsoft.EntityFrameworkCore;

namespace Marisa.Plugin.Osu;

public partial class Osu
{
    #region 绑定相关

    [MarisaPluginCommand("bind")]
    private static async Task<MarisaPluginTaskState> Bind(Message message, BotDbContext dbContext)
    {
        var name   = message.Command.Trim();
        var sender = message.Sender!.Id;

        if (string.IsNullOrEmpty(name))
        {
            message.Reply("？");
            return MarisaPluginTaskState.CompletedTask;
        }

        try
        {
            var info = await OsuApi.GetUserInfoByName(name);

            if (dbContext.OsuBinds.Any(o => o.OsuUserId == info.Id))
            {
                message.Reply($"名为 '{info.Username}' 的 osu 用户已被绑定。。");
            }
            else if (dbContext.OsuBinds.Any(o => o.UserId == sender))
            {
                var bind = dbContext.OsuBinds.First(o => o.UserId == sender);

                bind.OsuUserId   = info.Id;
                bind.OsuUserName = info.Username;
                bind.GameMode    = OsuApi.ModeList[0];
                await dbContext.SaveChangesAsync();
                message.Reply("好了");
            }
            else
            {
                await dbContext.OsuBinds.AddAsync(new OsuBind
                {
                    UserId      = sender,
                    GameMode    = OsuApi.ModeList[0],
                    OsuUserId   = info.Id,
                    OsuUserName = info.Username
                });
                await dbContext.SaveChangesAsync();
                message.Reply("好了");
            }
        }
        catch (FlurlHttpException e) when (e.StatusCode == 404)
        {
            message.Reply("NotFound");
        }

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginCommand("setMode", "set mode", "mode")]
    private static async Task<MarisaPluginTaskState> SetMode(Message message, BotDbContext db)
    {
        var sender = message.Sender!.Id;
        var mode   = message.Command.Trim();

        if (!OsuApi.ModeList.Contains(mode))
        {
            message.Reply("可选的模式：" + string.Join(", ", OsuApi.ModeList));
            return MarisaPluginTaskState.CompletedTask;
        }

        if (!db.OsuBinds.Any(o => o.UserId == sender))
        {
            message.Reply("您是？");
            return MarisaPluginTaskState.CompletedTask;
        }

        var o = await db.OsuBinds.FirstAsync(u => u.UserId == sender);
        o.GameMode = mode;
        db.OsuBinds.Update(o);
        await db.SaveChangesAsync();
        message.Reply("好了");

        return MarisaPluginTaskState.CompletedTask;
    }

    #endregion

    #region 查询相关（继承了猫猫的命令）

    [MarisaPluginCommand("info")]
    private async Task<MarisaPluginTaskState> Info(Message message)
    {
        await RunCommand(message, "info");
        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginCommand("pr")]
    private async Task<MarisaPluginTaskState> RecentPass(Message message, BotDbContext db)
    {
        await RunCommand(message, "pr");
        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginCommand("recent")]
    private async Task<MarisaPluginTaskState> Recent(Message message, BotDbContext db)
    {
        await RunCommand(message, "recent");
        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginCommand("bp")]
    private async Task<MarisaPluginTaskState> BestPerformance(Message message, BotDbContext db)
    {
        await RunCommand(message, "bp", true);
        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginCommand("todaybp")]
    private async Task<MarisaPluginTaskState> TodayBp(Message message, BotDbContext db)
    {
        await RunCommand(message, "todaybp");
        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginCommand("score")]
    private async Task<MarisaPluginTaskState> Score(Message message)
    {
        await ReplyMessageByCommand(message, $"score {message.Command}");
        return MarisaPluginTaskState.CompletedTask;
    }

    #endregion
}