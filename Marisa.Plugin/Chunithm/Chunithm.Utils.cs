using System.Net;
using System.Net.Sockets;
using Marisa.Database;
using Marisa.Database.Entity.Plugin.Chunithm;
using Marisa.Plugin.Shared.Chunithm;
using Marisa.Plugin.Shared.Chunithm.DataFetcher;
using Marisa.Plugin.Shared.DivingFish;
using Marisa.Plugin.Shared.Lxns;
using Marisa.Plugin.Shared.Util;
using Marisa.Plugin.Shared.Util.SongDb;
using Marisa.Plugin.Shared.Versus;
using Microsoft.Extensions.DependencyInjection;

namespace Marisa.Plugin.Chunithm;

public partial class Chunithm
{
    private static bool VersusRandomTrigger(Message message, IServiceProvider _) => VersusCommands.RandomTrigger(message);

    private static bool VersusBatchTrigger(Message message, IServiceProvider provider) =>
        VersusCommands.BatchTrigger(message, input =>
        {
            var plugin = provider.GetServices<MarisaPluginBase>().OfType<Chunithm>().FirstOrDefault();
            return plugin is not null && ChunithmVersusScope.TryParse(input, plugin.SongDb.SongList, out _, out _);
        });

    private (ResolvedPlayer? Player, string? Error) ResolveVersusPlayer(Message message, long qq)
    {
        using var realm = BotDbContext.OpenRealm();
        var server = realm.All<ChunithmBind>().FirstOrDefault(x => x.UId == qq)?.ServerName;
        switch (server)
        {
            case "lxns" when LxnsTokenStore.GetToken(qq) is null:
                return (null, "请先使用 chu bind 选择 lxns 完成 OAuth 授权");
            case "lxns":
                return (new ResolvedPlayer(qq, null, qq == message.Sender.Id, GetDataFetcher("lxns", null)), null);
            case "DivingFish" or null when DivingFishOAuth.IsConfigured && DivingFishTokenStore.GetToken(qq, "chunithm") is null:
                return (null, "请先使用 chu bind 选择水鱼完成 OAuth 授权");
            case "DivingFish" or null:
                return (new ResolvedPlayer(qq, null, qq == message.Sender.Id, GetDataFetcher("DivingFish", null)), null);
            default:
                return (null, "chu vs 暂仅支持水鱼和落雪，请使用 chu bind 改绑");
        }
    }

