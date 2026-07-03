using Marisa.Database;
using Marisa.Database.Entity.Plugin.Osu;
using Marisa.Plugin.Shared.Osu;
using Marisa.Plugin.Shared.Osu.Drawer;
using Marisa.Plugin.Shared.Util;

namespace Marisa.Plugin.Osu;

public partial class Osu
{
    #region 绑定 / help

    [MarisaPluginDoc("绑定一个 osu 账号", "你的**osu**用户名")]
    [MarisaPluginCommand("bind")]
    private static async Task<MarisaPluginTaskState> Bind(Message message)
    {
        var name   = message.Command.Trim();
        var sender = message.Sender.Id;

        if (name.IsEmpty)
        {
            message.Reply("请给出 osu! 的用户名");
            return MarisaPluginTaskState.CompletedTask;
        }

        var info = await OsuApi.GetUserInfoByName(name.ToString());

        using var realm = BotDbContext.OpenRealm();
        realm.Write(() =>
        {
            var bind = realm.All<OsuBind>().FirstOrDefault(o => o.UserId == sender);
            if (bind is null)
            {
                bind = realm.AddWithAutoId(new OsuBind
                {
                    UserId = sender
                });
            }

            bind.OsuUserId   = info.Id;
            bind.OsuUserName = info.Username;
            bind.GameMode    = info.Playmode.ToLower();
        });
        message.Reply("好了");

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("设置当前绑定的账户的默认模式", "`osu`、`taiko`、`catch` 和 `mania`")]
    [MarisaPluginCommand("setMode", "set mode", "mode")]
    private static Task<MarisaPluginTaskState> SetMode(Message message)
    {
        var sender = message.Sender.Id;
        var mode   = message.Command.Trim();

        if (!OsuApi.ModeList.Contains(mode))
        {
            message.Reply("可选的模式：" + string.Join(", ", OsuApi.ModeList));
            return Task.FromResult(MarisaPluginTaskState.CompletedTask);
        }

        using var realm = BotDbContext.OpenRealm();
        var bind = realm.All<OsuBind>().FirstOrDefault(o => o.UserId == sender);
        if (bind is null)
        {
            message.Reply("您未绑定！");
            return Task.FromResult(MarisaPluginTaskState.CompletedTask);
        }

        realm.Write(() => bind.GameMode = mode.ToString());
        message.Reply("好了");

        return Task.FromResult(MarisaPluginTaskState.CompletedTask);
    }

    #endregion

    #region 查询相关（继承了猫猫的命令）

