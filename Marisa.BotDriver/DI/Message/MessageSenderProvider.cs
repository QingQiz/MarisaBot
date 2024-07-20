using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;

namespace Marisa.BotDriver.DI.Message;

public class MessageSenderProvider(MessageQueueProvider queue)
{
    private async Task Send(MessageToSend toSend)
    {
        await queue.SendQueue.Writer.WriteAsync(toSend);
    }

    /// <summary>
    ///     指定目标的复杂消息
    /// </summary>
    public async Task Send(MessageChain message, MessageType type, long target, long? quote)
    {
        await Send(new MessageToSend(message, type, target, quote));
    }

    /// <summary>
    ///     指定目标的纯文本消息
    /// </summary>
    public async Task Send(string text, MessageType type, long target, long? quote)
    {
        await Send(MessageChain.FromText(text), type, target, quote);
    }


    /// <summary>
    ///     通过接收到的消息发送复杂消息到接收处
    /// </summary>
    public async Task Reply(MessageChain message, Entity.Message.Message recv, bool quote = true)
    {
        await Send(message, recv.Type, recv.Location, quote ? recv.MessageId.Id : null);
    }

    public async Task Reply(MessageData message, Entity.Message.Message recv, bool quote = true)
    {
        await Reply(new MessageChain(message), recv, quote);
    }

    /// <summary>
    ///     通过接收到的消息发送纯文本消息到接收处
    /// </summary>
    public async Task Reply(string text, Entity.Message.Message recv, bool quote = true)
    {
        await Reply(MessageChain.FromText(text), recv, quote);
    }

    /// <summary>
    ///     回复群里的消息，但是通过临时消息的方法发出
    /// </summary>
    public async Task ReplyWithTempMessage(MessageChain message, Entity.Message.Message recv, bool quote = true)
    {
        if (recv.Type != MessageType.GroupMessage)
        {
            throw new ArgumentException("临时消息只能从群消息发出");
        }

        await Send(new MessageToSend(message, MessageType.TempMessage, recv.Sender.Id, recv.GroupInfo?.Id,
            quote ? recv.MessageId.Id : null));
    }

    /// <summary>
    ///     回复群里的消息，但是通过临时消息的方法发出
    /// </summary>
    public async Task ReplyWithTempMessage(string text, Entity.Message.Message recv, bool quote = true)
    {
        await ReplyWithTempMessage(MessageChain.FromText(text), recv, quote);
    }
}