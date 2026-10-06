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
using NUnit.Framework;
using static Marisa.Plugin.Test.MaiMaiVersusMultiTest;

namespace Marisa.Plugin.Test;

public class MaiMaiVersusRoomTest
{
    private static long _nextGroup = 900000;

    [Test]
    public void InviteesJoinWithTheHostAndTheRoomCapsAtEight()
    {
        var room = new MaiVersusRoom(1, [2, 3, 2]);
        Assert.That(room.Count, Is.EqualTo(3));

        Assert.Multiple(() =>
        {
            Assert.That(room.Join(2), Is.EqualTo(MaiVersusRoom.JoinResult.AlreadyIn));
            for (var qq = 4; qq <= 8; qq++) Assert.That(room.Join(qq), Is.EqualTo(MaiVersusRoom.JoinResult.Joined));
            Assert.That(room.Join(9), Is.EqualTo(MaiVersusRoom.JoinResult.Full));
        });
    }

    [Test]
    public void HostLeavingHandsTheRoomToTheEarliestMemberAndTheLastLeaverDissolvesIt()
    {
        var room = new MaiVersusRoom(1, [2, 3]);

        Assert.Multiple(() =>
        {
            Assert.That(room.Leave(9), Is.EqualTo(MaiVersusRoom.LeaveResult.NotIn));
            Assert.That(room.Leave(1), Is.EqualTo(MaiVersusRoom.LeaveResult.HostChanged));
            Assert.That(room.Host, Is.EqualTo(2));
            Assert.That(room.Leave(3), Is.EqualTo(MaiVersusRoom.LeaveResult.Left));
            Assert.That(room.Leave(2), Is.EqualTo(MaiVersusRoom.LeaveResult.Dissolved));
            Assert.That(room.Join(4), Is.EqualTo(MaiVersusRoom.JoinResult.Closed));
        });
    }

    [Test]
    public void OnlyTheHostStartsTheFirstRoundThenAnyMemberCanContinue()
    {
        var room = new MaiVersusRoom(1, [2]);

        Assert.Multiple(() =>
        {
            Assert.That(room.TryStart(3, out _), Is.EqualTo(MaiVersusRoom.StartResult.NotMember));
            Assert.That(room.TryStart(2, out _), Is.EqualTo(MaiVersusRoom.StartResult.NotHost));
            Assert.That(room.TryStart(1, out var roster), Is.EqualTo(MaiVersusRoom.StartResult.Started));
            Assert.That(roster, Is.EqualTo(new long[] { 1, 2 }));
            Assert.That(room.TryStart(1, out _), Is.EqualTo(MaiVersusRoom.StartResult.Running));
        });

        room.EndRound(Round([Chart(1)], Player(1, (1, 100.0)), Player(2, (1, 99.0))));

        Assert.Multiple(() =>
        {
            Assert.That(room.Round, Is.EqualTo(1));
            Assert.That(room.TryStart(2, out _), Is.EqualTo(MaiVersusRoom.StartResult.Started));
        });
    }

    [Test]
    public void FailedRoundsDoNotCountAndAloneTheHostCannotStart()
    {
        var room = new MaiVersusRoom(1, [2]);
        room.TryStart(1, out _);
        room.EndRound(null);
        room.Leave(2);

        Assert.Multiple(() =>
        {
            Assert.That(room.Round, Is.Zero);
            Assert.That(room.TryStart(1, out _), Is.EqualTo(MaiVersusRoom.StartResult.TooFew));
        });
    }

    [Test]
    public void ExpiresAfterTenIdleMinutesButNotDuringARound()
    {
        var time = new ManualTime();
        var room = new MaiVersusRoom(1, [2], time);

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
        var room = new MaiVersusRoom(1, [2], time);
        room.CacheScores(Player(1));

        time.Advance(TimeSpan.FromSeconds(60));
        Assert.That(room.TryGetScores(1, out _), Is.True);
        time.Advance(TimeSpan.FromSeconds(1));
        Assert.That(room.TryGetScores(1, out _), Is.False);
    }

    [Test]
    public void StandingsUseMatchupRateSoLateJoinersAreComparable()
    {
        var room = new MaiVersusRoom(1, [2]);

        room.TryStart(1, out _);
        room.EndRound(Round([Chart(1), Chart(2)], Player(1, (1, 100.0), (2, 100.0)), Player(2, (1, 99.0))));
        room.Join(3);
        room.TryStart(3, out _);
        room.EndRound(Round([Chart(3)], Player(1, (3, 99.0)), Player(2, (3, 98.0)), Player(3, (3, 100.0))));
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
        var room    = new MaiVersusRoom(1, [2]);
        var charts  = Enumerable.Range(1, 3).Select(id => Chart(id)).ToArray();
        var players = new[] { Player(1, (1, 100.0), (2, 100.0), (3, 100.0)), Player(2, (1, 99.0), (2, 99.0), (3, 99.0)) };
        var random  = new Random(5);

        var first  = room.PickCharts(charts, players, 2, random).Select(x => x.Song.Id).ToArray();
        var second = room.PickCharts(charts, players, 2, random).Select(x => x.Song.Id).ToArray();
        var third  = room.PickCharts(charts, players, 2, random).Select(x => x.Song.Id).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(first.Concat(second), Is.EquivalentTo(new long[] { 1, 2, 3 }));
            Assert.That(third, Has.Length.EqualTo(2));
        });
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
            Assert.That(reply.Text, Does.EndWith("发送「加入」参加，房主发送「开始」开打"));
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
