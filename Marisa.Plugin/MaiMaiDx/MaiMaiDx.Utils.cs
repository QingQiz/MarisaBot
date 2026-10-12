using System.Security.Cryptography;
using Marisa.Configuration;
using Marisa.Database;
using Marisa.Plugin.Shared.Dialog;
using Marisa.Plugin.Shared.DivingFish;
using Marisa.Plugin.Shared.Lxns;
using Marisa.Plugin.Shared.Interface;
using MaiBattleChart = (double Constant, int LevelIdx, Marisa.Plugin.Shared.MaiMaiDx.MaiMaiSong Song);
using Marisa.Plugin.Shared.MaiMaiDx;
using Marisa.Plugin.Shared.MaiMaiDx.DataFetcher;
using Marisa.Plugin.Shared.Versus;
using Marisa.Plugin.Shared.Util.Cacheable;
using Marisa.Plugin.Shared.Util.SongDb;

namespace Marisa.Plugin.MaiMaiDx;

public partial class MaiMaiDx
{
    private const int MaxVersusRandomCount = MaiVersusBatch.DefaultPageSize;

    private static bool VersusBatchTrigger(Message message, IServiceProvider _serviceProvider) =>
        VersusCommands.BatchTrigger(message, query => PlateData.TryParseScope(query, out _, out _));

    private static bool VersusRandomTrigger(Message message, IServiceProvider _serviceProvider) =>
        VersusCommands.RandomTrigger(message);

    private VersusBattle? _battle;

    VersusBattle ICanBattleWithOthers.Battle => _battle ??= new VersusBattle<SongScore, MaiBattleChart>
    {
        CommandPrefix = "mai",
        ResolvePlayer = ResolveBattlePlayer,
        IsOpponentBindingError = IsOpponentBindingError,
        SelectSingle = SelectVersusSingle,
        ParseRandom = (message, usage) => ParseVersusRandom(message, usage) is { } parsed
            ? new VersusRandomSelection<MaiBattleChart>(parsed.Count, parsed.Query, parsed.Charts) : null,
        ParseBatch = message => ParseVersusScope(message) is { } parsed
            ? new VersusBatchSelection<MaiBattleChart>(parsed.Query, parsed.SortLabel, parsed.Charts) : null,
        ChartKey = chart => (chart.Song.Id, chart.LevelIdx),
        VersionOf = charts => VersusVersion(charts),
        ReplySingle = ReplyVersusSingle,
        ReplyBatch = ReplyVersusBatch,
        OpenRoom = (message, members, plan, version, singleSong) => OpenVersusRoom(message, members,
            new VersusRoomPlan(plan.Title, version, plan.Charts, plan.PickCount, singleSong))
    };

    private (VersusPlayerBinding<SongScore>? Player, string? Error) ResolveBattlePlayer(Message message, long qq)
    {
        var (player, error) = ResolveVersusPlayer(message, qq);
        return player is null ? (null, error) : (new(qq, () => FetchBattleData(player)), null);
    }

    private static async Task<VersusFetchResult<SongScore>> FetchBattleData(ResolvedPlayer player)
    {
        try
        {
            var (nickname, scores) = await player.Fetcher.GetScores(player);
            return new(new VersusPlayer<SongScore>(player.Qq, nickname ?? $"QQ {player.Qq}", scores), null);
        }
        catch (HttpRequestException e)
        {
            return new(null, e.Message);
        }
    }

    private async Task<VersusSingleSelection<MaiBattleChart>?> SelectVersusSingle(Message message)
    {
        var selection = ResolveVersusQuery(SongDb, message.Command.Trim().ToString());
        var level = selection.LevelIndex;
        var levelName = MaiMaiSong.LevelNameZh[level];
        if (selection.Random)
        {
            return new(VersusLevelCharts(level), true,
                level == 3 ? "随机单曲" : $"随机单曲（{levelName}谱）", string.Empty);
        }

        var song = await SongDb.MultiPageSelectResult(selection.Songs, message, false, true);
        if (song is null) return null;
        if (level >= song.Levels.Count)
        {
            message.Reply($"该歌曲没有{levelName}谱");
            return null;
        }
        return new([(song.Constants[level], level, song)], false, $"{song.Title}（{levelName}谱）", song.Version);
    }

