using Marisa.Plugin.Shared.Dialog;

namespace Marisa.Plugin.Shared.Versus;

/// <summary>两游戏共用的群房间对话；授权、谱面和绘图保留在游戏适配器。</summary>
public static class VersusRoomSession
{
    public static void OpenRendered<TScore, TChart, TResult>(
        MarisaPluginBase source, Message message, (long[] Joined, long[] Unbound) members,
        VersusRoomPlan<TChart> plan, Func<TChart, (long Id, int LevelIdx)> keySelector,
        Func<TChart, double> constantSelector,
        Func<Message, long, (VersusPlayerBinding<TScore>? Player, string? Error)> resolvePlayer,
        Func<IReadOnlyList<TChart>, IReadOnlyList<VersusPlayer<TScore>>, TResult> createResult,
        Func<TResult, IReadOnlyList<VersusRoundPlayer>> totals,
        Func<TResult, int, IReadOnlyList<VersusStanding>, Task<string>> renderSingle,
        Func<TResult, int, int, IReadOnlyList<VersusStanding>, Task<string>> renderBatch,
        Func<TResult, int> pageCount, VersusRoomView view) where TResult : class
    {
        Open(source, message, members, plan, keySelector, constantSelector,
            (next, qq) => resolvePlayer(next, qq).Error,
            async (next, qq) =>
            {
                var (player, error) = resolvePlayer(next, qq);
                return player is null ? new VersusFetchResult<TScore>(null, error) : await player.Fetch();
            }, createResult, totals, Reply);
        return;

        async Task Reply(Message next, TResult result, int round, IReadOnlyList<VersusStanding> standings)
        {
            if (view == VersusRoomView.SingleSong)
            {
                next.Reply(MessageDataImage.FromBase64(await renderSingle(result, round, standings)));
                return;
            }
            await VersusPaging.ReplyPages(source, next, pageCount(result),
                page => renderBatch(result, page, round, standings));
        }
    }

