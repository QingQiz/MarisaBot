namespace Marisa.Plugin.Shared.MaiMaiDx;

/// <summary>
///     完成表（plate progress）所用的数据表 + 命令解析。
///     支持版本代字、谱师、类别、作曲家、难度 label、定数等选择条件。
/// </summary>
public static class PlateData
{
    public const string CommandSuffix = "完成表";
    private const string OptionalDaiSuffix = "代";
    private const string RevivalGenreToken = "复活曲";

    /// <summary>
    ///     歌曲范围为「旧框（FiNALE 及之前）」的牌子：舞将/舞极/舞神/舞舞舞 与 霸者。
    ///     都覆盖同一批旧版本歌曲，均需打 Re:MASTER（走 <see cref="FinaleAndEarlierRemasterEntries"/> 白名单）。
    ///     舞系的成绩要求由命令里写的档位决定；霸者是特例，固定要求全曲达成率 ≥ 80%（A）。
    /// </summary>
    private static readonly HashSet<string> FinaleAndEarlierPlateKanji = ["舞", "霸者"];

    public enum Dimension { Achievement, Fc, Fs, DxScore }

    public enum PlateScope { VersionGroup, FinaleAndEarlier }

    /// <summary>
    ///     阈值定义。用 ordinal value 比较：score 的对应字段 ≥ Level 即视作达标。
    /// </summary>
    public sealed record Threshold(Dimension Dim, int Level, string DisplayName);

    /// <summary>
    ///     选择条件：版本（plate）/ 谱师 / 类别 / 作曲家 四选一。
    /// </summary>
    public abstract record Selector(string Display)
    {
        /// <summary>
        ///     版本。Versions 是 diving-fish basic_info.from 字段值集合。
        ///     ImpliedThreshold：特例牌子（如霸者）固定自带的成绩要求；用户未显式给阈值时启用。
        /// </summary>
        public sealed record Plate(string Kanji, string[] Versions, PlateScope Scope = PlateScope.VersionGroup, Threshold? ImpliedThreshold = null) : Selector(Kanji);

        /// <summary>谱师。Name 与 song.Charters[i] substring 匹配。</summary>
        public sealed record Charter(string Name) : Selector(Name);

        /// <summary>
        ///     谱师别名：用户输中文 / 罗马音别名 → 一组 canonical 谱师 substring，与 song.Charters[i] 做 OR substring 匹配。
        ///     一个别名映射多个 substring，用于合并同一谱师的本名、高难马甲与合作名义
        ///     （如「沙发太」同时覆盖 サファ太 与马甲 -ZONE- SaFaRi）。Input 是用户输入的别名（用于显示）。
        ///     Exclude：尽管含某 Name substring、但不属于本人的署名（如「サファ太 respects for 小鳥遊さん」
        ///     含「小鳥遊」却是 サファ太 的致敬独谱），命中 Exclude 的 chart 不计入。
        /// </summary>
        public sealed record CharterAlias(IReadOnlyList<string> Names, IReadOnlyList<string> Exclude, string Input) : Selector(Input);

        /// <summary>类别。FullName 与 song.Info.Genre 精确匹配；Alias 是用户输入的别名（用于显示）。</summary>
        public sealed record Genre(string FullName, string Alias) : Selector(Alias);

        /// <summary>
        ///     复活曲集合（虚拟类别，匹配 <see cref="RevivalSongIds"/> 里的歌）。
        ///     与版本代字组合时表示「首发自该版本的复活曲」——此时版本牌的复活曲排除被绕过。
        /// </summary>
        public sealed record Revival() : Selector(RevivalGenreToken);

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
    /// </summary>
    public sealed record Query(IReadOnlyList<Selector> Selectors, Threshold Threshold, IReadOnlyList<int> LevelIdxes);

    /// <summary>非版本查询的默认难度：MASTER + Re:MASTER。</summary>
    public static readonly IReadOnlyList<int> DefaultLevelIdxes = [3, 4];

    private static readonly IReadOnlyList<int> DefaultPlateLevelIdxes = [3];

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

