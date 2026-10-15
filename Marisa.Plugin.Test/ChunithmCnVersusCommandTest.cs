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
    public void NumericScopesUseCountryGradesAndConstantsWithoutMutatingLocalQueryData()
    {
        using var session = new Session();
        var fourteen = session.Core.ParseBatch(session.Message("14"))!;
        var thirteenPlus = session.Core.ParseBatch(session.Message("13+"))!;
        var constant = session.Core.ParseBatch(session.Message("14.3"))!;
        Assert.Multiple(() =>
        {
            Assert.That(fourteen.Charts.Select(chart => chart.Id), Is.EqualTo(new long[] { 2768, 2739, 7, 8 }));
            Assert.That(thirteenPlus.Charts.Select(chart => chart.Id), Is.EqualTo(new long[] { 136 }));
            Assert.That(constant.Charts.Select(chart => chart.Id), Is.EqualTo(new long[] { 2739 }));
            Assert.That(fourteen.Charts.All(chart => chart.Level == "14"), Is.True);
            Assert.That(session.Local.GetSongById(136)!.Constants[0], Is.EqualTo(14));
            Assert.That(session.Local.GetSongById(2768)!.Levels[0], Is.EqualTo("14+"));
            Assert.That(session.Local.GetSongById(2966)!.Title, Is.EqualTo("月葬"));
        });
    }

    [TestCase("月葬")]
    [TestCase("id2966")]
    [TestCase("缺 ULTIMA 黑谱")]
    public async Task SpecifiedUnavailableSongOrDifficultyGetsACountryMessage(string query)
    {
        using var session = new Session();
        var selected = await session.Core.SelectSingle(session.Message(query));
        Assert.That(selected, Is.Null);
        Assert.That(session.Replies().Single().Text, Does.Contain("国服当前曲库未收录").And.Contain("暂不能进行对战"));
    }

    [TestCase("Äventyr", 136, 3, "13+", 13.8)]
    [TestCase("新国服歌", 8, 3, "14", 14.1)]
    [TestCase("id8", 8, 3, "14", 14.1)]
    [TestCase("EXPERT MegiddØ", 2768, 2, "14", 14.4)]
    public async Task SpecifiedSongsUseCountryMetadataIncludingNewSongsMissingLocally(
        string query, long id, int difficulty, string level, double constant)
    {
        using var session = new Session();
        var selection = await session.Core.SelectSingle(session.Message(query));
        var chart = selection!.Charts.Single();
        Assert.Multiple(() =>
        {
            Assert.That(selection.Random, Is.False);
            Assert.That((chart.Id, chart.LevelIndex, chart.Level, chart.Constant), Is.EqualTo((id, difficulty, level, constant)));
            Assert.That(session.Replies(), Is.Empty);
            Assert.That(session.Local.GetSongById(8), Is.Null);
        });
    }

    [Test]
    public void RandomPoolsAndBatchKeepUnplayedCountryChartsButExcludeMissingUltima()
    {
        using var session = new Session();
        var random = session.Core.ParseRandom(session.Message("5 14"), "usage")!;
        var emptyScores = new Dictionary<(long, int), ChunithmScore>();
        var batch = new ChunithmVersusBatch("14", "", "sort", random.Charts,
            new(1, "a", emptyScores), new(2, "b", emptyScores));
        var ultima = session.Core.ParseRandom(session.Message("5 ULTIMA"), "usage")!;
        Assert.Multiple(() =>
        {
            Assert.That(batch.TotalCharts, Is.EqualTo(4));
            Assert.That(batch.Summary.Unplayed, Is.EqualTo(4));
            Assert.That(ultima.Charts, Is.Empty);
            Assert.That(random.Charts.Any(chart => chart.Id == 2966), Is.False);
        });
    }

    [TestCase("single", "缺 ULTIMA")]
    [TestCase("random", "2 14")]
    [TestCase("batch", "14")]
    [TestCase("single-room", "缺 ULTIMA")]
    [TestCase("random-room", "2 14")]
    [TestCase("batch-room", "14")]
    public async Task AllSixInterfaceEntriesUseThePreparedCountrySelection(string entry, string query)
    {
        using var session = new Session();
        var observed = new List<ChunithmVersusChart>();
        var core = session.Core;
        var scores = ChunithmVersusCatalog.StandardCharts(session.Country).ToDictionary(
            chart => (chart.Id, chart.LevelIndex), chart => ChunithmVersusModelTest.Score(chart.Id, chart.LevelIndex, 1_000_000));
        var instrumented = new VersusBattle<ChunithmScore, ChunithmVersusChart>
        {
            CommandPrefix = core.CommandPrefix, MatchScope = core.MatchScope,
            ResolvePlayer = (_, qq) => (new VersusPlayerBinding<ChunithmScore>(qq,
                () => Task.FromResult(new VersusFetchResult<ChunithmScore>(new(qq, "player", scores), null))), null),
            IsOpponentBindingError = core.IsOpponentBindingError, SelectSingle = core.SelectSingle,
            ParseRandom = core.ParseRandom, ParseBatch = core.ParseBatch, ChartKey = core.ChartKey, VersionOf = core.VersionOf,
            ReplySingle = (_, chart, _, _) => { observed.Add(chart); return Task.CompletedTask; },
            ReplyBatch = (_, _, _, _, charts, _, _) => { observed.AddRange(charts); return Task.CompletedTask; },
            OpenRoom = (_, _, plan, _) => observed.AddRange(plan.Charts)
        };
        var preparations = 0;
        ICanBattleWithOthers commands = new CommandOwner(new PreparingVersusBattle(core.MatchScope, () =>
        {
            preparations++;
            return Task.FromResult<VersusBattle>(instrumented);
        }));
        var message = session.Message(query);
        await (entry switch
        {
            "single" => commands.SongVersus(message), "random" => commands.SongVersusRandom(message),
            "batch" => commands.SongVersusBatch(message), "single-room" => commands.SongVersusRoom(message, 99),
            "random-room" => commands.SongVersusRoomRandom(message, 99), _ => commands.SongVersusRoomBatch(message, 99)
        });
        Assert.Multiple(() =>
        {
            Assert.That(preparations, Is.EqualTo(1));
            Assert.That(observed, Is.Not.Empty);
            Assert.That(observed.All(chart => session.Country.Contains(chart.Song)), Is.True);
            Assert.That(observed.Any(chart => chart.Id == 2966 || chart.LevelIndex == 4), Is.False);
        });
    }

    [Test]
    public void TriggerRecognitionDoesNotFetchTheCountryCatalogue()
    {
        using var session = new Session();
        using var http = new HttpTest();
        var battle = ((ICanBattleWithOthers)session.Plugin).Battle;
        Assert.Multiple(() =>
        {
            Assert.That(battle.CanParseScope("14"), Is.True);
            Assert.That(battle.CanParseScope("BLACKBOX"), Is.False);
            Assert.That(http.CallLog, Is.Empty);
        });
    }

    private sealed class CommandOwner(VersusBattle battle) : ICanBattleWithOthers { public VersusBattle Battle => battle; }

    private sealed class Session : IDisposable
    {
        private readonly MessageQueueProvider _queue = new();
        private readonly string _root = Path.Combine(Path.GetTempPath(), nameof(ChunithmCnVersusCommandTest), Guid.NewGuid().ToString("N"));
        public SongDb<ChunithmSong> Local { get; }
        public Chunithm.Chunithm Plugin { get; }
        public IReadOnlyList<ChunithmSong> Country { get; }
        public VersusBattle<ChunithmScore, ChunithmVersusChart> Core { get; }
        public Session()
        {
            Directory.CreateDirectory(_root);
            File.WriteAllText(Path.Combine(_root, "aliases.tsv"), "月葬\t月葬\n");
            Local = new(Path.Combine(_root, "aliases.tsv"), Path.Combine(_root, "extra.tsv"), () =>
            [Chart(136, "Äventyr", 3, "14", 14), Chart(2768, "MegiddØ", 2, "14+", 14.5),
                Chart(2739, "Aether Crest", 2, "14", 14.2), Chart(2966, "月葬", 3, "15+", 15.5),
                ChunithmVersusModelTest.Song(7, "缺 ULTIMA")]);
            Country = [Chart(136, "Äventyr", 3, "13+", 13.8), Chart(2768, "MegiddØ", 2, "14", 14.4),
                Chart(2739, "Aether Crest", 2, "14", 14.3), Chart(7, "缺 ULTIMA", 3, "14", 14.2),
                Chart(8, "新国服歌", 3, "14", 14.1)];
            Plugin = (Chunithm.Chunithm)RuntimeHelpers.GetUninitializedObject(typeof(Chunithm.Chunithm));
            typeof(Chunithm.Chunithm).GetField("<SongDb>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(Plugin, Local);
            Core = (VersusBattle<ChunithmScore, ChunithmVersusChart>)typeof(Chunithm.Chunithm)
                .GetMethod("CreateVersusBattle", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(Plugin, [Country])!;
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
        public void Dispose() { Local.Reset(); Directory.Delete(_root, true); }
    }
}
