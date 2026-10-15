using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Plugin;
using Marisa.Plugin.Shared.Versus;
using NUnit.Framework;

namespace Marisa.BotDriver.Test;

public class PreparingVersusBattleTest
{
    [TestCase("Single")]
    [TestCase("Random")]
    [TestCase("Batch")]
    [TestCase("SingleRoom")]
    [TestCase("RandomRoom")]
    [TestCase("BatchRoom")]
    public async Task EveryEntryPreparesOnceAndAwaitsBothPreparationAndTheDelegatedOperation(string entry)
    {
        var preparation = new TaskCompletionSource<VersusBattle>(TaskCreationOptions.RunContinuationsAsynchronously);
        var completion = new TaskCompletionSource<MarisaPluginTaskState>(TaskCreationOptions.RunContinuationsAsynchronously);
        var core = new RecordingBattle(completion.Task);
        var prepareCount = 0;
        var battle = new PreparingVersusBattle(_ => false, () =>
        {
            prepareCount++;
            return preparation.Task;
        });
        var message = NewMessage();

        var operation = Invoke(battle, entry, message);

        Assert.Multiple(() =>
        {
            Assert.That(prepareCount, Is.EqualTo(1));
            Assert.That(core.Method, Is.Null);
            Assert.That(operation.IsCompleted, Is.False);
        });

        preparation.SetResult(core);
        await core.Entered.Task;
        Assert.Multiple(() =>
        {
            Assert.That(prepareCount, Is.EqualTo(1));
            Assert.That(core.Method, Is.EqualTo(entry));
            Assert.That(core.Message, Is.SameAs(message));
            Assert.That(core.BotQq, Is.EqualTo(entry.EndsWith("Room", StringComparison.Ordinal) ? 99L : (long?)null));
            Assert.That(operation.IsCompleted, Is.False);
        });

        completion.SetResult(MarisaPluginTaskState.ToBeContinued);
        Assert.That(await operation, Is.EqualTo(MarisaPluginTaskState.ToBeContinued));
    }

    [Test]
    public void ScopeTriggerRecognizesBothValidAndInvalidQueriesWithoutPreparing()
    {
        var prepareCount = 0;
        var battle = new PreparingVersusBattle(query => query == "14", () =>
        {
            prepareCount++;
            throw new InvalidOperationException("Trigger must not load the catalogue");
        });

        Assert.Multiple(() =>
        {
            Assert.That(battle.CanParseScope("14"), Is.True);
            Assert.That(battle.CanParseScope("unknown"), Is.False);
            Assert.That(prepareCount, Is.Zero);
        });
    }

    [Test]
    public async Task ConcurrentCommandsKeepTheirOwnPreparedCoresEvenWhenPreparationCompletesOutOfOrder()
    {
        var firstPreparation = new TaskCompletionSource<VersusBattle>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondPreparation = new TaskCompletionSource<VersusBattle>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstCore = new RecordingBattle(Task.FromResult(MarisaPluginTaskState.ToBeContinued));
        var secondCore = new RecordingBattle(Task.FromResult(MarisaPluginTaskState.CompletedTask));
        var preparations = new Queue<Task<VersusBattle>>([firstPreparation.Task, secondPreparation.Task]);
        var battle = new PreparingVersusBattle(_ => true, preparations.Dequeue);
        var firstMessage = NewMessage();
        var secondMessage = NewMessage();

        var first = battle.BatchRoom(firstMessage, 11);
        var second = battle.BatchRoom(secondMessage, 22);
        Assert.That(preparations, Is.Empty);

        secondPreparation.SetResult(secondCore);
        Assert.That(await second, Is.EqualTo(MarisaPluginTaskState.CompletedTask));
        Assert.That(first.IsCompleted, Is.False);
        firstPreparation.SetResult(firstCore);
        Assert.That(await first, Is.EqualTo(MarisaPluginTaskState.ToBeContinued));

        Assert.Multiple(() =>
        {
            Assert.That(firstCore.Message, Is.SameAs(firstMessage));
            Assert.That(firstCore.BotQq, Is.EqualTo(11));
            Assert.That(secondCore.Message, Is.SameAs(secondMessage));
            Assert.That(secondCore.BotQq, Is.EqualTo(22));
        });
    }

    [Test]
    public async Task CompletedCommandsDoNotCacheThePreparedCore()
    {
        var firstCore = new RecordingBattle(Task.FromResult(MarisaPluginTaskState.CompletedTask));
        var secondCore = new RecordingBattle(Task.FromResult(MarisaPluginTaskState.CompletedTask));
        var cores = new Queue<VersusBattle>([firstCore, secondCore]);
        var battle = new PreparingVersusBattle(_ => true, () => Task.FromResult(cores.Dequeue()));
        var firstMessage = NewMessage();
        var secondMessage = NewMessage();

        await battle.SingleRoom(firstMessage, 11);
        await battle.SingleRoom(secondMessage, 22);

        Assert.Multiple(() =>
        {
            Assert.That(cores, Is.Empty);
            Assert.That(firstCore.Message, Is.SameAs(firstMessage));
            Assert.That(firstCore.BotQq, Is.EqualTo(11));
            Assert.That(secondCore.Message, Is.SameAs(secondMessage));
            Assert.That(secondCore.BotQq, Is.EqualTo(22));
        });
    }

    [Test]
    public void PreparationFailurePropagatesBeforeDispatch()
    {
        var error = new InvalidDataException("Catalogue unavailable");
        var battle = new PreparingVersusBattle(_ => true, () => Task.FromException<VersusBattle>(error));

        var thrown = Assert.ThrowsAsync<InvalidDataException>(async () => await battle.Single(NewMessage()));

        Assert.That(thrown, Is.SameAs(error));
    }

    private static Message NewMessage() => new(new MessageChain(new MessageDataText("request")), null!);

    private static Task<MarisaPluginTaskState> Invoke(VersusBattle battle, string entry, Message message) => entry switch
    {
        "Single" => battle.Single(message),
        "Random" => battle.Random(message),
        "Batch" => battle.Batch(message),
        "SingleRoom" => battle.SingleRoom(message, 99),
        "RandomRoom" => battle.RandomRoom(message, 99),
        "BatchRoom" => battle.BatchRoom(message, 99),
        _ => throw new ArgumentOutOfRangeException(nameof(entry))
    };

    private sealed class RecordingBattle(Task<MarisaPluginTaskState> completion) : VersusBattle
    {
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string? Method { get; private set; }
        public Message? Message { get; private set; }
        public long? BotQq { get; private set; }

        public override bool CanParseScope(string query) => true;
        public override Task<MarisaPluginTaskState> Single(Message message) => Record("Single", message);
        public override Task<MarisaPluginTaskState> Random(Message message) => Record("Random", message);
        public override Task<MarisaPluginTaskState> Batch(Message message) => Record("Batch", message);
        public override Task<MarisaPluginTaskState> SingleRoom(Message message, long botQq) => Record("SingleRoom", message, botQq);
        public override Task<MarisaPluginTaskState> RandomRoom(Message message, long botQq) => Record("RandomRoom", message, botQq);
        public override Task<MarisaPluginTaskState> BatchRoom(Message message, long botQq) => Record("BatchRoom", message, botQq);

        private Task<MarisaPluginTaskState> Record(string method, Message message, long? botQq = null)
        {
            Method = method;
            Message = message;
            BotQq = botQq;
            Entered.SetResult(true);
            return completion;
        }
    }
}