    /// <summary>
    ///     DX Score 星档（官方 1★-5★，对应 max DX 的 85/90/93/95/97%）。
    ///     用整数运算避免浮点误差：dxScore * 100 ≥ maxDx * pct 等价于 dxScore / maxDx ≥ pct/100。
    ///     注意：maimai でらっくす BUDDiES 官方 plate marker 也只到 5★，icon asset 只有这 5 档。
    ///     maxDx 接 long 是因为生产代码用 song.Charts[i].Notes.Sum() * 3 自然推 long
    ///     （Notes 是 List long）；call site 不用手动 cast。
    /// </summary>
    public static int DxScoreStar(int dxScore, long maxDx)
    {
        if (maxDx <= 0) return 0;
        var hundred = dxScore * 100L;
        if (hundred >= maxDx * 97L) return 5;
        if (hundred >= maxDx * 95L) return 4;
        if (hundred >= maxDx * 93L) return 3;
        if (hundred >= maxDx * 90L) return 2;
        if (hundred >= maxDx * 85L) return 1;
        return 0;
    }

    /// <summary>
    ///     给定 score 字段值，返回该 score 在指定维度上的 ordinal。
    ///     DxScore 维度需要 chart maxDx 才能算（不只看 score 字段），所以这个 helper 不处理；
    ///     调用方用 <see cref="DxScoreStar"/> 单独算。
    /// </summary>
    public static int LevelOf(Dimension dim, SongScore score) => dim switch
    {
        Dimension.Achievement => AchievementLevel(score.Achievement),
        Dimension.Fc          => FcLevel(score.Fc),
        Dimension.Fs          => FsLevel(score.Fs),
        _                     => 0,
    };

    private static readonly string[] FinaleAndEarlierVersions =
    [
        "maimai",
        "maimai PLUS",
        "maimai GreeN",
        "maimai GreeN PLUS",
        "maimai ORANGE",
        "maimai ORANGE PLUS",
        "maimai PiNK",
        "maimai PiNK PLUS",
        "maimai MURASAKi",
        "maimai MURASAKi PLUS",
        "maimai MiLK",
        "MiLK PLUS",
        "maimai FiNALE",
    ];

    // diving-fish 只有歌曲级发布日期；「舞/霸者」的 Re:MASTER 采用白名单，避免后续 DX 时代追加旧曲白谱时自动误计入。
    // 注：原列入的 39(146)/妄想感傷代償連盟(731)/ヒバナ(792) 是复活曲，已移至 RevivalSongEntries 整曲排除。
    private static readonly (long Id, string Title)[] FinaleAndEarlierRemasterEntries =
    [
        (17,  "Future"),
        (22,  "In Chaos"),
        (23,  "Crush On You"),
        (24,  "Sun Dance"),
        (58,  "Endless World"),
        (61,  "Beat Of Mind"),
        (62,  "檄！帝国華撃団(改)"),
        (65,  "ZIGG-ZAGG"),
        (66,  "ワールズエンド・ダンスホール"),
        (70,  "ジングルベル"),
        (71,  "マトリョシカ"),
        (80,  "City Escape: Act1"),
        (81,  "Rooftop Run: Act1"),
        (100, "Tell Your World"),
        (107, "ロミオとシンデレラ"),
        (143, "Fragrance"),
        (145, "Starlight Disco"),
        (198, "カゲロウデイズ"),
        (200, "Bad Apple!! feat nomico"),
        (204, "ナイト・オブ・ナイツ"),
        (226, "Blew Moon"),
        (227, "Garakuta Doll Play"),
        (247, "Danza zandA"),
        (255, "Burning Hearts ～炎のANGEL～"),
        (256, "いーあるふぁんくらぶ"),
        (265, "Save This World νMIX"),
        (266, "Living Universe"),
        (282, "からくりピエロ"),
        (295, "緋色のDance"),
        (296, "明星ロケット"),
        (299, "ロストワンの号哭"),
        (301, "患部で止まってすぐ溶ける～狂気の優曇華院"),
        (310, "エピクロスの虹はもう見えない"),
        (312, "ってゐ！ ～えいえんてゐVer～"),
        (365, "ガラテアの螺旋"),
        (414, "若い力 -SEGA HARD GIRLS MIX-"),
        (496, "AMAZING MIGHTYYYY!!!!"),
        (513, "だんだん早くなる"),
        (532, "洗脳"),
        (589, "Panopticon"),
        (741, "インビジブル"),
        (756, "CYBER Sparks"),
        (759, "サドマミホリック"),
        (763, "はやくそれになりたい！"),
        (777, "花と、雪と、ドラムンベース。"),
        (793, "ロキ"),
        (799, "QZKago Requiem"),
        (803, "Schwarzschild"),
        (806, "ナイトメア☆パーティーナイト"),
        (809, "結ンデ開イテ羅刹ト骸"),
        (812, "Alea jacta est!"),
        (816, "クレイジークレイジーダンサーズ"),
        (818, "隠然"),
        (820, "FFT"),
        (825, "雷切-RAIKIRI-"),
        (830, "立ち入り禁止"),
        (833, "the EmpErroR"),
        (834, "PANDORA PARADOXXX"),
        (838, "最終鬼畜妹フランドール・S"),
    ];

