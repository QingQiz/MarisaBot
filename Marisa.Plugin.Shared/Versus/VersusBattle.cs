namespace Marisa.Plugin.Shared.Versus;

public sealed record VersusPlayerBinding<TScore>(long Qq, Func<Task<VersusFetchResult<TScore>>> Fetch);

public sealed record VersusSingleSelection<TChart>(
    IReadOnlyList<TChart> Charts, bool Random, string Title, string Version);

public sealed record VersusRandomSelection<TChart>(int Count, string Query, IReadOnlyList<TChart> Charts);

public sealed record VersusBatchSelection<TChart>(string Query, string SortLabel, IReadOnlyList<TChart> Charts);

/// <summary>非泛型命令入口，使默认接口方法与游戏的强类型适配器共用一个契约。</summary>
public abstract class VersusBattle
{
    public abstract bool CanParseScope(string query);
    public abstract Task<MarisaPluginTaskState> Single(Message message);
    public abstract Task<MarisaPluginTaskState> Random(Message message);
    public abstract Task<MarisaPluginTaskState> Batch(Message message);
    public abstract Task<MarisaPluginTaskState> SingleRoom(Message message, long botQq);
    public abstract MarisaPluginTaskState RandomRoom(Message message, long botQq);
    public abstract MarisaPluginTaskState BatchRoom(Message message, long botQq);
}

/// <summary>共享玩家解析、成绩获取、共同谱面抽取及六种对战流程。</summary>
public sealed class VersusBattle<TScore, TChart> : VersusBattle
{
    public required Func<string, bool> MatchScope { get; init; }
    public required string CommandPrefix { get; init; }
    public required Func<Message, long, (VersusPlayerBinding<TScore>? Player, string? Error)> ResolvePlayer { get; init; }
    public required Func<string, bool> IsOpponentBindingError { get; init; }
    public required Func<Message, Task<VersusSingleSelection<TChart>?>> SelectSingle { get; init; }
    public required Func<Message, string, VersusRandomSelection<TChart>?> ParseRandom { get; init; }
    public required Func<Message, VersusBatchSelection<TChart>?> ParseBatch { get; init; }
    public required Func<TChart, (long Id, int LevelIdx)> ChartKey { get; init; }
    public required Func<IReadOnlyList<TChart>, string> VersionOf { get; init; }
    public required Func<Message, TChart, VersusPlayer<TScore>, VersusPlayer<TScore>, Task> ReplySingle { get; init; }
    public required Func<Message, string, string, string, IReadOnlyList<TChart>, VersusPlayer<TScore>, VersusPlayer<TScore>, Task> ReplyBatch { get; init; }
    public required Action<Message, (long[] Joined, long[] Unbound), VersusRoomPlan<TChart>, VersusRoomPresentation> OpenRoom { get; init; }

    public override bool CanParseScope(string query) => MatchScope(query);

    public override async Task<MarisaPluginTaskState> Single(Message message)
    {
        if (ResolvePlayers(message) is not { } players) return MarisaPluginTaskState.CompletedTask;
        if (await SelectSingle(message) is not { } selection) return MarisaPluginTaskState.CompletedTask;
        if (await FetchScores(message, players) is not { } sides) return MarisaPluginTaskState.CompletedTask;

        TChart chart;
        if (selection.Random)
        {
            var shared = SharedCharts(selection.Charts, sides);
            if (shared.Length == 0) return ReplyNoSharedCharts(message);
            chart = shared[System.Random.Shared.Next(shared.Length)];
        }
        else
        {
            chart = selection.Charts[0];
        }

        await ReplySingle(message, chart, sides.Self, sides.Opponent);
        return MarisaPluginTaskState.CompletedTask;
    }

    public override async Task<MarisaPluginTaskState> Random(Message message)
    {
        if (ParseRandom(message, $"{CommandPrefix} vs n 5 [难度/范围] @对手") is not { } parsed)
            return MarisaPluginTaskState.CompletedTask;
        if (ResolvePlayers(message) is not { } players) return MarisaPluginTaskState.CompletedTask;
        if (await FetchScores(message, players) is not { } sides) return MarisaPluginTaskState.CompletedTask;

        var shared = SharedCharts(parsed.Charts, sides);
        if (shared.Length == 0) return ReplyNoSharedCharts(message);
        var picked = shared.OrderBy(_ => System.Random.Shared.Next()).Take(parsed.Count).ToArray();
        await ReplyBatch(message, $"随机 {picked.Length} 首共同谱面", string.Empty, "随机", picked, sides.Self, sides.Opponent);
        return MarisaPluginTaskState.CompletedTask;
    }

    public override async Task<MarisaPluginTaskState> Batch(Message message)
    {
        if (ParseBatch(message) is not { } parsed) return MarisaPluginTaskState.CompletedTask;
        if (ResolvePlayers(message) is not { } players) return MarisaPluginTaskState.CompletedTask;
        if (await FetchScores(message, players) is not { } sides) return MarisaPluginTaskState.CompletedTask;

        await ReplyBatch(message, parsed.Query, VersionOf(parsed.Charts), parsed.SortLabel,
            parsed.Charts, sides.Self, sides.Opponent);
        return MarisaPluginTaskState.CompletedTask;
    }

