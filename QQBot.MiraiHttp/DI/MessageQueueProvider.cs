using System.Threading.Tasks.Dataflow;
using QQBot.MiraiHttp.Entity;

namespace QQBot.MiraiHttp.DI;

using MessageRecvQueue = BufferBlock<Message>;
using MessageSendQueue = BufferBlock<(Message message, MiraiMessageType mType, long target, long? quote)>;

public class MessageQueueProvider
{
    public readonly MessageRecvQueue RecvQueue = new();
    public readonly MessageSendQueue SendQueue = new();
}
