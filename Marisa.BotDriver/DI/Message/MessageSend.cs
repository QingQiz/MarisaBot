using Marisa.BotDriver.Entity.Message;

namespace Marisa.BotDriver.DI.Message;

public class MessageToSend
{
    public MessageChain MessageChain;
    public MessageType Type;
    public long ReceiverId;
    public long? GroupId;
    public long? QuoteId;

    public MessageToSend(MessageChain messageChain, MessageType type, long receiverId, long? quoteId)
    {
        MessageChain = messageChain;
        Type         = type;
        ReceiverId   = receiverId;
        QuoteId      = quoteId;
    }

    public MessageToSend(MessageChain messageChain, MessageType type, long receiverId, long? groupId, long? quoteId) :
        this(messageChain, type, receiverId, quoteId)
    {
        GroupId = groupId;
    }
}