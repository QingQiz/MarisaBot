using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Flurl.Http.Testing;
using Marisa.BotDriver;
using Marisa.BotDriver.DI;
using Marisa.BotDriver.DI.Message;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Entity.MessageSender;
using Marisa.BotDriver.Plugin;
using Marisa.Configuration;
using Marisa.Database;
using Marisa.Database.Entity.Plugin.Chunithm;
using Marisa.Database.Entity.Plugin.DivingFish;
using Marisa.Database.Entity.Plugin.MaiMaiDx;
using Marisa.Plugin.Shared.Dialog;
using Marisa.Plugin.Shared.DivingFish;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using DialogHandler = Marisa.Plugin.Shared.Dialog.Dialog.MessageHandler;

namespace Marisa.Plugin.Test;

[TestFixture("maimai", "maibind", true)]
[TestFixture("maimai", "maibind", false)]
[TestFixture("chunithm", "chubind", true)]
[TestFixture("chunithm", "chubind", false)]
[NonParallelizable]
public class DivingFishDeviceBindingDialogTest(string game, string command, bool inGroup)
{
    private const string TokenUrl = "https://auth.diving-fish.com/oauth/token";
    private static long _identitySeed = 9_000_000_000;
    private static readonly FieldInfo[] DispatcherCaches = [
        typeof(MessageDispatcher).GetField("_plugins", BindingFlags.Static | BindingFlags.NonPublic)!,
        typeof(MessageDispatcher).GetField("_commands", BindingFlags.Static | BindingFlags.NonPublic)!,
        typeof(MessageDispatcher).GetField("_subCommands", BindingFlags.Static | BindingFlags.NonPublic)!
    ];

    private object?[] _savedCaches = null!;
    private HttpTest _http = null!;
    private ServiceProvider _provider = null!;
    private MessageDispatcher _dispatcher = null!;
    private MessageQueueProvider _queue = null!;
    private MessageSenderProvider _sender = null!;
    private ManualClock _clock = null!;
    private string _testRoot = null!;
    private string _sourceConfig = null!;
    private long _user;
    private long? Group => inGroup ? 789 : null;
    private (long?, long?) Key => (Group, _user);

