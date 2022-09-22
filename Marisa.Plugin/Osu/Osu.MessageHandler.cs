using Flurl.Http;
using Marisa.EntityFrameworkCore;
using Marisa.EntityFrameworkCore.Entity.Plugin.Osu;
using Marisa.Plugin.Shared.Osu;
using Marisa.Plugin.Shared.Osu.Drawer;
using Microsoft.EntityFrameworkCore;

namespace Marisa.Plugin.Osu;

[MarisaPlugin(PluginPriority.Osu)]
[MarisaPluginDoc("音游 osu! 的相关功能，在子命令中可以使用 [name][#rank][:mode] 指定查询目标")]
[MarisaPluginCommand("osu!", "osu", "!", "！")]
public partial class Osu : MarisaPluginBase
{
    #region 绑定相关

    [MarisaPluginDoc("绑定一个 osu 账号，参数为：osu 的用户名")]
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

    [MarisaPluginDoc("设置当前绑定的账户的默认模式，参数为：osu、taiko、catch 和 mania")]
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

    [MarisaPluginDoc("查询某人的个人信息")]
    [MarisaPluginCommand("info")]
    private async Task<MarisaPluginTaskState> Info(Message message)
    {
        if (DebounceCheck(message))
        {
            return MarisaPluginTaskState.CompletedTask;
        }

        try
        {
            var command = ParseCommand(message);

            if (command == null)
            {
                message.Reply("错误的命令格式");
                return MarisaPluginTaskState.CompletedTask;
            }

            if (string.IsNullOrWhiteSpace(command.Name))
            {
                message.Reply("您是？");
                return MarisaPluginTaskState.CompletedTask;
            }

            var uInfo = await OsuApi.GetUserInfoByName(command.Name, command.Mode?.Value ?? -1);

            if (uInfo.RankHistory == null)
            {
                message.Reply("该玩家没有玩过该模式");
            }
            else
            {
                var img = await uInfo.GetImage();

                message.Reply(MessageDataImage.FromBase64(img.ToB64(100)));
            }
        }
        finally
        {
            DebounceCancel(message.Sender!.Id);
        }

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("查询某人最近通过的图")]
    [MarisaPluginCommand("pr")]
    private async Task<MarisaPluginTaskState> RecentPass(Message message)
    {
        var command = ParseCommand(message, true);

        if (command == null)
        {
            message.Reply("错误的命令格式");
            return MarisaPluginTaskState.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            message.Reply("您是？");
            return MarisaPluginTaskState.CompletedTask;
        }

        var mode = command.Mode?.Value ?? 0;

        if (mode != 3)
        {
            message.Reply("目前未实现");
            return MarisaPluginTaskState.CompletedTask;
        }

        var bpRank = command.BpRank?.Value ?? 1;

        var userInfo = await OsuApi.GetUserInfoByName(command.Name);

        var recentScores = await OsuApi.GetScores(userInfo.Id, "recent", OsuApi.GetModeName(mode), bpRank - 1, 1, false);

        if (!recentScores!.Any())
        {
            message.Reply("无");
            return MarisaPluginTaskState.CompletedTask;
        }

        var img = await recentScores![0].GetImage(userInfo);

        message.Reply(
            new MessageDataText("歌曲详情还没画，先不要急"),
            MessageDataImage.FromBase64(img.ToB64(100))
        );

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("查询某人最近打的图")]
    [MarisaPluginCommand("recent", "rec", "re")]
    private async Task<MarisaPluginTaskState> Recent(Message message, BotDbContext db)
    {
        var command = ParseCommand(message, true);

        if (command == null)
        {
            message.Reply("错误的命令格式");
            return MarisaPluginTaskState.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            message.Reply("您是？");
            return MarisaPluginTaskState.CompletedTask;
        }

        var mode = command.Mode?.Value ?? 0;

        if (mode != 3)
        {
            message.Reply("目前未实现");
            return MarisaPluginTaskState.CompletedTask;
        }

        var bpRank = command.BpRank?.Value ?? 1;

        var userInfo = await OsuApi.GetUserInfoByName(command.Name);

        var recentScores = await OsuApi.GetScores(userInfo.Id, "recent", OsuApi.GetModeName(mode), bpRank - 1, 1, true);

        if (!recentScores!.Any())
        {
            message.Reply("无");
            return MarisaPluginTaskState.CompletedTask;
        }

        var img = await recentScores![0].GetImage(userInfo);

        message.Reply(
            new MessageDataText("歌曲详情还没画，先不要急"),
            MessageDataImage.FromBase64(img.ToB64(100))
        );

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("查询某人 pp 最高的成绩图（bp）")]
    [MarisaPluginCommand("bp")]
    private async Task<MarisaPluginTaskState> BestPerformance(Message message, BotDbContext db)
    {
        var command = ParseCommand(message, true);

        if (command == null)
        {
            message.Reply("错误的命令格式");
            return MarisaPluginTaskState.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            message.Reply("您是？");
            return MarisaPluginTaskState.CompletedTask;
        }

        var mode = command.Mode?.Value ?? 0;

        if (mode != 3)
        {
            message.Reply("目前未实现");
            return MarisaPluginTaskState.CompletedTask;
        }

        var bpRank = command.BpRank?.Value ?? 1;

        var userInfo = await OsuApi.GetUserInfoByName(command.Name);

        var recentScores = await OsuApi.GetScores(userInfo.Id, "best", OsuApi.GetModeName(mode), bpRank - 1, 1);

        if (!recentScores!.Any())
        {
            message.Reply("无");
            return MarisaPluginTaskState.CompletedTask;
        }

        var img = await recentScores![0].GetImage(userInfo);

        message.Reply(
            new MessageDataText("歌曲详情还没画，先不要急"),
            MessageDataImage.FromBase64(img.ToB64(100))
        );

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("查询某人今天恰到的 pp")]
    [MarisaPluginCommand("todaybp")]
    private async Task<MarisaPluginTaskState> TodayBp(Message message, BotDbContext db)
    {
        message.Reply("服务暂时不可用！");
        // await ParseCommand(message, "todaybp");
        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("查询 *自己* 在某张图上的成绩")]
    [MarisaPluginCommand("score")]
    private Task<MarisaPluginTaskState> Score(Message message)
    {
        AddCommandToQueue(message);
        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    [MarisaPluginDoc("查询打 rank 图奖励的 pp (bonus pp)")]
    [MarisaPluginCommand("bonusPP")]
    private async Task<MarisaPluginTaskState> BonusPp(Message message)
    {
        message.Reply("服务暂时不可用！");
        // await ParseCommand(message, "bonuspp");
        return MarisaPluginTaskState.CompletedTask;
    }

    #endregion
}