    public static void Open<TScore, TChart, TResult>(
        MarisaPluginBase source,
        Message message,
        (long[] Joined, long[] Unbound) members,
        VersusRoomPlan<TChart> plan,
        Func<TChart, (long Id, int LevelIdx)> keySelector,
        Func<TChart, double> constantSelector,
        Func<Message, long, string?> validate,
        Func<Message, long, Task<VersusFetchResult<TScore>>> fetch,
        Func<IReadOnlyList<TChart>, IReadOnlyList<VersusPlayer<TScore>>, TResult> createResult,
        Func<TResult, IReadOnlyList<VersusRoundPlayer>> totals,
        Func<Message, TResult, int, IReadOnlyList<VersusStanding>, Task> reply)
        where TResult : class
    {
        var room = new VersusRoom<TScore>(message.Sender.Id, members.Joined);
        if (!DialogManager.TryAddDialog((message.GroupInfo!.Id, null), HandleRoom, source))
        {
            message.Reply("群里已经有进行中的房间或游戏");
            return;
        }

        var announce = new MessageBuilder(message).Text($"多人对战：{plan.Title}（{room.Count}/{VersusRoom<TScore>.MaxPlayers}）\n");
        if (members.Joined.Length > 0)
        {
            foreach (var qq in members.Joined) announce.At(qq).Text(" ");
            announce.Text("已加入，不参加可以发送「退出」\n");
        }

        if (members.Unbound.Length > 0)
        {
            foreach (var qq in members.Unbound) announce.At(qq).Text(" ");
            announce.Text("需要先绑定查分器才能加入\n");
        }

        announce.Text("发送「加入」参加，房主发送「开始」（vs/对战）开打，「取消」（解散/结束）关房").Reply();
        return;

        async Task<MarisaPluginTaskState> HandleRoom(Message next)
        {
            if (room.IsExpired) return MarisaPluginTaskState.Canceled;
            var command = next.Command.Trim().ToString();
            if (command is "VS" or "Vs" or "vS") command = "vs";
            return command switch
            {
                "加入" => Join(next),
                "退出" => Leave(next),
                "开始" or "vs" or "对战" => await Start(next),
                "取消" or "解散" or "结束" when next.Sender.Id == room.Host => Cancel(next),
                _ => MarisaPluginTaskState.NoResponse
            };
        }

        MarisaPluginTaskState Join(Message next)
        {
            if (validate(next, next.Sender.Id) is { } error)
            {
                next.Reply(error);
                return MarisaPluginTaskState.ToBeContinued;
            }

            switch (room.Join(next.Sender.Id))
            {
                case VersusRoom<TScore>.JoinResult.Joined:
                    next.Reply($"已加入（{room.Count}/{VersusRoom<TScore>.MaxPlayers}）");
                    break;
                case VersusRoom<TScore>.JoinResult.Full:
                    next.Reply("房间已满");
                    break;
                case VersusRoom<TScore>.JoinResult.Closed:
                    return MarisaPluginTaskState.NoResponse;
            }

            return MarisaPluginTaskState.ToBeContinued;
        }

        MarisaPluginTaskState Leave(Message next)
        {
            switch (room.Leave(next.Sender.Id))
            {
                case VersusRoom<TScore>.LeaveResult.NotIn:
                    return MarisaPluginTaskState.NoResponse;
                case VersusRoom<TScore>.LeaveResult.Dissolved:
                    next.Reply("房间已解散");
                    return MarisaPluginTaskState.CompletedTask;
                case VersusRoom<TScore>.LeaveResult.HostChanged:
                    new MessageBuilder(next).Text("已退出，房主转给 ").At(room.Host).Reply();
                    return MarisaPluginTaskState.ToBeContinued;
                default:
                    next.Reply("已退出");
                    return MarisaPluginTaskState.ToBeContinued;
            }
        }

        MarisaPluginTaskState Cancel(Message next)
        {
            room.Close();
            next.Reply("房间已解散");
            return MarisaPluginTaskState.CompletedTask;
        }

        async Task<MarisaPluginTaskState> Start(Message next)
        {
            switch (room.TryStart(next.Sender.Id, out var roster))
            {
                case VersusRoom<TScore>.StartResult.NotMember:
                    return MarisaPluginTaskState.NoResponse;
                case VersusRoom<TScore>.StartResult.TooFew:
                    next.Reply("至少需要 2 人才能开始");
                    return MarisaPluginTaskState.ToBeContinued;
                case not VersusRoom<TScore>.StartResult.Started:
                    return MarisaPluginTaskState.ToBeContinued;
            }

            if (!plan.Continuous) room.Close();
            TResult? result = null;
            VersusRoomSnapshot snapshot;
            try
            {
                result = await Play(next, roster);
            }
            finally
            {
                snapshot = room.EndRound(result is null ? null : totals(result));
            }

            if (result is not null)
            {
                var round = plan.Continuous ? snapshot.Round : 0;
                await reply(next, result, round, round >= 2 ? snapshot.Standings : []);
            }

            return plan.Continuous ? MarisaPluginTaskState.ToBeContinued : MarisaPluginTaskState.CompletedTask;
        }

        async Task<TResult?> Play(Message next, IReadOnlyList<long> roster)
        {
            var fetched = await Task.WhenAll(roster.Select(async qq => (Qq: qq, Result: await Fetch(next, qq))));
            var players = fetched.Where(x => x.Result.Player is not null).Select(x => x.Result.Player!).ToArray();
            var failed = fetched.Where(x => x.Result.Error is not null).ToArray();

            var notice = new MessageBuilder(next);
            for (var i = 0; i < failed.Length; i++)
            {
                notice.At(failed[i].Qq).Text($" 本轮跳过：{failed[i].Result.Error}{(i + 1 < failed.Length ? "\n" : "")}");
            }

            if (players.Length < 2)
            {
                notice.Text(failed.Length > 0 ? "\n取到成绩的玩家不足 2 人" : "取到成绩的玩家不足 2 人").Reply();
                return null;
            }

            if (failed.Length > 0) notice.Reply();
            var charts = plan.PickCount is { } count
                ? room.PickCharts(plan.Charts, players, keySelector, constantSelector, count, Random.Shared)
                : plan.Charts;
            if (charts.Count == 0)
            {
                next.Reply("没有找到至少两人玩过的谱面");
                return null;
            }

            return createResult(charts, players);
        }

        async Task<VersusFetchResult<TScore>> Fetch(Message next, long qq)
        {
            if (room.TryGetScores(qq, out var cached)) return new VersusFetchResult<TScore>(cached, null);
            VersusFetchResult<TScore> result;
            try
            {
                result = await fetch(next, qq);
            }
            catch (Exception error) when (error is InvalidDataException or HttpRequestException)
            {
                return new VersusFetchResult<TScore>(null, error.Message);
            }

            if (result.Player is { } player) room.CacheScores(player);
            return result;
        }
    }
}
