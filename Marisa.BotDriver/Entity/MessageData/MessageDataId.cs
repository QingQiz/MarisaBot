namespace Marisa.BotDriver.Entity.MessageData;

public class MessageDataId : MessageData
{
    public readonly long Id;
    // ReSharper disable once MemberCanBePrivate.Global
    public readonly long Time;

    public MessageDataId(long id, long time)
    {
        Id   = id;
        Time = time;
    }

    public override MessageDataType Type => MessageDataType.Id;
}