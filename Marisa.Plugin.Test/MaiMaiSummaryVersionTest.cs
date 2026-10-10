using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using Marisa.Plugin.Shared.MaiMaiDx;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class MaiMaiSummaryVersionTest
{
    private static readonly string[] Versions = ["maimai", "maimai PLUS", "maimai GreeN"];

    [TestCase("真", new[] { "maimai", "maimai PLUS" })]
    [TestCase("超", new[] { "maimai GreeN" })]
    [TestCase("maimai", new[] { "maimai" })]
    [TestCase(" MAIMAI plus ", new[] { "maimai PLUS" })]
    public void ResolvesVersionAliasesAndExactNames(string input, string[] expected)
    {
        Assert.That(Resolve(input, Versions), Is.EqualTo(expected));
    }

    [TestCase("")]
    [TestCase("不存在的版本")]
    [TestCase("雪")]
    public void RejectsQueriesWithoutAnAvailableVersion(string input)
    {
        Assert.That(Resolve(input, Versions), Is.Null);
    }

    [Test]
    public void MultiVersionAliasUsesAvailableCanonicalVersionsOnly()
    {
        Assert.That(Resolve("真", ["MAIMAI", "maimai GreeN"]), Is.EqualTo(new[] { "MAIMAI" }));
        Assert.That(Resolve("真", ["maimai GreeN"]), Is.Null);
    }

    [Test]
    public void ShinSummaryCombinesOriginalAndPlusIncludingJingleBell()
    {
        var charts = Select(Catalog(), Resolve("真", Versions)!);
        Assert.Multiple(() =>
        {
            Assert.That(charts.Select(x => x.Song.Id), Is.EquivalentTo(new[] { 17L, 70L, 80L, 10070L }));
            Assert.That(charts.All(x => x.LevelIdx == 3), Is.True);
            Assert.That(charts.Any(x => x.Song.Id == 90), Is.False);
        });
    }

    [Test]
    public void ShinSummaryKeepsRevivalSongsWhileVersionCompletionTablesExcludeThem()
    {
        var songs = Catalog();
        songs.AddRange(new[]
        {
            Song(44, "maimai PLUS", "ハッピーシンセサイザ"),
            Song(146, "maimai PLUS", "39"),
            Song(10146, "maimai でらっくす", "39")
        });
        var ids = Select(songs, Resolve("真", Versions)!).Select(x => x.Song.Id).ToArray();
        Assert.That(PlateData.TryParse("真完成表", [], [], out var query, out _), Is.True);
        var completionIds = PlateData.SelectCharts(query!, songs).Select(x => x.Song.Id).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(PlateData.IsRevivalSong(44), Is.True);
            Assert.That(PlateData.IsRevivalSong(146), Is.True);
            Assert.That(ids, Does.Contain(44L).And.Contain(146L));
            Assert.That(ids, Does.Not.Contain(10146L));
            Assert.That(completionIds, Does.Contain(17L));
            Assert.That(completionIds, Does.Not.Contain(44L).And.Not.Contain(146L));
        });
    }

    [Test]
    public void AllActiveRevivalSongsRemainInTheirVersionSummary()
    {
        var path = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..",
            "Marisa.Frontend", "public", "assets", "maimai", "SongInfo.json"));
        var data = JsonConvert.DeserializeObject<ExpandoObject[]>(File.ReadAllText(path))!;
        var catalog = data.Select(song => new MaiMaiSong(song)).ToArray();
        var revivals = catalog.Where(song => PlateData.IsRevivalSong(song.Id) && song.Constants.Count > 3).ToArray();
        Assert.That(revivals, Is.Not.Empty);

        foreach (var group in revivals.GroupBy(song => song.Version))
        {
            var selected = Select(catalog, [group.Key]);
            Assert.That(selected.Where(x => PlateData.IsRevivalSong(x.Song.Id)).Select(x => x.Song.Id),
                Is.EquivalentTo(group.Select(song => song.Id)), group.Key);
        }
    }

    [TestCase("maimai", new long[] { 17, 70 })]
    [TestCase("maimai PLUS", new long[] { 80, 10070 })]
    [TestCase("超", new long[] { 90 })]
    public void SingleVersionQueriesKeepTheirOriginalScope(string input, long[] expectedIds)
    {
        Assert.That(Select(Catalog(), Resolve(input, Versions)!).Select(x => x.Song.Id), Is.EquivalentTo(expectedIds));
    }

    [Test]
    public void VersionSummaryKeepsConstantDescendingOrder()
    {
        var songs = Catalog();
        songs.Single(x => x.Id == 80).Constants[3] = 14.8;
        var charts = Select(songs, Resolve("真", Versions)!);
        Assert.That(charts.First().Song.Id, Is.EqualTo(80));
        Assert.That(charts.Select(x => x.Constant), Is.Ordered.Descending);
    }

    [TestCase("真极完成表", false)]
    [TestCase("真神完成表", false)]
    [TestCase("真舞舞完成表", false)]
    [TestCase("真极红谱完成表", false)]
    [TestCase("真完成表", true)]
    [TestCase("真代完成表", true)]
    [TestCase("真将完成表", true)]
    [TestCase("真14神完成表", true)]
    [TestCase("真舞萌神完成表", true)]
    [TestCase("舞极完成表", true)]
    [TestCase("霸者完成表", true)]
    public void JingleBellSelectionOnlyExcludesTheRealShinPlates(string input, bool includeJingleBell)
    {
        Assert.That(PlateData.TryParse(input, [], [], out var query, out _), Is.True);
        var ids = PlateData.SelectCharts(query!, Catalog()).Select(x => x.Song.Id).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(ids.Contains(70), Is.EqualTo(includeJingleBell));
            Assert.That(ids, Does.Contain(17L));
            Assert.That(ids, Does.Contain(10070L));
        });
    }

    [Test]
    public void ShinScopeWithoutPlateConditionsKeepsJingleBell()
    {
        Assert.That(PlateData.TryParseScope("真", out var query, out _), Is.True);
        Assert.That(PlateData.SelectScopeCharts(query!, Catalog()).Select(x => x.Song.Id), Does.Contain(70L));
    }

    private static string[]? Resolve(string input, IReadOnlyList<string> versions) =>
        (string[]?)typeof(MaiMaiDx.MaiMaiDx).GetMethod("ResolveSummaryVersions", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [input, versions]);

    private static IReadOnlyList<(double Constant, int LevelIdx, MaiMaiSong Song)> Select(
        IReadOnlyList<MaiMaiSong> songs, IReadOnlyCollection<string> versions) =>
        (IReadOnlyList<(double, int, MaiMaiSong)>)typeof(MaiMaiDx.MaiMaiDx)
            .GetMethod("SelectVersionSummaryCharts", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [songs, versions])!;

    private static List<MaiMaiSong> Catalog() =>
    [
        Song(17, "maimai", "Future"), Song(70, "maimai", "ジングルベル"),
        Song(80, "maimai PLUS", "PLUS song"), Song(10070, "maimai PLUS", "ジングルベル"),
        Song(90, "maimai GreeN", "GreeN song")
    ];

    private static MaiMaiSong Song(long id, string version, string title)
    {
        var song = MaiMaiVersusCommandTest.Song(id, title);
        song.Version = version;
        return song;
    }
}
