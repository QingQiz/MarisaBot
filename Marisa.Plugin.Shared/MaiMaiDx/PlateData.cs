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
    ///     选择条件：版本（plate）/ 谱师 / 类别 / 作曲家 四选一。
    /// </summary>
    public abstract record Selector(string Display)
    {
        /// <summary>版本。Versions 是 diving-fish basic_info.from 字段值集合。</summary>
        public sealed record Plate(string Kanji, string[] Versions) : Selector(Kanji);

        /// <summary>谱师。Name 与 song.Charters[i] substring 匹配。</summary>
        public sealed record Charter(string Name) : Selector(Name);

        /// <summary>类别。FullName 与 song.Info.Genre 精确匹配；Alias 是用户输入的别名（用于显示）。</summary>
        public sealed record Genre(string FullName, string Alias) : Selector(Alias);

        /// <summary>作曲家。Name 与 song.Info.Artist substring 匹配（song-level，非 chart-level）。</summary>
        public sealed record Artist(string Name) : Selector(Name);

        /// <summary>
        ///     谱师 ∪ 作曲家：当 Name 同时是某 charter 名 substring 又是某 artist 名 substring 时使用
        ///     （例如 "rintaro soma" 既写谱又作曲）。handler 按 OR 筛 — 谱师维度命中或作曲家维度命中均收录。
        /// </summary>
        public sealed record CharterOrArtist(string Name) : Selector(Name);

        /// <summary>难度 label：用户输 "13" / "13+" 这种游戏内显示难度，匹 song.Levels[i] 精确相等。</summary>
        public sealed record Level(string Label) : Selector(Label);

        /// <summary>定数：用户输 "13.5" / "14.7" 这种小数（必含小数点），匹 song.Constants[i]。</summary>
        public sealed record Constant(double Value) : Selector(Value.ToString("F1"));
    }

    /// <summary>
    ///     完整查询：一组 Selectors（AND 合取——所有 selector 都命中的 chart 才入选）+ 阈值 + 难度。
    ///     当前 TryParse 阶段返回 Selectors.Count == 1（与历史单 selector 行为等价）；
    ///     multi-selector 解析在后续 commit 启用。
    /// </summary>
    public sealed record Query(IReadOnlyList<Selector> Selectors, Threshold Threshold, IReadOnlyList<int> LevelIdxes);

    /// <summary>默认难度：未指定时同时查 MASTER + Re:MASTER。</summary>
    public static readonly IReadOnlyList<int> DefaultLevelIdxes = [3, 4];

    /// <summary>默认阈值（"将"=SSS）。用户未指定阈值时自动应用。</summary>
    public static readonly Threshold DefaultThreshold = new(Dimension.Achievement, 12, "SSS");

    public enum ErrorKind { NotPlateCommand, EmptyQuery, UnsupportedPlate, UnknownSelector, ConflictingSelector }

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

    /// <summary>
    ///     Fs 字段顺序值。0=空，5=FDX+。sync 是 maimai でらっくす 早期成就，比 fs 还低。
    ///     diving-fish API 用 "fsd"/"fsdp" (FDX/FDX+) 命名；AllNet 内部也是 fsd/fsdp；
    ///     旧别名 "fdx"/"fdxp" 仍接受以防 fixture / 第三方源用旧命名。
    /// </summary>
    public static int FsLevel(string? fs) => fs switch
    {
        "sync"          => 1,
        "fs"            => 2,
        "fsp"           => 3,
        "fsd" or "fdx"  => 4,
        "fsdp" or "fdxp" => 5,
        _               => 0,
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
        IReadOnlyCollection<string> knownArtists,
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
        //    longest-first 顺序遍历表，找 rightmost 满足"边界合法"的出现位置，strip 掉得到剩余字符串。
        //    边界规则：纯 ASCII 字母 token（如 "A"、"AAA"、"FC+"）必须 word boundary（前后不是 ASCII 字母），
        //    否则会把 "HIMEHINA" 里的 "A" 当 threshold；CJK token（将/神/...）不受边界限制。
        //    缺省时回落到 DefaultThreshold（"将"=SSS）。
        Threshold? threshold = null;
        int thresholdAt = -1, thresholdLen = 0;
        foreach (var (token, t) in ThresholdEntriesLongestFirst)
        {
            var pos = FindBoundedRightmost(inner, token);
            if (pos >= 0)
            {
                threshold    = t;
                thresholdAt  = pos;
                thresholdLen = token.Length;
                break;
            }
        }

        threshold ??= DefaultThreshold;

        var afterThreshold = thresholdAt >= 0
            ? (inner[..thresholdAt] + inner[(thresholdAt + thresholdLen)..]).Trim()
            : inner;

        // 2. 难度 token（可选，同样 anywhere + rightmost + 同样的 ASCII letter 边界规则）。
        //    缺省时回落到 DefaultLevelIdxes ([3, 4] = MASTER + Re:MASTER)；显式给一个就是单元素。
        IReadOnlyList<int> levelIdxes = DefaultLevelIdxes;
        int diffAt = -1, diffLen = 0;
        foreach (var (token, idx) in DifficultyEntriesLongestFirst)
        {
            var pos = FindBoundedRightmost(afterThreshold, token);
            if (pos >= 0)
            {
                levelIdxes = [idx];
                diffAt     = pos;
                diffLen    = token.Length;
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

        // 3. 多 selector 解析：每轮在 workingPart 内找一个"硬" token（Plate / Genre / Constant / Level）
        //    的 rightmost-longest match，剥掉，加入 selectors 列表。重复直到无新匹配。
        //    剩下的连续片段当作 Charter / Artist（substring precise → CharterOrArtist union → catch-all）。
        //    AND 语义：handler 端对每首 chart 用 selectors.All(MatchSelector) 求交集。
        //
        //    冲突规则：同类 selector 出现两次（含 Level + Constant 互斥，因为 Constant 隐含确定 Level）
        //    → ConflictingSelector 错误。
        var selectors = new List<Selector>();
        var workingPart = selectorPart;

        while (true)
        {
            // 同一轮内收集 4 种 token 候选，按 (length DESC, start DESC) 取最优。
            // longest-first 是为了处理像 "宴会场" 的歧义 — "宴" 是 Plate kanji（BUDDiES）
            // 但 "宴会场" 是 Genre 别名；明确选最长（Genre）。
            int matchStart = -1, matchLen = 0;
            Selector? matched = null;

            if (TryFindRightmostPlateInString(workingPart, out var pStart, out var pLen, out var pSel, out var pErr))
            {
                if (pLen > matchLen || (pLen == matchLen && pStart > matchStart))
                { matchStart = pStart; matchLen = pLen; matched = pSel; }
            }
            else if (pErr != null)
            {
                error = pErr;       // 丸/彩 这种已知不支持，立刻返回
                return false;
            }

            if (TryFindRightmostGenreInString(workingPart, out var gStart, out var gLen, out var gSel))
            {
                if (gLen > matchLen || (gLen == matchLen && gStart > matchStart))
                { matchStart = gStart; matchLen = gLen; matched = gSel; }
            }

            if (TryFindRightmostConstantInString(workingPart, out var cStart, out var cLen, out var cSel))
            {
                if (cLen > matchLen || (cLen == matchLen && cStart > matchStart))
                { matchStart = cStart; matchLen = cLen; matched = cSel; }
            }

            if (TryFindRightmostLevelInString(workingPart, out var lStart, out var lLen, out var lSel))
            {
                if (lLen > matchLen || (lLen == matchLen && lStart > matchStart))
                { matchStart = lStart; matchLen = lLen; matched = lSel; }
            }

            if (matchStart < 0) break;

            // 同类冲突检查（Level + Constant 视为同类——Constant 隐含 Level，二者只能给一个）
            if (matched is Selector.Plate && selectors.OfType<Selector.Plate>().Any())
            {
                error = new(ErrorKind.ConflictingSelector, "版本");
                return false;
            }
            if (matched is Selector.Genre && selectors.OfType<Selector.Genre>().Any())
            {
                error = new(ErrorKind.ConflictingSelector, "类别");
                return false;
            }
            if (matched is Selector.Level or Selector.Constant
                && selectors.Any(s => s is Selector.Level or Selector.Constant))
            {
                error = new(ErrorKind.ConflictingSelector, "难度或定数");
                return false;
            }

            selectors.Add(matched!);
            workingPart = (workingPart[..matchStart] + workingPart[(matchStart + matchLen)..]).Trim();
        }

        // 剩余连续片段当 Charter / Artist
        if (workingPart.Length > 0)
        {
            var charterHit = TryResolveCharterPrecise(workingPart, knownCharters, out var charterPreciseSel);
            var artistHit  = TryResolveArtist(workingPart, knownArtists, out var artistSel);

            if (charterHit && artistHit)
            {
                selectors.Add(new Selector.CharterOrArtist(workingPart));
            }
            else if (charterHit)
            {
                selectors.Add(charterPreciseSel!);
            }
            else if (artistHit)
            {
                selectors.Add(artistSel!);
            }
            else
            {
                // catch-all：把剩余部分当 charter 接收，由 handler 在筛选时报 0 首。
                selectors.Add(new Selector.Charter(workingPart));
            }
        }

        if (selectors.Count == 0)
        {
            // 理论上不会走到（EmptyQuery 在更早被挡），保险兜底
            error = new(ErrorKind.UnknownSelector, inner);
            return false;
        }

        query = new Query(selectors, threshold, levelIdxes);
        return true;
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

    /// <summary>
    ///     难度 label：纯整数（1-15）+ 可选 "+" 后缀，匹 song.Levels[i] 精确相等。
    ///     例 "13" / "13+" / "14" — 跨所有难度找出所有该 label 的 chart。
    ///     注意：必须无小数点，否则走 TryResolveConstant；"13.0" 也走 Constant，不当 label。
    /// </summary>
    private static bool TryResolveLevel(string selectorPart, out Selector? selector)
    {
        selector = null;
        if (System.Text.RegularExpressions.Regex.IsMatch(selectorPart, @"^([1-9]|1[0-5])\+?$"))
        {
            selector = new Selector.Level(selectorPart);
            return true;
        }
        return false;
    }

    /// <summary>
    ///     定数：必含小数点，1-15.x 范围，1 位小数（如 "13.5" / "14.7"）。匹 song.Constants[i]。
    /// </summary>
    private static bool TryResolveConstant(string selectorPart, out Selector? selector)
    {
        selector = null;
        if (System.Text.RegularExpressions.Regex.IsMatch(selectorPart, @"^([1-9]|1[0-5])\.\d$")
            && double.TryParse(selectorPart, System.Globalization.CultureInfo.InvariantCulture, out var c))
        {
            selector = new Selector.Constant(c);
            return true;
        }
        return false;
    }

    /// <summary>
    ///     在 haystack 中找 token 的 rightmost 出现位置，要求 word boundary（仅当 token 含 ASCII 字母时）。
    ///     避免 "HIMEHINA" 里的单字母 "A" 被识别为 Achievement A 阈值。
    /// </summary>
    private static int FindBoundedRightmost(string haystack, string token)
    {
        var requireBoundary = token.Any(IsAsciiLetter);
        var at = haystack.LastIndexOf(token, StringComparison.OrdinalIgnoreCase);
        while (at >= 0)
        {
            if (!requireBoundary) return at;

            var before = at > 0 ? haystack[at - 1] : '\0';
            var after  = at + token.Length < haystack.Length ? haystack[at + token.Length] : '\0';
            if (!IsAsciiLetter(before) && !IsAsciiLetter(after)) return at;

            // 该位置边界不合法，往左继续找下一个 occurrence
            if (at == 0) return -1;
            at = haystack.LastIndexOf(token, at - 1, StringComparison.OrdinalIgnoreCase);
        }
        return -1;
    }

    private static bool IsAsciiLetter(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z';

    /// <summary>
    ///     精确 charter 匹配：当且仅当 selectorPart 是某个已知 charter 名的 substring 时命中。
    ///     未命中时返回 false，由后续 Artist precise / Charter catch-all 接力。
    ///     目的：在引入 Artist 后避免 charter catch-all 抢走本应识别为 artist 的输入。
    /// </summary>
    private static bool TryResolveCharterPrecise(
        string selectorPart, IReadOnlyCollection<string> knownCharters, out Selector? selector)
    {
        if (knownCharters.Any(c => c.Contains(selectorPart, StringComparison.OrdinalIgnoreCase)))
        {
            selector = new Selector.Charter(selectorPart);
            return true;
        }
        selector = null;
        return false;
    }

    /// <summary>
    ///     精确 artist 匹配：当且仅当 selectorPart 是某个已知 artist 名的 substring 时命中。
    ///     必须 precise（不能 catch-all），否则会跟 Charter catch-all 冲突。
    /// </summary>
    private static bool TryResolveArtist(
        string selectorPart, IReadOnlyCollection<string> knownArtists, out Selector? selector)
    {
        if (knownArtists.Any(a => a.Contains(selectorPart, StringComparison.OrdinalIgnoreCase)))
        {
            selector = new Selector.Artist(selectorPart);
            return true;
        }
        selector = null;
        return false;
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

    // ──────────────────────────────────────────────────────────────────────
    // multi-selector: 在 workingPart 内"任意位置"找一个 token 的 rightmost-longest match。
    // Plate / Genre 直接走表；Level / Constant 走 regex，并要求两侧不是 ASCII 字母
    // （避免吃到 charter / artist 名里的偶然数字，如假想 charter "k14"）。
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     找 workingPart 内"最佳"Plate kanji 匹配。优先级 (length DESC, rightmost) —
    ///     即更长的匹配（带"代"后缀的双字版本）优先；同长度时取靠右的位置。
    ///     BlockedPlateKanji 命中且比有效 kanji 更靠右时报错。
    /// </summary>
    private static bool TryFindRightmostPlateInString(
        string s, out int start, out int length, out Selector? selector, out ParseError? plateErr)
    {
        start = -1; length = 0; selector = null; plateErr = null;

        int bestStart = -1, bestLen = 0;
        string? bestKanji = null;
        string[]? bestVersions = null;

        foreach (var (kanji, versions) in PlateVersionMap)
        {
            var idx = s.LastIndexOf(kanji, StringComparison.Ordinal);
            if (idx < 0) continue;
            // 同位置时优先 "kanji+代"（更长）
            var withDai = idx + kanji.Length < s.Length && s[idx + kanji.Length] == '代';
            var len = withDai ? kanji.Length + 1 : kanji.Length;
            if (len > bestLen || (len == bestLen && idx > bestStart))
            {
                bestStart = idx; bestLen = len; bestKanji = kanji; bestVersions = versions;
            }
        }

        int blockedStart = -1;
        string? blockedKanji = null;
        foreach (var bk in BlockedPlateKanji)
        {
            var idx = s.LastIndexOf(bk, StringComparison.Ordinal);
            if (idx > blockedStart) { blockedStart = idx; blockedKanji = bk; }
        }

        // 若被屏蔽 kanji 出现得比任何有效 kanji 更右 → 立即报错（避免悄悄走 catch-all）
        if (blockedStart > bestStart)
        {
            plateErr = new(ErrorKind.UnsupportedPlate, blockedKanji!);
            return false;
        }

        if (bestStart < 0) return false;
        start = bestStart; length = bestLen;
        selector = new Selector.Plate(bestKanji!, bestVersions!);
        return true;
    }

    /// <summary>
    ///     找 workingPart 内"最佳"Genre alias 或全名匹配。优先级 (length DESC, rightmost) —
    ///     避免 "流行动漫" 被更短的 "动漫" 抢掉位置。
    /// </summary>
    private static bool TryFindRightmostGenreInString(
        string s, out int start, out int length, out Selector? selector)
    {
        start = -1; length = 0; selector = null;

        int bestStart = -1, bestLen = 0;
        Selector? bestSel = null;

        foreach (var (alias, fullName) in GenreAliasMap)
        {
            var idx = s.LastIndexOf(alias, StringComparison.Ordinal);
            if (idx < 0) continue;
            if (alias.Length > bestLen || (alias.Length == bestLen && idx > bestStart))
            {
                bestStart = idx; bestLen = alias.Length;
                bestSel = new Selector.Genre(fullName, alias);
            }
        }
        foreach (var fullName in GenreAliasMap.Values.Distinct(StringComparer.Ordinal))
        {
            var idx = s.LastIndexOf(fullName, StringComparison.Ordinal);
            if (idx < 0) continue;
            if (fullName.Length > bestLen || (fullName.Length == bestLen && idx > bestStart))
            {
                bestStart = idx; bestLen = fullName.Length;
                bestSel = new Selector.Genre(fullName, fullName);
            }
        }

        if (bestStart < 0) return false;
        start = bestStart; length = bestLen; selector = bestSel;
        return true;
    }

    /// <summary>
    ///     找 workingPart 内 rightmost Constant（X.Y 格式，1-15.x 范围）。
    ///     两侧不能是 ASCII 字母 / 数字 / 点号，避免吃到 charter/artist 名里的偶然数字（如 "DECO*27"）。
    ///     正则 alternation 顺序 (1[0-5]|[1-9]) 优先匹更长以保证 "14" 不被 "1" 吃。
    /// </summary>
    private static bool TryFindRightmostConstantInString(
        string s, out int start, out int length, out Selector? selector)
    {
        start = -1; length = 0; selector = null;
        var matches = System.Text.RegularExpressions.Regex.Matches(s, @"(1[0-5]|[1-9])\.\d");
        for (var i = matches.Count - 1; i >= 0; i--)
        {
            var m = matches[i];
            if (!IsCompleteNumberToken(s, m.Index, m.Length)) continue;
            if (!double.TryParse(m.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v)) continue;
            start = m.Index; length = m.Length;
            selector = new Selector.Constant(v);
            return true;
        }
        return false;
    }

    /// <summary>
    ///     找 workingPart 内 rightmost Level（X 或 X+ 格式，1-15）。
    ///     1）两侧不能是 ASCII 字母 / 数字 / 点号；2）右边不能是 ".X"（避免吃 Constant 前缀）。
    /// </summary>
    private static bool TryFindRightmostLevelInString(
        string s, out int start, out int length, out Selector? selector)
    {
        start = -1; length = 0; selector = null;
        var matches = System.Text.RegularExpressions.Regex.Matches(s, @"(1[0-5]|[1-9])\+?");
        for (var i = matches.Count - 1; i >= 0; i--)
        {
            var m = matches[i];
            if (m.Length == 0) continue;
            // 不能是 Constant 前缀（紧跟 ".d"），由 IsCompleteNumberToken 的 dot 检查覆盖。
            if (!IsCompleteNumberToken(s, m.Index, m.Length)) continue;
            start = m.Index; length = m.Length;
            selector = new Selector.Level(m.Value);
            return true;
        }
        return false;
    }

    /// <summary>
    ///     match 两侧（前一个 char、后一个 char）都不是 ASCII 字母、数字或点号 —
    ///     即整个数字 token 完整呈现于该匹配位置，没被更长的数字串截断。
    /// </summary>
    private static bool IsCompleteNumberToken(string s, int idx, int len)
    {
        var before = idx > 0 ? s[idx - 1] : '\0';
        var after  = idx + len < s.Length ? s[idx + len] : '\0';
        return !IsAsciiLetterOrDigitOrDot(before) && !IsAsciiLetterOrDigitOrDot(after);
    }

    private static bool IsAsciiLetterOrDigitOrDot(char c)
        => IsAsciiLetter(c) || (c >= '0' && c <= '9') || c == '.';
}
