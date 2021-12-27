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
    
    public void Send(MessageChain message, MiraiMessageType type, long target, long? quote)
    {
        _queue.SendQueue.Post((message, type, target, quote));
    }

    public void Send(MessageChain message, Message recv, bool quote=true)
    {
        Send(message, recv.Type, recv.Location, quote ? recv.Source.Id : null);
    }

    public void Send(string text, Message recv, bool quote = true) =>
        Send(MessageChain.FromPlainText(text), recv, quote);

    public void SendByRecv(MessageChain message, Message recv, bool quote = true) => Send(message, recv, quote);

}