    [SetUp]
    public void SetUp()
    {
        _user = Interlocked.Increment(ref _identitySeed);
        _testRoot = Path.Combine(Path.GetTempPath(), nameof(DivingFishDeviceBindingDialogTest), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
        _sourceConfig = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../..", "Marisa.StartUp/config.yaml"));
        var configPath = Path.Combine(_testRoot, "config.yaml");
        File.WriteAllText(configPath, $$"""
            tempPath: '{{_testRoot}}'
            databasePath: binding-test.db
            divingFish:
              clientId: test-client
              clientSecret: test-secret
            """);
        ConfigurationManager.SetConfigFilePath(configPath);

        _http = new HttpTest();
        _http.ForCallsTo("https://auth.diving-fish.com/.well-known/openid-configuration")
            .RespondWithJson(new { token_endpoint = TokenUrl, device_authorization_endpoint = "https://auth.diving-fish.com/oauth/device/code" });
        _http.ForCallsTo("https://auth.diving-fish.com/oauth/device/code").RespondWithJson(new
        {
            device_code = "test-device", user_code = "TEST-CODE",
            verification_uri = "https://auth.diving-fish.com/device",
            verification_uri_complete = "https://auth.diving-fish.com/device?user_code=TEST-CODE",
            expires_in = 600, interval = 1
        });

        _clock = new ManualClock();
        _provider = new ServiceCollection()
            .AddSingleton<TimeProvider>(_clock)
            .AddSingleton<MessageQueueProvider>()
            .AddSingleton<MessageSenderProvider>()
            .BuildServiceProvider();
        _queue = _provider.GetRequiredService<MessageQueueProvider>();
        _sender = _provider.GetRequiredService<MessageSenderProvider>();
        _savedCaches = DispatcherCaches.Select(f => f.GetValue(null)).ToArray();
        foreach (var field in DispatcherCaches) field.SetValue(null, null);
        MarisaPluginBase plugin = game == "maimai" ? new MaiMaiDx.MaiMaiDx() : new Chunithm.Chunithm();
        _dispatcher = new MessageDispatcher([new Plugin.Dialog(), plugin], _provider, new DictionaryProvider());
    }

    [TearDown]
    public void TearDown()
    {
        DialogManager.RemoveDialog(Key);
        _clock.Advance(TimeSpan.FromMinutes(11));
        _http.Dispose();
        _provider.Dispose();
        for (var i = 0; i < DispatcherCaches.Length; i++) DispatcherCaches[i].SetValue(null, _savedCaches[i]);
        ConfigurationManager.SetConfigFilePath(_sourceConfig);
        Directory.Delete(_testRoot, true);
    }

    [Test]
    public async Task Confirmation_ThroughDispatcher_BindsOnce_AndStopsTimeout()
    {
        MockAuthorization();
        await StartBinding();
        AssertBindingCount(0);

        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.That(await NextReply(), Does.Contain("回复收到"));
        AssertBindingCount(0);

        await Dispatch("收到", _user + 1);
        await Dispatch("收到", group: Group == null ? 789 : 790);
        Assert.That(_queue.SendQueue.Reader.TryRead(out _), Is.False);
        AssertBindingCount(0);
        Assert.That(DialogManager.ContainsDialog(Key), Is.True);

        await Dispatch("收到");
        Assert.That(await NextReply(), Is.EqualTo("ok"));
        AssertBindingCount(1);
        Assert.That(DialogManager.ContainsDialog(Key), Is.False);
        Assert.That(_clock.ActiveTimerCount, Is.Zero);

        await Dispatch("收到");
        _clock.Advance(TimeSpan.FromMinutes(11));
        Assert.That(_queue.SendQueue.Reader.TryRead(out _), Is.False);
        AssertBindingCount(1);
    }

    [TestCase("取消")]
    [TestCase("收到")]
    public async Task CancelBeforeAuthorization_StopsPolling(string reply)
    {
        MockAuthorization();
        await StartBinding();
        await Dispatch(reply);
        Assert.That(_queue.SendQueue.Reader.TryRead(out _), Is.False);
        Assert.That(DialogManager.ContainsDialog(Key), Is.False);
        Assert.That(_clock.ActiveTimerCount, Is.Zero);

        _clock.Advance(TimeSpan.FromMinutes(11));
        Assert.That(DevicePollCount(), Is.Zero);
        Assert.That(_queue.SendQueue.Reader.TryRead(out _), Is.False);
        AssertBindingCount(0);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task Timeout_CancelsOnlyOnce_AndNeverBinds(bool authorized)
    {
        MockAuthorization();
        await StartBinding();
        if (authorized)
        {
            _clock.Advance(TimeSpan.FromSeconds(1));
            Assert.That(await NextReply(), Does.Contain("回复收到"));
        }

        _clock.Advance(TimeSpan.FromMinutes(10));
        Assert.That(_queue.SendQueue.Reader.TryRead(out _), Is.False);
        Assert.That(DialogManager.ContainsDialog(Key), Is.False);
        Assert.That(_clock.ActiveTimerCount, Is.Zero);
        await Dispatch("收到");
        _clock.Advance(TimeSpan.FromMinutes(10));
        Assert.That(_queue.SendQueue.Reader.TryRead(out _), Is.False);
        AssertBindingCount(0);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task OldSession_DoesNotNotifyOrRemoveReplacement(bool authorized)
    {
        MockAuthorization();
        await StartBinding();
        if (authorized)
        {
            _clock.Advance(TimeSpan.FromSeconds(1));
            Assert.That(await NextReply(), Does.Contain("回复收到"));
        }

        DialogManager.RemoveDialog(Key);
        DialogHandler replacement = m =>
        {
            m.Reply("new dialog");
            return Task.FromResult(MarisaPluginTaskState.ToBeContinued);
        };
        Assert.That(DialogManager.TryAddDialog(Key, replacement), Is.True);
        _clock.Advance(TimeSpan.FromMinutes(11));
        await Dispatch("test");
        Assert.That(await NextReply(), Is.EqualTo("new dialog"));
        Assert.That(DialogManager.TryGetDialog(Key, out var current), Is.True);
        Assert.That(current, Is.SameAs(replacement));
        Assert.That(_queue.SendQueue.Reader.TryRead(out _), Is.False);
        AssertBindingCount(0);
    }

    [Test]
    public async Task AuthorizationFailure_ClosesDialog_AndStopsTimeout()
    {
        _http.ForCallsTo(TokenUrl).WithRequestBody("*device_code*")
            .RespondWithJson(new { error = "access_denied" }, 400);
        await StartBinding();
        _clock.Advance(TimeSpan.FromSeconds(1));
        Assert.That(await NextReply(), Does.StartWith("DivingFish OAuth 绑定失败："));
        Assert.That(DialogManager.ContainsDialog(Key), Is.False);
        Assert.That(_clock.ActiveTimerCount, Is.Zero);
        _clock.Advance(TimeSpan.FromMinutes(11));
        Assert.That(_queue.SendQueue.Reader.TryRead(out _), Is.False);
        AssertBindingCount(0);
    }

    [Test]
    public async Task AuthorizationResponse_AfterCancel_DoesNotPublishOrBind()
    {
        MockAuthorization();
        var responseArrived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseResponse = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _http.Settings.AfterCallAsync = async call =>
        {
            if (call.RequestBody?.Contains("device_code") != true) return;
            responseArrived.TrySetResult();
            await releaseResponse.Task;
        };

        DivingFishDeviceBindingSession session = null!;
        DialogHandler handler = m => Task.FromResult(session.Confirm(m));
        Assert.That(DialogManager.TryAddDialog(Key, handler), Is.True);
        session = new DivingFishDeviceBindingSession(CreateMessage("bind", _user, Group), game, handler, _clock);
        var background = session.RunAsync(new DivingFishOAuth.DeviceAuthorization("test-device", "TEST-CODE", "", 600, 1));
        _clock.Advance(TimeSpan.FromSeconds(1));
        try
        {
            await responseArrived.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Dispatch("取消");
            Assert.That(_queue.SendQueue.Reader.TryRead(out _), Is.False);
        }
        finally
        {
            releaseResponse.TrySetResult();
            await background.WaitAsync(TimeSpan.FromSeconds(5));
        }

        _clock.Advance(TimeSpan.FromMinutes(11));
        Assert.That(_queue.SendQueue.Reader.TryRead(out _), Is.False);
        AssertBindingCount(0);
    }

    [TestCase(MarisaPluginTaskState.CompletedTask)]
    [TestCase(MarisaPluginTaskState.Canceled)]
    [TestCase(MarisaPluginTaskState.NoResponse)]
    public async Task CompletingOldHandler_DoesNotRemoveReplacementOrRestoreOldHandler(MarisaPluginTaskState state)
    {
        DialogHandler replacement = m =>
        {
            m.Reply("replacement completed");
            return Task.FromResult(MarisaPluginTaskState.CompletedTask);
        };
        DialogHandler old = _ =>
        {
            DialogManager.RemoveDialog(Key);
            Assert.That(DialogManager.TryAddDialog(Key, replacement), Is.True);
            return Task.FromResult(state);
        };
        Assert.That(DialogManager.TryAddDialog(Key, old), Is.True);
        await Dispatch("test");

        if (state == MarisaPluginTaskState.CompletedTask)
        {
            Assert.That(DialogManager.TryGetDialog(Key, out var current), Is.True);
            Assert.That(current, Is.SameAs(replacement));
            Assert.That(_queue.SendQueue.Reader.TryRead(out _), Is.False);
        }
        else
        {
            Assert.That(await NextReply(), Is.EqualTo("replacement completed"));
            Assert.That(DialogManager.ContainsDialog(Key), Is.False);
        }
    }

    [Test]
    public async Task StoredAuthorization_ProbeAccepts_BindsWithoutDeviceFlow()
    {
        SeedAuthorization();
        if (game == "maimai") MockMusicData();
        _http.ForCallsTo(ProberUrl).RespondWithJson(new { });

        await StartBindingChoice();
        Assert.That(await NextReply(), Does.Contain("绑定成功"));
        AssertBindingCount(1);
        Assert.That(DialogManager.ContainsDialog(Key), Is.False);
        Assert.That(DevicePollCount(), Is.Zero);
        Assert.That(ProberCallCount(), Is.EqualTo(1), "验证应真实调用一次成绩接口");
    }

    [Test]
    public async Task RevokedOAuth_ProbeUnauthorized_FallsBackToDeviceCode()
    {
        SeedAuthorization();
        MockOnBehalfOfToken();
        MockDeviceCodeDenied();
        if (game == "maimai") MockMusicData();
        _http.ForCallsTo(ProberUrl).RespondWithJson(new { error = "invalid_token" }, 401);

        await StartBindingChoice();
        Assert.That(await NextReply(), Does.Contain("水鱼授权链接"));
        Assert.That(ProberCallCount(), Is.EqualTo(2), "401 后应丢票重拉一次再验");
        AssertGameBindingCount(0);
        Assert.That(DialogManager.ContainsDialog(Key), Is.True);
    }

    [TestCase(400)]
    [TestCase(403)]
    public async Task RejectedOAuth_ProbeError_FallsBackToDeviceCode(int status)
    {
        SeedAuthorization();
        MockDeviceCodeDenied();
        if (game == "maimai") MockMusicData();
        _http.ForCallsTo(ProberUrl).RespondWithJson(new { error = "rejected" }, status);

        await StartBindingChoice();
        Assert.That(await NextReply(), Does.Contain("水鱼授权链接"));
        Assert.That(ProberCallCount(), Is.EqualTo(1));
        AssertGameBindingCount(0);
    }

    [TestCase(429)]
    [TestCase(503)]
    public async Task ServerBusy_ProbeError_RepliesUnavailable(int status)
    {
        SeedAuthorization();
        if (game == "maimai") MockMusicData();
        _http.ForCallsTo(ProberUrl).RespondWithJson(new { error = "busy" }, status);

        await StartBindingChoice();
        Assert.That(await NextReply(), Does.StartWith("水鱼 OAuth 暂不可用"));
        AssertGameBindingCount(0);
        Assert.That(DialogManager.ContainsDialog(Key), Is.False);
    }

    private void MockAuthorization()
    {
        _http.ForCallsTo(TokenUrl).WithRequestBody("*device_code*").RespondWithJson(new
        {
            access_token = "test-access-token", token_type = "Bearer", expires_in = 3600,
            scope = DivingFishOAuth.ScopeOf(game), sub = "test-sub"
        });
    }

    private string ProberUrl => game == "maimai"
        ? "https://www.diving-fish.com/api/maimaidxprober/player/record"
        : "https://www.diving-fish.com/api/chunithmprober/player/records";

    /// <summary>预置一条已授权记录，模拟"该 QQ 之前已完成设备码授权"。</summary>
    private void SeedAuthorization(string sub = "test-sub", int expiresInSeconds = 3600)
    {
        DivingFishTokenStore.SaveAuthorization(_user, game, sub, new DivingFishToken
        {
            AccessToken = "stored-access-token",
            Scope = DivingFishOAuth.ScopeOf(game),
            ExpiresAt = DateTime.UtcNow.AddSeconds(expiresInSeconds)
        });
    }

    private void MockOnBehalfOfToken()
    {
        _http.ForCallsTo(TokenUrl).WithRequestBody("*on-behalf-of*").RespondWithJson(new
        {
            access_token = "obo-access-token", token_type = "Bearer", expires_in = 3600,
            scope = DivingFishOAuth.ScopeOf(game)
        });
    }

    private void MockDeviceCodeDenied()
    {
        _http.ForCallsTo(TokenUrl).WithRequestBody("*device_code*")
            .RespondWithJson(new { error = "access_denied" }, 400);
    }

    private void MockMusicData()
    {
        _http.ForCallsTo("https://www.diving-fish.com/api/maimaidxprober/music_data")
            .RespondWithJson(new[] { MaimaiSong() });
    }

    private static object MaimaiSong()
    {
        dynamic basicInfo = new ExpandoObject();
        basicInfo.title = "test";
        basicInfo.artist = "test";
        basicInfo.genre = "test";
        basicInfo.bpm = 100L;
        basicInfo.release_date = "2000-01-01";
        basicInfo.from = "maimai";
        basicInfo.is_new = false;

        dynamic chart = new ExpandoObject();
        chart.notes = new long[] { 63, 23, 8, 2 };

        dynamic song = new ExpandoObject();
        song.id = "8";
        song.title = "True Love Song";
        song.type = "SD";
        song.basic_info = basicInfo;
        song.ds = new[] { 5.0, 7.2, 10.2, 12.4 };
        song.level = new[] { "5", "7", "10", "12" };
        song.charts = new[] { chart, chart, chart, chart };
        return song;
    }

    private int ProberCallCount() => _http.CallLog.Count(c =>
        c.Request.Url.ToString().Contains("prober/player/record"));

    private async Task StartBindingChoice()
    {
        await Dispatch(command);
        Assert.That(await NextReply(), Does.Contain("请选择查分器"));
        await Dispatch("0");
    }

    private void AssertGameBindingCount(int count)
    {
        using var realm = BotDbContext.OpenRealm();
        Assert.That(game == "maimai"
            ? realm.All<MaiMaiDxBind>().Count(x => x.UId == _user)
            : realm.All<ChunithmBind>().Count(x => x.UId == _user), Is.EqualTo(count));
    }

    private async Task StartBinding()
    {
        // Flurl 的 HttpTest 对同一 URL+body 的多条规则取先注册者，OBO 默认拒绝必须在此注册，
        // 以便各测试可在其后注册自己的 OBO 响应
        _http.ForCallsTo(TokenUrl).WithRequestBody("*on-behalf-of*")
            .RespondWithJson(new { error = "consent_required" }, 400);
        await Dispatch(command);
        Assert.That(await NextReply(), Does.Contain("请选择查分器"));
        await Dispatch("0");
        Assert.That(await NextReply(), Does.Contain("水鱼授权链接"));
        Assert.That(DialogManager.ContainsDialog(Key), Is.True, "发链接后必须保留确认会话");
        Assert.That(_clock.ActiveTimerCount, Is.EqualTo(2), "确认超时与设备码轮询应都已启动");
    }

    private async Task Dispatch(string text, long? user = null, long? group = null)
    {
        var message = CreateMessage(text, user ?? _user, group ?? Group);
        foreach (var (plugin, method, routed) in _dispatcher.Dispatch(message))
        {
            if (await _dispatcher.Invoke(plugin, method, routed) == MarisaPluginTaskState.CompletedTask) break;
        }
    }

    private Message CreateMessage(string text, long user, long? group) => new(
        new MessageChain(new MessageDataId(1, 0), new MessageDataText(text)), _sender)
    {
        Sender = new SenderInfo(user, "test"),
        GroupInfo = group == null ? null : new GroupInfo(group.Value, "test", null),
        Type = group == null ? MessageType.FriendMessage : MessageType.GroupMessage
    };

    private async Task<string> NextReply() =>
        (await _queue.SendQueue.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5))).MessageChain.Text.ToString();

    private int DevicePollCount() => _http.CallLog.Count(c => c.RequestBody?.Contains("device_code") == true);

    private void AssertBindingCount(int count)
    {
        using var realm = BotDbContext.OpenRealm();
        Assert.That(realm.All<DivingFishAuthToken>().Count(x => x.Qq == _user), Is.EqualTo(count));
        Assert.That(game == "maimai"
            ? realm.All<MaiMaiDxBind>().Count(x => x.UId == _user)
            : realm.All<ChunithmBind>().Count(x => x.UId == _user), Is.EqualTo(count));
    }

    private sealed class ManualClock : TimeProvider
    {
        private readonly List<ManualTimer> _timers = [];
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public int ActiveTimerCount { get { lock (_timers) return _timers.Count; } }
        public override DateTimeOffset GetUtcNow() { lock (_timers) return _now; }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state);
            timer.Change(dueTime, period);
            return timer;
        }

        public void Advance(TimeSpan by)
        {
            List<ManualTimer> due;
            lock (_timers)
            {
                _now += by;
                due = _timers.Where(t => t.Due <= _now).OrderBy(t => t.Due).ToList();
            }
            foreach (var timer in due) timer.Fire();
        }

        private sealed class ManualTimer(ManualClock clock, TimerCallback callback, object? state) : ITimer
        {
            public DateTimeOffset Due { get; private set; }

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                Assert.That(period, Is.EqualTo(Timeout.InfiniteTimeSpan));
                lock (clock._timers)
                {
                    Due = dueTime == Timeout.InfiniteTimeSpan ? DateTimeOffset.MaxValue : clock._now + dueTime;
                    if (!clock._timers.Contains(this)) clock._timers.Add(this);
                    return true;
                }
            }

            public void Fire()
            {
                lock (clock._timers)
                {
                    if (!clock._timers.Remove(this)) return;
                }
                callback(state);
            }

            public void Dispose() { lock (clock._timers) clock._timers.Remove(this); }
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
