using System;
using System.Linq;
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
}
