using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Marisa.BotDriver.DI.Message;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Entity.MessageSender;
using Marisa.Plugin.Shared.Util;
using NUnit.Framework;
using OsuPlugin = Marisa.Plugin.Osu.Osu;

namespace Marisa.Plugin.Test;

public class CommonExceptionHandlerTest
{
    [Test]
    public async Task Osu_ExceptionHandler_Should_Send_Public_Url_When_Web_Render_Fails()
    {
        var queue = new MessageQueueProvider();
        var sender = new MessageSenderProvider(queue);
        var message = new Message(new MessageChain(new MessageDataText("ping")), sender)
        {
            Type = MessageType.FriendMessage,
            Sender = new SenderInfo(114514, "tester")
        };

        const string privateUrl = "http://127.0.0.1:14311/osu/score?name=test";
        const string publicUrl = "http://public.example.com:14311/osu/score?name=test";

        await new OsuPlugin().ExceptionHandler(
            new TargetInvocationException(new WebRenderFailedException(privateUrl, publicUrl, new InvalidOperationException("boom"))),
            message
        );

        Assert.That(queue.SendQueue.Reader.TryRead(out var reply), Is.True);
        var text = ((MessageDataText)reply!.MessageChain.Messages.Single()).Text.ToString();

        Assert.That(text, Is.EqualTo(publicUrl));
    }
}