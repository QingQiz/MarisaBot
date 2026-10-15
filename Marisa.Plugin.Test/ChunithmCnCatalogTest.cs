using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Marisa.Plugin.Shared.Chunithm;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class ChunithmCnCatalogTest
{
    private string _directory = null!;
    private string CachePath => Path.Combine(_directory, "Versus.CnCatalog.lxns.json");

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "Marisa-ChuCnCatalog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    [Test]
    public void ParseUsesCanonicalDifficultyAndDoesNotFillMissingUltima()
    {
        var songs = ChunithmCnCatalog.Parse(Json(Song(1, [Chart(3, "14", 14.4), Chart(0, "4", 4), Chart(2, "13+", 13.8)])));
        var song = songs.Single();
        var charts = ChunithmVersusCatalog.StandardCharts(songs);
        Assert.Multiple(() =>
        {
            Assert.That(song.Title, Is.EqualTo("国服歌曲 1"));
            Assert.That(song.Artist, Is.EqualTo("国服曲师"));
            Assert.That(song.Genre, Is.EqualTo("其他游戏"));
            Assert.That(song.Version, Is.EqualTo("CHUNITHM VERSE"));
            Assert.That(song.Bpm, Is.EqualTo(190));
            Assert.That(song.DiffNames, Is.EqualTo(new[] { "BASIC", "EXPERT", "MASTER" }));
            Assert.That(song.Levels, Is.EqualTo(new[] { "4", "13+", "14" }));
            Assert.That(song.Constants, Is.EqualTo(new[] { 4d, 13.8d, 14.4d }));
            Assert.That(song.Charters, Is.All.EqualTo("国服谱师"));
            Assert.That(song.MaxCombo, Is.All.EqualTo(1234));
            Assert.That(charts.Select(chart => chart.LevelIndex), Is.EqualTo(new[] { 0, 2, 3 }));
            Assert.That(charts.Single(chart => chart.LevelIndex == 3).ChartIndex, Is.EqualTo(2));
            Assert.That(ChunithmVersusCatalog.FindChart(song, 4), Is.Null);
        });
        Assert.Throws<NotSupportedException>(() => ((IList<ChunithmSong>)songs).Add(song));
    }

    [Test]
    public void ParseExcludesWeOnlyAndMixedWeWithoutLosingStandardDifficulty()
    {
        var songs = ChunithmCnCatalog.Parse(Json(
            Song(1, [Chart(5, "3", 0)]), Song(2, [Chart(5, "5", 0), Chart(4, "15", 15.3)])));
        Assert.Multiple(() =>
        {
            Assert.That(songs.Select(song => song.Id), Is.EqualTo(new[] { 2L }));
            Assert.That(songs.Single().DiffNames, Is.EqualTo(new[] { "ULTIMA" }));
            Assert.That(ChunithmVersusCatalog.StandardCharts(songs).Single().LevelIndex, Is.EqualTo(4));
        });
    }

    [Test]
    public void ParseRetainsPerChartBpmAndAllowsUnknownComboAndCharter()
    {
        var expert = Chart(2, "14", 14);
        expert["bpm"] = 200;
        expert["note_designer"] = null;
        expert["notes"] = null;
        var song = ChunithmCnCatalog.Parse(Json(Song(1, [expert, Chart(3, "14+", 14.8)]))).Single();
        Assert.Multiple(() =>
        {
            Assert.That(song.BpmList.Select(bpms => bpms.Single()), Is.EqualTo(new[] { 200d, 190d }));
            Assert.That(song.Charters[0], Is.Empty);
            Assert.That(song.MaxCombo[0], Is.Zero);
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase("not-json")]
    [TestCase("{}")]
    [TestCase("{\"songs\":[],\"versions\":[]}")]
    public void ParseRejectsMalformedOrEmptyData(string? json) =>
        Assert.Throws<InvalidDataException>(() => ChunithmCnCatalog.Parse(json!));

    [TestCase(-1)]
    [TestCase(6)]
    public void ParseRejectsInvalidDifficultyEvenWhenAnotherChartIsValid(int difficulty) =>
        Assert.Throws<InvalidDataException>(() => ChunithmCnCatalog.Parse(Json(Song(1, [Chart(3), Chart(difficulty)]))));

    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(16.1)]
    public void ParseRejectsInvalidStandardConstant(double constant) =>
        Assert.Throws<InvalidDataException>(() => ChunithmCnCatalog.Parse(Json(Song(1, [Chart(3, constant: constant)]))));

    [Test]
    public void ParseRejectsDuplicateSongsDifficultiesVersionsAndJsonProperties()
    {
        var normal = Json(Song(1, [Chart(3)]));
        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidDataException>(() => ChunithmCnCatalog.Parse(Json(Song(1, [Chart(3)]), Song(1, [Chart(4)]))));
            Assert.Throws<InvalidDataException>(() => ChunithmCnCatalog.Parse(Json(Song(1, [Chart(3), Chart(3)]))));
            Assert.Throws<InvalidDataException>(() => ChunithmCnCatalog.Parse(normal.Replace("\"id\":1,", "\"id\":1,\"id\":2,")));
            Assert.Throws<InvalidDataException>(() => ChunithmCnCatalog.Parse(normal.Replace(
                "\"version\":22000,\"title\":\"CHUNITHM VERSE\"}",
                "\"version\":22000,\"title\":\"CHUNITHM VERSE\"},{\"version\":22000,\"title\":\"duplicate\"}")));
        });
    }

    [Test]
    public void ParseRejectsOnlyWeAndUnmappedVersion()
    {
        var unknown = Song(1, [Chart(3)]);
        unknown["version"] = 99999;
        Assert.Multiple(() =>
        {
            Assert.Throws<InvalidDataException>(() => ChunithmCnCatalog.Parse(Json(Song(1, [Chart(5, constant: 0)]))));
            Assert.Throws<InvalidDataException>(() => ChunithmCnCatalog.Parse(Json(unknown)));
        });
    }

    [Test]
    public async Task ConcurrentRequestsShareOneFetchAndOneSnapshot()
    {
        var calls = 0;
        var fetched = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var catalog = new ChunithmCnCatalog(() =>
        {
            Interlocked.Increment(ref calls);
            return fetched.Task;
        }, CachePath);
        var tasks = Enumerable.Range(0, 8).Select(_ => catalog.GetAsync()).ToArray();
        Assert.That(calls, Is.EqualTo(1));
        fetched.SetResult(Json(Song(1, [Chart(3)])));
        var results = await Task.WhenAll(tasks);
        Assert.Multiple(() =>
        {
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(results.Skip(1).All(result => ReferenceEquals(result, results[0])), Is.True);
            Assert.That(File.ReadAllText(CachePath), Is.EqualTo(Json(Song(1, [Chart(3)]))));
        });
    }

    [Test]
    public async Task FreshCatalogLastsTwelveHoursAndExpiredCatalogRefreshes()
    {
        var clock = new Clock();
        var calls = 0;
        var catalog = new ChunithmCnCatalog(() => Task.FromResult(Json(Song(++calls, [Chart(3)]))), CachePath, clock);
        var first = await catalog.GetAsync();
        clock.Advance(TimeSpan.FromHours(11));
        Assert.That(await catalog.GetAsync(), Is.SameAs(first));
        clock.Advance(TimeSpan.FromHours(1));
        Assert.That((await catalog.GetAsync()).Single().Id, Is.EqualTo(2));
        Assert.That(calls, Is.EqualTo(2));
    }

    [Test]
    public async Task InvalidatedRefreshKeepsOldReferencesAndChangesLevelsFromCnData()
    {
        var response = Json(Song(1, [Chart(3, "14", 14.4)]));
        var catalog = new ChunithmCnCatalog(() => Task.FromResult(response), CachePath);
        var first = await catalog.GetAsync();
        response = Json(Song(1, [Chart(3, "13+", 13.8)]));
        catalog.Invalidate();
        Assert.That(File.Exists(CachePath), Is.True);
        var second = await catalog.GetAsync();
        Assert.Multiple(() =>
        {
            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(first.Single().Levels.Single(), Is.EqualTo("14"));
            Assert.That(first.Single().Constants.Single(), Is.EqualTo(14.4));
            Assert.That(second.Single().Levels.Single(), Is.EqualTo("13+"));
            Assert.That(second.Single().Constants.Single(), Is.EqualTo(13.8));
            Assert.That(ChunithmVersusScope.TryParse("14", second, out var query, out _), Is.True);
            Assert.That(ChunithmVersusScope.SelectCharts(query!, second), Is.Empty);
        });
    }

    [TestCase("{}")] 
    [TestCase("not-json")]
    [TestCase("{\"songs\":[],\"versions\":[]}")]
    public async Task FailedRefreshKeepsLastGoodMemoryAndDiskAndRetriesInFiveMinutes(string invalid)
    {
        var clock = new Clock();
        var good = Json(Song(1, [Chart(3)]));
        var response = good;
        var calls = 0;
        var catalog = new ChunithmCnCatalog(() => { calls++; return Task.FromResult(response); }, CachePath, clock);
        var first = await catalog.GetAsync();
        response = invalid;
        catalog.Invalidate();
        Assert.That(await catalog.GetAsync(), Is.SameAs(first));
        Assert.Multiple(() =>
        {
            Assert.That(calls, Is.EqualTo(2));
            Assert.That(File.ReadAllText(CachePath), Is.EqualTo(good));
        });
        response = Json(Song(2, [Chart(3)]));
        clock.Advance(TimeSpan.FromMinutes(4));
        Assert.That(await catalog.GetAsync(), Is.SameAs(first));
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.That((await catalog.GetAsync()).Single().Id, Is.EqualTo(2));
        Assert.That(calls, Is.EqualTo(3));
    }

    [Test]
    public async Task FirstFailureReportsUnavailableAndDoesNotHammerService()
    {
        var clock = new Clock();
        var calls = 0;
        var catalog = new ChunithmCnCatalog(() =>
        {
            calls++;
            return Task.FromException<string>(new HttpRequestException("offline"));
        }, CachePath, clock);
        var exception = Assert.ThrowsAsync<HttpRequestException>(async () => await catalog.GetAsync());
        Assert.That(exception!.Message, Does.Contain("国服曲库暂不可用"));
        Assert.ThrowsAsync<HttpRequestException>(async () => await catalog.GetAsync());
        Assert.That(calls, Is.EqualTo(1));
        clock.Advance(TimeSpan.FromMinutes(5));
        Assert.ThrowsAsync<HttpRequestException>(async () => await catalog.GetAsync());
        Assert.That(calls, Is.EqualTo(2));
        Assert.That(File.Exists(CachePath), Is.False);
        await Task.CompletedTask;
    }

    [Test]
    public async Task RestartLoadsLastGoodDiskWhenServiceIsOffline()
    {
        var source = new ChunithmCnCatalog(() => Task.FromResult(Json(Song(1, [Chart(3)]))), CachePath);
        await source.GetAsync();
        File.SetLastWriteTimeUtc(CachePath, DateTime.UtcNow - TimeSpan.FromDays(2));
        var calls = 0;
        var restarted = new ChunithmCnCatalog(() =>
        {
            calls++;
            return Task.FromException<string>(new HttpRequestException("offline"));
        }, CachePath);
        Assert.That((await restarted.GetAsync()).Single().Id, Is.EqualTo(1));
        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public async Task CorruptDiskDoesNotSupplyEligibilityAndValidFetchReplacesIt()
    {
        await File.WriteAllTextAsync(CachePath, "{}");
        var catalog = new ChunithmCnCatalog(() => Task.FromResult(Json(Song(2, [Chart(4)]))), CachePath);
        Assert.That((await catalog.GetAsync()).Single().Id, Is.EqualTo(2));
        Assert.That(ChunithmCnCatalog.Parse(await File.ReadAllTextAsync(CachePath)).Single().Id, Is.EqualTo(2));
    }

    [Test]
    public async Task DifferentCachePathsDoNotShareEligibility()
    {
        var first = new ChunithmCnCatalog(() => Task.FromResult(Json(Song(1, [Chart(3)]))), CachePath);
        var secondPath = Path.Combine(_directory, "other", "catalog.json");
        var second = new ChunithmCnCatalog(() => Task.FromException<string>(new HttpRequestException("offline")), secondPath);
        await first.GetAsync();
        Assert.ThrowsAsync<HttpRequestException>(async () => await second.GetAsync());
        Assert.That(File.Exists(secondPath), Is.False);
    }

    [Test]
    public async Task DynamicConfigurationPathSelectsItsOwnStateAndDisk()
    {
        var selectedPath = CachePath;
        var calls = 0;
        // 与 Shared 使用同一个动态路径构造；不加载真实 config 或凭据文件。
        var constructor = typeof(ChunithmCnCatalog).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance,
            null, [typeof(Func<Task<string>>), typeof(Func<string>), typeof(TimeProvider)], null)!;
        var catalog = (ChunithmCnCatalog)constructor.Invoke(new object[]
        {
            (Func<Task<string>>)(() => Task.FromResult(Json(Song(++calls, [Chart(3)])))),
            (Func<string>)(() => selectedPath), TimeProvider.System
        });
        var first = await catalog.GetAsync();
        selectedPath = Path.Combine(_directory, "other", "catalog.json");
        Assert.That((await catalog.GetAsync()).Single().Id, Is.EqualTo(2));
        selectedPath = CachePath;
        Assert.That(await catalog.GetAsync(), Is.SameAs(first));
        Assert.That(calls, Is.EqualTo(2));
    }

    [Test]
    public async Task UnexpectedConfigurationAndIoErrorsAreNotDowngradedToLastGood()
    {
        var failure = false;
        var catalog = new ChunithmCnCatalog(() => failure
            ? Task.FromException<string>(new InvalidOperationException("programming error"))
            : Task.FromResult(Json(Song(1, [Chart(3)]))), CachePath);
        await catalog.GetAsync();
        failure = true;
        catalog.Invalidate();
        Assert.ThrowsAsync<InvalidOperationException>(async () => await catalog.GetAsync());
        var blocked = new ChunithmCnCatalog(() => Task.FromResult(Json(Song(2, [Chart(3)]))), Path.Combine(CachePath, "invalid.json"));
        Assert.ThrowsAsync<IOException>(async () => await blocked.GetAsync());
    }

    private static Dictionary<string, object?> Chart(int difficulty, string level = "14", double constant = 14.4) => new()
    {
        ["difficulty"] = difficulty, ["level"] = level, ["level_value"] = constant,
        ["note_designer"] = "国服谱师", ["notes"] = new { total = 1234 }
    };

    private static Dictionary<string, object?> Song(long id, object[] charts) => new()
    {
        ["id"] = id, ["title"] = "国服歌曲 " + id, ["artist"] = "国服曲师", ["genre"] = "其他游戏",
        ["bpm"] = 190, ["version"] = 22000, ["difficulties"] = charts
    };

    private static string Json(params object[] songs) => JsonSerializer.Serialize(new
    {
        songs, versions = new[] { new { version = 22000, title = "CHUNITHM VERSE" } }
    });

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan elapsed) => _now += elapsed;
    }
}
