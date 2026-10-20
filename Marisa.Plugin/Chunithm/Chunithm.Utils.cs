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
        ChartKey = chart => chart.ScoreKey,
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

    private (ResolvedPlayer? Player, string? Error) ResolveVersusPlayer(Message message, long qq) =>
        ResolveScorePlayer(message, qq, "chu vs");

    private (ResolvedPlayer? Player, string? Error) ResolveScorePlayer(Message message, long qq, string command)
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
                return (null, $"{command} 暂仅支持水鱼和落雪，请使用 chu bind 改绑");
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

    private static IReadOnlyList<ChunithmVersusChart> VersusLevelCharts(int level) => level == 5
        ? ChunithmVersusCatalog.WorldsEndCharts(LxnsDataFetcher.GetSharedSongList()).Values.ToArray()
        : ChunithmVersusCatalog.StandardCharts(LxnsDataFetcher.GetSharedSongList()).Where(x => x.LevelIndex == level).ToArray();

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
        IReadOnlyList<ChunithmVersusChart> charts = cnSong is null ? []
            : selection.LevelIndex == 5 ? ChunithmVersusCatalog.WorldsEndCharts([cnSong]).Values.OrderBy(x => x.ScoreKey.Id).ToArray()
            : ChunithmVersusCatalog.FindChart(cnSong, selection.LevelIndex) is { } chart ? [chart] : [];
        if (charts.Count == 0)
        {
            message.Reply($"国服暂未收录「{song.Title}」的 {levelName} 谱面");
            return null;
        }

        return new VersusSingleSelection<ChunithmVersusChart>(charts, false, $"{song.Title}（{levelName}）", VersusVersion(charts), "歌曲 ID 升序");
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
            chart => chart.ScoreKey, chart => chart.Constant, ResolveBattlePlayer,
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

    /// <summary>
    ///     b30/b50 的筛选版：先拉全量成绩，挑出满足 <paramref name="predicate" /> 的谱面，
    ///     再按曲目版本分成「旧 / 新」两组。b50 取旧 30 + 新 20，b30 合并后按 rating 取前 30。
    /// </summary>
    private static async Task<ChunithmRating> GetFilteredBest(
        ResolvedPlayer player,
        Func<ChunithmScore, bool> predicate,
        bool b50
    )
    {
        var fetcher  = player.Fetcher;
        var rating   = await fetcher.GetRating(player);
        var scores   = await fetcher.GetScores(player);
        var newest   = await DataFetcher.FetchLatestVersions();
        var versions = fetcher.GetSongList().ToDictionary(song => song.Id, song => song.Version);

        var groups = scores.Values
            .Where(predicate)
            .GroupBy(score => newest.Contains(DataFetcher.NormalizeVersion(versions.GetValueOrDefault(score.Id, ""))))
            .ToList();

        var oldScores = groups.FirstOrDefault(g => !g.Key)?.OrderByDescending(s => s.Rating).ToArray() ?? [];
        var newScores = groups.FirstOrDefault(g => g.Key)?.OrderByDescending(s => s.Rating).ToArray() ?? [];

        rating.IsB50 = b50;
        rating.Records = b50
            ? new Records
            {
                Best   = oldScores.Take(30).ToArray(),
                Recent = newScores.Take(20).ToArray()
            }
            : new Records
            {
                Best   = oldScores.Concat(newScores).OrderByDescending(s => s.Rating).Take(30).ToArray(),
                Recent = []
            };

        return rating;
    }

    #region 汇总查询的排序与筛选后缀

    private static string ParseSortFlag(ref string cmd)
    {
        if (cmd.EndsWith(" -d")) { cmd = cmd[..^3]; return "desc"; }
        if (cmd.EndsWith(" -a")) { cmd = cmd[..^3]; return "asc"; }
        return "";
    }

    private static readonly (string Flag, ChunithmChartFilter Filter)[] ChartFilterFlags =
    [
        ("--op",    ChunithmChartFilter.OverPower),
        ("--aj",    ChunithmChartFilter.AllJustice),
        ("--fc",    ChunithmChartFilter.FullCombo),
        ("--ajc",   ChunithmChartFilter.AllJusticeCritical),
        ("--ss",    ChunithmChartFilter.Ss),
        ("--ss+",   ChunithmChartFilter.SsPlus),
        ("--sss",   ChunithmChartFilter.Sss),
        ("--sss+",  ChunithmChartFilter.SssPlus),
        ("--!aj",   ChunithmChartFilter.NotAllJustice),
        ("--!fc",   ChunithmChartFilter.NotFullCombo),
        ("--!ajc",  ChunithmChartFilter.NotAllJusticeCritical),
        ("--!ss",   ChunithmChartFilter.NotSs),
        ("--!ss+",  ChunithmChartFilter.NotSsPlus),
        ("--!sss",  ChunithmChartFilter.NotSss),
        ("--!sss+", ChunithmChartFilter.NotSssPlus)
    ];

    /// <summary>剥掉末尾的单个后缀，整个命令只有后缀时也算。</summary>
    private static bool StripFlag(ref string cmd, string flag)
    {
        if (cmd.Equals(flag, StringComparison.Ordinal))
        {
            cmd = "";

            return true;
        }

        if (!cmd.EndsWith(" " + flag, StringComparison.Ordinal)) return false;

        cmd = cmd[..^(flag.Length + 1)];

        return true;
    }

    /// <summary>剥掉末尾的谱面筛选后缀，可以叠加。</summary>
    private static ChunithmChartFilter ParseChartFilterFlag(ref string cmd)
    {
        var filter = ChunithmChartFilter.None;

        while (true)
        {
            var stripped = false;

            foreach (var (flag, value) in ChartFilterFlags)
            {
                if (!StripFlag(ref cmd, flag)) continue;

                filter  |= value;
                stripped =  true;

                break;
            }

            if (!stripped) return filter;
        }
    }

    /// <summary>同时解析排序与谱面筛选后缀，两类后缀的先后顺序随意。</summary>
    private static (string Sort, ChunithmChartFilter Filter) ParseSummaryFlags(ref string cmd)
    {
        var sort    = "";
        var filter  = ChunithmChartFilter.None;
        var changed = true;

        while (changed)
        {
            var before = cmd;

            var current = ParseSortFlag(ref cmd);
            if (current.Length > 0) sort = current;

            filter |= ParseChartFilterFlag(ref cmd);

            changed = cmd.Length != before.Length;
        }

        return (sort, filter);
    }

    #endregion

    #region 汇总图的标题

    private static readonly string[] ComboRankNames = ["FC", "AJ", "AJC"];
    private static readonly string[] ScoreRankNames = ["SS", "SS+", "SSS", "SSS+"];

    private static readonly ChunithmChartFilter[] ComboFlags =
        [ChunithmChartFilter.FullCombo, ChunithmChartFilter.AllJustice, ChunithmChartFilter.AllJusticeCritical];

    private static readonly ChunithmChartFilter[] NotComboFlags =
        [ChunithmChartFilter.NotFullCombo, ChunithmChartFilter.NotAllJustice, ChunithmChartFilter.NotAllJusticeCritical];

    private static readonly ChunithmChartFilter[] ScoreFlags =
        [ChunithmChartFilter.Ss, ChunithmChartFilter.SsPlus, ChunithmChartFilter.Sss, ChunithmChartFilter.SssPlus];

    private static readonly ChunithmChartFilter[] NotScoreFlags =
        [ChunithmChartFilter.NotSs, ChunithmChartFilter.NotSsPlus, ChunithmChartFilter.NotSss, ChunithmChartFilter.NotSssPlus];

    /// <summary>汇总图顶部的标题：[类别][参数][筛选条件]完成表，没有筛选条件时省掉最后一段。</summary>
    private static string SummaryTitle(string category, string argument, ChunithmChartFilter filter)
    {
        var parts = new List<string> { category, argument };

        if (DescribeChartFilter(filter) is { Length: > 0 } filterText) parts.Add(filterText);

        return $"[{string.Join("][", parts)}]完成表";
    }

    /// <summary>把筛选后缀归纳成一句话，只保留有信息量的部分：冗余的取最严格的那个，区间写成 A~B。</summary>
    private static string DescribeChartFilter(ChunithmChartFilter filter)
    {
        var parts = new List<string>();

        if ((filter & ChunithmChartFilter.OverPower) != 0) parts.Add("OP对象");

        if (DescribeRankSpan(filter, ComboFlags, NotComboFlags, ComboRankNames) is { Length: > 0 } combo) parts.Add(combo);
        if (DescribeRankSpan(filter, ScoreFlags, NotScoreFlags, ScoreRankNames) is { Length: > 0 } span) parts.Add(span);

        return string.Concat(parts);
    }

    /// <summary>
    ///     把一组「≥ 某档」的正向后缀和「&lt; 某档」的取反后缀归纳成区间：
    ///     `SS`（仅 SS）/ `SS~SSS+`（SS 及以上）/ `SS~SSS`（SS 到 SSS）/ `未SS`（没到 SS）。
    /// </summary>
    private static string DescribeRankSpan(
        ChunithmChartFilter filter,
        IReadOnlyList<ChunithmChartFilter> yes,
        IReadOnlyList<ChunithmChartFilter> no,
        IReadOnlyList<string> names)
    {
        var lower = -1;
        for (var i = 0; i < names.Count; i++)
        {
            if ((filter & yes[i]) != 0) lower = i;
        }

        var exclusive = int.MaxValue;
        for (var i = 0; i < names.Count; i++)
        {
            if ((filter & no[i]) != 0) exclusive = i;
        }

        var top   = names.Count - 1;
        var upper = exclusive - 1;

        if (lower >= 0)
        {
            if (exclusive == int.MaxValue) return lower == top ? names[top] : $"{names[lower]}~{names[top]}";
            if (upper < lower) return "无";

            return upper == lower ? names[lower] : $"{names[lower]}~{names[upper]}";
        }

        return exclusive == int.MaxValue ? "" : $"未{names[exclusive]}";
    }

    #endregion
}

