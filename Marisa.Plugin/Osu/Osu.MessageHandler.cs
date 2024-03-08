using Marisa.EntityFrameworkCore;
using Marisa.EntityFrameworkCore.Entity.Plugin.Osu;
using Marisa.Plugin.Shared.Osu;
using Marisa.Plugin.Shared.Osu.Drawer;
using Microsoft.EntityFrameworkCore;

namespace Marisa.Plugin.Osu;

[MarisaPlugin(PluginPriority.Osu)]
[MarisaPluginDoc("音游 osu! 的相关功能，在子命令中可以使用 [name][#rank][:mode] 指定查询目标")]
[MarisaPluginCommand("osu!", "osu！", "osu", "o", "!", "！")]
public partial class Osu : MarisaPluginBaseWithHelpCommand
{
    #region 绑定 / help

    [MarisaPluginDoc("绑定一个 osu 账号，参数为：osu 的用户名")]
    [MarisaPluginCommand("bind")]
    private static async Task<MarisaPluginTaskState> Bind(Message message, BotDbContext dbContext)
    {
        var name   = message.Command.Trim();
        var sender = message.Sender!.Id;

        if (string.IsNullOrEmpty(name))
        {
            message.Reply("请给出 osu! 的用户名");
            return MarisaPluginTaskState.CompletedTask;
        }

        var info = await OsuApi.GetUserInfoByName(name);

        if (dbContext.OsuBinds.Any(o => o.UserId == sender))
        {
            var bind = dbContext.OsuBinds.First(o => o.UserId == sender);

            bind.OsuUserId   = info.Id;
            bind.OsuUserName = info.Username;
            bind.GameMode    = info.Playmode.ToLower();
            await dbContext.SaveChangesAsync();
            message.Reply("好了");
        }
        else
        {
            await dbContext.OsuBinds.AddAsync(new OsuBind
            {
                UserId      = sender,
                GameMode    = info.Playmode.ToLower(),
                OsuUserId   = info.Id,
                OsuUserName = info.Username
            });
            await dbContext.SaveChangesAsync();
            message.Reply("好了");
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
            message.Reply("您未绑定！");
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
        if (!TryParseCommand(message, false, false, out var command)) return MarisaPluginTaskState.CompletedTask;

        if (DebounceCheck(message, command!.Name))
        {
            return MarisaPluginTaskState.CompletedTask;
        }

        try
        {
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
            DebounceCancel(command.Name);
        }

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("从 AlphaOsu 获取某人的推分推荐")]
    [MarisaPluginCommand("recommend", "什么推分", "打什么推分", "打什么歌推分")]
    private async Task<MarisaPluginTaskState> Recommend(Message message)
    {
        if (!TryParseCommand(message, false, false, out var command)) return MarisaPluginTaskState.CompletedTask;

        if (command!.Mode.Value is not (0 or 3))
        {
            message.Reply("目前只支持 osu 和 mania 模式");
            return MarisaPluginTaskState.CompletedTask;
        }

        var info = await OsuApi.GetUserInfoByName(command.Name);

        var context = new WebContext();

        context.Put("info", info);
        context.Put("recommend", await OsuApi.GetRecommend(info.Id, command.Mode.Value));

        message.Reply(MessageDataImage.FromBase64(await WebApi.OsuRecommend(context.Id)));

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("查询某人最近通过的图")]
    [MarisaPluginCommand("pr")]
    private async Task<MarisaPluginTaskState> RecentPass(Message message)
    {
        if (!TryParseCommand(message, true, false, out var command)) return MarisaPluginTaskState.CompletedTask;

        message.Reply(MessageDataImage.FromBase64(await WebApi.OsuScore(command!.Name, command.Mode.Value,
            command.BpRank?.Value.Item1, true, false)));

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("查询某人最近打的图")]
    [MarisaPluginCommand("recent", "rec", "re")]
    private async Task<MarisaPluginTaskState> Recent(Message message)
    {
        if (!TryParseCommand(message, true, false, out var command)) return MarisaPluginTaskState.CompletedTask;

        message.Reply(MessageDataImage.FromBase64(await WebApi.OsuScore(command!.Name, command.Mode.Value,
            command.BpRank?.Value.Item1, true, true)));

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("查询某人 pp 最高的成绩图（bp）")]
    [MarisaPluginCommand("bp")]
    private async Task<MarisaPluginTaskState> BestPerformance(Message message, BotDbContext db)
    {
        if (!TryParseCommand(message, true, true, out var command)) return MarisaPluginTaskState.CompletedTask;

        if (command!.BpRank?.Value?.Item2 == null)
        {
            message.Reply(MessageDataImage.FromBase64(await WebApi.OsuScore(command!.Name, command.Mode.Value,
                command.BpRank?.Value?.Item1, false, false)));
        }
        else
        {
            var oid  = await GetOsuIdByName(command.Name);
            var mode = OsuApi.GetModeName(command.Mode.Value);
            var best = (await OsuApi.GetScores(
                    oid,
                    OsuApi.OsuScoreType.Best,
                    mode,
                    command.BpRank.Value.Item1 - 1,
                    command.BpRank.Value.Item2.Value - command.BpRank.Value.Item1 + 1
                ))?
                .Select((x, i) => (x, i + command.BpRank.Value.Item1 - 1))
                .ToList();

            if (!(best?.Any() ?? false))
            {
                message.Reply("无");
            }
            else
            {
                message.Reply(MessageDataImage.FromBase64(best.GetMiniCards().ToB64(100)));
            }
        }

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("将 *你的* bp 和别人的 bp 进行比较")]
    [MarisaPluginSubCommand(nameof(BestPerformance))]
    [MarisaPluginCommand("compare", "cmp")]
    private async Task<MarisaPluginTaskState> BpCmp(Message message)
    {
        if (!TryParseCommand(message, false, false, out var command)) return MarisaPluginTaskState.CompletedTask;

        var db = new BotDbContext().OsuBinds;

        var bind = db.FirstOrDefault(x => x.UserId == message.Sender!.Id);

        if (bind == null)
        {
            message.Reply("您未绑定");
            return MarisaPluginTaskState.CompletedTask;
        }

        if (bind.OsuUserName == command!.Name)
        {
            message.Reply("你不能和自己比较");
            return MarisaPluginTaskState.CompletedTask;
        }

        // 别人的
        var best2 = (await OsuApi.GetScores(
                await GetOsuIdByName(command.Name), OsuApi.OsuScoreType.Best, OsuApi.GetModeName(command.Mode.Value), 0,
                100))!
            .ToList();

        if (!best2.Any())
        {
            message.Reply($"{command.Name} 在 {OsuApi.ModeList[command.Mode.Value]} 上没有成绩");
            return MarisaPluginTaskState.CompletedTask;
        }

        // 你的
        var best1 = (await OsuApi.GetScores(
                await GetOsuIdByName(bind.OsuUserName), OsuApi.OsuScoreType.Best,
                OsuApi.GetModeName(command.Mode.Value), 0, 100))!
            .ToList();

        if (!best1.Any())
        {
            message.Reply($"你在 {OsuApi.ModeList[command.Mode.Value]} 上没有成绩");
            return MarisaPluginTaskState.CompletedTask;
        }

        var im = best1.ToArray().CompareWith(best2.ToArray());

        message.Reply(MessageDataImage.FromBase64(im.ToB64()));

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("统计某人的 bp 分布")]
    [MarisaPluginSubCommand(nameof(BestPerformance))]
    [MarisaPluginCommand("distribution", "dist")]
    private async Task<MarisaPluginTaskState> BpDistribution(Message message)
    {
        if (!TryParseCommand(message, false, false, out var command)) return MarisaPluginTaskState.CompletedTask;

        var best = await OsuApi.GetScores(
            await GetOsuIdByName(command!.Name), OsuApi.OsuScoreType.Best, OsuApi.GetModeName(command.Mode.Value), 0,
            100
        );

        if (!(best?.Any() ?? false))
        {
            message.Reply("您无 bp");
        }
        else
        {
            message.Reply(MessageDataImage.FromBase64(best.Distribution().ToB64(100)));
        }

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("查询某人今天恰到的 pp")]
    [MarisaPluginCommand("todaybp", "tdbp")]
    private async Task<MarisaPluginTaskState> TodayBp(Message message)
    {
        if (!TryParseCommand(message, true, false, out var command)) return MarisaPluginTaskState.CompletedTask;

        var recentScores =
            (await OsuApi.GetScores(await GetOsuIdByName(command!.Name), OsuApi.OsuScoreType.Best,
                OsuApi.GetModeName(command.Mode.Value), 0, 100))?
            .Select((x, i) => (x, i))
            .Where(s => (DateTime.Now - s.x.CreatedAt).TotalHours < (command.BpRank == null ? 24 : command.BpRank.Value.Item1))
            .ToList();

        if (!(recentScores?.Any() ?? false))
        {
            message.Reply($"最近24小时内在 {OsuApi.GetModeName(command.Mode.Value)} 上未恰到分");
        }
        else
        {
            message.Reply(MessageDataImage.FromBase64(recentScores!.GetMiniCards().ToB64(100)));
        }

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("查询打 rank 图奖励的 pp (bonus pp)")]
    [MarisaPluginCommand("bns")]
    private async Task<MarisaPluginTaskState> BonusPp(Message message)
    {
        if (!TryParseCommand(message, false, false, out var command)) return MarisaPluginTaskState.CompletedTask;

        var userInfo = await OsuApi.GetUserInfoByName(command!.Name, mode: command.Mode.Value);

        var scores =
            (await OsuApi.GetScores(userInfo.Id, OsuApi.OsuScoreType.Best, OsuApi.GetModeName(command.Mode.Value), 0,
                100))?.ToArray();

        if (scores == null || !scores.Any())
        {
            message.Reply("无");
            return MarisaPluginTaskState.CompletedTask;
        }

        var bns = userInfo.BonusPp(scores);

        var img = OsuUserInfoDrawer.BonusPp(bns.bonusPp, bns.scorePp);

        message.Reply(
            new MessageDataText(
                $"总pp：{bns.scorePp + bns.bonusPp:F2}，原始pp：{bns.scorePp:F2}，bonus pp：{bns.bonusPp:F2}，成绩总数：{bns.rankedScores}\n"),
            MessageDataImage.FromBase64(img.ToB64(100))
        );

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("预览某个mania图，参数beatmap id，留空时给出最近打的一张图的预览（包括failed）")]
    [MarisaPluginCommand("view")]
    private async Task<MarisaPluginTaskState> Preview(Message message)
    {
        osu.Game.Beatmaps.Beatmap? beatmap = null;

        if (long.TryParse(message.Command.Trim(), out var beatmapId))
        {
            beatmap = await OsuApi.GetBeatmapNotesById(beatmapId);
        }
        else if (string.IsNullOrWhiteSpace(message.Command))
        {
            if (!TryParseCommand(message, false, false, out var command)) return MarisaPluginTaskState.CompletedTask;

            var id = await GetOsuIdByName(command!.Name);
            var scores = await OsuApi.GetScores(
                id, OsuApi.OsuScoreType.Recent, OsuApi.GetModeName(command.Mode.Value), 0, 1, true
            );

            if (scores.Any())
            {
                beatmap = await OsuApi.GetBeatmapNotesById(scores.First().Beatmap.Id);
            }
        }

        if (beatmap == null)
        {
            message.Reply("错误的命令格式");
            return MarisaPluginTaskState.CompletedTask;
        }

        var context = new WebContext();

        context.Put("beatmap", beatmap);

        message.Reply(
            new MessageDataText(beatmap.Metadata.TitleUnicode),
            MessageDataImage.FromBase64(await WebApi.OsuPreview(context.Id))
        );

        return MarisaPluginTaskState.CompletedTask;
    }

    #endregion
}