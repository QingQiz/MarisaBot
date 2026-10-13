using Marisa.BotDriver.DI;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Extension;
using Marisa.BotDriver.Plugin;
using Marisa.BotDriver.Plugin.Trigger;
using Marisa.Plugin.Shared.Help;
using Marisa.Plugin.Shared.Interface;
using Marisa.Plugin.Shared.Versus;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Reflection;
using ChuPlugin = Marisa.Plugin.Chunithm.Chunithm;
using MaiPlugin = Marisa.Plugin.MaiMaiDx.MaiMaiDx;

namespace Marisa.BotDriver.Test;

public class BattleInterfaceRegistrationTest
{
    private static readonly string[] BattleMethods =
    [
        nameof(ICanBattleWithOthers.SongVersus), nameof(ICanBattleWithOthers.SongVersusRandom), nameof(ICanBattleWithOthers.SongVersusBatch),
        nameof(ICanBattleWithOthers.SongVersusRoom), nameof(ICanBattleWithOthers.SongVersusRoomRandom), nameof(ICanBattleWithOthers.SongVersusRoomBatch)
    ];

    [TestCase(typeof(MaiPlugin))]
    [TestCase(typeof(ChuPlugin))]
    public void BattleCommandsAreDiscoveredOnceFromTheInterface(Type plugin)
    {
        var commands = plugin.GetAllMethods()
            .Where(method => BattleMethods.Contains(method.Name) &&
                method.GetCustomAttribute<MarisaPluginCommand>() is not null)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(commands.Select(method => method.Name), Is.EquivalentTo(BattleMethods));
            Assert.That(commands.Select(method => method.DeclaringType),
                Is.EqualTo(Enumerable.Repeat(typeof(ICanBattleWithOthers), 6)));
        });
    }

    [TestCase(typeof(MaiPlugin))]
    [TestCase(typeof(ChuPlugin))]
    public void HelpKeepsOneBattleTreeWithRoomSubcommands(Type plugin)
    {
        var help = HelpGenerator.GetHelp(plugin);
        var battles = help.SubHelp.Where(doc => doc.Commands.Contains("vs")).ToArray();
        Assert.That(battles, Has.Length.EqualTo(1));
        var battle = battles.Single();

        Assert.Multiple(() =>
        {
            Assert.That(battle.Commands, Is.EquivalentTo(new[] { "vs", "对战" }));
            Assert.That(battle.SubHelp.SelectMany(doc => doc.Commands), Is.EquivalentTo(new[] { "n", "b", "开房" }));
            Assert.That(help.SubHelp.Any(doc => doc.Commands.Contains("开房")), Is.False);
            Assert.That(battle.SubHelp.Where(doc => !doc.Commands.Contains("开房")).SelectMany(doc => doc.SubHelp), Is.Empty);
        });

        var room = battle.SubHelp.Single(doc => doc.Commands.Contains("开房"));
        Assert.Multiple(() =>
        {
            Assert.That(room.SubHelp.SelectMany(doc => doc.Commands), Is.EquivalentTo(new[] { "n", "b" }));
            Assert.That(room.SubHelp.SelectMany(doc => doc.SubHelp), Is.Empty);
        });
    }

    [TestCase(nameof(ICanBattleWithOthers.SongVersus), MarisaPluginTaskState.ToBeContinued)]
    [TestCase(nameof(ICanBattleWithOthers.SongVersusRandom), MarisaPluginTaskState.ToBeContinued)]
    [TestCase(nameof(ICanBattleWithOthers.SongVersusBatch), MarisaPluginTaskState.ToBeContinued)]
    [TestCase(nameof(ICanBattleWithOthers.SongVersusRoom), MarisaPluginTaskState.CompletedTask)]
    [TestCase(nameof(ICanBattleWithOthers.SongVersusRoomRandom), MarisaPluginTaskState.CompletedTask)]
    [TestCase(nameof(ICanBattleWithOthers.SongVersusRoomBatch), MarisaPluginTaskState.CompletedTask)]
    public async Task DispatcherInvokesDefaultInterfaceCommandsWithTheirArguments(string command, MarisaPluginTaskState expected)
    {
        const long botQq = 987654;
        var battle = new RecordingBattle();
        var plugin = new BattlePlugin(battle);
        using var provider = new ServiceCollection().BuildServiceProvider();
        var dispatcher = new MessageDispatcher([plugin], provider, new DictionaryProvider().Add("qq", botQq));
        var method = plugin.GetType().GetAllMethods().Single(method => method.Name == command);
        var message = new Message(new MessageChain(new MessageDataText("request")), null!);

        var result = await dispatcher.Invoke(plugin, method, message);

        Assert.Multiple(() =>
        {
            Assert.That(method.DeclaringType, Is.EqualTo(typeof(ICanBattleWithOthers)));
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(battle.Message, Is.SameAs(message));
            Assert.That(battle.Method, Is.EqualTo(command));
            Assert.That(battle.BotQq, Is.EqualTo(command.StartsWith(nameof(ICanBattleWithOthers.SongVersusRoom), StringComparison.Ordinal) ? botQq : (long?)null));
        });
    }

    private sealed class BattlePlugin(RecordingBattle battle) : MarisaPluginBase, ICanBattleWithOthers
    {
        public VersusBattle Battle => battle;

        public override Task ExceptionHandler(Exception exception, Message message) => Task.FromException(exception);
    }

    private sealed class RecordingBattle : VersusBattle
    {
        public Message? Message { get; private set; }
        public long? BotQq { get; private set; }
        public string? Method { get; private set; }

        public override bool CanParseScope(string scope) => false;

        public override Task<MarisaPluginTaskState> Single(Message message)
        {
            Message = message;
            Method = nameof(ICanBattleWithOthers.SongVersus);
            return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
        }

        public override Task<MarisaPluginTaskState> SingleRoom(Message message, long botQq)
        {
            Message = message;
            BotQq = botQq;
            Method = nameof(ICanBattleWithOthers.SongVersusRoom);
            return Task.FromResult(MarisaPluginTaskState.CompletedTask);
        }

        public override Task<MarisaPluginTaskState> Random(Message message)
        {
            Message = message;
            Method = nameof(ICanBattleWithOthers.SongVersusRandom);
            return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
        }

        public override Task<MarisaPluginTaskState> Batch(Message message)
        {
            Message = message;
            Method = nameof(ICanBattleWithOthers.SongVersusBatch);
            return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
        }

        public override MarisaPluginTaskState RandomRoom(Message message, long botQq)
        {
            Message = message;
            BotQq = botQq;
            Method = nameof(ICanBattleWithOthers.SongVersusRoomRandom);
            return MarisaPluginTaskState.CompletedTask;
        }

        public override MarisaPluginTaskState BatchRoom(Message message, long botQq)
        {
            Message = message;
            BotQq = botQq;
            Method = nameof(ICanBattleWithOthers.SongVersusRoomBatch);
            return MarisaPluginTaskState.CompletedTask;
        }
    }
}