    private async Task ReplyVersusSingle(Message message, MaiBattleChart chart,
        VersusPlayer<SongScore> self, VersusPlayer<SongScore> opponent)
    {
        var song = chart.Song;
        var levelIdx = chart.LevelIdx;
        var selfScore = self.Scores.GetValueOrDefault((song.Id, levelIdx));
        var opponentScore = opponent.Scores.GetValueOrDefault((song.Id, levelIdx));
        var winnerIndex = VersusRules.Compare(selfScore?.Achievement, opponentScore?.Achievement) switch
        {
            "left" => 0,
            "right" => 1,
            _ => -1
        };
        var context = new WebContext(new
        {
            versus = new
            {
                Song = new { song.Id, song.Title, song.Type, song.Info.Artist, song.Info.Genre, song.Info.Bpm, song.Info.From, song.Info.IsNew },
                LevelIndex = levelIdx,
                Level = song.Levels[levelIdx],
                Constant = song.Constants[levelIdx],
                MaxDx = song.Charts[levelIdx].Notes.Sum() * 3,
                Players = new[]
                {
                    new { Nickname = self.Name, Played = selfScore != null, Score = ProjectScore(selfScore) },
                    new { Nickname = opponent.Name, Played = opponentScore != null, Score = ProjectScore(opponentScore) }
                },
                WinnerIndex = winnerIndex
            }
        });
        message.Reply(MessageDataImage.FromBase64(await WebApi.MaiMaiVersus(context.Id)));
        return;

        object? ProjectScore(SongScore? score) => score == null ? null : new
        {
            score.Achievement,
            Rank = SongScore.CalcRank(score.Achievement),
            Rating = song.Ra(levelIdx, score.Achievement),
            score.DxScore,
            score.Fc,
            score.Fs
        };
    }

    private async Task ReplyVersusBatch(Message message, string title, string version, string sort,
        IReadOnlyList<MaiBattleChart> charts, VersusPlayer<SongScore> self, VersusPlayer<SongScore> opponent)
    {
        var batch = new MaiVersusBatch(title, version, sort, charts,
            new MaiVersusBatch.Player(self.Name, self.Scores), new MaiVersusBatch.Player(opponent.Name, opponent.Scores));
        await ReplyPages(message, batch.PageCount, page => MaiMaiDraw.DrawVersusBatch(batch, page));
    }

    /// <summary>解析单曲 vs 的歌曲与难度；空输入或只有难度时随机选曲。</summary>
    private static (List<MaiMaiSong> Songs, int LevelIndex, bool Random)
        ResolveVersusQuery(Shared.Util.SongDb.SongDb<MaiMaiSong> songs, string input)
    {
        var query = input.Trim();
        if (query.Length == 0) return ([], 3, true);

        var exact = songs.SearchSongExact(query.AsMemory());
        if (exact.Count > 0) return (exact, 3, false);
        if (PlateData.DifficultyAliasMap.TryGetValue(query, out var difficulty)) return ([], difficulty, true);

        var hasAffix = PlateData.TryStripDifficultyAffix(query.AsMemory(), out var level, out var rest);
        if (hasAffix)
        {
            exact = songs.SearchSongExact(rest);
            if (exact.Count > 0) return (exact, level, false);
        }

        var fuzzy = songs.SearchSong(query.AsMemory());
        if (fuzzy.Count > 0) return (fuzzy, 3, false);
        return hasAffix ? (songs.SearchSong(rest), level, false) : ([], 3, false);
    }

    private Task ReplyPages(Message message, int pageCount, Func<int, Task<string>> render) =>
        VersusPaging.ReplyPages(this, message, pageCount, render);

    /// <summary>解析 `N [难度/范围]`；失败时已回复原因。</summary>
    private (int Count, string Query, IReadOnlyList<(double Constant, int LevelIdx, MaiMaiSong Song)> Charts)?
        ParseVersusRandom(Message message, string usage)
    {
        var command = message.Command.Trim().ToString();
        var digits  = command.TakeWhile(char.IsAsciiDigit).Count();
        if (!int.TryParse(command[..digits], out var count) || count is < 1 or > MaxVersusRandomCount)
        {
            message.Reply($"N 须为 1～{MaxVersusRandomCount} 的整数，用法：{usage}");
            return null;
        }

        var query    = command[digits..].Trim();
        var levelIdx = 3;
        if (query.Length == 0 || PlateData.DifficultyAliasMap.TryGetValue(query, out levelIdx))
        {
            return (count, query, VersusLevelCharts(levelIdx));
        }

        if (PlateData.TryParseScope(query, out var scope, out _))
        {
            return (count, query, PlateData.SelectScopeCharts(scope, SongDb.SongList));
        }

        message.Reply($"无法解析难度或完成表范围：{query}");
        return null;
    }

