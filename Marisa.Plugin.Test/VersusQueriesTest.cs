using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Marisa.BotDriver.DI.Message;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Entity.MessageSender;
using Marisa.Plugin.Shared.MaiMaiDx;
using Marisa.Plugin.Shared.Dialog;
using Marisa.Plugin.Shared.Util.SongDb;
using Marisa.Plugin.Shared.Versus;
using NUnit.Framework;
using SixLabors.ImageSharp;

namespace Marisa.Plugin.Test;

[TestFixture]
public class VersusQueriesTest
{
    [TestCase("BASIC", 1)]
    [TestCase("白金ディスコ", 2)]
    public void CompleteSongTitleWinsBeforeDifficultySyntax(string input, long songId)
    {
        var songs = Songs("BASIC", "白金ディスコ");
        var result = VersusQueries.ResolveSongQuery(songs, input,
            _ => throw new AssertionException("完整歌名不应进入难度解析"),
            _ => throw new AssertionException("完整歌名不应剥离难度前后缀"));

        Assert.Multiple(() =>
        {
            Assert.That(result.Songs.Select(song => song.Id), Is.EqualTo(new[] { songId }));
            Assert.That(result.LevelIndex, Is.EqualTo(3));
            Assert.That(result.Random, Is.False);
        });
    }

    [TestCase("BSC", 0)]
    [TestCase("adv", 1)]
    [TestCase("EXP", 2)]
    [TestCase("MST", 3)]
    [TestCase("ReMAS", 4)]
    public void DifficultyAbbreviationsSelectRandomAtTheirActualLevel(string input, int level)
    {
        var result = Resolve(Songs("Night"), input);
        Assert.Multiple(() =>
        {
            Assert.That(result.Songs, Is.Empty);
            Assert.That(result.LevelIndex, Is.EqualTo(level));
            Assert.That(result.Random, Is.True);
        });
    }

    [Test]
    public void ExactSongAfterAffixWinsBeforeWholeQueryFuzzyMatch()
    {
        var songs = Songs("Night", "EXPERT Night Remix");
        Assert.That(songs.SearchSong("EXPERT Night".AsMemory()).Select(song => song.Id), Is.EqualTo(new[] { 2L }));

        var result = Resolve(songs, "EXPERT Night");
        Assert.Multiple(() =>
        {
            Assert.That(result.Songs.Select(song => song.Id), Is.EqualTo(new[] { 1L }));
            Assert.That(result.LevelIndex, Is.EqualTo(2));
            Assert.That(result.Random, Is.False);
        });
    }

    [Test]
    public void WholeQueryFuzzyMatchWinsBeforeStrippedQueryFuzzyMatch()
    {
        var songs = Songs("白fragment poem", "fragment shadow");
        Assert.That(songs.SearchSong("fragment".AsMemory()), Has.Count.EqualTo(2));

        var result = Resolve(songs, "白fragment");
        Assert.Multiple(() =>
        {
            Assert.That(result.Songs.Select(song => song.Id), Is.EqualTo(new[] { 1L }));
            Assert.That(result.LevelIndex, Is.EqualTo(3));
            Assert.That(result.Random, Is.False);
        });
    }

    [Test]
    public void StrippedFuzzyMatchKeepsDifficultyWhenWholeQueryHasNoMatch()
    {
        var songs = Songs("fragment shadow");
        Assert.That(songs.SearchSong("白shadow".AsMemory()), Is.Empty);

        var result = Resolve(songs, "白shadow");
        Assert.That(result.Songs.Select(song => song.Id), Is.EqualTo(new[] { 1L }));
        Assert.That(result.LevelIndex, Is.EqualTo(4));
    }

    [Test]
    public void BlankQueryAndMissingSongStayDistinct()
    {
        var songs = Songs("Night");
        Assert.That(songs.SearchSong("".AsMemory()), Has.Count.EqualTo(1));
        var blank = Resolve(songs, " \t　");
        var missing = Resolve(songs, "unknown-song");

        Assert.Multiple(() =>
        {
            Assert.That(blank.Songs, Is.Empty);
            Assert.That(blank.LevelIndex, Is.EqualTo(3));
            Assert.That(blank.Random, Is.True);
            Assert.That(missing.Songs, Is.Empty);
            Assert.That(missing.LevelIndex, Is.EqualTo(3));
            Assert.That(missing.Random, Is.False);
        });
    }

