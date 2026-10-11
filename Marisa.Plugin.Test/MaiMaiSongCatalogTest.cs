using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Marisa.Plugin.Shared.MaiMaiDx;
using Marisa.Plugin.Shared.Util.SongDb;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class MaiMaiSongCatalogTest
{
    private static JObject Fixture => JObject.Parse(File.ReadAllText(Path.Join(
        TestContext.CurrentContext.TestDirectory, "Resources", "Testing", "MaiMai", "public_catalog_fixture.json")));

    [Test]
    public void Lxns_ConvertsActualDxIdAndPreservesNotes()
    {
        var songs = LxnsSongCatalog.FromJson(Fixture["lxns"]!.ToString());
        var blackbox = songs.Single(song => song.Id == 11818);
        Assert.Multiple(() =>
        {
            Assert.That(blackbox.Title, Is.EqualTo("World's end BLACKBOX"));
            Assert.That(blackbox.Type, Is.EqualTo("DX"));
            Assert.That(blackbox.Levels.Count, Is.EqualTo(4));
            Assert.That(blackbox.Charts[3].Notes, Is.EqualTo(new long[] { 831, 137, 116, 115, 123 }));
            Assert.That(blackbox.Constants[3], Is.EqualTo(14.9));
            Assert.That(blackbox.Info.IsNew, Is.True);
        });
    }

    [Test]
    public void Lxns_StandardAndDxUseSeparateFullIds()
    {
        var source = (JObject)Fixture["lxns"]!;
        var blackbox = (JObject)source["songs"]!.Single(song => (long)song["id"]! == 1818);
        var charts = (JArray)blackbox["difficulties"]!["dx"]!;
        blackbox["difficulties"]!["standard"] = charts.DeepClone();
        var songs = LxnsSongCatalog.FromJson(source.ToString());
        Assert.Multiple(() =>
        {
            Assert.That(songs.Single(song => song.Id == 1818).Type, Is.EqualTo("SD"));
            Assert.That(songs.Single(song => song.Id == 1818).Charts[3].Notes,
                Is.EqualTo(new long[] { 831, 137, 116, 123 }));
            Assert.That(songs.Single(song => song.Id == 11818).Type, Is.EqualTo("DX"));
        });
    }

    [Test]
    public void Lxns_UsesActualChartTypeDebutInsteadOfSharedSongDebut()
    {
        // 公开 fixture：ネコ日和。的同一 song.version=10000，SD=10000，DX=25500。
        var songs = LxnsSongCatalog.FromJson(Fixture["lxns"]!.ToString());
        Assert.Multiple(() =>
        {
            Assert.That(songs.Single(song => song.Id == 30).Info.From, Is.EqualTo("maimai"));
            Assert.That(songs.Single(song => song.Id == 30).Info.IsNew, Is.False);
            Assert.That(songs.Single(song => song.Id == 10030).Info.From, Is.EqualTo("舞萌DX 2026"));
            Assert.That(songs.Single(song => song.Id == 10030).Info.IsNew, Is.True);
        });
    }

    [TestCase("standard", 30)]
    [TestCase("dx", 10030)]
    public void Lxns_LaterRemDoesNotMoveBasicChartsIntoNewVersion(string sourceType, long id)
    {
        var source = (JObject)Fixture["lxns"]!;
        var song = source["songs"]!.Single(item => (long)item["id"]! == 30);
        var charts = (JArray)song["difficulties"]![sourceType]!;
        // 模拟查分器日后按各难度准确给出不同的首次推出版本；不是实际快照中的差异。
        foreach (var chart in charts) chart["version"] = 25000;
        var rem = (JObject)charts.Last!.DeepClone();
        rem["difficulty"] = 4;
        rem["version"] = 25500;
        charts.Add(rem);

        var parsed = LxnsSongCatalog.FromJson(source.ToString()).Single(item => item.Id == id);
        Assert.Multiple(() =>
        {
            Assert.That(parsed.Levels.Count, Is.EqualTo(5));
            Assert.That(parsed.Info.From, Is.EqualTo("舞萌DX 2025"));
            Assert.That(parsed.Info.IsNew, Is.False);
        });
    }

    [Test]
    public void Lxns_BuddyPreservesBothActualSidesWithoutInventingChartIds()
    {
        var song = LxnsSongCatalog.FromJson(Fixture["lxns"]!.ToString()).Single(song => song.Id == 110281);
        Assert.Multiple(() =>
        {
            Assert.That(song.Levels, Is.EqualTo(new[] { "13?", "13?" }));
            Assert.That(song.Charts, Has.Count.EqualTo(2));
            Assert.That(song.Charts[0].Notes, Is.EqualTo(new long[] { 557, 20, 55, 0, 21 }));
            Assert.That(song.Charts[1].Notes, Is.EqualTo(new long[] { 557, 20, 55, 0, 21 }));
        });
    }

    [TestCase("notes")]
    [TestCase("level_value")]
    public void Lxns_RejectsIncompleteChartsInsteadOfInventingData(string property)
    {
        var source = (JObject)Fixture["lxns"]!;
        var song = source["songs"]!.Single(item => (long)item["id"]! == 1818);
        ((JObject)song["difficulties"]!["dx"]![2]!).Remove(property);
        Assert.That(LxnsSongCatalog.FromJson(source.ToString()).Any(item => item.Id == 11818), Is.False);
    }

    [Test]
    public void Lxns_RejectsMissingDifficultyInsteadOfShiftingMaster()
    {
        var source = (JObject)Fixture["lxns"]!;
        var song = source["songs"]!.Single(item => (long)item["id"]! == 1818);
        ((JArray)song["difficulties"]!["dx"]!).RemoveAt(1);
        Assert.That(LxnsSongCatalog.FromJson(source.ToString()).Any(item => item.Id == 11818), Is.False);
    }

    [Test]
    public async Task Provider_MergesLxnsOnlySongsAndRefreshesAfterTtl()
    {
        var fixture = Fixture;
        var handler = new CatalogHandler { DivingFish = fixture["divingfish"]!.ToString(), Lxns = fixture["lxns"]!.ToString() };
        var clock = new CatalogClock();
        var catalog = new MaiMaiSongCatalog("no-fallback-needed.json", new HttpClient(handler), clock);
        var first = await catalog.GetAsync();
        var same = await catalog.GetAsync();
        Assert.Multiple(() =>
        {
            Assert.That(first.Single(song => song.Id == 11818).Info.From,
                Is.EqualTo("maimai でらっくす PRiSM PLUS"), "Duplicate IDs must retain the DivingFish metadata");
            Assert.That(first.Any(song => song.Id == 110281), Is.True, "Lxns-only songs must be searchable");
            Assert.That(same, Is.SameAs(first));
            Assert.That(handler.RequestCount, Is.EqualTo(2));
        });

        // 模拟落雪先上线：替换公开 fixture 的基础 ID，未向真实查分器发请求。
        var source = (JObject)fixture["lxns"]!;
        var next = source["songs"]!.Single(song => (long)song["id"]! == 1818);
        next["id"] = 1900;
        handler.Lxns = source.ToString();
        clock.Advance(MaiMaiSongCatalog.RefreshInterval + TimeSpan.FromSeconds(1));
        var refreshed = await catalog.GetAsync();
        var db = new SongDb<MaiMaiSong>("unused-alias.tsv", "unused-alias.tmp", () => first);
        _ = db.SongIndexer;
        db.ReplaceSongList(refreshed);
        Assert.Multiple(() =>
        {
            Assert.That(db.SearchSongExact("id11900".AsMemory()).Single().Id, Is.EqualTo(11900));
            Assert.That(db.SongIndexer.ContainsKey(11900), Is.True);
            Assert.That(db.SearchSongExact("id999999".AsMemory()), Is.Empty);
            Assert.That(handler.RequestCount, Is.EqualTo(4));
        });
    }

    [Test]
    public async Task Provider_FailuresPreserveLastSuccessfulCatalogAndRemainRateLimited()
    {
        var fixture = Fixture;
        var handler = new CatalogHandler { DivingFish = fixture["divingfish"]!.ToString(), Lxns = fixture["lxns"]!.ToString() };
        var clock = new CatalogClock();
        var catalog = new MaiMaiSongCatalog("no-fallback-needed.json", new HttpClient(handler), clock);
        var first = await catalog.GetAsync();
        handler.Fail = true;
        clock.Advance(MaiMaiSongCatalog.RefreshInterval + TimeSpan.FromSeconds(1));
        var retained = await catalog.GetAsync();
        await catalog.GetAsync();
        Assert.Multiple(() =>
        {
            Assert.That(retained, Is.SameAs(first));
            Assert.That(retained.Any(song => song.Id == 11818), Is.True);
            Assert.That(handler.RequestCount, Is.EqualTo(4), "Failure must not cause every lookup to hit the network");
        });
    }

    [Test]
    public async Task Provider_LxnsCanAddNewSongWhileDivingFishIsUnavailable()
    {
        var fixture = Fixture;
        var handler = new CatalogHandler { DivingFish = fixture["divingfish"]!.ToString(), Lxns = fixture["lxns"]!.ToString() };
        var clock = new CatalogClock();
        var catalog = new MaiMaiSongCatalog("no-fallback-needed.json", new HttpClient(handler), clock);
        await catalog.GetAsync();

        var source = (JObject)fixture["lxns"]!;
        source["songs"]!.Single(song => (long)song["id"]! == 1818)["id"] = 1900;
        handler.Lxns = source.ToString();
        handler.FailDivingFish = true;
        clock.Advance(MaiMaiSongCatalog.RefreshInterval + TimeSpan.FromSeconds(1));
        var refreshed = await catalog.GetAsync();
        Assert.Multiple(() =>
        {
            Assert.That(refreshed.Any(song => song.Id == 11900), Is.True);
            Assert.That(refreshed.Any(song => song.Id == 11818), Is.True, "Last successful DivingFish data must remain available");
        });

        handler.Lxns = "{}";
        clock.Advance(MaiMaiSongCatalog.RefreshInterval + TimeSpan.FromSeconds(1));
        Assert.That(await catalog.GetAsync(), Is.SameAs(refreshed), "Malformed responses must not replace working data");
    }

    [Test]
    public void SongDb_ReplacementRebuildsAliasesAndPreservesBothAliasFiles()
    {
        var fixtureName = Path.Join(TestContext.CurrentContext.WorkDirectory, $"catalog-alias-{Guid.NewGuid():N}");
        var aliases = fixtureName + ".tsv";
        var userAliases = fixtureName + ".tmp";
        const string aliasText = "World's end BLACKBOX\tconfirmedalias\n[回]ダブルラリアット\tnewly-visible\n";
        const string userAliasText = "World's end BLACKBOX\tmyalias\n";
        File.WriteAllText(aliases, aliasText);
        File.WriteAllText(userAliases, userAliasText);
        var fixture = Fixture;
        var db = new SongDb<MaiMaiSong>(aliases, userAliases,
            () => MaiMaiSongCatalog.ParseDivingFish(fixture["divingfish"]!.ToString()));
        try
        {
            Assert.That(db.SearchSongExact("newly-visible".AsMemory()), Is.Empty);
            Assert.That(db.SearchSongExact("myalias".AsMemory()).Single().Id, Is.EqualTo(11818));
            db.ReplaceSongList(LxnsSongCatalog.FromJson(fixture["lxns"]!.ToString()));
            Assert.Multiple(() =>
            {
                Assert.That(db.SearchSongExact("confirmedalias".AsMemory()).Single().Id, Is.EqualTo(11818));
                Assert.That(db.SearchSongExact("myalias".AsMemory()).Single().Id, Is.EqualTo(11818));
                Assert.That(db.SearchSongExact("newly-visible".AsMemory()).Single().Id, Is.EqualTo(110281));
                Assert.That(File.ReadAllText(aliases), Is.EqualTo(aliasText));
                Assert.That(File.ReadAllText(userAliases), Is.EqualTo(userAliasText));
            });
        }
        finally
        {
            db.Reset();
            File.Delete(aliases);
            File.Delete(userAliases);
        }
    }

    [Test]
    public async Task Provider_InitialFailureUsesOnlyTheExplicitStaticFallback()
    {
        var fixturePath = Path.Join(TestContext.CurrentContext.WorkDirectory, $"catalog-fallback-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(fixturePath, Fixture["divingfish"]!.ToString());
        try
        {
            var handler = new CatalogHandler { Fail = true };
            var catalog = new MaiMaiSongCatalog(fixturePath, new HttpClient(handler), new CatalogClock());
            var songs = await catalog.GetAsync();
            Assert.That(songs.Select(song => song.Id), Is.EquivalentTo(new long[] { 10030, 11818 }));
        }
        finally
        {
            File.Delete(fixturePath);
        }
    }

    private sealed class CatalogClock : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 10, 11, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }

    private sealed class CatalogHandler : HttpMessageHandler
    {
        public string DivingFish = "[]";
        public string Lxns = "{}";
        public bool Fail;
        public bool FailDivingFish;
        public int RequestCount;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref RequestCount);
            var divingFish = request.RequestUri!.AbsoluteUri == MaiMaiSongCatalog.DivingFishEndpoint;
            return Task.FromResult(new HttpResponseMessage(Fail || (FailDivingFish && divingFish)
                ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
            {
                Content = new StringContent(divingFish ? DivingFish : Lxns)
            });
        }
    }
}