    private static readonly HashSet<long> FinaleAndEarlierRemasterSongIds =
        FinaleAndEarlierRemasterEntries.Select(e => e.Id).ToHashSet();

    /// <summary>
    ///     复活曲：曾从国服删除、后又重新上线的歌。游戏不会把复活曲重新计入版本完成牌（姓名框），
    ///     完成表同样整曲排除（所有难度，含「舞」）。来源：RemyWiki 舞萌DX 各版本(China)「Revived Songs」段
    ///     ∩ diving-fish 现役曲库。仅收录在国服「删除→复活」过的；「首发缺席→后补」类（おこちゃま戦争 382 /
    ///     拝啓ドッペルゲンガー 711）按难度档表现不一致，暂不收录。
    ///     另：魔理沙は大変なものを盗んでいきました(201) 虽国服删过又复活，但属姓名框系统上线（2021）前因合规原因
    ///     的本地临时下架（非全系列删除、日亚服从未删、于姓名框上线当版复活），实测仍计入超牌，故不收录。
    /// </summary>
    private static readonly (long Id, string Title)[] RevivalSongEntries =
    [
        (44,    "ハッピーシンセサイザ"),
        (146,   "39"),
        (185,   "＊ハロー、プラネット。"),
        (189,   "弱虫モンブラン"),
        (281,   "ダブルラリアット"),
        (341,   "二息歩行"),
        (419,   "ストリーミングハート"),
        (451,   "頓珍漢の宴"),
        (455,   "Counselor"),
        (460,   "閃鋼のブリューナク"),
        (524,   "Hand in Hand"),
        (687,   "共感覚おばけ"),
        (688,   "麒麟"),
        (712,   "ミラクル・ショッピング"),
        (731,   "妄想感傷代償連盟"),
        (792,   "ヒバナ"),
        (853,   "前前前世"),
        (10146, "39"),
        (11213, "Arty Party"),
        (11253, "おジャ魔女カーニバル!!"),
        (11267, "ディカディズム"),
    ];

    private static readonly HashSet<long> RevivalSongIds =
        RevivalSongEntries.Select(e => e.Id).ToHashSet();

    public static bool IsRevivalSong(long songId) => RevivalSongIds.Contains(songId);

    /// <summary>
    ///     代字 → diving-fish 版本字符串集合。
    ///     繁简体差异（暁/晓 櫻/樱 菫/堇 輝/辉 華/华 鏡/镜）双向都收录指向同一个版本集合。
    ///     真：两个 from 字段都查（旧版 PLUS 在 diving-fish 是独立条目）。
    ///     熊/華、爽/煌、宙/星、祭/祝、双/宴：DX 时代 PLUS 已被 diving-fish 合并到主版本下，
    ///     所以两个代字都映射到同一个 from 字符串。
    ///     鏡(PRiSM)/彩(PRiSM PLUS)：国服分作两个年份(2025/2026)，是各自独立的 from 条目。
    /// </summary>
    public static readonly Dictionary<string, string[]> PlateVersionMap = new()
    {
        ["舞"]   = FinaleAndEarlierVersions,
        ["霸者"] = FinaleAndEarlierVersions, // 特例：舞系同款歌曲（旧框），固定要求全曲达成率 ≥ 80%（A）
        ["真"]   = ["maimai", "maimai PLUS"],
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
        ["彩"] = ["maimai でらっくす PRiSM PLUS"],
    };

    /// <summary>
    ///     牌名本身就固定了成绩要求的特例牌子（命令里不用、也不能再写档位）。
    ///     霸者：舞系同款歌曲，全曲达成率 ≥ 80%（A）。
    /// </summary>
    private static readonly Dictionary<string, Threshold> NamedPlateImpliedThreshold = new()
    {
        ["霸者"] = new(Dimension.Achievement, 5, "A"),
    };

