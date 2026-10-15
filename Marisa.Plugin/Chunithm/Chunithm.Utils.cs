using System.Net;
using System.Net.Sockets;
using Marisa.Database;
using Marisa.Database.Entity.Plugin.Chunithm;
using Marisa.Plugin.Shared.Chunithm;
using Marisa.Plugin.Shared.Chunithm.DataFetcher;
using Marisa.Plugin.Shared.DivingFish;
using Marisa.Plugin.Shared.Interface;
using Marisa.Plugin.Shared.Lxns;
using Marisa.Plugin.Shared.Util;
using Marisa.Plugin.Shared.Util.SongDb;
using Marisa.Plugin.Shared.Versus;

namespace Marisa.Plugin.Chunithm;

public partial class Chunithm
{
    private VersusBattle? _battle;

    VersusBattle ICanBattleWithOthers.Battle => _battle ??= new VersusBattle<ChunithmScore, ChunithmVersusChart>
    {
        CommandPrefix = "chu",
        ResolvePlayer = ResolveBattlePlayer,
        IsOpponentBindingError = error => error.Contains("OAuth", StringComparison.OrdinalIgnoreCase) ||
                                          error.Contains("绑定", StringComparison.Ordinal),
        SelectSingle = SelectVersusSingle,
        ParseRandom = ParseVersusRandom,
        ParseBatch = ParseVersusScope,
        MatchScope = query => ChunithmVersusScope.TryParse(query, SongDb.SongList, out _, out _),
        ChartKey = chart => (chart.Id, chart.LevelIndex),
        VersionOf = VersusVersion,
        ReplySingle = async (message, chart, self, opponent) =>
            message.Reply(MessageDataImage.FromBase64(await ChunithmDraw.DrawVersus(ChunithmVersusBatch.GetSingle(chart, self, opponent)))),
        ReplyBatch = async (message, title, version, sortLabel, charts, self, opponent) =>
        {
            var batch = new ChunithmVersusBatch(title, version, sortLabel, charts, self, opponent);
            await VersusPaging.ReplyPages(this, message, batch.PageCount, page => ChunithmDraw.DrawVersusBatch(batch, page));
        },
        OpenRoom = OpenVersusRoom
    };

    private (VersusPlayerBinding<ChunithmScore>? Player, string? Error) ResolveBattlePlayer(Message message, long qq)
    {
        var (player, error) = ResolveVersusPlayer(message, qq);
        return (player is null ? null : new VersusPlayerBinding<ChunithmScore>(qq, () => FetchBattleData(player)), error);
    }

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

    private static (List<ChunithmSong> Songs, int LevelIndex, bool Random) ResolveVersusQuery(SongDb<ChunithmSong> songs, string input)
        => VersusQueries.ResolveSongQuery(songs, input,
            query => ChunithmVersusScope.TryParseDifficulty(query, out var difficulty) ? difficulty : null,
            query => ChunithmVersusScope.TryStripDifficultyAffix(query, out var level, out var rest) ? (level, rest) : null);

    private static IReadOnlyList<ChunithmVersusChart> VersusLevelCharts(int level) =>
        ChunithmVersusCatalog.StandardCharts(LxnsDataFetcher.GetSharedSongList()).Where(x => x.LevelIndex == level).ToArray();

    private async Task<VersusSingleSelection<ChunithmVersusChart>?> SelectVersusSingle(Message message)
    {
        var selection = ResolveVersusQuery(SongDb, message.Command.Trim().ToString());
        var levelName = ChunithmSong.LevelLabel[selection.LevelIndex];
        if (selection.Random)
        {
            var randomCharts = VersusLevelCharts(selection.LevelIndex);
            var title = selection.LevelIndex == 3 ? "随机单曲" : $"随机单曲（{levelName}）";
            return new VersusSingleSelection<ChunithmVersusChart>(randomCharts, true, title, VersusVersion(randomCharts));
        }

        var song = await SongDb.MultiPageSelectResult(selection.Songs, message, false, true);
        if (song is null) return null;
        var cnSong = LxnsDataFetcher.GetSharedSongList().FirstOrDefault(x => x.Id == song.Id);
        var chart = cnSong is null ? null : ChunithmVersusCatalog.FindChart(cnSong, selection.LevelIndex);
        if (chart is null)
        {
            message.Reply($"国服暂未收录「{song.Title}」的 {levelName} 谱面");
            return null;
        }

        IReadOnlyList<ChunithmVersusChart> charts = [chart];
        return new VersusSingleSelection<ChunithmVersusChart>(charts, false, $"{song.Title}（{levelName}）", VersusVersion(charts));
    }

    private VersusRandomSelection<ChunithmVersusChart>? ParseVersusRandom(Message message, string usage)
        => VersusQueries.ParseRandom(message, usage, 20,
            query => ChunithmVersusScope.TryParseDifficulty(query, out var difficulty) ? difficulty : null,
            VersusLevelCharts, ParseVersusScopeQuery,
            query => $"无法解析难度或范围：{query}；支持 BASIC～ULTIMA");

    private VersusBatchSelection<ChunithmVersusChart>? ParseVersusScope(Message message)
        => VersusQueries.ParseBatch(message, ParseVersusScopeQuery);

    private static (VersusScopeSelection<ChunithmVersusChart>? Scope, string? Error) ParseVersusScopeQuery(string query)
    {
        var songs = LxnsDataFetcher.GetSharedSongList();
        if (!ChunithmVersusScope.TryParse(query, songs, out var scope, out var error) || scope is null)
            return (null, error ?? $"无法解析范围：{query}");

        return (new VersusScopeSelection<ChunithmVersusChart>(
            ChunithmVersusScope.SelectCharts(scope, songs), scope.SortLabel), null);
    }

    private static string VersusVersion(IEnumerable<ChunithmVersusChart> charts)
    {
        var versions = charts.Select(x => x.Song.Version).Distinct(StringComparer.OrdinalIgnoreCase).Take(2).ToArray();
        return versions.Length == 1 ? versions[0] : string.Empty;
    }

    private void OpenVersusRoom(Message message, (long[] Joined, long[] Unbound) members,
        VersusRoomPlan<ChunithmVersusChart> plan, VersusRoomPresentation presentation)
    {
        VersusRoomSession.OpenRendered<ChunithmScore, ChunithmVersusChart, ChunithmVersusMulti>(this, message, members, plan,
            chart => (chart.Id, chart.LevelIndex), chart => chart.Constant, ResolveBattlePlayer,
            (charts, players) => new ChunithmVersusMulti(plan.Title, presentation.Version, charts, players),
            result => result.ForRoom(),
            ChunithmDraw.DrawVersusMulti, ChunithmDraw.DrawVersusMultiBatch, result => result.PageCount, presentation.View);
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
