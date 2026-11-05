using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Channels;
using Marisa.Backend.OneBot;
using Marisa.BotDriver.DI;
using Marisa.BotDriver.DI.Message;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Plugin;
using Marisa.Configuration;
using Marisa.Database;
using Marisa.Plugin;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace Marisa.BotDriver.Test;

/// <summary>
/// 通过进程内伪 OneBot 服务器验证共享 OneBot 后端的传输层：
/// 连接、echo 匹配、发送段构造与事件转换。
/// </summary>
public class OneBotBackendTransportTest
{
    private FakeOneBotServer _server = null!;
    private TestBackend _backend = null!;
    private ServiceProvider _provider = null!;
    private MessageQueueProvider _queues = null!;
    private string _tempRoot = null!;

    [SetUp]
    public async Task SetUp()
    {
        _server = await FakeOneBotServer.StartAsync();

        _tempRoot = Path.Join(Path.GetTempPath(), "Marisa.BotDriver.Test", Guid.NewGuid().ToString("N"));
        var configPath = CreateTestConfig(_tempRoot, _server.Endpoint);
        ConfigurationManager.SetConfigFilePath(configPath);

        var sc = OneBotBackend.Config(Utils.Assembly().GetTypes());
        sc.AddScoped<TestBackend>();
        _provider = sc.BuildServiceProvider();

        BotDbContext.EnsureCreated();

        _backend = _provider.GetRequiredService<TestBackend>();
        _queues = _provider.GetRequiredService<MessageQueueProvider>();
    }

