namespace Marisa.BotDriver.Entity.MessageData;

public class MessageDataId : MessageData
{
    public long Id;
    public long Time;

    public MessageDataId(long id, long time)
    {
        Id   = id;
        Time = time;
        Type = MessageDataType.Id;
    }
}