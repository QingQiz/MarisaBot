using Marisa.Backend.OneBot;
using Marisa.BotDriver.DI;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Plugin;
using Marisa.Database;
using Marisa.Plugin;
using Marisa.Plugin.Chunithm;
using Marisa.Plugin.MaiMaiDx;
using Marisa.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System.Text.RegularExpressions;
using EventHandler = Marisa.Plugin.EventHandler.EventHandler;

namespace Marisa.BotDriver.Test;

public class DispatcherTest
{
    private MessageDispatcher _dispatcher = null!;
    private ServiceProvider _provider = null!;
    private string _configPath = null!;
    private string _tempRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _tempRoot = Path.Join(Path.GetTempPath(), "Marisa.BotDriver.Test", Guid.NewGuid().ToString("N"));
        _configPath = CreateTestConfig(_tempRoot);

        ConfigurationManager.SetConfigFilePath(_configPath);

        var sc = OneBotBackend.Config(Utils.Assembly().GetTypes());

        _provider = sc.BuildServiceProvider();

        BotDbContext.EnsureCreated();

        _dispatcher = new MessageDispatcher(_provider.GetServices<MarisaPluginBase>(), _provider, _provider.GetService<DictionaryProvider>()!);
    }

    [TearDown]
    public void TearDown()
    {
        _provider.Dispose();

        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, true);
        }
    }

    private static string CreateTestConfig(string tempRoot)
    {
        var sourceConfigPath = Path.Join(Directory.GetParent(Environment.CurrentDirectory)!.Parent!.Parent!.Parent!.ToString(), "Marisa.StartUp", "config.yaml");
        var escapedTempRoot = tempRoot.Replace("\\", "\\\\");
        var config = File.ReadAllText(sourceConfigPath);
        config = Regex.Replace(config, @"^tempPath:\s*.*$", $"tempPath:     {escapedTempRoot}", RegexOptions.Multiline);
        config = Regex.Replace(config, @"^databasePath:\s*.*$", "databasePath: bot.db", RegexOptions.Multiline);
        var configPath = Path.Join(tempRoot, "config.yaml");

        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(configPath, config);

        return configPath;
    }

    private static Message CreateMessage(params MessageData[] data)
    {
        return new Message(new MessageChain(data), null!);
    }

    public static IEnumerable<TestCaseData> TestCaseData1
    {
        get
        {
            yield return new TestCaseData(CreateMessage(new MessageDataText("mai b50")), typeof(MaiMaiDx), "B50").SetName("MaimaiDX");
            yield return new TestCaseData(CreateMessage(new MessageDataText("mai 含金量分析")), typeof(MaiMaiDx), "GoldValueAnalysis").SetName("mai gold analysis");
            yield return new TestCaseData(CreateMessage(new MessageDataText("mai 水分分析")), typeof(MaiMaiDx), "WaterValueAnalysis").SetName("mai water analysis");
            yield return new TestCaseData(CreateMessage(new MessageDataText("mai 鸟加含金量分析")), typeof(MaiMaiDx), "FilteredValueAnalysis").SetName("mai bird plus gold analysis");
            yield return new TestCaseData(CreateMessage(new MessageDataText("mai SSS+ 水分分析")), typeof(MaiMaiDx), "FilteredValueAnalysis").SetName("mai SSS plus water analysis");
            yield return new TestCaseData(CreateMessage(new MessageDataText("mai D含金量分析")), typeof(MaiMaiDx), "FilteredValueAnalysis").SetName("mai D gold analysis");
            yield return new TestCaseData(CreateMessage(new MessageDataText("mai 14+含金量分析")) with { Type = MessageType.GroupMessage }, typeof(MaiMaiDx), "FilteredValueAnalysis").SetName("mai level gold analysis in group");
            yield return new TestCaseData(CreateMessage(new MessageDataText("mai 13.9 水分分析")) with { Type = MessageType.FriendMessage }, typeof(MaiMaiDx), "FilteredValueAnalysis").SetName("mai constant water analysis in friend");
            yield return new TestCaseData(CreateMessage(new MessageDataText("mai MASTER含金量分析")) with { Type = MessageType.TempMessage }, typeof(MaiMaiDx), "FilteredValueAnalysis").SetName("mai master gold analysis in temp");
            yield return new TestCaseData(CreateMessage(new MessageDataText("mai 白谱 水分分析")), typeof(MaiMaiDx), "FilteredValueAnalysis").SetName("mai remaster water analysis");
            yield return new TestCaseData(CreateMessage(new MessageDataText("mai Re:MASTER 14.8 SSS+ 含金量分析")), typeof(MaiMaiDx), "FilteredValueAnalysis").SetName("mai combined value analysis");
            yield return new TestCaseData(CreateMessage(new MessageDataText("mai 鸟＋14＋紫谱14．8水分分析")), typeof(MaiMaiDx), "FilteredValueAnalysis").SetName("mai compact full-width value analysis");
            yield return new TestCaseData(CreateMessage(new MessageDataText("mai 1 14.8含金量分析")), typeof(MaiMaiDx), "FilteredValueAnalysis").SetName("mai preserves filter whitespace boundaries");
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

            yield return new TestCaseData(CreateMessage(new MessageDataText("mai best")) with
            {
                Type = MessageType.TempMessage
            }, typeof(MaiMaiDx), null).SetName("temp");

            yield return new TestCaseData(CreateMessage(new MessageDataText("mai sum ver")), typeof(MaiMaiDx), "SummaryVersion").SetName("mai sum ver");
            yield return new TestCaseData(CreateMessage(new MessageDataText("maisumver")), typeof(MaiMaiDx), "SummaryVersion").SetName("maisumver");
            yield return new TestCaseData(CreateMessage(new MessageDataText("mai sum ver白")), typeof(MaiMaiDx), "SummaryVersion").SetName("mai sum ver白");
            yield return new TestCaseData(CreateMessage(new MessageDataText("maisumver白")), typeof(MaiMaiDx), "SummaryVersion").SetName("maisumver白");
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

            yield return new TestCaseData(CreateMessage(new MessageDataText("mai 双星含金量分析")), typeof(MaiMaiDx), "FilteredValueAnalysis").SetName("mai rejects invented double star rank");

            yield return new TestCaseData(CreateMessage(new MessageDataText("mai SSS++含金量分析")), typeof(MaiMaiDx), "FilteredValueAnalysis").SetName("mai rejects invalid achievement rank");

            yield return new TestCaseData(CreateMessage(new MessageDataText("mai 15+含金量分析")), typeof(MaiMaiDx), "FilteredValueAnalysis").SetName("mai rejects invalid level");

            yield return new TestCaseData(CreateMessage(new MessageDataText("mai 14.80水分分析")), typeof(MaiMaiDx), "FilteredValueAnalysis").SetName("mai rejects invalid constant");

            yield return new TestCaseData(CreateMessage(new MessageDataText("mai MASTER EXPERT含金量分析")), typeof(MaiMaiDx), "FilteredValueAnalysis").SetName("mai rejects duplicate difficulty");

            yield return new TestCaseData(CreateMessage(new MessageDataText("mai 14 13+水分分析")), typeof(MaiMaiDx), "FilteredValueAnalysis").SetName("mai rejects duplicate level");

            yield return new TestCaseData(CreateMessage(new MessageDataText("mai S S含金量分析")), typeof(MaiMaiDx), "FilteredValueAnalysis").SetName("mai rejects ranks separated by whitespace");

            yield return new TestCaseData(CreateMessage(new MessageDataText("mai 含金量分析 extra")), typeof(MaiMaiDx), "GoldValueAnalysis").SetName("mai gold analysis is exact");

            yield return new TestCaseData(CreateMessage(new MessageDataText("mai best")) with
            {
                Type = MessageType.StrangerMessage
            }, typeof(MaiMaiDx), null).SetName("no stranger");

            yield return new TestCaseData(CreateMessage(new MessageDataUnknown()) with
            {
                Type = MessageType.GroupMessage
            }, typeof(EventHandler), null).SetName("no unknown");
        }
    }

    [Test]
    [TestCaseSource(nameof(TestCaseData1))]
    public void Message_Should_Be_Dispatched(Message message, Type type, string? method)
    {
        Assert.That(_dispatcher.Dispatch(message).Any(x => method == null
            ? x.Plugin.GetType() == type
            : x.Plugin.GetType() == type && x.Method.Name == method)
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
