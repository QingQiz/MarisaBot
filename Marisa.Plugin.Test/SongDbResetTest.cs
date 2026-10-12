using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Marisa.Plugin.Shared.Interface;
using Marisa.Plugin.Shared.Util.SongDb;
using NUnit.Framework;
using SixLabors.ImageSharp;

namespace Marisa.Plugin.Test;

public class SongDbResetTest
{
    private sealed class StubSong : Song
    {
        // 测试只用到 Id / Title（SongIndexer 走 ToDictionary(Id)），其余抽象成员不会被调用。
        public override string MaxLevel() => throw new NotSupportedException();
        public override string GetImage() => throw new NotSupportedException();
        public override Image  GetCover() => throw new NotSupportedException();
    }

    private static SongDb<StubSong> NewDb(Func<int, StubSong[]> gen, out Func<int> calls)
    {
        var n = 0;
        calls = () => n;
        var capture = gen;
        return new SongDb<StubSong>("nonexistent.tsv", "nonexistent.tmp", () =>
        {
            n++;
            return capture(n).ToList();
        });
    }

    [Test]
    public void Reset_RegeneratesSongList_AndRebuildsIndexer()
    {
        var db = NewDb(gen => [new StubSong { Id = gen, Title = $"song{gen}" }], out var calls);

        var list1  = db.SongList;     // 触发生成 #1
        var index1 = db.SongIndexer;  // 从 #1 建索引
        Assert.That(calls(), Is.EqualTo(1));
        Assert.That(index1[1].Title, Is.EqualTo("song1"));

        db.Reset();

        var list2  = db.SongList;     // 应重新生成 #2
        var index2 = db.SongIndexer;  // 应重建

        Assert.That(calls(), Is.EqualTo(2), "Reset 后再访问 SongList 应重新调用 generator");
        Assert.That(list2,  Is.Not.SameAs(list1));
        // 这条是 b50 rollover 的关键：SongIndexer 必须一起作废重建，否则 is_new 查询仍读旧索引。
        Assert.That(index2, Is.Not.SameAs(index1), "Reset 后 SongIndexer 必须重建");
        Assert.That(index2.ContainsKey(2), Is.True,  "新索引应反映重新生成的歌单");
        Assert.That(index2.ContainsKey(1), Is.False, "旧歌单的 id 不应残留在新索引里");
    }