    [TearDown]
    public async Task TearDown()
    {
        _backend.Stop();
        _provider.Dispose();
        await _server.DisposeAsync();

        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, true);
        }
    }

    [Test]
    public async Task SendAction_Should_Resolve_Echo_And_Return_Data()
    {
        _ = _backend.RunTransport();
        await _server.WaitForClientAsync(TimeSpan.FromSeconds(10));

        var data = await _backend.Action("get_login_info", new Dictionary<string, object?>());

        Assert.That(data.GetProperty("user_id").GetInt64(), Is.EqualTo(3548314110L));

        var request = JsonDocument.Parse(await _server.WaitForActionAsync(TimeSpan.FromSeconds(10)));
        Assert.That(request.RootElement.GetProperty("action").GetString(), Is.EqualTo("get_login_info"));
        Assert.That(request.RootElement.GetProperty("echo").GetString(), Is.EqualTo("1"));
    }

    [Test]
    public async Task SendQueue_Should_Construct_OneBot_Segments()
    {
        _ = _backend.RunTransport();
        _ = _backend.RunSendLoop();
        await _server.WaitForClientAsync(TimeSpan.FromSeconds(10));

        var chain = new MessageChain(
            new MessageDataText("hello"),
            new MessageDataAt(123456),
            MessageDataImage.FromBase64("aGVsbG8=")
        );
        await _queues.SendQueue.Writer.WriteAsync(new MessageToSend(chain, MessageType.GroupMessage, 987654, quoteId: 99));

        var request = JsonDocument.Parse(await _server.WaitForActionAsync(TimeSpan.FromSeconds(10)));
        var root = request.RootElement;

        Assert.That(root.GetProperty("action").GetString(), Is.EqualTo("send_group_msg"));
        Assert.That(root.GetProperty("params").GetProperty("group_id").GetString(), Is.EqualTo("987654"));

        var segments = root.GetProperty("params").GetProperty("message").EnumerateArray().ToList();
        Assert.That(segments, Has.Count.EqualTo(4));

        Assert.That(segments[0].GetProperty("type").GetString(), Is.EqualTo("reply"));
        Assert.That(segments[0].GetProperty("data").GetProperty("id").GetString(), Is.EqualTo("99"));

        Assert.That(segments[1].GetProperty("type").GetString(), Is.EqualTo("text"));
        Assert.That(segments[1].GetProperty("data").GetProperty("text").GetString(), Is.EqualTo("hello"));

        Assert.That(segments[2].GetProperty("type").GetString(), Is.EqualTo("at"));
        Assert.That(segments[2].GetProperty("data").GetProperty("qq").GetString(), Is.EqualTo("123456"));

        Assert.That(segments[3].GetProperty("type").GetString(), Is.EqualTo("image"));
        Assert.That(segments[3].GetProperty("data").GetProperty("file").GetString(), Is.EqualTo("base64://aGVsbG8="));
    }

    [Test]
    public async Task HandleEvent_Should_Convert_Group_Message()
    {
        var json = """
            {
              "post_type": "message",
              "message_type": "group",
              "sub_type": "normal",
              "message_id": 12345,
              "time": 1700000000,
              "user_id": 67890,
              "group_id": 55555,
              "group_name": "测试群",
              "sender": { "card": "卡片", "nickname": "昵称", "role": "member" },
              "message": [
                { "type": "text", "data": { "text": "hi" } },
                { "type": "face", "data": { "id": "1" } }
              ]
            }
            """;

        await _backend.Handle(JsonDocument.Parse(json).RootElement);

        var message = await ReadRecvQueue();

        Assert.That(message.Type, Is.EqualTo(MessageType.GroupMessage));
        Assert.That(message.GroupInfo!.Id, Is.EqualTo(55555));
        Assert.That(message.Sender.Id, Is.EqualTo(67890));
        Assert.That(message.Sender.Name, Is.EqualTo("卡片"));
        Assert.That(message.MessageChain!.Messages, Has.Count.EqualTo(3));
        Assert.That(message.MessageChain.Messages[1], Is.TypeOf<MessageDataText>());
        Assert.That(((MessageDataText)message.MessageChain.Messages[1]).Text.ToString(), Is.EqualTo("hi"));
        Assert.That(message.MessageChain.Messages[2], Is.TypeOf<MessageDataOneBotSegment>());
    }

    [Test]
    public async Task HandleEvent_Should_Convert_Bot_Ban_Notice()
    {
        var json = """
            {
              "post_type": "notice",
              "notice_type": "group_ban",
              "sub_type": "ban",
              "group_id": 55555,
              "user_id": 0,
              "operator_id": 66666,
              "duration": 60
            }
            """;

        await _backend.Handle(JsonDocument.Parse(json).RootElement);

        var message = await ReadRecvQueue();

        Assert.That(message.Type, Is.EqualTo(MessageType.GroupMessage));
        Assert.That(message.MessageChain!.Messages.Single(), Is.TypeOf<MessageDataBotMute>());
        var mute = (MessageDataBotMute)message.MessageChain.Messages.Single();
        Assert.That(mute.GroupId, Is.EqualTo(55555));
        Assert.That(mute.Time, Is.EqualTo(TimeSpan.FromSeconds(60)));
    }

    [TestCase("leave", 77777L, null)]
    [TestCase("leave", 0L, null)]
    [TestCase("kick", 66666L, 66666L)]
    public async Task Member_Leave_Should_Carry_Nickname_And_Only_Kicker_When_Kicked(string subType, long operatorId, long? kicker)
    {
        _ = _backend.RunTransport();
        await _server.WaitForClientAsync(TimeSpan.FromSeconds(10));

        await _server.PushEventAsync($$"""
            {
              "post_type": "notice",
              "notice_type": "group_decrease",
              "sub_type": "{{subType}}",
              "group_id": 55555,
              "user_id": 77777,
              "operator_id": {{operatorId}}
            }
            """);

        var message = await ReadRecvQueue();
        var leave = (MessageDataMemberLeave)message.MessageChain!.Messages.Single();

        Assert.That(message.GroupInfo!.Id, Is.EqualTo(55555));
        Assert.That(leave.Id, Is.EqualTo(77777));
        Assert.That(leave.Name, Is.EqualTo("stranger-77777"));
        Assert.That(leave.Kicker, Is.EqualTo(kicker));
    }

    private async Task<Message> ReadRecvQueue()
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        return await _queues.RecvQueue.Reader.ReadAsync(cts.Token);
    }

    private static string CreateTestConfig(string tempRoot, string endpoint)
    {
        var sourceConfigPath = Path.Join(Directory.GetParent(Environment.CurrentDirectory)!.Parent!.Parent!.Parent!.ToString(), "Marisa.StartUp", "config.yaml");
        var escapedTempRoot = tempRoot.Replace("\\", "\\\\");
        var config = File.ReadAllText(sourceConfigPath);
        config = Regex.Replace(config, @"^tempPath:\s*.*$", $"tempPath:     {escapedTempRoot}", RegexOptions.Multiline);
        config = Regex.Replace(config, @"^databasePath:\s*.*$", "databasePath: bot.db", RegexOptions.Multiline);
        config = Regex.Replace(config,
            @"^onebot:\r?\n  endpoint:[^\r\n]*\r?\n  token:[^\r\n]*\r?\n  selfId:[^\r\n]*",
            $"onebot:\n  endpoint: {endpoint}\n  token:\n  selfId: 3548314110",
            RegexOptions.Multiline);
        var configPath = Path.Join(tempRoot, "config.yaml");

        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(configPath, config);

        return configPath;
    }
}

