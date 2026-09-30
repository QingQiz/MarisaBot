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
    private static List<MaiMaiSong> SharedVersusSongs(
        IEnumerable<MaiMaiSong> songs, int level,
        IReadOnlyDictionary<(long Id, int LevelIdx), SongScore> left,
        IReadOnlyDictionary<(long Id, int LevelIdx), SongScore> right) =>
        songs.Where(song => song.Levels.Count > level && song.Constants.Count > level &&
                            song.Charts.Count > level && left.ContainsKey((song.Id, level)) &&
                            right.ContainsKey((song.Id, level))).ToList();

    private static (List<MaiMaiSong> Songs, int LevelIndex, bool Random, PlateData.Query? Scope)
        ResolveVersusQuery(Shared.Util.SongDb.SongDb<MaiMaiSong> songs, string input)
    {
        var query = input.Trim();
        if (query.Length == 0) return ([], 3, true, null);

        var exact = songs.SearchSongExact(query.AsMemory());
        if (exact.Count > 0) return (exact, 3, false, null);
        if (PlateData.DifficultyAliasMap.TryGetValue(query, out var difficulty))
            return ([], difficulty, true, null);

        var hasAffix = PlateData.TryStripDifficultyAffix(query.AsMemory(), out var level, out var rest);
        var explicitAffix = PlateData.DifficultyAliasMap.Keys.Any(token =>
            query.StartsWith(token, StringComparison.OrdinalIgnoreCase) ||
            query.EndsWith(token, StringComparison.OrdinalIgnoreCase));
        if (hasAffix && explicitAffix)
        {
            exact = songs.SearchSongExact(rest);
            if (exact.Count > 0) return (exact, level, false, null);
        }

        // 单字白/紫优先作为版本代字；白谱/紫谱可用于指定单曲难度。
        if (PlateData.TryParseScope(query, out var scope, out _)) return ([], 3, false, scope);
        if (hasAffix)
        {
            exact = songs.SearchSongExact(rest);
            if (exact.Count > 0) return (exact, level, false, null);
        }

        var fuzzy = songs.SearchSong(query.AsMemory());
        if (fuzzy.Count > 0) return (fuzzy, 3, false, null);
        return (hasAffix ? songs.SearchSong(rest) : [], hasAffix ? level : 3, false, null);
    }

    private async Task ReplyBatchVersus(
        Message message,
        MaiVersusBatch batch,
        Func<MaiVersusBatch, int, Task<string>>? render = null)
    {
        render ??= MaiMaiDraw.DrawVersusBatch;
        var firstPage = await render(batch, 1);
        message.Reply(MessageDataImage.FromBase64(firstPage));
        if (batch.PageCount == 1) return;

        var key = (message.GroupInfo?.Id, message.Sender.Id);
        if (!DialogManager.TryAddDialog(key, HandlePage, this)) return;

        async Task<MarisaPluginTaskState> HandlePage(Message next)
        {
            if (!next.IsPlainText()) return MarisaPluginTaskState.Canceled;

            var command = next.Command.Trim().ToString();
            if (command.Length < 2 || command[0] is not ('p' or 'P') ||
                !int.TryParse(command[1..], out var page) || page < 1 || page > batch.PageCount)
            {
                return MarisaPluginTaskState.Canceled;
            }

            var image = page == 1 ? firstPage : await render(batch, page);
            next.Reply(MessageDataImage.FromBase64(image));
            return MarisaPluginTaskState.ToBeContinued;
        }
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
