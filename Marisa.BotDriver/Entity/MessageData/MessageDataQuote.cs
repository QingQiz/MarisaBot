using Marisa.BotDriver.Entity.Message;

namespace Marisa.BotDriver.Entity.MessageData;

public class MessageDataQuote : MessageData
{
    public long Id;
    public long GroupId;
    public long SenderId;
    public long TargetId;
    public MessageChain Origin;

    public MessageDataQuote(long id, long groupId, long senderId, long targetId, MessageChain origin)
    {
        Id       = id;
        GroupId  = groupId;
        SenderId = senderId;
        TargetId = targetId;
        Origin   = origin;
        Type     = MessageDataType.Quote;
    }
}