    /// <summary>
    ///     游戏内成就姓名框贴图：版本代字 → 该版「極」牌贴图 id。極/将/神/舞舞 为连续 4 号。
    ///     真系历来无「将」且只占 6101-6103 三号，在 <see cref="NamePlateImage"/> 里单独处理。
    /// </summary>
    private static readonly Dictionary<string, int> NamePlateKiwamiId = new()
    {
        ["超"] = 6104, ["檄"] = 6108, ["橙"] = 6112, ["暁"] = 6116, ["晓"] = 6116,
        ["桃"] = 6120, ["櫻"] = 6124, ["樱"] = 6124, ["紫"] = 6128, ["菫"] = 6132, ["堇"] = 6132,
        ["白"] = 6136, ["雪"] = 6140, ["輝"] = 6144, ["辉"] = 6144, ["舞"] = 6149,
        ["熊"] = 55101, ["華"] = 109101, ["华"] = 109101, ["爽"] = 159101, ["煌"] = 209101,
        ["宙"] = 259101, ["星"] = 309101, ["祭"] = 359101, ["祝"] = 409101,
        ["双"] = 459101, ["宴"] = 509101, ["鏡"] = 559101, ["镜"] = 559101, ["彩"] = 609101,
    };

    /// <summary>
    ///     查询对应的游戏内姓名框贴图文件名；无对应姓名框时为 null。
    ///     仅当查询恰为「单个版本代字 + 極/将/神/舞舞 之一的阈值」时命中；霸者用其固有阈值（A）。
    /// </summary>
    public static string? NamePlateImage(Query query)
    {
        if (query.Selectors is not [Selector.Plate plate]) return null;

        var (dim, level) = (query.Threshold.Dim, query.Threshold.Level);

        if (plate.Kanji == "霸者")
        {
            return dim == Dimension.Achievement && level == 5 ? PlateImageName(6148) : null;
        }

        var offset = (dim, level) switch
        {
            (Dimension.Fc, 1)           => 0, // 極 = FC
            (Dimension.Achievement, 12) => 1, // 将 = SSS
            (Dimension.Fc, 3)           => 2, // 神 = AP
            (Dimension.Fs, 4)           => 3, // 舞舞 = FDX
            _                           => -1,
        };
        if (offset < 0) return null;

        if (plate.Kanji == "真")
        {
            return offset switch
            {
                0 => PlateImageName(6101),
                2 => PlateImageName(6102),
                3 => PlateImageName(6103),
                _ => null,
            };
        }

        return NamePlateKiwamiId.TryGetValue(plate.Kanji, out var kiwami) ? PlateImageName(kiwami + offset) : null;
    }

    private static string PlateImageName(int id) => $"UI_Plate_{id:D6}.png";