    /// <summary>解析完成表范围并确认范围内有谱面；失败时已回复原因。</summary>
    private (string Query, string SortLabel, IReadOnlyList<(double Constant, int LevelIdx, MaiMaiSong Song)> Charts)?
        ParseVersusScope(Message message)
    {
        var query = message.Command.Trim().ToString();
        if (!PlateData.TryParseScope(query, out var scope, out _))
        {
            message.Reply($"无法解析完成表范围：{query}");
            return null;
        }

        var charts = PlateData.SelectScopeCharts(scope, SongDb.SongList);
        if (charts.Count == 0)
        {
            message.Reply($"没有找到 {query} 对应的谱面");
            return null;
        }

        var sortLabel = scope.Selectors.Any(x => x is PlateData.Selector.Constant or PlateData.Selector.ConstantRange)
            ? "歌曲 ID 升序"
            : "定数降序";

        return (query, sortLabel, charts);
    }

    private List<(double Constant, int LevelIdx, MaiMaiSong Song)> VersusLevelCharts(int levelIdx) =>
        SongDb.SongList
            .Where(song => song.Levels.Count > levelIdx && song.Constants.Count > levelIdx && song.Charts.Count > levelIdx)
            .Select(song => (song.Constants[levelIdx], levelIdx, song))
            .ToList();

    /// <summary>范围内只有一个版本时返回该版本，卡片据此显示版本 logo。</summary>
    private static string VersusVersion(IEnumerable<(double Constant, int LevelIdx, MaiMaiSong Song)> charts)
    {
        var versions = charts.Select(x => x.Song.Version).Distinct().Take(2).ToArray();
        return versions.Length == 1 ? versions[0] : string.Empty;
    }

    /// <summary>
    ///     房间挂在群级 dialog 上，用不带前缀的「加入」「退出」「开始」「取消」操作，其它消息原样放行；
    ///     闲置超时后由下一条消息静默结束。
    /// </summary>
    private void OpenVersusRoom(Message message, (long[] Joined, long[] Unbound) members, VersusRoomPlan plan)
    {
        VersusRoomSession.Open<SongScore, (double Constant, int LevelIdx, MaiMaiSong Song), MaiVersusMulti>(
            this, message, members, new(plan.Title, plan.Charts, plan.PickCount),
            chart => (chart.Song.Id, chart.LevelIdx), chart => chart.Constant,
            (next, qq) => ResolveVersusPlayer(next, qq).Error,
            FetchRoomPlayer,
            (charts, players) => new MaiVersusMulti(plan.Title, plan.Version, charts,
                players.Select(p => new MaiVersusMulti.Player(p.Qq, p.Name, p.Scores)).ToArray()),
            result => result.Players.Select((p, i) =>
                new VersusRoundPlayer(p.Qq, p.Name, result.Totals[i].Points, result.Totals[i].Matchups)).ToArray(),
            ReplyRound);
        return;

        async Task<VersusFetchResult<SongScore>> FetchRoomPlayer(Message next, long qq)
        {
            var (resolved, error) = ResolveVersusPlayer(next, qq);
            if (resolved is null) return new(null, error);

            return await FetchBattleData(resolved);
        }

        async Task ReplyRound(Message next, MaiVersusMulti result, int round, IReadOnlyList<VersusStanding> standings)
        {
            var projected = standings.Select(s =>
                new MaiVersusMulti.Standing(s.Name, s.Wins, s.Matchups, s.Rounds, s.Left)).ToArray();
            if (plan.SingleSong)
            {
                next.Reply(MessageDataImage.FromBase64(await MaiMaiDraw.DrawVersusMulti(result, round, projected)));
                return;
            }

            await ReplyPages(next, result.PageCount, page => MaiMaiDraw.DrawVersusMultiBatch(result, page, round, projected));
        }
    }

