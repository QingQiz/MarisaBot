using System;
using System.Linq;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Marisa.BotDriver.DI.Message;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Entity.MessageSender;
using Marisa.Plugin.Shared.Chunithm;
using Marisa.Plugin.Shared.Interface;
using Marisa.Plugin.Shared.Util.SongDb;
using Marisa.Plugin.Shared.Versus;
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

    private static readonly (string Version, string Alias)[] OfficialVersions =
    [
        ("CHUNITHM", "CHUNITHM"), ("CHUNITHM PLUS", "PLUS"),
        ("CHUNITHM AIR", "AIR"), ("CHUNITHM AIR PLUS", "AIR PLUS"),
        ("CHUNITHM STAR", "STAR"), ("CHUNITHM STAR PLUS", "STAR PLUS"),
        ("CHUNITHM AMAZON", "AMAZON"), ("CHUNITHM AMAZON PLUS", "AMAZON PLUS"),
        ("CHUNITHM CRYSTAL", "CRYSTAL"), ("CHUNITHM CRYSTAL PLUS", "CRYSTAL PLUS"),
        ("CHUNITHM PARADISE", "PARADISE"), ("CHUNITHM PARADISE LOST", "PARADISE LOST"),
        ("CHUNITHM NEW!!", "NEW"), ("CHUNITHM NEW PLUS!!", "NEW PLUS"),
        ("CHUNITHM SUN", "SUN"), ("CHUNITHM SUN PLUS", "SUN PLUS"),
        ("CHUNITHM LUMINOUS", "LUMINOUS"), ("CHUNITHM LUMINOUS PLUS", "LUMINOUS PLUS"),
        ("CHUNITHM VERSE", "VERSE"), ("CHUNITHM XVERSE", "XVERSE"),
        ("CHUNITHM XVERSEX", "XVERSEX")
    ];

    private static readonly ChunithmSong[] VersionCatalog = OfficialVersions
        .Select((entry, index) => ChunithmVersusModelTest.Song(10_000 + index, version: entry.Version)).ToArray();

    private static IEnumerable<TestCaseData> VersionAliasCases() => OfficialVersions
        .SelectMany(entry => new[] { entry.Alias, entry.Version }.Distinct()
            .Select(alias => new TestCaseData(alias + "14", entry.Version)));

    [TestCaseSource(nameof(VersionAliasCases))]
    [TestCase("NEW!!14", "CHUNITHM NEW!!")]
    [TestCase("new!14", "CHUNITHM NEW!!")]
    [TestCase("NEW！！！14", "CHUNITHM NEW!!")]
    [TestCase("  chunithm   new!!14  ", "CHUNITHM NEW!!")]
    [TestCase("new\tplus14", "CHUNITHM NEW PLUS!!")]
    [TestCase("NEW PLUS!!14", "CHUNITHM NEW PLUS!!")]
    [TestCase("NEW+14", "CHUNITHM NEW PLUS!!")]
    [TestCase("NEW ＋14", "CHUNITHM NEW PLUS!!")]
    [TestCase("CHUNITHM NEW+14", "CHUNITHM NEW PLUS!!")]
    [TestCase("SUN+14", "CHUNITHM SUN PLUS")]
    [TestCase("SUN ＋14", "CHUNITHM SUN PLUS")]
    [TestCase("AIR+14", "CHUNITHM AIR PLUS")]
    [TestCase("STAR+14", "CHUNITHM STAR PLUS")]
    [TestCase("AMAZON+14", "CHUNITHM AMAZON PLUS")]
    [TestCase("CRYSTAL+14", "CHUNITHM CRYSTAL PLUS")]
    [TestCase("LUMINOUS+14", "CHUNITHM LUMINOUS PLUS")]
    public void VersionAliasesSelectOnlyTheirCanonicalCatalogVersion(string input, string version)
    {
        Assert.That(ChunithmVersusScope.TryParse(input, VersionCatalog, out var query, out var error), Is.True, error);
        Assert.Multiple(() =>
        {
            Assert.That(query!.Version, Is.EqualTo(version));
            Assert.That(query.MinimumLevel, Is.EqualTo("14"));
            Assert.That(query.MaximumLevel, Is.EqualTo("14"));
            Assert.That(ChunithmVersusScope.SelectCharts(query, VersionCatalog)
                .Select(chart => (chart.Song.Version, chart.LevelIndex)), Is.EqualTo(new[] { (version, 3) }));
        });
    }

    [TestCase("NEW", "CHUNITHM NEW!!")]
    [TestCase("NEW PLUS", "CHUNITHM NEW PLUS!!")]
    [TestCase("NEW!!", "CHUNITHM NEW!!")]
    [TestCase("CHUNITHM NEW PLUS!!", "CHUNITHM NEW PLUS!!")]
    [TestCase("SUN", "CHUNITHM SUN")]
    [TestCase("SUN PLUS", "CHUNITHM SUN PLUS")]
    public void BareNaturalVersionAliasesDefaultToMaster(string input, string version)
    {
        Assert.That(ChunithmVersusScope.TryParse(input, VersionCatalog, out var query, out var error), Is.True, error);
        Assert.Multiple(() =>
        {
            Assert.That(query!.Version, Is.EqualTo(version));
            Assert.That(query.MinimumLevel, Is.Null);
            Assert.That(query.LevelIndexes, Is.EqualTo(new[] { 3 }));
            Assert.That(ChunithmVersusScope.SelectCharts(query, VersionCatalog)
                .Select(chart => chart.Song.Version), Is.EqualTo(new[] { version }));
        });
    }

    [TestCase("NEWNESS14")]
    [TestCase("NEW PLUSER14")]
    [TestCase("NEW PLUS PLUS14")]
    [TestCase("SUNNONEXISTENT14")]
    [TestCase("XVERSEXY14")]
    [TestCase("CHUNITHM UNKNOWN14")]
    public void UnknownVersionsAndSimilarPrefixesAreRejected(string input) =>
        Assert.That(ChunithmVersusScope.TryParse(input, VersionCatalog, out _, out _), Is.False);

    [TestCase("CHUNITHM NEW!!", "NEW PLUS14")]
    [TestCase("CHUNITHM NEW PLUS!!", "NEW14")]
    [TestCase("CHUNITHM SUN", "SUN+14")]
    [TestCase("CHUNITHM SUN PLUS", "SUN14")]
    public void VersionAliasesDoNotCreateAbsentCatalogVersions(string catalogVersion, string input)
    {
        var catalog = new[] { ChunithmVersusModelTest.Song(1, version: catalogVersion) };
        Assert.That(ChunithmVersusScope.TryParse(input, catalog, out _, out _), Is.False);
    }

    [TestCase("NEW14+", "CHUNITHM NEW!!", 4)]
    [TestCase("NEW PLUS14+", "CHUNITHM NEW PLUS!!", 4)]
    [TestCase("NEW 14-14+ MASTER", "CHUNITHM NEW!!", 3)]
    [TestCase("NEW PLUS14.0-14.5 ULT", "CHUNITHM NEW PLUS!!", 4)]
    public void VersionAliasesPreserveLevelRangesAndDifficultyKeywords(string input, string version, int level)
    {
        Assert.That(ChunithmVersusScope.TryParse(input, VersionCatalog, out var query, out var error), Is.True, error);
        Assert.That(ChunithmVersusScope.SelectCharts(query!, VersionCatalog)
            .Select(chart => (chart.Song.Version, chart.LevelIndex)), Is.EqualTo(new[] { (version, level) }));
    }

    [TestCase("NEW14", "CHUNITHM NEW!!")]
    [TestCase("NEW PLUS14", "CHUNITHM NEW PLUS!!")]
    [TestCase("SUN+14", "CHUNITHM SUN PLUS")]
    public void RandomAndBatchAdaptersUseNaturalVersionAliases(string input, string version)
    {
        var songs = new SongDb<ChunithmSong>("unused.tsv", "unused.tsv", () => VersionCatalog.ToList());
        var plugin = (Chunithm.Chunithm)RuntimeHelpers.GetUninitializedObject(typeof(Chunithm.Chunithm));
        typeof(Chunithm.Chunithm).GetField("<SongDb>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(plugin, songs);
        var adapter = (VersusBattle<ChunithmScore, ChunithmVersusChart>)((ICanBattleWithOthers)plugin).Battle;
        var queue = new MessageQueueProvider();
        var random = adapter.ParseRandom(ScopeMessage("2 " + input), "usage");
        var batch = adapter.ParseBatch(ScopeMessage(input));
        Assert.Multiple(() =>
        {
            Assert.That(adapter.CanParseScope(input), Is.True);
            Assert.That(random, Is.Not.Null);
            Assert.That(batch, Is.Not.Null);
            Assert.That(random!.Count, Is.EqualTo(2));
            Assert.That(random.Charts.Select(chart => chart.Song.Version), Is.EqualTo(new[] { version }));
            Assert.That(batch!.Charts.Select(chart => chart.Song.Version), Is.EqualTo(new[] { version }));
            Assert.That(queue.SendQueue.Reader.TryRead(out _), Is.False);
        });
        return;

        Message ScopeMessage(string command) => new(new MessageSenderProvider(queue), new MessageDataId(1, 1), new MessageDataText(command))
        {
            Type = MessageType.FriendMessage,
            Sender = new SenderInfo(100, "tester"),
            Command = command.AsMemory()
        };
    }

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
