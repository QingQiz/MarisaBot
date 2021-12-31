using System.Threading.Tasks.Dataflow;
using QQBot.MiraiHttp.Entity;

namespace QQBot.MiraiHttp.DI;

public class MessageSenderProvider
{
    private readonly MessageQueueProvider _queue;

    public MessageSenderProvider(MessageQueueProvider queue)
    {
        _queue = queue;
    }

    private void Post(MessageChain message, MiraiMessageType type, long target, long? groupId, long? quote)
    {
        _queue.SendQueue.Post((message, type, target, groupId, quote));
    }

    /// <summary>
    /// 指定目标的纯文本消息
    /// </summary>
    public void Send(string text, MiraiMessageType type, long target, long? quote)
    {
        Post(MessageChain.FromPlainText(text), type, target, null, quote);
    }

    /// <summary>
    /// 指定目标的复杂消息
    /// </summary>
    public void Send(MessageChain message, MiraiMessageType type, long target, long? quote)
    {
        Post(message, type, target, null, quote);
    }

    /// <summary>
    /// 通过接收到的消息发送复杂消息到接收处
    /// </summary>
    public void Reply(MessageChain message, Message recv, bool quote=true)
    {
        Post(message, recv.Type, recv.Location, recv.GroupInfo!.Id, quote ? recv.Source.Id : null);
    }

    /// <summary>
    /// 通过接收到的消息发送纯文本消息到接收处
    /// </summary>
    public void Reply(string text, Message recv, bool quote = true) =>
        Reply(MessageChain.FromPlainText(text), recv, quote);

    public void SendByRecv(MessageChain message, Message recv, bool quote = true) => Reply(message, recv, quote);

    /// <summary>
    /// 指定目标的临时会话消息
    /// </summary>
    public void SendTempMessage(MessageChain message, long target, long? groupId, long? quote)
    {
        Post(message, MiraiMessageType.TempMessage, target, groupId, quote);
    }

    /// <summary>
    /// 回复群里的消息，但是通过临时消息的方法发出
    /// </summary>
    public void ReplyWithTempMessage(MessageChain message, Message recv, bool quote = true)
    {
        if (recv.Type != MiraiMessageType.GroupMessage)
        {
            throw new ArgumentException("临时消息只能从群消息发出");
        }

        Post(message, MiraiMessageType.TempMessage, recv.Sender!.Id, recv.GroupInfo!.Id, quote ? recv.Source.Id : null);
    }

    /// <summary>
    /// 回复群里的消息，但是通过临时消息的方法发出
    /// </summary>
    public void ReplyWithTempMessage(string text, Message recv, bool quote = true) =>
        ReplyWithTempMessage(MessageChain.FromPlainText(text), recv, quote);
}