    /// <param name="PickCount">每轮随机抽取的谱面数；为空时比较全部谱面，比完即关房。</param>
    private sealed record VersusRoomPlan(
        string Title,
        string Version,
        IReadOnlyList<(double Constant, int LevelIdx, MaiMaiSong Song)> Charts,
        int? PickCount,
        bool SingleSong)
    {
        public bool Continuous => PickCount is not null;
    }

    private static string DeviceBindingLabel(long qq)
    {
        var value = qq.ToString();
        return value.Length > 4 ? $"QQ {value[..2]}****{value[^2..]}" : $"QQ {value}";
    }

    private string[]? _versions;

    private string[] Versions => _versions ??= BuildVersionList(SongDb.SongList);

    private void ResetCaches()
    {
        _versions = null;
    }

    private static string[] BuildVersionList(IReadOnlyList<MaiMaiSong> songs)
    {
        return VersionOrderHelper.BuildVersionList(songs, song => song.Version, song => song.Id);
    }

    private static string[]? ResolveSummaryVersions(string input, IReadOnlyList<string> versions)
    {
        var key = input.Trim();
        var direct = versions.FirstOrDefault(v => v.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (direct != null) return [direct];

        if (!PlateData.PlateVersionMap.TryGetValue(key, out var mapped))
        {
            return null;
        }

        var selected = mapped
            .Select(version => versions.FirstOrDefault(v => v.Equals(version, StringComparison.OrdinalIgnoreCase)))
            .OfType<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return selected.Length == 0 ? null : selected;
    }

    private static IReadOnlyList<(double Constant, int LevelIdx, MaiMaiSong Song)> SelectVersionSummaryCharts(
        IReadOnlyList<MaiMaiSong> songs, IReadOnlyCollection<string> versions)
    {
        return songs
            .Where(song => versions.Contains(song.Version, StringComparer.OrdinalIgnoreCase))
            .SelectMany(song => song.Constants.Select((constant, i) => (Constant: constant, LevelIdx: i, Song: song)))
            .Where(chart => chart.LevelIdx == 3)
            .OrderByDescending(chart => chart.Constant)
            .ToArray();
    }

    #region 等级/定数解析

    /// <summary>严格解析等级（纯数字可带尾加号，禁符号/空白/前导零；加号等级最高 14+），
    /// 输出规范串。宽松的 int.TryParse 会放行 "+13"/"013"/"13 +" 这类写法。</summary>
    private static bool TryParseLevel(string value, out string level)
    {
        level = "";
        var plus = value.EndsWith('+');
        var core = plus ? value[..^1] : value;

        if (core.Length is 0 or > 2 || !core.All(char.IsAsciiDigit) || core[0] == '0') return false;

        var lv = int.Parse(core);
        if (lv < 1 || lv > (plus ? 14 : 15)) return false;

        level = plus ? $"{lv}+" : $"{lv}";
        return true;
    }

    /// <summary>严格解析定数（X 或 X.X，一位小数，禁符号/千分位/NaN）。宽松的 double.TryParse
    /// 会放行 "NaN"（穿过范围比较）、"14.75"（被格式化静默取整）、zh-CN 下的 "1,4"（千分位）。</summary>
    private static bool TryParseConstant(string value, out double constant)
    {
        constant = 0;
        var dot = value.IndexOf('.');
        var ip  = dot < 0 ? value : value[..dot];
        var fp  = dot < 0 ? "0" : value[(dot + 1)..];

        if (ip.Length is 0 or > 2 || !ip.All(char.IsAsciiDigit) || ip[0] == '0') return false;
        if (fp.Length != 1 || !char.IsAsciiDigit(fp[0])) return false;

        constant = int.Parse(ip) + (fp[0] - '0') / 10.0;
        return constant is >= 1 and <= 15;
    }

    /// <summary>难度曲线数据的版本哈希（进程内取一次）：读前端分发的 difficulty_curves.json，
    /// 数据随前端更新后带哈希的缓存文件名自动翻新；取不到时按天退化。</summary>
    private static readonly Lazy<string> CurveDataHash = new(() =>
    {
        try
        {
            using var http = new HttpClient();
            var bytes = http.GetByteArrayAsync(
                ConfigurationManager.Configuration.Web.PrivateBaseUrl + "/assets/maimai/difficulty_curves.json").Result;
            return Convert.ToHexString(MD5.HashData(bytes))[..12];
        }
        catch
        {
            return DateTime.Today.ToString("yyyyMMdd");
        }
    });

    #endregion

    #region value analysis

    private static bool TryParseValueAnalysisCommand(
        ReadOnlyMemory<char> command,
        out MaiValueAnalysisMode mode,
        out MaiValueAnalysisFilter filter)
    {
        mode = default;
        filter = MaiValueAnalysisFilter.Empty;
        var input = command.ToString().Trim();

        const string goldSuffix = "含金量分析";
        const string waterSuffix = "水分分析";
        string filterToken;
        if (input.EndsWith(goldSuffix, StringComparison.OrdinalIgnoreCase))
        {
            mode        = MaiValueAnalysisMode.Gold;
            filterToken = input[..^goldSuffix.Length].Trim();
        }
        else if (input.EndsWith(waterSuffix, StringComparison.OrdinalIgnoreCase))
        {
            mode        = MaiValueAnalysisMode.Water;
            filterToken = input[..^waterSuffix.Length].Trim();
        }
        else
        {
            return false;
        }

        return filterToken.Length == 0 || MaiValueAnalysisFilters.TryParse(filterToken, out filter);
    }

    private static bool FilteredValueAnalysisTrigger(Message message, IServiceProvider _serviceProvider)
    {
        return TryParseValueAnalysisCommand(message.Command, out _, out var filter) && !filter.IsEmpty;
    }

    private async Task<MarisaPluginTaskState> ValueAnalysis(
        Message message,
        MaiValueAnalysisMode mode,
        MaiValueAnalysisFilter filter)
    {
        var player  = ResolvePlayer(message);
        var rating  = await player.Fetcher.GetRating(player);
        var engine  = new MaiValueAnalysisEngine(SongDb.SongList);
        IReadOnlyList<SongScore> scores;
        string scope;
        if (filter.IsEmpty)
        {
            scores = rating.OldScores.Concat(rating.NewScores).ToList();
            scope  = "B50";
        }
        else
        {
            scores = engine.FilterScores((await player.Fetcher.GetScores(player)).Scores.Values, filter);
            scope  = MaiValueAnalysisFilters.Scope(filter);
        }

        if (scores.Count == 0)
        {
            message.Reply(filter.IsEmpty ? "当前 B50 中没有可分析的成绩" : "没有符合筛选条件的成绩");
            return MarisaPluginTaskState.CompletedTask;
        }

        var fallback = engine.RequiresFallback(scores)
            ? await DivingFishChartStatsProvider.Default.GetAsync()
            : DivingFishChartStatsCatalog.Empty;
        var data = engine.Build(
            rating.Nickname,
            rating.Rating,
            scope,
            scores,
            mode,
            fallback);

        if (data.AnalyzedCount == 0)
        {
            message.Reply("这些成绩目前都没有可用的拟合定数");
            return MarisaPluginTaskState.CompletedTask;
        }

        var context = new WebContext(new { analysis = data });
        message.Reply(MessageDataImage.FromBase64(await WebApi.MaiMaiValueAnalysis(context.Id)));
        return MarisaPluginTaskState.CompletedTask;
    }

    #endregion

    #region recommend

    private MaiMaiRecommendationEngine CreateRecommendationEngine()
    {
        return new MaiMaiRecommendationEngine(SongDb.SongList);
    }

    #endregion

    #region data fetcher

    /// <summary>
    ///     解析查询目标：@ 优先，其次命令文本（仅当调用方允许把它当账号名时），否则发送者自己；
    ///     并按该 QQ 的本地绑定选定查分器。只查本地库，不发网络请求，fetcher 不再自己从消息里反推目标。
    /// </summary>
    private ResolvedPlayer ResolvePlayer(Message message, bool allowUsername = false)
    {
        var at = message.MessageChain!.Messages.FirstOrDefault(m => m.Type == MessageDataType.At);

        // 账号名查询只有水鱼有公开接口；账号名不代表本人，一律拿不到 OAuth 票据
        if (at is null && allowUsername && !message.Command.IsWhiteSpace())
        {
            return new ResolvedPlayer(message.Sender.Id, message.Command.Trim().ToString(), false,
                GetDataFetcher(DataFetcherType.DivingFish));
        }

        var qq = (at as MessageDataAt)?.Target ?? message.Sender.Id;

        return new ResolvedPlayer(qq, null, qq == message.Sender.Id, GetDataFetcher(qq));
    }

    /// <summary>
    ///     vs 的一方：对手必然来自 @。要求能读到对方的完整成绩——华立不支持 vs，
    ///     水鱼/落雪都要求本地存有该 QQ 的 OAuth 授权。只查绑定表与票据表，不发网络请求。
    /// </summary>
    private (ResolvedPlayer? Player, string? Error) ResolveVersusPlayer(Message message, long qq)
    {
        var server = ServerOf(qq);

        switch (server)
        {
            // 落雪只有 OAuth 一条取数路径
            case "lxns" when LxnsTokenStore.GetToken(qq) is null:
                return (null, LxnsDataFetcher.NotBoundHint);
            case "lxns":
                return (new ResolvedPlayer(qq, null, qq == message.Sender.Id, GetDataFetcher(server)), null);

            // 水鱼：配置了 OAuth 就必须有授权；未配置时走 bot 的 dev token，不依赖用户票据
            case "DivingFish" or null when DivingFishOAuth.IsConfigured && DivingFishTokenStore.GetToken(qq, "maimai") is null:
                return (null, DivingFishDataFetcher.NotBoundHint);
            case "DivingFish" or null:
                return (new ResolvedPlayer(qq, null, qq == message.Sender.Id, GetDataFetcher(DataFetcherType.DivingFish)), null);

            // 华立等旧绑定不支持 vs（bind 早已不提供华立）
            default:
                return (null, "该查分器不支持 vs，请先 bind 改绑水鱼或落雪");
        }
    }

    /// <summary>该 QQ 绑定的查分器名；没绑定记录时为空。</summary>
    private static string? ServerOf(long qq)
    {
        using var realm = BotDbContext.OpenRealm();

        return realm.All<Marisa.Database.Entity.Plugin.MaiMaiDx.MaiMaiDxBind>()
            .FirstOrDefault(x => x.UId == qq)?.ServerName;
    }

    /// <summary>按绑定选查分器：未绑定时按水鱼处理，认不出的绑定名按华立处理（历史数据的兜底）。</summary>
    private DataFetcher GetDataFetcher(long qq)
    {
        var server = ServerOf(qq);

        return server is null ? GetDataFetcher(DataFetcherType.DivingFish) : GetDataFetcher(server);
    }

    private DataFetcher GetDataFetcher(string server) => server switch
    {
        "lxns" => GetDataFetcher(DataFetcherType.Lxns),
        "DivingFish" => GetDataFetcher(DataFetcherType.DivingFish),
        _ => GetDataFetcher(DataFetcherType.Wahlap),
    };

    /// <summary>授权类错误（对手没绑定，或票据已失效）：要 @ 对手本人，原文里已写明怎么重新绑定。</summary>
    private static bool IsOpponentBindingError(string error) =>
        error.Contains("OAuth", StringComparison.OrdinalIgnoreCase) ||
        error.Contains("未绑定水鱼", StringComparison.OrdinalIgnoreCase);

    private readonly Dictionary<DataFetcherType, DataFetcher> _dataFetchers = new();

    private DataFetcher GetDataFetcher(DataFetcherType type)
    {
        if (_dataFetchers.TryGetValue(type, out var fetcher)) return fetcher;

        return _dataFetchers[type] = type switch
        {
            DataFetcherType.DivingFish => new DivingFishDataFetcher(SongDb),
            DataFetcherType.Wahlap     => new AllNetDataFetcher(SongDb),
            DataFetcherType.Lxns       => new LxnsDataFetcher(SongDb),
            _                          => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
    }

    private enum DataFetcherType
    {
        DivingFish,
        Wahlap,
        Lxns
    }

    #endregion

    #region label helpers

    // diving-fish fc/fs 字段 → 可读标记。fs 的 fsd/fsdp 是 FDX/FDX+ 的老命名。
    private static string FcLabel(string? fc) => fc switch
    {
        "app" => "AP+",
        "ap" => "AP",
        "fcp" => "FC+",
        "fc" => "FC",
        _ => ""
    };

    private static string FsLabel(string? fs) => fs switch
    {
        "fsdp" => "FDX+",
        "fsd" => "FDX",
        "fsp" => "FS+",
        "fs" => "FS",
        "sync" => "SYNC",
        _ => ""
    };

    #endregion
}
