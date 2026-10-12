using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Marisa.BotDriver.DI.Message;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Entity.MessageSender;
using Marisa.BotDriver.Plugin;
using Marisa.Plugin.Shared.Dialog;
using Marisa.Plugin.Shared.MaiMaiDx;
using Marisa.Plugin.Shared.Versus;
using Room = Marisa.Plugin.Shared.Versus.VersusRoom<Marisa.Plugin.Shared.MaiMaiDx.SongScore>;
using NUnit.Framework;
using static Marisa.Plugin.Test.MaiMaiVersusMultiTest;

namespace Marisa.Plugin.Test;

public class MaiMaiVersusRoomTest
{
    private static long _nextGroup = 900000;

    [Test]
    public void InviteesJoinWithTheHostAndTheRoomCapsAtEight()
    {
        var room = new Room(1, [2, 3, 2]);
        Assert.That(room.Count, Is.EqualTo(3));

        Assert.Multiple(() =>
        {
            Assert.That(room.Join(2), Is.EqualTo(Room.JoinResult.AlreadyIn));
            for (var qq = 4; qq <= 8; qq++) Assert.That(room.Join(qq), Is.EqualTo(Room.JoinResult.Joined));
            Assert.That(room.Join(9), Is.EqualTo(Room.JoinResult.Full));
        });
    }

    [Test]
    public void HostLeavingHandsTheRoomToTheEarliestMemberAndTheLastLeaverDissolvesIt()
    {
        var room = new Room(1, [2, 3]);

        Assert.Multiple(() =>
        {
            Assert.That(room.Leave(9), Is.EqualTo(Room.LeaveResult.NotIn));
            Assert.That(room.Leave(1), Is.EqualTo(Room.LeaveResult.HostChanged));
            Assert.That(room.Host, Is.EqualTo(2));
            Assert.That(room.Leave(3), Is.EqualTo(Room.LeaveResult.Left));
            Assert.That(room.Leave(2), Is.EqualTo(Room.LeaveResult.Dissolved));
            Assert.That(room.Join(4), Is.EqualTo(Room.JoinResult.Closed));
        });
    }

    [Test]
    public void OnlyTheHostStartsTheFirstRoundThenAnyMemberCanContinue()
    {
        var room = new Room(1, [2]);

        Assert.Multiple(() =>
        {
            Assert.That(room.TryStart(3, out _), Is.EqualTo(Room.StartResult.NotMember));
            Assert.That(room.TryStart(2, out _), Is.EqualTo(Room.StartResult.NotHost));
            Assert.That(room.TryStart(1, out var roster), Is.EqualTo(Room.StartResult.Started));
            Assert.That(roster, Is.EqualTo(new long[] { 1, 2 }));
            Assert.That(room.TryStart(1, out _), Is.EqualTo(Room.StartResult.Running));
        });

        Finish(room, Round([Chart(1)], Player(1, (1, 100.0)), Player(2, (1, 99.0))));

        Assert.Multiple(() =>
        {
            Assert.That(room.Round, Is.EqualTo(1));
            Assert.That(room.TryStart(2, out _), Is.EqualTo(Room.StartResult.Started));
        });
    }

    [Test]
    public void FailedRoundsDoNotCountAndAloneTheHostCannotStart()
    {
        var room = new Room(1, [2]);
        room.TryStart(1, out _);
        room.EndRound(null);
        room.Leave(2);

        Assert.Multiple(() =>
        {
            Assert.That(room.Round, Is.Zero);
            Assert.That(room.TryStart(1, out _), Is.EqualTo(Room.StartResult.TooFew));
        });
    }

