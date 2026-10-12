using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using Marisa.Plugin.Shared.MaiMaiDx;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class MaiMaiDxPlateNameBoundaryTest
{
    private static readonly Lazy<(MaiMaiSong[] Songs, string[] Charters, string[] Artists)> Catalog = new(LoadCatalog);

    private static IEnumerable<TestCaseData> PartialNameQueries()
    {
        foreach (var name in new[] { "Luxi", "mai-Sta", "PANDORA", "舞舞10年ズ" })
        {
            yield return new TestCaseData(name + "完成表", name, "SSS", new[] { 3, 4 });
            yield return new TestCaseData(name + "神完成表", name, "AP", new[] { 3, 4 });
            yield return new TestCaseData("神" + name + "完成表", name, "AP", new[] { 3, 4 });
            yield return new TestCaseData("EXP " + name + " MST神完成表", name, "AP", new[] { 2, 3 });
            yield return new TestCaseData("神 MST " + name + "完成表", name, "AP", new[] { 3 });
        }
    }

    [TestCaseSource(nameof(PartialNameQueries))]
    public void PartialNamesRemainWholeWhenTargetsOrDifficultiesAreAdded(
        string raw, string name, string threshold, int[] indexes)
    {
        var query = MustParse(raw);
        Assert.Multiple(() =>
        {
            Assert.That(query.Selectors, Has.Count.EqualTo(1));
            Assert.That(query.Selectors.Single(), Is.EqualTo(new PlateData.Selector.Charter(name)));
            Assert.That(query.Threshold.DisplayName, Is.EqualTo(threshold));
            Assert.That(query.LevelIdxes, Is.EqualTo(indexes));
        });
        var expected = ExpectedByName(name, indexes);
        Assert.That(expected, Is.Not.Empty, "真实部分姓名必须有命中谱面作为正对照。");
        AssertSelection(query, expected);
    }

    [Test]
    public void LuxiPositiveAndWrongArtistNegativeControlsRemainDistinct()
    {
        var selected = Keys(MustParse("Luxi神完成表"));
        var expected = ExpectedByName("Luxi", [3, 4]);
        var wrongChart = AllCharts().Single(x => x.Song.Id == 11535 && x.Index == 3);
        Assert.Multiple(() =>
        {
            Assert.That(expected, Is.Not.Empty, "正对照必须确实存在 Luxi 署名。");
            Assert.That(wrongChart.Charter.Contains("Luxi", StringComparison.OrdinalIgnoreCase), Is.False);
            Assert.That(wrongChart.Song.Info.Artist.Contains("xi", StringComparison.OrdinalIgnoreCase), Is.True,
                "错误拆分 xi 曲师的负对照必须仍在真实曲库。");
            Assert.That(selected, Is.EquivalentTo(expected));
            Assert.That(selected, Does.Not.Contain((11535L, 3)), "Redemption 的曲师 XinG 0lulu 不能替代 Luxi 谱师条件。");
            Assert.That(Keys(MustParse("__plate_name_boundary_null__神完成表")), Is.Empty);
        });
    }

    [TestCase("Sta")]
    [TestCase("xi")]
    [TestCase("Ado")]
    [TestCase("pan")]
    [TestCase("からめる")]
    [TestCase("佑")]
    [TestCase("はっぴー")]
    [TestCase("三枝明那")]
    public void NamesSharedByBothSubstringDimensionsKeepTheUnionWithTargets(string name)
    {
        var expected = ExpectedByName(name, [3, 4], artistUnion: true);
        Assert.That(expected, Is.Not.Empty, "真实名称的正对照不能是空集合。");
        foreach (var raw in new[] { name + "完成表", name + "神完成表", "神" + name + "完成表" })
        {
            var query = MustParse(raw);
            Assert.That(query.Selectors.Single(), Is.EqualTo(new PlateData.Selector.CharterOrArtist(name)), raw);
            AssertSelection(query, expected);
        }
    }

    [TestCase("maistar Sta神完成表")]
    [TestCase("Sta maistar神完成表")]
    [TestCase("神 maistar Sta完成表")]
    public void AliasAndOverlappingRealNameHaveOrderIndependentIntersection(string raw)
    {
        var query = MustParse(raw);
        Assert.Multiple(() =>
        {
            Assert.That(query.Selectors, Has.Count.EqualTo(2));
            Assert.That(query.Selectors.OfType<PlateData.Selector.CharterAlias>().Single().Input, Is.EqualTo("maistar"));
            Assert.That(query.Selectors.OfType<PlateData.Selector.CharterOrArtist>().Single().Name, Is.EqualTo("Sta"));
            Assert.That(query.Threshold.DisplayName, Is.EqualTo("AP"));
        });
        // mai-Star 含 Sta，故 AND Sta 的并集条件不会缩掉这些谱面。
        var expected = ExpectedByName("mai-Star", [3, 4]);
        AssertSelection(query, expected);
        Assert.That(Keys(query), Does.Contain((80L, 3)), "曲师不是 Sta 的 City Escape 仍按谱师 substring 命中。");
        Assert.That(Keys(query), Does.Not.Contain((17L, 3)), "仅曲师维度命中 Sta 仍不满足 maistar。");
    }

    [TestCase("St Luxi神完成表")]
    [TestCase("Luxi St神完成表")]
    public void TwoPartialNamesKeepTheirDimensionsAndIntersectionInEitherOrder(string raw)
    {
        var query = MustParse(raw);
        Assert.Multiple(() =>
        {
            Assert.That(query.Selectors, Has.Count.EqualTo(2));
            Assert.That(query.Selectors.OfType<PlateData.Selector.Charter>().Single().Name, Is.EqualTo("Luxi"));
            Assert.That(query.Selectors.OfType<PlateData.Selector.CharterOrArtist>().Single().Name, Is.EqualTo("St"));
            Assert.That(query.Threshold.DisplayName, Is.EqualTo("AP"));
            Assert.That(query.LevelIdxes, Is.EqualTo(new[] { 3, 4 }));
        });
        var expected = ExpectedByName("Luxi", [3, 4])
            .Intersect(ExpectedByName("St", [3, 4], artistUnion: true)).ToHashSet();
        AssertSelection(query, expected);
        Assert.That(Keys(query), Does.Contain((11618L, 3)), "Energizing Flame 同时满足 Luxizhel 谱师与 St 曲师片段。");
        Assert.That(Keys(query), Does.Contain((11676L, 3)), "Lunatic Vibes 同时满足两个独立姓名条件。");
        Assert.That(AllCharts().Any(x => x.Song.Id == 11535 && x.Index == 3), Is.True);
        Assert.That(Keys(query), Does.Not.Contain((11535L, 3)), "Redemption 不满足 Luxi 谱师条件。");
    }

    [TestCase("EXP翠楼屋MST完成表")]
    [TestCase("EXP 翠楼屋 MST完成表")]
    [TestCase("红谱翠楼屋紫谱完成表")]
    public void RemovingANamePreservesBothAdjacentDifficultyTokens(string raw)
    {
        var query = MustParse(raw);
        Assert.Multiple(() =>
        {
            Assert.That(query.Selectors.Single(), Is.EqualTo(new PlateData.Selector.Charter("翠楼屋")));
            Assert.That(query.LevelIdxes, Is.EqualTo(new[] { 2, 3 }));
        });
        AssertSelection(query, ExpectedByName("翠楼屋", [2, 3]));
        Assert.That(Keys(query), Does.Contain((1235L, 2)));
        Assert.That(Keys(query), Does.Not.Contain((1235L, 0)));
    }

    [TestCase("EXP神MST真完成表")]
    [TestCase("EXP 神 MST 真完成表")]
    public void RemovingATargetPreservesBothAdjacentDifficultyTokens(string raw)
    {
        var query = MustParse(raw);
        Assert.Multiple(() =>
        {
            Assert.That(query.Selectors.Single(), Is.InstanceOf<PlateData.Selector.Plate>());
            Assert.That(((PlateData.Selector.Plate)query.Selectors.Single()).Kanji, Is.EqualTo("真"));
            Assert.That(query.LevelIdxes, Is.EqualTo(new[] { 2, 3 }));
            Assert.That(query.Threshold.DisplayName, Is.EqualTo("AP"));
        });
        var expected = AllCharts().Where(x => x.Index is 2 or 3 && x.Song.Info.From is "maimai" or "maimai PLUS")
            .Where(x => x.Song.Id is not (44 or 70 or 146)) // 真牌排除复活曲及 SD ジングルベル。
            .Select(x => (x.Song.Id, x.Index)).ToHashSet();
        AssertSelection(query, expected);
        Assert.That(Keys(query), Does.Contain((8L, 2)));
        Assert.That(Keys(query), Does.Contain((8L, 3)));
        Assert.That(Keys(query), Does.Not.Contain((70L, 3)));
    }

    [TestCase("真1东方3完成表")]
    [TestCase("真1 东方 3完成表")]
    [TestCase("真1红谱3完成表")]
    [TestCase("真1 红谱 3完成表")]
    public void RemovingFieldsCannotJoinConflictingLevelsIntoThirteen(string raw)
    {
        var (_, charters, artists) = Catalog.Value;
        var ok = PlateData.TryParse(raw, charters, artists, out _, out var error);
        Assert.That(ok, Is.False, raw);
        Assert.That(error?.Kind, Is.EqualTo(PlateData.ErrorKind.ConflictingSelector), raw);
    }

    [TestCase("真13.东方5完成表")]
    [TestCase("真13. 东方 5完成表")]
    public void RemovingAGenreCannotInventADecimalConstant(string raw)
    {
        var (_, charters, artists) = Catalog.Value;
        var ok = PlateData.TryParse(raw, charters, artists, out var query, out _);
        if (ok)
            Assert.That(query!.Selectors.OfType<PlateData.Selector.Constant>(), Is.Empty, raw);
    }

    [TestCase("真EX神PERT完成表")]
    [TestCase("真EX 神 PERT完成表")]
    public void RemovingATargetCannotInventAnExpertDifficulty(string raw)
    {
        var (_, charters, artists) = Catalog.Value;
        var ok = PlateData.TryParse(raw, charters, artists, out var query, out _);
        if (ok)
        {
            Assert.That(query!.LevelIdxes, Is.EqualTo(new[] { 3 }), raw);
            Assert.That(PlateData.SelectCharts(query, Catalog.Value.Songs), Is.Empty, raw);
        }
    }

    [TestCase("7.3星神完成表", "7.3", "星", "maimai でらっくす UNiVERSE", 1020L, 17L)]
    [TestCase("7.3 星神完成表", "7.3", "星", "maimai でらっくす UNiVERSE", 1020L, 17L)]
    [TestCase("紫谱面-100号神完成表", "谱面-100号", "紫", "maimai MURASAKi", 573L, 17L)]
    [TestCase("紫谱面100号神完成表", "谱面100号", "紫", "maimai MURASAKi", 573L, 17L)]
    [TestCase("紫 谱面-100号神完成表", "谱面-100号", "紫", "maimai MURASAKi", 573L, 17L)]
    [TestCase("白谱面-100号神完成表", "谱面-100号", "白", "maimai MiLK", 689L, 24L)]
    [TestCase("白 谱面-100号神完成表", "谱面-100号", "白", "maimai MiLK", 689L, 24L)]
    public void AnAliasCannotBeConsumedAcrossATargetOrDifficultyBoundary(
        string raw, string alias, string kanji, string version, long positiveId, long negativeId)
    {
        var query = MustParse(raw);
        Assert.Multiple(() =>
        {
            Assert.That(query.Selectors, Has.Count.EqualTo(2));
            Assert.That(query.Selectors.OfType<PlateData.Selector.CharterAlias>().Single().Input, Is.EqualTo(alias));
            Assert.That(query.Selectors.OfType<PlateData.Selector.Plate>().Single().Kanji, Is.EqualTo(kanji));
            Assert.That(query.Threshold.DisplayName, Is.EqualTo("AP"));
            Assert.That(query.LevelIdxes, Is.EqualTo(new[] { 3 }));
        });
        var names = PlateData.CharterAliasMap[alias];
        var expected = AllCharts().Where(x => x.Index == 3 && x.Song.Info.From == version)
            .Where(x => x.Song.Id is not (11213 or 11253 or 11267)) // 该 UNiVERSE 曲库中的复活曲。
            .Where(x => names.Any(name => x.Charter.Contains(name, StringComparison.OrdinalIgnoreCase)))
            .Select(x => (x.Song.Id, x.Index)).ToHashSet();
        AssertSelection(query, expected);
        Assert.That(Keys(query), Does.Contain((positiveId, 3)));
        Assert.That(AllCharts().Any(x => x.Song.Id == negativeId && x.Index == 3), Is.True,
            "排除的真实负对照必须存在，不能以缺失谱面代替正确排除。");
        Assert.That(Keys(query), Does.Not.Contain((negativeId, 3)));
    }

    [TestCase("-ZONE- 紫谱完成表", "-ZONE-", 3)]
    [TestCase("紫谱 -ZONE-完成表", "-ZONE-", 3)]
    [TestCase("-ZONE-紫谱完成表", "-ZONE-", 3)]
    [TestCase("A- EXP完成表", "A-", 2)]
    [TestCase("EXP A-完成表", "A-", 2)]
    public void HyphensInsidePartialNamesRemainNamesBesideDifficulties(string raw, string name, int index)
    {
        var query = MustParse(raw);
        Assert.That(query.Selectors, Has.Count.EqualTo(1));
        Assert.That(query.Selectors.Single().Display, Is.EqualTo(name));
        Assert.That(query.LevelIdxes, Is.EqualTo(new[] { index }));
        AssertSelection(query, ExpectedByName(name, [index], artistUnion: name == "A-"));
        Assert.That(Keys(query), Is.Not.Empty);
    }

    [TestCase("EXP A- MST完成表", "A-")]
    [TestCase("EXP -ZONE- MST完成表", "-ZONE-")]
    public void PartialNamesWithHyphensRemainWholeBetweenTwoDifficulties(string raw, string name)
    {
        var query = MustParse(raw);
        Assert.Multiple(() =>
        {
            Assert.That(query.Selectors.Single(), name == "A-"
                ? Is.EqualTo(new PlateData.Selector.Artist(name))
                : Is.EqualTo(new PlateData.Selector.Charter(name)));
            Assert.That(query.LevelIdxes, Is.EqualTo(new[] { 2, 3 }));
        });
        var expected = ExpectedByName(name, [2, 3], artistUnion: name == "A-");
        Assert.That(expected, Is.Not.Empty);
        AssertSelection(query, expected);
    }

    [TestCase("定数14.5曲师", "定数14.5神完成表", 14.5)]
    [TestCase("X定数14.5曲师", "定数14.5神完成表", 14.5)]
    [TestCase("定数7.3曲师", "定数7.3神完成表", 7.3)]
    [TestCase("X定数7.3曲师", "定数7.3神完成表", 7.3)]
    public void ExplicitConstantsCannotBecomePartialArtistNames(string knownArtist, string raw, double value)
    {
        // 定数字段优先于姓名的 substring；7.3 同时也有谱师别名，必须通过显式前缀消歧。
        var ok = PlateData.TryParse(raw, [], [knownArtist], out var query, out var error);
        Assert.That(ok, Is.True, $"{raw}: {error?.Kind}/{error?.Detail}");
        Assert.Multiple(() =>
        {
            Assert.That(query!.Selectors.Single(), Is.EqualTo(new PlateData.Selector.Constant(value)));
            Assert.That(query.Threshold.DisplayName, Is.EqualTo("AP"));
            Assert.That(query.LevelIdxes, Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
        });
        var expected = AllCharts().Where(x => Math.Abs(x.Song.Constants[x.Index] - value) < 0.05)
            .Select(x => (x.Song.Id, x.Index)).ToHashSet();
        Assert.That(expected, Is.Not.Empty, "真实定数必须有命中谱面作为正对照。");
        AssertSelection(query!, expected);
    }

    [TestCase("真MST-EXP完成表")]
    [TestCase("真红谱//紫谱完成表")]
    public void RealMalformedDifficultyConnectorsStillFail(string raw)
    {
        var (_, charters, artists) = Catalog.Value;
        var ok = PlateData.TryParse(raw, charters, artists, out _, out var error);
        Assert.That(ok, Is.False, raw);
        Assert.That(error?.Kind, Is.EqualTo(PlateData.ErrorKind.InvalidDifficulty), raw);
    }

    private static PlateData.Query MustParse(string raw)
    {
        var (_, charters, artists) = Catalog.Value;
        var ok = PlateData.TryParse(raw, charters, artists, out var query, out var error);
        Assert.That(ok, Is.True, $"{raw}: {error?.Kind}/{error?.Detail}");
        return query!;
    }

    private static HashSet<(long Id, int Index)> Keys(PlateData.Query query) =>
        PlateData.SelectCharts(query, Catalog.Value.Songs).Select(x => (x.Song.Id, x.LevelIdx)).ToHashSet();

    private static void AssertSelection(PlateData.Query query, HashSet<(long Id, int Index)> expected) =>
        Assert.That(Keys(query), Is.EquivalentTo(expected));

    private static IEnumerable<(MaiMaiSong Song, string Charter, int Index)> AllCharts() =>
        Catalog.Value.Songs.SelectMany(song => song.Charters.Select((charter, index) => (song, charter, index)));

    private static HashSet<(long Id, int Index)> ExpectedByName(string name, int[] indexes, bool artistUnion = false)
    {
        // 仅复用身份定义数据；从原曲库字段独立筛选，不调用解析器或 MatchCharter/MatchesSelector。
        var (names, excludes) = PlateData.CharterIdentityMap.TryGetValue(name, out var identity)
            ? identity : (new[] { name }, Array.Empty<string>());
        return AllCharts().Where(x => indexes.Contains(x.Index))
            .Where(x => (names.Any(n => x.Charter.Contains(n, StringComparison.OrdinalIgnoreCase)) &&
                         !excludes.Any(n => x.Charter.Contains(n, StringComparison.OrdinalIgnoreCase))) ||
                        (artistUnion && x.Song.Info.Artist.Contains(name, StringComparison.OrdinalIgnoreCase)))
            .Select(x => (x.Song.Id, x.Index)).ToHashSet();
    }

    private static (MaiMaiSong[] Songs, string[] Charters, string[] Artists) LoadCatalog()
    {
        var path = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..",
            "Marisa.Frontend", "public", "assets", "maimai", "SongInfo.json"));
        var songs = JsonConvert.DeserializeObject<ExpandoObject[]>(File.ReadAllText(path))!
            .Select(song => new MaiMaiSong(song)).ToArray();
        var charters = songs.SelectMany(song => song.Charters).Where(name => !string.IsNullOrWhiteSpace(name) && name != "N/A")
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var artists = songs.Select(song => song.Info.Artist).Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return (songs, charters, artists);
    }
}