/// <summary>汇总类指令末尾的谱面筛选后缀，可以叠加。</summary>
[Flags]
public enum ChunithmChartFilter
{
    None                  = 0,
    OverPower             = 1 << 0,
    FullCombo             = 1 << 1,
    AllJustice            = 1 << 2,
    AllJusticeCritical    = 1 << 3,
    Ss                    = 1 << 4,
    SsPlus                = 1 << 5,
    Sss                   = 1 << 6,
    SssPlus               = 1 << 7,
    NotFullCombo          = 1 << 8,
    NotAllJustice         = 1 << 9,
    NotAllJusticeCritical = 1 << 10,
    NotSs                 = 1 << 11,
    NotSsPlus             = 1 << 12,
    NotSss                = 1 << 13,
    NotSssPlus            = 1 << 14
}

public static class ChunithmChartFilterExtensions
{
    /// <summary>与成绩有关的后缀，AJC ⊂ AJ ⊂ FC，SS ⊂ SS+ ⊂ SSS ⊂ SSS+。</summary>
    private static readonly (ChunithmChartFilter Yes, ChunithmChartFilter No, Func<ChunithmScore, bool> Test)[]
        AchievementTests =
        [
            (ChunithmChartFilter.FullCombo, ChunithmChartFilter.NotFullCombo,
                score => score.IsFullCombo),
            (ChunithmChartFilter.AllJustice, ChunithmChartFilter.NotAllJustice,
                score => score.IsAllJustice),
            (ChunithmChartFilter.AllJusticeCritical, ChunithmChartFilter.NotAllJusticeCritical,
                score => score.IsAllJusticeCritical),
            (ChunithmChartFilter.Ss, ChunithmChartFilter.NotSs,
                score => score.Achievement >= ChunithmScore.RankSs),
            (ChunithmChartFilter.SsPlus, ChunithmChartFilter.NotSsPlus,
                score => score.Achievement >= ChunithmScore.RankSsp),
            (ChunithmChartFilter.Sss, ChunithmChartFilter.NotSss,
                score => score.Achievement >= ChunithmScore.RankSss),
            (ChunithmChartFilter.SssPlus, ChunithmChartFilter.NotSssPlus,
                score => score.Achievement >= ChunithmScore.RankSssp)
        ];