    private (ResolvedPlayer Self, ResolvedPlayer Opponent)? ResolveVersusPlayers(Message message)
    {
        var opponents = message.At().Distinct().ToArray();
        if (opponents.Length != 1)
        {
            message.Reply(opponents.Length == 0 ? "请 @一名对手" : "请只指定一名对手");
            return null;
        }

        var (self, selfError) = ResolveVersusPlayer(message, message.Sender.Id);
        var (opponent, opponentError) = ResolveVersusPlayer(message, opponents[0]);
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

    private static async Task<VersusFetchResult<ChunithmScore>> FetchBattleData(ResolvedPlayer player)
    {
        try
        {
            var data = await player.Fetcher.GetBattleData(player);
            return new VersusFetchResult<ChunithmScore>(new VersusPlayer<ChunithmScore>(player.Qq,
                string.IsNullOrWhiteSpace(data.Nickname) ? $"QQ {player.Qq}" : data.Nickname, data.Scores), null);
        }
        catch (HttpRequestException e)
        {
            return new VersusFetchResult<ChunithmScore>(null, e.Message);
        }
    }

    private static async Task<(VersusPlayer<ChunithmScore> Self, VersusPlayer<ChunithmScore> Opponent)?> FetchVersusScores(
        Message message, (ResolvedPlayer Self, ResolvedPlayer Opponent) players)
    {
        var fetched = await Task.WhenAll(FetchBattleData(players.Self), FetchBattleData(players.Opponent));
        if (fetched[1].Error is { } opponentError &&
            (opponentError.Contains("OAuth", StringComparison.OrdinalIgnoreCase) || opponentError.Contains("绑定", StringComparison.Ordinal)))
        {
            new MessageBuilder(message).Text(opponentError).At(players.Opponent.Qq).Reply();
            return null;
        }
        if ((fetched[0].Error ?? fetched[1].Error) is { } error)
        {
            message.Reply(error);
            return null;
        }
        return (fetched[0].Player!, fetched[1].Player!);
    }

    private static (List<ChunithmSong> Songs, int LevelIndex, bool Random) ResolveVersusQuery(SongDb<ChunithmSong> songs, string input)
    {
        var query = input.Trim();
        if (query.Length == 0) return ([], 3, true);
        var exact = songs.SearchSongExact(query.AsMemory());
        if (exact.Count > 0) return (exact, 3, false);
        if (ChunithmVersusScope.TryParseDifficulty(query, out var difficulty)) return ([], difficulty, true);
        var hasAffix = ChunithmVersusScope.TryStripDifficultyAffix(query.AsMemory(), out var level, out var rest);
        if (hasAffix)
        {
            exact = songs.SearchSongExact(rest);
            if (exact.Count > 0) return (exact, level, false);
        }
        var fuzzy = songs.SearchSong(query.AsMemory());
        if (fuzzy.Count > 0) return (fuzzy, 3, false);
        return hasAffix ? (songs.SearchSong(rest), level, false) : ([], 3, false);
    }

    private IReadOnlyList<ChunithmVersusChart> VersusLevelCharts(int level) =>
        ChunithmVersusCatalog.StandardCharts(SongDb.SongList).Where(x => x.LevelIndex == level).ToArray();

    private (int Count, string Query, IReadOnlyList<ChunithmVersusChart> Charts)? ParseVersusRandom(Message message, string usage)
    {
        var command = message.Command.Trim().ToString();
        var digits = command.TakeWhile(char.IsAsciiDigit).Count();
        if (!int.TryParse(command[..digits], out var count) || count is < 1 or > 20)
        {
            message.Reply($"N 须为 1～20 的整数，用法：{usage}");
            return null;
        }
        var query = command[digits..].Trim();
        if (query.Length == 0) return (count, query, VersusLevelCharts(3));
        if (ChunithmVersusScope.TryParseDifficulty(query, out var level)) return (count, query, VersusLevelCharts(level));
        if (ChunithmVersusScope.TryParse(query, SongDb.SongList, out var scope, out _) && scope is not null)
            return (count, query, ChunithmVersusScope.SelectCharts(scope, SongDb.SongList));
        message.Reply($"无法解析难度或范围：{query}；支持 BASIC～ULTIMA");
        return null;
    }

    private (string Query, string SortLabel, IReadOnlyList<ChunithmVersusChart> Charts)? ParseVersusScope(Message message)
    {
        var query = message.Command.Trim().ToString();
        if (!ChunithmVersusScope.TryParse(query, SongDb.SongList, out var scope, out var error) || scope is null)
        {
            message.Reply(error ?? $"无法解析范围：{query}");
            return null;
        }
        var charts = ChunithmVersusScope.SelectCharts(scope, SongDb.SongList);
        if (charts.Count == 0)
        {
            message.Reply($"没有找到 {query} 对应的谱面");
            return null;
        }
        return (query, scope.SortLabel, charts);
    }

    private static string VersusVersion(IEnumerable<ChunithmVersusChart> charts)
    {
        var versions = charts.Select(x => x.Song.Version).Distinct(StringComparer.OrdinalIgnoreCase).Take(2).ToArray();
        return versions.Length == 1 ? versions[0] : string.Empty;
    }

    private (long[] Joined, long[] Unbound)? ResolveVersusRoomMembers(Message message, long botQq)
    {
        if (message.GroupInfo is null)
        {
            message.Reply("请在群里开房");
            return null;
        }
        var (self, error) = ResolveVersusPlayer(message, message.Sender.Id);
        if (self is null)
        {
            message.Reply(error!);
            return null;
        }
        var invitees = message.At().Distinct().Where(x => x != message.Sender.Id && x != botQq).ToArray();
        var unbound = invitees.Where(x => ResolveVersusPlayer(message, x).Player is null).ToArray();
        var joined = invitees.Except(unbound).ToArray();
        if (joined.Length >= VersusRoom<ChunithmScore>.MaxPlayers)
        {
            message.Reply($"一个房间最多 {VersusRoom<ChunithmScore>.MaxPlayers} 人");
            return null;
        }
        return (joined, unbound);
    }

    private void OpenVersusRoom(Message message, (long[] Joined, long[] Unbound) members,
        VersusRoomPlan<ChunithmVersusChart> plan, bool singleSong)
    {
        var version = VersusVersion(plan.Charts);
        VersusRoomSession.Open<ChunithmScore, ChunithmVersusChart, ChunithmVersusMulti>(this, message, members, plan,
            chart => (chart.Id, chart.LevelIndex), chart => chart.Constant,
            (next, qq) => ResolveVersusPlayer(next, qq).Error,
            async (next, qq) =>
            {
                var (player, error) = ResolveVersusPlayer(next, qq);
                return player is null ? new VersusFetchResult<ChunithmScore>(null, error) : await FetchBattleData(player);
            },
            (charts, players) => new ChunithmVersusMulti(plan.Title, version, charts, players),
            result => result.Players.Select((player, i) =>
                new VersusRoundPlayer(player.Qq, player.Name, result.Totals[i].Points, result.Totals[i].Matchups)).ToArray(),
            async (next, result, round, standings) =>
            {
                if (singleSong)
                {
                    next.Reply(MessageDataImage.FromBase64(await ChunithmDraw.DrawVersusMulti(result, round, standings)));
                    return;
                }
                await VersusPaging.ReplyPages(this, next, result.PageCount,
                    page => ChunithmDraw.DrawVersusMultiBatch(result, page, round, standings));
            });
    }

    private static string DeviceBindingLabel(long qq)
    {
        var value = qq.ToString();
        return value.Length > 4 ? $"QQ {value[..2]}****{value[^2..]}" : $"QQ {value}";
    }

    private DataFetcher GetDataFetcher(string name, string? accessCode)
    {
        try
        {
            return name switch
            {
                "DivingFish" => new DivingFishDataFetcher(SongDb),
                "Louis"      => new LouisDataFetcher(SongDb),
                "lxns"       => new LxnsDataFetcher(SongDb),
                "RinNET" => new AllNetBasedNetDataFetcher(SongDb, "RinNET", "aqua.naominet.live",
                    ConfigurationManager.Configuration.Chunithm.RinNetKeyChip, accessCode!),
                "Aqua" => new AllNetBasedNetDataFetcher(SongDb, "Aqua", "aqua.msm.moe",
                    ConfigurationManager.Configuration.Chunithm.AllNetKeyChip, accessCode!),
                _ => Dns.GetHostAddresses(name).Length != 0
                    ? new AllNetBasedNetDataFetcher(SongDb, name, name,
                        ConfigurationManager.Configuration.Chunithm.AllNetKeyChip, accessCode!)
                    : throw new InvalidDataException("无效的服务器名：" + name)
            };
        }
        catch (Exception e) when (e is SocketException or ArgumentException)
        {
            throw new InvalidDataException("无效的服务器名：" + name);
        }
    }

    /// <summary>
    ///     解析查询目标：@ 优先，其次命令文本（仅当调用方允许把它当账号名时），否则发送者自己；
    ///     并按该 QQ 的本地绑定选定查分器。只查本地库，不发网络请求，fetcher 不再自己从消息里反推目标。
    /// </summary>
    private ResolvedPlayer ResolvePlayer(Message message, bool allowUsername = false)
    {
        var at = message.MessageChain!.Messages.FirstOrDefault(m => m.Type == MessageDataType.At);

        // 账号名查询只有水鱼有公开接口（Louis 也支持按账号名查，但没启用）；账号名不代表本人，拿不到本人票据
        if (at is null && allowUsername && !message.Command.IsWhiteSpace())
        {
            return new ResolvedPlayer(message.Sender.Id, message.Command.Trim().ToString(), false,
                GetDataFetcher("DivingFish", null));
        }

        var qq = (at as MessageDataAt)?.Target ?? message.Sender.Id;

        return new ResolvedPlayer(qq, null, qq == message.Sender.Id, GetDataFetcher(qq));
    }

    /// <summary>按绑定选查分器：没绑定记录时按水鱼处理。</summary>
    private DataFetcher GetDataFetcher(long qq)
    {
        using var realm = BotDbContext.OpenRealm();

        var bind = realm.All<ChunithmBind>().FirstOrDefault(x => x.UId == qq);

        return bind == null ? GetDataFetcher("DivingFish", null) : GetDataFetcher(bind.ServerName, bind.AccessCode);
    }

    private async Task<ChunithmRating> GetRating(Message message, bool b50 = false)
    {
        var player = ResolvePlayer(message, allowUsername: true);

        var rating = await player.Fetcher.GetRating(player);

        if (b50)
        {
            rating.IsB50 = true;
            return rating;
        }

        // B30: 合并 best+recent，兜底避免查分器数据未合并
        var allScores = rating.Records.Best
            .Concat(rating.Records.Recent)
            .GroupBy(x => new { x.Id, x.LevelIndex })
            .Select(g => g.OrderByDescending(x => x.Achievement).First())
            .OrderByDescending(x => x.Rating)
            .Take(30)
            .ToArray();
        rating.Records.Best = allScores;
        rating.Records.Recent = [];
        rating.IsB50 = false;
        return rating;
    }

    private async Task<MessageChain> GetRatingImg(Message message, bool b50 = false)
    {
        var ctx = new WebContext();
        ctx.Put("rating", await GetRating(message, b50));

        return MessageChain.FromImageB64(await WebApi.ChunithmBest(ctx.Id, b50));
    }
}