    [Test]
    public void ExpiresAfterTenIdleMinutesButNotDuringARound()
    {
        var time = new ManualTime();
        var room = new Room(1, [2], time);

        time.Advance(TimeSpan.FromMinutes(9));
        room.Join(3);
        time.Advance(TimeSpan.FromMinutes(10));
        Assert.That(room.IsExpired, Is.False);

        room.TryStart(1, out _);
        time.Advance(TimeSpan.FromMinutes(11));
        Assert.That(room.IsExpired, Is.False);

        room.EndRound(null);
        time.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));
        Assert.That(room.IsExpired, Is.True);
    }

    [Test]
    public void ScoresAreReusedForSixtySeconds()
    {
        var time = new ManualTime();
        var room = new Room(1, [2], time);
        room.CacheScores(ToRoomPlayer(Player(1)));

        time.Advance(TimeSpan.FromSeconds(60));
        Assert.That(room.TryGetScores(1, out _), Is.True);
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.That(room.TryGetScores(1, out _), Is.False);
    }

    [Test]
    public void StandingsUseMatchupRateSoLateJoinersAreComparable()
    {
        var room = new Room(1, [2]);

        room.TryStart(1, out _);
        Finish(room, Round([Chart(1), Chart(2)], Player(1, (1, 100.0), (2, 100.0)), Player(2, (1, 99.0))));
        room.Join(3);
        room.TryStart(3, out _);
        Finish(room, Round([Chart(3)], Player(1, (3, 99.0)), Player(2, (3, 98.0)), Player(3, (3, 100.0))));
        room.Leave(2);

        Assert.That(room.Standings().Select(x => (x.Name, x.Wins, x.Matchups, x.Rounds, x.Left)), Is.EqualTo(new[]
        {
            ("p3", 2.0, 2, 1, false),
            ("p1", 3.0, 4, 2, false),
            ("p2", 0.0, 4, 2, true)
        }));
    }

    [Test]
    public void PickedChartsDoNotRepeatUntilEveryChartWasCompared()
    {
        var room    = new Room(1, [2]);
        var charts  = Enumerable.Range(1, 3).Select(id => Chart(id)).ToArray();
        var players = new[] { Player(1, (1, 100.0), (2, 100.0), (3, 100.0)), Player(2, (1, 99.0), (2, 99.0), (3, 99.0)) }.Select(ToRoomPlayer).ToArray();
        var random  = new Random(5);

        var first  = room.PickCharts(charts, players, x => (x.Song.Id, x.LevelIdx), x => x.Constant, 2, random).Select(x => x.Song.Id).ToArray();
        var second = room.PickCharts(charts, players, x => (x.Song.Id, x.LevelIdx), x => x.Constant, 2, random).Select(x => x.Song.Id).ToArray();
        var third  = room.PickCharts(charts, players, x => (x.Song.Id, x.LevelIdx), x => x.Constant, 2, random).Select(x => x.Song.Id).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(first.Concat(second), Is.EquivalentTo(new long[] { 1, 2, 3 }));
            Assert.That(third, Has.Length.EqualTo(2));
        });
    }

    [Test]
    public void CompletedRoundSnapshotDoesNotChangeWithLaterRoundsOrLeavers()
    {
        var room = new Room(1, [2]);
        room.TryStart(1, out _);
        var first = Finish(room, Round([Chart(1)], Player(1, (1, 100.0)), Player(2, (1, 99.0))));
        room.TryStart(2, out _);
        Finish(room, Round([Chart(2)], Player(1, (2, 98.0)), Player(2, (2, 100.0))));
        room.Leave(2);

        Assert.Multiple(() =>
        {
            Assert.That(first.Round, Is.EqualTo(1));
            Assert.That(first.Standings.Select(s => (s.Name, s.Wins, s.Rounds, s.Left)), Is.EqualTo(new[]
            {
                ("p1", 1.0, 1, false), ("p2", 0.0, 1, false)
            }));
            Assert.That(room.Round, Is.EqualTo(2));
            Assert.That(room.Standings().Single(s => s.Name == "p2").Left, Is.True);
        });
    }

    [Test]
    public async Task FailedSharedRoundKeepsHostPermissionAndCachesSuccessfulFetches()
    {
        using var session = new SharedSession { FailingPlayer = 2 };
        session.Open();
        Assert.That(await session.Send(1, "开始"), Is.EqualTo(MarisaPluginTaskState.ToBeContinued));
        Assert.That(session.TextReplies(), Does.Contain("取到成绩的玩家不足 2 人"));
        await session.Send(2, "开始");
        Assert.That(session.Renders, Is.Empty);

        session.FailingPlayer = null;
        await session.Send(1, "开始");
        await session.Send(2, "开始");
        Assert.Multiple(() =>
        {
            Assert.That(session.Renders.Select(x => x.Round), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(session.Fetches[1], Is.EqualTo(1));
            Assert.That(session.Fetches[2], Is.EqualTo(2));
            Assert.That(session.Renders[0].Standings, Is.Empty);
            Assert.That(session.Renders[1].Standings.Select(s => s.Rounds), Is.EqualTo(new[] { 2, 2 }));
        });
    }

    [Test]
    public async Task JoiningDuringFetchOnlyChangesTheFollowingRoster()
    {
        using var session = new SharedSession { FetchGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        session.Open();
        var first = session.Send(1, "开始");
        await session.Send(3, "加入");
        session.FetchGate.SetResult(true);
        await first;
        await session.Send(3, "开始");

        Assert.Multiple(() =>
        {
            Assert.That(session.Renders[0].Players, Is.EqualTo(new long[] { 1, 2 }));
            Assert.That(session.Renders[1].Players, Is.EqualTo(new long[] { 1, 2, 3 }));
        });
    }

    [TestCase(false, "开始")]
    [TestCase(false, "vs")]
    [TestCase(false, " VS ")]
    [TestCase(false, "对战")]
    [TestCase(true, "开始")]
    [TestCase(true, "vs")]
    [TestCase(true, " VS ")]
    [TestCase(true, "对战")]
    public async Task SharedStartAliasesKeepHostFirstAndAllowMembersAfterSuccess(bool chunithm, string command)
    {
        using var session = new SharedSession(chunithm);
        session.Open();
        session.TextReplies();

        Assert.That(await session.Send(2, command), Is.EqualTo(MarisaPluginTaskState.ToBeContinued));
        Assert.That(await session.Send(9, command), Is.EqualTo(MarisaPluginTaskState.NoResponse));
        Assert.That(session.Renders, Is.Empty);
        Assert.That(session.Fetches, Is.Empty);
        Assert.That(session.TextReplies(), Is.Empty);

        Assert.That(await session.Send(1, command), Is.EqualTo(MarisaPluginTaskState.ToBeContinued));
        Assert.That(await session.Send(2, command), Is.EqualTo(MarisaPluginTaskState.ToBeContinued));
        Assert.Multiple(() =>
        {
            Assert.That(session.Renders.Select(result => result.Round), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(session.Renders.All(result => result.Players.SequenceEqual(new long[] { 1, 2 })), Is.True);
            Assert.That(session.Renders[1].Standings.Select(standing => standing.Rounds), Is.EqualTo(new[] { 2, 2 }));
            Assert.That(session.HasDialog, Is.True);
        });
    }

    [TestCase(false, "取消")]
    [TestCase(false, "解散")]
    [TestCase(false, " 结束 ")]
    [TestCase(true, "取消")]
    [TestCase(true, "解散")]
    [TestCase(true, " 结束 ")]
    public async Task SharedCancelAliasesOnlyAllowHostAndReleaseTheRoom(bool chunithm, string command)
    {
        using var session = new SharedSession(chunithm);
        session.Open();
        session.TextReplies();

        Assert.That(await session.Send(2, command), Is.EqualTo(MarisaPluginTaskState.NoResponse));
        Assert.That(await session.Send(9, command), Is.EqualTo(MarisaPluginTaskState.NoResponse));
        Assert.That(session.HasDialog, Is.True);
        Assert.That(session.TextReplies(), Is.Empty);

        Assert.That(await session.Send(1, command), Is.EqualTo(MarisaPluginTaskState.CompletedTask));
        Assert.That(session.TextReplies(), Is.EqualTo("房间已解散"));
        Assert.That(session.HasDialog, Is.False);
        session.Open();
        Assert.That(session.HasDialog, Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task SharedAliasesDoNotConsumeSubstringsOrGameCommands(bool chunithm)
    {
        using var session = new SharedSession(chunithm);
        session.Open();
        session.TextReplies();

        foreach (var command in new[] { "vs n 5", "mai vs n 5", "chu vs n 5", "我要结束了", "解散吧", "开始吧", "对战中", "v s", "ｖｓ", "vſ" })
        {
            Assert.That(await session.Send(1, command), Is.EqualTo(MarisaPluginTaskState.NoResponse), command);
            Assert.That(session.HasDialog, Is.True, command);
        }
        Assert.That(session.Renders, Is.Empty);
        Assert.That(session.Fetches, Is.Empty);
        Assert.That(session.TextReplies(), Is.Empty);
        await session.Send(1, "vs");
        Assert.That(session.Renders.Select(result => result.Round), Is.EqualTo(new[] { 1 }));
    }

    [Test]
    public async Task AnnouncementTellsInviteesHowToLeave()
    {
        var session = new Session(host: 1, joined: [2, 3], unbound: [4]);
        Assert.That(session.Open(), Is.True);

        var reply = session.Replies().Single();
        Assert.Multiple(() =>
        {
            Assert.That(reply.Messages.OfType<MessageDataAt>().Select(x => x.Target), Is.EqualTo(new long[] { 2, 3, 4 }));
            Assert.That(reply.Text, Does.StartWith("多人对战：测试（3/8）"));
            Assert.That(reply.Text, Does.Contain("已加入，不参加可以发送「退出」"));
            Assert.That(reply.Text, Does.Contain("需要先绑定查分器才能加入"));
            Assert.That(reply.Text, Does.EndWith("发送「加入」参加，房主发送「开始」（vs/对战）开打，「取消」（解散/结束）关房"));
        });
        await session.Close();
    }

    [Test]
    public async Task OrdinaryMessagesAndOtherPeoplesCommandsPassThrough()
    {
        var session = new Session(host: 1, joined: [2]);
        session.Open();

        session.Replies();

        Assert.That(await session.Send(1, "随便聊聊"), Is.EqualTo(MarisaPluginTaskState.NoResponse));
        Assert.That(await session.Send(2, "取消"), Is.EqualTo(MarisaPluginTaskState.NoResponse));
        Assert.That(await session.Send(9, "退出"), Is.EqualTo(MarisaPluginTaskState.NoResponse));
        Assert.That(await session.Send(9, "开始"), Is.EqualTo(MarisaPluginTaskState.NoResponse));
        Assert.That(await session.Send(2, "开始"), Is.EqualTo(MarisaPluginTaskState.ToBeContinued));
        Assert.That(session.TextReplies(), Is.Empty);
        await session.Close();
    }

    [Test]
    public async Task LeavingHandsOverTheRoomUntilItIsEmpty()
    {
        var session = new Session(host: 1, joined: [2, 3]);
        session.Open();
        session.Replies();

        Assert.That(await session.Send(2, "退出"), Is.EqualTo(MarisaPluginTaskState.ToBeContinued));
        Assert.That(session.TextReplies(), Is.EqualTo("已退出"));
        Assert.That(await session.Send(1, "退出"), Is.EqualTo(MarisaPluginTaskState.ToBeContinued));
        Assert.That(session.TextReplies(), Is.EqualTo("已退出，房主转给 "));
        Assert.That(await session.Send(3, "开始"), Is.EqualTo(MarisaPluginTaskState.ToBeContinued));
        Assert.That(session.TextReplies(), Is.EqualTo("至少需要 2 人才能开始"));
        Assert.That(await session.Send(3, "退出"), Is.EqualTo(MarisaPluginTaskState.CompletedTask));
        Assert.That(session.TextReplies(), Is.EqualTo("房间已解散"));
    }

    [Test]
    public async Task HostCancelsAndTheGroupCanOpenAnotherRoom()
    {
        var session = new Session(host: 1, joined: [2]);
        session.Open();
        Assert.That(session.Open(), Is.False);
        Assert.That(session.Replies().Last().Text, Is.EqualTo("群里已经有进行中的房间或游戏"));

        Assert.That(await session.Send(1, "取消"), Is.EqualTo(MarisaPluginTaskState.CompletedTask));
        Assert.That(session.TextReplies(), Is.EqualTo("房间已解散"));
        Assert.That(session.Open(), Is.True);
        await session.Close();
    }

    private static VersusPlayer<SongScore> ToRoomPlayer(MaiVersusMulti.Player player) =>
        new(player.Qq, player.Name, player.Scores);

    private static VersusRoomSnapshot Finish(Room room, MaiVersusMulti result) =>
        room.EndRound(result.Players.Select((player, i) =>
            new VersusRoundPlayer(player.Qq, player.Name, result.Totals[i].Points, result.Totals[i].Matchups)).ToArray());

    private sealed class SharedSession(bool chunithm = false) : IDisposable
    {
        private readonly MessageQueueProvider _queue = new();
        private readonly MarisaPluginBase _plugin = (MarisaPluginBase)RuntimeHelpers.GetUninitializedObject(
            chunithm ? typeof(Chunithm.Chunithm) : typeof(MaiMaiDx.MaiMaiDx));
        private readonly long _group = Interlocked.Increment(ref _nextGroup);
        private (long?, long?) Key => (_group, null);
        public bool HasDialog => DialogManager.ContainsDialog(Key);

        public long? FailingPlayer { get; set; }
        public TaskCompletionSource<bool>? FetchGate { get; set; }
        public Dictionary<long, int> Fetches { get; } = [];
        public List<(int Round, long[] Players, IReadOnlyList<VersusStanding> Standings)> Renders { get; } = [];

        public void Open()
        {
            VersusRoomSession.Open<int, int, IReadOnlyList<VersusRoundPlayer>>(
                _plugin, Message(1, ""), (new long[] { 2 }, Array.Empty<long>()), new("测试", new[] { 1 }, 1),
                id => (id, 3), _ => 14.0, (_, _) => null, Fetch,
                (_, players) =>
                {
                    var cells = VersusRules.ScoreChart(players.Select(p => (double?)p.Scores[(1, 3)]).ToArray());
                    return players.Select((p, i) => new VersusRoundPlayer(p.Qq, p.Name, cells[i].Points, cells[i].Matchups)).ToArray();
                },
                result => result,
                (_, result, round, standings) =>
                {
                    Renders.Add((round, result.Select(p => p.Qq).ToArray(), standings));
                    return Task.CompletedTask;
                });
        }

        public async Task<MarisaPluginTaskState> Send(long sender, string text)
        {
            Assert.That(DialogManager.TryGetDialog(Key, out var handler), Is.True);
            var result = await handler!(Message(sender, text));
            if (result is MarisaPluginTaskState.CompletedTask or MarisaPluginTaskState.Canceled) DialogManager.RemoveDialog(Key);
            return result;
        }

        public string TextReplies()
        {
            var texts = new List<string>();
            while (_queue.SendQueue.Reader.TryRead(out var reply)) texts.Add(reply.MessageChain.Text);
            return string.Join("", texts);
        }

        public void Dispose() => DialogManager.RemoveDialog(Key);

        private async Task<VersusFetchResult<int>> Fetch(Message _, long qq)
        {
            Fetches[qq] = Fetches.GetValueOrDefault(qq) + 1;
            if (FetchGate is not null) await FetchGate.Task;
            return FailingPlayer == qq
                ? new(null, "取分失败")
                : new(new VersusPlayer<int>(qq, $"p{qq}", new Dictionary<(long, int), int> { [(1, 3)] = (int)qq }), null);
        }

        private Message Message(long sender, string text) =>
            new(new MessageSenderProvider(_queue), new MessageDataId(1, 1), new MessageDataText(text))
            {
                Sender = new SenderInfo(sender, $"user{sender}"),
                GroupInfo = new GroupInfo(_group, "group", null),
                Type = MessageType.GroupMessage,
                Command = text.AsMemory()
            };
    }

    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UnixEpoch;

        public void Advance(TimeSpan span) => _now += span;

        public override DateTimeOffset GetUtcNow() => _now;
    }

    private sealed class Session(long host, long[] joined, long[]? unbound = null)
    {
        private readonly MessageQueueProvider _queue = new();
        private readonly MaiMaiDx.MaiMaiDx _plugin = (MaiMaiDx.MaiMaiDx)RuntimeHelpers.GetUninitializedObject(typeof(MaiMaiDx.MaiMaiDx));
        private readonly long _group = Interlocked.Increment(ref _nextGroup);

        private (long?, long?) Key => (_group, null);

        public bool Open()
        {
            var hadRoom  = DialogManager.ContainsDialog(Key);
            var planType = typeof(MaiMaiDx.MaiMaiDx).GetNestedType("VersusRoomPlan", BindingFlags.NonPublic)!;
            var plan     = Activator.CreateInstance(planType, "测试", "", Array.Empty<(double, int, MaiMaiSong)>(), 1, true);
            typeof(MaiMaiDx.MaiMaiDx).GetMethod("OpenVersusRoom", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(_plugin, [Message(host, "mai vs 开房"), (joined, unbound ?? []), plan]);
            return !hadRoom && DialogManager.ContainsDialog(Key);
        }

        public async Task<MarisaPluginTaskState> Send(long sender, string text)
        {
            Assert.That(DialogManager.TryGetDialog(Key, out var handler), Is.True);
            var result = await handler!(Message(sender, text));
            if (result is MarisaPluginTaskState.CompletedTask or MarisaPluginTaskState.Canceled) DialogManager.RemoveDialog(Key);
            return result;
        }

        public Task Close()
        {
            DialogManager.RemoveDialog(Key);
            return Task.CompletedTask;
        }

        public List<MessageChain> Replies()
        {
            var output = new List<MessageChain>();
            while (_queue.SendQueue.Reader.TryRead(out var reply)) output.Add(reply.MessageChain);
            return output;
        }

        public string TextReplies() => string.Join("", Replies().Select(x => x.Text));

        private Message Message(long sender, string text) =>
            new(new MessageSenderProvider(_queue), new MessageDataId(1, 1), new MessageDataText(text))
            {
                Sender    = new SenderInfo(sender, $"user{sender}"),
                GroupInfo = new GroupInfo(_group, "group", null),
                Type      = MessageType.GroupMessage,
                Command   = text.AsMemory()
            };
    }
}
