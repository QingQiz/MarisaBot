namespace QQBot.MiraiHttp.Entity.MessageData;

public class QuoteMessage : MessageData
{
    public long Id;
    public long GroupId;
    public long SenderId;
    public long TargetId;
    public MessageChain Origin;

    public QuoteMessage(long id, long groupId, long senderId, long targetId, MessageChain origin)
    {
        Id       = id;
        GroupId  = groupId;
        SenderId = senderId;
        TargetId = targetId;
        Origin   = origin;
    }
}