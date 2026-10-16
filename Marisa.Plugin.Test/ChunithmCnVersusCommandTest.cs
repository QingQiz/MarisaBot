using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Flurl.Http.Testing;
using Marisa.BotDriver.DI.Message;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Entity.MessageSender;
using Marisa.Plugin.Shared.Chunithm;
using Marisa.Plugin.Shared.Interface;
using Marisa.Plugin.Shared.Util.SongDb;
using Marisa.Plugin.Shared.Versus;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class ChunithmCnVersusCommandTest
{
    private static ChunithmSong Chart(long id, string title, int difficulty, string level, double constant)
    {
        var song = ChunithmVersusModelTest.Song(id, title, names: [ChunithmSong.LevelLabel[difficulty]]);
        song.Levels[0] = level;
        song.Constants[0] = constant;
        return song;
    }

    [Test]
    public void NumericScopesUseNationalLevelsAndConstants()
    {
        using var session = new Session();
        var fourteen = session.Battle.ParseBatch(session.Message("14"))!;
        var thirteenPlus = session.Battle.ParseBatch(session.Message("13+"))!;
        var constant = session.Battle.ParseBatch(session.Message("14.3"))!;
        Assert.Multiple(() =>
        {
            Assert.That(fourteen.Charts.Select(chart => chart.Id), Is.EqualTo(new long[] { 2768, 2739, 7 }));
            Assert.That(thirteenPlus.Charts.Select(chart => chart.Id), Is.EqualTo(new long[] { 136 }));
            Assert.That(constant.Charts.Select(chart => chart.Id), Is.EqualTo(new long[] { 2739 }));
            Assert.That(fourteen.Charts.All(chart => chart.Level == "14"), Is.True);
            Assert.That(session.Local.GetSongById(136)!.Constants[0], Is.EqualTo(14));
        });
    }

    [TestCase("月葬")]
    [TestCase("id2966")]
    [TestCase("缺 ULTIMA 黑谱")]
    public async Task UnavailableSongOrDifficultyIsReported(string query)
    {
        using var session = new Session();
        var selected = await session.Battle.SelectSingle(session.Message(query));
        Assert.That(selected, Is.Null);
        Assert.That(session.Replies().Single().Text, Does.Contain("国服暂未收录"));
    }

    [TestCase("Äventyr", 136, 3, "13+", 13.8)]
    [TestCase("EXPERT MegiddØ", 2768, 2, "14", 14.4)]
    public async Task SpecifiedSongsUseNationalMetadata(string query, long id, int difficulty, string level, double constant)
    {
        using var session = new Session();
        var selection = await session.Battle.SelectSingle(session.Message(query));
        var chart = selection!.Charts.Single();
        Assert.Multiple(() =>
        {
            Assert.That(selection.Random, Is.False);
            Assert.That((chart.Id, chart.LevelIndex, chart.Level, chart.Constant), Is.EqualTo((id, difficulty, level, constant)));
            Assert.That(session.Replies(), Is.Empty);
        });
    }

    [Test]
    public void RandomPoolsKeepUnplayedNationalChartsButExcludeMissingUltima()
    {
        using var session = new Session();
        var random = session.Battle.ParseRandom(session.Message("5 14"), "usage")!;
        var emptyScores = new Dictionary<(long, int), ChunithmScore>();
        var batch = new ChunithmVersusBatch("14", "", "sort", random.Charts, new(1, "a", emptyScores), new(2, "b", emptyScores));
        var ultima = session.Battle.ParseRandom(session.Message("5 ULTIMA"), "usage")!;
        Assert.Multiple(() =>
        {
            Assert.That(batch.TotalCharts, Is.EqualTo(3));
            Assert.That(batch.Summary.Unplayed, Is.EqualTo(3));
            Assert.That(ultima.Charts, Is.Empty);
            Assert.That(random.Charts.Any(chart => chart.Id == 2966), Is.False);
        });
    }

    [Test]
    public void TriggerMatchingUsesLocalDataWithoutFetchingNationalCatalog()
    {
        using var session = new Session(national: false);
        using var http = new HttpTest();
        Assert.Multiple(() =>
        {
            Assert.That(session.Battle.CanParseScope("14"), Is.True);
            Assert.That(session.Battle.CanParseScope("音击舞萌14"), Is.True);
            Assert.That(session.Battle.CanParseScope("BLACKBOX"), Is.False);
            Assert.That(http.CallLog, Is.Empty);
        });
    }

    private sealed class Session : IDisposable
    {
        private readonly MessageQueueProvider _queue = new();
        private readonly string _root = Path.Combine(Path.GetTempPath(), nameof(ChunithmCnVersusCommandTest), Guid.NewGuid().ToString("N"));
        private readonly IDisposable? _national;
        public SongDb<ChunithmSong> Local { get; }
        public VersusBattle<ChunithmScore, ChunithmVersusChart> Battle { get; }

        public Session(bool national = true)
        {
            Directory.CreateDirectory(_root);
            File.WriteAllText(Path.Combine(_root, "aliases.tsv"), "月葬\t月葬\n");
            Local = new(Path.Combine(_root, "aliases.tsv"), Path.Combine(_root, "extra.tsv"), () =>
            [
                Chart(136, "Äventyr", 3, "14", 14), Chart(2768, "MegiddØ", 2, "14+", 14.5),
                Chart(2739, "Aether Crest", 2, "14", 14.2), Chart(2966, "月葬", 3, "15+", 15.5),
                ChunithmVersusModelTest.Song(7, "缺 ULTIMA", genre: "ゲキマイ")
            ]);
            if (national)
                _national = ChunithmVersusModelTest.UseNationalSongs(
                [
                    Chart(136, "Äventyr", 3, "13+", 13.8), Chart(2768, "MegiddØ", 2, "14", 14.4),
                    Chart(2739, "Aether Crest", 2, "14", 14.3), Chart(7, "缺 ULTIMA", 3, "14", 14.2)
                ]);
            var plugin = (Chunithm.Chunithm)RuntimeHelpers.GetUninitializedObject(typeof(Chunithm.Chunithm));
            typeof(Chunithm.Chunithm).GetField("<SongDb>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(plugin, Local);
            Battle = (VersusBattle<ChunithmScore, ChunithmVersusChart>)((ICanBattleWithOthers)plugin).Battle;
        }

        public Message Message(string query) => new(new MessageChain(new MessageDataId(1, 1), new MessageDataText(query), new MessageDataAt(2)), new MessageSenderProvider(_queue))
        {
            Sender = new SenderInfo(1, "self"), Type = MessageType.GroupMessage, GroupInfo = new GroupInfo(998877, "group", null), Command = query.AsMemory()
        };

        public List<MessageChain> Replies()
        {
            var replies = new List<MessageChain>();
            while (_queue.SendQueue.Reader.TryRead(out var reply)) replies.Add(reply.MessageChain);
            return replies;
        }

        public void Dispose()
        {
            _national?.Dispose();
            Local.Reset();
            Directory.Delete(_root, true);
        }
    }
}
