using System.Collections.Generic;
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
    [TestCase("辉将完成表",   new[] { "maimai FiNALE" })]    // 简体辉
    [TestCase("輝将完成表",   new[] { "maimai FiNALE" })]
    [TestCase("真将完成表",   new[] { "maimai", "maimai PLUS" })] // 真双 from
    public void PlateMapsToCorrectVersions(string raw, string[] expected)
    {
        var query = MustParse(raw);
        var plate = (PlateData.Selector.Plate)query.Selectors.Single();
        Assert.That(plate.Versions, Is.EquivalentTo(expected));
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
    [TestCase("彩将完成表",   "彩")]
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
    // 缺省 fallback：阈值未指定时默认 SSS（"将"）；难度未指定时默认 MASTER + Re:MASTER。
    // ──────────────────────────────────────────────────────────────────────

    [TestCase("真完成表",   "真",   12)]   // selector=Plate, default SSS + (MASTER+Re:MASTER)
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
        Assert.That(q.LevelIdxes, Is.EquivalentTo(new[] {3, 4}));
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
        // LevelIdxes 仍是默认 MASTER+Re:MASTER ([3, 4])。
        var q = MustParse("真代SSS+红完成表");
        Assert.That(q.LevelIdxes, Is.EquivalentTo(new[] {3, 4}), "single '红' must not be parsed as difficulty");
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
        // "14+神完成表" → Selector.Level("14+") + Fc=AP + 默认难度 [3, 4]
        var q = MustParse("14+神完成表");
        Assert.That(q.Selectors.Single(), Is.InstanceOf<PlateData.Selector.Level>());
        Assert.That(((PlateData.Selector.Level)q.Selectors.Single()).Label, Is.EqualTo("14+"));
        Assert.That(q.Threshold.Dim, Is.EqualTo(PlateData.Dimension.Fc));
        Assert.That(q.Threshold.Level, Is.EqualTo(3));
        Assert.That(q.LevelIdxes, Is.EquivalentTo(new[] {3, 4}));
    }

    [Test]
    public void ParsesConstant()
    {
        // "14.9FDX完成表" → Selector.Constant(14.9) + Fs=FDX + 默认难度 [3, 4]
        var q = MustParse("14.9FDX完成表");
        Assert.That(q.Selectors.Single(), Is.InstanceOf<PlateData.Selector.Constant>());
        Assert.That(((PlateData.Selector.Constant)q.Selectors.Single()).Value, Is.EqualTo(14.9).Within(0.001));
        Assert.That(q.Threshold.Dim, Is.EqualTo(PlateData.Dimension.Fs));
        Assert.That(q.Threshold.Level, Is.EqualTo(4));
        Assert.That(q.LevelIdxes, Is.EquivalentTo(new[] {3, 4}));
    }

    // ──────────────────────────────────────────────────────────────────────
    // Multi-selector (AND 合取)
    // ──────────────────────────────────────────────────────────────────────

    [Test]
    public void MultiSelector_PlateAndLevel()
    {
        // "镜代13+AP完成表" → Plate(镜) ∩ Level("13+") + Fc=AP
        var q = MustParse("镜代13+AP完成表");
        Assert.That(q.Selectors, Has.Exactly(2).Items);
        Assert.That(q.Selectors.OfType<PlateData.Selector.Plate>().Single().Kanji, Is.EqualTo("镜"));
        Assert.That(q.Selectors.OfType<PlateData.Selector.Level>().Single().Label, Is.EqualTo("13+"));
        Assert.That(q.Threshold.DisplayName, Is.EqualTo("AP"));
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
    }

    [Test]
    public void MultiSelector_PlateAndConstant()
    {
        // "镜代14.6神完成表" → Plate(镜) ∩ Constant(14.6) + AP
        var q = MustParse("镜代14.6神完成表");
        Assert.That(q.Selectors, Has.Exactly(2).Items);
        Assert.That(q.Selectors.OfType<PlateData.Selector.Plate>().Single().Kanji, Is.EqualTo("镜"));
        Assert.That(q.Selectors.OfType<PlateData.Selector.Constant>().Single().Value, Is.EqualTo(14.6).Within(0.001));
        Assert.That(q.Threshold.DisplayName, Is.EqualTo("AP"));
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
}
