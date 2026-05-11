namespace Marisa.Plugin.Shared.MaiMaiDx;

/// <summary>
///     完成表（plate progress）所用的数据表 + 命令解析。
///     语义：游戏内的"真极/熊将/鏡神/..."等称号，按 BASIC ~ MASTER 四个难度的成绩判定。
///     不参与 Re:MASTER。
/// </summary>
public static class PlateData
{
    public const string CommandSuffix = "完成表";
    private const string OptionalDaiSuffix = "代";

    public enum Dimension { Achievement, Fc, Fs }

    /// <summary>
    ///     阈值定义。用 ordinal value 比较：score 的对应字段 ≥ Level 即视作达标。
    /// </summary>
    public sealed record Threshold(Dimension Dim, int Level, string DisplayName);

    /// <summary>
    ///     选择条件：版本（plate）/ 谱师 / 类别 三选一。
    /// </summary>
    public abstract record Selector(string Display)
    {
        /// <summary>版本。Versions 是 diving-fish basic_info.from 字段值集合。</summary>
        public sealed record Plate(string Kanji, string[] Versions) : Selector(Kanji);

        /// <summary>谱师。Name 与 song.Charters[i] 精确匹配。</summary>
        public sealed record Charter(string Name) : Selector(Name);

        /// <summary>类别。FullName 与 song.Info.Genre 精确匹配；Alias 是用户输入的别名（用于显示）。</summary>
        public sealed record Genre(string FullName, string Alias) : Selector(Alias);
    }

    public sealed record Query(Selector Selector, Threshold Threshold, int LevelIdx);

    /// <summary>默认难度（MASTER）。完成表语义里 BASIC/ADV/EXPERT 也允许显式指定，Re:MASTER 永不参与。</summary>
    public const int DefaultLevelIdx = 3;

    public enum ErrorKind { NotPlateCommand, EmptyQuery, UnknownThreshold, UnsupportedPlate, UnknownSelector }

    public sealed record ParseError(ErrorKind Kind, string? Detail = null);

    /// <summary>Achievement 段位顺序值。0=D，13=SSS+。</summary>
    public static int AchievementLevel(double achievement) => achievement switch
    {
        >= 100.5 => 13, // SSS+
        >= 100   => 12, // SSS
        >= 99.5  => 11, // SS+
        >= 99    => 10, // SS
        >= 98    => 9,  // S+
        >= 97    => 8,  // S
        >= 94    => 7,  // AAA
        >= 90    => 6,  // AA
        >= 80    => 5,  // A
        >= 75    => 4,  // BBB
        >= 70    => 3,  // BB
        >= 60    => 2,  // B
        >= 50    => 1,  // C
        _        => 0,  // D
    };

    /// <summary>Fc 字段顺序值。0=空，4=app(AP+)。</summary>
    public static int FcLevel(string? fc) => fc switch
    {
        "fc"  => 1,
        "fcp" => 2,
        "ap"  => 3,
        "app" => 4,
        _     => 0,
    };

    /// <summary>Fs 字段顺序值。0=空，5=fdxp(FDX+)。sync 是 maimai でらっくす 早期成就，比 fs 还低。</summary>
    public static int FsLevel(string? fs) => fs switch
    {
        "sync" => 1,
        "fs"   => 2,
        "fsp"  => 3,
        "fdx"  => 4,
        "fdxp" => 5,
        _      => 0,
    };

    /// <summary>给定 score 字段值，返回该 score 在指定维度上的 ordinal。</summary>
    public static int LevelOf(Dimension dim, SongScore score) => dim switch
    {
        Dimension.Achievement => AchievementLevel(score.Achievement),
        Dimension.Fc          => FcLevel(score.Fc),
        Dimension.Fs          => FsLevel(score.Fs),
        _                     => 0,
    };

