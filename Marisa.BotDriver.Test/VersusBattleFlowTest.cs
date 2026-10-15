using Marisa.BotDriver.DI.Message;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Entity.MessageSender;
using Marisa.BotDriver.Plugin;
using Marisa.Plugin.Shared.Versus;
using NUnit.Framework;

namespace Marisa.BotDriver.Test;

public class VersusBattleFlowTest
{
    [Test]
    public async Task SingleResolvesBindingsAndFinishesSongSelectionBeforeFetchingScores()
    {
        var session = new Session { SelectionGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var operation = session.Battle.Single(session.Message(2));

        Assert.That(session.Events, Is.EqualTo(new[] { "bind:1", "bind:2", "select" }));
        session.SelectionGate.SetResult(session.Selection);
        Assert.That(await operation, Is.EqualTo(MarisaPluginTaskState.CompletedTask));
        Assert.That(session.Events, Is.EqualTo(new[] { "bind:1", "bind:2", "select", "fetch:1", "fetch:2", "single" }));
    }

    [Test]
    public async Task BothPlayersStartFetchingBeforeEitherFetchFinishes()
    {
        var session = new Session { FetchGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var operation = session.Battle.Single(session.Message(2));

        Assert.That(session.Events, Is.EqualTo(new[] { "bind:1", "bind:2", "select", "fetch:1", "fetch:2" }));
        session.FetchGate.SetResult(true);
        Assert.That(await operation, Is.EqualTo(MarisaPluginTaskState.CompletedTask));
        Assert.That(session.Rendered, Is.EqualTo(new[] { new Chart(1, 3) }));
    }

    [TestCase(1)]
    [TestCase(2)]
    public void FaultedFetchWaitsForTheOtherPlayersFaultBeforePropagating(int firstFailure)
    {
        var session = new Session();
        var sibling = new TaskCompletionSource<VersusFetchResult<int>>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.FetchOverrides[firstFailure] = () => Task.FromException<VersusFetchResult<int>>(new InvalidDataException("first failure"));
        session.FetchOverrides[3 - firstFailure] = () => sibling.Task;

        var operation = session.Battle.Single(session.Message(2));
        var completedBeforeSibling = operation.IsCompleted;
        var eventsBeforeSibling = session.Events.ToArray();
        sibling.SetException(new InvalidDataException("sibling failure"));
        Assert.ThrowsAsync<InvalidDataException>(async () => await operation);
        Assert.Multiple(() =>
        {
            Assert.That(eventsBeforeSibling, Is.EqualTo(new[] { "bind:1", "bind:2", "select", "fetch:1", "fetch:2" }));
            Assert.That(completedBeforeSibling, Is.False);
            Assert.That(session.Rendered, Is.Empty);
            Assert.That(session.Replies(), Is.Empty);
        });
    }

    [Test]
    public void SingleComparisonPropagatesInvalidScoreDataWithoutConvertingItToAFetchError()
    {
        var session = new Session();
        var error = new InvalidDataException("invalid achievement");
        session.FetchOverrides[1] = () => Task.FromException<VersusFetchResult<int>>(error);

        var thrown = Assert.ThrowsAsync<InvalidDataException>(async () => await session.Battle.Single(session.Message(2)));

        Assert.Multiple(() =>
        {
            Assert.That(thrown, Is.SameAs(error));
            Assert.That(session.Events, Does.Contain("fetch:2"));
            Assert.That(session.Rendered, Is.Empty);
            Assert.That(session.Replies(), Is.Empty);
        });
    }

    [Test]
    public async Task FailedBindingAndCanceledSelectionDoNotFetchScores()
    {
        var failedBinding = new Session();
        failedBinding.BindingErrors[1] = "自己未授权";
        failedBinding.BindingErrors[2] = "对手未授权";
        await failedBinding.Battle.Single(failedBinding.Message(2));
        Assert.That(failedBinding.Events, Is.EqualTo(new[] { "bind:1", "bind:2" }));
        var reply = failedBinding.Replies().Single();
        Assert.That(reply.Text, Is.EqualTo("对手未授权"));
        Assert.That(reply.Messages.OfType<MessageDataAt>().Select(at => at.Target), Is.EqualTo(new long[] { 2 }));

        var canceledSelection = new Session { Selection = null };
        await canceledSelection.Battle.Single(canceledSelection.Message(2));
        Assert.That(canceledSelection.Events, Is.EqualTo(new[] { "bind:1", "bind:2", "select" }));
    }

    [Test]
    public async Task SpecifiedSongStillRendersWhenOnePlayerHasNotPlayedIt()
    {
        var session = new Session { Selection = new([new(1, 3)], false, "song", "version") };
        session.Scores[2] = new Dictionary<(long, int), int>();

        await session.Battle.Single(session.Message(2));

        Assert.Multiple(() =>
        {
            Assert.That(session.Rendered, Is.EqualTo(new[] { new Chart(1, 3) }));
            Assert.That(session.Replies(), Is.Empty);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task RandomComparisonOnlyUsesSharedKeysFromTheSelectedCatalogue(bool multiple)
    {
        var session = new Session();
        session.Charts = [new(1, 3), new(2, 3), new(3, 3)];
        session.Scores[1] = new Dictionary<(long, int), int> { [(1, 3)] = 100, [(2, 3)] = 100, [(4, 3)] = 100 };
        session.Scores[2] = new Dictionary<(long, int), int> { [(1, 3)] = 99, [(2, 2)] = 99, [(3, 3)] = 99, [(4, 3)] = 99 };

        await (multiple ? session.Battle.Random(session.Message(2)) : session.Battle.Single(session.Message(2)));

        Assert.Multiple(() =>
        {
            Assert.That(session.Rendered, Is.EqualTo(new[] { new Chart(1, 3) }));
            Assert.That(session.Replies(), Is.Empty);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task NoSharedDifficultyDoesNotRenderAComparison(bool multiple)
    {
        var session = new Session();
        session.Scores[2] = new Dictionary<(long, int), int> { [(1, 2)] = 99 };

        await (multiple ? session.Battle.Random(session.Message(2)) : session.Battle.Single(session.Message(2)));

        Assert.Multiple(() =>
        {
            Assert.That(session.Rendered, Is.Empty);
            Assert.That(session.Replies().Single().Text, Is.EqualTo("没有找到双方都已游玩且当前可查询的谱面"));
        });
    }

    [TestCase("对手未授权", "对手未授权", true)]
    [TestCase("对手普通错误", "自己的取分错误", false)]
    public async Task FetchFailureKeepsBindingErrorPriorityAndMentionsOnlyTheOpponentBindingError(
        string opponentError, string expected, bool mention)
    {
        var session = new Session();
        session.FetchErrors[1] = "自己的取分错误";
        session.FetchErrors[2] = opponentError;

        await session.Battle.Single(session.Message(2));

        var reply = session.Replies().Single();
        Assert.Multiple(() =>
        {
            Assert.That(session.Events, Does.Contain("fetch:1").And.Contain("fetch:2"));
            Assert.That(session.Rendered, Is.Empty);
            Assert.That(reply.Text, Is.EqualTo(expected));
            Assert.That(reply.Messages.OfType<MessageDataAt>().Select(at => at.Target),
                Is.EqualTo(mention ? new long[] { 2 } : Array.Empty<long>()));
        });
    }

    [Test]
    public async Task RoomMembersExcludeHostBotAndRepeatedMentionsAndKeepUnboundInvitees()
    {
        var session = new Session();
        session.BindingErrors[3] = "未授权";

        await session.Battle.SingleRoom(session.Message(1, 99, 2, 2, 3), 99);

        Assert.Multiple(() =>
        {
            Assert.That(session.OpenedMembers!.Value.Joined, Is.EqualTo(new long[] { 2 }));
            Assert.That(session.OpenedMembers.Value.Unbound, Is.EqualTo(new long[] { 3 }));
            Assert.That(session.Events, Is.EqualTo(new[] { "bind:1", "bind:2", "bind:3", "select", "room" }));
            Assert.That(session.OpenedPlan!.PickCount, Is.EqualTo(1));
            Assert.That(session.OpenedAsSingleSong, Is.True);
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task SpecifiedSingleAndBatchRoomsKeepDistinctViewsWithTheSamePickCount(bool batch)
    {
        var session = new Session { Selection = new([], false, "song", "single version") };
        session.BatchSelection = new("range", "sort", session.Charts);

        if (batch) await session.Battle.BatchRoom(session.Message(2), 99);
        else await session.Battle.SingleRoom(session.Message(2), 99);

        Assert.Multiple(() =>
        {
            Assert.That(session.OpenedPlan!.PickCount, Is.Null);
            Assert.That(session.OpenedPlan.Title, Is.EqualTo(batch ? "range" : "song"));
            Assert.That(session.OpenedPresentation, Is.EqualTo(new VersusRoomPresentation(
                batch ? "version" : "single version", batch ? VersusRoomView.Batch : VersusRoomView.SingleSong)));
        });
    }

    [Test]
    public async Task RandomRoomUsesTheBatchViewAndPreservesTheRequestedCount()
    {
        var session = new Session();

        await session.Battle.RandomRoom(session.Message(2), 99);

        Assert.Multiple(() =>
        {
            Assert.That(session.OpenedPlan!.PickCount, Is.EqualTo(20));
            Assert.That(session.OpenedPresentation, Is.EqualTo(new VersusRoomPresentation("version", VersusRoomView.Batch)));
        });
    }

    [TestCase(7, true)]
    [TestCase(8, false)]
    public async Task RoomLimitIncludesTheHost(int inviteeCount, bool opened)
    {
        var session = new Session();

        await session.Battle.SingleRoom(session.Message(Enumerable.Range(2, inviteeCount).Select(value => (long)value).ToArray()), 99);

        Assert.Multiple(() =>
        {
            Assert.That(session.OpenedMembers.HasValue, Is.EqualTo(opened));
            Assert.That(session.Events.Contains("select"), Is.EqualTo(opened));
            Assert.That(session.Replies().Select(reply => reply.Text),
                Is.EqualTo(opened ? Array.Empty<string>() : new[] { "一个房间最多 8 人" }));
        });
    }

    private readonly record struct Chart(long Id, int Level);

    private sealed class Session
    {
        private readonly MessageQueueProvider _queue = new();
        public List<string> Events { get; } = [];
        public List<Chart> Rendered { get; } = [];
        public Dictionary<long, string> BindingErrors { get; } = [];
        public Dictionary<long, string> FetchErrors { get; } = [];
        public Dictionary<long, Func<Task<VersusFetchResult<int>>>> FetchOverrides { get; } = [];
        public Dictionary<long, IReadOnlyDictionary<(long, int), int>> Scores { get; } = new()
        {
            [1] = new Dictionary<(long, int), int> { [(1, 3)] = 100 },
            [2] = new Dictionary<(long, int), int> { [(1, 3)] = 99 }
        };
        public IReadOnlyList<Chart> Charts { get; set; } = [new(1, 3)];
        public VersusSingleSelection<Chart>? Selection { get; set; } = new([new(1, 3)], true, "随机单曲", "");
        public VersusBatchSelection<Chart>? BatchSelection { get; set; }
        public TaskCompletionSource<VersusSingleSelection<Chart>?>? SelectionGate { get; init; }
        public TaskCompletionSource<bool>? FetchGate { get; init; }
        public (long[] Joined, long[] Unbound)? OpenedMembers { get; private set; }
        public VersusRoomPlan<Chart>? OpenedPlan { get; private set; }
        public VersusRoomPresentation? OpenedPresentation { get; private set; }
        public bool OpenedAsSingleSong { get; private set; }
        public VersusBattle<int, Chart> Battle { get; }

        public Session()
        {
            Battle = new VersusBattle<int, Chart>
            {
                CommandPrefix = "test",
                MatchScope = _ => false,
                ResolvePlayer = (_, qq) =>
                {
                    Events.Add($"bind:{qq}");
                    return BindingErrors.TryGetValue(qq, out var error)
                        ? (null, error)
                        : (new VersusPlayerBinding<int>(qq, () => Fetch(qq)), null);
                },
                IsOpponentBindingError = error => error == "对手未授权",
                SelectSingle = async _ =>
                {
                    Events.Add("select");
                    if (SelectionGate is not null) return await SelectionGate.Task;
                    return Selection is null ? null : Selection with { Charts = Charts };
                },
                ParseRandom = (_, _) => new(20, "", Charts),
                ParseBatch = _ => BatchSelection,
                ChartKey = chart => (chart.Id, chart.Level),
                VersionOf = _ => "version",
                ReplySingle = (_, chart, _, _) =>
                {
                    Events.Add("single");
                    Rendered.Add(chart);
                    return Task.CompletedTask;
                },
                ReplyBatch = (_, _, _, _, charts, _, _) =>
                {
                    Events.Add("batch");
                    Rendered.AddRange(charts);
                    return Task.CompletedTask;
                },
                OpenRoom = (_, members, plan, presentation) =>
                {
                    Events.Add("room");
                    OpenedMembers = members;
                    OpenedPlan = plan;
                    OpenedPresentation = presentation;
                    OpenedAsSingleSong = presentation.View == VersusRoomView.SingleSong;
                }
            };
        }

        public Message Message(params long[] mentions) => new(new MessageChain(
            new MessageData[] { new MessageDataId(1, 1), new MessageDataText("query") }
                .Concat(mentions.Select(qq => new MessageDataAt(qq))).ToArray()), new MessageSenderProvider(_queue))
        {
            Sender = new SenderInfo(1, "self"),
            GroupInfo = new GroupInfo(42, "group", null),
            Type = MessageType.GroupMessage
        };

        public List<MessageChain> Replies()
        {
            var replies = new List<MessageChain>();
            while (_queue.SendQueue.Reader.TryRead(out var reply)) replies.Add(reply.MessageChain);
            return replies;
        }

        private async Task<VersusFetchResult<int>> Fetch(long qq)
        {
            Events.Add($"fetch:{qq}");
            if (FetchOverrides.TryGetValue(qq, out var fetch)) return await fetch();
            if (FetchGate is not null) await FetchGate.Task;
            return FetchErrors.TryGetValue(qq, out var error)
                ? new VersusFetchResult<int>(null, error)
                : new(new VersusPlayer<int>(qq, $"p{qq}", Scores.GetValueOrDefault(qq) ?? new Dictionary<(long, int), int>()), null);
        }
    }
}