    public override async Task<MarisaPluginTaskState> SingleRoom(Message message, long botQq)
    {
        if (ResolveRoomMembers(message, botQq) is not { } members) return MarisaPluginTaskState.CompletedTask;
        if (await SelectSingle(message) is not { } selection) return MarisaPluginTaskState.CompletedTask;

        OpenRoom(message, members, new VersusRoomPlan<TChart>(selection.Title, selection.Charts, selection.Random ? 1 : null),
            new VersusRoomPresentation(selection.Version, VersusRoomView.SingleSong));
        return MarisaPluginTaskState.CompletedTask;
    }

    public override MarisaPluginTaskState RandomRoom(Message message, long botQq)
    {
        if (ParseRandom(message, $"{CommandPrefix} vs 开房 n 5 [难度/范围]") is not { } parsed)
            return MarisaPluginTaskState.CompletedTask;
        if (ResolveRoomMembers(message, botQq) is not { } members) return MarisaPluginTaskState.CompletedTask;

        var title = parsed.Query.Length == 0 ? $"随机 {parsed.Count} 首" : $"{parsed.Query} 随机 {parsed.Count} 首";
        OpenRoom(message, members, new VersusRoomPlan<TChart>(title, parsed.Charts, parsed.Count),
            new VersusRoomPresentation(VersionOf(parsed.Charts), VersusRoomView.Batch));
        return MarisaPluginTaskState.CompletedTask;
    }

    public override MarisaPluginTaskState BatchRoom(Message message, long botQq)
    {
        if (ParseBatch(message) is not { } parsed) return MarisaPluginTaskState.CompletedTask;
        if (ResolveRoomMembers(message, botQq) is not { } members) return MarisaPluginTaskState.CompletedTask;

        OpenRoom(message, members, new VersusRoomPlan<TChart>(parsed.Query, parsed.Charts, null),
            new VersusRoomPresentation(VersionOf(parsed.Charts), VersusRoomView.Batch));
        return MarisaPluginTaskState.CompletedTask;
    }

    private (VersusPlayerBinding<TScore> Self, VersusPlayerBinding<TScore> Opponent)? ResolvePlayers(Message message)
    {
        var opponents = message.At().Distinct().ToArray();
        if (opponents.Length != 1)
        {
            message.Reply(opponents.Length == 0 ? "请 @一名对手" : "请只指定一名对手");
            return null;
        }

        var (self, selfError) = ResolvePlayer(message, message.Sender.Id);
        var (opponent, opponentError) = ResolvePlayer(message, opponents[0]);
        if (opponent is null)
        {
            new MessageBuilder(message).Text(opponentError!).At(opponents[0]).Reply();
            return null;
        }
        if (self is null)
        {
            message.Reply(selfError!);
            return null;
        }
        return (self, opponent);
    }

    private async Task<(VersusPlayer<TScore> Self, VersusPlayer<TScore> Opponent)?> FetchScores(
        Message message, (VersusPlayerBinding<TScore> Self, VersusPlayerBinding<TScore> Opponent) players)
    {
        var selfFetch = players.Self.Fetch();
        var opponentFetch = players.Opponent.Fetch();
        var fetched = await Task.WhenAll(selfFetch, opponentFetch);
        var selfData = fetched[0];
        var opponentData = fetched[1];

        if (opponentData.Error is { } opponentError && IsOpponentBindingError(opponentError))
        {
            new MessageBuilder(message).Text(opponentError).At(players.Opponent.Qq).Reply();
            return null;
        }
        if ((selfData.Error ?? opponentData.Error) is { } error)
        {
            message.Reply(error);
            return null;
        }
        return (selfData.Player!, opponentData.Player!);
    }

    private (long[] Joined, long[] Unbound)? ResolveRoomMembers(Message message, long botQq)
    {
        if (message.GroupInfo is null)
        {
            message.Reply("请在群里开房");
            return null;
        }
        var (self, error) = ResolvePlayer(message, message.Sender.Id);
        if (self is null)
        {
            message.Reply(error!);
            return null;
        }

        var invitees = message.At().Distinct().Where(x => x != message.Sender.Id && x != botQq).ToArray();
        var unbound = invitees.Where(x => ResolvePlayer(message, x).Player is null).ToArray();
        var joined = invitees.Except(unbound).ToArray();
        if (joined.Length >= VersusRoom<TScore>.MaxPlayers)
        {
            message.Reply($"一个房间最多 {VersusRoom<TScore>.MaxPlayers} 人");
            return null;
        }
        return (joined, unbound);
    }

    private TChart[] SharedCharts(IReadOnlyList<TChart> charts, (VersusPlayer<TScore> Self, VersusPlayer<TScore> Opponent) sides) =>
        charts.Where(chart => sides.Self.Scores.ContainsKey(ChartKey(chart)) && sides.Opponent.Scores.ContainsKey(ChartKey(chart))).ToArray();

    private static MarisaPluginTaskState ReplyNoSharedCharts(Message message)
    {
        message.Reply("没有找到双方都已游玩且当前可查询的谱面");
        return MarisaPluginTaskState.CompletedTask;
    }
}
