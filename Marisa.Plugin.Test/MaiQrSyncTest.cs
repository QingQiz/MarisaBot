using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Flurl.Http.Testing;
using Marisa.BotDriver.DI.Message;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Entity.MessageSender;
using Marisa.Plugin.Shared.MaiMaiDx;
using NUnit.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using ZXing;
using ZXing.Common;
using MaiMaiDxPlugin = Marisa.Plugin.MaiMaiDx.MaiMaiDx;

namespace Marisa.Plugin.Test;

[TestFixture]
public class MaiQrSyncTest
{
    private const long Qq = 1002;
    private const string Base = MaiScoreHubClient.BaseUrl;
    private const string Qr = "SGWCMAID261029120000" +
                              "0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";

    private static readonly ConcurrentDictionary<long, string> MshJwt = (ConcurrentDictionary<long, string>)
        typeof(MaiMaiDxPlugin).GetField("MshJwt", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

    private static readonly ConcurrentDictionary<long, byte> Syncing = (ConcurrentDictionary<long, byte>)
        typeof(MaiMaiDxPlugin).GetField("Syncing", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

    [TearDown]
    public void TearDown()
    {
        SpinWait.SpinUntil(() => !Syncing.ContainsKey(Qq), TimeSpan.FromSeconds(5));
        MshJwt.TryRemove(Qq, out _);
    }

    [TestCase("png")]
    [TestCase("screenshot")]
    [TestCase("transparent")]
    public async Task Image_WithoutLogin_LogsInWithDecodedQr(string kind)
    {
        using var http = new HttpTest();
        http.RespondWith(() => new ByteArrayContent(QrImage(Qr, kind)));
        http.RespondWithJson(new { kind = "fast", token = "new-jwt" }, 201);

        var replies = await Run("", MessageDataImage.FromUrl("https://gchat.qpic.cn/qr.png"));

        http.ShouldHaveCalled($"{Base}/auth/qr-login").WithRequestJson(new { qrCode = Qr });
        Assert.That(MshJwt[Qq], Is.EqualTo("new-jwt"));
        Assert.That(replies[^1], Does.StartWith("MSH 登录成功"));
    }

    [Test]
    public async Task AsyncLogin_PollsUntilMatched()
    {
        using var http = new HttpTest();
        http.RespondWithJson(new { kind = "async", attemptId = "attempt" }, 201);
        http.RespondWithJson(new { attemptId = "attempt", status = "adding_rival", token = (string?)null });
        http.RespondWith("Bad Gateway", 502);
        http.RespondWithJson(new { attemptId = "attempt", status = "matched", token = "new-jwt" });

        var replies = await Run(Qr);

        http.ShouldHaveCalled($"{Base}/auth/qr-login/attempt").Times(3);
        Assert.That(MshJwt[Qq], Is.EqualTo("new-jwt"));
        Assert.That(replies[^1], Does.StartWith("MSH 登录成功"));
    }

    [Test]
    public void AsyncLogin_Failed_ReportsReason()
    {
        using var http = new HttpTest();
        http.RespondWithJson(new { kind = "async", attemptId = "attempt" }, 201);
        http.RespondWithJson(new
        {
            attemptId = "attempt",
            status    = "failed",
            token     = (string?)null,
            error     = "未在超时时间内拿到可确认身份的好友列表快照"
        });

        var error = Assert.ThrowsAsync<InvalidOperationException>(async () => await Run(Qr));

        Assert.That(error!.Message, Is.EqualTo("未在超时时间内拿到可确认身份的好友列表快照"));
        Assert.That(MshJwt.ContainsKey(Qq), Is.False);
    }

    [Test]
    public async Task Tokens_WithLogin_SetTokensAndExport()
    {
        MshJwt[Qq] = "jwt";
        using var http = new HttpTest();
        http.RespondWithJson(new { });
        http.RespondWithJson(new { });
        http.RespondWithJson(new { hasLxnsImportToken = true, hasDivingFishImportToken = true });
        http.RespondWithJson(new { status = "completed", result = new { lxns = new { status = "success", exported = 10, scores = 10 } } });
        http.RespondWithJson(new { status = "completed", result = new { divingFish = new { status = "success", exported = 10, scores = 10 } } });

        var queue  = new MessageQueueProvider();
        var plugin = RuntimeHelpers.GetUninitializedObject(typeof(MaiMaiDxPlugin));
        var sync   = typeof(MaiMaiDxPlugin).GetMethod("Sync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)sync.Invoke(plugin, [CreateMessage(queue, "落雪 lxns-token 水鱼 df-token")])!;

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var replies = new List<string>();
        while (replies.Count < 2) replies.Add((await queue.SendQueue.Reader.ReadAsync(timeout.Token)).MessageChain.Text);

        http.ShouldHaveCalled($"{Base}/me").WithVerb(HttpMethod.Patch).WithRequestJson(new { lxnsImportToken = "lxns-token" });
        http.ShouldHaveCalled($"{Base}/me").WithVerb(HttpMethod.Patch).WithRequestJson(new { divingFishImportToken = "df-token" });
        http.ShouldNotHaveCalled($"{Base}/auth/login-requests");
        Assert.That(replies[1].ReplaceLineEndings("\n"), Is.EqualTo("同步完成：\n落雪 ✅ 导入 10/10 条\n水鱼 ✅ 导入 10/10 条"));
    }

    [Test]
    public async Task ReExport_ExpiredLogin_ClearsLogin()
    {
        MshJwt[Qq] = "stale-jwt";
        using var http = new HttpTest();
        http.RespondWithJson(new { statusCode = 401, message = "Unauthorized" }, 401);

        var queue    = new MessageQueueProvider();
        var reExport = typeof(MaiMaiDxPlugin).GetMethod("ReExport", BindingFlags.NonPublic | BindingFlags.Static)!;
        reExport.Invoke(null, [CreateMessage(queue, "")]);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var replies = new List<string>();
        while (replies.Count < 2) replies.Add((await queue.SendQueue.Reader.ReadAsync(timeout.Token)).MessageChain.Text);

        Assert.That(replies[1], Is.EqualTo("MSH 登录已失效，请重新发送指令。"));
        Assert.That(MshJwt.ContainsKey(Qq), Is.False);
    }

    [Test]
    public async Task ExpiredLogin_UsesSameQrToLogIn()
    {
        MshJwt[Qq] = "stale-jwt";
        using var http = new HttpTest();
        http.RespondWithJson(new { statusCode = 401, message = "Unauthorized" }, 401);
        http.RespondWithJson(new { kind = "fast", token = "new-jwt" }, 201);

        var replies = await Run(Qr);

        http.ShouldHaveCalled($"{Base}/auth/qr-login").WithRequestJson(new { qrCode = Qr }).Times(1);
        Assert.That(MshJwt[Qq], Is.EqualTo("new-jwt"));
        Assert.That(replies[^1], Does.StartWith("MSH 登录成功"));
    }

    [Test]
    public async Task CompletedJob_ExportsToConfiguredProbers()
    {
        MshJwt[Qq] = "jwt";
        using var http = new HttpTest();
        http.RespondWithJson(new { jobId = "job" }, 202);
        http.RespondWithJson(new { id = "job", status = "completed", error = (object?)null });
        http.RespondWithJson(new { hasLxnsImportToken = false, hasDivingFishImportToken = true });
        http.RespondWithJson(new
        {
            status = "completed",
            result = new { divingFish = new { status = "success", exported = 10, scores = 10 } }
        });

        var replies = await Run(Qr);

        http.ShouldHaveCalled($"{Base}/me/cabinet-score-jobs").WithVerb(HttpMethod.Post).WithRequestJson(new { qrCode = Qr });
        Assert.That(replies[^1], Is.EqualTo("同步完成：\n水鱼 ✅ 导入 10/10 条"));
    }

    [Test]
    public async Task MismatchedAccount_ClearsLogin()
    {
        MshJwt[Qq] = "jwt";
        using var http = new HttpTest();
        http.RespondWithJson(new { jobId = "job" }, 202);
        http.RespondWithJson(new
        {
            id = "job",
            status = "failed",
            error = new { code = "CABINET_USER_MISMATCH", message = "二维码与当前绑定账号不一致" }
        });

        var replies = await Run(Qr);

        Assert.That(MshJwt.ContainsKey(Qq), Is.False);
        Assert.That(replies[^1], Does.StartWith("同步失败：二维码与当前绑定账号不一致"));
    }

    private static async Task<List<string>> Run(string arg, MessageDataImage? image = null)
    {
        var queue     = new MessageQueueProvider();
        var runQrSync = typeof(MaiMaiDxPlugin).GetMethod("RunQrSync", BindingFlags.NonPublic | BindingFlags.Static)!;
        await (Task)runQrSync.Invoke(null, [CreateMessage(queue, "mai sync"), arg, image])!;

        var replies = new List<string>();
        while (queue.SendQueue.Reader.TryRead(out var sent)) replies.Add(sent.MessageChain.Text);
        return replies;
    }

    private static Message CreateMessage(MessageQueueProvider queue, string text) =>
        new(new MessageChain(new MessageDataId(1, 0), new MessageDataText(text)), new MessageSenderProvider(queue))
        {
            Type   = MessageType.FriendMessage,
            Sender = new SenderInfo(Qq, "tester")
        };

    private static byte[] QrImage(string text, string kind)
    {
        var data = new BarcodeWriterPixelData
        {
            Format  = BarcodeFormat.QR_CODE,
            Options = new EncodingOptions { Width = 360, Height = 360 }
        }.Write(text);

        using var qr     = Image.LoadPixelData<Bgra32>(data.Pixels, data.Width, data.Height);
        using var stream = new MemoryStream();
        switch (kind)
        {
            case "png":
                qr.SaveAsPng(stream);
                break;
            // 手机截图：二维码居中于大画布，JPEG 有损压缩
            case "screenshot":
                using (var screen = new Image<Rgba32>(1080, 2340, new Rgba32(242, 242, 242)))
                {
                    screen.Mutate(x => x.DrawImage(qr, new Point(360, 900), 1f));
                    screen.SaveAsJpeg(stream, new JpegEncoder { Quality = 70 });
                }
                break;
            // 网页保存的透明底二维码：透明像素的 RGB 为 0
            case "transparent":
                qr.ProcessPixelRows(rows =>
                {
                    for (var y = 0; y < rows.Height; y++)
                        foreach (ref var p in rows.GetRowSpan(y))
                            if (p.R == 255) p = new Bgra32(0, 0, 0, 0);
                });
                qr.SaveAsPng(stream);
                break;
        }

        return stream.ToArray();
    }
}
