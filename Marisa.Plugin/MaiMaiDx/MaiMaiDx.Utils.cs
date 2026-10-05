using System.Security.Cryptography;
using Marisa.Configuration;
using Marisa.Database;
using Marisa.Plugin.Shared.Dialog;
using Marisa.Plugin.Shared.DivingFish;
using Marisa.Plugin.Shared.Lxns;
using Marisa.Plugin.Shared.MaiMaiDx;
using Marisa.Plugin.Shared.MaiMaiDx.DataFetcher;

namespace Marisa.Plugin.MaiMaiDx;

public partial class MaiMaiDx
{
    private const int MaxVersusRandomCount = MaiVersusBatch.DefaultPageSize;

    /// <summary>双方都能读取完整成绩时返回；否则已回复原因。</summary>
    private (ResolvedPlayer Self, ResolvedPlayer Opponent)? ResolveVersusPlayers(Message message)
    {
        var opponents = message.At().Distinct().ToArray();
        if (opponents.Length != 1)
        {
            message.Reply(opponents.Length == 0 ? "请 @一名对手" : "请只指定一名对手");
            return null;
        }

        var (self, selfError)         = ResolveVersusPlayer(message, message.Sender.Id);
        var (opponent, opponentError) = ResolveVersusPlayer(message, opponents[0]);
        if (opponent is null)
        {
            ReplyOpponentNotBound(message, opponents[0], opponentError!);
            return null;
        }

        if (self is null)
        {
            message.Reply(selfError!);
            return null;
        }

        return (self, opponent);
    }

    /// <summary>并行取双方成绩；失败时已回复原因并返回空。</summary>
    private static async Task<(MaiVersusBatch.Player Self, MaiVersusBatch.Player Opponent)?> FetchVersusScores(
        Message message, (ResolvedPlayer Self, ResolvedPlayer Opponent) players)
    {
        var selfFetch     = FetchBattleData(players.Self);
        var opponentFetch = FetchBattleData(players.Opponent);
        var selfData      = await selfFetch;
        var opponentData  = await opponentFetch;

        if (opponentData.Error is not null && IsOpponentBindingError(opponentData.Error))
        {
            ReplyOpponentNotBound(message, players.Opponent.Qq, opponentData.Error);
            return null;
        }

        if ((selfData.Error ?? opponentData.Error) is { } error)
        {
            message.Reply(error);
            return null;
        }

        return (new MaiVersusBatch.Player(selfData.Nickname ?? $"QQ {players.Self.Qq}", selfData.Scores),
            new MaiVersusBatch.Player(opponentData.Nickname ?? $"QQ {players.Opponent.Qq}", opponentData.Scores));
    }

    /// <summary>授权/网络错误转成 Error 文本交给调用方回复，其余异常照旧抛出。</summary>
    private static async Task<(string? Nickname, Dictionary<(long Id, int LevelIdx), SongScore> Scores, string? Error)>
        FetchBattleData(ResolvedPlayer player)
    {
        try
        {
            var (nickname, scores) = await player.Fetcher.GetScores(player);
            return (nickname, scores, null);
        }
        catch (HttpRequestException e)
        {
            return (null, [], e.Message);
        }
    }

    private static List<MaiMaiSong> SharedVersusSongs(
        IEnumerable<MaiMaiSong> songs, int level,
        IReadOnlyDictionary<(long Id, int LevelIdx), SongScore> left,
        IReadOnlyDictionary<(long Id, int LevelIdx), SongScore> right) =>
        songs.Where(song => song.Levels.Count > level && song.Constants.Count > level &&
                            song.Charts.Count > level && left.ContainsKey((song.Id, level)) &&
                            right.ContainsKey((song.Id, level))).ToList();

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

