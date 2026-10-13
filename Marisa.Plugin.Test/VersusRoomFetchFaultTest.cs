using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Marisa.BotDriver.DI.Message;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Entity.MessageSender;
using Marisa.BotDriver.Plugin;
using Marisa.Configuration;
using Marisa.Plugin.Shared.Dialog;
using Marisa.Plugin.Shared.Versus;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class VersusRoomFetchFaultTest
{
    private static long _nextGroup = 1100000;

    [TestCase(false)]
    [TestCase(true)]
    public async Task MemberDataOrTransportFaultSkipsOnlyThatPlayerAndKeepsTheRoomPlayable(bool transportFault)
    {
        using var session = new Session([2, 3]);
        session.Faults[3] = transportFault
            ? new HttpRequestException("transport unavailable")
            : new InvalidDataException("invalid achievement");
        session.Open();
        session.Replies();

        Assert.That(await session.Send(1, "开始"), Is.EqualTo(MarisaPluginTaskState.ToBeContinued));
        var skipped = session.Replies().Single();
        Assert.Multiple(() =>
        {
            Assert.That(skipped.Messages.OfType<MessageDataAt>().Select(at => at.Target), Is.EqualTo(new long[] { 3 }));
            Assert.That(skipped.Text, Is.EqualTo($" 本轮跳过：{session.Faults[3].Message}"));
            Assert.That(session.Renders.Single().Round, Is.EqualTo(1));
            Assert.That(session.Renders.Single().Players, Is.EqualTo(new long[] { 1, 2 }));
            Assert.That(session.Renders.Single().Standings, Is.Empty);
            Assert.That(session.HasDialog, Is.True);
        });

        session.Faults.Remove(3);
        Assert.That(await session.Send(3, "vs"), Is.EqualTo(MarisaPluginTaskState.ToBeContinued));
        Assert.Multiple(() =>
        {
            Assert.That(session.Renders.Select(result => result.Round), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(session.Renders[1].Players, Is.EqualTo(new long[] { 1, 2, 3 }));
            Assert.That(session.Renders[1].Standings.Single(standing => standing.Name == "p3").Rounds, Is.EqualTo(1));
            Assert.That(session.Fetches[1], Is.EqualTo(1));
            Assert.That(session.Fetches[2], Is.EqualTo(1));
            Assert.That(session.Fetches[3], Is.EqualTo(2));
            Assert.That(session.Replies(), Is.Empty);
            Assert.That(session.HasDialog, Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task TooFewValidPlayersDoNotRecordARoundOrUnlockMemberStart(bool transportFault)
    {
        using var session = new Session([2]);
        session.Faults[2] = transportFault
            ? new HttpRequestException("transport unavailable")
            : new InvalidDataException("invalid achievement");
        session.Open();
        session.Replies();

        Assert.That(await session.Send(1, "开始"), Is.EqualTo(MarisaPluginTaskState.ToBeContinued));
        var skipped = session.Replies().Single();
        Assert.Multiple(() =>
        {
            Assert.That(skipped.Messages.OfType<MessageDataAt>().Select(at => at.Target), Is.EqualTo(new long[] { 2 }));
            Assert.That(skipped.Text, Does.Contain(session.Faults[2].Message).And.EndsWith("取到成绩的玩家不足 2 人"));
            Assert.That(session.Renders, Is.Empty);
            Assert.That(session.HasDialog, Is.True);
        });

        session.Faults.Remove(2);
        Assert.That(await session.Send(2, "开始"), Is.EqualTo(MarisaPluginTaskState.ToBeContinued));
        Assert.Multiple(() =>
        {
            Assert.That(session.Fetches[2], Is.EqualTo(1));
            Assert.That(session.Renders, Is.Empty);
            Assert.That(session.Replies(), Is.Empty);
        });

        await session.Send(1, "开始");
        await session.Send(2, "开始");
        Assert.Multiple(() =>
        {
            Assert.That(session.Renders.Select(result => result.Round), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(session.Renders[0].Standings, Is.Empty);
            Assert.That(session.Renders[1].Standings.Select(standing => standing.Rounds), Is.EqualTo(new[] { 2, 2 }));
            Assert.That(session.Fetches[1], Is.EqualTo(1));
            Assert.That(session.Fetches[2], Is.EqualTo(2));
        });
    }

    [TestCase("configuration")]
    [TestCase("programming")]
    [TestCase("cancellation")]
    public async Task ConfigurationProgrammingAndCancellationFaultsPropagateAndReleaseTheRunningRound(string kind)
    {
        using var session = new Session([2]);
        Exception error = kind switch
        {
            "configuration" => new MissingConfigurationException("divingFish.devToken"),
            "programming" => new InvalidOperationException("unexpected state"),
            _ => new OperationCanceledException("fetch canceled")
        };
        session.Faults[2] = error;
        session.Open();
        session.Replies();

        var thrown = Assert.ThrowsAsync(error.GetType(), async () => await session.Send(1, "开始"));
        Assert.Multiple(() =>
        {
            Assert.That(thrown, Is.SameAs(error));
            Assert.That(session.Renders, Is.Empty);
            Assert.That(session.Replies(), Is.Empty);
            Assert.That(session.HasDialog, Is.True);
        });

        session.Faults.Remove(2);
        Assert.That(await session.Send(1, "开始"), Is.EqualTo(MarisaPluginTaskState.ToBeContinued));
        Assert.That(session.Renders.Single().Round, Is.EqualTo(1));
    }

    private sealed class Session(long[] joined) : IDisposable
    {
        private readonly MessageQueueProvider _queue = new();
        private readonly MarisaPluginBase _plugin = (MarisaPluginBase)RuntimeHelpers.GetUninitializedObject(typeof(Chunithm.Chunithm));
        private readonly long _group = Interlocked.Increment(ref _nextGroup);
        private (long?, long?) Key => (_group, null);
        public bool HasDialog => DialogManager.ContainsDialog(Key);
        public Dictionary<long, Exception> Faults { get; } = [];
        public Dictionary<long, int> Fetches { get; } = [];
        public List<(int Round, long[] Players, IReadOnlyList<VersusStanding> Standings)> Renders { get; } = [];

        public void Open()
        {
            VersusRoomSession.Open<int, int, IReadOnlyList<VersusRoundPlayer>>(
                _plugin, Message(1, ""), (joined, Array.Empty<long>()), new("测试", new[] { 1 }, 1),
                id => (id, 3), _ => 14.0, (_, _) => null, Fetch,
                (_, players) =>
                {
                    var cells = VersusRules.ScoreChart(players.Select(player => (double?)player.Scores[(1, 3)]).ToArray());
                    return players.Select((player, index) =>
                        new VersusRoundPlayer(player.Qq, player.Name, cells[index].Points, cells[index].Matchups)).ToArray();
                }, result => result,
                (_, result, round, standings) =>
                {
                    Renders.Add((round, result.Select(player => player.Qq).ToArray(), standings));
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

        public List<MessageChain> Replies()
        {
            var output = new List<MessageChain>();
            while (_queue.SendQueue.Reader.TryRead(out var reply)) output.Add(reply.MessageChain);
            return output;
        }

        public void Dispose() => DialogManager.RemoveDialog(Key);

        private Task<VersusFetchResult<int>> Fetch(Message _, long qq)
        {
            Fetches[qq] = Fetches.GetValueOrDefault(qq) + 1;
            return Faults.TryGetValue(qq, out var error)
                ? Task.FromException<VersusFetchResult<int>>(error)
                : Task.FromResult(new VersusFetchResult<int>(
                    new(qq, $"p{qq}", new Dictionary<(long, int), int> { [(1, 3)] = (int)qq }), null));
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
}
