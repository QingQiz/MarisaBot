using System.Threading.Channels;
using Msg = Marisa.BotDriver.Entity.Message.Message;

namespace Marisa.BotDriver.DI.Message;


public class MessageQueueProvider
{
    public readonly Channel<Msg> RecvQueue = Channel.CreateUnbounded<Msg>();
    public readonly Channel<MessageToSend> SendQueue = Channel.CreateUnbounded<MessageToSend>();
}
