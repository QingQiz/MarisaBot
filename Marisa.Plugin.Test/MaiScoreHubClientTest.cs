using System;
using System.Reflection;
using System.Threading.Tasks;
using Flurl.Http.Testing;
using Marisa.BotDriver.DI.Message;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Entity.MessageSender;
using Marisa.Plugin.Shared.MaiMaiDx;
using NUnit.Framework;
using MaiMaiDxPlugin = Marisa.Plugin.MaiMaiDx.MaiMaiDx;

namespace Marisa.Plugin.Test;

[TestFixture]
public class MaiScoreHubClientTest
{
    private const string FriendCode = "000000000000000";
    private const string BotFriendCode = "999999999999999";

    [Test]
    public async Task LoginRequest_AssignedBotBeforeRequestSent_IsNotReportedAsSent()
    {
        using var http = new HttpTest();
        http.RespondWithJson(new
        {
            jobId = "login-job",
            job = new
            {
                status = "queued",
                stage = "send_request",
                botUserFriendCode = BotFriendCode,
                friendRequestSentAt = (string?)null,
                deadlineAt = "2026-08-28T04:40:00.000Z"
            }
        });

        var result = await new MaiScoreHubClient().LoginRequestAsync(FriendCode);

        Assert.Multiple(() =>
        {
            Assert.That(result.BotFriendCode, Is.EqualTo(BotFriendCode));
            Assert.That(result.FriendRequestSent, Is.False);
            Assert.That(result.DeadlineAt, Is.EqualTo(DateTimeOffset.Parse("2026-08-28T04:40:00.000Z")));
        });
    }

    [Test]
    public async Task LoginRequest_WaitingForAcceptance_IsReportedAsSent()
    {
        using var http = new HttpTest();
        http.RespondWithJson(new
        {
            jobId = "login-job",
            job = new
            {
                status = "processing",
                stage = "wait_acceptance",
                botUserFriendCode = BotFriendCode,
                friendRequestSentAt = (string?)null,
                deadlineAt = "2026-08-28T04:40:00.000Z"
            }
        });

        var result = await new MaiScoreHubClient().LoginRequestAsync(FriendCode);

        Assert.That(result.FriendRequestSent, Is.True);
    }

    [Test]
    public void LoginRequest_CapacityFailure_IsStructuredAndRetryable()
    {
        using var http = new HttpTest();
        http.RespondWithJson(new
        {
            statusCode = 400,
            code = "cabinet_bot_unavailable",
            message = "Bot friend capacity is exhausted"
        }, 400);

        var error = Assert.ThrowsAsync<MaiScoreHubApiException>(async () =>
            await new MaiScoreHubClient().LoginRequestAsync(FriendCode));

        Assert.Multiple(() =>
        {
            Assert.That(error!.StatusCode, Is.EqualTo(400));
            Assert.That(error.ErrorCode, Is.EqualTo("cabinet_bot_unavailable"));
            Assert.That(error.IsTransientLoginFailure, Is.True);
            Assert.That(error.Message, Is.EqualTo("MSH 当前没有可用的 Bot 账号"));
        });
    }

    [Test]
    public void LoginRequest_ValidationFailure_IsNotRetryable()
    {
        using var http = new HttpTest();
        http.RespondWithJson(new
        {
            statusCode = 400,
            message = "Validation failed"
        }, 400);

        var error = Assert.ThrowsAsync<MaiScoreHubApiException>(async () =>
            await new MaiScoreHubClient().LoginRequestAsync(FriendCode));

        Assert.Multiple(() =>
        {
            Assert.That(error!.IsTransientLoginFailure, Is.False);
            Assert.That(error.Message, Is.EqualTo("MSH 拒绝了登录请求（参数验证失败）"));
        });
    }

