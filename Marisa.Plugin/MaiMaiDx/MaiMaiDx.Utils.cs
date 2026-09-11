using System.Security.Cryptography;
using Marisa.Configuration;
using Marisa.Database;
using Marisa.Plugin.Shared.MaiMaiDx;
using Marisa.Plugin.Shared.MaiMaiDx.DataFetcher;
using Marisa.Plugin.Shared.Util;

namespace Marisa.Plugin.MaiMaiDx;

public partial class MaiMaiDx
{
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
        var fetcher = GetDataFetcher(message);
        var rating  = await fetcher.GetRating(message);
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
            scores = engine.FilterScores((await fetcher.GetScores(message)).Values, filter);
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

    private DataFetcher GetDataFetcher(Message message, bool allowUsername = false)
    {
        // Command不为空的话，就是用用户名查。只有DivingFish能使用用户名查
        if (allowUsername && !message.Command.IsWhiteSpace())
        {
            return GetDataFetcher(DataFetcherType.DivingFish);
        }

        var qq = message.Sender.Id;

        var at = message.MessageChain!.Messages.FirstOrDefault(m => m.Type == MessageDataType.At);
        if (at != null)
        {
            qq = (at as MessageDataAt)?.Target ?? qq;
        }

        using var realm = BotDbContext.OpenRealm();

        var bind = realm.All<Marisa.Database.Entity.Plugin.MaiMaiDx.MaiMaiDxBind>().FirstOrDefault(x => x.UId == qq);

        if (bind == null)
        {
            return GetDataFetcher(DataFetcherType.DivingFish);
        }

        return bind.ServerName switch
        {
            "lxns" => GetDataFetcher(DataFetcherType.Lxns),
            "DivingFish" => GetDataFetcher(DataFetcherType.DivingFish),
            "Wahlap" => GetDataFetcher(DataFetcherType.Wahlap),
            _ => GetDataFetcher(DataFetcherType.Wahlap)
        };
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
