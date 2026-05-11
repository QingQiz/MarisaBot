using System.Collections.Generic;
using Marisa.Plugin.Shared.MaiMaiDx;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class MaiMaiDxPlateDataTest
{
    private static readonly IReadOnlyCollection<string> Charters =
        ["翠楼屋", "Jack", "はっぴー", "ロシェ@ペンギン", "rioN"];

    private static PlateData.Query MustParse(string raw)
    {
        var ok = PlateData.TryParse(raw, Charters, out var query, out var error);
        Assert.That(ok, Is.True, $"parse failed for '{raw}': {error?.Kind}/{error?.Detail}");
        return query!;
    }

    private static PlateData.ParseError MustFail(string raw)
    {
        var ok = PlateData.TryParse(raw, Charters, out _, out var error);
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
        Assert.That(query.Selector, Is.InstanceOf<PlateData.Selector.Plate>());
        var plate = (PlateData.Selector.Plate)query.Selector;
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
        var plate = (PlateData.Selector.Plate)query.Selector;
        Assert.That(plate.Versions, Is.EquivalentTo(expected));
    }

    [TestCase("翠楼屋将完成表",     "翠楼屋", 12)]
    [TestCase("Jack神完成表",       "Jack",   3)]    // Fc=ap
    [TestCase("rioN大将完成表",     "rioN",   13)]
    [TestCase("はっぴー理论值完成表", "はっぴー", 4)]   // Fc=app
    public void ParsesCharter(string raw, string charter, int level)
    {
        var query = MustParse(raw);
        Assert.That(query.Selector, Is.InstanceOf<PlateData.Selector.Charter>());
        var c = (PlateData.Selector.Charter)query.Selector;
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
        Assert.That(query.Selector, Is.InstanceOf<PlateData.Selector.Genre>());
        var g = (PlateData.Selector.Genre)query.Selector;
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
        Assert.That(query.Selector, Is.InstanceOf<PlateData.Selector.Charter>());
        Assert.That(((PlateData.Selector.Charter)query.Selector).Name, Is.EqualTo("翠楼屋代"));
    }

    [Test]
    public void AcceptsArbitraryCharterStringAsCharter()
    {
        // "张顺飞" 不存在 — 但因为不是代字也不是 genre alias，按规则当 charter 接受。
        // 真不真存在由 SelectChartsForQuery 里的 substring 匹配决定（找不到时 handler 报 0 首）。
        var query = MustParse("张顺飞将完成表");
        Assert.That(query.Selector, Is.InstanceOf<PlateData.Selector.Charter>());
        Assert.That(((PlateData.Selector.Charter)query.Selector).Name, Is.EqualTo("张顺飞"));
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

    [Test]
    public void RejectsUnknownThreshold()
    {
        // "真" 自己就是代字，但没阈值
        Assert.That(MustFail("真完成表").Kind, Is.EqualTo(PlateData.ErrorKind.UnknownThreshold));
    }

    // 注：以前这里有个 RejectsUnknownSelector 测试。改了 charter substring 行为后，
    // 任何非代字非 genre 的 selector 都会被当 charter 接受（实际找不到会在 handler
    // 渲染时报 0 首），所以解析层不再产生 UnknownSelector。该 ErrorKind 仍保留作 future 使用。

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

    [TestCase("真将完成表",            3)]   // 默认 MASTER
    [TestCase("真大将完成表",          3)]
    [TestCase("熊代理论值完成表",      3)]
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
        Assert.That(query.LevelIdx, Is.EqualTo(expectedLevelIdx));
    }

    [Test]
    public void DifficultySingleKanjiNotAccepted()
    {
        // 单字"红"不在 difficulty alias map（避免跟未来代字冲突，要写就写"红谱"或"EXPERT"）。
        // 新版"字段位置任意"算法下，"真代SSS+红完成表" 阈值 SSS+ 在中间也能剥，剩"真代红"
        // 走 charter substring（catch-all），LevelIdx 仍是默认 MASTER (=3)。
        var q = MustParse("真代SSS+红完成表");
        Assert.That(q.LevelIdx, Is.EqualTo(3), "single '红' must not be parsed as difficulty");
        Assert.That(q.Threshold.DisplayName, Is.EqualTo("SSS+"));
        Assert.That(q.Selector, Is.InstanceOf<PlateData.Selector.Charter>());
    }

    [TestCase("真代神白谱完成表", 4)]
    [TestCase("真代神Re:MASTER完成表", 4)]
    [TestCase("熊代将re:master完成表", 4)]
    public void RemasterDifficultyParsing(string raw, int expectedLevelIdx)
    {
        Assert.That(MustParse(raw).LevelIdx, Is.EqualTo(expectedLevelIdx));
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
        Assert.That(q.Selector, Is.InstanceOf<PlateData.Selector.Plate>(), "should resolve to Plate(真)");
        Assert.That(((PlateData.Selector.Plate)q.Selector).Kanji, Is.EqualTo("真"));
        Assert.That(q.Threshold.DisplayName, Is.EqualTo("SSS"));
        Assert.That(q.LevelIdx, Is.EqualTo(2));    // EXPERT
    }

    /// <summary>白谱（Re:MASTER）放在中间或前面都能识别。</summary>
    [TestCase("真神白谱完成表")]
    [TestCase("真白谱神完成表")]
    [TestCase("白谱真神完成表")]
    public void FieldOrderIsCommutative_PlateShinWhitePu(string raw)
    {
        var q = MustParse(raw);
        Assert.That(((PlateData.Selector.Plate)q.Selector).Kanji, Is.EqualTo("真"));
        Assert.That(q.Threshold.DisplayName, Is.EqualTo("AP"));  // 神
        Assert.That(q.LevelIdx, Is.EqualTo(4));                  // 白谱 = Re:MASTER
    }
}