    [Test]
    public void CatalogChange_CannotBeOverwrittenByAnInFlightOldIndexer()
    {
        using var generating = new ManualResetEventSlim();
        using var releaseGeneration = new ManualResetEventSlim();
        using var changing = new ManualResetEventSlim();
        var generations = 0;
        var db = new SongDb<StubSong>("unused.tsv", "unused.tmp", () =>
        {
            var generation = Interlocked.Increment(ref generations);
            if (generation == 1)
            {
                generating.Set();
                if (!releaseGeneration.Wait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("Test did not release the initial catalog generation");
            }
            return [new StubSong { Id = generation, Title = $"song{generation}" }];
        });
        var oldIndexer = Task.Factory.StartNew(() => db.SongIndexer,
            CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        Task? change = null;
        try
        {
            Assert.That(generating.Wait(TimeSpan.FromSeconds(10)), Is.True);
            change = Task.Factory.StartNew(() =>
            {
                changing.Set();
                db.Reset();
            }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            Assert.That(changing.Wait(TimeSpan.FromSeconds(10)), Is.True);
            // A change must wait for the previous construction, then invalidate it atomically.
            Assert.That(change.Wait(TimeSpan.FromMilliseconds(100)), Is.False);
            releaseGeneration.Set();
            Assert.That(Task.WaitAll([oldIndexer, change], TimeSpan.FromSeconds(10)), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(oldIndexer.Result.ContainsKey(1), Is.True);
                Assert.That(db.SongList.Single().Id, Is.EqualTo(2));
                Assert.That(db.SongIndexer.Keys, Is.EquivalentTo(new long[] { 2 }));
                Assert.That(generations, Is.EqualTo(2));
            });
        }
        finally
        {
            releaseGeneration.Set();
            Task.WaitAll(change == null ? [oldIndexer] : [oldIndexer, change], TimeSpan.FromSeconds(10));
            db.Reset();
        }
    }

    [Test]
    public void AliasCallbacks_FromDisposedWatchersCannotOverwriteOrReviveCurrentCache()
    {
        var path = Path.Join(TestContext.CurrentContext.WorkDirectory, $"song-db-alias-{Guid.NewGuid():N}");
        var aliases = path + ".tsv";
        var userAliases = path + ".tmp";
        File.WriteAllText(aliases, "song2\tstable\n");
        const string userText = "song2\tpersonal\n";
        File.WriteAllText(userAliases, userText);
        var songs = new[] { new StubSong { Id = 1, Title = "song1" } };
        var db = new SongDb<StubSong>(aliases, userAliases, () => songs.ToList());
        var watcherField = typeof(SongDb<StubSong>).GetField("_songAliasChangedWatcher", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var aliasField = typeof(SongDb<StubSong>).GetField("_songAlias", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var refresh = typeof(SongDb<StubSong>).GetMethod("RefreshSongAliases", BindingFlags.Instance | BindingFlags.NonPublic)!;
        try
        {
            _ = db.GetSongAliasesByName("song1");
            var oldWatcher = (FileSystemWatcher)watcherField.GetValue(db)!;
            oldWatcher.EnableRaisingEvents = false;

            songs = [new StubSong { Id = 2, Title = "song2" }];
            db.Reset();
            Assert.That(db.SearchSongExact("stable".AsMemory()).Single().Id, Is.EqualTo(2));
            var currentWatcher = (FileSystemWatcher)watcherField.GetValue(db)!;
            currentWatcher.EnableRaisingEvents = false;
            const string updatedText = "song2\tupdated\n";
            File.WriteAllText(aliases, updatedText);

            // Deliver the delayed reload directly, without timing an OS notification or debounce sleep.
            refresh.Invoke(db, [oldWatcher]);
            Assert.Multiple(() =>
            {
                Assert.That(db.SearchSongExact("stable".AsMemory()).Single().Id, Is.EqualTo(2));
                Assert.That(db.SearchSongExact("updated".AsMemory()), Is.Empty);
            });

            refresh.Invoke(db, [currentWatcher]);
            Assert.Multiple(() =>
            {
                Assert.That(db.SearchSongExact("stable".AsMemory()), Is.Empty);
                Assert.That(db.SearchSongExact("updated".AsMemory()).Single().Id, Is.EqualTo(2));
                Assert.That(db.SearchSongExact("personal".AsMemory()).Single().Id, Is.EqualTo(2));
                Assert.That(File.ReadAllText(aliases), Is.EqualTo(updatedText));
                Assert.That(File.ReadAllText(userAliases), Is.EqualTo(userText));
            });

            db.Reset();
            refresh.Invoke(db, [currentWatcher]);
            Assert.That(aliasField.GetValue(db), Is.Null, "A queued callback must not revive a reset cache");
        }
        finally
        {
            db.Reset();
            File.Delete(aliases);
            File.Delete(userAliases);
        }
    }

    [Test]
    public void ResetCommandFilter_NeedsInterfaceCheck_NotIsSubclassOf()
    {
        // 复刻 reset 命令的筛选语义：从一堆对象里挑出实现了 ICanReset 的。
        var db = NewDb(_ => [], out _);
        object[] items = { db, "not-resettable", 123 };

        // 曾经的坏写法：IsSubclassOf 只认类继承链，对接口恒 false → 一个都选不中（reset 因此空转）。
        var brokenCount = items.Count(x => x.GetType().IsSubclassOf(typeof(ICanReset)));
        Assert.That(brokenCount, Is.EqualTo(0), "IsSubclassOf(接口) 恒 false，钉住这个陷阱");

        // 正确写法：OfType 做接口判断 + 转换。
        var selected = items.OfType<ICanReset>().ToList();
        Assert.That(selected, Has.Count.EqualTo(1));
        Assert.That(selected[0], Is.SameAs(db));
    }

    [Test]
    public void SearchSongExact_DoesNotTreatEmptyInputAsWholeCatalog()
    {
        var db = new SongDb<StubSong>("nonexistent.tsv", "nonexistent.tmp", () =>
        [
            new StubSong { Id = 1, Title = "exact title" },
            new StubSong { Id = 2, Title = "another title" }
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(db.SearchSongExact("".AsMemory()).Count, Is.Zero);
            Assert.That(db.SearchSongExact("exact title".AsMemory()).Select(x => x.Id), Is.EqualTo(new[] { 1L }));
            Assert.That(db.SearchSongExact("id999".AsMemory()), Is.Empty);
        });
    }
}
