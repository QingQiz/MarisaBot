using System.Threading.Tasks.Dataflow;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;

namespace Marisa.BotDriver.DI.Message;

public class MessageSenderProvider
{
    private readonly MessageQueueProvider _queue;

    public MessageSenderProvider(MessageQueueProvider queue)
    {
        _queue = queue;
    }

    private void Send(MessageToSend toSend)
    {
        _queue.SendQueue.Post(toSend);
    }

    /// <summary>
    /// 指定目标的复杂消息
    /// </summary>
    public void Send(MessageChain message, MessageType type, long target, long? quote)
    {
        Send(new MessageToSend(message, type, target, quote));
    }

    /// <summary>
    /// 指定目标的纯文本消息
    /// </summary>
    public void Send(string text, MessageType type, long target, long? quote)
    {
        Send(MessageChain.FromText(text), type, target, quote);
    }


    /// <summary>
    /// 通过接收到的消息发送复杂消息到接收处
    /// </summary>
    public void Reply(MessageChain message, Entity.Message.Message recv, bool quote = true)
    {
        Send(message, recv.Type, recv.Location, quote ? recv.MessageId.Id : null);
    }

    public void Reply(MessageData message, Entity.Message.Message recv, bool quote = true)
    {
        Reply(new MessageChain(message), recv, quote);
    }

    /// <summary>
    /// 通过接收到的消息发送纯文本消息到接收处
    /// </summary>
    public void Reply(string text, Entity.Message.Message recv, bool quote = true) =>
        Reply(MessageChain.FromText(text), recv, quote);

    /// <summary>
    /// 回复群里的消息，但是通过临时消息的方法发出
    /// </summary>
    public void ReplyWithTempMessage(MessageChain message, Entity.Message.Message recv, bool quote = true)
    {
        if (recv.Type != MessageType.GroupMessage)
        {
            throw new ArgumentException("临时消息只能从群消息发出");
        }

        Send(new MessageToSend(message, MessageType.TempMessage, recv.Sender!.Id, recv.GroupInfo?.Id,
            quote ? recv.MessageId.Id : null));
    }

    /// <summary>
    /// 回复群里的消息，但是通过临时消息的方法发出
    /// </summary>
    public void ReplyWithTempMessage(string text, Entity.Message.Message recv, bool quote = true) =>
        ReplyWithTempMessage(MessageChain.FromText(text), recv, quote);
}