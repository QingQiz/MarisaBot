using Marisa.Backend.Mirai;
using Marisa.BotDriver.DI;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Plugin;
using Marisa.Plugin.Chunithm;
using Marisa.Plugin.MaiMaiDx;
using Marisa.Plugin.Shared.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using EventHandler = Marisa.Plugin.EventHandler.EventHandler;

namespace Marisa.BotDriver.Test;

public class DispatcherTest
{
    private MessageDispatcher _dispatcher;

    [SetUp]
    public void SetUp()
    {
        var configPath = Path.Join(Directory.GetParent(Environment.CurrentDirectory)!.Parent!.Parent!.Parent!.ToString(), "Marisa.StartUp", "config.yaml");

        ConfigurationManager.SetConfigFilePath(configPath);

        var sc = MiraiBackend.Config(Marisa.Plugin.Utils.Assembly());

        var provider = sc.BuildServiceProvider();

        provider.GetService<DictionaryProvider>()!
            .Add("QQ", 0)
            .Add("ServerAddress", "")
            .Add("AuthKey", "");

        _dispatcher = new MessageDispatcher(provider.GetServices<MarisaPluginBase>(), provider, provider.GetService<DictionaryProvider>()!);
    }

    private static Message CreateMessage(params MessageData[] data)
    {
        return new Message(new MessageChain(data), null!);
    }

    public static IEnumerable<TestCaseData> TestCaseData1
    {
        get
        {
            yield return new TestCaseData(CreateMessage(new MessageDataText("mai b50")), typeof(MaiMaiDx), "MaiMaiDxB50").SetName("MaimaiDX");
            yield return new TestCaseData(CreateMessage(new MessageDataSignServerLose("")), typeof(EventHandler), null).SetName("SignServer");
            yield return new TestCaseData(CreateMessage(new MessageDataBotOffline()), typeof(EventHandler), null).SetName("Online");
            yield return new TestCaseData(CreateMessage(new MessageDataBotOnline()), typeof(EventHandler), null).SetName("Offline");

            yield return new TestCaseData(CreateMessage(new MessageDataText("mai best")) with
            {
                Type = MessageType.GroupMessage
            }, typeof(MaiMaiDx), null).SetName("group");

            yield return new TestCaseData(CreateMessage(new MessageDataText("mai best")) with
            {
                Type = MessageType.FriendMessage
            }, typeof(MaiMaiDx), null).SetName("friend");
        }
    }

    public static IEnumerable<TestCaseData> TestCaseData2
    {
        get
        {
            yield return new TestCaseData(CreateMessage(new MessageDataText("mai sum b 15")), typeof(MaiMaiDx), "MaiMaiDxSummarySongBpm").SetName("mai sum b");

            yield return new TestCaseData(CreateMessage(new MessageDataText("mai sum bpm 15")), typeof(MaiMaiDx), "MaiMaiDxSummarySongBase").SetName("mai sum bpm");

            yield return new TestCaseData(CreateMessage(new MessageDataText("chu sum b 15")), typeof(Chunithm), "SummarySongBpm").SetName("chu sum b");

            yield return new TestCaseData(CreateMessage(new MessageDataText("mai sum bpm 15")), typeof(MaiMaiDx), "SummarySongBase").SetName("chu sum bpm");

            yield return new TestCaseData(CreateMessage(new MessageDataText("mai best")) with
            {
                Type = MessageType.StrangerMessage
            }, typeof(MaiMaiDx), null).SetName("no stranger");

            yield return new TestCaseData(CreateMessage(new MessageDataText("mai best")) with
            {
                Type = MessageType.TempMessage
            }, typeof(MaiMaiDx), null).SetName("no temp");
        }
    }

    [Test]
    [TestCaseSource(nameof(TestCaseData1))]
    public void Message_Should_Be_Dispatched(Message message, Type type, string? method)
    {
        Assert.That(_dispatcher.Dispatch(message).Any(x =>
            {
                Console.WriteLine(x.Plugin.GetType().Name + "." + x.Method.Name);
                return method == null
                    ? x.Plugin.GetType() == type
                    : x.Plugin.GetType() == type && x.Method.Name == method;
            })
        );
    }

    [Test]
    [TestCaseSource(nameof(TestCaseData2))]
    public void Message_Should_Not_Be_Dispatched(Message message, Type type, string? method)
    {
        Assert.That(!_dispatcher.Dispatch(message).Any(x => method == null
            ? x.Plugin.GetType() == type
            : x.Plugin.GetType() == type && x.Method.Name == method)
        );
    }
}