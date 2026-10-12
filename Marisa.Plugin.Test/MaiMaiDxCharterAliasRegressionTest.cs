using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using Marisa.Plugin.Shared.MaiMaiDx;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class MaiMaiDxCharterAliasRegressionTest
{
    // Sta 是实际曲库中的曲师；只用谱师名字构造 fixture 会漏掉 maistar 被切成 sta + mair 的回归。
    private static readonly string[] Charters = ["mai-Star", "Jack vs mai-Star", "Jack", "Revo@LC", "7.3Hz", "隅田川星人"];
    private static readonly string[] Artists = ["Sta", "Other artist"];
    private static readonly Lazy<(MaiMaiSong[] Songs, string[] Charters, string[] Artists)> Catalog = new(LoadCatalog);

    [TestCase("maistar神完成表", "AP", new[] { 3, 4 }, null)]
    [TestCase("MAISTAR神完成表", "AP", new[] { 3, 4 }, null)]
    [TestCase("mAiStAr神完成表", "AP", new[] { 3, 4 }, null)]
    [TestCase("maistar完成表", "SSS", new[] { 3, 4 }, null)]
    [TestCase("神maistar完成表", "AP", new[] { 3, 4 }, null)]
    [TestCase("MST maistar神完成表", "AP", new[] { 3 }, null)]
    [TestCase("maistar MST神完成表", "AP", new[] { 3 }, null)]
    [TestCase("maistar14神完成表", "AP", new[] { 0, 1, 2, 3, 4 }, "14")]
    [TestCase("14maistar神完成表", "AP", new[] { 0, 1, 2, 3, 4 }, "14")]
    [TestCase("MST maistar14神完成表", "AP", new[] { 3 }, "14")]
    [TestCase("神14maistar MST完成表", "AP", new[] { 3 }, "14")]
    [TestCase("maistar EXP/Re:MASTER 13+ SSS+完成表", "SSS+", new[] { 2, 4 }, "13+")]
    public void MaistarRemainsAnAliasWhenArtistStaIsKnown(
        string raw, string threshold, int[] levelIdxes, string? levelLabel)
    {
        var query = MustParse(raw, Charters, Artists);
        AssertAliasQuery(query, "maistar", threshold, levelIdxes, levelLabel, null);
    }

    [Test]
    public void AChineseAliasContainingAShortArtistNameAlsoRemainsWhole()
    {
        var query = MustParse("华火职人神完成表", ["華火職人", "Jack"], ["火", "Other artist"]);
        AssertAliasQuery(query, "华火职人", "AP", [3, 4], null, null);
        var songs = new[] { CreateSong(1, "華火職人", "Other artist"), CreateSong(2, "Jack", "火") };
        Assert.That(PlateData.SelectCharts(query, songs).Select(x => (x.Song.Id, x.LevelIdx)),
            Is.EquivalentTo(new[] { (1L, 3), (1L, 4) }));
    }

    [TestCase("Sta神完成表", "Sta", new[] { 3, 4 }, null)]
    [TestCase("sta14MST神完成表", "sta", new[] { 3 }, "14")]
    [TestCase("MSTSta14神完成表", "Sta", new[] { 3 }, "14")]
    public void StandaloneStaStillParsesAndSelectsItsOwnSongs(
        string raw, string artist, int[] levelIdxes, string? levelLabel)
    {
        var query = MustParse(raw, Charters, Artists);
        Assert.Multiple(() =>
        {
            Assert.That(query.Selectors.OfType<PlateData.Selector.Artist>().Select(x => x.Name), Is.EqualTo(new[] { artist }));
            Assert.That(query.Selectors, Has.None.InstanceOf<PlateData.Selector.CharterAlias>());
            Assert.That(query.Threshold.DisplayName, Is.EqualTo("AP"));
            Assert.That(query.LevelIdxes, Is.EqualTo(levelIdxes));
        });
        if (levelLabel is not null)
            Assert.That(query.Selectors.OfType<PlateData.Selector.Level>().Single().Label, Is.EqualTo(levelLabel));

        var songs = new[] { CreateSong(1, "mai-Star", "Other artist"), CreateSong(2, "Jack", "Sta") };
        Assert.That(PlateData.SelectCharts(query, songs).Select(x => x.Song.Id).Distinct(), Is.EqualTo(new[] { 2L }));
    }

    [TestCase("Revo", "Revo")]
    [TestCase("REVO", "Revo")]
    [TestCase("maistar", "maistar")]
    public void AnAliasAlsoPresentInKnownNamesKeepsAliasPriority(string knownName, string alias)
    {
        // 真名与别名同长同位时仍按别名；否则大小写或新增曲师会改变同一条命令的含义。
        var query = MustParse($"{knownName}神完成表", [knownName, .. Charters], [knownName, .. Artists]);
        AssertAliasQuery(query, alias, "AP", [3, 4], null, null);
    }

    [TestCase("Revo@LC", "Revo")]
    [TestCase("7.3Hz", "7.3")]
    [TestCase("隅田川星人", "隅田川")]
    public void ALongerRealCharterNameContainingAnAliasRemainsWhole(string charter, string alias)
    {
        foreach (var raw in new[] { $"{charter}神完成表", $"MST{charter}14神完成表", $"神14{charter}MST完成表" })
        {
            var query = MustParse(raw, Charters, Artists);
            Assert.Multiple(() =>
            {
                Assert.That(query.Selectors.OfType<PlateData.Selector.Charter>().Select(x => x.Name),
                    Is.EqualTo(new[] { charter }), raw);
                Assert.That(query.Selectors, Has.None.InstanceOf<PlateData.Selector.CharterAlias>(), $"{raw}; alias={alias}");
                Assert.That(query.Threshold.DisplayName, Is.EqualTo("AP"), raw);
            });
        }
    }

    [Test]
    public void MaistarSelectionIncludesSoloAndCollaborationChartsButNotArtistOnlyMatches()
    {
        var query = MustParse("maistar神完成表", Charters, Artists);
        var songs = new[]
        {
            CreateSong(1, "mai-Star", "Other artist"),
            CreateSong(2, "Jack vs mai-Star", "Other artist"),
            CreateSong(3, "Jack", "Sta"),
            CreateSong(4, "Jack", "Other artist")
        };

        Assert.That(PlateData.SelectCharts(query, songs).Select(x => (x.Song.Id, x.LevelIdx)), Is.EquivalentTo(new[]
        {
            (1L, 3), (1L, 4), (2L, 3), (2L, 4)
        }));
    }

    [Test]
    public void RealCatalogMaistarSelectionRetainsCollaborationsAndExcludesStaOnlyCharts()
    {
        var (songs, charters, artists) = Catalog.Value;
        Assert.That(artists, Does.Contain("Sta"), "实际触发回归的曲师必须存在于测试曲库。");
        var query = MustParse("maistar神完成表", charters, artists);
        AssertAliasQuery(query, "maistar", "AP", [3, 4], null, null);
        var selected = PlateData.SelectCharts(query, songs);
        var selectedKeys = selected.Select(x => (x.Song.Id, x.LevelIdx)).ToHashSet();
        var independentExpected = songs.SelectMany(song => song.Charters.Select((charter, index) => (Song: song, Charter: charter, LevelIdx: index)))
            .Where(x => x.LevelIdx is 3 or 4 && x.Charter.Contains("mai-Star", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var staOnly = songs.SelectMany(song => song.Charters.Select((charter, index) => (Song: song, Charter: charter, LevelIdx: index)))
            .Where(x => x.LevelIdx is 3 or 4 && x.Song.Info.Artist.Equals("Sta", StringComparison.OrdinalIgnoreCase) &&
                        !x.Charter.Contains("mai-Star", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(independentExpected.Any(x => x.Charter.Equals("mai-Star", StringComparison.OrdinalIgnoreCase)), Is.True);
            Assert.That(independentExpected.Any(x => !x.Charter.Equals("mai-Star", StringComparison.OrdinalIgnoreCase)), Is.True,
                "实际曲库应保留 mai-Star 的合作名义。");
            Assert.That(staOnly, Is.Not.Empty, "负对照必须包含只有曲师 Sta 命中的真实谱面。");
            Assert.That(selectedKeys, Is.EquivalentTo(independentExpected.Select(x => (x.Song.Id, x.LevelIdx))));
            Assert.That(staOnly.Any(x => selectedKeys.Contains((x.Song.Id, x.LevelIdx))), Is.False);
        });
    }

    private static IEnumerable<TestCaseData> RealAliasQueries()
    {
        foreach (var alias in PlateData.CharterAliasMap.Keys)
        {
            yield return Case($"{alias}完成表", "SSS", [3, 4]);
            yield return Case($"神{alias}完成表", "AP", [3, 4]);
            yield return Case($"{alias}神完成表", "AP", [3, 4]);
            yield return Case($"MST {alias}完成表", "SSS", [3]);
            yield return Case($"{alias} MST完成表", "SSS", [3]);
            yield return Case($"MST {alias} 14 神完成表", "AP", [3], "14");
            yield return Case($"神 14 {alias} MST完成表", "AP", [3], "14");
            yield return Case($"{alias} EXP/Re:MASTER 13+ SSS+完成表", "SSS+", [2, 4], "13+");
            yield return Case($"MST {alias} 定数14.5 神完成表", "AP", [3], constant: 14.5);
            foreach (var input in new[] { alias.ToUpperInvariant(), alias.ToLowerInvariant() }.Distinct(StringComparer.Ordinal))
            {
                if (input == alias) continue;
                yield return Case($"{input}神完成表", "AP", [3, 4]);
                yield return Case($"神 14 {input} MST完成表", "AP", [3], "14");
            }

            TestCaseData Case(string raw, string threshold, int[] indexes, string? label = null, double? constant = null) =>
                new TestCaseData(raw, alias, threshold, indexes, label, constant).SetName($"RealAlias_{raw}");
        }
    }

    [TestCaseSource(nameof(RealAliasQueries))]
    public void EveryAliasParsesAndSelectsRealChartsWithExternalFields(
        string raw, string alias, string threshold, int[] levelIdxes, string? levelLabel, double? constant)
    {
        var (songs, charters, artists) = Catalog.Value;
        var query = MustParse(raw, charters, artists);
        AssertAliasQuery(query, alias, threshold, levelIdxes, levelLabel, constant);

        // 直接用 JSON 中的署名、等级和定数构造预期，不经过 MatchesSelector/MatchCharter。
        var names = PlateData.CharterAliasMap[alias];
        var excludes = PlateData.CharterAliasExclude.GetValueOrDefault(alias, []);
        var expected = songs.SelectMany(song => song.Charters.Select((charter, index) => (Song: song, Charter: charter, LevelIdx: index)))
            .Where(x => levelIdxes.Contains(x.LevelIdx))
            .Where(x => names.Any(name => x.Charter.Contains(name, StringComparison.OrdinalIgnoreCase)))
            .Where(x => !excludes.Any(exclude => x.Charter.Contains(exclude, StringComparison.OrdinalIgnoreCase)))
            .Where(x => levelLabel is null || x.Song.Levels[x.LevelIdx] == levelLabel)
            .Where(x => constant is null || Math.Abs(x.Song.Constants[x.LevelIdx] - constant.Value) < 0.05)
            .Select(x => (x.Song.Id, x.LevelIdx));
        Assert.That(PlateData.SelectCharts(query, songs).Select(x => (x.Song.Id, x.LevelIdx)), Is.EquivalentTo(expected), raw);
    }

    private static PlateData.Query MustParse(string raw, IReadOnlyCollection<string> charters, IReadOnlyCollection<string> artists)
    {
        var ok = PlateData.TryParse(raw, charters, artists, out var query, out var error);
        Assert.That(ok, Is.True, $"parse failed for '{raw}': {error?.Kind}/{error?.Detail}");
        return query!;
    }

    private static void AssertAliasQuery(
        PlateData.Query query, string alias, string threshold, int[] levelIdxes, string? levelLabel, double? constant)
    {
        var aliasSelectors = query.Selectors.OfType<PlateData.Selector.CharterAlias>().ToArray();
        Assert.That(aliasSelectors, Has.Length.EqualTo(1));
        var selector = aliasSelectors.Single();
        Assert.Multiple(() =>
        {
            Assert.That(selector.Input, Is.EqualTo(alias).IgnoreCase);
            Assert.That(selector.Names, Is.EqualTo(PlateData.CharterAliasMap[alias]));
            Assert.That(selector.Exclude, Is.EqualTo(PlateData.CharterAliasExclude.GetValueOrDefault(alias, [])));
            Assert.That(query.Selectors.Count, Is.EqualTo(1 + (levelLabel is null ? 0 : 1) + (constant is null ? 0 : 1)));
            Assert.That(query.Threshold.DisplayName, Is.EqualTo(threshold));
            Assert.That(query.LevelIdxes, Is.EqualTo(levelIdxes));
        });
        if (levelLabel is not null)
            Assert.That(query.Selectors.OfType<PlateData.Selector.Level>().Single().Label, Is.EqualTo(levelLabel));
        if (constant is not null)
            Assert.That(query.Selectors.OfType<PlateData.Selector.Constant>().Single().Value, Is.EqualTo(constant.Value).Within(0.001));
    }

    private static (MaiMaiSong[] Songs, string[] Charters, string[] Artists) LoadCatalog()
    {
        var path = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..",
            "Marisa.Frontend", "public", "assets", "maimai", "SongInfo.json"));
        var data = JsonConvert.DeserializeObject<ExpandoObject[]>(File.ReadAllText(path))!;
        var songs = data.Select(song => new MaiMaiSong(song)).ToArray();
        var charters = songs.SelectMany(song => song.Charters).Where(name => !string.IsNullOrWhiteSpace(name) && name != "N/A")
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var artists = songs.Select(song => song.Info.Artist).Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return (songs, charters, artists);
    }

    private static MaiMaiSong CreateSong(long id, string charter, string artist)
    {
        dynamic song = new ExpandoObject();
        song.id = id.ToString();
        song.title = $"song-{id}";
        song.type = "SD";
        dynamic basicInfo = new ExpandoObject();
        basicInfo.title = song.title;
        basicInfo.artist = artist;
        basicInfo.genre = "genre";
        basicInfo.bpm = 120;
        basicInfo.release_date = "2024-01-01";
        basicInfo.from = "maimai";
        basicInfo.is_new = false;
        song.basic_info = basicInfo;
        song.ds = new[] { 3.0, 7.0, 10.0, 14.0, 14.5 };
        song.level = new[] { "3", "7", "10", "14", "14+" };
        song.charts = Enumerable.Range(0, 5).Select(_ =>
        {
            dynamic chart = new ExpandoObject();
            chart.notes = new long[] { 100, 10, 10, 0 };
            chart.charter = charter;
            return chart;
        }).ToArray();
        return new MaiMaiSong(song);
    }
}