    public static bool MatchPlate(Selector.Plate plate, MaiMaiSong song, int levelIdx, bool includeRevival = false)
    {
        if (!plate.Versions.Any(v => string.Equals(v, song.Version, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        // 复活曲不计入任何版本完成牌（含「舞/霸者」），整曲排除。
        // 例外：查询里带「复活曲」selector 时（如「真复活曲」），意图是筛该版本首发的复活曲，故不排除。
        if (!includeRevival && RevivalSongIds.Contains(song.Id))
        {
            return false;
        }

        return plate.Scope != PlateScope.FinaleAndEarlier
               || IsFinaleAndEarlierChart(song.Id, levelIdx);
    }

    public static bool IsFinaleAndEarlierChart(long songId, int levelIdx) =>
        levelIdx != 4 || FinaleAndEarlierRemasterSongIds.Contains(songId);

    /// <summary>diving-fish 暂未提供数据的代字（CiRCLE 的丸）。报错而非"未识别"。</summary>
    public static readonly HashSet<string> BlockedPlateKanji = ["丸"];

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
    ///     谱师别名 → 一组 canonical 谱师 substring（与 song.Charters[i] 做 OR substring 匹配）。
    ///     纯假名 / 难打的日文谱师名，给中文意译、昵称、罗马音、繁简等别名。值是多个 substring：
    ///     合并同一谱师的本名、高难马甲、合作名义——substring 一侧用 OrdinalIgnoreCase，
    ///     故大小写不敏感，且合作名义（"X vs サファ太"）会一并命中。别名内容由群友提供。
    ///     注：「7.3」与定数 7.3 同形，裸「7.3」按谱师别名；定数 7.3 用「定数7.3」消歧。
    /// </summary>
    public static readonly Dictionary<string, string[]> CharterAliasMap = new()
    {
        ["哈皮"]       = ["はっぴー", "緑風 犬三郎", "原田ひろゆき", "シチミッピー", "鳩ホルぴー", "いぬっくまとボコっくま", "たかなっぴー", "Luxiいぬ", "“H”ack", "“H”ack underground", "PANDORA PARADOXXX", "Sukiyaki vs Happy", "舞舞10年ズ"],
        ["沙发太"]     = ["サファ太", "さふぁた", "Safari", "-ZONE- SaFaRi", "-ZONE-Phoenix", "Safata", "ボコ太", "鳩サファzhel", "サぴぴぴぴちネファ太太太太コ", "ﾚよ†ょ／∪ヽ”┠  (十,3､了ﾅﾆ", "PANDORA BOXXX", "Ruby", "project raputa", "Safazhel"],
        ["maistar"]    = ["mai-Star"],
        ["小鸟游"]     = ["小鳥遊", "Phoenix", "たかなっぴー", "Anomaly Labyrinth", "ネコトリサーカス団"],
        ["谱面-100号"] = ["譜面-100号"],
        ["谱面100号"]  = ["譜面-100号"],
        ["100号"]      = ["譜面-100号"],
        ["鸠"]         = ["鳩ホルダー", "The Dove", "鳩ホルぴー", "鳩サファzhel"],
        ["鸽子"]       = ["鳩ホルダー", "The Dove", "鳩ホルぴー", "鳩サファzhel"],
        ["企鹅"]       = ["ペンギン", "ものくロシェ"],
        ["泸溪河"]     = ["Luxizhel", "BELiZHEL", "Luxiいぬ", "鳩サファzhel", "LuxiHertz", "Safazhel"],
        ["二大爷"]     = ["ニャイン"],
        ["7.3"]        = ["シチミヘルツ", "7.3", "Safata.Hz", "Safata.GHz", "超七味星人", "七味星人", "小鳥遊チミ", "シチミッピー", "あまくちヘルツ", "しちみりこりす", "SHICHIMI☆CAT", "Hz-R.Arrow", "LuxiHertz"],
        ["隅田川"]     = ["隅田川星人", "The ALiEN", "超七味星人", "七味星人", "隅田川華火大会", "はっぴー星人"],
        ["川哥"]       = ["隅田川星人", "The ALiEN", "超七味星人", "七味星人", "隅田川華火大会", "はっぴー星人"],
        ["华火职人"]   = ["華火職人", "7.3連発華火", "ﾚよ†ょ／∪ヽ”┠  (十,3､了ﾅﾆ", "“Carpe diem” ＊ HAN∀BI", "隅田川華火大会"],
        ["桃子猫"]     = ["ぴちネコ", "ロシアンブラック", "BLaCK rOSE dIsEASe pATiENT", "サぴぴぴぴちネファ太太太太コ", "チェシャ猫とハートのジャック", "SHICHIMI☆CAT", "ネコトリサーカス団", "R-blacX of JacQ", "SAFARi☆CAT"],
        ["阿玛莉莉丝"] = ["アマリリス"],
        ["柠檬"]       = ["じゃこレモン", "僕の檸檬本当上手"],
        ["DP皆传"]     = ["チャン@DP皆伝", "Garakuta Scramble!", "舞舞10年ズ"],
        ["科技厨房"]   = ["Techno Kitchen"],
        ["Revo"]       = ["Revo@LC"],
        ["蟹棒君"]     = ["カマボコ", "ボコ太", "いぬっくまとボコっくま"],
        ["蟹棒男"]     = ["カマボコ", "ボコ太", "いぬっくまとボコっくま"],
        ["寿喜烧"]     = ["すきやき", "Sukiyaki vs Happy"],
        ["叠返"]       = ["畳返し"],
        ["红箭"]       = ["Redarrow", "red phoenix", "Hz-R.Arrow"],
        ["甜口姜"]     = ["あまくちジンジャー", "あまくちヘルツ", "EL DiABLO"],
        ["habakiri"]   = ["アミノハバキリ"],
        ["hbkr"]       = ["アミノハバキリ"],
        ["melonpop"]   = ["メロンポップ", "ずんだポップ"],
    };

    /// <summary>
    ///     谱师别名的反向排除：某署名虽含别名的 Name substring，却不属于该谱师，命中即排除。
    ///     如「サファ太 respects for 小鳥遊さん」含「小鳥遊」却是 サファ太 的致敬独谱，不计入「小鸟游」。
    /// </summary>
    public static readonly Dictionary<string, string[]> CharterAliasExclude = new()
    {
        ["小鸟游"] = ["サファ太 respects for 小鳥遊さん"],
    };

    /// <summary>
    ///     谱师身份名（本名/独立马甲）→ 该谱师全部名义 + 反向排除，仅匹配层使用（解析层不感知）。
    ///     合作名义（七味星人、鳩サファzhel 等）不收——精确打合作名义仍只查该署名。
    /// </summary>
    public static readonly Dictionary<string, (string[] Names, string[] Exclude)> CharterIdentityMap = BuildCharterIdentityMap();

    private static Dictionary<string, (string[] Names, string[] Exclude)> BuildCharterIdentityMap()
    {
        // (别名 key, 身份名列表)。无中文别名的谱师（翠楼屋），名义组即身份列表。
        (string AliasKey, string[] Identities)[] groups =
        [
            ("哈皮",     ["はっぴー", "緑風 犬三郎", "原田ひろゆき", "“H”ack", "“H”ack underground", "PANDORA PARADOXXX"]),
            ("沙发太",   ["サファ太", "さふぁた", "Safari", "-ZONE- SaFaRi", "Safata", "PANDORA BOXXX", "Ruby", "project raputa"]),
            ("小鸟游",   ["小鳥遊", "小鳥遊さん", "Phoenix", "Anomaly Labyrinth"]),
            ("鸠",       ["鳩ホルダー", "The Dove"]),
            ("企鹅",     ["ペンギン", "ロシェ@ペンギン", "ロシェ＠ペンギン"]),
            ("泸溪河",   ["Luxizhel", "BELiZHEL"]),
            ("7.3",      ["シチミヘルツ", "7.3Hz", "7.3GHz"]),
            ("隅田川",   ["隅田川星人", "The ALiEN"]),
            ("华火职人", ["華火職人", "“Carpe diem” ＊ HAN∀BI"]),
            ("桃子猫",   ["ぴちネコ", "ロシアンブラック", "BLaCK rOSE dIsEASe pATiENT"]),
            ("柠檬",     ["じゃこレモン", "僕の檸檬本当上手"]),
            ("DP皆传",   ["チャン@DP皆伝", "Garakuta Scramble!"]),
            ("蟹棒君",   ["カマボコ", "カマボコ君"]),
            ("寿喜烧",   ["すきやき", "すきやき奉行"]),
            ("红箭",     ["Redarrow"]),
            ("甜口姜",   ["あまくちジンジャー", "EL DiABLO"]),
            ("melonpop", ["メロンポップ", "ずんだポップ"]),
            ("翠楼屋",   ["翠楼屋", "翡翠マナ"]),
        ];

        var map = new Dictionary<string, (string[], string[])>(StringComparer.OrdinalIgnoreCase);
        foreach (var (aliasKey, identities) in groups)
        {
            var names   = CharterAliasMap.GetValueOrDefault(aliasKey) ?? identities;
            var exclude = CharterAliasExclude.GetValueOrDefault(aliasKey, []);
            foreach (var identity in identities) map.Add(identity, (names, exclude));
        }
        return map;
    }

    /// <summary>
    ///     精确 Charter 的匹配谓词：输入恰为某身份名（忽略大小写）时按该人全部名义并集（含反向排除）匹配，
    ///     使真名查询与别名查询结果一致；否则单一 substring（兼容 "サファ太 vs 翠楼屋" 这种合作署名）。
    /// </summary>
    public static bool MatchCharter(string signedCharter, string input) =>
        CharterIdentityMap.TryGetValue(input, out var person)
            ? MatchCharter(signedCharter, person.Names, person.Exclude)
            : signedCharter.Contains(input, StringComparison.OrdinalIgnoreCase);

    /// <summary>名义组 OR 命中且不落 Exclude。CharterAlias selector 与身份查询共用。</summary>
    public static bool MatchCharter(string signedCharter, IReadOnlyList<string> names, IReadOnlyList<string> exclude) =>
        names.Any(n => signedCharter.Contains(n, StringComparison.OrdinalIgnoreCase))
        && !exclude.Any(x => signedCharter.Contains(x, StringComparison.OrdinalIgnoreCase));

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

        // DxScore 星档 1-5★（85/90/93/95/97% × max DX）— 中文 + ASCII 数字两套写法
        ("一星", new(Dimension.DxScore, 1, "1★")),
        ("二星", new(Dimension.DxScore, 2, "2★")),
        ("三星", new(Dimension.DxScore, 3, "3★")),
        ("四星", new(Dimension.DxScore, 4, "4★")),
        ("五星", new(Dimension.DxScore, 5, "5★")),
        ("1星",  new(Dimension.DxScore, 1, "1★")),
        ("2星",  new(Dimension.DxScore, 2, "2★")),
        ("3星",  new(Dimension.DxScore, 3, "3★")),
        ("4星",  new(Dimension.DxScore, 4, "4★")),
        ("5星",  new(Dimension.DxScore, 5, "5★")),
        ("1*",   new(Dimension.DxScore, 1, "1★")),
        ("2*",   new(Dimension.DxScore, 2, "2★")),
        ("3*",   new(Dimension.DxScore, 3, "3★")),
        ("4*",   new(Dimension.DxScore, 4, "4★")),
        ("5*",   new(Dimension.DxScore, 5, "5★")),
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

        // 默认阈值延后到 selector 解析后再定（完整牌名如「霸者」可能自带阈值档）。
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
                error = pErr;       // 丸 这种已知不支持，立刻返回
                return false;
            }

            if (TryFindRightmostGenreInString(workingPart, out var gStart, out var gLen, out var gSel))
            {
                if (gLen > matchLen || (gLen == matchLen && gStart > matchStart))
                { matchStart = gStart; matchLen = gLen; matched = gSel; }
            }

            // 谱师别名作为一等 token，且在 Constant 之前检查：多字别名「华火职人」靠长度压过单字代字「华」，
            // 同长度同位置时（「7.3」vs 定数 7.3）按先检查者胜出 → 别名优先。
            // 别名/定数 token 嵌在 workingPart 内出现的某个真谱师名/身份名里时（如「隅田川星人 13」
            // 的「隅田川」）不参与竞争：让等级等 token 先剥，真名整段落到 precise Charter。
            if (TryFindRightmostCharterAliasInString(workingPart, out var caStart, out var caLen, out var caSel)
                && !TokenEmbeddedInCharterName(workingPart, caStart, caLen, knownCharters))
            {
                if (caLen > matchLen || (caLen == matchLen && caStart > matchStart))
                { matchStart = caStart; matchLen = caLen; matched = caSel; }
            }

            if (TryFindRightmostConstantInString(workingPart, out var cStart, out var cLen, out var cSel)
                && !TokenEmbeddedInCharterName(workingPart, cStart, cLen, knownCharters))
            {
                if (cLen > matchLen || (cLen == matchLen && cStart > matchStart))
                { matchStart = cStart; matchLen = cLen; matched = cSel; }
            }

            if (TryFindRightmostLevelInString(workingPart, out var lStart, out var lLen, out var lSel))
            {
                if (lLen > matchLen || (lLen == matchLen && lStart > matchStart))
                { matchStart = lStart; matchLen = lLen; matched = lSel; }
            }

            if (TryFindRevivalInString(workingPart, out var rStart, out var rLen, out var rSel))
            {
                if (rLen > matchLen || (rLen == matchLen && rStart > matchStart))
                { matchStart = rStart; matchLen = rLen; matched = rSel; }
            }

            if (matchStart < 0) break;

            // 反查保护：版本代字/类别/谱师别名若只是某真谱师/曲师名的一部分（如"隅田川星人"的"星"、
            // "超七味"的"超"，或别名"Revo"嵌在真名"Revo@LC"里），它不是 selector，否决匹配让整段落到下面 Charter/Artist。
            if (matched is Selector.Plate or Selector.Genre or Selector.CharterAlias
                && workingPart.Length > matchLen
                && (TryResolveCharterPrecise(workingPart, knownCharters, out _)
                    || TryResolveArtist(workingPart, knownArtists, out _)))
            {
                break;
            }

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

        if (diffAt < 0)
        {
            if (selectors.Any(s => s is Selector.Level or Selector.Constant))
            {
                // 指定了等级/定数：该等级/定数的谱面可能分布在任意难度（如 6+ 只在 BASIC/ADVANCED，
                // 13+ 在 EXPERT/MASTER/Re:MASTER），全难度都查——此时版本代字的「仅 MASTER」默认不适用。
                levelIdxes = [0, 1, 2, 3, 4];
            }
            else if (selectors.Any(s => s is Selector.Revival))
            {
                // 复活曲按类别处理：默认 MASTER + Re:MASTER（即便同时带版本代字也不用版本牌的单 MASTER 默认）。
                levelIdxes = DefaultLevelIdxes;
            }
            else if (selectors.OfType<Selector.Plate>().FirstOrDefault() is { } plate)
            {
                levelIdxes = plate.Scope == PlateScope.FinaleAndEarlier
                    ? DefaultLevelIdxes
                    : DefaultPlateLevelIdxes;
            }
            else if (selectors.OfType<Selector.Genre>().Any(g => g.FullName == "宴会場"))
            {
                levelIdxes = [0, 1, 2, 3, 4];
            }
        }

        // 阈值缺省回落：特例牌子（霸者）固定自带的成绩要求优先，否则默认 SSS（将）。
        threshold ??= selectors.OfType<Selector.Plate>()
            .Select(p => p.ImpliedThreshold)
            .FirstOrDefault(t => t is not null) ?? DefaultThreshold;

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
            selector = CreatePlateSelector(raw, versions);
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
                selector = CreatePlateSelector(stripped, versions2);
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

    /// <summary>候选 token 区间被 workingPart 内出现的某个更长真谱师名/身份名覆盖时，它是名字的一部分而非 selector。</summary>
    private static bool TokenEmbeddedInCharterName(
        string s, int start, int length, IReadOnlyCollection<string> knownCharters)
    {
        return knownCharters.Concat(CharterIdentityMap.Keys).Any(name =>
        {
            if (name.Length <= length) return false;
            var i = s.IndexOf(name, Math.Max(0, start + length - name.Length), StringComparison.OrdinalIgnoreCase);
            return i >= 0 && i <= start;
        });
    }

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
        selector = CreatePlateSelector(bestKanji!, bestVersions!);
        return true;
    }

    private static Selector.Plate CreatePlateSelector(string kanji, string[] versions) =>
        new(kanji, versions,
            FinaleAndEarlierPlateKanji.Contains(kanji) ? PlateScope.FinaleAndEarlier : PlateScope.VersionGroup,
            NamedPlateImpliedThreshold.GetValueOrDefault(kanji));

    /// <summary>找 workingPart 内最靠右的「复活曲」token。</summary>
    private static bool TryFindRevivalInString(string s, out int start, out int length, out Selector? selector)
    {
        start = -1; length = 0; selector = null;
        var idx = s.LastIndexOf(RevivalGenreToken, StringComparison.Ordinal);
        if (idx < 0) return false;
        start = idx; length = RevivalGenreToken.Length; selector = new Selector.Revival();
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
    ///     找 workingPart 内最佳（length DESC, rightmost）谱师别名 key 匹配。
    ///     OrdinalIgnoreCase：别名含 ASCII（maistar / Revo / 7.3）时大小写不敏感。
    /// </summary>
    private static bool TryFindRightmostCharterAliasInString(
        string s, out int start, out int length, out Selector? selector)
    {
        start = -1; length = 0; selector = null;

        int bestStart = -1, bestLen = 0;
        string[]? bestNames = null;
        string? bestKey = null;

        foreach (var (alias, names) in CharterAliasMap)
        {
            var idx = s.LastIndexOf(alias, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;
            if (alias.Length > bestLen || (alias.Length == bestLen && idx > bestStart))
            {
                bestStart = idx; bestLen = alias.Length; bestNames = names; bestKey = alias;
            }
        }

        if (bestStart < 0) return false;
        start = bestStart; length = bestLen;
        var exclude = CharterAliasExclude.GetValueOrDefault(bestKey!, []);
        selector = new Selector.CharterAlias(bestNames!, exclude, s.Substring(bestStart, bestLen));
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
        // 可选「定数」前缀强制按定数解析，用于与同形谱师别名（如「7.3」）消歧：
        // 「7.3完成表」走别名，「定数7.3完成表」走定数。前缀计入 token 长度，靠 longest-first 压过别名。
        var matches = System.Text.RegularExpressions.Regex.Matches(s, @"(?:定数)?((?:1[0-5]|[1-9])\.\d)");
        for (var i = matches.Count - 1; i >= 0; i--)
        {
            var m   = matches[i];
            var num = m.Groups[1];   // 数字部分（不含前缀）；边界检查只针对数字两侧。
            if (!IsCompleteNumberToken(s, num.Index, num.Length)) continue;
            if (!double.TryParse(num.Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v)) continue;
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
