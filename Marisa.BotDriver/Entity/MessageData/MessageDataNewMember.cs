namespace Marisa.BotDriver.Entity.MessageData;

public class MessageDataNewMember : MessageData
{
    public long Id { get; }
    public long GroupId { get; }
    public long? InvitorId { get; }

    public MessageDataNewMember(long id, long groupId, long? invitorId)
    {
        Id        = id;
        GroupId   = groupId;
        InvitorId = invitorId;
        Type      = MessageDataType.NewMember;
    }
}