    [TestCase("CN Night", 3)]
    [TestCase("999", 3)]
    [TestCase("ID 999", 3)]
    [TestCase("EXPERT CN Night", 2)]
    [TestCase("白CN Night", 4)]
    [TestCase("cn n", 3)]
    [TestCase("白cn n", 4)]
    public void AdditionalCatalogueSongsCanBeSelectedByTitleIdAffixAndFuzzyQuery(string input, int level)
    {
        var songs = Songs("Local Song");
        var additional = new QuerySong { Id = 999, Title = "CN Night" };

        var result = Resolve(songs, input, [additional]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Songs, Has.Count.EqualTo(1));
            Assert.That(result.Songs[0], Is.SameAs(additional));
            Assert.That(result.LevelIndex, Is.EqualTo(level));
            Assert.That(result.Random, Is.False);
        });
    }

    [Test]
    public void AdditionalExactTitleWinsBeforeDifficultySyntax()
    {
        var additional = new QuerySong { Id = 999, Title = "BASIC" };
        var result = VersusQueries.ResolveSongQuery(Songs("Local Song"), "BASIC",
            _ => throw new AssertionException("新增完整歌名应优先于难度语法"),
            _ => throw new AssertionException("新增完整歌名不应剥离难度"), [additional]);

        Assert.That(result.Songs.Single(), Is.SameAs(additional));
        Assert.That(result.Random, Is.False);
    }

    [Test]
    public void AdditionalAffixExactMatchWinsBeforeLocalWholeQueryFuzzyMatch()
    {
        var additional = new QuerySong { Id = 999, Title = "CN Night" };
        var result = Resolve(Songs("EXPERT CN Night Remix"), "EXPERT CN Night", [additional]);

        Assert.That(result.Songs.Single(), Is.SameAs(additional));
        Assert.That(result.LevelIndex, Is.EqualTo(2));
    }

    [Test]
    public void ExistingExactAliasWinsBeforeAdditionalAffixAndFuzzyMatches()
    {
        var songs = Songs("Local Song");
        var aliases = (Dictionary<ReadOnlyMemory<char>, List<ReadOnlyMemory<char>>>)typeof(SongDb<QuerySong>)
            .GetField("_songAlias", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(songs)!;
        aliases.Add("白CN Night".AsMemory(), ["Local Song".AsMemory()]);
        var additional = new QuerySong { Id = 999, Title = "CN Night" };

        var result = Resolve(songs, "白CN Night", [additional]);

        Assert.That(result.Songs.Single(), Is.SameAs(songs.SongList[0]));
        Assert.That(result.LevelIndex, Is.EqualTo(3));
    }

    [Test]
    public void AdditionalSearchMergesInOrderAndDeduplicatesById()
    {
        var songs = Songs("Night Local");
        var sameId = new QuerySong { Id = 1, Title = "Night Local" };
        var newSong = new QuerySong { Id = 999, Title = "Night CN" };

        var result = Resolve(songs, "Night", [sameId, newSong, newSong]);

        Assert.That(result.Songs, Is.EqualTo(new[] { songs.SongList[0], newSong }));
    }

    [TestCase("", true)]
    [TestCase("missing", false)]
    public void AdditionalCatalogueKeepsBlankAndMissingQueriesDistinct(string input, bool random)
    {
        var additional = new QuerySong { Id = 999, Title = "CN Night" };
        var result = Resolve(Songs("Local Song"), input, [additional]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Songs, Is.Empty);
            Assert.That(result.LevelIndex, Is.EqualTo(3));
            Assert.That(result.Random, Is.EqualTo(random));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task SongSelectionUsesTheCustomLookupForAdditionalIdsAndKeepsTheDefaultLookup(bool customLookup)
    {
        var songs = Songs("Local One", "Local Two");
        var additional = new QuerySong { Id = 999, Title = "CN New Song" };
        var (message, queue) = Message("Local");
        var key = (message.GroupInfo?.Id, (long?)message.Sender.Id);
        Assert.That(DialogManager.ContainsDialog(key), Is.False);
        var lookupIds = new List<long>();
        var candidates = songs.SongList.Append(additional).ToArray();

        var selection = songs.MultiPageSelectResult(candidates, message, reply: false, waitRes: true,
            findSong: customLookup ? FindAdditional : null);
        try
        {
            Assert.That(selection.IsCompleted, Is.False);
            Assert.That(DialogManager.TryGetDialog(key, out var handler), Is.True);
            var (choice, _) = Message(customLookup ? "999" : "2");
            await handler!(choice);

            Assert.That(await selection, Is.SameAs(customLookup ? additional : songs.SongList[1]));
            Assert.That(lookupIds, Is.EqualTo(customLookup ? new[] { 999L } : Array.Empty<long>()));
            Assert.That(Replies(queue).Single(), Does.Contain("CN New Song"));
        }
        finally
        {
            DialogManager.RemoveDialog(key);
        }
        return;

        QuerySong? FindAdditional(long id)
        {
            lookupIds.Add(id);
            return id == additional.Id ? additional : null;
        }
    }

    [TestCase("")]
    [TestCase("0 invalid-scope")]
    [TestCase("21")]
    [TestCase("-1")]
    [TestCase("+1")]
    [TestCase("１")]
    [TestCase("١")]
    [TestCase("99999999999999999999999")]
    public void InvalidCountRepliesBeforeResolvingDifficultyOrScope(string command)
    {
        var (message, queue) = Message(command);
        var result = VersusQueries.ParseRandom<int>(message, "test vs n 5 [范围]", 20,
            _ => throw new AssertionException("数量无效时不应解析难度"),
            _ => throw new AssertionException("数量无效时不应选择谱面"),
            _ => throw new AssertionException("数量无效时不应解析范围"),
            _ => throw new AssertionException("数量错误应优先回复"));

        Assert.That(result, Is.Null);
        Assert.That(Replies(queue), Is.EqualTo(new[] { "N 须为 1～20 的整数，用法：test vs n 5 [范围]" }));
    }

    [TestCase("1", 1)]
    [TestCase("20", 20)]
    [TestCase(" 0001　", 1)]
    public void ValidAsciiCountWithoutQueryUsesMaster(string command, int expectedCount)
    {
        var (message, queue) = Message(command);
        var result = VersusQueries.ParseRandom<int>(message, "usage", 20,
            _ => throw new AssertionException("空查询应直接使用 MASTER"),
            level => new[] { level },
            _ => throw new AssertionException("空查询不应进入范围解析"),
            query => $"invalid: {query}");

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Count, Is.EqualTo(expectedCount));
            Assert.That(result.Query, Is.Empty);
            Assert.That(result.Charts, Is.EqualTo(new[] { 3 }));
            Assert.That(Replies(queue), Is.Empty);
        });
    }

    [TestCase("1BSC", "BSC", 0)]
    [TestCase("2 exp", "exp", 2)]
    [TestCase("3 白谱", "白谱", 4)]
    public void RandomDifficultyQuerySkipsScopeParsing(string command, string query, int level)
    {
        var (message, queue) = Message(command);
        var result = VersusQueries.ParseRandom<int>(message, "usage", 20, Difficulty,
            selectedLevel => new[] { selectedLevel },
            _ => throw new AssertionException("难度词不应进入范围解析"),
            invalid => $"invalid: {invalid}");

        Assert.That(result!.Query, Is.EqualTo(query));
        Assert.That(result.Charts, Is.EqualTo(new[] { level }));
        Assert.That(Replies(queue), Is.Empty);
    }

    [Test]
    public void ValidScopePreservesChartsAndOrderForBothSelectionModes()
    {
        var charts = new[] { 42, 7 };
        var (randomMessage, randomQueue) = Message(" 2　valid-scope ");
        var (batchMessage, batchQueue) = Message("  valid-scope　");
        var parsedQueries = new List<string>();

        var random = VersusQueries.ParseRandom<int>(randomMessage, "usage", 20, Difficulty,
            _ => throw new AssertionException("范围查询不应选择难度谱面"), ParseScope, query => $"invalid: {query}");
        var batch = VersusQueries.ParseBatch(batchMessage, ParseScope);

        Assert.Multiple(() =>
        {
            Assert.That(random!.Count, Is.EqualTo(2));
            Assert.That(random.Query, Is.EqualTo("valid-scope"));
            Assert.That(random.Charts, Is.SameAs(charts));
            Assert.That(batch!.Query, Is.EqualTo("valid-scope"));
            Assert.That(batch.SortLabel, Is.EqualTo("歌曲 ID 升序"));
            Assert.That(batch.Charts, Is.SameAs(charts));
            Assert.That(parsedQueries, Is.EqualTo(new[] { "valid-scope", "valid-scope" }));
            Assert.That(Replies(randomQueue), Is.Empty);
            Assert.That(Replies(batchQueue), Is.Empty);
        });
        return;

        (VersusScopeSelection<int>? Scope, string? Error) ParseScope(string query)
        {
            parsedQueries.Add(query);
            return (new(charts, "歌曲 ID 升序"), "应忽略成功解析附带的错误文本");
        }
    }

    [Test]
    public void ValidEmptyScopeIsAcceptedForRandomButRejectedForBatch()
    {
        var (randomMessage, randomQueue) = Message("2 empty-scope");
        var (batchMessage, batchQueue) = Message("  empty-scope  ");
        var random = VersusQueries.ParseRandom<int>(randomMessage, "usage", 20, Difficulty,
            _ => throw new AssertionException("范围查询不应选择难度谱面"), EmptyScope, query => $"invalid: {query}");
        var batch = VersusQueries.ParseBatch(batchMessage, EmptyScope);

        Assert.Multiple(() =>
        {
            Assert.That(random, Is.Not.Null);
            Assert.That(random!.Charts, Is.Empty);
            Assert.That(Replies(randomQueue), Is.Empty);
            Assert.That(batch, Is.Null);
            Assert.That(Replies(batchQueue), Is.EqualTo(new[] { "没有找到 empty-scope 对应的谱面" }));
        });
        return;

        static (VersusScopeSelection<int>? Scope, string? Error) EmptyScope(string _) => (new([], "sort"), null);
    }

    [Test]
    public void InvalidScopeUsesRandomErrorFormatterAndBatchScopeError()
    {
        var (randomMessage, randomQueue) = Message("2 unknown scope");
        var (batchMessage, batchQueue) = Message(" unknown scope ");
        var random = VersusQueries.ParseRandom<int>(randomMessage, "usage", 20, Difficulty,
            _ => throw new AssertionException("无效范围不应选择难度谱面"), InvalidScope, query => $"无法解析难度或完成表范围：{query}");
        var batch = VersusQueries.ParseBatch(batchMessage, InvalidScope);

        Assert.Multiple(() =>
        {
            Assert.That(random, Is.Null);
            Assert.That(Replies(randomQueue), Is.EqualTo(new[] { "无法解析难度或完成表范围：unknown scope" }));
            Assert.That(batch, Is.Null);
            Assert.That(Replies(batchQueue), Is.EqualTo(new[] { "范围解析的详细错误：unknown scope" }));
        });
        return;

        static (VersusScopeSelection<int>? Scope, string? Error) InvalidScope(string query) =>
            (null, $"范围解析的详细错误：{query}");
    }

    private static (List<QuerySong> Songs, int LevelIndex, bool Random) Resolve(
        SongDb<QuerySong> songs, string input, IReadOnlyList<QuerySong>? additionalSongs = null) =>
        VersusQueries.ResolveSongQuery(songs, input, Difficulty, StripAffix, additionalSongs);

    private static int? Difficulty(string query) =>
        PlateData.DifficultyAliasMap.TryGetValue(query, out var level) ? level : null;

    private static (int Level, ReadOnlyMemory<char> Remaining)? StripAffix(ReadOnlyMemory<char> query) =>
        PlateData.TryStripDifficultyAffix(query, out var level, out var rest) ? (level, rest) : null;

    private static SongDb<QuerySong> Songs(params string[] titles)
    {
        var catalog = titles.Select((title, index) => new QuerySong { Id = index + 1, Title = title }).ToList();
        var songs = new SongDb<QuerySong>("unused.tsv", "unused.tsv", () => catalog);
        var aliases = catalog.ToDictionary(song => song.Title.AsMemory(),
            song => new List<ReadOnlyMemory<char>> { song.Title.AsMemory() });
        typeof(SongDb<QuerySong>).GetField("_songAlias", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(songs, aliases);
        return songs;
    }

    private static (Message Message, MessageQueueProvider Queue) Message(string command)
    {
        var queue = new MessageQueueProvider();
        var message = new Message(new MessageSenderProvider(queue), new MessageDataId(1, 1), new MessageDataText(command))
        {
            Type = MessageType.FriendMessage,
            Sender = new SenderInfo(100, "tester"),
            Command = command.AsMemory()
        };
        return (message, queue);
    }

    private static string[] Replies(MessageQueueProvider queue)
    {
        var replies = new List<string>();
        while (queue.SendQueue.Reader.TryRead(out var reply)) replies.Add(reply.MessageChain.Text);
        return replies.ToArray();
    }

    private sealed class QuerySong : Song
    {
        public override string MaxLevel() => "14";
        public override string GetImage() => throw new NotSupportedException();
        public override Image GetCover() => throw new NotSupportedException();
    }
}
