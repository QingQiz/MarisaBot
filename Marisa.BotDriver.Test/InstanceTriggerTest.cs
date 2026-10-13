using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Plugin;
using Marisa.BotDriver.Plugin.Trigger;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace Marisa.BotDriver.Test;

public class InstanceTriggerTest
{
    [TestCase(typeof(IInstanceTrigger), nameof(IInstanceTrigger.Allow))]
    [TestCase(typeof(InstancePlugin), nameof(InstancePlugin.Evaluate))]
    public void InstanceTriggerEvaluatesTheCurrentPlugin(Type triggerType, string method)
    {
        var trigger = new MarisaPluginTrigger(triggerType, method, MessageType.GroupMessage);
        var accepted = new InstancePlugin(true);
        var rejected = new InstancePlugin(false);
        var message = CreateMessage(MessageType.GroupMessage);
        using var provider = new ServiceCollection().BuildServiceProvider();

        Assert.Multiple(() =>
        {
            Assert.That(trigger.Matches(accepted, message, provider), Is.True);
            Assert.That(trigger.Matches(rejected, message, provider), Is.False);
            Assert.That(accepted.Calls, Is.EqualTo(1));
            Assert.That(rejected.Calls, Is.EqualTo(1));
            Assert.That(accepted.Message, Is.SameAs(message));
            Assert.That(rejected.Message, Is.SameAs(message));
            Assert.That(accepted.Provider, Is.SameAs(provider));
            Assert.That(rejected.Provider, Is.SameAs(provider));
            Assert.Throws<InvalidOperationException>(() => trigger.Trigger(message, provider));
        });
    }

    [TestCase(MessageType.GroupMessage, true)]
    [TestCase(MessageType.FriendMessage, false)]
    [TestCase(MessageType.TempMessage, false)]
    [TestCase(MessageType.StrangerMessage, false)]
    [TestCase((MessageType)0, true)]
    public void InstanceTriggerChecksMessageTargetBeforeCallingThePlugin(MessageType messageType, bool expected)
    {
        var trigger = new MarisaPluginTrigger(typeof(IInstanceTrigger), nameof(IInstanceTrigger.Allow), MessageType.GroupMessage);
        var plugin = new InstancePlugin(true);
        using var provider = new ServiceCollection().BuildServiceProvider();

        var result = trigger.Matches(plugin, CreateMessage(messageType), provider);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(plugin.Calls, Is.EqualTo(expected ? 1 : 0));
        });
    }

    [TestCase(typeof(IInstanceTrigger), nameof(IInstanceTrigger.Allow))]
    [TestCase(typeof(InstancePlugin), nameof(InstancePlugin.Evaluate))]
    public void InstanceTriggerRejectsAnUnrelatedPlugin(Type triggerType, string method)
    {
        var trigger = new MarisaPluginTrigger(triggerType, method, MessageType.GroupMessage);
        using var provider = new ServiceCollection().BuildServiceProvider();

        Assert.Multiple(() =>
        {
            Assert.That(trigger.Matches(new MarisaPluginBase(), CreateMessage(MessageType.GroupMessage), provider), Is.False);
            Assert.That(trigger.Matches(new MarisaPluginBase(), CreateMessage(0), provider), Is.False);
        });
    }

    public static IEnumerable<TestCaseData> StaticTriggerCases
    {
        get
        {
            foreach (var member in new[] { nameof(StaticTriggers.Field), nameof(StaticTriggers.Property), nameof(StaticTriggers.Method) })
            foreach (var (type, expected) in new[]
                     {
                         (MessageType.GroupMessage, true), (MessageType.FriendMessage, false),
                         (MessageType.TempMessage, false), (MessageType.StrangerMessage, false), ((MessageType)0, true)
                     })
                yield return new TestCaseData(member, type, expected);
        }
    }

    [TestCaseSource(nameof(StaticTriggerCases))]
    public void StaticFieldPropertyAndMethodKeepTheirDelegateAndTargetBehavior(string member, MessageType messageType, bool expected)
    {
        var trigger = new MarisaPluginTrigger(typeof(StaticTriggers), member, MessageType.GroupMessage);
        var message = CreateMessage(messageType);
        using var provider = new ServiceCollection().BuildServiceProvider();
        StaticTriggers.Calls.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(trigger.Matches(new MarisaPluginBase(), message, provider), Is.EqualTo(expected));
            Assert.That(trigger.Trigger(message, provider), Is.EqualTo(expected));
            Assert.That(StaticTriggers.Calls, Has.Count.EqualTo(expected ? 2 : 0));
            Assert.That(StaticTriggers.Calls.All(call => ReferenceEquals(call.Message, message) && ReferenceEquals(call.Provider, provider)), Is.True);
        });
    }

    [TestCase(nameof(StaticTriggers.Field))]
    [TestCase(nameof(StaticTriggers.Property))]
    [TestCase(nameof(StaticTriggers.Method))]
    public void StaticTriggerRetainsItsFalseResult(string member)
    {
        var trigger = new MarisaPluginTrigger(typeof(StaticTriggers), member, MessageType.GroupMessage);
        var message = CreateMessage(MessageType.GroupMessage) with { Command = "deny".AsMemory() };
        using var provider = new ServiceCollection().BuildServiceProvider();
        StaticTriggers.Calls.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(trigger.Matches(new MarisaPluginBase(), message, provider), Is.False);
            Assert.That(trigger.Trigger(message, provider), Is.False);
            Assert.That(StaticTriggers.Calls, Has.Count.EqualTo(2));
        });
    }

    private static Message CreateMessage(MessageType type) =>
        new(new MessageChain(new MessageDataText("allow")), null!) { Type = type };

    public interface IInstanceTrigger
    {
        bool Allow(Message message, IServiceProvider provider) => ((InstancePlugin)this).Evaluate(message, provider);
    }

    public sealed class InstancePlugin(bool result) : MarisaPluginBase, IInstanceTrigger
    {
        public int Calls { get; private set; }
        public Message? Message { get; private set; }
        public IServiceProvider? Provider { get; private set; }

        public bool Evaluate(Message message, IServiceProvider provider)
        {
            Calls++;
            Message = message;
            Provider = provider;
            return result;
        }
    }

    private static class StaticTriggers
    {
        public static readonly List<(Message Message, IServiceProvider Provider)> Calls = [];
        public static readonly MarisaPluginTrigger.PluginTrigger Field = Record;
        public static MarisaPluginTrigger.PluginTrigger Property => Record;
        public static bool Method(Message message, IServiceProvider provider) => Record(message, provider);

        private static bool Record(Message message, IServiceProvider provider)
        {
            Calls.Add((message, provider));
            return message.Command.Span.SequenceEqual("allow");
        }
    }
}