    [MarisaPluginDoc("查询某人的个人信息")]
    [MarisaPluginCommand("info")]
    private async Task<MarisaPluginTaskState> Info(Message message)
    {
        var command = await ResolveCommand(message, false, false);
        if (command == null) return MarisaPluginTaskState.CompletedTask;

        if (DebounceCheck(message, command!.Name))
        {
            return MarisaPluginTaskState.CompletedTask;
        }

        try
        {
            var uInfo = await OsuApi.GetUserInfoByName(command.Name, command.Mode ?? -1);

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
        var command = await ResolveCommand(message, false, false);
        if (command == null) return MarisaPluginTaskState.CompletedTask;

        if (command.Mode is not (0 or 3))
        {
            message.Reply("目前只支持 osu 和 mania 模式");
            return MarisaPluginTaskState.CompletedTask;
        }

        var info = await OsuApi.GetUserInfoByName(command.Name);

        var context = new WebContext();

        context.Put("info", info);
        context.Put("recommend", await OsuApi.GetRecommend(info.Id, command.Mode!.Value));

        message.Reply(MessageDataImage.FromBase64(await WebApi.OsuRecommend(context.Id)));

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("查询某人最近通过的图")]
    [MarisaPluginCommand("pr")]
    private async Task<MarisaPluginTaskState> RecentPass(Message message)
    {
        var command = await ResolveCommand(message, true, false);
        if (command == null) return MarisaPluginTaskState.CompletedTask;

        message.Reply(MessageDataImage.FromBase64(await WebApi.OsuScore(command!.Name, command.Mode!.Value,
            command.Rank?.Start, true, false)));

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("查询某人最近打的图")]
    [MarisaPluginCommand("recent", "rec", "re")]
    private async Task<MarisaPluginTaskState> Recent(Message message)
    {
        var command = await ResolveCommand(message, true, false);
        if (command == null) return MarisaPluginTaskState.CompletedTask;

        message.Reply(MessageDataImage.FromBase64(await WebApi.OsuScore(command!.Name, command.Mode!.Value,
            command.Rank?.Start, true, true)));

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("查询某人 pp 最高的成绩图（bp）")]
    [MarisaPluginCommand("bp")]
    private async Task<MarisaPluginTaskState> BestPerformance(Message message)
    {
        var command = await ResolveCommand(message, true, true);
        if (command == null) return MarisaPluginTaskState.CompletedTask;

        if (command!.Rank?.End == null)
        {
            message.Reply(MessageDataImage.FromBase64(await WebApi.OsuScore(command.Name, command.Mode!.Value,
                command.Rank?.Start, false, false)));
        }
        else
        {
            var oid = await GetOsuIdByName(command.Name);
            var mode = OsuApi.GetModeName(command.Mode!.Value);
            var rank = command.Rank!;
            var best = (await OsuApi.GetScores(
                    oid,
                    OsuApi.OsuScoreType.Best,
                    mode,
                    rank.Start - 1,
                    (int)rank.End - rank.Start + 1
                ))?
                .Select((x, i) => (x, i + rank.Start - 1))
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
    private MarisaPluginTaskState BpCmp(Message message)
    {
        message.Reply("Disabled");

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("统计某人的 bp 分布")]
    [MarisaPluginSubCommand(nameof(BestPerformance))]
    [MarisaPluginCommand("distribution", "dist")]
    private MarisaPluginTaskState BpDistribution(Message message)
    {
        message.Reply("Disabled");

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("查询某人今天恰到的 pp")]
    [MarisaPluginCommand("todaybp", "tdbp")]
    private async Task<MarisaPluginTaskState> TodayBp(Message message)
    {
        var command = await ResolveCommand(message, true, false);
        if (command == null) return MarisaPluginTaskState.CompletedTask;

        var recentScores =
            (await OsuApi.GetScores(await GetOsuIdByName(command!.Name), OsuApi.OsuScoreType.Best,
                OsuApi.GetModeName(command.Mode!.Value), 0, 100))?
            .Select((x, i) => (x, i))
            .Where(s => (DateTime.Now - s.x.CreatedAt).TotalHours < (command.Rank?.Start ?? 24))
            .ToList();

        if (!(recentScores?.Any() ?? false))
        {
            message.Reply($"最近{command.Rank?.Start ?? 24}小时内在 {OsuApi.GetModeName(command.Mode!.Value)} 上未恰到分");
        }
        else
        {
            message.Reply(MessageDataImage.FromBase64(recentScores.GetMiniCards().ToB64(100)));
        }

        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("查询打 rank 图奖励的 pp (bonus pp)")]
    [MarisaPluginCommand("bns")]
    private static MarisaPluginTaskState BonusPp(Message message)
    {
        message.Reply("Disabled");
        return MarisaPluginTaskState.CompletedTask;
    }

    [MarisaPluginDoc("预览某个mania图", "`beatmap id`，留空时给出最近打的一张图的预览（包括failed）")]
    [MarisaPluginCommand("view")]
    private async Task<MarisaPluginTaskState> Preview(Message message)
    {
        if (long.TryParse(message.Command.Span.Trim(), out var beatmapId))
        {
            goto result;
        }

        if (message.Command.IsWhiteSpace())
        {
            var command = await ResolveCommand(message, false, false);
            if (command == null) return MarisaPluginTaskState.CompletedTask;

            var id = await GetOsuIdByName(command!.Name);
            var scores = await OsuApi.GetScores(
                id, OsuApi.OsuScoreType.Recent, OsuApi.GetModeName(command.Mode!.Value), 0, 1, true
            );

            if (scores.Length != 0)
            {
                beatmapId = scores.First().Beatmap.Id;
                goto result;
            }
        }

        message.Reply("错误的命令格式");
        return MarisaPluginTaskState.CompletedTask;

        result:

        var info = await OsuApi.GetBeatmapInfoById(beatmapId);
        if (info.ModeInt != 3)
        {
            message.Reply("只支持 osu!mania");
            return MarisaPluginTaskState.CompletedTask;
        }

        message.Reply(
            new MessageDataText(info.Beatmapset?.TitleUnicode ?? ""),
            MessageDataImage.FromBase64(await WebApi.OsuPreview(beatmapId))
        );

        return MarisaPluginTaskState.CompletedTask;
    }

    #endregion
}
