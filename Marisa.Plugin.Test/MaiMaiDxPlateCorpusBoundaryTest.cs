using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using Marisa.Plugin.Shared.MaiMaiDx;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class MaiMaiDxPlateCorpusBoundaryTest
{
    private MaiMaiSong[] songs = [];
    private string[] charters = [];
    private string[] artists = [];
    private string[] names = [];

    [OneTimeSetUp]
    public void LoadRepositoryCatalog()
    {
        var repositoryRoot = Directory.GetParent(Environment.CurrentDirectory)!.Parent!.Parent!.Parent!.ToString();
        var path = Path.Join(repositoryRoot, "Marisa.Frontend", "public", "assets", "maimai", "SongInfo.json");
        songs = JsonConvert.DeserializeObject<ExpandoObject[]>(File.ReadAllText(path))!
            .Select(data => new MaiMaiSong(data)).ToArray();
        charters = songs.SelectMany(song => song.Charters)
            .Where(name => !string.IsNullOrWhiteSpace(name) && name != "N/A")
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        artists = songs.Select(song => song.Info.Artist).Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        names = charters.Concat(artists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    [Test]
    public void EveryCatalogNameKeepsItsDimensionAndFieldsInFifteenForms()
    {
        var failures = new List<string>();
        foreach (var name in names)
        {
            Check($"{name}完成表", name, "SSS", [3, 4]);
            Check($"神{name}完成表", name, "AP", [3, 4]);
            Check($"{name}神完成表", name, "AP", [3, 4]);
            Check($"MST {name}完成表", name, "SSS", [3]);
            Check($"{name} EXP/Re:MASTER完成表", name, "SSS", [2, 4]);
            Check($"彩代 {name} 神完成表", name, "AP", [3], plate: "彩");
            Check($"{name} 彩代 神完成表", name, "AP", [3], plate: "彩");
            Check($"东方 {name} 神完成表", name, "AP", [3, 4], genre: "东方Project");
            Check($"{name} 东方 神完成表", name, "AP", [3, 4], genre: "东方Project");
            Check($"14+ {name} 神完成表", name, "AP", [0, 1, 2, 3, 4], level: "14+");
            Check($"{name} 14+ 神完成表", name, "AP", [0, 1, 2, 3, 4], level: "14+");
            Check($"定数14.5 {name} 神完成表", name, "AP", [0, 1, 2, 3, 4], constant: 14.5);
            Check($"{name} 定数14.5 神完成表", name, "AP", [0, 1, 2, 3, 4], constant: 14.5);
            Check($"彩代 东方 14+ MST {name} AP+完成表", name, "AP+", [3], "彩", "东方Project", "14+");
            Check($"{name} MST 14+ 东方 彩代 AP+完成表", name, "AP+", [3], "彩", "东方Project", "14+");
        }

        Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures.Take(30)));
        return;

        void Check(string raw, string name, string threshold, int[] levels,
            string? plate = null, string? genre = null, string? level = null, double? constant = null)
        {
            var ok = PlateData.TryParse(raw, charters, artists, out var query, out var error);
            var nameSelectors = query?.Selectors.Where(IsName).ToArray();
            var expectedCount = 1 + (plate == null ? 0 : 1) + (genre == null ? 0 : 1)
                + (level == null ? 0 : 1) + (constant == null ? 0 : 1);
            if (!ok || nameSelectors is not { Length: 1 } || nameSelectors[0].Display != name
                || nameSelectors[0].GetType() != ExpectedNameType(name)
                || query!.Selectors.Count != expectedCount || query.Threshold.DisplayName != threshold
                || !query.LevelIdxes.SequenceEqual(levels)
                || (plate != null && !query.Selectors.OfType<PlateData.Selector.Plate>().Any(p => p.Kanji == plate))
                || (genre != null && !query.Selectors.OfType<PlateData.Selector.Genre>().Any(g => g.FullName == genre))
                || (level != null && !query.Selectors.OfType<PlateData.Selector.Level>().Any(l => l.Label == level))
                || (constant != null && !query.Selectors.OfType<PlateData.Selector.Constant>().Any(c => c.Value == constant)))
                failures.Add($"{raw}: expected {ExpectedNameType(name).Name}; {Describe(query, error)}");
        }
    }

    [Test]
    public void DifficultyOnBothSidesOfEveryEligibleNameHasTheSameMeaningAsSpaces()
    {
        var failures = new List<string>();
        var eligibleNames = names.Where(name => !IsAsciiLetter(name[0]) && !IsAsciiLetter(name[^1])).ToArray();
        Assert.That(eligibleNames.Length, Is.GreaterThan(400), "the real non-ASCII name corpus must be exercised");
        foreach (var name in eligibleNames)
        {
            var spaced = Parse($"EXP {name} MST完成表");
            var ok = PlateData.TryParse($"EXP{name}MST完成表", charters, artists, out var query, out var error);
            if (!ok || query!.Selectors.Count != 1 || query.Selectors[0].Display != name
                || query.Selectors[0].GetType() != ExpectedNameType(name)
                || !query.LevelIdxes.SequenceEqual(new[] { 2, 3 })
                || !spaced.LevelIdxes.SequenceEqual(new[] { 2, 3 })
                || spaced.Selectors.Count != 1 || spaced.Selectors[0].Display != name
                || spaced.Selectors[0].GetType() != ExpectedNameType(name)
                || query.Threshold != PlateData.DefaultThreshold || spaced.Threshold != PlateData.DefaultThreshold)
                failures.Add($"EXP{name}MST: {Describe(query, error)}; spaced: {Describe(spaced, null)}");
        }

        Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures.Take(30)));
    }

    [Test]
    public void AllAliasesKeepTheirRangeAndFieldsAcrossScopeCombinations()
    {
        var tokens = PlateData.PlateVersionMap.Keys.Concat(PlateData.GenreAliasMap.Keys)
            .Concat(PlateData.GenreAliasMap.Values).Append("复活曲").Distinct().ToArray();
        var failures = new List<string>();
        var ambiguous = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var alias in PlateData.CharterAliasMap.Keys)
        foreach (var token in tokens)
        {
            Check($"{alias}{token}神完成表", alias + token, alias, token, "AP", DefaultLevels(token));
            Check($"{token}{alias}神完成表", token + alias, alias, token, "AP", DefaultLevels(token));
            Check($"神{alias} {token}完成表", null, alias, token, "AP", DefaultLevels(token));
            Check($"MST {token}{alias}完成表", token + alias, alias, token, token == "霸者" ? "A" : "SSS", [3]);
            Check($"{alias}{token}14神完成表", alias + token, alias, token, "AP", [0, 1, 2, 3, 4], true);
        }

        // 无分隔的「隅田川星/隅田川華」同时是合法姓名片段；不能强迫它们变成别名 + 版本。
        Assert.That(ambiguous, Does.Contain("隅田川星"));
        Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures.Take(30)));
        return;

        void Check(string raw, string? adjacent, string alias, string token, string threshold, int[] levels,
            bool hasLevel = false)
        {
            if (adjacent != null && names.Concat(PlateData.CharterIdentityMap.Keys)
                    .Any(name => name.Contains(adjacent, StringComparison.OrdinalIgnoreCase)))
            {
                ambiguous.Add(adjacent);
                return;
            }

            var ok = PlateData.TryParse(raw, charters, artists, out var query, out var error);
            var aliasSelectors = query?.Selectors.OfType<PlateData.Selector.CharterAlias>().ToArray();
            if (!ok || aliasSelectors is not { Length: 1 } || aliasSelectors[0].Input != alias
                || !aliasSelectors[0].Names.SequenceEqual(PlateData.CharterAliasMap[alias])
                || !aliasSelectors[0].Exclude.SequenceEqual(PlateData.CharterAliasExclude.GetValueOrDefault(alias, []))
                || query!.Selectors.Count != (hasLevel ? 3 : 2)
                || !HasScope(query, token) || query.Threshold.DisplayName != threshold
                || !query.LevelIdxes.SequenceEqual(levels)
                || (hasLevel && !query.Selectors.OfType<PlateData.Selector.Level>().Any(level => level.Label == "14")))
                failures.Add($"{raw}: {Describe(query, error)}");
        }

        static int[] DefaultLevels(string token) => PlateData.PlateVersionMap.ContainsKey(token)
            ? token is "舞" or "霸者" ? [3, 4] : [3]
            : PlateData.GenreAliasMap.GetValueOrDefault(token, token) == "宴会場" ? [0, 1, 2, 3, 4] : [3, 4];

        static bool HasScope(PlateData.Query query, string token) => token == "复活曲"
            ? query.Selectors.OfType<PlateData.Selector.Revival>().Count() == 1
            : PlateData.PlateVersionMap.TryGetValue(token, out var versions)
                ? query.Selectors.OfType<PlateData.Selector.Plate>().Any(p => p.Kanji == token && p.Versions.SequenceEqual(versions))
                : query.Selectors.OfType<PlateData.Selector.Genre>()
                    .Any(g => g.FullName == PlateData.GenreAliasMap.GetValueOrDefault(token, token));
    }

    [TestCase("Sta")]
    [TestCase("xi")]
    [TestCase("Ado")]
    [TestCase("pan")]
    [TestCase("からめる")]
    [TestCase("佑")]
    [TestCase("はっぴー")]
    [TestCase("三枝明那")]
    public void NamesInBothDimensionsSelectTheIndependentUnionRegardlessOfFields(string name)
    {
        Assert.That(ExpectedNameType(name), Is.EqualTo(typeof(PlateData.Selector.CharterOrArtist)));
        var expected = DirectNameCharts(name, [3, 4]);
        Assert.That(expected, Is.Not.Empty, "known positive control");
        Assert.That(expected.Count, Is.LessThan(songs.Sum(song => Math.Max(0, song.Constants.Count - 3))),
            "unrelated charts must provide negative controls");
        foreach (var raw in new[] { $"{name}完成表", $"{name}神完成表", $"神{name}完成表" })
        {
            var query = Parse(raw);
            Assert.That(query.Selectors.Single(), Is.InstanceOf<PlateData.Selector.CharterOrArtist>(), raw);
            Assert.That(SelectedCharts(query), Is.EquivalentTo(expected), raw);
        }
        Assert.That(SelectedCharts(Parse("__plate_corpus_unknown__神完成表")), Is.Empty, "unknown-name negative control");
    }

    [TestCase("Luxi", "神", "AP")]
    [TestCase("mai-Sta", "神", "AP")]
    [TestCase("PANDORA", "神", "AP")]
    [TestCase("舞舞10年ズ", "神", "AP")]
    [TestCase("-ZONE-", "紫谱", "SSS")]
    [TestCase("A-", "EXP", "SSS")]
    public void RepresentativePartialNamesKeepTheirIndependentChartRange(string name, string field, string threshold)
    {
        int[] levels = field == "紫谱" ? [3] : field == "EXP" ? [2] : [3, 4];
        var expected = DirectNameCharts(name, levels);
        Assert.That(expected, Is.Not.Empty, "known positive control");
        foreach (var raw in new[] { $"{name} {field}完成表", $"{field} {name}完成表" })
        {
            var query = Parse(raw);
            Assert.That(query.Selectors.Single().Display, Is.EqualTo(name), raw);
            Assert.That(query.Selectors.Single().GetType(), Is.EqualTo(ExpectedNameType(name)), raw);
            Assert.That(query.Threshold.DisplayName, Is.EqualTo(threshold), raw);
            Assert.That(query.LevelIdxes, Is.EqualTo(levels), raw);
            Assert.That(SelectedCharts(query), Is.EquivalentTo(expected), raw);
        }
    }

    [Test]
    public void AddingAchievementToUnambiguousGeneratedNameFragmentsKeepsNameDimensionAndDifficulty()
    {
        var fragments = names.Where(name => name.Length >= 5)
            .SelectMany(name => new[] { name[..^1], name[1..], name[..(name.Length / 2)], name[(name.Length / 2)..] })
            .Where(fragment => fragment.Length >= 3 && !char.IsSurrogate(fragment[0]) && !char.IsSurrogate(fragment[^1]))
            .Where(fragment => !PlateData.CharterAliasMap.Keys.Any(alias => fragment.Contains(alias, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(fragment => fragment, StringComparer.Ordinal).Take(512);
        var checkedFragments = 0;
        var failures = new List<string>();
        foreach (var fragment in fragments)
        {
            if (!PlateData.TryParse($"{fragment}完成表", charters, artists, out var baseline, out _)
                || baseline!.Selectors.Count != 1 || !IsName(baseline.Selectors[0])
                || baseline.Selectors[0].Display != fragment || baseline.Selectors[0].GetType() != ExpectedNameType(fragment)
                || baseline.Threshold != PlateData.DefaultThreshold
                || !baseline.LevelIdxes.SequenceEqual(PlateData.DefaultLevelIdxes)) continue;

            checkedFragments++;
            foreach (var raw in new[] { $"{fragment}神完成表", $"神{fragment}完成表" })
            {
                var ok = PlateData.TryParse(raw, charters, artists, out var query, out var error);
                if (!ok || query!.Selectors.Count != 1 || query.Selectors[0] != baseline.Selectors[0]
                    || !query.LevelIdxes.SequenceEqual(baseline.LevelIdxes)
                    || query.Threshold != new PlateData.Threshold(PlateData.Dimension.Fc, 3, "AP"))
                    failures.Add($"{raw}: baseline {Describe(baseline, null)}; actual {Describe(query, error)}");
            }
        }

        Assert.That(checkedFragments, Is.GreaterThan(100), "fragment filtering must retain a meaningful control corpus");
        Assert.That(failures, Is.Empty, string.Join(Environment.NewLine, failures.Take(30)));
    }

    private PlateData.Query Parse(string raw)
    {
        var ok = PlateData.TryParse(raw, charters, artists, out var query, out var error);
        Assert.That(ok, Is.True, $"{raw}: {error}");
        return query!;
    }

    private Type ExpectedNameType(string input)
    {
        var charterHit = charters.Concat(PlateData.CharterIdentityMap.Keys)
            .Any(name => name.Contains(input, StringComparison.OrdinalIgnoreCase));
        var artistHit = artists.Any(name => name.Contains(input, StringComparison.OrdinalIgnoreCase));
        return charterHit && artistHit ? typeof(PlateData.Selector.CharterOrArtist)
            : charterHit ? typeof(PlateData.Selector.Charter) : typeof(PlateData.Selector.Artist);
    }

    // 从曲库署名直接构造预期集合，不调用解析器或生产选谱/匹配函数。
    private HashSet<(long Id, int LevelIdx)> DirectNameCharts(string input, int[] levels)
    {
        var hasIdentity = PlateData.CharterIdentityMap.TryGetValue(input, out var identity);
        return songs.SelectMany(song => song.Constants.Select((_, levelIdx) => (Song: song, LevelIdx: levelIdx)))
            .Where(chart => levels.Contains(chart.LevelIdx))
            .Where(chart => chart.Song.Info.Artist.Contains(input, StringComparison.OrdinalIgnoreCase)
                || (chart.LevelIdx < chart.Song.Charters.Count && (hasIdentity
                    ? identity.Names.Any(name => chart.Song.Charters[chart.LevelIdx].Contains(name, StringComparison.OrdinalIgnoreCase))
                        && !identity.Exclude.Any(name => chart.Song.Charters[chart.LevelIdx].Contains(name, StringComparison.OrdinalIgnoreCase))
                    : chart.Song.Charters[chart.LevelIdx].Contains(input, StringComparison.OrdinalIgnoreCase))))
            .Select(chart => (chart.Song.Id, chart.LevelIdx)).ToHashSet();
    }

    private HashSet<(long Id, int LevelIdx)> SelectedCharts(PlateData.Query query) => PlateData.SelectCharts(query, songs)
        .Select(chart => (chart.Song.Id, chart.LevelIdx)).ToHashSet();

    private static bool IsName(PlateData.Selector selector) => selector is PlateData.Selector.Charter
        or PlateData.Selector.Artist or PlateData.Selector.CharterOrArtist;

    private static bool IsAsciiLetter(char ch) => ch is >= 'a' and <= 'z' or >= 'A' and <= 'Z';

    private static string Describe(PlateData.Query? query, PlateData.ParseError? error) => query == null
        ? $"{error?.Kind}/{error?.Detail}"
        : $"{string.Join(';', query.Selectors.Select(selector => $"{selector.GetType().Name}:{selector.Display}"))}"
            + $"; threshold={query.Threshold.DisplayName}; levels={string.Join(',', query.LevelIdxes)}";
}