/// <summary>
/// 暴露共享 OneBot 后端的受保护成员，供测试直接驱动传输层。
/// </summary>
internal sealed class TestBackend : OneBotBackend
{
    public TestBackend(
        IServiceProvider serviceProvider,
        IEnumerable<MarisaPluginBase> pluginsAll,
        DictionaryProvider dict,
        MessageSenderProvider messageSenderProvider,
        MessageQueueProvider messageQueueProvider
    ) : base(serviceProvider, pluginsAll, dict, messageSenderProvider, messageQueueProvider)
    {
    }

    public Task RunTransport() => Task.Run(async () =>
    {
        await ConnectWithRetry();
        await ReceiveLoop();
    });

    public Task RunSendLoop() => SendMessage();

    public Task<JsonElement> Action(string action, Dictionary<string, object?> parameters) => SendAction(action, parameters);

    public Task Handle(JsonElement root) => HandleEvent(root);
}

/// <summary>
/// 进程内伪 OneBot v11 服务器：记录收到的动作请求，并以标准应答信封回复。
/// </summary>
internal sealed class FakeOneBotServer : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly CancellationTokenSource _cts = new();
    private readonly TaskCompletionSource _clientConnected = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Channel<string> ReceivedActions { get; } = Channel.CreateUnbounded<string>();

    private string? _endpoint;
    private WebSocket? _socket;

    public string Endpoint => _endpoint ??= ComputeEndpoint();

    private FakeOneBotServer(WebApplication app)
    {
        _app = app;
    }

    public static async Task<FakeOneBotServer> StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        var app = builder.Build();
        app.UseWebSockets();

        var server = new FakeOneBotServer(app);
        app.Run(server.HandleHttp);

        await app.StartAsync();
        return server;
    }

    private string ComputeEndpoint()
    {
        var address = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        var port = new Uri(address).Port;
        return $"ws://127.0.0.1:{port}/onebot";
    }

    public Task WaitForClientAsync(TimeSpan timeout) => _clientConnected.Task.WaitAsync(timeout);

    public async Task<string> WaitForActionAsync(TimeSpan timeout)
    {
        var cts = new CancellationTokenSource(timeout);
        return await ReceivedActions.Reader.ReadAsync(cts.Token);
    }

    private async Task HandleHttp(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        var socket = await context.WebSockets.AcceptWebSocketAsync();
        _socket = socket;
        _clientConnected.TrySetResult();

        var buffer = new byte[8192];
        while (socket.State == WebSocketState.Open)
        {
            using var stream = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                if (result.MessageType == WebSocketMessageType.Close) break;
                stream.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Close) break;

            await HandleRequest(socket, Encoding.UTF8.GetString(stream.ToArray()));
        }
    }

    private async Task HandleRequest(WebSocket socket, string request)
    {
        await ReceivedActions.Writer.WriteAsync(request);

        using var document = JsonDocument.Parse(request);
        var root = document.RootElement;
        var echo = root.TryGetProperty("echo", out var echoElement) ? echoElement.GetString() : null;

        var data = (root.TryGetProperty("action", out var action) ? action.GetString() : null) switch
        {
            "get_login_info"    => new Dictionary<string, object?> { ["user_id"] = 3548314110L, ["nickname"] = "test-bot" },
            "get_stranger_info" => new Dictionary<string, object?> { ["nickname"] = $"stranger-{root.GetProperty("params").GetProperty("user_id").GetString()}" },
            _                   => new Dictionary<string, object?> { ["message_id"] = 42L }
        };

        var response = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["status"] = "ok",
            ["retcode"] = 0,
            ["data"] = data,
            ["echo"] = echo
        });

        await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(response)), WebSocketMessageType.Text, true, _cts.Token);
    }

    public Task PushEventAsync(string json) =>
        _socket!.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)), WebSocketMessageType.Text, true, _cts.Token);

    public ValueTask DisposeAsync()
    {
        _cts.Cancel();
        return _app.DisposeAsync();
    }
}
