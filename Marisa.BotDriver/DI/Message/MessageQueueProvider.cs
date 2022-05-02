using System.Threading.Tasks.Dataflow;

namespace Marisa.BotDriver.DI.Message;

public class MessageQueueProvider
{
    public readonly BufferBlock<Entity.Message.Message> RecvQueue = new();
    public readonly BufferBlock<MessageToSend> SendQueue = new();
}