    /// <summary>
    ///     代字 → diving-fish 版本字符串集合。
    ///     繁简体差异（暁/晓 櫻/樱 菫/堇 輝/辉 華/华 鏡/镜）双向都收录指向同一个版本集合。
    ///     真：两个 from 字段都查（旧版 PLUS 在 diving-fish 是独立条目）。
    ///     熊/華、爽/煌、宙/星、祭/祝、双/宴：DX 时代 PLUS 已被 diving-fish 合并到主版本下，
    ///     所以两个代字都映射到同一个 from 字符串。
    /// </summary>
    public static readonly Dictionary<string, string[]> PlateVersionMap = new()
    {
        ["真"] = ["maimai", "maimai PLUS"],
        ["超"] = ["maimai GreeN"],
        ["檄"] = ["maimai GreeN PLUS"],
        ["橙"] = ["maimai ORANGE"],
        ["暁"] = ["maimai ORANGE PLUS"],
        ["晓"] = ["maimai ORANGE PLUS"],
        ["桃"] = ["maimai PiNK"],
        ["櫻"] = ["maimai PiNK PLUS"],
        ["樱"] = ["maimai PiNK PLUS"],
        ["紫"] = ["maimai MURASAKi"],
        ["菫"] = ["maimai MURASAKi PLUS"],
        ["堇"] = ["maimai MURASAKi PLUS"],
        ["白"] = ["maimai MiLK"],
        ["雪"] = ["MiLK PLUS"],
        ["輝"] = ["maimai FiNALE"],
        ["辉"] = ["maimai FiNALE"],
        ["熊"] = ["maimai でらっくす"],
        ["華"] = ["maimai でらっくす"],
        ["华"] = ["maimai でらっくす"],
        ["爽"] = ["maimai でらっくす Splash"],
        ["煌"] = ["maimai でらっくす Splash"],
        ["宙"] = ["maimai でらっくす UNiVERSE"],
        ["星"] = ["maimai でらっくす UNiVERSE"],
        ["祭"] = ["maimai でらっくす FESTiVAL"],
        ["祝"] = ["maimai でらっくす FESTiVAL"],
        ["双"] = ["maimai でらっくす BUDDiES"],
        ["宴"] = ["maimai でらっくす BUDDiES"],
        ["鏡"] = ["maimai でらっくす PRiSM"],
        ["镜"] = ["maimai でらっくす PRiSM"],
    };

    /// <summary>diving-fish 暂未提供数据的代字（CiRCLE 的丸、PRiSM PLUS 的彩）。报错而非"未识别"。</summary>
    public static readonly HashSet<string> BlockedPlateKanji = ["丸", "彩"];

    /// <summary>类别别名 → diving-fish basic_info.genre 实际值。</summary>
    public static readonly Dictionary<string, string> GenreAliasMap = new()
    {
        ["术力口"]    = "niconico & VOCALOID",
        ["V家"]       = "niconico & VOCALOID",
        ["VOCALOID"]  = "niconico & VOCALOID",
        ["东方"]      = "东方Project",
        ["击中"]      = "音击&中二节奏",
        ["流行"]      = "流行&动漫",
        ["动漫"]      = "流行&动漫",
        ["流行动漫"]  = "流行&动漫",
        ["其他"]      = "其他游戏",
        ["宴会场"]    = "宴会場",
        ["宴谱"]      = "宴会場",
        ["舞萌"]      = "舞萌",
        ["原创"]      = "舞萌",
    };

    /// <summary>
    ///     阈值表。第一项是用户输入的字符串（中文别名 + ASCII rank/fc/fs 缩写），第二项是阈值定义。
    ///     字符串后缀匹配走 longest match，所以排序时 longer-first。
    /// </summary>
    private static readonly (string Token, Threshold Threshold)[] ThresholdEntries =
    [
        // 中文别名（用户最常用）
        ("大将",   new(Dimension.Achievement, 13, "SSS+")),
        ("将",     new(Dimension.Achievement, 12, "SSS")),
        ("理论值", new(Dimension.Fc,           4, "AP+")),
        ("舞舞",   new(Dimension.Fs,           4, "FDX")),
        ("极",     new(Dimension.Fc,           1, "FC")),
        ("神",     new(Dimension.Fc,           3, "AP")),

        // Achievement rank 全集
        ("SSS+", new(Dimension.Achievement, 13, "SSS+")),
        ("SSS",  new(Dimension.Achievement, 12, "SSS")),
        ("SS+",  new(Dimension.Achievement, 11, "SS+")),
        ("SS",   new(Dimension.Achievement, 10, "SS")),
        ("S+",   new(Dimension.Achievement,  9, "S+")),
        ("S",    new(Dimension.Achievement,  8, "S")),
        ("AAA",  new(Dimension.Achievement,  7, "AAA")),
        ("AA",   new(Dimension.Achievement,  6, "AA")),
        ("A",    new(Dimension.Achievement,  5, "A")),
        ("BBB",  new(Dimension.Achievement,  4, "BBB")),
        ("BB",   new(Dimension.Achievement,  3, "BB")),
        ("B",    new(Dimension.Achievement,  2, "B")),
        ("C",    new(Dimension.Achievement,  1, "C")),
        ("D",    new(Dimension.Achievement,  0, "D")),

        // Fc 段
        ("FC+", new(Dimension.Fc, 2, "FC+")),
        ("FC",  new(Dimension.Fc, 1, "FC")),
        ("AP+", new(Dimension.Fc, 4, "AP+")),
        ("AP",  new(Dimension.Fc, 3, "AP")),

        // Fs 段
        ("FDX+", new(Dimension.Fs, 5, "FDX+")),
        ("FDX",  new(Dimension.Fs, 4, "FDX")),
        ("FS+",  new(Dimension.Fs, 3, "FS+")),
        ("FS",   new(Dimension.Fs, 2, "FS")),
    ];