    [Test]
    public void LoginRequest_AssignmentBusy_IsRetryable()
    {
        using var http = new HttpTest();
        http.RespondWithJson(new
        {
            statusCode = 503,
            code = "bot_assignment_busy",
            message = "Bot assignment is busy; retry after 5 seconds"
        }, 503);

        var error = Assert.ThrowsAsync<MaiScoreHubApiException>(async () =>
            await new MaiScoreHubClient().LoginRequestAsync(FriendCode));

        Assert.Multiple(() =>
        {
            Assert.That(error!.StatusCode, Is.EqualTo(503));
            Assert.That(error.IsTransientLoginFailure, Is.True);
            Assert.That(error.Message, Is.EqualTo("MSH 正在分配 Bot 账号"));
        });
    }

    [Test]
    public void Sync_RetriesTransientLoginFailure()
    {
        using var http = new HttpTest();
        http.RespondWithJson(new
        {
            statusCode = 400,
            code = "cabinet_bot_unavailable",
            message = "Bot friend capacity is exhausted"
        }, 400);
        http.RespondWithJson(new
        {
            statusCode = 400,
            message = "Validation failed"
        }, 400);

        var queue = new MessageQueueProvider();
        var sender = new MessageSenderProvider(queue);
        var message = new Message(new MessageChain(new MessageDataId(1, 0), new MessageDataText("mai sync")), sender)
        {
            Type = MessageType.FriendMessage,
            Sender = new SenderInfo(1001, "tester")
        };
        var runSync = typeof(MaiMaiDxPlugin).GetMethod("RunSync", BindingFlags.NonPublic | BindingFlags.Static)!;
        var task = (Task)runSync.Invoke(null, [message, FriendCode, null])!;

        var error = Assert.ThrowsAsync<MaiScoreHubApiException>(async () => await task);

        Assert.That(error!.Message, Is.EqualTo("MSH 拒绝了登录请求（参数验证失败）"));
    }

    [Test]
    public async Task LoginStatus_DeadlineFailure_IsRecognized()
    {
        using var http = new HttpTest();
        http.RespondWithJson(new
        {
            status = "failed",
            job = new
            {
                status = "failed",
                stage = "send_request",
                error = "DXNet job deadline exceeded",
                deadlineAt = "2026-08-28T04:40:00.000Z"
            }
        });

        var result = await new MaiScoreHubClient().LoginStatusAsync("login-job");

        Assert.Multiple(() =>
        {
            Assert.That(result.DeadlineExceeded, Is.True);
            Assert.That(result.ErrorCode, Is.Null);
            Assert.That(result.Message, Is.EqualTo("DXNet job deadline exceeded"));
        });
    }

    [Test]
    public async Task CreateUpdateScoreJob_ParsesServerDeadline()
    {
        using var http = new HttpTest();
        http.RespondWithJson(new
        {
            jobId = "crawl-job",
            job = new
            {
                status = "queued",
                stage = "update_score",
                deadlineAt = "2026-08-28T04:55:00.000Z"
            }
        }, 201);

        var result = await new MaiScoreHubClient().CreateUpdateScoreJobAsync("jwt", "login-job");

        Assert.Multiple(() =>
        {
            Assert.That(result.JobId, Is.EqualTo("crawl-job"));
            Assert.That(result.DeadlineAt, Is.EqualTo(DateTimeOffset.Parse("2026-08-28T04:55:00.000Z")));
        });
    }

    [Test]
    public async Task DxNetJob_DeadlineFailure_IsRecognized()
    {
        using var http = new HttpTest();
        http.RespondWithJson(new
        {
            status = "failed",
            stage = "update_score",
            error = "DXNet job deadline exceeded",
            errorCode = "job_deadline_exceeded",
            deadlineAt = "2026-08-28T04:55:00.000Z"
        });

        var result = await new MaiScoreHubClient().GetJobAsync("jwt", "crawl-job");

        Assert.Multiple(() =>
        {
            Assert.That(result.DeadlineExceeded, Is.True);
            Assert.That(result.ErrorCode, Is.EqualTo("job_deadline_exceeded"));
            Assert.That(result.DeadlineAt, Is.EqualTo(DateTimeOffset.Parse("2026-08-28T04:55:00.000Z")));
        });
    }
}