    private static readonly ChunithmChartFilter AchievementFilters =
        AchievementTests.Aggregate(ChunithmChartFilter.None, (all, test) => all | test.Yes | test.No);

    /// <summary>按 op 对象谱面与成绩状态筛选谱面。</summary>
    public static IEnumerable<(double constant, int i, ChunithmSong song)> FilterCharts(
        this IEnumerable<(double constant, int i, ChunithmSong song)> charts,
        ChunithmChartFilter filter,
        IReadOnlyDictionary<(long Id, int LevelIdx), ChunithmScore> scores)
    {
        if ((filter & ChunithmChartFilter.OverPower) != 0)
        {
            charts = charts.Where(x => x.i == x.song.OpObjectIndex());
        }

        if ((filter & AchievementFilters) == 0) return charts;

        return charts.Where(x => Matches(filter, scores.GetValueOrDefault((x.song.Id, x.i))));
    }

    /// <summary>每个后缀都要成立，所以取反也能和正向叠加，没游玩的谱面只有取反会保留。</summary>
    private static bool Matches(ChunithmChartFilter filter, ChunithmScore? score)
    {
        foreach (var (yes, no, test) in AchievementTests)
        {
            if ((filter & yes) != 0 && (score is null || !test(score))) return false;
            if ((filter & no) != 0 && score is not null && test(score)) return false;
        }

        return true;
    }
}
