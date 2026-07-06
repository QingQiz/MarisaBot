using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using Marisa.Plugin.Shared.MaiMaiDx;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class MaiMaiDxPlateDataTest
{
    // 含合作名义"サファ太 vs 翠楼屋"，验证 substring 命中。"rintaro soma" 同时进 charters + artists
    // (见下) 用于 CharterOrArtist union case。
    private static readonly IReadOnlyCollection<string> Charters =
    [
        "翠楼屋", "サファ太 vs 翠楼屋", "Jack", "はっぴー", "ロシェ@ペンギン", "rioN", "rintaro soma",
        "超七味星人", "隅田川星人", // 名字含版本代字（超=GreeN / 星=UNiVERSE），验证反查保护
        "Revo@LC",                 // 别名"Revo"是它的子串，验证 CharterAlias 反查保护
        "JAQ", "Jack & Licorice Gunjyo", "群青リコリス", // Jack/群青リコリス 未设别名（可打），验证解析正确
    ];

    // 含合作名义"sasakure.UK x DECO*27"，验证 substring 命中；"HIMEHINA" 验证纯 artist 单边命中。
    private static readonly IReadOnlyCollection<string> Artists =
    [
        "DECO*27", "sasakure.UK x DECO*27", "HIMEHINA", "rintaro soma", "Various Artists",
    ];

    private static PlateData.Query MustParse(string raw)
    {
        var ok = PlateData.TryParse(raw, Charters, Artists, out var query, out var error);
        Assert.That(ok, Is.True, $"parse failed for '{raw}': {error?.Kind}/{error?.Detail}");
        return query!;
    }

    private static PlateData.ParseError MustFail(string raw)
    {
        var ok = PlateData.TryParse(raw, Charters, Artists, out _, out var error);
        Assert.That(ok, Is.False, $"parse unexpectedly succeeded for '{raw}'");
        return error!;
    }

    private static MaiMaiSong CreateSong(long id, string version)
    {
        dynamic song = new ExpandoObject();
        song.id = id.ToString();
        song.title = $"song-{id}";
        song.type = "SD";

        dynamic basicInfo = new ExpandoObject();
        basicInfo.title = $"song-{id}";
        basicInfo.artist = "artist";
        basicInfo.genre = "genre";
        basicInfo.bpm = 120;
        basicInfo.release_date = "2024-01-01";
        basicInfo.from = version;
        basicInfo.is_new = false;
        song.basic_info = basicInfo;

        song.ds = new[] { 3.0, 7.0, 10.0, 12.0, 13.0 };
        song.level = new[] { "3", "7", "10", "12", "13" };
        song.charts = Enumerable.Range(0, 5).Select(_ =>
        {
            dynamic chart = new ExpandoObject();
            chart.notes = new long[] { 100, 10, 10, 0 };
            chart.charter = "-";
            return chart;
        }).ToArray();

        return new MaiMaiSong(song);
    }

    [TestCase("真大将完成表",   "真",        13)]
    [TestCase("真将完成表",     "真",        12)]
    [TestCase("真代SSS+完成表", "真",        13)]
    [TestCase("真代SSS完成表",  "真",        12)]
    [TestCase("真SSS完成表",    "真",        12)]
    [TestCase("真sss完成表",    "真",        12)]
    public void ParsesPlateAchievement(string raw, string kanji, int level)
    {
        var query = MustParse(raw);
        Assert.That(query.Selectors.Single(), Is.InstanceOf<PlateData.Selector.Plate>());
        var plate = (PlateData.Selector.Plate)query.Selectors.Single();
        Assert.That(plate.Kanji, Is.EqualTo(kanji));
        Assert.That(query.Threshold.Dim, Is.EqualTo(PlateData.Dimension.Achievement));
        Assert.That(query.Threshold.Level, Is.EqualTo(level));
    }

    [TestCase("熊将完成表",   new[] { "maimai でらっくす" })]
    [TestCase("华将完成表",   new[] { "maimai でらっくす" })] // 简体华
    [TestCase("華将完成表",   new[] { "maimai でらっくす" })] // 繁体華
    [TestCase("镜将完成表",   new[] { "maimai でらっくす PRiSM" })] // 简体镜
    [TestCase("鏡将完成表",   new[] { "maimai でらっくす PRiSM" })]
    [TestCase("彩将完成表",   new[] { "maimai でらっくす PRiSM PLUS" })]
    [TestCase("辉将完成表",   new[] { "maimai FiNALE" })]    // 简体辉
    [TestCase("輝将完成表",   new[] { "maimai FiNALE" })]
    [TestCase("真将完成表",   new[] { "maimai", "maimai PLUS" })] // 真双 from
    public void PlateMapsToCorrectVersions(string raw, string[] expected)
    {
        var query = MustParse(raw);
        var plate = (PlateData.Selector.Plate)query.Selectors.Single();
        Assert.That(plate.Versions, Is.EquivalentTo(expected));
    }

    [Test]
    public void FinaleAndEarlierPlateMapsToOldVersions()
    {
        var query = MustParse("舞将完成表");
        var plate = query.Selectors.OfType<PlateData.Selector.Plate>().Single();

        Assert.That(plate.Kanji, Is.EqualTo("舞"));
        Assert.That(plate.Scope, Is.EqualTo(PlateData.PlateScope.FinaleAndEarlier));
        Assert.That(plate.Versions, Is.EqualTo(new[]
        {
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
        }));
    }

    [TestCase("翠楼屋将完成表",     "翠楼屋", 12)]
    [TestCase("Jack神完成表",       "Jack",   3)]    // Fc=ap
    [TestCase("rioN大将完成表",     "rioN",   13)]
    [TestCase("はっぴー理论值完成表", "はっぴー", 4)]   // Fc=app
    public void ParsesCharter(string raw, string charter, int level)
    {
        var query = MustParse(raw);
        Assert.That(query.Selectors.Single(), Is.InstanceOf<PlateData.Selector.Charter>());
        var c = (PlateData.Selector.Charter)query.Selectors.Single();
        Assert.That(c.Name, Is.EqualTo(charter));
        Assert.That(query.Threshold.Level, Is.EqualTo(level));
    }

    [TestCase("术力口将完成表",  "niconico & VOCALOID")]
    [TestCase("V家舞舞完成表",   "niconico & VOCALOID")]
    [TestCase("东方神完成表",    "东方Project")]
    [TestCase("击中极完成表",    "音击&中二节奏")]
    [TestCase("流行将完成表",    "流行&动漫")]
    [TestCase("流行动漫大将完成表", "流行&动漫")]
    [TestCase("其他SSS+完成表",   "其他游戏")]
    [TestCase("宴会场FC完成表",   "宴会場")]
    [TestCase("舞萌将完成表",     "舞萌")]
    [TestCase("原创将完成表",     "舞萌")]
    [TestCase("东方Project将完成表", "东方Project")] // 全名直传
    public void ParsesGenre(string raw, string fullName)
    {
        var query = MustParse(raw);
        Assert.That(query.Selectors.Single(), Is.InstanceOf<PlateData.Selector.Genre>());
        var g = (PlateData.Selector.Genre)query.Selectors.Single();
        Assert.That(g.FullName, Is.EqualTo(fullName));
    }

    [TestCase("丸将完成表",   "丸")]
    [TestCase("丸代神完成表", "丸")]
    public void RejectsBlockedPlates(string raw, string kanji)
    {
        var error = MustFail(raw);
        Assert.That(error.Kind, Is.EqualTo(PlateData.ErrorKind.UnsupportedPlate));
        Assert.That(error.Detail, Is.EqualTo(kanji));
    }

    [Test]
    public void DoesNotStripDaiForCharter()
    {
        // "翠楼屋代" 不能被剥成"翠楼屋" — "代"后缀仅对版本代字生效。
        // charter 阶段总是接受，于是 selector 落到 Charter("翠楼屋代")，handler 在
        // SelectChartsForQuery 里 substring 找不到任何含"翠楼屋代"的真 charter 名，
        // 报 "没有找到歌曲"。这里只验证解析层。
        var query = MustParse("翠楼屋代将完成表");
        Assert.That(query.Selectors.Single(), Is.InstanceOf<PlateData.Selector.Charter>());
        Assert.That(((PlateData.Selector.Charter)query.Selectors.Single()).Name, Is.EqualTo("翠楼屋代"));
    }

    [Test]
    public void AcceptsArbitraryCharterStringAsCharter()
    {
        // "张顺飞" 不存在 — 但因为不是代字也不是 genre alias，按规则当 charter 接受。
        // 真不真存在由 SelectChartsForQuery 里的 substring 匹配决定（找不到时 handler 报 0 首）。
        var query = MustParse("张顺飞将完成表");
        Assert.That(query.Selectors.Single(), Is.InstanceOf<PlateData.Selector.Charter>());
        Assert.That(((PlateData.Selector.Charter)query.Selectors.Single()).Name, Is.EqualTo("张顺飞"));
    }

    [Test]
    public void RejectsNotPlateCommand()
    {
        Assert.That(MustFail("真将").Kind, Is.EqualTo(PlateData.ErrorKind.NotPlateCommand));
        Assert.That(MustFail("hello world").Kind, Is.EqualTo(PlateData.ErrorKind.NotPlateCommand));
    }

    [Test]
    public void RejectsEmptyQuery()
    {
        Assert.That(MustFail("完成表").Kind, Is.EqualTo(PlateData.ErrorKind.EmptyQuery));
        Assert.That(MustFail("  完成表  ").Kind, Is.EqualTo(PlateData.ErrorKind.EmptyQuery));
    }

    // ──────────────────────────────────────────────────────────────────────
    // 缺省 fallback：阈值未指定时默认 SSS（"将"）。难度未指定时，
    // 版本代字查询默认 MASTER；其它查询默认 MASTER + Re:MASTER。
    // ──────────────────────────────────────────────────────────────────────

    [TestCase("真完成表",   "真",   12)]   // selector=Plate, default SSS + MASTER
    [TestCase("真代完成表", "真",   12)]   // 含可选"代"
    [TestCase("熊完成表",   "熊",   12)]
    public void DefaultsToSssWhenThresholdMissingForPlate(string raw, string kanji, int level)
    {
        var q = MustParse(raw);
        Assert.That(q.Selectors.Single(), Is.InstanceOf<PlateData.Selector.Plate>());
        Assert.That(((PlateData.Selector.Plate)q.Selectors.Single()).Kanji, Is.EqualTo(kanji));
        Assert.That(q.Threshold.Dim, Is.EqualTo(PlateData.Dimension.Achievement));
        Assert.That(q.Threshold.Level, Is.EqualTo(level));
        Assert.That(q.Threshold.DisplayName, Is.EqualTo("SSS"));
        Assert.That(q.LevelIdxes, Is.EquivalentTo(new[] {3}));
    }

    [TestCase("舞完成表")]
    [TestCase("舞代完成表")]
    public void FinaleAndEarlierPlateDefaultsToMasterRemaster(string raw)
    {
        var q = MustParse(raw);
        Assert.That(q.Selectors.OfType<PlateData.Selector.Plate>().Single().Scope,
            Is.EqualTo(PlateData.PlateScope.FinaleAndEarlier));
        Assert.That(q.Threshold.DisplayName, Is.EqualTo("SSS"));
        Assert.That(q.LevelIdxes, Is.EquivalentTo(new[] {3, 4}));
    }

    [Test]
    public void FinaleAndEarlierPlateExplicitDifficultyKept()
    {
        var q = MustParse("舞红谱完成表");
        Assert.That(q.LevelIdxes, Is.EquivalentTo(new[] {2}));
    }

    [TestCase(144, "maimai PLUS")]  // air's gravity
    [TestCase(240, "maimai GreeN")] // Beat of getting entangled
    [TestCase(261, "maimai GreeN")] // Death Scythe
    [TestCase(108, "maimai PLUS")]  // YA･DA･YO [Reborn]
    public void FinaleAndEarlierPlateExcludesRemasterOutsideWhitelist(long songId, string version)
    {
        var plate = MustParse("舞完成表").Selectors.OfType<PlateData.Selector.Plate>().Single();
        var song = CreateSong(songId, version);

        Assert.That(PlateData.MatchPlate(plate, song, 4), Is.False);
        Assert.That(PlateData.MatchPlate(plate, song, 3), Is.True);
    }

    [TestCase(17,  "maimai")]        // Future
    [TestCase(204, "maimai GreeN")]  // ナイト・オブ・ナイツ
    [TestCase(838, "maimai FiNALE")] // 最終鬼畜妹フランドール・S
    public void FinaleAndEarlierPlateKeepsWhitelistedRemaster(long songId, string version)
    {
        var plate = MustParse("舞完成表").Selectors.OfType<PlateData.Selector.Plate>().Single();
        var song = CreateSong(songId, version);

        Assert.That(PlateData.MatchPlate(plate, song, 4), Is.True);
    }

    [Test]
    public void NormalVersionPlateDoesNotApplyFinaleAndEarlierRemasterExclusion()
    {
        var plate = MustParse("真完成表").Selectors.OfType<PlateData.Selector.Plate>().Single();
        var song = CreateSong(144, "maimai PLUS");

        Assert.That(PlateData.MatchPlate(plate, song, 4), Is.True);
    }

    // ──────────────────────────────────────────────────────────────────────
    // 霸者：特例牌子——舞系同款歌曲（旧框），固定要求全曲达成率 ≥ 80%（A）。
    // ──────────────────────────────────────────────────────────────────────

    [Test]
    public void AllOldVersionsClearPlateImpliesAThreshold()
    {
        var q = MustParse("霸者完成表");
        var plate = q.Selectors.OfType<PlateData.Selector.Plate>().Single();
        Assert.That(plate.Kanji, Is.EqualTo("霸者"));
        Assert.That(plate.Scope, Is.EqualTo(PlateData.PlateScope.FinaleAndEarlier));
        // 与「舞」系同范围（旧框）
        var wuVersions = MustParse("舞完成表").Selectors.OfType<PlateData.Selector.Plate>().Single().Versions;
        Assert.That(plate.Versions, Is.EqualTo(wuVersions));
        // 固定要求全曲达成率 ≥ A（80%）
        Assert.That(q.Threshold.Dim, Is.EqualTo(PlateData.Dimension.Achievement));
        Assert.That(q.Threshold.Level, Is.EqualTo(5));
        Assert.That(q.Threshold.DisplayName, Is.EqualTo("A"));
        // FinaleAndEarlier scope → 默认 MASTER + Re:MASTER
        Assert.That(q.LevelIdxes, Is.EquivalentTo(new[] {3, 4}));
    }

    [Test]
    public void AllOldVersionsClearPlateKeepsThresholdWithExplicitDifficulty()
    {
        var q = MustParse("霸者红谱完成表");
        Assert.That(q.Selectors.OfType<PlateData.Selector.Plate>().Single().Kanji, Is.EqualTo("霸者"));
        Assert.That(q.Threshold.DisplayName, Is.EqualTo("A"));
        Assert.That(q.LevelIdxes, Is.EquivalentTo(new[] {2}));
    }

    [Test]
    public void AllOldVersionsClearPlateRemasterAndRevivalHandling()
    {
        var plate = MustParse("霸者完成表").Selectors.OfType<PlateData.Selector.Plate>().Single();

        // 白名单内 Re:MASTER 计入（最終鬼畜妹フランドール・S 838）
        Assert.That(PlateData.MatchPlate(plate, CreateSong(838, "maimai FiNALE"), 4), Is.True);
        // 白名单外 Re:MASTER 不计入，但 MASTER 计入（air's gravity 144）
        Assert.That(PlateData.MatchPlate(plate, CreateSong(144, "maimai PLUS"), 4), Is.False);
        Assert.That(PlateData.MatchPlate(plate, CreateSong(144, "maimai PLUS"), 3), Is.True);
        // 复活曲整曲排除（ヒバナ 792）
        Assert.That(PlateData.MatchPlate(plate, CreateSong(792, "maimai FiNALE"), 3), Is.False);
    }

    // 复活曲：国服删后复活的歌不计入任何版本完成牌（所有难度，含「舞」）。
    [TestCase("白", 688,   "maimai MiLK")]      // 麒麟
    [TestCase("真", 146,   "maimai PLUS")]      // 39
    [TestCase("熊", 10146, "maimai でらっくす")]  // 39（DX 谱）
    [TestCase("菫", 853,   "maimai MURASAKi PLUS")] // 前前前世
    public void RevivalSongExcludedFromItsVersionPlate(string plateKanji, long songId, string version)
    {
        var plate = MustParse($"{plateKanji}完成表").Selectors.OfType<PlateData.Selector.Plate>().Single();
        var song = CreateSong(songId, version);

        Assert.That(PlateData.MatchPlate(plate, song, 3), Is.False);
        Assert.That(PlateData.MatchPlate(plate, song, 0), Is.False);
    }

    // 复活曲在「舞」牌同样整曲排除——含原本在 Re:MASTER 白名单里的 ヒバナ/妄想感傷代償連盟。
    [TestCase(792, "maimai FiNALE")] // ヒバナ
    [TestCase(731, "MiLK PLUS")]     // 妄想感傷代償連盟
    [TestCase(688, "maimai MiLK")]   // 麒麟
    public void RevivalSongExcludedFromFinaleAndEarlierPlate(long songId, string version)
    {
        var plate = MustParse("舞完成表").Selectors.OfType<PlateData.Selector.Plate>().Single();
        var song = CreateSong(songId, version);

        Assert.That(PlateData.MatchPlate(plate, song, 3), Is.False);
        Assert.That(PlateData.MatchPlate(plate, song, 4), Is.False);
    }

    // 「首发缺席→后补」类（按难度档表现不一致）暂不收录，仍计入对应版本牌。
    [TestCase("橙", 382, "maimai ORANGE")] // おこちゃま戦争
    [TestCase("白", 711, "maimai MiLK")]   // 拝啓ドッペルゲンガー
    public void LaunchAbsentSongStillCountsForPlate(string plateKanji, long songId, string version)
    {
        var plate = MustParse($"{plateKanji}完成表").Selectors.OfType<PlateData.Selector.Plate>().Single();
        var song = CreateSong(songId, version);

        Assert.That(PlateData.MatchPlate(plate, song, 3), Is.True);
    }

    // 魔理沙は…(201)：国服姓名框上线前因合规原因本地临时下架、上线当版复活、日亚服从未删——实测仍计入超牌，不入排除集。
    [Test]
    public void CnLocalCensorRevivalStillCountsForPlate()
    {
        var plate = MustParse("超完成表").Selectors.OfType<PlateData.Selector.Plate>().Single();
        var song = CreateSong(201, "maimai GreeN");

        Assert.That(PlateData.MatchPlate(plate, song, 3), Is.True);
        Assert.That(PlateData.MatchPlate(plate, song, 0), Is.True);
    }

    // ──────────────────────────────────────────────────────────────────────
    // 复活曲：虚拟类别。独立查 = 全部复活曲；与版本代字组合 = 首发自该版本的复活曲。
    // ──────────────────────────────────────────────────────────────────────

    [Test]
    public void RevivalGenreParsesStandalone()
    {
        var q = MustParse("复活曲完成表");
        Assert.That(q.Selectors.Single(), Is.InstanceOf<PlateData.Selector.Revival>());
        Assert.That(q.Threshold.DisplayName, Is.EqualTo("SSS"));          // 默认阈值
        Assert.That(q.LevelIdxes, Is.EquivalentTo(new[] {3, 4}));         // 类别默认 MASTER + Re:MASTER
    }

    [Test]
    public void RevivalGenreParsesWithThreshold()
    {
        var q = MustParse("复活曲神完成表");
        Assert.That(q.Selectors.Single(), Is.InstanceOf<PlateData.Selector.Revival>());
        Assert.That(q.Threshold.Dim, Is.EqualTo(PlateData.Dimension.Fc));
        Assert.That(q.Threshold.DisplayName, Is.EqualTo("AP"));           // 神
    }

    [Test]
    public void RevivalGenreCombinesWithVersionPlate()
    {
        // 真代复活曲 = Plate(真) ∩ Revival；难度默认仍是类别的 [3,4]（不被版本牌的单 MASTER 覆盖）。
        var q = MustParse("真代复活曲完成表");
        Assert.That(q.Selectors, Has.Exactly(2).Items);
        Assert.That(q.Selectors.OfType<PlateData.Selector.Plate>().Single().Kanji, Is.EqualTo("真"));
        Assert.That(q.Selectors.OfType<PlateData.Selector.Revival>().Any(), Is.True);
        Assert.That(q.LevelIdxes, Is.EquivalentTo(new[] {3, 4}));
    }

    [Test]
    public void IsRevivalSongChecksList()
    {
        Assert.That(PlateData.IsRevivalSong(146), Is.True);    // 39（SD）
        Assert.That(PlateData.IsRevivalSong(10146), Is.True);  // 39（DX）
        Assert.That(PlateData.IsRevivalSong(44), Is.True);     // ハッピーシンセサイザ
        Assert.That(PlateData.IsRevivalSong(204), Is.False);   // 非复活曲
    }

    // 「真代复活曲」= 首发自 maimai/maimai PLUS 的复活曲：含 39(SD,146) 与 快乐合成器(44)，不含 39(DX,10146)。
    [Test]
    public void RevivalGenreWithPlateFiltersByOriginVersion()
    {
        var plate = MustParse("真完成表").Selectors.OfType<PlateData.Selector.Plate>().Single();

        // 查询带复活曲 → 绕过排除，按首发版本筛
        Assert.That(PlateData.MatchPlate(plate, CreateSong(146, "maimai PLUS"), 3, includeRevival: true), Is.True);          // 39 SD
        Assert.That(PlateData.MatchPlate(plate, CreateSong(44,  "maimai PLUS"), 3, includeRevival: true), Is.True);          // 快乐合成器
        Assert.That(PlateData.MatchPlate(plate, CreateSong(10146, "maimai でらっくす"), 3, includeRevival: true), Is.False); // 39 DX：版本不属真

        // 普通真牌（不带复活曲）→ 复活曲仍整曲排除
        Assert.That(PlateData.MatchPlate(plate, CreateSong(146, "maimai PLUS"), 3, includeRevival: false), Is.False);
    }

    [Test]
    public void DefaultsToSssWhenThresholdMissingForCharter()
    {
        var q = MustParse("翠楼屋完成表");
        Assert.That(q.Selectors.Single(), Is.InstanceOf<PlateData.Selector.Charter>());
        Assert.That(((PlateData.Selector.Charter)q.Selectors.Single()).Name, Is.EqualTo("翠楼屋"));
        Assert.That(q.Threshold.DisplayName, Is.EqualTo("SSS"));
        // 难度未指定 → 默认 MASTER + Re:MASTER
        Assert.That(q.LevelIdxes, Is.EquivalentTo(new[] {3, 4}));
    }

    [Test]
    public void DefaultsToSssWhenThresholdMissingForCharterWithDifficulty()
    {
        // 阈值缺省 + 难度显式给：threshold=SSS / LevelIdxes=[EXPERT]
        var q = MustParse("翠楼屋红谱完成表");
        Assert.That(q.Selectors.Single(), Is.InstanceOf<PlateData.Selector.Charter>());
        Assert.That(q.Threshold.DisplayName, Is.EqualTo("SSS"));
        Assert.That(q.LevelIdxes, Is.EquivalentTo(new[] {2}));
    }

    // 注：以前 RejectsUnknownThreshold 测 "真完成表" 应该报 UnknownThreshold。改默认 fallback 后
    // 该输入合法 → Plate(真) + SSS + MASTER（见 DefaultsToSssWhenThresholdMissingForPlate）。
    // ErrorKind.UnknownThreshold 整个 enum value 已从 PlateData 移除（dead code）。

    [Test]
    public void LongestThresholdMatchPicksDaiJiang()
    {
        // "真大将完成表" 应取"大将"(SSS+)而非"将"(SSS)
        var query = MustParse("真大将完成表");
        Assert.That(query.Threshold.Level, Is.EqualTo(13)); // SSS+
        Assert.That(query.Threshold.DisplayName, Is.EqualTo("SSS+"));
    }

    [Test]
    public void LongestThresholdMatchPicksFdxPlus()
    {
        var query = MustParse("熊FDX+完成表");
        Assert.That(query.Threshold.Dim, Is.EqualTo(PlateData.Dimension.Fs));
        Assert.That(query.Threshold.Level, Is.EqualTo(5));
        Assert.That(query.Threshold.DisplayName, Is.EqualTo("FDX+"));
    }

    [TestCase(101.0, 13)] // SSS+
    [TestCase(100.5, 13)] // SSS+ 边界
    [TestCase(100.0, 12)] // SSS
    [TestCase(99.9999, 11)] // SS+
    [TestCase(0.0, 0)]      // D
    public void AchievementLevelBoundaries(double ach, int expected)
    {
        Assert.That(PlateData.AchievementLevel(ach), Is.EqualTo(expected));
    }

    [TestCase("",     0)]
    [TestCase("fc",   1)]
    [TestCase("fcp",  2)]
    [TestCase("ap",   3)]
    [TestCase("app",  4)]
    [TestCase("xxx",  0)]
    public void FcLevelMapping(string fc, int expected)
    {
        Assert.That(PlateData.FcLevel(fc), Is.EqualTo(expected));
    }

    [TestCase("",     0)]
    [TestCase("sync", 1)]
    [TestCase("fs",   2)]
    [TestCase("fsp",  3)]
    [TestCase("fdx",  4)]
    [TestCase("fdxp", 5)]
    public void FsLevelMapping(string fs, int expected)
    {
        Assert.That(PlateData.FsLevel(fs), Is.EqualTo(expected));
    }

    [TestCase("真代SSS+紫谱完成表",    3)]
    [TestCase("真代SSS+MASTER完成表",  3)]
    [TestCase("真代SSS+MST完成表",     3)]
    [TestCase("真代SSS+红谱完成表",    2)]
    [TestCase("真代SSS+EXPERT完成表",  2)]
    [TestCase("真代SSS+EXP完成表",     2)]
    [TestCase("真代SSS+expert完成表",  2)]   // case-insensitive
    [TestCase("熊AP+黄谱完成表",       1)]
    [TestCase("熊AP+ADVANCED完成表",   1)]
    [TestCase("熊AP+ADV完成表",        1)]
    [TestCase("鏡神绿谱完成表",        0)]
    [TestCase("鏡神BASIC完成表",       0)]
    [TestCase("鏡神BSC完成表",         0)]
    public void DifficultyAliasParsing(string raw, int expectedLevelIdx)
    {
        var query = MustParse(raw);
        Assert.That(query.LevelIdxes, Is.EquivalentTo(new[] {expectedLevelIdx}));
    }

    [Test]
    public void DifficultySingleKanjiNotAccepted()
    {
        // 单字"红"不在 difficulty alias map（避免跟未来代字冲突，要写就写"红谱"或"EXPERT"）。
        // multi-selector 解析下，"真代SSS+红完成表" 剥阈值后剩"真代红"
        //   → "真代" 作 Plate(真), 单字"红"作 Charter（catch-all，handler 找不到含"红"的谱师报 0 首）。
        // 含版本代字，未显式指定难度时默认 MASTER。
        var q = MustParse("真代SSS+红完成表");
        Assert.That(q.LevelIdxes, Is.EquivalentTo(new[] {3}), "single '红' must not be parsed as difficulty");
        Assert.That(q.Threshold.DisplayName, Is.EqualTo("SSS+"));
        Assert.That(q.Selectors, Has.Exactly(2).Items);
        Assert.That(q.Selectors.OfType<PlateData.Selector.Plate>().Single().Kanji, Is.EqualTo("真"));
        Assert.That(q.Selectors.OfType<PlateData.Selector.Charter>().Single().Name, Is.EqualTo("红"));
    }

    [TestCase("真代神白谱完成表", 4)]
    [TestCase("真代神Re:MASTER完成表", 4)]
    [TestCase("熊代将re:master完成表", 4)]
    public void RemasterDifficultyParsing(string raw, int expectedLevelIdx)
    {
        Assert.That(MustParse(raw).LevelIdxes, Is.EquivalentTo(new[] {expectedLevelIdx}));
    }

    // ──────────────────────────────────────────────────────────────────────
    // 字段位置可任意互换 — 用户原话：「这里面的条件字段应该是可以随意互换位置的」
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>各种字段顺序排列都应解析成 plate=真 + threshold=SSS + difficulty=EXPERT。</summary>
    [TestCase("真EXPERT将完成表")]   // difficulty 在 threshold 前
    [TestCase("真代EXPERT将完成表")] // 同上 + 含"代"
    [TestCase("真将EXPERT完成表")]   // threshold 在 difficulty 前
    [TestCase("真代将EXPERT完成表")] // 同上 + 含"代"
    [TestCase("EXPERT真将完成表")]   // difficulty 在最前
    [TestCase("将真EXPERT完成表")]   // threshold 在最前
    public void FieldOrderIsCommutative_PlateExpertJiang(string raw)
    {
        var q = MustParse(raw);
        Assert.That(q.Selectors.Single(), Is.InstanceOf<PlateData.Selector.Plate>(), "should resolve to Plate(真)");
        Assert.That(((PlateData.Selector.Plate)q.Selectors.Single()).Kanji, Is.EqualTo("真"));
        Assert.That(q.Threshold.DisplayName, Is.EqualTo("SSS"));
        Assert.That(q.LevelIdxes, Is.EquivalentTo(new[] {2}));    // EXPERT
    }

    /// <summary>白谱（Re:MASTER）放在中间或前面都能识别。</summary>
    [TestCase("真神白谱完成表")]
    [TestCase("真白谱神完成表")]
    [TestCase("白谱真神完成表")]
    public void FieldOrderIsCommutative_PlateShinWhitePu(string raw)
    {
        var q = MustParse(raw);
        Assert.That(((PlateData.Selector.Plate)q.Selectors.Single()).Kanji, Is.EqualTo("真"));
        Assert.That(q.Threshold.DisplayName, Is.EqualTo("AP"));  // 神
        Assert.That(q.LevelIdxes, Is.EquivalentTo(new[] {4}));     // 白谱 = Re:MASTER
    }

    // ──────────────────────────────────────────────────────────────────────
    // Artist (作曲家) — substring 匹配；纯 artist 单边命中应解析为 Artist。
    // ──────────────────────────────────────────────────────────────────────

    [TestCase("HIMEHINA神完成表",        "HIMEHINA",   3)]   // Fc=AP / 默认 master+remaster
    [TestCase("HIMEHINA舞舞完成表",      "HIMEHINA",   4)]   // Fs=FDX / 默认
    [TestCase("HIMEHINA完成表",          "HIMEHINA",  12)]   // 默认 SSS / 默认
    public void ParsesArtistWhenOnlyArtistMatches(string raw, string artist, int level)
    {
        var q = MustParse(raw);
        Assert.That(q.Selectors.Single(), Is.InstanceOf<PlateData.Selector.Artist>(), $"expected Artist for '{raw}'");
        Assert.That(((PlateData.Selector.Artist)q.Selectors.Single()).Name, Is.EqualTo(artist));
        Assert.That(q.Threshold.Level, Is.EqualTo(level));
        Assert.That(q.LevelIdxes, Is.EquivalentTo(new[] {3, 4}));
    }

    [Test]
    public void ParsesArtistViaCollabSubstring()
    {
        // "DECO*27" 不在 charters 列表，但 artists 列表里有 "sasakure.UK x DECO*27" —— substring 命中
        var q = MustParse("DECO*27将完成表");
        Assert.That(q.Selectors.Single(), Is.InstanceOf<PlateData.Selector.Artist>());
        Assert.That(((PlateData.Selector.Artist)q.Selectors.Single()).Name, Is.EqualTo("DECO*27"));
    }

    [Test]
    public void ParsesArtistViaCollabSubstringSasakure()
    {
        // 验证 substring 方向：用户输 collab 名义里的部分 ("sasakure.UK")，应命中 Artist
        var q = MustParse("sasakure.UK将完成表");
        Assert.That(q.Selectors.Single(), Is.InstanceOf<PlateData.Selector.Artist>());
        Assert.That(((PlateData.Selector.Artist)q.Selectors.Single()).Name, Is.EqualTo("sasakure.UK"));
    }

    // ──────────────────────────────────────────────────────────────────────
    // CharterOrArtist union — selector 同时是某 charter substring 和某 artist substring 时
    // ──────────────────────────────────────────────────────────────────────

    [Test]
    public void ParsesCharterOrArtistWhenBothMatch()
    {
        // "rintaro soma" 在 Charters 和 Artists 列表里都有 → Selector 应合并为 CharterOrArtist，
        // handler 按 OR 筛 (谱师维度命中 ∪ 作曲家维度命中)。
        var q = MustParse("rintaro soma将完成表");
        Assert.That(q.Selectors.Single(), Is.InstanceOf<PlateData.Selector.CharterOrArtist>(),
            "rintaro soma 同时是已知谱师和作曲家，应解析为 CharterOrArtist union");
        Assert.That(((PlateData.Selector.CharterOrArtist)q.Selectors.Single()).Name, Is.EqualTo("rintaro soma"));
        Assert.That(q.Threshold.DisplayName, Is.EqualTo("SSS"));
        Assert.That(q.LevelIdxes, Is.EquivalentTo(new[] {3, 4}));
    }

    [Test]
    public void ParsesCharterOrArtistViaSubstring()
    {
        // 输 substring "rintaro" 仍应命中 union（charter "rintaro soma" 含 + artist "rintaro soma" 含）。
        var q = MustParse("rintaro完成表");
        Assert.That(q.Selectors.Single(), Is.InstanceOf<PlateData.Selector.CharterOrArtist>());
        Assert.That(((PlateData.Selector.CharterOrArtist)q.Selectors.Single()).Name, Is.EqualTo("rintaro"));
    }

    // ──────────────────────────────────────────────────────────────────────
    // Charter precise — 输入是 collab 名义的 substring 时仍能命中 Charter
    // ──────────────────────────────────────────────────────────────────────

    [Test]
    public void ParsesCharterViaCollabSubstring()
    {
        // "サファ太" 不在 charters 列表直接条目，但 "サファ太 vs 翠楼屋" 含它 — substring 命中
        // 且 artists 列表里没含 → 单边 Charter (不是 CharterOrArtist)
        var q = MustParse("サファ太将完成表");
        Assert.That(q.Selectors.Single(), Is.InstanceOf<PlateData.Selector.Charter>());
        Assert.That(((PlateData.Selector.Charter)q.Selectors.Single()).Name, Is.EqualTo("サファ太"));
    }

    // ──────────────────────────────────────────────────────────────────────
    // Level / Constant selector
    // ──────────────────────────────────────────────────────────────────────

    [Test]
    public void ParsesLevelLabel()
    {
        // "14+神完成表" → Selector.Level("14+") + Fc=AP + 指定等级 → 全难度 [0,1,2,3,4]
        var q = MustParse("14+神完成表");
        Assert.That(q.Selectors.Single(), Is.InstanceOf<PlateData.Selector.Level>());
        Assert.That(((PlateData.Selector.Level)q.Selectors.Single()).Label, Is.EqualTo("14+"));
        Assert.That(q.Threshold.Dim, Is.EqualTo(PlateData.Dimension.Fc));
        Assert.That(q.Threshold.Level, Is.EqualTo(3));
        Assert.That(q.LevelIdxes, Is.EquivalentTo(new[] {0, 1, 2, 3, 4}));
    }

    [Test]
    public void ParsesConstant()
    {
        // "14.9FDX完成表" → Selector.Constant(14.9) + Fs=FDX + 指定定数 → 全难度 [0,1,2,3,4]
        var q = MustParse("14.9FDX完成表");
        Assert.That(q.Selectors.Single(), Is.InstanceOf<PlateData.Selector.Constant>());
        Assert.That(((PlateData.Selector.Constant)q.Selectors.Single()).Value, Is.EqualTo(14.9).Within(0.001));
        Assert.That(q.Threshold.Dim, Is.EqualTo(PlateData.Dimension.Fs));
        Assert.That(q.Threshold.Level, Is.EqualTo(4));
        Assert.That(q.LevelIdxes, Is.EquivalentTo(new[] {0, 1, 2, 3, 4}));
    }

    // ──────────────────────────────────────────────────────────────────────
    // Multi-selector (AND 合取)
    // ──────────────────────────────────────────────────────────────────────

    [Test]
    public void MultiSelector_PlateAndLevel()
    {
        // "镜代13+AP完成表" → Plate(镜) ∩ Level("13+") + Fc=AP；指定等级 → 全难度 [0,1,2,3,4]
        var q = MustParse("镜代13+AP完成表");
        Assert.That(q.Selectors, Has.Exactly(2).Items);
        Assert.That(q.Selectors.OfType<PlateData.Selector.Plate>().Single().Kanji, Is.EqualTo("镜"));
        Assert.That(q.Selectors.OfType<PlateData.Selector.Level>().Single().Label, Is.EqualTo("13+"));
        Assert.That(q.Threshold.DisplayName, Is.EqualTo("AP"));
        Assert.That(q.LevelIdxes, Is.EquivalentTo(new[] {0, 1, 2, 3, 4}));
    }

    [Test]
    public void MultiSelector_PlateLevelExplicitDifficulty()
    {
        // 显式写难度时以难度 token 为准：镜代 紫谱(MASTER) 13+ → 仅 [MASTER]，覆盖「含等级 → 全难度」默认
        var q = MustParse("镜代紫谱13+完成表");
        Assert.That(q.Selectors.OfType<PlateData.Selector.Plate>().Single().Kanji, Is.EqualTo("镜"));
        Assert.That(q.Selectors.OfType<PlateData.Selector.Level>().Single().Label, Is.EqualTo("13+"));
        Assert.That(q.LevelIdxes, Is.EquivalentTo(new[] {3}));
    }

    [Test]
    public void MultiSelector_PlateAndGenre()
    {
        // "镜代V家将完成表" → Plate(镜) ∩ Genre("niconico & VOCALOID") + SSS
        var q = MustParse("镜代V家将完成表");
        Assert.That(q.Selectors, Has.Exactly(2).Items);
        Assert.That(q.Selectors.OfType<PlateData.Selector.Plate>().Single().Kanji, Is.EqualTo("镜"));
        Assert.That(q.Selectors.OfType<PlateData.Selector.Genre>().Single().FullName, Is.EqualTo("niconico & VOCALOID"));
        Assert.That(q.Threshold.DisplayName, Is.EqualTo("SSS"));
        Assert.That(q.LevelIdxes, Is.EquivalentTo(new[] {3}));
    }

    [Test]
    public void MultiSelector_PlateAndConstant()
    {
        // "镜代14.6神完成表" → Plate(镜) ∩ Constant(14.6) + AP；指定定数 → 全难度 [0,1,2,3,4]
        var q = MustParse("镜代14.6神完成表");
        Assert.That(q.Selectors, Has.Exactly(2).Items);
        Assert.That(q.Selectors.OfType<PlateData.Selector.Plate>().Single().Kanji, Is.EqualTo("镜"));
        Assert.That(q.Selectors.OfType<PlateData.Selector.Constant>().Single().Value, Is.EqualTo(14.6).Within(0.001));
        Assert.That(q.Threshold.DisplayName, Is.EqualTo("AP"));
        Assert.That(q.LevelIdxes, Is.EquivalentTo(new[] {0, 1, 2, 3, 4}));
    }

    [Test]
    public void MultiSelector_LevelAndCharter()
    {
        // "14+翠楼屋将完成表" → Level("14+") ∩ Charter("翠楼屋")
        var q = MustParse("14+翠楼屋将完成表");
        Assert.That(q.Selectors, Has.Exactly(2).Items);
        Assert.That(q.Selectors.OfType<PlateData.Selector.Level>().Single().Label, Is.EqualTo("14+"));
        Assert.That(q.Selectors.OfType<PlateData.Selector.Charter>().Single().Name, Is.EqualTo("翠楼屋"));
    }

    [Test]
    public void MultiSelector_GenreAndCharter()
    {
        // "V家翠楼屋将完成表" → Genre(niconico) ∩ Charter("翠楼屋")
        var q = MustParse("V家翠楼屋将完成表");
        Assert.That(q.Selectors, Has.Exactly(2).Items);
        Assert.That(q.Selectors.OfType<PlateData.Selector.Genre>().Single().FullName, Is.EqualTo("niconico & VOCALOID"));
        Assert.That(q.Selectors.OfType<PlateData.Selector.Charter>().Single().Name, Is.EqualTo("翠楼屋"));
    }

    [Test]
    public void MultiSelector_ThreeWayPlateLevelGenre()
    {
        // "镜代13+V家将完成表" → 3 个 selector AND
        var q = MustParse("镜代13+V家将完成表");
        Assert.That(q.Selectors, Has.Exactly(3).Items);
        Assert.That(q.Selectors.OfType<PlateData.Selector.Plate>().Single().Kanji, Is.EqualTo("镜"));
        Assert.That(q.Selectors.OfType<PlateData.Selector.Level>().Single().Label, Is.EqualTo("13+"));
        Assert.That(q.Selectors.OfType<PlateData.Selector.Genre>().Single().FullName, Is.EqualTo("niconico & VOCALOID"));
        Assert.That(q.LevelIdxes, Is.EquivalentTo(new[] {0, 1, 2, 3, 4}));   // 含 Level → 全难度
    }

    [TestCase("镜代13+AP完成表")]
    [TestCase("AP镜代13+完成表")]
    [TestCase("13+AP镜代完成表")]
    [TestCase("13+镜代AP完成表")]
    [TestCase("AP13+镜代完成表")]
    public void MultiSelector_OrderIndependent(string raw)
    {
        // 多 selector + 阈值/难度的字段顺序应不影响解析结果（rightmost match + iterative strip）
        var q = MustParse(raw);
        Assert.That(q.Selectors, Has.Exactly(2).Items);
        Assert.That(q.Selectors.OfType<PlateData.Selector.Plate>().Single().Kanji, Is.EqualTo("镜"));
        Assert.That(q.Selectors.OfType<PlateData.Selector.Level>().Single().Label, Is.EqualTo("13+"));
        Assert.That(q.Threshold.DisplayName, Is.EqualTo("AP"));
        Assert.That(q.LevelIdxes, Is.EquivalentTo(new[] {0, 1, 2, 3, 4}));   // 含 Level → 全难度
    }

    // ──────────────────────────────────────────────────────────────────────
    // 同类 selector 冲突 → ConflictingSelector
    // ──────────────────────────────────────────────────────────────────────

    [Test]
    public void Conflict_TwoPlates()
    {
        var err = MustFail("镜代真完成表");
        Assert.That(err.Kind, Is.EqualTo(PlateData.ErrorKind.ConflictingSelector));
        Assert.That(err.Detail, Is.EqualTo("版本"));
    }

    [Test]
    public void Conflict_TwoLevels()
    {
        var err = MustFail("13+15完成表");
        Assert.That(err.Kind, Is.EqualTo(PlateData.ErrorKind.ConflictingSelector));
        Assert.That(err.Detail, Is.EqualTo("难度或定数"));
    }

    [Test]
    public void Conflict_LevelAndConstant()
    {
        // Constant 已隐含 Level；二者同给视作冲突
        var err = MustFail("13+14.6完成表");
        Assert.That(err.Kind, Is.EqualTo(PlateData.ErrorKind.ConflictingSelector));
        Assert.That(err.Detail, Is.EqualTo("难度或定数"));
    }

    [Test]
    public void Conflict_TwoGenres()
    {
        var err = MustFail("V家东方完成表");
        Assert.That(err.Kind, Is.EqualTo(PlateData.ErrorKind.ConflictingSelector));
        Assert.That(err.Detail, Is.EqualTo("类别"));
    }

    // ──────────────────────────────────────────────────────────────────────
    // 宴会場 special-case：宴谱只有 1-2 个低 idx 谱面（没有 MASTER+Re:MASTER），
    // 默认 LevelIdxes=[3,4] 会全过滤光。Genre("宴会場") + 用户未给难度 → 默认全难度。
    // ──────────────────────────────────────────────────────────────────────

    [TestCase("宴会场完成表",   "宴会場")]
    [TestCase("宴谱完成表",     "宴会場")]
    public void BanquetGenreDefaultsToAllDifficulties(string raw, string fullName)
    {
        var q = MustParse(raw);
        Assert.That(q.Selectors.Single(), Is.InstanceOf<PlateData.Selector.Genre>());
        Assert.That(((PlateData.Selector.Genre)q.Selectors.Single()).FullName, Is.EqualTo(fullName));
        Assert.That(q.LevelIdxes, Is.EquivalentTo(new[] {0, 1, 2, 3, 4}),
            "宴会場 Genre 用户未给难度时默认应扩为全难度");
    }

    [Test]
    public void BanquetGenreExplicitDifficultyKept()
    {
        // 用户显式给 BASIC → LevelIdxes 仍是 [0]，不被特殊扩展
        var q = MustParse("宴会场BASIC完成表");
        Assert.That(q.LevelIdxes, Is.EquivalentTo(new[] {0}));
    }

    [Test]
    public void NonBanquetGenreKeepsDefaultMasterRemaster()
    {
        // 其他 Genre（如 V家）默认仍是 [3,4]；只 宴会場 特殊扩展
        var q = MustParse("V家完成表");
        Assert.That(q.LevelIdxes, Is.EquivalentTo(new[] {3, 4}));
    }

    [Test]
    public void PlateSelectorOverridesBanquetDefaultDifficulties()
    {
        var q = MustParse("宴代宴会场完成表");
        Assert.That(q.Selectors.OfType<PlateData.Selector.Plate>().Single().Kanji, Is.EqualTo("宴"));
        Assert.That(q.Selectors.OfType<PlateData.Selector.Genre>().Single().FullName, Is.EqualTo("宴会場"));
        Assert.That(q.LevelIdxes, Is.EquivalentTo(new[] {3}));
    }

    // ──────────────────────────────────────────────────────────────────────
    // DxScore 维度 — 5 个星档 threshold（1星-5星，对应 max DX 的 85/90/93/95/97%）
    // ──────────────────────────────────────────────────────────────────────

    [TestCase("镜代5星完成表",   "镜",   5)]
    [TestCase("镜代五星完成表", "镜",   5)]
    [TestCase("镜代4星完成表",   "镜",   4)]
    [TestCase("镜代四星完成表", "镜",   4)]
    [TestCase("镜代3星完成表",   "镜",   3)]
    [TestCase("镜代2星完成表",   "镜",   2)]
    [TestCase("镜代1星完成表",   "镜",   1)]
    [TestCase("镜代一星完成表", "镜",   1)]
    [TestCase("真1*完成表",     "真",   1)]
    [TestCase("真2*完成表",     "真",   2)]
    [TestCase("真3*完成表",     "真",   3)]
    [TestCase("真4*完成表",     "真",   4)]
    [TestCase("真5*完成表",     "真",   5)]
    [TestCase("镜代1*完成表",   "镜",   1)]
    [TestCase("镜代5*完成表",   "镜",   5)]
    public void ParsesDxScoreStar(string raw, string kanji, int level)
    {
        var q = MustParse(raw);
        Assert.That(q.Selectors.Single(), Is.InstanceOf<PlateData.Selector.Plate>());
        Assert.That(((PlateData.Selector.Plate)q.Selectors.Single()).Kanji, Is.EqualTo(kanji));
        Assert.That(q.Threshold.Dim, Is.EqualTo(PlateData.Dimension.DxScore));
        Assert.That(q.Threshold.Level, Is.EqualTo(level));
        Assert.That(q.Threshold.DisplayName, Is.EqualTo($"{level}★"));
    }

    [Test]
    public void DxScoreStar_WithLevel_MultiSelector()
    {
        // multi-selector: Plate(镜) ∩ Level("14+") + DxScore=5；指定等级 → 全难度 [0,1,2,3,4]
        var q = MustParse("镜代14+5星完成表");
        Assert.That(q.Selectors, Has.Exactly(2).Items);
        Assert.That(q.Selectors.OfType<PlateData.Selector.Plate>().Single().Kanji, Is.EqualTo("镜"));
        Assert.That(q.Selectors.OfType<PlateData.Selector.Level>().Single().Label, Is.EqualTo("14+"));
        Assert.That(q.Threshold.Dim, Is.EqualTo(PlateData.Dimension.DxScore));
        Assert.That(q.Threshold.Level, Is.EqualTo(5));
        Assert.That(q.LevelIdxes, Is.EquivalentTo(new[] {0, 1, 2, 3, 4}));
    }

    [Test]
    public void DxScoreStar_PlateDefaultsToMaster()
    {
        var q = MustParse("镜代5星完成表");
        Assert.That(q.LevelIdxes, Is.EquivalentTo(new[] {3}));
    }

    [Test]
    public void DxScoreStar_ExplicitDifficulty()
    {
        var q = MustParse("镜代5星红谱完成表");
        Assert.That(q.LevelIdxes, Is.EquivalentTo(new[] {2}));
        Assert.That(q.Threshold.Level, Is.EqualTo(5));
    }

    // ──────────────────────────────────────────────────────────────────────
    // 1* / 2* 简写与 digit-boundary 处理：11星完成表、91星完成表、星1星完成表
    // ──────────────────────────────────────────────────────────────────────

    [Test]
    public void StarShorthand_1Star_Asterisk()
    {
        // "1*" 等价于 "1星"
        var q = MustParse("真1*完成表");
        Assert.That(q.Selectors.OfType<PlateData.Selector.Plate>().Single().Kanji, Is.EqualTo("真"));
        Assert.That(q.Threshold.Dim, Is.EqualTo(PlateData.Dimension.DxScore));
        Assert.That(q.Threshold.Level, Is.EqualTo(1));
    }

    [TestCase("11星完成表",     null, "1",   1, 1,     "1★")]
    [TestCase("21星完成表",     null, "2",   1, 1,     "1★")]
    [TestCase("91星完成表",     null, "9",   1, 1,     "1★")]
    [TestCase("11*完成表",      null, "1",   1, 1,     "1★")]
    [TestCase("星1星完成表",     "星", null, 1, 1,     "1★")]
    [TestCase("星1*完成表",      "星", null, 1, 1,     "1★")]
    [TestCase("11星1星完成表",   "星", "11", 1, 1,     "1★")]
    [TestCase("星111*完成表",    "星", "11", 1, 1,     "1★")]
    [TestCase("星11*完成表",     "星", "1",  1, 1,     "1★")]
    [TestCase("11星SSS完成表",   "星", "11", 0, 12,    "SSS")]
    public void LevelOrPlate_WithStarThreshold(
        string raw, string? plateKanji, string? levelLabel,
        int starLevel, int thresholdLevel, string thresholdDisplay)
    {
        var q = MustParse(raw);

        Assert.That(q.Threshold.Dim,
            Is.EqualTo(starLevel > 0 ? PlateData.Dimension.DxScore : PlateData.Dimension.Achievement));
        Assert.That(q.Threshold.Level, Is.EqualTo(thresholdLevel));
        Assert.That(q.Threshold.DisplayName, Is.EqualTo(thresholdDisplay));

        var selectorCount = (plateKanji != null ? 1 : 0) + (levelLabel != null ? 1 : 0);
        Assert.That(q.Selectors, Has.Exactly(selectorCount).Items);

        if (plateKanji != null)
            Assert.That(q.Selectors.OfType<PlateData.Selector.Plate>().Single().Kanji, Is.EqualTo(plateKanji));
        if (levelLabel != null)
            Assert.That(q.Selectors.OfType<PlateData.Selector.Level>().Single().Label, Is.EqualTo(levelLabel));
    }

    // DxScoreStar helper integer math 验证（边界 85/90/93/95/97%）
    [TestCase(0,    1000, 0)]
    [TestCase(849,  1000, 0)]
    [TestCase(850,  1000, 1)]   // 85.0% 边界
    [TestCase(899,  1000, 1)]
    [TestCase(900,  1000, 2)]   // 90.0%
    [TestCase(929,  1000, 2)]
    [TestCase(930,  1000, 3)]   // 93.0%
    [TestCase(949,  1000, 3)]
    [TestCase(950,  1000, 4)]   // 95.0%
    [TestCase(969,  1000, 4)]
    [TestCase(970,  1000, 5)]   // 97.0%
    [TestCase(1000, 1000, 5)]
    [TestCase(266,  288,  2)]   // True Love Song BASIC, TamakoZz 实际样本 92.36% → 2★
    [TestCase(100,  0,    0)]   // maxDx=0 兜底
    public void DxScoreStarHelper_Boundaries(int dxScore, int maxDx, int expected)
    {
        Assert.That(PlateData.DxScoreStar(dxScore, maxDx), Is.EqualTo(expected));
    }

    // 反查保护：谱师名里恰好含版本代字时（"超七味星人"含超/星、"隅田川星人"含星），整名当 charter，不被切成 Plate + 残名。覆盖全名与半截名。
    [TestCase("隅田川星人将完成表", "隅田川星人")]
    [TestCase("隅田川星人完成表",   "隅田川星人")]
    [TestCase("超七味星人完成表",   "超七味星人")]
    [TestCase("超七味完成表",       "超七味")]
    public void ProtectsCharterNameContainingPlateKanji(string raw, string charter)
    {
        var query = MustParse(raw);
        Assert.That(query.Selectors, Has.None.InstanceOf<PlateData.Selector.Plate>());
        Assert.That(query.Selectors.Single(), Is.InstanceOf<PlateData.Selector.Charter>());
        Assert.That(((PlateData.Selector.Charter)query.Selectors.Single()).Name, Is.EqualTo(charter));
    }

    // 只给一个版本代字字时（即便 mock 里存在含该字的谱师），length == 代字长度，length 判据短路保护，仍当版本。
    [TestCase("超将完成表", "超")]
    [TestCase("星将完成表", "星")]
    public void StillTreatsBarePlateKanjiAsVersion(string raw, string kanji)
    {
        var query = MustParse(raw);
        Assert.That(query.Selectors.Single(), Is.InstanceOf<PlateData.Selector.Plate>());
        Assert.That(((PlateData.Selector.Plate)query.Selectors.Single()).Kanji, Is.EqualTo(kanji));
    }

    // 多轮 strip 后保护仍生效：先 strip Level(14)，剩"超七味星人"再触发反查保护，与 Level selector 共存。
    [Test]
    public void ProtectsCharterNameAlongsideLevelToken()
    {
        var query = MustParse("超七味星人14完成表");
        Assert.That(query.Selectors, Has.None.InstanceOf<PlateData.Selector.Plate>());
        Assert.That(query.Selectors.OfType<PlateData.Selector.Charter>().Single().Name, Is.EqualTo("超七味星人"));
        Assert.That(query.Selectors.OfType<PlateData.Selector.Level>().Single().Label, Is.EqualTo("14"));
    }

    // ──────────────────────────────────────────────────────────────────────
    // 谱师别名（CharterAlias）：中文 / 罗马音别名 → 一组 canonical 谱师 substring（OR 命中）。
    // 别名内容是静态表，与 knownCharters 无关，故这些用例不依赖上面的 Charters fixture。
    // ──────────────────────────────────────────────────────────────────────

    [TestCase("沙发太完成表",   "沙发太", "サファ太")]
    [TestCase("沙发太将完成表", "沙发太", "サファ太")]
    [TestCase("企鹅完成表",     "企鹅",   "ペンギン")]
    [TestCase("柠檬神完成表",   "柠檬",   "じゃこレモン")]
    [TestCase("二大爷完成表",   "二大爷", "ニャイン")]
    public void ParsesCharterAlias(string raw, string input, string expectedName)
    {
        var q = MustParse(raw);
        Assert.That(q.Selectors.Single(), Is.InstanceOf<PlateData.Selector.CharterAlias>());
        var ca = (PlateData.Selector.CharterAlias)q.Selectors.Single();
        Assert.That(ca.Input, Is.EqualTo(input));
        Assert.That(ca.Names, Has.Member(expectedName));
    }

    [Test]
    public void CharterAliasMergesAlterEgos()
    {
        // "沙发太" 合并本名 + 高难马甲；"哈皮" 合并 はっぴー + 緑風 犬三郎。
        var sa = (PlateData.Selector.CharterAlias)MustParse("沙发太完成表").Selectors.Single();
        Assert.That(sa.Names, Is.SupersetOf(new[] { "サファ太", "-ZONE- SaFaRi" }));

        var happy = (PlateData.Selector.CharterAlias)MustParse("哈皮完成表").Selectors.Single();
        Assert.That(happy.Names, Is.SupersetOf(new[] { "はっぴー", "緑風 犬三郎" }));
    }

    [Test]
    public void CharterAliasCarriesExclusions()
    {
        // 「小鸟游」带反向排除：含「小鳥遊」却属 サファ太 的致敬独谱要剔除（匹配层用）。
        var ca = (PlateData.Selector.CharterAlias)MustParse("小鸟游完成表").Selectors.Single();
        Assert.That(ca.Names, Has.Member("Phoenix"));
        Assert.That(ca.Exclude, Has.Member("サファ太 respects for 小鳥遊さん"));
    }

    [TestCase("华火职人完成表", "华火职人")] // 别名含版本代字「华」(= 熊華 でらっくす)
    [TestCase("桃子猫完成表",   "桃子猫")]   // 别名含版本代字「桃」(= PiNK)
    public void CharterAliasContainingPlateKanjiBeatsPlate(string raw, string input)
    {
        // 别名作为更长 token 靠长度压过单字代字，不被切成 Plate + 残字。
        var q = MustParse(raw);
        Assert.That(q.Selectors, Has.None.InstanceOf<PlateData.Selector.Plate>());
        Assert.That(q.Selectors.OfType<PlateData.Selector.CharterAlias>().Single().Input, Is.EqualTo(input));
    }

    [Test]
    public void CharterAliasSevenThreeBeatsConstant()
    {
        // "7.3" 与定数 7.3 同形，按谱师别名优先。
        var q = MustParse("7.3完成表");
        Assert.That(q.Selectors, Has.None.InstanceOf<PlateData.Selector.Constant>());
        Assert.That(q.Selectors.OfType<PlateData.Selector.CharterAlias>().Single().Names, Has.Member("シチミヘルツ"));
    }

    [Test]
    public void RealConstantStillParsesWhenNotAnAlias()
    {
        // 非别名的定数照常解析（回归：别名优先只影响 "7.3"）。
        var q = MustParse("14.6完成表");
        Assert.That(q.Selectors.Single(), Is.InstanceOf<PlateData.Selector.Constant>());
        Assert.That(((PlateData.Selector.Constant)q.Selectors.Single()).Value, Is.EqualTo(14.6).Within(0.001));
    }

    [Test]
    public void CharterAliasCombinesWithVersionPlate()
    {
        // "真桃子猫将完成表" → Plate(真) ∩ CharterAlias(桃子猫)；「桃」不被当版本。
        var q = MustParse("真桃子猫将完成表");
        Assert.That(q.Selectors, Has.Exactly(2).Items);
        Assert.That(q.Selectors.OfType<PlateData.Selector.Plate>().Single().Kanji, Is.EqualTo("真"));
        Assert.That(q.Selectors.OfType<PlateData.Selector.CharterAlias>().Single().Input, Is.EqualTo("桃子猫"));
        Assert.That(q.Threshold.DisplayName, Is.EqualTo("SSS"));
    }

    [Test]
    public void CharterAliasYieldsToRealCharterNameItIsSubstringOf()
    {
        // 别名 "Revo" 是真名 "Revo@LC" 的子串：用户输完整真名时反查保护让整段走 precise Charter，
        // 不切成 CharterAlias("Revo") + 残段。
        var q = MustParse("Revo@LC完成表");
        Assert.That(q.Selectors, Has.None.InstanceOf<PlateData.Selector.CharterAlias>());
        Assert.That(((PlateData.Selector.Charter)q.Selectors.Single()).Name, Is.EqualTo("Revo@LC"));
    }

    // ──────────────────────────────────────────────────────────────────────
    // 「定数」前缀：强制定数解析，与同形谱师别名「7.3」消歧。
    // ──────────────────────────────────────────────────────────────────────

    [TestCase("定数7.3完成表")]
    public void ConstantPrefixForcesConstantOverAlias(string raw)
    {
        var q = MustParse(raw);
        Assert.That(q.Selectors, Has.None.InstanceOf<PlateData.Selector.CharterAlias>());
        Assert.That(((PlateData.Selector.Constant)q.Selectors.Single()).Value, Is.EqualTo(7.3).Within(0.001));
    }

    [Test]
    public void ConstantPrefixWorksForOrdinaryConstants()
    {
        // 普通定数加前缀也可（一般无需，但不应报错）。
        var q = MustParse("定数14.6神完成表");
        Assert.That(((PlateData.Selector.Constant)q.Selectors.Single()).Value, Is.EqualTo(14.6).Within(0.001));
        Assert.That(q.Threshold.DisplayName, Is.EqualTo("AP"));
    }

    [Test]
    public void BareSevenThreeStillResolvesToCharterAliasNotConstant()
    {
        // 回归：无前缀的「7.3」仍按谱师别名（别名优先于同形定数）。
        var q = MustParse("7.3完成表");
        Assert.That(q.Selectors, Has.None.InstanceOf<PlateData.Selector.Constant>());
        Assert.That(q.Selectors.Single(), Is.InstanceOf<PlateData.Selector.CharterAlias>());
    }

    // Jack / 群青リコリス 未设别名（本身可打）。验证其相关署名仍正确解析为 Charter。
    [Test]
    public void JaqParsesAsCharterNotThresholdA()
    {
        // 「JAQ」是 Jack 变体署名；中间「A」不能被当 Achievement 阈值剥成「JQ」。
        var q = MustParse("JAQ完成表");
        Assert.That(q.Selectors.Single(), Is.InstanceOf<PlateData.Selector.Charter>());
        Assert.That(((PlateData.Selector.Charter)q.Selectors.Single()).Name, Is.EqualTo("JAQ"));
        Assert.That(q.Threshold.DisplayName, Is.EqualTo("SSS")); // 默认阈值，未误吞 A
    }

    [Test]
    public void JackResolvesAsCharterAndMatchesCollab()
    {
        // 打「Jack」按 Charter 解析（小写 a/c 不被当阈值），substring 命中合作署名「Jack & Licorice Gunjyo」。
        var q = MustParse("Jack完成表");
        Assert.That(q.Selectors.Single(), Is.InstanceOf<PlateData.Selector.Charter>());
        var c = (PlateData.Selector.Charter)q.Selectors.Single();
        Assert.That(c.Name, Is.EqualTo("Jack"));
        Assert.That(Charters.Any(x => x.Contains(c.Name)), Is.True); // 含 "Jack & Licorice Gunjyo"
    }

    [Test]
    public void GunjyoTypeableResolvesAsCharter()
    {
        // 群青リコリス 未设别名但可打：直接打名字按 Charter 解析（不被代字/别名误吞）。
        var q = MustParse("群青リコリス完成表");
        Assert.That(q.Selectors.Single(), Is.InstanceOf<PlateData.Selector.Charter>());
        Assert.That(((PlateData.Selector.Charter)q.Selectors.Single()).Name, Is.EqualTo("群青リコリス"));
    }

    // ──────────────────────────────────────────────────────────────────────
    // 谱师身份并集（CharterIdentityMap + MatchCharter）：精确输入某身份名时按人级并集匹配，
    // 使真名查询与别名查询同集。解析层不感知（selector 类型不变）。
    // ──────────────────────────────────────────────────────────────────────

    // 同一人的不同身份互相带出（含合作署名）；大小写不敏感。
    [TestCase("The ALiEN",  "隅田川星人",     true)]
    [TestCase("The ALiEN",  "隅田川華火大会", true)]
    [TestCase("the alien",  "隅田川星人",     true)]
    [TestCase("隅田川星人", "The ALiEN",      true)]
    [TestCase("隅田川星人", "The ALiEN vs. Phoenix", true)]
    [TestCase("翠楼屋",     "翡翠マナ",       true)]
    [TestCase("翡翠マナ",   "翠楼屋",         true)]
    [TestCase("Phoenix",    "小鳥遊さん",     true)]
    [TestCase("BELiZHEL",   "Luxizhel",       true)]
    [TestCase("ロシェ＠ペンギン", "ロシェ@ペンギン", true)]           // 全角＠身份（实际署名写法），与半角同人
    [TestCase("チャン@DP皆伝", "舞舞10年ズ ～ファイナル～", true)]   // 短子串「舞舞10年ズ」覆盖两种合作署名
    [TestCase("はっぴー",       "舞舞10年ズ ～ファイナル～", true)]
    // 合作名义不是身份：精确打合作名义仍只查该署名。
    [TestCase("七味星人",   "隅田川星人",     false)]
    [TestCase("七味星人",   "超七味星人",     true)]  // substring 命中，非身份并集
    // 非身份输入退化为单一 substring。
    [TestCase("Jack",       "Jack & Licorice Gunjyo", true)]
    [TestCase("Jack",       "隅田川星人",     false)]
    public void MatchCharterUnifiesIdentitiesOfSamePerson(string input, string signed, bool expected)
    {
        Assert.That(PlateData.MatchCharter(signed, input), Is.EqualTo(expected));
    }

    [Test]
    public void MatchCharterAppliesExcludeToIdentityQueries()
    {
        // 「サファ太 respects for 小鳥遊さん」是 サファ太 的致敬独谱：身份查询（小鳥遊/Phoenix）要剔除，
        // 但按 サファ太 身份查询要命中。
        const string respects = "サファ太 respects for 小鳥遊さん";
        Assert.That(PlateData.MatchCharter(respects, "小鳥遊"),  Is.False);
        Assert.That(PlateData.MatchCharter(respects, "Phoenix"), Is.False);
        Assert.That(PlateData.MatchCharter(respects, "サファ太"), Is.True);
    }

    [Test]
    public void CharterIdentityMapIsConsistent()
    {
        // 每个身份名必须与其名义组相关（身份是某名义的 substring 或反之），防止身份挂错组。
        foreach (var (identity, (names, _)) in PlateData.CharterIdentityMap)
        {
            Assert.That(names.Any(n =>
                    n.Contains(identity, StringComparison.OrdinalIgnoreCase)
                    || identity.Contains(n, StringComparison.OrdinalIgnoreCase)),
                Is.True, $"身份「{identity}」与其名义组无关联");
        }
    }

    [Test]
    public void IdentityQueryKeepsCharterSelectorType()
    {
        // 身份并集只在匹配层生效，解析层契约不变：真名输入仍是普通 Charter selector。
        var q = MustParse("翠楼屋将完成表");
        Assert.That(q.Selectors.Single(), Is.InstanceOf<PlateData.Selector.Charter>());
    }

    // 别名/定数 token 嵌在真名里时不参与竞争：与等级组合的真名查询不被切开（否则静默丢同人署名）。
    [TestCase("隅田川星人14完成表", "隅田川星人")] // 「隅田川星人」内嵌别名 key「隅田川」
    [TestCase("7.3Hz 14完成表",     "7.3Hz")]     // 「7.3Hz」内嵌别名/定数同形 token「7.3」
    public void IdentityNameEmbeddingAliasKeySurvivesComboQuery(string raw, string charter)
    {
        var q = MustParse(raw);
        Assert.That(q.Selectors.OfType<PlateData.Selector.Charter>().Single().Name, Is.EqualTo(charter));
        Assert.That(q.Selectors.OfType<PlateData.Selector.Level>().Single().Label, Is.EqualTo("14"));
    }

    [Test]
    public void AliasComboWithLevelStillParsesAsAlias()
    {
        // 别名不嵌在任何真名里时，组合查询照常走别名（回归）。
        var q = MustParse("川哥14完成表");
        Assert.That(q.Selectors.OfType<PlateData.Selector.CharterAlias>().Single().Input, Is.EqualTo("川哥"));
        Assert.That(q.Selectors.OfType<PlateData.Selector.Level>().Single().Label, Is.EqualTo("14"));
    }

    // ---- 游戏内成就姓名框贴图映射 ----

    [TestCase("樱将完成表", "UI_Plate_006125.png")]    // 简体代字同样命中
    [TestCase("櫻将完成表", "UI_Plate_006125.png")]
    [TestCase("櫻SSS完成表", "UI_Plate_006125.png")]   // 按 (Dim, Level) 判，不看用户敲的别名
    [TestCase("超极完成表", "UI_Plate_006104.png")]
    [TestCase("真极完成表", "UI_Plate_006101.png")]    // 真系只占 6101-6103 三号
    [TestCase("真神完成表", "UI_Plate_006102.png")]
    [TestCase("真舞舞完成表", "UI_Plate_006103.png")]
    [TestCase("霸者完成表", "UI_Plate_006148.png")]    // 固有阈值 A
    [TestCase("舞舞舞完成表", "UI_Plate_006152.png")]  // 舞系 + 舞舞
    [TestCase("熊极完成表", "UI_Plate_055101.png")]    // DX 时代块内后四位是 5101（编号体系例外）
    [TestCase("熊舞舞完成表", "UI_Plate_055104.png")]
    [TestCase("华神完成表", "UI_Plate_109103.png")]
    [TestCase("镜将完成表", "UI_Plate_559102.png")]
    [TestCase("彩将完成表", "UI_Plate_609102.png")]
    [TestCase("彩舞舞完成表", "UI_Plate_609104.png")]
    public void NamePlateImageResolves(string raw, string expected)
    {
        Assert.That(PlateData.NamePlateImage(MustParse(raw)), Is.EqualTo(expected));
    }

    [TestCase("真将完成表")]      // 真系历来无「将」
    [TestCase("樱大将完成表")]    // 大将(SSS+)没有对应姓名框
    [TestCase("霸者神完成表")]    // 覆盖固有阈值后不再是「霸者」姓名框
    [TestCase("镜代V家将完成表")] // 多 selector 不出牌
    [TestCase("翠楼屋将完成表")]  // 非版本代字查询
    public void NamePlateImageAbsent(string raw)
    {
        Assert.That(PlateData.NamePlateImage(MustParse(raw)), Is.Null);
    }
}