    private async Task ReplyPages(Message message, int pageCount, Func<int, Task<string>> render)
    {
        var firstPage = await render(1);
        message.Reply(MessageDataImage.FromBase64(firstPage));
        if (pageCount == 1) return;

        var key = (message.GroupInfo?.Id, message.Sender.Id);
        if (!DialogManager.TryAddDialog(key, HandlePage, this)) return;

        async Task<MarisaPluginTaskState> HandlePage(Message next)
        {
            if (!next.IsPlainText()) return MarisaPluginTaskState.Canceled;

            var command = next.Command.Trim().ToString();
            if (command.Length < 2 || command[0] is not ('p' or 'P') ||
                !int.TryParse(command[1..], out var page) || page < 1 || page > pageCount)
            {
                return MarisaPluginTaskState.Canceled;
            }

            var image = page == 1 ? firstPage : await render(page);
            next.Reply(MessageDataImage.FromBase64(image));
            return MarisaPluginTaskState.ToBeContinued;
        }
    }

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

    /// <summary>开房前检查房主能 vs；返回能直接加入的被邀请人和还没绑定的被邀请人。失败时已回复原因。</summary>
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
        var unbound  = invitees.Where(x => ResolveVersusPlayer(message, x).Player is null).ToArray();
        var joined   = invitees.Except(unbound).ToArray();
        if (joined.Length >= MaiVersusRoom.MaxPlayers)
        {
            message.Reply($"一个房间最多 {MaiVersusRoom.MaxPlayers} 人");
            return null;
        }

