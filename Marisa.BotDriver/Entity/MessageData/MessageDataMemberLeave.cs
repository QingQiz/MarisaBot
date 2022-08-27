namespace Marisa.BotDriver.Entity.MessageData;

public class MessageDataMemberLeave : MessageData
{
    public override MessageDataType Type => MessageDataType.MemberLeave;

    public readonly long Id;
    public readonly string Name;
    public readonly long? Kicker;

    public MessageDataMemberLeave(long id, string name, long? kicker = null)
    {
        Id     = id;
        Name   = name;
        Kicker = kicker;
    }
}