    /// <summary>按 token 长度倒序，保证 longest-suffix-match 正确（"大将" 优先于 "将"，"FDX+" 优先于 "FDX"）。</summary>
    private static readonly (string Token, Threshold Threshold)[] ThresholdEntriesLongestFirst =
        ThresholdEntries.OrderByDescending(t => t.Token.Length).ToArray();

    /// <summary>
    ///     难度别名表。**故意不收单字**（绿/黄/红/紫/白），因为这些字跟代字（紫=MURASAKi、白=MiLK）
    ///     或者用户预期的别名会冲突。要写中文必须双字"绿谱/黄谱/红谱/紫谱/白谱"。
    ///     Re:MASTER 是合法的 difficulty（用户显式指定时才走），但不是所有歌都有该难度。
    /// </summary>
    public static readonly Dictionary<string, int> DifficultyAliasMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BASIC"]     = 0, ["BSC"] = 0, ["绿谱"] = 0,
        ["ADVANCED"]  = 1, ["ADV"] = 1, ["黄谱"] = 1,
        ["EXPERT"]    = 2, ["EXP"] = 2, ["红谱"] = 2,
        ["MASTER"]    = 3, ["MST"] = 3, ["紫谱"] = 3,
        ["Re:MASTER"] = 4, ["白谱"] = 4,
    };

    private static readonly (string Token, int LevelIdx)[] DifficultyEntriesLongestFirst =
        DifficultyAliasMap.Select(kv => (kv.Key, kv.Value))
            .OrderByDescending(t => t.Key.Length).ToArray();

    /// <summary>
    ///     解析完整命令字符串（剥过 plugin 前缀后的部分）。
    ///     成功返回 (true, query, null)，失败返回 (false, null, error)。
    /// </summary>
    public static bool TryParse(
        string raw,
        IReadOnlyCollection<string> knownCharters,
        out Query? query,
        out ParseError? error)
    {
        query = null;
        error = null;

        var trimmed = raw.Trim();

        if (!trimmed.EndsWith(CommandSuffix, StringComparison.Ordinal))
        {
            error = new(ErrorKind.NotPlateCommand);
            return false;
        }

        var inner = trimmed[..^CommandSuffix.Length].Trim();

        if (inner.Length == 0)
        {
            error = new(ErrorKind.EmptyQuery);
            return false;
        }

        // 1. 阈值 token —— 字段位置可任意（"真代EXPERT将"=阈值在末尾、"将真EXPERT"=阈值在开头都接受）。
        //    longest-first 顺序遍历表，找最后一次出现位置（rightmost），strip 掉得到剩余字符串。
        Threshold? threshold = null;
        int thresholdAt = -1, thresholdLen = 0;
        foreach (var (token, t) in ThresholdEntriesLongestFirst)
        {
            var at = inner.LastIndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (at >= 0)
            {
                threshold    = t;
                thresholdAt  = at;
                thresholdLen = token.Length;
                break;
            }
        }

        if (threshold == null)
        {
            error = new(ErrorKind.UnknownThreshold, inner);
            return false;
        }

        var afterThreshold = (inner[..thresholdAt] + inner[(thresholdAt + thresholdLen)..]).Trim();

        // 2. 难度 token（可选，同样 anywhere + rightmost）。默认 MASTER。
        var levelIdx = DefaultLevelIdx;
        int diffAt = -1, diffLen = 0;
        foreach (var (token, idx) in DifficultyEntriesLongestFirst)
        {
            var at = afterThreshold.LastIndexOf(token, StringComparison.OrdinalIgnoreCase);
            if (at >= 0)
            {
                levelIdx = idx;
                diffAt   = at;
                diffLen  = token.Length;
                break;
            }
        }

        var selectorPart = diffAt >= 0
            ? (afterThreshold[..diffAt] + afterThreshold[(diffAt + diffLen)..]).Trim()
            : afterThreshold;

        if (selectorPart.Length == 0)
        {
            error = new(ErrorKind.UnknownSelector, inner);
            return false;
        }

        // 3. 解析 selector：先试代字（含可选末尾"代"），再 genre（固定 alias 表），
        //    最后 charter（substring catch-all，永远 succeed）。
        //    顺序关键：charter 阶段不再要求精确匹配，所以必须放最后，否则会拦截 genre。
        if (TryResolvePlate(selectorPart, out var plateSel, out var plateErr))
        {
            query = new Query(plateSel!, threshold, levelIdx);
            return true;
        }
        if (plateErr != null)
        {
            error = plateErr;  // 丸/彩 这种已知不支持，立刻返回
            return false;
        }

        if (TryResolveGenre(selectorPart, out var genreSel))
        {
            query = new Query(genreSel!, threshold, levelIdx);
            return true;
        }

        if (TryResolveCharter(selectorPart, knownCharters, out var charterSel))
        {
            query = new Query(charterSel!, threshold, levelIdx);
            return true;
        }

        error = new(ErrorKind.UnknownSelector, selectorPart);
        return false;
    }

    /// <summary>
    ///     尝试把 selectorPart 解析为代字。允许末尾可选"代"后缀（仅对版本代字生效）。
    ///     返回值：true = 命中代字；false + plateErr != null = 命中已知"不支持"代字；
    ///     false + plateErr == null = 不是代字，让下一阶段尝试。
    /// </summary>
    private static bool TryResolvePlate(string selectorPart, out Selector? selector, out ParseError? plateErr)
    {
        selector = null;
        plateErr = null;

        // 首先尝试不剥"代"
        var raw = selectorPart;
        if (PlateVersionMap.TryGetValue(raw, out var versions))
        {
            selector = new Selector.Plate(raw, versions);
            return true;
        }
        if (BlockedPlateKanji.Contains(raw))
        {
            plateErr = new(ErrorKind.UnsupportedPlate, raw);
            return false;
        }

        // 然后尝试剥末尾"代"
        if (raw.EndsWith(OptionalDaiSuffix, StringComparison.Ordinal) && raw.Length > OptionalDaiSuffix.Length)
        {
            var stripped = raw[..^OptionalDaiSuffix.Length];
            if (PlateVersionMap.TryGetValue(stripped, out var versions2))
            {
                selector = new Selector.Plate(stripped, versions2);
                return true;
            }
            if (BlockedPlateKanji.Contains(stripped))
            {
                plateErr = new(ErrorKind.UnsupportedPlate, stripped);
                return false;
            }
        }

        return false;
    }

    private static bool TryResolveCharter(
        string selectorPart, IReadOnlyCollection<string> knownCharters, out Selector? selector)
    {
        // 永远当作 charter 接受 — 实际筛选用 substring（Contains）匹配，目的是支持
        // "サファ太 vs 翠楼屋" 这类合作谱师名义（输 "翠楼屋" 也能命中）。
        // 真不存在的谱师名（用户 typo）就在 SelectChartsForQuery 那边匹到 0 首，
        // 由 handler 报 "没有找到 X 对应的歌曲"，而不是在这里报 UnknownSelector。
        // knownCharters 当前不参与判定，保留参数方便将来需要时（例如想做模糊提示）。
        _ = knownCharters;
        selector = new Selector.Charter(selectorPart);
        return true;
    }

    private static bool TryResolveGenre(string selectorPart, out Selector? selector)
    {
        selector = null;

        if (GenreAliasMap.TryGetValue(selectorPart, out var fullName))
        {
            selector = new Selector.Genre(fullName, selectorPart);
            return true;
        }

        // genre 全名直接接受
        if (GenreAliasMap.Values.Any(v => v.Equals(selectorPart, StringComparison.Ordinal)))
        {
            selector = new Selector.Genre(selectorPart, selectorPart);
            return true;
        }

        return false;
    }
}