        return (joined, unbound);
    }

    /// <summary>
    ///     房间挂在群级 dialog 上，用不带前缀的「加入」「退出」「开始」「取消」操作，其它消息原样放行；
    ///     闲置超时后由下一条消息静默结束。
    /// </summary>
    private void OpenVersusRoom(Message message, (long[] Joined, long[] Unbound) members, VersusRoomPlan plan)
    {
        var room = new MaiVersusRoom(message.Sender.Id, members.Joined);
        if (!DialogManager.TryAddDialog((message.GroupInfo!.Id, null), HandleRoom, this))
        {
            message.Reply("群里已经有进行中的房间或游戏");
            return;
        }

        var announce = new MessageBuilder(message).Text($"多人对战：{plan.Title}（{room.Count}/{MaiVersusRoom.MaxPlayers}）\n");
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

        announce.Text("发送「加入」参加，房主发送「开始」开打").Reply();
        return;

        async Task<MarisaPluginTaskState> HandleRoom(Message next)
        {
            if (room.IsExpired) return MarisaPluginTaskState.Canceled;

            return next.Command.Trim().ToString() switch
            {
                "加入" => JoinRoom(next),
                "退出" => LeaveRoom(next),
                "开始" => await StartRound(next),
                "取消" when next.Sender.Id == room.Host => CancelRoom(next),
                _ => MarisaPluginTaskState.NoResponse
            };
        }

        MarisaPluginTaskState JoinRoom(Message next)
        {
            var (player, error) = ResolveVersusPlayer(next, next.Sender.Id);
            if (player is null)
            {
                next.Reply(error!);
                return MarisaPluginTaskState.ToBeContinued;
            }

            switch (room.Join(next.Sender.Id))
            {
                case MaiVersusRoom.JoinResult.Joined:
                    next.Reply($"已加入（{room.Count}/{MaiVersusRoom.MaxPlayers}）");
                    break;
                case MaiVersusRoom.JoinResult.Full:
                    next.Reply("房间已满");
                    break;
                case MaiVersusRoom.JoinResult.Closed:
                    return MarisaPluginTaskState.NoResponse;
            }

            return MarisaPluginTaskState.ToBeContinued;
        }

        MarisaPluginTaskState LeaveRoom(Message next)
        {
            switch (room.Leave(next.Sender.Id))
            {
                case MaiVersusRoom.LeaveResult.NotIn:
                    return MarisaPluginTaskState.NoResponse;
                case MaiVersusRoom.LeaveResult.Dissolved:
                    next.Reply("房间已解散");
                    return MarisaPluginTaskState.CompletedTask;
                case MaiVersusRoom.LeaveResult.HostChanged:
                    new MessageBuilder(next).Text("已退出，房主转给 ").At(room.Host).Reply();
                    return MarisaPluginTaskState.ToBeContinued;
                default:
                    next.Reply("已退出");
                    return MarisaPluginTaskState.ToBeContinued;
            }
        }

        MarisaPluginTaskState CancelRoom(Message next)
        {
            room.Close();
            next.Reply("房间已解散");
            return MarisaPluginTaskState.CompletedTask;
        }

        async Task<MarisaPluginTaskState> StartRound(Message next)
        {
            switch (room.TryStart(next.Sender.Id, out var roster))
            {
                case MaiVersusRoom.StartResult.NotMember:
                    return MarisaPluginTaskState.NoResponse;
                case MaiVersusRoom.StartResult.TooFew:
                    next.Reply("至少需要 2 人才能开始");
                    return MarisaPluginTaskState.ToBeContinued;
                case not MaiVersusRoom.StartResult.Started:
                    return MarisaPluginTaskState.ToBeContinued;
            }

            if (!plan.Continuous) room.Close();

            MaiVersusMulti? result = null;
            try
            {
                result = await PlayRound(next, roster);
            }
            finally
            {
                room.EndRound(result);
            }

            if (result is not null) await ReplyRound(next, result);
            return plan.Continuous ? MarisaPluginTaskState.ToBeContinued : MarisaPluginTaskState.CompletedTask;
        }

        async Task<MaiVersusMulti?> PlayRound(Message next, IReadOnlyList<long> roster)
        {
            var fetched = await Task.WhenAll(roster.Select(qq => FetchRoomPlayer(next, qq)));
            var players = fetched.Where(x => x.Player is not null).Select(x => x.Player!).ToArray();
            var failed  = fetched.Where(x => x.Error is not null).ToArray();

            var notice = new MessageBuilder(next);
            for (var i = 0; i < failed.Length; i++)
            {
                notice.At(failed[i].Qq).Text($" 本轮跳过：{failed[i].Error}{(i + 1 < failed.Length ? "\n" : "")}");
            }

            if (players.Length < 2)
            {
                notice.Text(failed.Length > 0 ? "\n取到成绩的玩家不足 2 人" : "取到成绩的玩家不足 2 人").Reply();
                return null;
            }

            if (failed.Length > 0) notice.Reply();

            var charts = plan.PickCount is { } count
                ? room.PickCharts(plan.Charts, players, count, Random.Shared)
                : plan.Charts;
            if (charts.Count == 0)
            {
                next.Reply("没有找到至少两人玩过的谱面");
                return null;
            }

            return new MaiVersusMulti(plan.Title, plan.Version, charts, players);
        }

        async Task<(long Qq, MaiVersusMulti.Player? Player, string? Error)> FetchRoomPlayer(Message next, long qq)
        {
            if (room.TryGetScores(qq, out var cached)) return (qq, cached, null);

            var (resolved, error) = ResolveVersusPlayer(next, qq);
            if (resolved is null) return (qq, null, error);

            var (nickname, scores, fetchError) = await FetchBattleData(resolved);
            if (fetchError is not null) return (qq, null, fetchError);

            var player = new MaiVersusMulti.Player(qq, nickname ?? $"QQ {qq}", scores);
            room.CacheScores(player);
            return (qq, player, null);
        }

        async Task ReplyRound(Message next, MaiVersusMulti result)
        {
            var round     = plan.Continuous ? room.Round : 0;
            var standings = round >= 2 ? room.Standings() : [];
            if (plan.SingleSong)
            {
                next.Reply(MessageDataImage.FromBase64(await MaiMaiDraw.DrawVersusMulti(result, round, standings)));
                return;
            }

            await ReplyPages(next, result.PageCount, page => MaiMaiDraw.DrawVersusMultiBatch(result, page, round, standings));
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

    private static string? ResolveSummaryVersion(string input, IReadOnlyList<string> versions)
    {
        var key = input.Trim();
        var direct = versions.FirstOrDefault(v => v.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (direct != null) return direct;

        if (!PlateData.PlateVersionMap.TryGetValue(key, out var mapped) || mapped.Length != 1)
        {
            return null;
        }

        return versions.FirstOrDefault(v => v.Equals(mapped[0], StringComparison.OrdinalIgnoreCase));
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

    private static MarisaPluginTaskState ReplyOpponentNotBound(Message message, long opponentQq, string error)
    {
        new MessageBuilder(message).Text(error).At(opponentQq).Reply();

        return MarisaPluginTaskState.CompletedTask;
    }

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
