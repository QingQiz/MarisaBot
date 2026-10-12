using System.Linq;
using System.Dynamic;
using System.IO;
using System.Text;
using Marisa.Plugin.Shared.Chunithm;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class ChunithmVersusScopeTest
{
    private static readonly ChunithmSong[] Catalog =
    [
        ChunithmVersusModelTest.Song(1),
        ChunithmVersusModelTest.Song(2, version: "CHUNITHM X-VERSE", genre: "東方Project"),
        ChunithmVersusModelTest.Song(3, version: "CHUNITHM LUMINOUS", genre: "niconico")
    ];

    [TestCase("VERSE", 1, 3)]
    [TestCase("CHUNITHM VERSE", 1, 3)]
    [TestCase("X-VERSE 东方 ULT", 2, 4)]
    [TestCase("XVERSE 14+ ULTIMA", 2, 4)]
    [TestCase("术力口 14 MASTER", 3, 3)]
    public void VersionGenreAndDifficultyIntersect(string input, long id, int level)
    {
        Assert.That(ChunithmVersusScope.TryParse(input, Catalog, out var query, out var error), Is.True, error);
        var charts = ChunithmVersusScope.SelectCharts(query!, Catalog);
        Assert.That(charts.Select(chart => (chart.Id, chart.LevelIndex)), Is.EqualTo(new[] { (id, level) }));
    }

    [TestCase("14", 3)]
    [TestCase("14+", 3)]
    [TestCase("14-14+", 6)]
    [TestCase("14.0-14.5", 6)]
    public void NumericScopeDefaultsToAllStandardDifficulties(string input, int count)
    {
        Assert.That(ChunithmVersusScope.TryParse(input, Catalog, out var query, out var error), Is.True, error);
        Assert.That(ChunithmVersusScope.SelectCharts(query!, Catalog), Has.Count.EqualTo(count));
        Assert.That(query!.LevelIndexes, Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
    }

    [TestCase("")]
    [TestCase("WORLD'S END")]
    [TestCase("VERSE WE")]
    [TestCase("彩代14+")]
    [TestCase("14.9-14.0")]
    [TestCase("99")]
    [TestCase("MASTERPIECE")]
    public void InvalidAndMaiSpecificScopeIsRejected(string input) =>
        Assert.That(ChunithmVersusScope.TryParse(input, Catalog, out _, out _), Is.False);

    [Test]
    public void VersionAndConstantScopeExposeRenderLabels()
    {
        Assert.That(ChunithmVersusScope.TryParse("X-VERSE 14.5", Catalog, out var query, out _), Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(query!.Version, Is.EqualTo("CHUNITHM X-VERSE"));
            Assert.That(query.SortLabel, Is.EqualTo("歌曲 ID 升序"));
        });
    }

    [TestCase("CHUNITHM XVERSE", "X-VERSE")]
    [TestCase("CHUNITHM X-VERSE", "XVERSE")]
    [TestCase("CHUNITHM XVERSEX", "X-VERSE-X")]
    [TestCase("CHUNITHM X-VERSE-X", "XVERSEX")]
    public void VersionHyphensAreNormalizedInBothDirections(string catalogVersion, string input)
    {
        var catalog = new[] { ChunithmVersusModelTest.Song(1, version: catalogVersion) };
        Assert.That(ChunithmVersusScope.TryParse(input + " 14-15", catalog, out var query, out var error), Is.True, error);
        Assert.Multiple(() =>
        {
            Assert.That(query!.Version, Is.EqualTo(catalogVersion));
            Assert.That(query.MinimumLevel, Is.EqualTo("14"));
            Assert.That(query.MaximumLevel, Is.EqualTo("15"));
            Assert.That(ChunithmVersusScope.SelectCharts(query, catalog).Select(chart => chart.LevelIndex), Is.EqualTo(new[] { 4, 3 }));
            Assert.That(ChunithmVersusScope.TryParse("X-VERSE-NONEXISTENT 14-15", catalog, out _, out _), Is.False);
        });
    }

    [TestCase("东方", "東方Project")]
    [TestCase("击中", "ゲキマイ")]
    [TestCase("彩绿", "イロドリミドリ")]
    public void ActualUtf8CatalogGenreAliasesSelectStandardCharts(string alias, string genre)
    {
        var catalog = ActualCatalog();
        Assert.That(ChunithmVersusScope.TryParse(alias, catalog, out var query, out var error), Is.True, error);
        var selected = ChunithmVersusScope.SelectCharts(query!, catalog);
        Assert.That(selected, Is.Not.Empty);
        Assert.That(selected.All(chart => chart.Song.Genre == genre && chart.LevelIndex == 3), Is.True);
    }

    [Test]
    public void ActualLevelSixteenStandardChartIsSelectableButSixteenPlusIsRejected()
    {
        var catalog = ActualCatalog();
        Assert.That(ChunithmVersusScope.TryParse("16", catalog, out var query, out var error), Is.True, error);
        var charts = ChunithmVersusScope.SelectCharts(query!, catalog);
        Assert.Multiple(() =>
        {
            Assert.That(charts.Any(chart => chart.Id == 2999 && chart.LevelIndex == 4 && chart.Level == "16" && chart.Constant == 16), Is.True);
            Assert.That(ChunithmVersusScope.TryParse("16+", catalog, out _, out _), Is.False);
        });
    }

    private static ChunithmSong[] ActualCatalog()
    {
        var path = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../..", "Marisa.Frontend/public/assets/chunithm/SongInfo.json"));
        return JsonConvert.DeserializeObject<ExpandoObject[]>(File.ReadAllText(path, Encoding.UTF8))!
            .Select(data => new ChunithmSong(data)).ToArray();
    }
}
